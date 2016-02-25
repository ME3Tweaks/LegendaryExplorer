using System;
using System.Collections.Generic;
using System.Windows.Documents;

namespace MassEffect3.TlkEditor
{
	public class TlkStrings : List<TlkString>
	{
		public TlkStrings()
		{}

		public TlkStrings(int capacity) : base(capacity)
		{}

		public TlkStrings(IEnumerable<TlkString> collection) : base(collection)
		{}

		public char[] ToCharArray(bool ignoreNegativeIds = true)
		{
			var list = new List<char>();

			foreach (var str in this)
			{
				if (ignoreNegativeIds && str.Id < 0)
				{
					continue;
				}

				list.AddRange(str.Value.ToCharArray());
			}

			return list.ToArray();
		}
	}
}