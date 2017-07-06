using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CRMEnhancedCLI;

namespace CRMDocumentTemplates
{
	class Program
	{
		#region Arguments
		static List<string> files = new List<string>();
		static bool doUpload = false;
		static bool doBackup = false;
		static bool doRetrieve = false;
		static bool doCompile = false;
		static bool doDecompile = false;

		static Dictionary<string, Func<string[], int>> commlines = new Dictionary<string, Func<string[], int>>()
		{
			{"retrieve", (a) => {doRetrieve = true; return 0;}},
			{"compile", (a) => {doCompile = true; return GetFiles(a);}},
			{"decompile", (a) => {doDecompile = true; return GetFiles(a);}},
			{"backup", (a) => {doBackup = true; return GetFiles(a);}},
			{"upload", (a) => {doUpload = true; return GetFiles(a);}},
			{"org", (a) => {return ConnectArgs(a);}},
			{"help", (a) => {Help(); return 0;}}
		};

		static IOrganizationService OrgService = null;

		static void ParseArgs(params string[] args)
		{
			int i = 0;
			for (; i < args.Length; i++)
			{
				if (args[i][0] == '-')
				{
					if (commlines.ContainsKey(args[i].Substring(1)))
						i += commlines[args[i].Substring(1)].Invoke(args.Skip(i).ToArray());
					else
					{
						var kv = commlines.Where(p => p.Key[0] == args[i][1]);
						if (kv.Count() == 1)
						{
							var aa = args.Skip(i + 1).ToArray();
							i += kv.FirstOrDefault().Value.Invoke(aa);
						}
						else
						{
							Console.WriteLine("Couldn't match:");
							foreach (var p in kv)
							{
								Console.WriteLine("\t" + p.Key);
							}
						}
					}
				}
				else if (File.Exists(args[i]))
				{
					Console.WriteLine("Adding file " + args[i]);
					files.Add(args[i]);
				}
				else
				{
					Console.WriteLine("Ignoring " + args[i]);
				}
			}
		}
		#endregion

		static void Help()
		{
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

		[STAThread]
		static void Main(string[] args)
		{
			try
			{
				if (args.Length == 0)
				{
					Console.WriteLine("Enter arguments:");
					var comms = Console.ReadLine();
					args = ExtensionMethods.SplitCommandLine(comms).ToArray();
				}
				if (args.Length == 0)
				{
					Help();
					return;
				}

				ParseArgs(args);

				if (files.Count > 0 && doRetrieve && !doUpload && !doBackup && !doCompile)
				{
					Console.WriteLine("Do what with " + files.Count + " files?");
					var comms = Console.ReadLine();
					args = ExtensionMethods.SplitCommandLine(comms).ToArray();
					ParseArgs(args);
				}

				if (doRetrieve && doUpload)
				{
					Console.Error.WriteLine("Cannot retrieve and upload at the same time :(");
					return;
				}

				if (doCompile && doDecompile)
				{
					Console.Error.WriteLine("Cannot compile and decompile at the same time :(");
					return;
				}

				if (doBackup)
				{
					if (doRetrieve || files.Count == 0)
					{
						// backup everything
						new DirectoryInfo("Document Templates").ToZip("Template Backups\\Document Templates" + DateTime.Now.ToString("yyyyMMdd_HHmm"));
						Console.WriteLine("Backed up working data");
					}
					else if (!doUpload || doDecompile)
					{
						foreach (var f in files)
						{
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

				if (OrgService == null)
					OrgService = ExtensionMethods.Connect("http://localhost/");

				if (doRetrieve)
					DocumentTemplates.GetDocumentTemplates(OrgService, files?.ToArray());

				if (doUpload && !doCompile && !doDecompile)
				{
					// try to auto determine what these are
					foreach (var f in files)
					{
						if (File.Exists(f + ".docx"))
							DocumentTemplates.DecompileTemplate(f);
						else if (Directory.Exists(f))
							DocumentTemplates.CompileTemplate(f);
					}
				}

				if (doCompile)
				{
					foreach (var f in files)
					{
						DocumentTemplates.CompileTemplate(f);
					}
				}
				else if (doDecompile)
				{
					foreach (var f in files)
					{
						DocumentTemplates.DecompileTemplate(f);
					}
				}

				if (doUpload)
				{
					foreach (var f in files)
					{
						DocumentTemplates.UploadTemplate(OrgService, f, doBackup);
					}
				}
			}
			catch (Exception ex)
			{
				Console.Error.WriteLine(ex.GetType() + ": " + ex.Message + Environment.NewLine + ex.StackTrace);
			}
			finally
			{
				Console.WriteLine("Finished all tasks");
				Console.Read();
			}
		}

		static int ConnectArgs(string[] args)
		{
			Uri u;
			if (!Uri.TryCreate(args[0], UriKind.Absolute, out u))
				return 0;

			if (args.Length == 1 || args[1][0] == '-')
			{
				OrgService = ExtensionMethods.Connect(args[0]);
				return 1;
			}
			OrgService = ExtensionMethods.Connect(args[0], args[1], args[2]);
			return 3;
		}

		static int GetFiles(string[] args)
		{
			int i = 0;
			while (i < args.Length && args[i][0] != '-')
			{
				files.Add(args[i].Replace(".docx", ""));
				Console.WriteLine("Adding file: " + args[i].Replace(".docx", ""));
				i++;
			}
			return i;
		}
	}

}
