using IniParser.Model;
using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Security.Cryptography;
using System.Text;

namespace CEC.Extensions {
	public abstract class ProgramBase {
		protected abstract Dictionary<string, Func<string[], int>> getArgDic();
		protected static readonly HashSet<string> files = new HashSet<string>();
		protected static IOrganizationService orgService = null;
		public static IOrganizationService OrgService { get { return orgService; } }
		protected bool argsParsed = false;
		protected static bool cecChecked = false;
		protected bool autoConnect = true;
		public static bool Verbose { get; set; } = false;
		protected Func<bool> NoCommands = () => false;
		private static string parProcName = null;

		protected virtual string argsPrefix {
			get {
				return "-";
			}
		}

		public static string ParProcName {
			get {
				if (parProcName == null)
					parProcName = GetParentProcessName();
				return parProcName;
			}
		}
		private static string GetParentProcessName() {
			var myId = Process.GetCurrentProcess().Id;
			var query = string.Format("SELECT ParentProcessId FROM Win32_Process WHERE ProcessId = {0}", myId);
			var search = new ManagementObjectSearcher("root\\CIMV2", query);
			var queryObj = search.Get().OfType<ManagementBaseObject>().FirstOrDefault();
			if (queryObj == null) {
				return null;
			}
			var parentId = (uint)queryObj["ParentProcessId"];
			var parent = Process.GetProcessById((int)parentId);
			return parent.ProcessName;
		}

		public static readonly Dictionary<ConsoleColor, string> colCodes = new Dictionary<ConsoleColor, string>() {
			{ ConsoleColor.Black, "0;30" },
			{ ConsoleColor.Red, "0;31" },
			{ ConsoleColor.Green, "0;32" },
			{ ConsoleColor.Blue, "0;34" },
			{ ConsoleColor.Magenta, "0:35" },
			{ ConsoleColor.Cyan, "0;36" },
			{ ConsoleColor.Gray, "0;37" },
			{ ConsoleColor.DarkGray, "1;30" },
			{ ConsoleColor.Yellow, "1;33" },
			{ ConsoleColor.White, "1;37" },
		};

		public static ConsoleColor ConCol {
			get {
				return Console.ForegroundColor;
			}
			set {
				Console.ForegroundColor = value;
				if (ProgramBase.ParProcName == "bash" && colCodes.ContainsKey(value)) {
					Console.Write($"\u001b[{colCodes[value]}m");
				}
			}
		}

		public static void ConColReset() {
			if (ProgramBase.ParProcName == "bash") {
				Console.Write("\u001b[0m");
			}
			Console.ResetColor();
		}

		public void Start(string[] args) {
			Splash();
			if (this.PromptCommands) {
				if (args.Length == 0) {
					Console.WriteLine("Enter arguments:");
					var comms = Console.ReadLine();
					args = ExtensionMethods.SplitCommandLine(comms).ToArray();
				}
			}
			if (args.Length == 0) {
				Help();
				if (DebuggerPauseOnEnd)
					pauseDebugger();
				return;
			}

			ParseArgs(args);

			if (this.PromptNoCommands && files.Count > 0 && this.NoCommands()) {
				Console.WriteLine("Do what with " + files.Count + " files?");
				var comms = Console.ReadLine();
				args = ExtensionMethods.SplitCommandLine(comms).ToArray();
				argsParsed = false;
				ParseArgs(args);
			}

			try {
				this.Execute(args);
			}
			catch (Exception e) {
				Console.WriteLine(e.GetType() + ": " + e.Message);
				Console.WriteLine(e.StackTrace);
			}
			if (DebuggerPauseOnEnd)
				pauseDebugger();
		}

		/// <summary>
		/// Trigger from CEC.exe
		/// </summary>
		public abstract string ShortName { get; }
		public virtual string FullName { get { return this.GetType().Name; } }

		/// <summary>
		/// If to stop and ask for things if run with no args
		/// </summary>
		public virtual bool PromptCommands { get { return true; } }

		/// <summary>
		/// if this.NoCommands returns true, prompt for commands
		/// </summary>
		public virtual bool PromptNoCommands { get { return true; } }

		public bool DebuggerPauseOnEnd { get; set; } = true;

		public bool SupressSplash { get; set; } = false;

		public bool SupressArgErrors { get; set; } = false;

		/// <summary>
		/// Show a splash if you want
		/// </summary>
		public virtual void Splash() {
			if (SupressSplash)
				return;

			var splash = "CEC " + this.FullName + " v." + version();
			Console.WriteLine(new string('=', splash.Length));
			Console.WriteLine(splash);
			Console.WriteLine(new string('-', splash.Length));
			Console.WriteLine("From " + Environment.CurrentDirectory);
		}

		public string version() {
			return this.GetType().Assembly.GetName().Version.ToString();
		}

		/// <summary>
		/// Print to the console how-to
		/// </summary>
		public abstract void Help();

		/// <summary>
		/// Output a cheesy help based on argDic
		/// </summary>
		public void HelpDefault() {
			var acceptArgs = this.getArgDic();
			Console.Write(this.GetType().Name);
			if (!string.IsNullOrWhiteSpace(this.ShortName))
				Console.Write(" -- " + this.ShortName);
			Console.WriteLine(" usage:");
			Console.WriteLine();
			foreach (var arg in acceptArgs) {
				Console.WriteLine("\t" + argsPrefix + arg.Key);
			}
		}

		/// <summary>
		/// Actually do something
		/// </summary>
		/// <param name="args">Passed in for safety, already parsed.</param>
		public abstract void Execute(string[] args);
		
		protected int debug(string[] args) {
			Console.WriteLine("Attach console or anykey to continue...");
			while (!System.Diagnostics.Debugger.IsAttached) {
				if (!Console.IsInputRedirected && Console.KeyAvailable)
					break;
			}
			if (System.Diagnostics.Debugger.IsAttached) {
				Console.WriteLine("Debugger detected...");
			}

			ParseArgs(args.Skip(1).ToArray());

			return 0;
		}
		
		protected void ParseArgs(params string[] args) {
			Dictionary<string, Func<string[], int>> commlines = this.getArgDic();
			CheckCec();
			if (argsParsed) {
				Console.Out.Verbose("Skipping args");
				return;
			}
			int i = 0;
			for (; i < args.Length; i++) {
				if (string.IsNullOrWhiteSpace(argsPrefix) || args[i].StartsWith(argsPrefix)) {
					if (commlines.ContainsKey(args[i].Substring(argsPrefix.Length))) {
						i += commlines[args[i].Substring(argsPrefix.Length)].Invoke(args.Skip(i).ToArray());
					}
					else {
						var kv = commlines.Where(p => p.Key[0] == args[i][argsPrefix.Length]);
						if (kv.Count() == 1) {
							var aa = args.Skip(i + 1).ToArray();
							i += kv.FirstOrDefault().Value.Invoke(aa);
						}
						else if (!SupressArgErrors) {
							ConCol = ConsoleColor.Red;
							Console.Write("Couldn't match");
							if (!kv.Any()) {
								Console.WriteLine(": " + args[i]);
							}
							else {
								Console.WriteLine(" from:");
								foreach (var p in kv) {
									Console.WriteLine("\t" + p.Key);
								}
							}
							ConColReset();
						}
					}
				}
				else if (File.Exists(args[i])) {
					Console.WriteLine("Adding file " + args[i]);
					files.Add(args[i]);
				}
				else {
					Console.WriteLine("Ignoring " + args[i]);
				}
			}
			argsParsed = true;
		}

		protected static bool IsCec() {
			return Directory.Exists(".cec") && File.Exists(".cec/config");
		}

		public static void pauseDebugger(string action = "continue") {
			if (System.Diagnostics.Debugger.IsAttached) {
				Console.WriteLine("Press anykey to " + action + "...");
				Console.ReadKey();
			}
		}

		protected static IniData config;
		protected static void LoadCec() {
			var p = new IniParser.FileIniDataParser();
			try {
				config = p.ReadFile(".cec/config");
			}
			catch {
				config = new IniData();
			}
		}

		protected void CheckCec() {
			if (cecChecked) {
				if (autoConnect && OrgService == null)
					ConnectCec();
				Console.Out.Verbose("Skipping cec check");
				return;
			}
			if (IsCec()) {
				LoadCec();
				if (autoConnect)
					ConnectCec();
			}
			cecChecked = true;
		}

		protected static void ConnectCec() {
			var url = config["org"]["url"];
			var user = config["org"]["user"];
			var oPass = config["org"]["pass"];

			var passB = Convert.FromBase64String(oPass);
			var passU = ProtectedData.Unprotect(passB, new byte[0], DataProtectionScope.CurrentUser);
			var passS = Encoding.Default.GetString(passU);
			string pass;
			if (passS.Contains("legit:")) {
				pass = passS.Replace("legit:", "");
			}
			else {
				Console.WriteLine("Enter Password for " + user + ":" + url);
				pass = Console.ReadLine();
			}
			ConnectArgs(new string[] { url, user, pass });
		}

		protected static int ConnectArgs(string[] args) {
			System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls12;
			Uri u;
			if (!Uri.TryCreate(args[0], UriKind.Absolute, out u))
				return 0;

			Console.WriteLine("Connecting to " + args[0]);

			if (args.Length == 1 || args[1][0] == '-') {
				orgService = ExtensionMethods.Connect(args[0]);
				return 1;
			}
			orgService = ExtensionMethods.Connect(args[0], args[1], args[2]);
			return 3;
		}

		protected static int GetFiles(string[] args) {
			int i = 0;
			while (i < args.Length && args[i][0] != '-') {
				files.Add(args[i]);
				Console.WriteLine("Adding file: " + args[i].Replace(Environment.CurrentDirectory, ""));
				i++;
			}
			return i;
		}

		[System.AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
		sealed class CommandLineArgumentAttribute : Attribute {
			// See the attribute guidelines at 
			//  http://go.microsoft.com/fwlink/?LinkId=85236
			readonly string argumentName;
			readonly bool canBeShort;

			// This is a positional argument
			public CommandLineArgumentAttribute(string argName, bool canBeShort = false) {
				argumentName = argName;
				this.canBeShort = canBeShort;
			}

			public string PositionalString {
				get { return argumentName; }
			}

			public bool CanBeShort {
				get { return canBeShort; }
			}
		}
	}
}