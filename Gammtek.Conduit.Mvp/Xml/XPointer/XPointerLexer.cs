using System;
using System.Globalization;
using System.Text;
using System.Xml;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XPointer
{
	internal class XPointerLexer
	{
		public enum LexKind
		{
			NcName = 'N',
			QName = 'Q',
			LrBracket = '(',
			RrBracket = ')',
			Circumflex = '^',
			Number = 'd',
			Eq = '=',
			Space = 'S',
			Slash = '/',
			EscapedData = 'D',
			Eof = 'E'
		}

		private readonly string _ptr;
		private char _currChar;
		private LexKind _kind;
		private int _ptrIndex;

		public XPointerLexer(string p)
		{
			if (p == null)
			{
				throw new ArgumentNullException("p", Resources.NullXPointer);
			}
			_ptr = p;
			NextChar();
		}

		public LexKind Kind
		{
			get { return _kind; }
		}

		public int Number { get; private set; }

		public string NcName { get; private set; }

		public string Prefix { get; private set; }

		public bool CanBeSchemaName { get; private set; }

		public bool NextChar()
		{
			if (_ptrIndex < _ptr.Length)
			{
				_currChar = _ptr[_ptrIndex++];
				return true;
			}
			_currChar = '\0';
			return false;
		}

		public bool NextLexeme()
		{
			switch (_currChar)
			{
				case '\0':
					_kind = LexKind.Eof;
					return false;
				case '(':
				case ')':
				case '=':
				case '/':
					_kind = (LexKind) Convert.ToInt32(_currChar);
					NextChar();
					break;
				case '^':
					NextChar();
					if (_currChar == '^' || _currChar == '(' || _currChar == ')')
					{
						_kind = LexKind.EscapedData;
						NextChar();
					}
					else
					{
						throw new XPointerSyntaxException(Resources.CircumflexCharMustBeEscaped);
					}
					break;
				default:
					if (Char.IsDigit(_currChar))
					{
						_kind = LexKind.Number;
						var start = _ptrIndex - 1;
						var len = 0;
						while (Char.IsDigit(_currChar))
						{
							NextChar();
							len++;
						}
						Number = XmlConvert.ToInt32(_ptr.Substring(start, len));
						break;
					}
					if (LexUtils.IsStartNameChar(_currChar))
					{
						_kind = LexKind.NcName;
						Prefix = String.Empty;
						NcName = ParseName();
						if (_currChar == ':')
						{
							//QName?
							NextChar();
							Prefix = NcName;
							_kind = LexKind.QName;
							if (LexUtils.IsStartNcNameChar(_currChar))
							{
								NcName = ParseName();
							}
							else
							{
								throw new XPointerSyntaxException(String.Format(CultureInfo.CurrentCulture, Resources.InvalidNameToken, Prefix, _currChar));
							}
						}
						CanBeSchemaName = _currChar == '(';
						break;
					}
					if (LexUtils.IsWhitespace(_currChar))
					{
						_kind = LexKind.Space;
						while (LexUtils.IsWhitespace(_currChar))
						{
							NextChar();
						}
						break;
					}
					_kind = LexKind.EscapedData;
					break;
			}
			return true;
		}

		public string ParseEscapedData()
		{
			var depth = 0;
			var sb = new StringBuilder();
			while (true)
			{
				switch (_currChar)
				{
					case '^':
						if (!NextChar())
						{
							throw new XPointerSyntaxException(Resources.UnexpectedEndOfSchemeData);
						}
						if (_currChar == '^' || _currChar == '(' || _currChar == ')')
						{
							sb.Append(_currChar);
						}
						else
						{
							throw new XPointerSyntaxException(Resources.CircumflexCharMustBeEscaped);
						}
						break;
					case '(':
						depth++;
						goto default;
					case ')':
						if (depth-- == 0)
						{
							//Skip ')'
							NextLexeme();
							return sb.ToString();
						}
						goto default;
					default:
						sb.Append(_currChar);
						break;
				}
				if (!NextChar())
				{
					throw new XPointerSyntaxException(Resources.UnexpectedEndOfSchemeData);
				}
			}
		}

		private string ParseName()
		{
			var start = _ptrIndex - 1;
			var len = 0;
			while (LexUtils.IsNcNameChar(_currChar))
			{
				NextChar();
				len++;
			}
			return _ptr.Substring(start, len);
		}

		public void SkipWhiteSpace()
		{
			while (LexUtils.IsWhitespace(_currChar))
			{
				NextChar();
			}
		}
	}
}
