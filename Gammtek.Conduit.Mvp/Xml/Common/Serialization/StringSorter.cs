using System.Collections.Generic;

namespace Gammtek.Conduit.Mvp.Xml.Common.Serialization
{
	public class StringSorter
	{
		private readonly List<string> _list = new List<string>();

		public void AddString(string s)
		{
			_list.Add(s);
		}

		public string[] GetOrderedArray()
		{
			_list.Sort();
			return _list.ToArray();
		}
	}
}
