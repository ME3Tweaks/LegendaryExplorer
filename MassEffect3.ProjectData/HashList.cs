using System.Collections.Generic;

namespace MassEffect3.ProjectData
{
	public sealed class HashList<TType>
	{
		internal static HashList<TType> Dummy = new HashList<TType>();
		internal Dictionary<TType, string> Lookup;

		internal HashList()
		{
			Lookup = new Dictionary<TType, string>();
		}

		public string this[TType index]
		{
			get
			{
				if (Lookup.ContainsKey(index) == false)
				{
					return null;
				}

				return Lookup[index];
			}
		}

		public bool Contains(TType index)
		{
			return Lookup.ContainsKey(index);
		}

		public IEnumerable<string> GetStrings()
		{
			return Lookup.Values;
		}
	}
}