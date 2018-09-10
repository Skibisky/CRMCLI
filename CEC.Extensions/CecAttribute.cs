using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEC.Extensions {
	[System.AttributeUsage(AttributeTargets.Assembly, Inherited = false, AllowMultiple = true)]
	public sealed class CecTypeAttribute : Attribute {
		readonly Type cectype;

		public CecTypeAttribute(Type type) {
			this.cectype = type;
		}

		public Type CecType {
			get { return cectype; }
		}
	}

	[System.AttributeUsage(AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
	public sealed class CecNoOrgAttribute : Attribute {
	}
}
