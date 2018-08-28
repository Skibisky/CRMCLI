using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CEC.Extensions {
	public class DirectoryStack {
		Stack<string> directories = new Stack<string>();

		public DirectoryStack(string initial = null) {
			if (!string.IsNullOrWhiteSpace(initial)) {
				foreach (var s in initial.Split('/')) {
					directories.Push(s);
				}
			}
		}

		public void Push(string folder) {
			if (!string.IsNullOrWhiteSpace(folder)) {
				foreach (var s in folder.Split('/')) {
					directories.Push(s);
				}
			}
		}

		public void Pop(int depth = 1) {
			for (int i = 0; i < depth; i++) {
				directories.Pop();
			}
		}
		
		public static implicit operator string(DirectoryStack rhs) {
			return rhs.ToString();
		}

		public override string ToString() {
			return string.Join("/", directories.Reverse().ToArray());
		}
	}
}
