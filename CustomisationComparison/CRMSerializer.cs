using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Diagnostics;
using System.Xml;
using System.Collections;

namespace CEC.CustomisationComparer.DCE
{
	public abstract class CRMSerializer
	{
		public abstract void Write(XmlDocument rootDoc, object o, string name = null, XmlElement currentElement = null);

		public static Type GetSerializer(Type type)
		{
			var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => typeof(CRMSerializer).IsAssignableFrom(t));
			types = types.Concat(Assembly.GetCallingAssembly().GetTypes().Where(t => typeof(CRMSerializer).IsAssignableFrom(t)));
			var tt = types.Where(t => t.BaseType.IsConstructedGenericType && t.BaseType.GenericTypeArguments.First() == type);
			return tt?.FirstOrDefault() ?? typeof(ObjectSerializer);
		}

		static List<int> test = new List<int>();

		public static void Serialize(XmlDocument rootDoc, object o, string name = null, XmlElement currentElement = null)
		{
			if (o == null)
				return;
			if (test.Contains(o.GetHashCode()))
				return;
			test.Add(o.GetHashCode());
			var wr = GetSerializer(o.GetType());
			var ds = (CRMSerializer)wr.GetConstructor(new Type[] { }).Invoke(null);
			ds.Write(rootDoc, o, name, currentElement);
		}
	}

	public abstract class CRMSerializer<T> : CRMSerializer
	{
	}

	public class ObjectSerializer : CRMSerializer<object>
	{
		public override void Write(XmlDocument rootDoc, object o, string name = null, XmlElement currentElement = null)
		{
			name = (name ?? o.GetType().ToString()).Replace("<", "").Replace(">", "").Replace("[", "").Replace("]", "");
			if (o.GetType().IsValueType)
			{
				currentElement.SetAttribute(name, o.ToString());
			}
			else
			{
				XmlElement xe = rootDoc.CreateElement(name);
				if (currentElement != null)
					currentElement.AppendChild(xe);
				else
					rootDoc.AppendChild(xe);

				if (o.GetType().GetInterfaces().Contains(typeof(IEnumerable)))
				{
					foreach (var thing in (IEnumerable)o)
					{
						CRMSerializer.Serialize(rootDoc, thing, null, xe);
					}
				}
				else
				{
					var fields = o.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var thing in fields)
					{
						CRMSerializer.Serialize(rootDoc, thing.GetValue(o), thing.Name, xe);
					}
					var prop = o.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					foreach (var thing in prop)
					{
						CRMSerializer.Serialize(rootDoc, thing.GetValue(o), thing.Name, xe);
					}
				}
			}

		}
	}

	public class StringSerializer : CRMSerializer<string>
	{
		public override void Write(XmlDocument rootDoc, object o, string name = null, XmlElement currentElement = null)
		{
			name = (name ?? o.GetType().ToString()).Replace("<", "").Replace(">", "").Replace("[", "").Replace("]", "");
			var xe = rootDoc.CreateElement(name);
			xe.InnerText = o.ToString();
			if (currentElement != null)
				currentElement.AppendChild(xe);
			else
				rootDoc.AppendChild(xe);
		}
	}


}
