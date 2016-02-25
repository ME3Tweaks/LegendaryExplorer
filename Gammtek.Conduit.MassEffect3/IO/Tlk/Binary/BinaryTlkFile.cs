using System;
using System.Collections.Generic;
using System.IO;
using Gammtek.Conduit.Extensions;

namespace Gammtek.Conduit.IO.Tlk.Binary
{
	public class BinaryTlkFile : TlkFile
	{
		public BinaryTlkFile(IList<TlkEntry> entries = null, IList<TlkStringRef> stringRefs = null)
			: base(entries)
		{
			StringRefs = stringRefs ?? new List<TlkStringRef>();
		}

		public BinaryTlkFile(TlkFile other)
			: base(other)
		{
			if (other is BinaryTlkFile)
			{
				StringRefs = (other as BinaryTlkFile).StringRefs;
			}

			if (StringRefs == null)
			{
				StringRefs = new List<TlkStringRef>();
			}
		}

		public IList<TlkStringRef> StringRefs { get; protected set; }

		public static BinaryTlkFile Load(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException();
			}

			return File.Exists(path) ? Load(File.Open(path, FileMode.Open)) : null;
		}

		public static BinaryTlkFile Load(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var reader = new BinaryTlkReader(stream))
			{
				return reader.ToBinaryFile();
			}
		}

		public void Save(string path)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (!File.Exists(path))
			{
				return;
			}

			Save(File.Open(path, FileMode.Create));
		}

		public void Save(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var writer = new BinaryTlkWriter(stream))
			{
				
			}
		}
	}
}
