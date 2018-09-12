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
using IniParser.Model;

[assembly: CEC.Extensions.CecType(typeof(CEC.CustomisationDownloader.CLI.CustomisationDownloader))]
namespace CEC.CustomisationDownloader.CLI {
	public class CustomisationDownloader : ProgramBase {
		static bool doUpload = false;
		static bool doDownload = false;
		protected static readonly HashSet<string> types = new HashSet<string>();

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				{ "target", (a) => { return GetTypes(a); }},
				{ "download", (a) => { doDownload = true; return GetFiles(a); }},
				{ "upload", (a) => { doUpload = true; return GetFiles(a); }},
				{ "org", (a) => {return ConnectArgs(a);}},
				{ "set", set },
			};
		}

		public override string ShortName { get { return StaticName; } }
		// ugh
		private const string StaticName = "custdl";

		public override string FullName { get { return "Customisation Downloader"; } }

		private static IniData custdlConfig;

		protected static int GetTypes(string[] args) {
			int i = 0;
			while (i < args.Length && args[i][0] != '-') {
				types.Add(args[i]);
				Console.WriteLine("Adding type: " + args[i]);
				i++;
			}
			return i;
		}

		public override void Help() {
			Console.WriteLine(@"Usage:
CustomisationDownloader [-h] [-set] [-o URI [user pass]] [-t type ...] -d | -u [file ...]
	-h Help: Display this help
	-s Set: change a custdl setting
	-o Org: URI [user pass]
		Connects to the CRM specified by URI with user pass.
		If user pass isn't specified, will use Default Network Creds.
	-t Target: What type of things to download (see -t ?)
	-d Download: Creates copies of files depending on action
	-u Upload: Uploads the specified templates
");
		}

		public static void HelpSet(string item) {
			switch (item) {
				case "":
					Console.WriteLine("set: will try to change a config setting for custdl");
					foreach (var s in settable) {
						Console.WriteLine(s);
					}
					break;
				case "map":
					Console.WriteLine("map: will remap the output directory for a target.");
					Console.WriteLine("Required Arguments: <Target> <Relative Path>");
					Console.WriteLine("Try 'custdl -t ?' for Target types");
					break;
				default:
					Console.WriteLine("No help for: " + item);
					break;
			}
		}

		static string[] settable = new string[] { "map" };

		protected static int set(string[] args) {
			try {
				if (args.Length <= 1) {
					HelpSet("");
					return 0;
				}
				if (settable.Contains(args[1])) {
					var setThis = args[1];
					if (args.Length == 2) {
						switch (setThis) {
							// TODO: insert 1 arg sized sets
							default:
								HelpSet(setThis);
								break;
						}
					}
					if (args.Length == 3) {
						switch (setThis) {
							// TODO: insert 2 arg sized sets
							default:
								HelpSet(setThis);
								break;
						}
					}
					if (args.Length >= 4) {
						switch (setThis) {
							// TODO: insert 3 arg sized sets
							case "map":
								if (typeQueries.Keys.Contains(args[2])) {
									custdlConfig["map"][args[2]] = args[3];
									Console.WriteLine("Set " + args[2] + " to be downloaded to " + args[3]);
								}
								else {
									HelpTypes();
								}
								return 3;
								break;
							default:
								HelpSet(setThis);
								break;
						}
					}
					return 0;
				}
				else {
					Console.WriteLine("Couldn't set: " + args[1]);

					return 0;
				}
			}
			finally {
				var p = new IniParser.FileIniDataParser();
				p.WriteFile(".cec/" + StaticName, custdlConfig);
			}
		}

		static void HelpTypes() {
			Console.WriteLine(@"Types:");
			foreach (var t in typeQueries) {
				Console.WriteLine("\t" + t.Key);
			}
		}

		public CustomisationDownloader() {
			NoCommands = () => { return !doUpload && !doDownload; };
			var p = new IniParser.FileIniDataParser();
			if (IsCec()) {
				if (File.Exists(".cec/" + ShortName)) {
					custdlConfig = p.ReadFile(".cec/" + ShortName);
				}
				else {
					custdlConfig = new IniData();
					p.WriteFile(".cec/" + ShortName, custdlConfig);
				}
			}
		}

		public static void Main(string[] args) {
			new CustomisationDownloader().Start(args);
		}

		class DownloadTarget {
			public string Name;
			public string Filename;
			public string Data;
			public QueryExpression Query;
			public Func<string, Entity, string> Process = null;
			public string FileExten;
		}

		private static Dictionary<string, DownloadTarget> typeQueries = new Dictionary<string, DownloadTarget>() {
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

		public override void Execute(string[] args) {
			if (types.Contains("?")) {
				HelpTypes();
				return;
			}

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

					var targDir = t;
					if (custdlConfig != null && custdlConfig.Sections.ContainsSection("map")) {
						if (custdlConfig["map"].ContainsKey(t))
							targDir = custdlConfig["map"][t];
					}

					Console.WriteLine("Writing to: " + targDir);

					System.IO.Directory.CreateDirectory(targDir);
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

							File.WriteAllText(targDir + "/" + fname, data);
							Console.WriteLine("Downloaded " + r.GetAttributeValue<string>(downTarg.Filename));
						}
						else {
							Console.WriteLine("Failed " + r.GetAttributeValue<string>(downTarg.Name));
						}
					}

				}
				catch {
					throw;
				}
			}
		}

		static string XmlPretty(string xml, Entity e) {
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

				resp = OrgService.Execute(req) as ExportMappingsImportMapResponse;
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

				resp = OrgService.Execute(req) as DownloadReportDefinitionResponse;
			}
			catch (Exception ex) {
				Console.WriteLine("Report not supported " + ex.Message);
				return null;
			}

			return XmlPretty(resp.BodyText, null);
		}

	}
}
