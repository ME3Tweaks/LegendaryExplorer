using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using Gammtek.Conduit.Extensions;

namespace Gammtek.Conduit.IO.Tlk.Xml
{
	public class XmlTlkFile : TlkFile
	{
		public XmlTlkFile(IList<TlkEntry> entries = null)
			: base(entries) {}

		public XmlTlkFile(TlkFile other)
			: base(other) { }

		/*public static XmlTlkFile FromBinaryFile(BinaryTlkFile binaryTlkFile)
		{
			if (binaryTlkFile == null)
			{
				throw new ArgumentNullException("binaryTlkFile");
			}

			XElement rootElement, includesElement, stringsElement;

			var xDoc = new XDocument(rootElement = new XElement("TlkFile",
				includesElement = new XElement("Includes"),
				stringsElement = new XElement("Strings")));

			foreach (var stringRef in binaryTlkFile.StringRefs)
			{
				if (stringRef.Position == binaryTlkFile.Header.Entry1Count)
				{
					// End of male entries section
				}

				stringsElement.Add(new XElement("String", ((stringRef.Offset < 0) ? "-1" : stringRef.Data), new XAttribute("id", stringRef.Id)));
			}

			return new XmlTlkFile(xDoc);
		}*/

		public static XmlTlkFile Load(string path, LoadOptions options = LoadOptions.None)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}
			
			return File.Exists(path) ? Load(XDocument.Load(path, options)) : null;
		}

		public static XmlTlkFile Load(Stream stream, LoadOptions options = LoadOptions.None)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			return Load(XDocument.Load(stream, options));
		}

		public static XmlTlkFile Load(XDocument doc)
		{
			if (doc == null)
			{
				throw new ArgumentNullException(nameof(doc));
			}

			var tlkFile = new XmlTlkFile();



			return tlkFile;
		}

		public void Save(string path, XmlWriterSettings settings = null)
		{
			if (path.IsNullOrEmpty())
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (!File.Exists(path))
			{
				return;
			}

			using (var writer = XmlWriter.Create(File.Open(path, FileMode.Create), settings))
			{
				Save(writer);
			}
		}

		public void Save(Stream stream, XmlWriterSettings settings = null)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			using (var writer = XmlWriter.Create(stream, settings))
			{
				Save(writer);
			}
		}

		public void Save(XmlWriter writer)
		{
			if (writer == null)
			{
				throw new ArgumentNullException(nameof(writer));
			}

			var xDoc = new XDocument();

			

			xDoc.Save(writer);
		}

		protected IList<TlkEntry> ReadTlkEntries()
		{
			return null;
		}

		protected TlkEntry ReadTlkEntry()
		{
			return null;
		}

		protected void WriteTlkEntries(IList<TlkEntry> entries)
		{
			
		}

		protected void WriteTlkEntry(TlkEntry entry)
		{
			
		}
	}
}
