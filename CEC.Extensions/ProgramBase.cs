using Microsoft.Xrm.Sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CEC.Extensions
{
	public abstract class ProgramBase
	{
		public abstract Dictionary<string, Func<string[], int>> getArgDic();
		private static Dictionary<string, Func<string[], int>> commlines;
		protected static List<string> files = new List<string>();
		protected static IOrganizationService OrgService = null;

		public ProgramBase()
		{
			commlines = getArgDic();
		}

		protected static void ParseArgs(params string[] args)
		{
			int i = 0;
			for (; i < args.Length; i++)
			{
				if (args[i][0] == '-')
				{
					if (commlines.ContainsKey(args[i].Substring(1)))
						i += commlines[args[i].Substring(1)].Invoke(args.Skip(i).ToArray());
					else
					{
						var kv = commlines.Where(p => p.Key[0] == args[i][1]);
						if (kv.Count() == 1)
						{
							var aa = args.Skip(i + 1).ToArray();
							i += kv.FirstOrDefault().Value.Invoke(aa);
						}
						else
						{
							Console.WriteLine("Couldn't match:");
							foreach (var p in kv)
							{
								Console.WriteLine("\t" + p.Key);
							}
						}
					}
				}
				else if (File.Exists(args[i]))
				{
					Console.WriteLine("Adding file " + args[i]);
					files.Add(args[i]);
				}
				else
				{
					Console.WriteLine("Ignoring " + args[i]);
				}
			}
		}

		protected static int ConnectArgs(string[] args)
		{
			Uri u;
			if (!Uri.TryCreate(args[0], UriKind.Absolute, out u))
				return 0;

			Console.WriteLine("Connecting to " + args[0]);

			if (args.Length == 1 || args[1][0] == '-')
			{
				OrgService = ExtensionMethods.Connect(args[0]);
				return 1;
			}
			OrgService = ExtensionMethods.Connect(args[0], args[1], args[2]);
			return 3;
		}

		protected static int GetFiles(string[] args)
		{
			int i = 0;
			while (i < args.Length && args[i][0] != '-')
			{
				files.Add(args[i].Replace(".docx", ""));
				Console.WriteLine("Adding file: " + args[i].Replace(Environment.CurrentDirectory, ""));
				i++;
			}
			return i;
		}

		[System.AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = true)]
		sealed class CommandLineArgumentAttribute : Attribute
		{
			// See the attribute guidelines at 
			//  http://go.microsoft.com/fwlink/?LinkId=85236
			readonly string argumentName;
			readonly bool canBeShort;

			// This is a positional argument
			public CommandLineArgumentAttribute(string argName, bool canBeShort = false)
			{
				this.argumentName = argName;
				this.canBeShort = canBeShort;
			}

			public string PositionalString
			{
				get { return argumentName; }
			}

			public bool CanBeShort
			{
				get { return canBeShort; }
			}
		}
	}
}