using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace CLI.EarlyboundShrinker {
	class Program {
		static void Main(string[] args) {
			var fname = args.FirstOrDefault() ?? "Xrm.cs";
			if (!File.Exists(fname)) {
				Console.WriteLine("No file specified, and no Xrm.cs in CD.");
				Console.Read();
				return;
			}

			var fileLines = File.ReadAllLines(fname);

			var classes = new List<string>();

			Regex r = new Regex("public partial class (?:Xrm.)?(.*?) :");
			foreach (var l in fileLines) {
				var m = r.Match(l);
				if (m.Success) {
					classes.Add(m.Groups[1].Value);
				}
			}
			classes = classes.Distinct().ToList();

			var inputLines = new Queue<string>(fileLines);

			int tabClass = -1;
			int tabFunc = -1;

			// we need to go back up the file, so we use a stack
			var outputLines = new Stack<string>();

			// it's dangerous to go alone
			outputLines.Push("#define XrmServiceContext");

			// when going backwards through the completed lines, we need to put them back correctly
			var tempLines = new Stack<string>();

			int remainingLines = inputLines.Count;
			int totalLines = inputLines.Count;

			while (inputLines.Any()) {
				var line = inputLines.Dequeue();
				remainingLines--;
				// pick a dandy looking prime
				if (remainingLines % 1171 == 0) {
					Console.Write("\r" + (totalLines - remainingLines) + "/" + totalLines + "  --  " + string.Format("{0:N2}%", ((totalLines - remainingLines) * 100 / (double)totalLines)));
				}

				/// ignore comments and lines that are probably just \t\t{
				if (line.Length > 10 && !line.Contains("//")) {
					string cn = null;

					// only start checking if you have an X
					if (line.Any(c => c == 'X')) {
						if (cn == null) {
							cn = classes.FirstOrDefault(c => line.Contains($"Xrm.{c} "));
						}
						if (cn == null) {
							cn = classes.FirstOrDefault(c => line.Contains($"Xrm.{c}>"));
						}
					}

					// only class defs have ' x ', so check for 1 tab
					if (cn == null && line.Count(c => c == '\t') == 1) {
						cn = classes.FirstOrDefault(c => line.Contains($" {c} "));
					}

					if (cn != null) {
						if (line.Contains("class ")) {
							if (tabClass == -1) {
								tabClass = line.Count(c => c == '\t');
								while (outputLines.Any() && !string.IsNullOrWhiteSpace(outputLines.Peek())) {
									tempLines.Push(outputLines.Pop());
								}
								outputLines.Push($"#if {cn}");
								while (tempLines.Any())
									outputLines.Push(tempLines.Pop());
							}
						}
						else {
							if (tabFunc == -1) {
								tabFunc = line.Count(c => c == '\t');
								while (outputLines.Any() && !string.IsNullOrWhiteSpace(outputLines.Peek())) {
									tempLines.Push(outputLines.Pop());
								}
								outputLines.Push($"#if {cn}");
								while (tempLines.Any())
									outputLines.Push(tempLines.Pop());
							}
						}
					}
				}

				outputLines.Push(line);

				if (line.Contains("}")) {
					if (tabClass == line.Count(c => c == '\t')) {
						outputLines.Push("#endif");
						tabClass = -1;
					}
					if (tabFunc == line.Count(c => c == '\t')) {
						outputLines.Push("#endif");
						tabFunc = -1;
					}
				}
			}

			// reverse the stack to have it output in the correct direction
			File.WriteAllLines(Path.GetFileNameWithoutExtension(fname) + ".fix.cs", outputLines.Reverse().ToArray());

			Console.ReadKey();
			return;
		}
	}
}
