using CEC.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;

namespace CLI.DataDownloader {
	class Program : ProgramBase {

		static bool showUnimportable = false;
		static bool showBadAttrs = false;

		static void Main(string[] args) {

			if (args.Length == 0) {
				Console.WriteLine("Enter arguments:");
				var comms = Console.ReadLine();
				args = ExtensionMethods.SplitCommandLine(comms).ToArray();
			}
			if (args.Length == 0) {
				Help();
				return;
			}

			ParseArgs(args);

			var allResp = orgService.Execute(new RetrieveAllEntitiesRequest() {
				EntityFilters = Microsoft.Xrm.Sdk.Metadata.EntityFilters.Entity,
			}) as RetrieveAllEntitiesResponse;

			foreach (var et in allResp.EntityMetadata.OrderBy(e => e.LogicalName)) {
				if (showUnimportable) {
					if (et.IsImportable ?? false)
						ConCol = ConsoleColor.Green;
					else
						ConCol = ConsoleColor.Red;
					Console.WriteLine(et.LogicalName + " -- " + et.IsImportable);
				}
				else if (et.IsImportable ?? false) {
					Console.WriteLine(et.LogicalName);
				}
			}
			ConColReset();

			var resp = orgService.Execute(new RetrieveEntityRequest() {
				LogicalName = "contact",
				EntityFilters = Microsoft.Xrm.Sdk.Metadata.EntityFilters.Attributes,
			}) as RetrieveEntityResponse;


			foreach (var at in resp.EntityMetadata.Attributes.OrderBy(a => a.LogicalName)) {
				if (showBadAttrs) {
					if (at.IsValidForCreate ?? false)
						Console.Write("c\t");
					else
						Console.Write("\t");

					if (at.IsValidForUpdate ?? false)
						Console.Write("u\t");
					else
						Console.Write("\t");
				}
				if (showBadAttrs || (at.IsValidForCreate ?? false))
					Console.WriteLine(at.LogicalName);
			}

			if ("" == null) {

			}

			// TODO: write out a CSV for the fields

			pauseDebugger();
		}

		static void Help() {
			Console.WriteLine(@"Usage:
DataDownloader [-h] [-o URI [user pass]] [-t type ...] -d | -u [file ...]
	-h Help: Display this help
	-o Org: URI [user pass]
		Connects to the CRM specified by URI with user pass.
		If user pass isn't specified, will use Default Network Creds.
	-t Target: What type of things to download (see -t ?)
");
		}

		protected override Dictionary<string, Func<string[], int>> getArgDic() {
			return new Dictionary<string, Func<string[], int>>() {
				
			};
		}

		protected override void SubMain() {
		}
	}
}
