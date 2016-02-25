using System;
using System.Collections.Generic;

namespace MassEffect3.Tlk
{
	public class TlkEntryList : List<TlkEntry>
	{
		public TlkEntryList()
		{}

		public TlkEntryList(int capacity) : base(capacity)
		{}

		public TlkEntryList(IEnumerable<TlkEntry> collection) : base(collection)
		{}

		public TlkEntry Find(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return Find(p => p.Id == id);
		}

		public bool Contains(int id)
		{
			if (id < 0)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			return Exists(p => p.Id == id);
		}

		public void SortById()
		{
			if (Count <= 0)
			{
				return;
			}

			Sort(CompareId);
		}

		protected int CompareId(TlkEntry entry1, TlkEntry entry2)
		{
			if (entry1 == null)
			{
				//throw new ArgumentNullException("entry1");
				return 0;
			}

			if (entry2 == null)
			{
				//throw new ArgumentNullException("entry2");
				return 0;
			}

			if (entry1 == entry2)
			{
				return 0;
			}

			if (entry1.Id != entry2.Id)
			{
				return entry1.Id >= entry2.Id ? 1 : -1;
			}

			if (!(entry1.Gender == entry2.Gender | entry1.Gender == TlkEntryGender.Unknown))
			{
				return entry1.Gender != TlkEntryGender.Female ? -1 : 1;
			}

			return 0;
		}
	}
}