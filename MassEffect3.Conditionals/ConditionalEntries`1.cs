using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class ConditionalEntries<T> : List<T>
		where T : ConditionalEntry
	{
		public ConditionalEntries() {}

		public ConditionalEntries(int capacity) 
			: base(capacity) {}

		public ConditionalEntries(IEnumerable<T> collection) 
			: base(collection) {}

		public bool Contains(int id)
		{
			return Exists(entry => entry.Id == id);
		}

		public T Find(int id)
		{
			return Find(entry => entry.Id == id);
		}

		public int FindIndex(int id)
		{
			return FindIndex(entry => entry.Id == id);
		}

		public int IndexOf(int id)
		{
			return FindIndex(id);
		}
	}
}