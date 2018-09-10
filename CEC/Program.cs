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
using System.Reflection;

namespace CEC {
	public class Cec : ProgramBase {

		public Cec() {
			autoConnect = false;
			SupressSplash = true;
			SupressArgErrors = true;
		}

		protected override string argsPrefix { get { return ""; } }

		public override string ShortName { get { return "cec"; } }

		static bool supressCon = false;

		static void Main(string[] args) {
			new Cec().Start(args);
		}

		public override void Help() {

			Console.WriteLine(@"cec :3");

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

			Console.WriteLine();
			foreach (var ps in Starters) {
				Console.WriteLine("\t" + ps.Key);
			}
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
				{ "set", set },
				{ "sup", supress },
			};
		}

		static int set(string[] args) {

			// set .cec config vars and stuff

			return 0;
		}

		static int supress (string[] args) {
			supressCon = true;
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

		private IEnumerable<Type> GetCecTypes() {
			var exes = Directory.EnumerateFiles(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "*.exe");
			foreach (var e in exes.Where(ex => !ex.Contains(".vshost") && !ex.Contains("\\cec.exe"))) {
				// TODO: sandbox this loading, check for customattrs
				var assem = Assembly.LoadFile(e);
			}

			var ass = AppDomain.CurrentDomain.GetAssemblies();
			var cecAss = ass.Where(a => a.GetCustomAttribute<CecTypeAttribute>() != null);

			List<Type> types = new List<Type>();
			foreach (var a in cecAss) {
				var cecType = a.GetCustomAttribute<CecTypeAttribute>().CecType;
				types.Add(cecType);
			}
			cecTypes = types;
			return types;
		}

		private IEnumerable<Type> cecTypes = null;
		public IEnumerable<Type> CecTypes {
			get {
				if (cecTypes == null) {
					GetCecTypes();
				}
				return cecTypes;
			}
		}

		public class ProgramStarter {
			Type cecType;

			public ProgramStarter(Type t) {
				if (!typeof(ProgramBase).IsAssignableFrom(t)) {
					throw new InvalidOperationException(t.Name + " isn't a ProgramBase.");
				}
				cecType = t;
			}

			public T Start<T>() where T : ProgramBase {
				var ctor = cecType.GetConstructor(new Type[0]);
				return (T)ctor.Invoke(new object[0]);
			}
		}

		private Dictionary<string, ProgramStarter> starters = null;
		public Dictionary<string, ProgramStarter> Starters {
			get {
				if (starters == null) {
					RegenStarters();
				}
				return starters;
			}
		}

		public void RegenStarters() {
			if (starters == null)
				starters = new Dictionary<string, ProgramStarter>();
			foreach (var t in CecTypes) {
				ProgramStarter ps = new ProgramStarter(t);
				var pb = ps.Start<ProgramBase>();
				starters[pb.ShortName] = ps;
			}
		}

		public override void Execute(string[] args) {

			var targs = Starters.Keys.Intersect(args);
			var prog = targs.FirstOrDefault();
			var skip = args.ToList().IndexOf(prog);
			

			if (Starters.ContainsKey(prog)) {
				var pb = Starters[prog].Start<ProgramBase>();
				pb.DebuggerPauseOnEnd = false;
				if (supressCon)
					pb.autoConnect = false;
				pb.Start(args.Skip(skip + 1).ToArray());
				return;
			}
			Console.WriteLine("Found no CecType: " + prog);
		}
	}
}
