using System;
using System.Collections.Generic;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System.Xml;
using System.IO;
using Microsoft.Xrm.Sdk;
using CEC.CustomisationComparer.DCE;

namespace CEC.CustomisationComparer.CLI {
	class Test {
		int value = 3;
		public string key = "topkek";
		public KeyValuePair<string, int> rep = new KeyValuePair<string, int>("topkek", 3);
	}

	class EntityMetaDataSerializer : CRMSerializer<EntityMetadata> {
		public override void Write(XmlDocument rootDoc, object o, string name = null, XmlElement currentElement = null) {
			var m = (EntityMetadata)o;
			var xe = rootDoc.CreateElement("Entity");
			if (currentElement == null)
				rootDoc.AppendChild(xe);
			else
				currentElement.AppendChild(xe);

			var e = rootDoc.CreateElement("ObjectTypeCode");
			e.InnerText = m.ObjectTypeCode.ToString();
			xe.AppendChild(e);

		}
	}

	class Program {

		static void Main(string[] args) {
			var org1 = Extensions.ExtensionMethods.Connect("http://iscrmdev2013.cloudapp.net/IS2", "samuel.warnock", "80Barrack");

			var me = ((WhoAmIResponse)org1.Execute(new WhoAmIRequest())).UserId;

			/**/
			var resp = org1.Execute(new RetrieveAllEntitiesRequest() {
				EntityFilters = EntityFilters.All
			});
			var res1 = resp.Results["EntityMetadata"];

			XmlDocument doc = new XmlDocument();
			using (TextWriter tw = new StringWriter())
			using (XmlWriter xw = XmlWriter.Create(tw, new XmlWriterSettings() {
				Indent = true,
				IndentChars = "\t"
			})) {
				CRMSerializer.Serialize(doc, res1, "MetaData");
				doc.WriteTo(xw);
				xw.Flush();
				Console.WriteLine(tw.ToString());
				File.WriteAllText("sol.xml", tw.ToString());
			}

			Console.ReadKey();
			return;

			/**/

			List<EntityReference> forms = new List<EntityReference>();
			for (int i = 0; i < 10; i++) {

				var res1b = (RetrieveFilteredFormsResponse)org1.Execute(new RetrieveFilteredFormsRequest() {
					EntityLogicalName = "incident",
					FormType = new Microsoft.Xrm.Sdk.OptionSetValue(i),
					SystemUserId = me
				});
				forms.AddRange(res1b.SystemForms);
			}
			foreach (var er in forms) {
				var e = org1.Retrieve(er.LogicalName, er.Id, new ColumnSet(true));
			}
		}
	}
}
