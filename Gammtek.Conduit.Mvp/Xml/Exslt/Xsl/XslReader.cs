using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.XPath;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class XslReader : XmlReader
	{
		private const string NsXml = "http://www.w3.org/XML/1998/namespace";
		private const string NsXmlNs = "http://www.w3.org/2000/xmlns/";
		private const int DefaultBufferSize = 256;
		private static readonly XmlReaderSettings ReaderSettings;
		private static readonly char[] QNameSeparator = { ':' };
		private readonly bool _multiThread;
		private readonly XmlNameTable _nameTable;
		private readonly TokenPipe _pipe;
		private readonly ScopeManager _scope;
		private readonly BufferWriter _writer;
		private XsltArgumentList _args;
		private int _attCount;
		private int _attOffset; // 0 - means reader is positioned on element, when reader potitionrd on the first attribute attOffset == 1
		private XmlInput _defaulDocument;
		private int _depth;
		private XmlNodeType _nodeType = XmlNodeType.None;
		private QName _qname;
		private ReadState _readState = ReadState.Initial;
		private Thread _thread;
		private string _value;

		static XslReader()
		{
			ReaderSettings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Prohibit
			};
		}

		public XslReader(XslCompiledTransform xslTransform, bool multiThread, int initialBufferSize)
		{
			XslCompiledTransform = xslTransform;
			_multiThread = multiThread;
			InitialBufferSize = initialBufferSize;

			_nameTable = new NameTable();
			_pipe = _multiThread ? new TokenPipeMultiThread(initialBufferSize) : new TokenPipe(initialBufferSize);
			_writer = new BufferWriter(_pipe, _nameTable);
			_scope = new ScopeManager(_nameTable);
			SetUndefinedState(ReadState.Initial);
		}

		public XslReader(XslCompiledTransform xslTransform)
			: this(xslTransform, true, DefaultBufferSize) {}

		public XslCompiledTransform XslCompiledTransform { get; set; }

		public int InitialBufferSize { get; set; }

		public override int AttributeCount
		{
			get { return _attCount; }
		}

		public override string BaseURI
		{
			get { return string.Empty; }
		}

		public override XmlNameTable NameTable
		{
			get { return _nameTable; }
		}

		public override int Depth
		{
			get { return _depth; }
		}

		public override bool EOF
		{
			get { return ReadState == ReadState.EndOfFile; }
		}

		public override bool HasValue
		{
			get { return 0 != ( /*HasValueBitmap:*/0x2659C & (1 << (int) _nodeType)); }
		}

		public override XmlNodeType NodeType
		{
			get { return _nodeType; }
		}

		public override bool IsEmptyElement
		{
			get { return false; }
		}

		public override string LocalName
		{
			get { return _qname.Local; }
		}

		public override string NamespaceURI
		{
			get { return _qname.NsUri; }
		}

		public override string Prefix
		{
			get { return _qname.Prefix; }
		}

		public override string Value
		{
			get { return _value; }
		}

		public override ReadState ReadState
		{
			get { return _readState; }
		}

		public override string XmlLang
		{
			get { return _scope.Lang; }
		}

		public override XmlSpace XmlSpace
		{
			get { return _scope.Space; }
		}

		private void ChangeDepthToElement()
		{
			switch (_nodeType)
			{
				case XmlNodeType.Attribute:
					_depth--;
					break;
				case XmlNodeType.Text:
					if (_attOffset != 0)
					{
						_depth -= 2;
					}
					break;
			}
		}

		public override void Close()
		{
			SetUndefinedState(ReadState.Closed);
		}

		private int FindAttribute(string name)
		{
			if (IsInsideElement())
			{
				string prefix, local;
				var strings = name.Split(QNameSeparator, StringSplitOptions.None);
				switch (strings.Length)
				{
					case 1:
						prefix = string.Empty;
						local = name;
						break;
					case 2:
						if (strings[0].Length == 0)
						{
							return 0; // ":local-name"
						}
						prefix = strings[0];
						local = strings[1];
						break;
					default:
						return 0;
				}
				for (var i = 1; i <= _attCount; i++)
				{
					QName attName;
					string attValue;
					_pipe.GetToken(i, out attName, out attValue);
					if (attName.Local == local && attName.Prefix == prefix)
					{
						return i;
					}
				}
			}
			return 0;
		}

		public override string GetAttribute(int i)
		{
			if (IsInsideElement())
			{
				if (0 <= i && i < _attCount)
				{
					QName attName;
					string attValue;
					_pipe.GetToken(i + 1, out attName, out attValue);
					return attValue;
				}
			}
			throw new ArgumentOutOfRangeException("i");
		}

		public override string GetAttribute(string name)
		{
			var attNum = FindAttribute(name);
			if (attNum != 0)
			{
				return GetAttribute(attNum - 1);
			}
			return null;
		}

		public override string GetAttribute(string name, string ns)
		{
			if (IsInsideElement())
			{
				for (var i = 1; i <= _attCount; i++)
				{
					QName attName;
					string attValue;
					_pipe.GetToken(i, out attName, out attValue);
					if (attName.Local == name && attName.NsUri == ns)
					{
						return attValue;
					}
				}
			}
			return null;
		}

		private bool IsInsideElement()
		{
			return (
				_nodeType == XmlNodeType.Element ||
					_nodeType == XmlNodeType.Attribute ||
					_nodeType == XmlNodeType.Text && _attOffset != 0
				);
		}

		private bool IsWhitespace(string s)
		{
			// Because our xml is presumably valid only and all ws chars <= ' '
			return s.All(c => ' ' >= c);
		}

		public override string LookupNamespace(string prefix)
		{
			return _scope.LookupNamespace(prefix);
		}

		public override bool MoveToAttribute(string name)
		{
			var attNum = FindAttribute(name);
			if (attNum != 0)
			{
				MoveToAttribute(attNum - 1);
				return true;
			}
			return false;
		}

		public override void MoveToAttribute(int i)
		{
			if (IsInsideElement())
			{
				if (0 <= i && i < _attCount)
				{
					ChangeDepthToElement();
					_attOffset = i + 1;
					_depth++;
					_pipe.GetToken(_attOffset, out _qname, out _value);
					_nodeType = XmlNodeType.Attribute;
					return;
				}
			}
			throw new ArgumentOutOfRangeException("i");
		}

		public override bool MoveToAttribute(string name, string ns)
		{
			if (IsInsideElement())
			{
				for (var i = 1; i <= _attCount; i++)
				{
					QName attName;
					string attValue;
					_pipe.GetToken(i, out attName, out attValue);
					if (attName.Local == name && attName.NsUri == ns)
					{
						ChangeDepthToElement();
						_nodeType = XmlNodeType.Attribute;
						_attOffset = i;
						_qname = attName;
						_depth++;
						_value = attValue;
					}
				}
			}
			return false;
		}

		public override bool MoveToElement()
		{
			if (
				_nodeType == XmlNodeType.Attribute ||
					_nodeType == XmlNodeType.Text && _attOffset != 0
				)
			{
				ChangeDepthToElement();
				_nodeType = XmlNodeType.Element;
				_attOffset = 0;
				_pipe.GetToken(0, out _qname, out _value);
				return true;
			}
			return false;
		}

		public override bool MoveToFirstAttribute()
		{
			ChangeDepthToElement();
			_attOffset = 0;
			return MoveToNextAttribute();
		}

		public override bool MoveToNextAttribute()
		{
			if (_attOffset < _attCount)
			{
				ChangeDepthToElement();
				_depth++;
				_attOffset++;
				_pipe.GetToken(_attOffset, out _qname, out _value);
				_nodeType = XmlNodeType.Attribute;
				return true;
			}
			return false;
		}

		public override bool Read()
		{
			// Leave Current node
			switch (_nodeType)
			{
				case XmlNodeType.None:
					if (_readState == ReadState.EndOfFile || _readState == ReadState.Closed)
					{
						return false;
					}
					_readState = ReadState.Interactive;
					break;
				case XmlNodeType.Attribute:
					_attOffset = 0;
					_depth--;
					goto case XmlNodeType.Element;
				case XmlNodeType.Element:
					_pipe.FreeTokens(1 + _attCount);
					_depth++;
					break;
				case XmlNodeType.EndElement:
					_scope.PopScope();
					_pipe.FreeTokens(1);
					break;
				case XmlNodeType.Text:
					if (_attOffset != 0)
					{
						// We are on text node inside of the attribute
						_attOffset = 0;
						_depth -= 2;
						goto case XmlNodeType.Element;
					}
					_pipe.FreeTokens(1);
					break;
				case XmlNodeType.ProcessingInstruction:
				case XmlNodeType.Comment:
				case XmlNodeType.SignificantWhitespace:
				case XmlNodeType.Whitespace:
					_pipe.FreeTokens(1);
					break;
				default:
					throw new InvalidProgramException("Internal Error: unexpected node type");
			}
			Debug.Assert(_attOffset == 0);
			Debug.Assert(_readState == ReadState.Interactive);
			_attCount = 0;
			// Step on next node
			_pipe.Read(out _nodeType, out _qname, out _value);
			if (_nodeType == XmlNodeType.None)
			{
				SetUndefinedState(ReadState.EndOfFile);
				return false;
			}

			switch (_nodeType)
			{
				case XmlNodeType.Element:
					for (_attCount = 0;; _attCount++)
					{
						XmlNodeType attType;
						QName attName;
						string attText;
						_pipe.Read(out attType, out attName, out attText);
						if (attType != XmlNodeType.Attribute)
						{
							break; // We are done with attributes for this element
						}
						if (RefEquals(attName.Prefix, "xmlns"))
						{
							_scope.AddNamespace(attName.Local, attText);
						}
						else if (RefEquals(attName, _writer.QNameXmlNs))
						{
							_scope.AddNamespace(attName.Prefix, attText);
						} // prefix is atomized empty string
						else if (RefEquals(attName, _writer.QNameXmlLang))
						{
							_scope.AddLang(attText);
						}
						else if (RefEquals(attName, _writer.QNameXmlSpace))
						{
							_scope.AddSpace(attText);
						}
					}
					_scope.PushScope(_qname);
					break;
				case XmlNodeType.EndElement:
					_qname = _scope.Name;
					_depth--;
					break;
				case XmlNodeType.Comment:
				case XmlNodeType.ProcessingInstruction:
					break;
				case XmlNodeType.Text:
					if (IsWhitespace(_value))
					{
						_nodeType = XmlSpace == XmlSpace.Preserve ? XmlNodeType.SignificantWhitespace : XmlNodeType.Whitespace;
					}
					break;
				default:
					throw new InvalidProgramException("Internal Error: unexpected node type");
			}
			return true;
		}

		public override bool ReadAttributeValue()
		{
			if (_nodeType == XmlNodeType.Attribute)
			{
				_nodeType = XmlNodeType.Text;
				_depth++;
				return true;
			}
			return false;
		}

		private static bool RefEquals(string strA, string strB)
		{
			Debug.Assert(
				(strA == strB) || !String.Equals(strA, strB),
				"String atomization Failure: '" + strA + "'"
				);
			return strA == strB;
		}

		private static bool RefEquals(QName qnA, QName qnB)
		{
			Debug.Assert(
				(qnA == qnB) || qnA.Local != qnB.Local || qnA.NsUri != qnB.NsUri || qnA.Prefix != qnB.Prefix,
				"QName atomization Failure: '" + qnA + "'"
				);
			return qnA == qnB;
		}

		public override void ResolveEntity()
		{
			throw new InvalidOperationException();
		}

		private void SetUndefinedState(ReadState readState)
		{
			_qname = _writer.QNameEmpty;
			_value = string.Empty;
			_nodeType = XmlNodeType.None;
			_attCount = 0;
			_readState = readState;
		}

		private void Start()
		{
			if (_thread != null && _thread.IsAlive)
			{
				// We can also reuse this thread or use ThreadPool. For simplicity we create new thread each time.
				// Some problem with TreadPool will be the need to notify transformation thread when user calls new Start() befor previous transformation completed
				_thread.Abort();
				_thread.Join();
			}
			_writer.Reset();
			_scope.Reset();
			_pipe.Reset();
			_depth = 0;
			SetUndefinedState(ReadState.Initial);
			if (_multiThread)
			{
				_thread = new Thread(StartTransform);
				_thread.Start();
			}
			else
			{
				StartTransform();
			}
		}

		public XmlReader StartTransform(XmlInput input, XsltArgumentList args)
		{
			_defaulDocument = input;
			_args = args;
			Start();
			return this;
		}

		private void StartTransform()
		{
			try
			{
				while (true)
				{
					var xmlReader = _defaulDocument.Source as XmlReader;
					if (xmlReader != null)
					{
						XslCompiledTransform.Transform(xmlReader, _args, _writer, _defaulDocument.Resolver);
						break;
					}
					var nav = _defaulDocument.Source as IXPathNavigable;
					if (nav != null)
					{
						XslCompiledTransform.Transform(nav, _args, _writer);
						break;
					}
					var str = _defaulDocument.Source as string;
					if (str != null)
					{
						using (var reader = Create(str, ReaderSettings))
						{
							XslCompiledTransform.Transform(reader, _args, _writer, _defaulDocument.Resolver);
						}
						break;
					}
					var strm = _defaulDocument.Source as Stream;
					if (strm != null)
					{
						using (var reader = Create(strm, ReaderSettings))
						{
							XslCompiledTransform.Transform(reader, _args, _writer, _defaulDocument.Resolver);
						}
						break;
					}
					var txtReader = _defaulDocument.Source as TextReader;
					if (txtReader != null)
					{
						using (var reader = Create(txtReader, ReaderSettings))
						{
							XslCompiledTransform.Transform(reader, _args, _writer, _defaulDocument.Resolver);
						}
						break;
					}
					throw new Exception("Unexpected XmlInput");
				}
				_writer.Close();
			}
			catch (Exception e)
			{
				if (_multiThread)
				{
					// we need this exception on main thread. So pass it through pipe.
					_pipe.WriteException(e);
				}
				else
				{
					throw;
				}
			}
		}

		private class BufferWriter : XmlWriter
		{
			public readonly QName QNameEmpty;
			public readonly QName QNameXmlLang;
			public readonly QName QNameXmlNs;
			public readonly QName QNameXmlSpace;
			private readonly TokenPipe _pipe;
			private readonly QNameTable _qnameTable;
			private readonly StringBuilder _sbuilder;
			private QName _curAttribute;
			private string _firstText;

			public BufferWriter(TokenPipe pipe, XmlNameTable nameTable)
			{
				_pipe = pipe;
				_qnameTable = new QNameTable(nameTable);
				_sbuilder = new StringBuilder();
				QNameXmlSpace = _qnameTable.GetQName("space", NsXml, "xml"); // xml:space
				QNameXmlLang = _qnameTable.GetQName("lang", NsXml, "xml"); // xml:lang
				QNameXmlNs = _qnameTable.GetQName("xmlns", NsXmlNs, ""); // xmlsn=""
				QNameEmpty = _qnameTable.GetQName("", "", "");
			}

			public override WriteState WriteState
			{
				get { throw new NotSupportedException(); }
			}

			public override XmlSpace XmlSpace
			{
				get { throw new NotSupportedException(); }
			}

			public override string XmlLang
			{
				get { throw new NotSupportedException(); }
			}

			private void AppendText(string text)
			{
				if (_firstText == null)
				{
					Debug.Assert(_sbuilder.Length == 0);
					_firstText = text;
				}
				else if (_sbuilder.Length == 0)
				{
					_sbuilder.Append(_firstText);
				}
				_sbuilder.Append(text);
			}

			public override void Close()
			{
				FinishTextNode();
				_pipe.Close();
			}

			private void FinishTextNode()
			{
				var text = MergeText();
				if (text.Length != 0)
				{
					_pipe.Write(XmlNodeType.Text, QNameEmpty, text);
				}
			}

			public override void Flush() {}

			public override string LookupPrefix(string ns)
			{
				throw new NotSupportedException();
			}

			private string MergeText()
			{
				if (_firstText == null)
				{
					return string.Empty; // There was no text ouptuted
				}
				if (_sbuilder.Length != 0)
				{
					// merge content of sbuilder into firstText
					Debug.Assert(_firstText != null);
					_firstText = _sbuilder.ToString();
					_sbuilder.Length = 0;
				}
				var result = _firstText;
				_firstText = null;
				return result;
			}

			public void Reset()
			{
				_firstText = null;
				_sbuilder.Length = 0;
			}

			public override void WriteBase64(byte[] buffer, int index, int count)
			{
				throw new NotSupportedException();
			}

			public override void WriteCData(string text)
			{
				throw new NotSupportedException();
			}

			public override void WriteCharEntity(char ch)
			{
				throw new NotSupportedException();
			}

			public override void WriteChars(char[] buffer, int index, int count)
			{
				throw new NotSupportedException();
			}

			public override void WriteComment(string text)
			{
				FinishTextNode();
				_pipe.Write(XmlNodeType.Comment, QNameEmpty, text);
			}

			public override void WriteDocType(string name, string pubid, string sysid, string subset)
			{
				throw new NotSupportedException();
			}

			public override void WriteEndAttribute()
			{
				_pipe.Write(XmlNodeType.Attribute, _curAttribute, MergeText());
			}

			public override void WriteEndDocument()
			{
				throw new NotSupportedException();
			}

			public override void WriteEndElement()
			{
				FinishTextNode();
				_pipe.Write(XmlNodeType.EndElement, QNameEmpty, "");
			}

			public override void WriteEntityRef(string name)
			{
				throw new NotSupportedException();
			}

			public override void WriteFullEndElement()
			{
				WriteEndElement();
			}

			public override void WriteProcessingInstruction(string name, string text)
			{
				FinishTextNode();
				_pipe.Write(XmlNodeType.ProcessingInstruction, _qnameTable.GetQName(name, string.Empty, string.Empty), text);
			}

			public override void WriteRaw(string data)
			{
				WriteString(data); // In XslReader output we ignore disable-output-escaping
			}

			public override void WriteRaw(char[] buffer, int index, int count)
			{
				throw new NotSupportedException();
			}

			public override void WriteStartAttribute(string prefix, string name, string ns)
			{
				_curAttribute = _qnameTable.GetQName(name, ns, prefix);
			}

			public override void WriteStartDocument()
			{
				throw new NotSupportedException();
			}

			public override void WriteStartDocument(bool standalone)
			{
				throw new NotSupportedException();
			}

			public override void WriteStartElement(string prefix, string name, string ns)
			{
				FinishTextNode();
				_pipe.Write(XmlNodeType.Element, _qnameTable.GetQName(name, ns, prefix), "");
			}

			public override void WriteString(string text)
			{
				AppendText(text);
			}

			public override void WriteSurrogateCharEntity(char lowChar, char highChar)
			{
				throw new NotSupportedException();
			}

			public override void WriteWhitespace(string ws)
			{
				throw new NotSupportedException();
			}

			private class QNameTable
			{
				// This class atomizes QNames.
				private readonly XmlNameTable _nameTable;
				private readonly Dictionary<string, List<QName>> _qnames = new Dictionary<string, List<QName>>();

				public QNameTable(XmlNameTable nameTable)
				{
					_nameTable = nameTable;
				}

				public QName GetQName(string local, string nsUri, string prefix)
				{
					nsUri = _nameTable.Add(nsUri);
					prefix = _nameTable.Add(prefix);
					List<QName> list;
					if (!_qnames.TryGetValue(local, out list))
					{
						list = new List<QName>();
						_qnames.Add(local, list);
					}
					else
					{
						foreach (var qn in list)
						{
							Debug.Assert(qn.Local == local, "Atomization Failure: '" + local + "'");
							if (RefEquals(qn.Prefix, prefix) && RefEquals(qn.NsUri, nsUri))
							{
								return qn;
							}
						}
					}
					var qname = new QName(_nameTable.Add(local), nsUri, prefix);
					list.Add(qname);
					return qname;
				}
			}
		}

		private class ScopeManager
		{
			// We need the scope for the following reasons:
			// 1. Report QName on EndElement  (local, nsUri, prefix )
			// 2. Keep scope of Namespaces    (null , nsUri, prefix )
			// 3. Keep scope of xml:lang      (null , lang , "lang" )
			// 4. Keep scope of xml:space     (null , space, "space")
			// On each StartElement we adding record(s) to the scope, 
			// Its convinient to add QName last becuase in this case it will be directly available for EndElement
			private static readonly string AtomLang = new String("lang".ToCharArray());
			private static readonly string AtomSpace = new String("space".ToCharArray());
			private readonly XmlNameTable _nameTable;
			private readonly string _stringEmpty;
			private int _lastRecord;
			private QName[] _records = new QName[32];

			public ScopeManager(XmlNameTable nameTable)
			{
				_nameTable = nameTable;
				_stringEmpty = nameTable.Add(string.Empty);
				Lang = _stringEmpty;
				Space = XmlSpace.None;
				Reset();
			}

			public string Lang { get; private set; }

			public XmlSpace Space { get; private set; }

			public QName Name
			{
				get
				{
					Debug.Assert(_records[_lastRecord - 1].Local != null, "Element Name is expected");
					return _records[_lastRecord - 1];
				}
			}

			public void AddLang(string lang)
			{
				Debug.Assert(lang != null);
				lang = _nameTable.Add(lang);
				if (RefEquals(lang, Lang))
				{
					return;
				}
				AddRecord(new QName(null, Lang, AtomLang));
				Lang = lang;
			}

			public void AddNamespace(string prefix, string uri)
			{
				Debug.Assert(prefix != null);
				Debug.Assert(uri != null);
				Debug.Assert(prefix == _nameTable.Add(prefix), "prefixes are expected to be already atomized in this NameTable");
				uri = _nameTable.Add(uri);
				Debug.Assert(
					!RefEquals(prefix, AtomLang) &&
						!RefEquals(prefix, AtomSpace)
					,
					"This assumption is important to distinct NsDecl from xml:space and xml:lang"
					);
				AddRecord(new QName(null, uri, prefix));
			}

			private void AddRecord(QName qname)
			{
				if (_lastRecord == _records.Length)
				{
					var temp = new QName[_records.Length * 2];
					_records.CopyTo(temp, 0);
					_records = temp;
				}
				_records[_lastRecord++] = qname;
			}

			public void AddSpace(string space)
			{
				Debug.Assert(space != null);
				var xmlSpace = Str2Space(space);
				if (xmlSpace == XmlSpace.None)
				{
					throw new Exception("Unexpected value for xml:space attribute");
				}
				if (xmlSpace == Space)
				{
					return;
				}
				AddRecord(new QName(null, Space2Str(Space), AtomSpace));
				Space = xmlSpace;
			}

			public string LookupNamespace(string prefix)
			{
				Debug.Assert(prefix != null);
				prefix = _nameTable.Get(prefix);
				for (var i = _lastRecord - 2; 0 <= i; i--)
				{
					var record = _records[i];
					if (record.Local == null && RefEquals(record.Prefix, prefix))
					{
						return record.NsUri;
					}
				}
				return null;
			}

			public void PopScope()
			{
				Debug.Assert(_records[_lastRecord - 1].Local != null, "LastRecord in each scope is expected to be ElementName");
				do
				{
					_lastRecord--;
					Debug.Assert(0 < _lastRecord, "Push/Pop balance error");
					var record = _records[_lastRecord - 1];
					if (record.Local != null)
					{
						break; //  this record is Element QName
					}
					if (RefEquals(record.Prefix, AtomLang))
					{
						Lang = record.NsUri;
					}
					else if (RefEquals(record.Prefix, AtomSpace))
					{
						Space = Str2Space(record.NsUri);
					}
				} while (true);
			}

			public void PushScope(QName qname)
			{
				Debug.Assert(qname.Local != null, "Scope is Element Name");
				AddRecord(qname);
			}

			public void Reset()
			{
				_lastRecord = 0;
				_records[_lastRecord++] = new QName(null, _nameTable.Add(NsXml), _nameTable.Add("xml"));
				// xmlns:xml="http://www.w3.org/XML/1998/namespace"
				_records[_lastRecord++] = new QName(null, _stringEmpty, _stringEmpty); // xml=""
				_records[_lastRecord++] = new QName(_stringEmpty, _stringEmpty, _stringEmpty); // --  lookup barier
			}

			private static string Space2Str(XmlSpace space)
			{
				switch (space)
				{
					case XmlSpace.Preserve:
						return "preserve";
					case XmlSpace.Default:
						return "default";
					default:
						return "none";
				}
			}

			private static XmlSpace Str2Space(string space)
			{
				switch (space)
				{
					case "preserve":
						return XmlSpace.Preserve;
					case "default":
						return XmlSpace.Default;
					default:
						return XmlSpace.None;
				}
			}
		}

		private class TokenPipe
		{
			protected XmlToken[] Buffer;
			protected int Mask; // used in TokenPipeMultiThread
			protected int ReadEndPos; // 
			protected int ReadStartPos; // 
			protected int WritePos; // position after last wrote token

			public TokenPipe(int bufferSize)
			{
				/*BuildMask*/
				{
					if (bufferSize < 2)
					{
						bufferSize = DefaultBufferSize;
					}
					// To make or round buffer work bufferSize should be == 2 power N and mask == bufferSize - 1
					bufferSize--;
					Mask = bufferSize;
					while ((bufferSize = bufferSize >> 1) != 0)
					{
						Mask |= bufferSize;
					}
				}
				Buffer = new XmlToken[Mask + 1];
			}

			public virtual void Close()
			{
				Write(XmlNodeType.None, null, null);
			}

			public virtual void FreeTokens(int num)
			{
				ReadStartPos += num;
				ReadEndPos = ReadStartPos;
			}

			public virtual void GetToken(int attNum, out QName name, out string value)
			{
				Debug.Assert(0 <= attNum && attNum < ReadEndPos - ReadStartPos - 1);
				XmlNodeType nodeType;
				XmlToken.Get(ref Buffer[ReadStartPos + attNum], out nodeType, out name, out value);
				Debug.Assert(nodeType == (attNum == 0 ? XmlNodeType.Element : XmlNodeType.Attribute),
					"We use GetToken() only to access parts of start element tag.");
			}

			public virtual void Read(out XmlNodeType nodeType, out QName name, out string value)
			{
				Debug.Assert(ReadEndPos < Buffer.Length);
				XmlToken.Get(ref Buffer[ReadEndPos], out nodeType, out name, out value);
				ReadEndPos++;
			}

			public virtual void Reset()
			{
				ReadStartPos = ReadEndPos = WritePos = 0;
			}

			public virtual void Write(XmlNodeType nodeType, QName name, string value)
			{
				Debug.Assert(WritePos <= Buffer.Length);
				if (WritePos == Buffer.Length)
				{
					var temp = new XmlToken[Buffer.Length * 2];
					Buffer.CopyTo(temp, 0);
					Buffer = temp;
				}
				Debug.Assert(WritePos < Buffer.Length);
				XmlToken.Set(ref Buffer[WritePos], nodeType, name, value);
				WritePos++;
			}

			public virtual void WriteException(Exception e)
			{
				throw e;
			}
		}

		private class TokenPipeMultiThread : TokenPipe
		{
			private Exception _exception;

			public TokenPipeMultiThread(int bufferSize)
				: base(bufferSize) {}

			public override void Close()
			{
				Write(XmlNodeType.None, null, null);
				lock (this)
				{
					Monitor.Pulse(this);
				}
			}

			private void ExpandBuffer()
			{
				// Buffer is too smal for this amount of attributes.
				Debug.Assert(WritePos == ReadStartPos + Buffer.Length, "no space to write next token");
				Debug.Assert(WritePos == ReadEndPos, "all tokens ware read");
				var newMask = (Mask << 1) | 1;
				var newBuffer = new XmlToken[newMask + 1];
				for (var i = ReadStartPos; i < WritePos; i++)
				{
					newBuffer[i & newMask] = Buffer[i & Mask];
				}
				Buffer = newBuffer;
				Mask = newMask;
				Debug.Assert(WritePos < ReadStartPos + Buffer.Length, "we should have now space to next write token");
			}

			public override void FreeTokens(int num)
			{
				lock (this)
				{
					ReadStartPos += num;
					ReadEndPos = ReadStartPos;
					Monitor.Pulse(this);
				}
			}

			public override void GetToken(int attNum, out QName name, out string value)
			{
				Debug.Assert(0 <= attNum && attNum < ReadEndPos - ReadStartPos - 1);
				XmlNodeType nodeType;
				XmlToken.Get(ref Buffer[(ReadStartPos + attNum) & Mask], out nodeType, out name, out value);
				Debug.Assert(nodeType == (attNum == 0 ? XmlNodeType.Element : XmlNodeType.Attribute),
					"We use GetToken() only to access parts of start element tag.");
			}

			public override void Read(out XmlNodeType nodeType, out QName name, out string value)
			{
				lock (this)
				{
					Debug.Assert(ReadEndPos <= WritePos && WritePos <= ReadStartPos + Buffer.Length);
					if (ReadEndPos == WritePos)
					{
						if (ReadEndPos == ReadStartPos + Buffer.Length)
						{
							ExpandBuffer();
							Monitor.Pulse(this);
						}
						Monitor.Wait(this);
					}
					if (_exception != null)
					{
						throw new XsltException("Exception happened during transformation. See inner exception for details:\n", _exception);
					}
				}
				Debug.Assert(ReadEndPos < WritePos);
				XmlToken.Get(ref Buffer[ReadEndPos & Mask], out nodeType, out name, out value);
				ReadEndPos++;
			}

			public override void Reset()
			{
				base.Reset();
				_exception = null;
			}

			public override void Write(XmlNodeType nodeType, QName name, string value)
			{
				lock (this)
				{
					Debug.Assert(ReadEndPos <= WritePos && WritePos <= ReadStartPos + Buffer.Length);
					if (WritePos == ReadStartPos + Buffer.Length)
					{
						if (WritePos == ReadEndPos)
						{
							ExpandBuffer();
						}
						else
						{
							Monitor.Wait(this);
						}
					}

					Debug.Assert(WritePos < ReadStartPos + Buffer.Length);
					XmlToken.Set(ref Buffer[WritePos & Mask], nodeType, name, value);

					WritePos++;
					if (ReadStartPos + Buffer.Length <= WritePos)
					{
						// This "if" is some heuristics, it may wrk or may not:
						// To minimize task switching we wakeup reader ony if we wrote enouph tokens.
						// So if reader already waits, let it sleep before we fill up the buffer. 
						Monitor.Pulse(this);
					}
				}
			}

			public override void WriteException(Exception e)
			{
				lock (this)
				{
					_exception = e;
					Monitor.Pulse(this);
				}
			}
		}

		[DebuggerDisplay("{_nodeType}: name={_name}, Value={_value}")]
		private struct XmlToken
		{
			private QName _name;
			private XmlNodeType _nodeType;
			private string _value;

			// it seams that it faster to set fields of structure in one call.
			// This trick is workaround of the C# limitation of declaring variable as ref to a struct.

			public static void Get(ref XmlToken evnt, out XmlNodeType nodeType, out QName name, out string value)
			{
				nodeType = evnt._nodeType;
				name = evnt._name;
				value = evnt._value;
			}

			public static void Set(ref XmlToken evnt, XmlNodeType nodeType, QName name, string value)
			{
				evnt._nodeType = nodeType;
				evnt._name = name;
				evnt._value = value;
			}
		}
	}
}
