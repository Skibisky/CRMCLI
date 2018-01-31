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
			BaseMain(args);
		}

		protected override void SubMain() {
			try {
				Console.WriteLine("Decompiling Solution");
				var doc = new XmlDocument();

				foreach (var f in files) {
					Console.WriteLine("Loading " + f);
					if (!File.Exists(Path.ChangeExtension(f, ".backup.xml")))
						File.Copy(f, Path.ChangeExtension(f, ".backup.xml"));
					doc.Load(f);

					// pull things out
					ExtractEntities(doc);
					ExtractRoles(doc);
					ExtractWorkflows(doc);
					ExtractEntityRelationships(doc);
					ExtractWebResources(doc);
					ExtractConnectionRoles(doc);
					ExtractEntityMap(doc);
					ExtractReports(doc);

					// recompile the customizations.xml
					using (FileStream fs = new FileStream(f, FileMode.Create))
					using (XmlTextWriter wr = new XmlTextWriter(fs, Encoding.UTF8)) {
						XmlDocument tdoc = new XmlDocument();
						tdoc.LoadXml(doc.OuterXml);
						wr.Formatting = Formatting.Indented;
						tdoc.WriteContentTo(wr);
						wr.Flush();
						fs.Flush();
						Console.WriteLine("Wrote " + f + " to file");
					}
				}
			}
			catch (Exception ex) {
				Console.WriteLine(ex.GetType() + " during disassemble.\r\n" + ex.Message);
			}
			if (Debugger.IsAttached) {
				Console.WriteLine("Press anykey to continue.");
				Console.ReadKey();
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
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
					try {
						var name = e["Name"].InnerText;
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

		private static void ExtractRoles(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Roles");
			Console.WriteLine("XML has " + nodeCols.Count + " <Roles>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Role");
				Console.WriteLine("Roles has " + nodes.Count + " <Role>.");
				Directory.CreateDirectory("roles");
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
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

		private static void ExtractWorkflows(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Workflows");
			Console.WriteLine("XML has " + nodeCols.Count + " <Workflows>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Workflow");
				Console.WriteLine("Workflows has " + nodes.Count + " <Workflow>.");
				Directory.CreateDirectory("Workflows");
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
					try {
						var name = e.Attributes["Name"].Value.Replace("\"", "");
						var id = e.Attributes["WorkflowId"].Value;
						WriteElement(e, name, "Workflows/" + name + "_" + id + ".xml");
						var xlink = doc.CreateElement("WorkflowLink");
						xlink.InnerText = "Workflows/" + name + "_" + id + ".xml";
						xCol.RemoveChild(e);
						xCol.AppendChild(xlink);
					}
					catch (Exception ex) {
						Console.WriteLine(ex.GetType() + " during workflow disassemble.\r\n" + ex.Message);
					}
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
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
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

		private static void ExtractWebResources(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("WebResources");
			Console.WriteLine("XML has " + nodeCols.Count + " <WebResources>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("WebResource");
				Console.WriteLine("WebResources has " + nodes.Count + " <WebResource>.");
				Directory.CreateDirectory("WebResources");
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
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

		private static void ExtractEntityMap(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("EntityMaps");
			Console.WriteLine("XML has " + nodeCols.Count + " <EntityMaps>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				try {
					WriteElement(xCol, "EntityMap", "EntityMap.xml");
					var xlink = doc.CreateElement("EntityMapLink");
					xlink.InnerText = "EntityMap.xml";
					doc.FirstChild.RemoveChild(xCol);
					doc.FirstChild.AppendChild(xlink);
				}
				catch (Exception ex) {
					Console.WriteLine(ex.GetType() + " during EntityMaps disassemble.\r\n" + ex.Message);
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
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
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

		private static void ExtractReports(XmlDocument doc) {
			var nodeCols = doc.GetElementsByTagName("Reports");
			Console.WriteLine("XML has " + nodeCols.Count + " <Reports>, 1 would be nice.");
			if (nodeCols.Count > 0) {
				var xCol = (XmlElement)nodeCols.Item(0);
				var nodes = xCol.GetElementsByTagName("Report");
				Console.WriteLine("Reports has " + nodes.Count + " <Report>.");
				Directory.CreateDirectory("Reports");
				int i = nodes.Cast<XmlNode>().Count();
				for (; i > 0;) {
					i--;
					var e = nodes.Cast<XmlNode>().ElementAt(i);
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

		private static void WriteElement(XmlNode e, string name, string path) {
			using (FileStream fs = new FileStream(path, FileMode.Create))
			using (XmlTextWriter wr = new XmlTextWriter(fs, Encoding.UTF8)) {
				XmlDocument tdoc = new XmlDocument();
				tdoc.LoadXml(e.OuterXml);
				wr.Formatting = Formatting.Indented;
				tdoc.WriteContentTo(wr);
				wr.Flush();
				fs.Flush();
				Console.WriteLine("Wrote " + name + " to file");
			}
		}

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				{ "decompile", (a) => { return GetFiles(a); } }
			};
		}
	}
}
