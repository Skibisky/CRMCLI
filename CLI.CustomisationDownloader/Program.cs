using CEC.Extensions;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace CEC.CustomisationDownloader.CLI {
	class Program : ProgramBase {
		static bool doUpload = false;
		static bool doDownload = false;
		protected static readonly HashSet<string> types = new HashSet<string>();

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				{ "target", (a) => { return GetTypes(a); }},
				{ "download", (a) => { doDownload = true; return GetFiles(a); }},
				{ "upload", (a) => { doUpload = true; return GetFiles(a); }},
				{"org", (a) => {return ConnectArgs(a);}},
			};
		}

		static IOrganizationService orgServ { get; set; }

		protected static int GetTypes(string[] args) {
			int i = 0;
			while (i < args.Length && args[i][0] != '-') {
				types.Add(args[i]);
				Console.WriteLine("Adding type: " + args[i]);
				i++;
			}
			return i;
		}

		static void Help() {
			Console.WriteLine(@"Usage:
CustomisationDownloader [-h] [-o URI [user pass]] [-t type ...] -d | -u [file ...]
	-h Help: Display this help
	-o Org: URI [user pass]
		Connects to the CRM specified by URI with user pass.
		If user pass isn't specified, will use Default Network Creds.
	-t Target: What type of things to download (see -t ?)
	-d Download: Creates copies of files depending on action
	-u Upload: Uploads the specified templates
");
		}

		static void HelpTypes() {
			Console.WriteLine(@"Types:
		report
		javascript
");
		}

		static void Main(string[] args) {
			var splash = "CRM Customisation Downloader prelease-" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Console.WriteLine(new string('=', splash.Length));
			Console.WriteLine(splash);
			Console.WriteLine(new string('-', splash.Length));
			Console.WriteLine("From " + Environment.CurrentDirectory);

			if (args.Length == 0) {
				Console.WriteLine("Enter arguments:");
				var comms = Console.ReadLine();
				args = ExtensionMethods.SplitCommandLine(comms).ToArray();
			}
			if (args.Length == 0) {
				Help();
				return;
			}

			ParseArgs(typeof(Program), args);

			if (types.Contains("?")) {
				HelpTypes();
				return;
			}

			if (files.Count > 0 && !doUpload && !doDownload) {
				Console.WriteLine("Do what with " + files.Count + " files?");
				var comms = Console.ReadLine();
				args = ExtensionMethods.SplitCommandLine(comms).ToArray();
				ParseArgs(args);
			}

			BaseMain(args);
		}

		class DownloadTarget {
			public string Name;
			public string Filename;
			public string Data;
			public QueryExpression Query;
			public Func<string, Entity, string> Process = null;
			public string FileExten;
		}

		private Dictionary<string, DownloadTarget> typeQueries = new Dictionary<string, DownloadTarget>() {
			{ "reports", new DownloadTarget() {
				Name = "name",
				Filename = "filename",
				Data = "name",
				Query = new QueryExpression("report") {
						ColumnSet = new ColumnSet("name", "filename"),
						//ColumnSet = new ColumnSet(true),
					},
				Process = ReportDownload
				}
			},
			{ "javascript", new DownloadTarget() {
				Name = "displayname",
				Filename = "name",
				Data = "content",
				FileExten = "js",
				Query = new QueryExpression("webresource") {
						ColumnSet = new ColumnSet("displayname", "name", "content"),
						Criteria = new FilterExpression(LogicalOperator.And) {
							Conditions = {
								new ConditionExpression("webresourcetype", ConditionOperator.Equal, 3),
								new ConditionExpression("iscustomizable", ConditionOperator.Equal, true),
							}
						}
					},
				Process = (d, e) => Encoding.Default.GetString(Convert.FromBase64String(d)),
				}
			},
			{ "importmaps", new DownloadTarget() {
				Name = "name",
				Filename = "name",
				FileExten = "xml",
				Data = "name",
				Query = new QueryExpression("importmap") {
						ColumnSet = new ColumnSet("name"),
						//ColumnSet = new ColumnSet(true),
					},
				Process = ImportMapDownload
				}
			},
		};

		protected override void SubMain() {
			orgServ = OrgService;

			foreach (var t in types) {
				try {

					DownloadTarget downTarg = null;
					if (typeQueries.ContainsKey(t.ToLower()))
						downTarg = typeQueries[t.ToLower()];
					else if (typeQueries.ContainsKey(t.ToLower() + "s"))
						downTarg = typeQueries[t.ToLower() + "s"];

					if (downTarg == null) {
						Console.WriteLine("No ability to download " + t);
						Console.WriteLine("---------");
						continue;
					}

					Console.WriteLine("Downloading " + t);

					downTarg.Query.PageInfo = new PagingInfo() {
						Count = 50,
						PageNumber = 1,
					};
					var entList = new List<Entity>();

					EntityCollection entRes = null;
					while (true) {
						entRes = OrgService.RetrieveMultiple(downTarg.Query);
						foreach (var res in entRes.Entities) {
							if (!files.Any() || files.Contains(res.GetAttributeValue<string>(downTarg.Name)))
								entList.Add(res);
						}
						Console.WriteLine("Currently " + entList.Count + " records in queue...");

						if (entRes.MoreRecords) {
							downTarg.Query.PageInfo.PageNumber++;
							downTarg.Query.PageInfo.PagingCookie = entRes.PagingCookie;
						}
						else {
							break;
						}
					}


					System.IO.Directory.CreateDirectory(t);
					foreach (var r in entList) {
						if (r.Attributes.ContainsKey(downTarg.Filename) && r.Attributes.ContainsKey(downTarg.Data)) {
							var data = r.GetAttributeValue<string>(downTarg.Data);
							if (downTarg.Process != null)
								data = downTarg.Process(data, r);
							if (data == null)
								continue;

							var fname = r.GetAttributeValue<string>(downTarg.Filename);
							fname = fname.Replace("/", "_");
							fname = fname.Replace("\\", "_");
							if (!string.IsNullOrWhiteSpace(downTarg.FileExten) && new FileInfo(fname).Extension != "." + downTarg.FileExten) {
								fname = Path.ChangeExtension(fname, downTarg.FileExten);
							}

							File.WriteAllText(t + "/" + fname, data);
							Console.WriteLine("Downloaded " + r.GetAttributeValue<string>(downTarg.Filename));
						}
						else {
							Console.WriteLine("Failed " + r.GetAttributeValue<string>(downTarg.Name));
						}
					}

				}
				catch {

				}
			}

			if (System.Diagnostics.Debugger.IsAttached) {
				Console.WriteLine("Press anykey to exit...");
				Console.ReadKey();
			}
		}

		static string XmlPretty (string xml, Entity e) {
			var stringBuilder = new StringBuilder();

			var element = XElement.Parse(xml);

			var settings = new XmlWriterSettings();
			settings.OmitXmlDeclaration = false;
			settings.Indent = true;
			settings.Encoding = new UTF8Encoding(false);
			settings.NewLineOnAttributes = false;

			var outstr = "";// stringBuilder.ToString();
			var ms = new MemoryStream();
			using (var xmlWriter = XmlWriter.Create(ms, settings)) {
				element.Save(xmlWriter);
			}

			using (var sr = new StreamReader(ms)) {
				ms.Position = 0;
				outstr = sr.ReadToEnd();
			}

			return outstr;
		}

		static string ImportMapDownload(string xml, Entity e) {
			ExportMappingsImportMapResponse resp = null;
			try {
				var req = new ExportMappingsImportMapRequest() {
					ImportMapId = e.Id,
				};

				resp = orgServ.Execute(req) as ExportMappingsImportMapResponse;
			}
			catch (Exception ex) {
				Console.WriteLine("ImportMap not supported " + ex.Message);
				return null;
			}

			return XmlPretty(resp.MappingsXml, null);
		}

		static string ReportDownload(string xml, Entity e) {
			DownloadReportDefinitionResponse resp = null;
			try {
				var req = new DownloadReportDefinitionRequest() {
					ReportId = e.Id,
				};

				resp = orgServ.Execute(req) as DownloadReportDefinitionResponse;
			}
			catch (Exception ex) {
				Console.WriteLine("Report not supported " + ex.Message);
				return null;
			}

			return XmlPretty(resp.BodyText, null);
		}
	}
}
