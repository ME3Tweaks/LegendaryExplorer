using System;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public abstract class XmlWrappingWriter : XmlWriter
	{
		private XmlWriter _baseWriter;

		protected XmlWrappingWriter(XmlWriter baseWriter)
		{
			Guard.ArgumentNotNull(baseWriter, "baseWriter");

			_baseWriter = baseWriter;
		}

		protected XmlWriter BaseWriter
		{
			get { return _baseWriter; }
			set
			{
				Guard.ArgumentNotNull(value, "value");
				_baseWriter = value;
			}
		}

		public override XmlWriterSettings Settings
		{
			get { return _baseWriter.Settings; }
		}

		public override WriteState WriteState
		{
			get { return _baseWriter.WriteState; }
		}

		public override string XmlLang
		{
			get { return _baseWriter.XmlLang; }
		}

		public override XmlSpace XmlSpace
		{
			get { return _baseWriter.XmlSpace; }
		}

		public override void Close()
		{
			_baseWriter.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (WriteState != WriteState.Closed)
			{
				Close();
			}

			((IDisposable) _baseWriter).Dispose();
		}

		public override void Flush()
		{
			_baseWriter.Flush();
		}

		public override string LookupPrefix(string ns)
		{
			return _baseWriter.LookupPrefix(ns);
		}

		public override void WriteBase64(byte[] buffer, int index, int count)
		{
			_baseWriter.WriteBase64(buffer, index, count);
		}

		public override void WriteCData(string text)
		{
			_baseWriter.WriteCData(text);
		}

		public override void WriteCharEntity(char ch)
		{
			_baseWriter.WriteCharEntity(ch);
		}

		public override void WriteChars(char[] buffer, int index, int count)
		{
			_baseWriter.WriteChars(buffer, index, count);
		}

		public override void WriteComment(string text)
		{
			_baseWriter.WriteComment(text);
		}

		public override void WriteDocType(string name, string pubid, string sysid, string subset)
		{
			_baseWriter.WriteDocType(name, pubid, sysid, subset);
		}

		public override void WriteEndAttribute()
		{
			_baseWriter.WriteEndAttribute();
		}

		public override void WriteEndDocument()
		{
			_baseWriter.WriteEndDocument();
		}

		public override void WriteEndElement()
		{
			_baseWriter.WriteEndElement();
		}

		public override void WriteEntityRef(string name)
		{
			_baseWriter.WriteEntityRef(name);
		}

		public override void WriteFullEndElement()
		{
			_baseWriter.WriteFullEndElement();
		}

		public override void WriteProcessingInstruction(string name, string text)
		{
			_baseWriter.WriteProcessingInstruction(name, text);
		}

		public override void WriteRaw(string data)
		{
			_baseWriter.WriteRaw(data);
		}

		public override void WriteRaw(char[] buffer, int index, int count)
		{
			_baseWriter.WriteRaw(buffer, index, count);
		}

		public override void WriteStartAttribute(string prefix, string localName, string ns)
		{
			_baseWriter.WriteStartAttribute(prefix, localName, ns);
		}

		public override void WriteStartDocument()
		{
			_baseWriter.WriteStartDocument();
		}

		public override void WriteStartDocument(bool standalone)
		{
			_baseWriter.WriteStartDocument(standalone);
		}

		public override void WriteStartElement(string prefix, string localName, string ns)
		{
			_baseWriter.WriteStartElement(prefix, localName, ns);
		}

		public override void WriteString(string text)
		{
			_baseWriter.WriteString(text);
		}

		public override void WriteSurrogateCharEntity(char lowChar, char highChar)
		{
			_baseWriter.WriteSurrogateCharEntity(lowChar, highChar);
		}

		public override void WriteValue(bool value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(DateTime value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(decimal value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(double value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(int value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(long value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(object value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(float value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteValue(string value)
		{
			_baseWriter.WriteValue(value);
		}

		public override void WriteWhitespace(string ws)
		{
			_baseWriter.WriteWhitespace(ws);
		}
	}
}
