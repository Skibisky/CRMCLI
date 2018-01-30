using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CLI.EarlyboundShrinker {
	class Program {
		static void Main(string[] args) {
			var fname = args.FirstOrDefault() ?? "Xrm.cs";
			if (!File.Exists(fname)) {
				Console.WriteLine("No file specified, and no Xrm.cs in CD.");
				Console.Read();
				return;
			}

			var q = File.ReadAllLines(fname);

			var classes = new List<string>();

			Regex r = new Regex("public partial class (?:Xrm.)?(.*?) :");
			foreach (var l in q) {
				var m = r.Match(l);
				if (m.Success) {
					classes.Add(m.Groups[1].Value);
				}
			}
			classes = classes.Distinct().ToList();

			var fq = new Queue<string>(q);

			int tabClass = -1;
			int tabFunc = -1;

			var fl = new Stack<string>();
			
			// it's dangerous to go alone
			fl.Push("#define XrmServiceContext");

			var ts = new Stack<string>();

			int i = fq.Count;
			int t = fq.Count;
			
			while (fq.Any()) {
				var l = fq.Dequeue();
				i--;
				// pick a dandy looking prime
				if (i % 1171 == 0) {
					Console.Write("\r" + (t - i) + "/" + t + "  --  " + string.Format("{0:N2}%", ((t - i) * 100 / (double)t)));
				}

				/// ignore comments and lines that are probably just \t\t{
				if (l.Length > 10 && !l.Contains("//")) {
					string cn = null;

					// only start checking if you have an X
					if (l.Any(c => c == 'X')) {
						if (cn == null) {
							cn = classes.FirstOrDefault(c => l.Contains($"Xrm.{c} "));
						}
						if (cn == null) {
							cn = classes.FirstOrDefault(c => l.Contains($"Xrm.{c}>"));
						}
					}

					// only class defs have ' x ', so check for 1 tab
					if (cn == null && l.Count(c => c == '\t') == 1) {
						cn = classes.FirstOrDefault(c => l.Contains($" {c} "));
					}

					if (cn != null) {
						if (l.Contains("class ")) {
							if (tabClass == -1) {
								tabClass = l.Count(c => c == '\t');
								while (fl.Any() && !string.IsNullOrWhiteSpace(fl.Peek())) {
									ts.Push(fl.Pop());
								}
								fl.Push($"#if {cn}");
								while (ts.Any())
									fl.Push(ts.Pop());
							}
						}
						else {
							if (tabFunc == -1) {
								tabFunc = l.Count(c => c == '\t');
								while (fl.Any() && !string.IsNullOrWhiteSpace(fl.Peek())) {
									ts.Push(fl.Pop());
								}
								fl.Push($"#if {cn}");
								while (ts.Any())
									fl.Push(ts.Pop());
							}
						}
					}
				}

				fl.Push(l);

				if (l.Contains("}")) {
					if (tabClass == l.Count(c => c == '\t')) {
						fl.Push("#endif");
						tabClass = -1;
					}
					if (tabFunc == l.Count(c => c == '\t')) {
						fl.Push("#endif");
						tabFunc = -1;
					}
				}
			}

			File.WriteAllLines(Path.GetFileNameWithoutExtension(fname) + ".fix.cs", fl.Reverse().ToArray());

			Console.ReadKey();
			return;
		}
	}
}
