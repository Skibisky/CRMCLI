using CEC.Extensions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CEC.DocumentTemplates.DCE;

[assembly:CecType(typeof(CEC.DocumentTemplates.CLI.DocumentTemplates))]
namespace CEC.DocumentTemplates.CLI {
	class DocumentTemplates : ProgramBase {
		#region Arguments
		static bool doUpload = false;
		static bool doBackup = false;
		static bool doRetrieve = false;
		static bool doCompile = false;
		static bool doDecompile = false;

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>()
			{
				{"retrieve", (a) => {doRetrieve = true; return 0;}},
				{"compile", (a) => {doCompile = true; return GetFiles(a);}},
				{"decompile", (a) => {doDecompile = true; return GetFiles(a);}},
				{"backup", (a) => {doBackup = true; return GetFiles(a);}},
				{"upload", (a) => {doUpload = true; return GetFiles(a);}},
				{"org", (a) => {return ConnectArgs(a);}},
				{"help", (a) => {Help(); return 0;}}
			};
		}
		#endregion

		public override string ShortName { get { return "doctemp"; } }

		public override void Help() {
			Console.WriteLine(@"Usage:
CRMDocumentTemplates [-h] [-o URI [user pass]] [-b [template1 ...]] [[-c | -d] [template1 ...]] [[-u | -r] template1 ...]
	-h Help: Display this help
	-o Org: URI [user pass]
		Connects to the CRM specified by URI with user pass.
		If user pass isn't specified, will use Default Network Creds.
	-b Backup: Creates copies of files depending on action
	-r Retrieve:Downloads all the templates
	-c Compile: Compiles the directories back up into docxs
	-d Decompile: Decompiles the docxs into their directories
	-u Upload: Uploads the specified templates
");
		}

		public override void Splash() {
			var splash = "CRM Document Template tool prelease-" + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
			Console.WriteLine(new string('=', splash.Length));
			Console.WriteLine(splash);
			Console.WriteLine(new string('-', splash.Length));
			Console.WriteLine("From " + Environment.CurrentDirectory);
		}

		public DocumentTemplates() {
			this.NoCommands = () => doRetrieve && !doUpload && !doBackup && !doCompile;
		}

		static void Main(string[] args) {
			new DocumentTemplates().Start(args);
		}

		public override void Execute(string[] args) {
			try {
				if (doRetrieve && doUpload) {
					Console.Error.WriteLine("Cannot retrieve and upload at the same time :(");
					return;
				}

				if (doCompile && doDecompile) {
					Console.Error.WriteLine("Cannot compile and decompile at the same time :(");
					return;
				}

				if (doBackup)
					Console.Write("Backup ");
				if (doRetrieve)
					Console.Write("Retrieve ");
				if (doCompile)
					Console.Write("Compile ");
				if (doDecompile)
					Console.Write("Decompile ");
				if (doUpload)
					Console.Write("Upload ");
				Console.Write(files.Count + " files");

				Console.WriteLine();

				if (doBackup) {
					if (doRetrieve || files.Count == 0) {
						// backup everything
						new DirectoryInfo("Document Templates").ToZip("Template Backups\\Document Templates" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
						Console.WriteLine("Backed up working data");
					}
					else if (!doUpload || doDecompile) {
						foreach (var f in files) {
							Console.WriteLine("Backed up " + f);
							DirectoryInfo d = null;
							if (Directory.Exists("Document Templates/" + f))
								d = new DirectoryInfo("Document Templates/" + f);
							else if (Directory.Exists(f))
								d = new DirectoryInfo(f);
							else if (Directory.Exists("Document Templates/" + new FileInfo(f).Name.Replace(".docx", "")))
								d = new DirectoryInfo("Document Templates/" + new FileInfo(f).Name.Replace(".docx", ""));

							var bd = Directory.CreateDirectory(Path.Combine(d.Parent.Parent.FullName, "Template Backups", "local"));

							File.Delete(Path.Combine(bd.FullName, d.Name + ".docx"));
							var file = d.ToZip(zipExt: ".docx", basepath: bd.FullName);
							DateTime mod = new FileInfo(file).LastWriteTimeUtc;
							File.Copy(file, Path.Combine(bd.FullName, d.Name) + mod.ToString("yyyyMMdd_HHmmss") + ".docx");
						}
					}
				}

				if (doRetrieve)
					DocumentTemplater.GetDocumentTemplates(OrgService, files?.ToArray());

				if (doUpload && !doCompile && !doDecompile) {
					// try to auto determine what these are
					foreach (var f in files) {
						if (File.Exists(f))
							DocumentTemplater.DecompileTemplate(f);
						else if (Directory.Exists(f))
							DocumentTemplater.CompileTemplate(f);
					}
				}

				if (doCompile) {
					foreach (var f in files) {
						DocumentTemplater.CompileTemplate(f);
					}
				}
				else if (doDecompile) {
					foreach (var f in files) {
						DocumentTemplater.DecompileTemplate(f);
					}
				}

				if (doUpload) {
					foreach (var f in files) {
						DocumentTemplater.UploadTemplate(OrgService, f, doBackup);
					}
				}
			}
			catch (Exception ex) {
				Console.Error.WriteLine(ex.GetType() + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
			}
			finally {
				Console.WriteLine("Finished all tasks");
				Console.Read();
			}
		}

	}

}
