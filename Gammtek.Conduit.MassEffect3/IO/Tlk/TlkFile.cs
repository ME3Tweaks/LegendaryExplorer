using System;
using System.Collections.Generic;
using System.Linq;

namespace Gammtek.Conduit.IO.Tlk
{
	public class TlkFile
	{
		public TlkFile(IList<TlkEntry> entries = null)
		{
			Entries = entries ?? new TlkEntries();
		}

		public TlkFile(TlkFile other)
		{
			if (other == null)
			{
				throw new ArgumentNullException(nameof(other));
			}

			Entries = other.Entries;
			MinVersion = other.MinVersion;
			Version = other.Version;
		}

		public IList<TlkEntry> Entries { get; protected set; }

		public int MinVersion { get; protected set; }

		public int Version { get; protected set; }

		public IList<TlkEntry> FemaleEntries
		{
			get { return Entries.Where(entry => entry.Gender == TlkEntryGender.Female) as IList<TlkEntry>; }
		}

		public IList<TlkEntry> MaleEntries
		{
			get { return Entries.Where(entry => entry.Gender == TlkEntryGender.Male) as IList<TlkEntry>; }
		}
	}
}
