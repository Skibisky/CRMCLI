using CEC.Extensions;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace CEC.DocumentTemplates.DCE {
	public static class DocumentTemplater {
		public static void GetDocumentTemplates(IOrganizationService orgService, params string[] files) {
			if (orgService == null)
				throw new ArgumentNullException("orgService", "Provided null Organization Service while attempting to Get Document Templates");

			List<DocumentTemplate> docTemps = new List<DocumentTemplate>();

			var req = orgService.GetItems("documenttemplate", LogicalOperator.Or);

			if (files != null && files.Length > 0) {
				foreach (var f in files) {
					var ff = new FileInfo(f);
					req.AddConditions(new ConditionExpression("name", ConditionOperator.Equal, ff.Name.Replace(ff.Extension, "")));
				}
			}
			docTemps.AddRange(req.Retrieve<DocumentTemplate>());

			Directory.CreateDirectory("Document Templates");
			foreach (var q in docTemps) {
				var location = "Document Templates/" + q["name"];
				File.WriteAllBytes(location + ".zip", Convert.FromBase64String(q.Attributes["content"].ToString()));
				using (ZipArchive zip = ZipFile.OpenRead("Document Templates/" + q["name"] + ".zip")) {
					Console.WriteLine("Extracting " + q["name"]);
					if (Directory.Exists(location))
						Directory.Delete(location, true);
					zip.ExtractToDirectory(location);
				}
				File.Delete(location + ".zip");
			}
		}

		public static void CompileTemplate(string fname) {
			DirectoryInfo d = null;
			if (Directory.Exists("Document Templates/" + fname))
				d = new DirectoryInfo("Document Templates/" + fname);
			else if (Directory.Exists(fname))
				d = new DirectoryInfo(fname);

			if (d != null) {
				if (File.Exists(d.Name))
					File.Delete(d.Name + ".docx");
				d.ToZip(zipExt: ".docx");
				Console.WriteLine("Compiled " + d.Name + ".docx");
			}
		}

		public static void DecompileTemplate(string fname) {
			FileInfo f = new FileInfo(fname + ".docx");
			DirectoryInfo d = new DirectoryInfo("Document Templates/" + f.Name.Replace(".docx", ""));

			using (ZipArchive zip = ZipFile.OpenRead(f.FullName)) {
				Console.WriteLine("Extracting " + f.Name + " to " + d.FullName.Replace(Environment.CurrentDirectory, ""));
				if (Directory.Exists(d.FullName))
					Directory.Delete(d.FullName, true);
				zip.ExtractToDirectory(d.FullName);
			}
		}

		public static void UploadTemplate(IOrganizationService orgService, string fname, bool backup = false) {
			if (orgService == null)
				throw new ArgumentNullException("orgService", "Provided null Organization Service while attempting to Upload Templates");

			FileInfo d = new FileInfo(new FileInfo(fname).Name + ".docx");

			if (File.Exists(d.FullName)) {
				var qw = orgService.RetrieveMultiple(new QueryExpression("documenttemplate") { ColumnSet = new ColumnSet(true) }).Entities
					.FirstOrDefault(r => r.GetAttributeValue<string>("name") == d.Name.Replace(d.Extension, ""));

				if (qw != null) {
					if (backup) {
						var uri = ((OrganizationServiceProxy)orgService).ServiceConfiguration.CurrentServiceEndpoint.ListenUri;
						var loc = uri.Host + string.Join("_", uri.Segments.Take(2).Select(s => s.Replace("/", "")));

						var bd = Directory.CreateDirectory(Path.Combine(Environment.CurrentDirectory, "Template Backups", loc));
						DateTime mod = (DateTime)qw["modifiedon"];
						Console.WriteLine("Backed up " + d.Name);
						File.WriteAllBytes(Path.Combine(bd.FullName, d.Name) + mod.ToString("yyyyMMdd_HHmm") + ".docx", Convert.FromBase64String(qw["content"].ToString()));
					}

					var newContent = Convert.ToBase64String(File.ReadAllBytes(d.Name));
					if (newContent.Equals(qw["content"])) {
						Console.WriteLine(d.Name + " is identical to server.");
						return;
					}

					qw["content"] = newContent;
					orgService.Update(qw);
					File.Delete(d.Name);
					Console.WriteLine("Uploaded " + d.Name);
				}
				else {
					Console.Error.WriteLine(d.Name + " not found on Server!");
				}
			}
			else {
				Console.Error.WriteLine("Failed to load " + d.FullName.Replace(Environment.CurrentDirectory, ""));
			}
		}
	}

	class DocumentTemplate : CEC.Extensions.ExtensionMethods.EntityNamed {
		public DocumentTemplate() { }
		public DocumentTemplate(Entity e) {
			Id = e.Id;
			Attributes = e.Attributes;
		}
	}
}
