using CEC.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Crm.Sdk.Messages;

[assembly:CecType(typeof(CEC.DataDownloader.CLI.DataDownloader))]
namespace CEC.DataDownloader.CLI {
	public class DataDownloader : ProgramBase {

		static bool showUnimportable = false;
		static bool showBadAttrs = false;

		public override string ShortName { get { return "datadl"; } }

		public static void Main(string[] args) {
			new DataDownloader().Start(args);
		}

		public static int Test(string[] args) {
			return 0;
		}

		[Obsolete]
		public static int Orher(string[] args) {
			return 0;
		}

		public override void Execute(string[] args) {
			Func<string[], int> eag = Test;
			Func<string[], int> eagg = Orher;
			System.Reflection.MethodInfo info = eag.Method;
			System.Reflection.MethodInfo inffo = eagg.Method;


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
			
			// TODO: write out a CSV for the fields
		}

		public override void Help() {
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
		
	}
}
