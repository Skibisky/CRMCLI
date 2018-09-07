using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CEC.Extensions;
using IniParser.Model;
using Microsoft.Xrm.Sdk.Query;
using System.Diagnostics;
using System.Management;

namespace CEC {
	class Program : ProgramBase {
		static void Main(string[] args) {
			if (System.Diagnostics.Debugger.IsAttached && args.Length == 0) {
				Console.WriteLine("Enter arguments:");
				var comms = Console.ReadLine();
				args = ExtensionMethods.SplitCommandLine(comms).ToArray();
			}
			if (args.Length == 0) {
				Help();
				return;
			}

			argsPrefix = "";
			ParseArgs(args);

			pauseDebugger();
		}

		static void Help() {

			Console.WriteLine(@"cec help:
:S");


			if (IsCec()) {
				ConCol = ConsoleColor.Green;
				LoadCec();
				var name = config["org"]["name"];
				if (name == null)
					name = config["org"]["url"];
				if (name == null)
					name = "a CEC folder";

				Console.WriteLine("This is " + name + ".");
			}
			else if (IsGit()) {
				ConCol = ConsoleColor.Yellow;
				Console.WriteLine("You could use this folder for CEC");
				string maybename = "it's";
				string maybebranch = "?";
				try {
					maybename = "'" + LoadGit()["remote \"origin\""]["url"] + "'";
					maybebranch = " on " + File.ReadAllText(".git/HEAD").Replace("ref: refs/heads/", "").Replace("\n", "");
				}
				catch {
				}
					Console.WriteLine("Make sure that " + maybename + maybebranch + " is the right place.");
			}
			else {
				ConCol = ConsoleColor.Red;
				Console.WriteLine("You cannot CEC here, try a git repo.");
			}
			ConColReset();
		}

		static bool IsGit() {
			return Directory.Exists(".git") && File.Exists(".git/config");
		}
		
		static IniData LoadGit() {
			var p = new IniParser.FileIniDataParser();
			return p.ReadFile(".git/config");
		}

		static bool validCec() {
			// TODO: check config

			return true;
		}

		static bool closeNow = true;

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				{ "init", init },
				{ "edit", edit },
				{ "test", test },
				{ "debug", debug },
				{ "custdl", customisationDownloader },
			};
		}

		static int customisationDownloader(string[] args) {
			ProcessStartInfo info = new ProcessStartInfo() {
				Arguments = string.Join(" ", args.Skip(1)),
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardError = true,
				RedirectStandardOutput = true,
				UseShellExecute = false,
				WorkingDirectory = Environment.CurrentDirectory,
				FileName = "CustomisationDownloader.exe",
			};
			var p = new Process();
			p.StartInfo = info;
			p.OutputDataReceived += (s, e) => {
				Console.WriteLine(e.Data);
			};
			p.ErrorDataReceived += (s, e) => {
				Console.WriteLine(e.Data);
			};
			p.Start();
			p.BeginOutputReadLine();
			p.BeginErrorReadLine();

			p.WaitForExit();
			return 0;
		}

		static int test(string[] args) {
			closeNow = true;

			if (config == null)
				LoadCec();

			if (config == null) {
				ConCol = ConsoleColor.Red;
				Console.WriteLine("CEC not configured.");
				ConColReset();
				return args.Length;
			}

			if (orgService == null)
				ConnectCec();

			var res = OrgService.RetrieveMultiple(new QueryExpression("systemuser") {
				TopCount = 1,
			});

			if (res != null && res.Entities != null && res.Entities.Count == 1) {
				ConCol = ConsoleColor.Green;
				Console.WriteLine("CEC can connect!");
			}
			else {
				ConCol = ConsoleColor.Red;
				Console.WriteLine("CEC can not connect!");
			}
			ConColReset();

			return args.Length;
		}

		static int edit(string[] args) {
			Func<string, string, bool, string> prompt = (pr, v, pa) => {
				Console.Write(pr + " (");
				if (!string.IsNullOrWhiteSpace(v)) {
					Console.Write("leave blank for: \"" + v + "\",");
				}
				Console.WriteLine("ctrl+z to skip)");

				if (pa) {
					string op = "";
					ConsoleKeyInfo akey = Console.ReadKey();
					while (akey.Key != ConsoleKey.Enter) {
						Console.CursorLeft = Console.CursorLeft - 1;
						Console.Write("*");
						op += akey.KeyChar;
						akey = Console.ReadKey();
					}
					return op;
				}

				var str = Console.ReadLine();
				if (str == null || str.Contains("\u001a"))
					return null;

				var val = str.Replace(Environment.NewLine, ""); ;
				if (string.IsNullOrWhiteSpace(val))
					return v;
				return val;
			};

			LoadCec();

			config["org"]["url"] = prompt("Enter Url", config["org"]["url"], false);
			config["org"]["name"] = prompt("Enter Name", config["org"]["name"], false);
			config["org"]["user"] = prompt("Enter User", config["org"]["user"], false);
			var oPass = config["org"]["pass"];
			bool hasPass = false;
			if (oPass != null) {
				var udat = Encoding.Default.GetString(ProtectedData.Unprotect(Convert.FromBase64String(oPass), new byte[0], DataProtectionScope.CurrentUser));
				if (udat.StartsWith("legit:")) {
					hasPass = true;
				}
			}
			var pass = prompt("Enter pass", hasPass ? "*****" : "", true);

			if (!string.IsNullOrWhiteSpace(pass) && pass != "*****") {
				var protec = ProtectedData.Protect(Encoding.Default.GetBytes("legit:" + pass), new byte[0], DataProtectionScope.CurrentUser);
				var protStr = Convert.ToBase64String(protec);
				config["org"]["pass"] = protStr;
			}

			var p = new IniParser.FileIniDataParser();
			p.WriteFile(".cec/config", config);

			return args.Length;
		}

		static int init(string[] args) {

			if (!IsGit()) {
				Console.Error.WriteLine("Not even close to a git repo.");
			}
			else if (IsCec()) {
				Console.Error.WriteLine("This is already CEC init'd.");
			}
			else {
				Directory.CreateDirectory(".cec");
				config = new IniData();
				edit(args);
			}

			closeNow = true;
			return args.Length;
		}

		protected override void SubMain() {



		}
	}
}
