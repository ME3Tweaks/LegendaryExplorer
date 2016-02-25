using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MassEffect3.Core.IO;

namespace MassEffect3.Conditionals
{
	public class ConditionalsReader : IDisposable
	{
		private readonly DataReader _dataReader;
		private ConditionalsFile _conditionals;
		private byte[] _rawBytes;
		private bool _leaveOpen;

		protected ConditionalsReader(bool leaveOpen = false)
		{
			_conditionals = new ConditionalsFile();
			_rawBytes = new byte[]{};
			_leaveOpen = leaveOpen;
		}

		public ConditionalsReader(Stream input, bool leaveOpen = false)
			: this(leaveOpen)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}

			_dataReader = new DataReader(input, leaveOpen: leaveOpen);
		}

		public ConditionalsReader(string path, bool leaveOpen = false)
			: this(new FileStream(path, FileMode.Open), leaveOpen) {}

		private static void CalculateEntriesSize(IList<ConditionalEntry> entries, long fileSize)
		{
			if (entries == null)
			{
				throw new ArgumentNullException("entries");
			}

			if (fileSize < 0)
			{
				throw new ArgumentOutOfRangeException("fileSize");
			}

			for (var i = 0; i < entries.Count(); i++)
			{
				var size1 = 0;
				var size2 = entries[i].Offset;

				if (i == entries.Count - 1)
				{
					size1 = (int) fileSize;
				}
				else
				{
					for (var j = i + 1; j < entries.Count(); j++)
					{
						if (entries[j].Offset <= size2)
						{
							continue;
						}

						size1 = entries[j].Offset;

						break;
					}
				}

				entries[i].Size = size1 - size2;
			}
		}

		public static ConditionalsFile Load(string path)
		{
			var conditionalsFile = new ConditionalsFile();

			using (var reader = new DataReader(new FileStream(path, FileMode.Open)))
			{
				var _buffer = reader.ReadBytes((int) reader.Length);

				reader.Seek(0);

				var headerId = reader.ReadInt32();

				if (headerId != ConditionalsFile.ValidHeaderId)
				{
					reader.Close();

					return null;
				}

				var version = reader.ReadInt32();

				if (version != 1)
				{
					reader.Close();

					return null;
				}

				var unknownInt16 = reader.ReadInt16();
				var count = reader.ReadInt16();
				var entries = new ConditionalEntries();

				for (var i = 0; i < count; i++)
				{
					var temp = new ConditionalEntry
					{
						Id = reader.ReadInt32(),
						Offset = reader.ReadInt32(),
						ListOffset = i * 8 + 12,
						Size = -1
					};

					entries.Add(temp);
				}

				// Sort by Offset
				entries = new ConditionalEntries(entries.OrderBy(entry => entry.Offset));

				CalculateEntriesSize(entries, reader.Length);

				foreach (var entry in entries)
				{
					entry.Data = new byte[entry.Size];

					reader.Seek(entry.Offset);
					reader.Read(entry.Data, 0, entry.Size);
				}

				// Sort by Id
				conditionalsFile.Entries = new ConditionalEntries(entries.OrderBy(entry => entry.Id));
			}

			return conditionalsFile;
		}

		#region Implementation of IDisposable

		public void Dispose()
		{
			Dispose(true);
		}

		protected void Dispose(bool disposing)
		{
			if (disposing)
			{
				if (_dataReader != null)
				{
					_dataReader.Close();
				}
			}

			_conditionals = null;
		}

		#endregion
	}
}
