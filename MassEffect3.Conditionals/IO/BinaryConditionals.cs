using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Gammtek.Conduit.Extensions;

namespace MassEffect3.Conditionals.IO
{
	public class BinaryConditionals : Conditionals
	{
		public const uint HeaderSignature = 0x434F4E44;

		public new BinaryConditionalEntries Entries { get; set; }

		public static BinaryConditionals Load(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException("path");
			}

			return !File.Exists(path) ? null : Load(File.Open(path, FileMode.Open));
		}

		public static BinaryConditionals Load(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException("stream");
			}

			var conditionals = new BinaryConditionals();

			using (var reader = new BinaryConditionalsReader(stream))
			{
				var headerSignature = reader.ReadUInt32();

				if (headerSignature != HeaderSignature)
				{
					throw new FormatException("Not a proper conditionals file.");
				}

				var version = reader.ReadUInt32();

				if (version != 1)
				{
					throw new FormatException("Not a proper conditionals version.");
				}

				conditionals.Version = version;

				var unknown1 = reader.ReadInt16();
				var entryCount = reader.ReadInt16();

				var entries = new List<BinaryConditionalEntry>();

				for (var i = 0; i < entryCount; i++)
				{
					var entry = new BinaryConditionalEntry
					{
						Id = reader.ReadInt32(), 
						Offset = reader.ReadInt32()
					};

					entries.Add(entry);
				}

				entries = entries
					.OrderBy(o => o.Offset)
					.Distinct().ToList();

				for (var i = 0; i < entries.Count; i++)
				{
					var entry = entries[i];

					if (entry.Offset <= 0)
					{
						continue;
					}

					var nextOffset = i + 1 < entries.Count
						? entries[i + 1].Offset
						: reader.Length;

					reader.Seek(entry.Offset);

					entry.Size = (int)(nextOffset - entry.Offset);
					entry.Data = reader.ReadBytes(entry.Size);
				}

				entries = entries.OrderBy(entry => entry.Id).ToList();
				conditionals.Entries = new BinaryConditionalEntries(entries);
			}

			return conditionals;
		}
	}
}