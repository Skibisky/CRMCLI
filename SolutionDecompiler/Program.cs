using CEC.Extensions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.IO;
using System.Xml;

namespace SolutionDecompiler {
	class Program : ProgramBase {

		static void Main(string[] args) {
			autoConnect = false;
			BaseMain(args);
		}

		bool? direction = null;

		static bool explodeEntities = false;

		public static readonly Encoding OutEncoding = Encoding.UTF8;

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				{ "decompile", (a) => { direction = false; return GetFiles(a); } },
				{ "compile", (a) => { direction = true; return GetFiles(a); } },
				{ "explode", (a) => { explodeEntities = true; return 0; } },
			};
		}

		protected override void SubMain() {
			try {
				var doc = new XmlDocument();

				if (direction == null) {
					Console.WriteLine("Please use decompile or compile.");
				}
				else if (direction.Value) {
					foreach (var f in files) {
						Console.WriteLine("Loading " + f);
						doc.Load(Path.ChangeExtension(f, ".out.xml"));

						PackEntities(doc);
						PackRoles(doc);
						PackWorkflows(doc);
						PackConnectionRoles(doc);
						PackEntityMap(doc);
						PackEntityRelationships(doc);
						PackReports(doc);
						PackWebResources(doc);

						// recompile the customizations.xml
						WriteDoc(doc, Path.ChangeExtension(f, ".in.xml"));
						SlamDoc(Path.ChangeExtension(f, ".in.xml"));
					}
				}
				else {
					Console.WriteLine("Decompiling Solutions");

					foreach (var f in files) {
						Console.WriteLine("Loading " + f);
						if (!File.Exists(Path.ChangeExtension(f, ".backup.xml")))
							File.Copy(f, Path.ChangeExtension(f, ".backup.xml"));
						doc.Load(f);

						// pull things out
						ExtractEntities(doc);
						ExtractRoles(doc);
						ExtractWorkflows(doc);
						ExtractConnectionRoles(doc);
						ExtractEntityMap(doc);
						ExtractEntityRelationships(doc);
						ExtractReports(doc);
						ExtractWebResources(doc);

						// recompile the customizations.xml
						WriteDoc(doc, Path.ChangeExtension(f, ".out.xml"));
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine(ex.GetType() + " during disassemble.\r\n" + ex.Message);
			}
			finally {
				if (Debugger.IsAttached) {
					Console.WriteLine("Press anykey to continue.");
					Console.ReadKey();
				}
			}
		}

		private static void WriteDoc(XmlDocument doc, string f) {
			using (FileStream fs = new FileStream(f, FileMode.Create))
			using (XmlTextWriter wr = new XmlTextWriter(fs, OutEncoding)) {
				XmlDocument tdoc = new XmlDocument();
				tdoc.LoadXml(doc.OuterXml);
				wr.Formatting = Formatting.Indented;
				tdoc.WriteContentTo(wr);
				wr.Flush();
				fs.Flush();
				Console.WriteLine("Wrote " + f + " to file");
			}
		}

		private static void SlamDoc(string f) {
			var lines = File.ReadAllLines(f);
			var outlines = new List<string>();
			for (int i = 0; i < lines.Length - 1; i++) {
				var l = lines[i];
				var t = l.Trim();
				var tsind = t.IndexOf(' ');
				var tt = tsind == -1 ? t : t.Substring(0, tsind);
				var nl = lines[i + 1];
				var nt = nl.Trim();
				if ("/" + tt.Replace("<", "").Replace(">", "") == nt.Replace("<", "").Replace(">", "")) {
					outlines.Add(l + nt);
					i++;
				}
				else {
					outlines.Add(l);
				}
			}
			outlines.Add(lines.Last());
			File.WriteAllText(Path.ChangeExtension(f, ".test.xml"), string.Join("\r\n", outlines.ToArray()));
		}

		private static XmlNode ReadElement(XmlNode parent, string path) {
			var doc = (parent as XmlDocument) ?? parent.OwnerDocument;

			using (FileStream fs = new FileStream(path, FileMode.Open))
			using (XmlReader rd = XmlTextReader.Create(fs)) {
				var qw = rd.Read();
				var on = doc.CreateNode(XmlNodeType.Element, rd.Name, rd.NamespaceURI);
				on.InnerXml = rd.ReadOuterXml();
				var n = doc.CreateNode(XmlNodeType.Element, on.Name, rd.NamespaceURI);
				n.InnerXml = on.FirstChild.InnerXml;
				var sz = on.FirstChild.Attributes.Count;
				for (int i = 0; i < sz; i++) {
					n.Attributes.Append(on.FirstChild.Attributes[0]);
				}
				parent.AppendChild(n);
				Console.WriteLine("Read " + path + " to xml.");
				return n;
			}
		}

		private static void WriteElement(XmlNode e, string name, string path) {
			/*var d = Path.GetDirectoryName(path);
			Console.WriteLine(path + ": " + d);
			Directory.CreateDirectory(d);*/
			using (FileStream fs = new FileStream(path, FileMode.Create))
			using (XmlTextWriter wr = new XmlTextWriter(fs, OutEncoding)) {
				XmlDocument tdoc = new XmlDocument();
				tdoc.LoadXml(e.OuterXml);
				wr.Formatting = Formatting.Indented;
				tdoc.WriteContentTo(wr);
				wr.Flush();
				fs.Flush();
				Console.WriteLine("Wrote " + name + " to file");
			}
		}

		private static void ExtractEntities(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Entities");
			Console.WriteLine("XML has " + nodeCols.Count + " <Entities>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Entity");
				Console.WriteLine("Entities has " + nodes.Count + " <Entity>.");
				Directory.CreateDirectory("entities");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e["Name"].InnerText;
						if (explodeEntities)
							ExtractEntity(e as XmlElement);
						WriteElement(e, name, "entities/" + name + ".xml");
						var xlink = doc.CreateElement("EntityLink");
						xlink.InnerText = "entities/" + name + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during entity disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void ExtractEntity(XmlElement entNode) {
			var dir = new DirectoryStack("entities");
			var name = entNode["Name"].InnerText;
			dir.Push(name);
			Directory.CreateDirectory(dir);
			var xCol = (XmlElement)entNode.GetElementsByTagName("attributes")[0];
			if (xCol != null) {
				var nodes = xCol.GetElementsByTagName("attribute").Cast<XmlElement>();
				dir.Push("attributes");
				Directory.CreateDirectory(dir);
				while (nodes.Any()) {
					var e = nodes.ElementAt(0);
					var attr = e.GetAttribute("PhysicalName");
					WriteElement(e, attr, dir + "/" + attr + ".xml");
					var xlink = entNode.OwnerDocument.CreateElement("AttributeLink");
					xlink.InnerText = dir + "/" + attr + ".xml";
					xCol.RemoveChild(e);
					xCol.AppendChild(xlink);
				}
				dir.Pop();
			}

			PullNodeFromEntity("forms", "systemform", entNode, dir);
			PullNodeFromEntity("savedqueries", "savedquery", entNode, dir);
			PullNodeFromEntity("Visualizations", "visualization", entNode, dir);
		}

		private static void PullNodeFromEntity(string outernode, string innernodes, XmlElement entNode, DirectoryStack dir) {
			var tdir = outernode;
			foreach (var xCol in entNode.GetElementsByTagName(outernode).Cast<XmlElement>()) { 
				var nodes = xCol.GetElementsByTagName(innernodes).Cast<XmlElement>();
				if (outernode == "forms") {
					tdir = outernode + "_" + xCol.GetAttribute("type");
				}
				dir.Push(tdir);
				Directory.CreateDirectory(dir);
				while (nodes.Any()) {
					var e = nodes.ElementAt(0);
					var query = (e.GetElementsByTagName("LocalizedName")[0] as XmlElement).GetAttribute("description");
					query = new string(query.Where(c => !Path.GetInvalidPathChars().Contains(c) && !"?:\\/".Contains(c)).ToArray());
					WriteElement(e, query, dir + "/" + query + ".xml");
					var xlink = entNode.OwnerDocument.CreateElement(innernodes + "Link");
					xlink.InnerText = dir + "/" + query + ".xml";
					xCol.RemoveChild(e);
					xCol.AppendChild(xlink);
				}
				dir.Pop();
			}
		}

		private static void PackEntities(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Entities");
			Console.WriteLine("XML has " + nodeCols.Count + " <Entities>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("EntityLink");
				Console.WriteLine("Entities has " + nodes.Count + " <EntityLink>.");
				//Directory.CreateDirectory("entities");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during entity assemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void ExtractRoles(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Roles");
			Console.WriteLine("XML has " + nodeCols.Count + " <Roles>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Role");
				Console.WriteLine("Roles has " + nodes.Count + " <Role>.");
				Directory.CreateDirectory("roles");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.Attributes["name"].Value;
						var id = e.Attributes["id"].Value;
						WriteElement(e, name, "roles/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("RoleLink");
						xlink.InnerText = "roles/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during role disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackRoles(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Roles");
			Console.WriteLine("XML has " + nodeCols.Count + " <Roles>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("RoleLink");
				Console.WriteLine("Roles has " + nodes.Count + " <RoleLink>.");
				//Directory.CreateDirectory("roles");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during role assemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static string killChars = "\\\"?/<>:";

		private static void ExtractWorkflows(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Workflows");
			Console.WriteLine("XML has " + nodeCols.Count + " <Workflows>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Workflow");
				Console.WriteLine("Workflows has " + nodes.Count + " <Workflow>.");
				Directory.CreateDirectory("Workflows");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					string id = "";
					string name = "";
					try {
						name = e.Attributes["Name"].Value;
						foreach (var c in killChars) {
							name = name.Replace("" + c, "");
						}
						id = e.Attributes["WorkflowId"].Value;
						WriteElement(e, name, "Workflows/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("WorkflowLink");
						xlink.InnerText = "Workflows/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during workflow " + "Workflows/" + name + "_" + id + ".xml" + " disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackWorkflows(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Workflows");
			Console.WriteLine("XML has " + nodeCols.Count + " <Workflows>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("WorkflowLink");
				Console.WriteLine("Workflows has " + nodes.Count + " <WorkflowLink>.");
				//Directory.CreateDirectory("roles");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during workflow assemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void ExtractConnectionRoles(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("ConnectionRoles");
			Console.WriteLine("XML has " + nodeCols.Count + " <ConnectionRoles>, 2 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(1);
				var nodes = xCol.GetElementsByTagName("ConnectionRole");
				Console.WriteLine("ConnectionRoles has " + nodes.Count + " <ConnectionRole>.");
				Directory.CreateDirectory("ConnectionRoles");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e["name"].InnerText.Replace("/", "_").Replace("\\", "");
						var id = e["connectionroleid"].InnerText;
						WriteElement(e, name, "ConnectionRoles/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("ConnectionRoleLink");
						xlink.InnerText = "ConnectionRoles/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during ConnectionRole disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackConnectionRoles(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("ConnectionRoles");
			Console.WriteLine("XML has " + nodeCols.Count + " <ConnectionRoles>, 2 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(1);
				var nodes = xCol.GetElementsByTagName("ConnectionRoleLink");
				Console.WriteLine("ConnectionRoles has " + nodes.Count + " <ConnectionRoleLink>.");
				//Directory.CreateDirectory("ConnectionRoles");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during ConnectionRole disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void ExtractEntityMap(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("EntityMaps");
			Console.WriteLine("XML has " + nodeCols.Count + " <EntityMaps>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				try {
					WriteElement(xCol, "EntityMap", "EntityMap.xml");
					var xlink = doc.CreateElement("EntityMapLink");
					var xafter = doc.GetElementsByTagName("Templates")[0];
					xlink.InnerText = "EntityMap.xml";
					doc.FirstChild.RemoveChild(xCol);
					doc.FirstChild.InsertAfter(xlink, xafter);
				}
				catch (Exception ex) {
					Console.WriteLine(ex.GetType() + " during EntityMaps disassemble.\r\n" + ex.Message);
				}
			}
		}

		private static void PackEntityMap(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("EntityMapLink");
			Console.WriteLine("XML has " + nodeCols.Count + " <EntityMapLink>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				try {
					var xlink = doc.GetElementsByTagName("EntityMapLink")[0];
					var n = ReadElement(doc.FirstChild, xlink.InnerText);
					//var xafter = doc.GetElementsByTagName("Templates")[0];
					doc.FirstChild.InsertAfter(n, xlink);
					doc.FirstChild.RemoveChild(xlink);
				}
				catch (Exception ex) {
					Console.WriteLine(ex.GetType() + " during EntityMaps assemble.\r\n" + ex.Message);
				}
			}
		}

		private static void ExtractEntityRelationships(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("EntityRelationships");
			Console.WriteLine("XML has " + nodeCols.Count + " <EntityRelationships>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("EntityRelationship");
				Console.WriteLine("EntityRelationships has " + nodes.Count + " <EntityRelationship>.");
				Directory.CreateDirectory("EntityRelationships");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.Attributes["Name"].Value;
						WriteElement(e, name, "EntityRelationships/" + name + ".xml");
						var xlink = doc.CreateElement("EntityRelationshipLink");
						xlink.InnerText = "EntityRelationships/" + name + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during entityRel disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackEntityRelationships(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("EntityRelationships");
			Console.WriteLine("XML has " + nodeCols.Count + " <EntityRelationships>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("EntityRelationshipLink");
				Console.WriteLine("EntityRelationships has " + nodes.Count + " <EntityRelationshipLink>.");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during entityRel assemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void ExtractReports(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Reports");
			Console.WriteLine("XML has " + nodeCols.Count + " <Reports>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Report");
				Console.WriteLine("Reports has " + nodes.Count + " <Report>.");
				Directory.CreateDirectory("Reports");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e["name"].InnerText.Replace("/", "_").Replace("\\", "");
						var id = e["reportid"].InnerText;
						WriteElement(e, name, "Reports/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("ReportLink");
						xlink.InnerText = "Reports/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during Reports disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackReports(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Reports");
			Console.WriteLine("XML has " + nodeCols.Count + " <Reports>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("ReportLink");
				Console.WriteLine("Reports has " + nodes.Count + " <ReportLink>.");
				var invalidP = xCol.GetElementsByTagName("ReportLinks").Item(0);
				int i = 0;
				XmlNode lastNode = null;
				while (nodes.Cast<XmlNode>().Count() > i) {
					var e = nodes.Cast<XmlNode>().ElementAt(i);
					try {
						if (!string.IsNullOrWhiteSpace(e.InnerText) && File.Exists(e.InnerText)) {
							var name = e.InnerText;
							lastNode = ReadElement(xCol, name);
							xCol.RemoveChild(e);
						}
						else {
							i++;
						}
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during Reports assemble.\r\n" + ex.Message);
					}
				}
				xCol.InsertAfter(invalidP, lastNode);

			}
		}


		private static void ExtractWebResources(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("WebResources");
			Console.WriteLine("XML has " + nodeCols.Count + " <WebResources>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("WebResource");
				Console.WriteLine("WebResources has " + nodes.Count + " <WebResource>.");
				Directory.CreateDirectory("WebResources");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e["Name"].InnerText;
						var id = e["WebResourceId"].InnerText;
						Directory.CreateDirectory(new DirectoryInfo("WebResources/" + name).Parent.FullName);
						WriteElement(e, name, "WebResources/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("WebResourceLink");
						xlink.InnerText = "WebResources/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during WebResource disassemble.\r\n" + ex.Message);
					}
				}
			}
		}

		private static void PackWebResources(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("WebResources");
			Console.WriteLine("XML has " + nodeCols.Count + " <WebResources>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("WebResourceLink");
				Console.WriteLine("WebResources has " + nodes.Count + " <WebResourceLink>.");
				while (nodes.Cast<XmlNode>().Any()) {
					var e = nodes.Cast<XmlNode>().ElementAt(0);
					try {
						var name = e.InnerText;
						ReadElement(xCol, name);
						xCol.RemoveChild(e);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during WebResource assemble.\r\n" + ex.Message);
					}
				}
			}
		}

	}
}
