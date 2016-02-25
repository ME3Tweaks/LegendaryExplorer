using System;
using System.Collections.Generic;

namespace MassEffect3.Conditionals
{
	public class ConditionalEntries : List<ConditionalEntry>
	{
		public ConditionalEntries() {}

		public ConditionalEntries(int capacity)
			: base(capacity) {}

		public ConditionalEntries(IEnumerable<ConditionalEntry> collection)
			: base(collection) {}

		/// <summary>
		/// Determines whether an element with the id is in the <see cref="T:MassEffect3.Conditionals.ConditionalEntries"/>.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>	
		public bool Contains(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return Exists(entry => entry.Id == id);
		}

		/// <summary>
		/// Searches for the specified id and returns the zero-based index of the first occurrence within the entire <see cref="T:MassEffect3.Conditionals.ConditionalEntries"/>.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public int IndexOf(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return FindIndex(id);
		}

		/// <summary>
		/// Searches for an element that matches the id, and returns the first occurrence within the entire <see cref="T:MassEffect3.Conditionals.ConditionalEntries"/>.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public ConditionalEntry Find(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return Find(entry => entry.Id == id);
		}

		/// <summary>
		/// Searches for an element that matches the id, and returns the zero-based index of the first occurrence within the entire <see cref="T:MassEffect3.Conditionals.ConditionalEntries"/>.
		/// </summary>
		/// <param name="id"></param>
		/// <returns></returns>
		public int FindIndex(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return FindIndex(entry => entry.Id == id);
		}
	}
}
