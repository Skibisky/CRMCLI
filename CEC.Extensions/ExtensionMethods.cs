using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.ServiceModel.Description;

namespace CEC.Extensions
{
	public static class ExtensionMethods
	{
		public class OrgQuery
		{
			private readonly IOrganizationService orgService;
			private QueryExpression qe =  null;

			public OrgQuery(IOrganizationService service)
			{
				this.orgService = service;
			}

			public OrgQuery SetColumns(params string[] cols)
			{
				qe.ColumnSet = new ColumnSet(cols);
				return this;
			}

			public OrgQuery SetColumns(bool allCols)
			{
				qe.ColumnSet = new ColumnSet(allCols);
				return this;
			}

			public OrgQuery AddConditions(params ConditionExpression[] conds)
			{
				qe.Criteria.Conditions.AddRange(conds);
				return this;
			}

			public OrgQuery AddFilters(params FilterExpression[] filts)
			{
				qe.Criteria.Filters.AddRange(filts);
				return this;
			}

			public DataCollection<Entity> Retrieve()
			{
				return orgService.RetrieveMultiple(qe).Entities;
			}

			public IEnumerable<T> Retrieve<T>() where T : Entity, new()
			{
				return orgService.RetrieveMultiple(qe).Entities
					.Select(e => new T()
					{
						Id = e.Id,
						Attributes = e.Attributes,
						LogicalName = e.LogicalName,
					});
			}
		}

		public class EntityNamed : Entity
		{
			public override string ToString()
			{
				if (!string.IsNullOrWhiteSpace(GetAttributeValue<string>("name")))
					return GetAttributeValue<string>("name");
				else
					return Id.ToString();
			}
		}

		public static OrgQuery GetItems(this IOrganizationService orgService, string logicalName, LogicalOperator baseOp = LogicalOperator.And)
		{
			return new OrgQuery(orgService).SetColumns(true).AddFilters(new FilterExpression(baseOp));
		}

		public static IOrganizationService Connect(string crmuri, string user = null, string pass = null)
		{
			ClientCredentials creds = new ClientCredentials();
			if (user == null)
				creds.Windows.ClientCredential = CredentialCache.DefaultNetworkCredentials;
			else
			{
				creds.UserName.UserName = user;
				creds.UserName.Password = pass;
			}
			return new OrganizationServiceProxy(new Uri(crmuri + "/XRMServices/2011/Organization.svc"), null, creds, null);
		}

		// Sweet splits from
		// https://stackoverflow.com/questions/298830/split-string-containing-command-line-parameters-into-string-in-c-sharp/298990#298990
		public static IEnumerable<string> SplitCommandLine(string commandLine)
		{
			bool inQuotes = false;

			return commandLine.Split(c =>
				{
					if (c == '\"')
						inQuotes = !inQuotes;

					return !inQuotes && c == ' ';
				})
				.Select(arg => arg.Trim().TrimMatchingQuotes('\"'))
				.Where(arg => !string.IsNullOrEmpty(arg));
		}

		public static IEnumerable<string> Split(this string str, Func<char, bool> controller)
		{
			int nextPiece = 0;

			for (int c = 0; c < str.Length; c++)
			{
				if (controller(str[c]))
				{
					yield return str.Substring(nextPiece, c - nextPiece);
					nextPiece = c + 1;
				}
			}

			yield return str.Substring(nextPiece);
		}

		public static string TrimMatchingQuotes(this string input, char quote)
		{
			if ((input.Length >= 2) &&
				(input[0] == quote) && (input[input.Length - 1] == quote))
				return input.Substring(1, input.Length - 2);

			return input;
		}

		public static string ToZip(this DirectoryInfo d, string zipName = null, string zipExt = "zip", string basepath = null)
		{
			if (d == null)
				throw new ArgumentNullException("Target Directory was null");
			if (zipName == null)
				zipName = d.Name;
			if (basepath == null)
				basepath = Environment.CurrentDirectory;

			/// Inbuilt ZIP functions don't give a Dynamics CRM readable archive, but System ZIP function does
			// https://www.codeproject.com/Articles/12064/Compress-Zip-files-with-Windows-Shell-API-and-C
			byte[] emptyzip = new byte[] { 80, 75, 5, 6, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
			var path = Path.Combine(basepath, zipName + ".zip");
			FileStream fs = File.Create(path);
			fs.Write(emptyzip, 0, emptyzip.Length);
			fs.Flush();
			fs.Close();
			fs = null;
			Shell32.ShellClass sc = new Shell32.ShellClass();
			Shell32.Folder SrcFlder = sc.NameSpace(d.FullName);
			Shell32.Folder DestFlder = sc.NameSpace(path);
			Shell32.FolderItems items = SrcFlder.Items();
			DestFlder.CopyHere(items, 20);

			// TODO: FIXME: stop waiting for explorer to finish zipping
			do
			{
				System.Threading.Thread.Sleep(260);
			} while (new FileInfo(path).Length < 2 * 1024);

			if (zipExt != "zip")
			{
				File.Copy(path, Path.Combine(basepath, zipName + zipExt));
				File.Delete(path);
			}

			return Path.Combine(basepath, zipName + zipExt);
		}
	}
}
