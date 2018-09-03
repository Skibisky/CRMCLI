using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CEC.Extensions {
	public abstract class ProgramBase {
		protected abstract Dictionary<string, Func<string[], int>> getArgDic();
		protected static readonly HashSet<string> files = new HashSet<string>();
		protected static IOrganizationService orgService = null;
		public static IOrganizationService OrgService { get { return orgService; } }
		static bool argsParsed = false;
		protected static bool autoConnect = true;

		protected static string argsPrefix = "-";

		private static ProgramBase _single = null;
		private static ProgramBase Single {
			get {
				if (_single == null) {
					var asms = AppDomain.CurrentDomain.GetAssemblies();
					List<Type> types = new List<Type>();
					foreach (var asm in asms) {
						var type = asm.GetTypes();
						types.AddRange(type.Where(t => t.IsClass && !t.IsAbstract && t.IsSubclassOf(typeof(ProgramBase))));
					}
					if (types.Count != 1) {
						Console.WriteLine("Too many ProgramBase derivitives loaded.");
						return null;
					}
					var y = types.Single();
					var c = y.GetConstructor(new Type[0]);
					_single = (ProgramBase)c.Invoke(new object[0]);
				}
				return _single;
			}
		}

		protected static void BaseMain(string[] args) {
			ParseArgs(args);
			if (OrgService == null && autoConnect) {
				Console.WriteLine("Connection defaulting to localhost...");
				orgService = ExtensionMethods.Connect("http://localhost");
			}
			Single.SubMain();
		}

		protected abstract void SubMain();

		protected static void ParseArgs(params string[] args) {
			ParseArgs(Single.getArgDic(), args);
		}

		protected static void ParseArgs(Type prog, params string[] args) {
			var c = prog.GetConstructor(new Type[0]);
			var p = (ProgramBase)c.Invoke(new object[0]);
			_single = p;
			ParseArgs(Single.getArgDic(), args);
		}

		protected static void ParseArgs(Dictionary<string, Func<string[], int>> commlines, params string[] args) {
			int i = 0;
			for (; i < args.Length; i++) {
				if (string.IsNullOrWhiteSpace(argsPrefix) || args[i].StartsWith(argsPrefix)) {
					if (commlines.ContainsKey(args[i].Substring(argsPrefix.Length)))
						i += commlines[args[i].Substring(argsPrefix.Length)].Invoke(args.Skip(i).ToArray());
					else {
						var kv = commlines.Where(p => p.Key[0] == args[i][argsPrefix.Length]);
						if (kv.Count() == 1) {
							var aa = args.Skip(i + 1).ToArray();
							i += kv.FirstOrDefault().Value.Invoke(aa);
						}
						else {
							Console.WriteLine("Couldn't match:");
							foreach (var p in kv) {
								Console.WriteLine("\t" + p.Key);
							}
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

		protected static int ConnectArgs(string[] args) {
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