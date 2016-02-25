using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using Gammtek.Conduit.Mvp.Xml.Common.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class MultiXmlTextWriter : XmlTextWriter
	{
		protected const string RedirectNamespace = "http://exslt.org/common";

		protected const string RedirectElementName = "document";

		private readonly XmlResolver _outResolver;

		// Stack of output states
		// Currently processed attribute name 
		private string _currentAttributeName;

		//Redirecting state - relaying by default
		private RedirectState _redirectState = RedirectState.Relaying;
		private OutputState _state;
		private Stack _states;

		public MultiXmlTextWriter(String fileName, Encoding encoding)
			: base(fileName, encoding)
		{
			_outResolver = new OutputResolver(Path.GetDirectoryName(fileName));
		}

		public MultiXmlTextWriter(TextWriter w)
			: base(w) {}

		public MultiXmlTextWriter(TextWriter w, XmlResolver outResolver)
			: base(w)
		{
			_outResolver = outResolver;
		}

		public MultiXmlTextWriter(Stream w, Encoding encoding)
			: base(w, encoding) {}

		public MultiXmlTextWriter(Stream w, Encoding encoding, XmlResolver outResolver)
			: base(w, encoding)
		{
			_outResolver = outResolver;
		}

		private void CheckContentStart()
		{
			if (_redirectState == RedirectState.WritingRedirectElementAttrs)
			{
				//Check required href attribute
				if (_state.Href == null)
				{
					throw new ArgumentNullException("", @"'href' attribute of exsl:document element must be specified.");
				}
				//Are we writing to this URI already?
				if (_states.Cast<OutputState>().Any(nestedState => nestedState.Href == _state.Href))
				{
					throw new ArgumentException("Cannot write to " + _state.Href + " two documents simultaneously.");
				}
				_state.InitWriter(_outResolver);
				_redirectState = RedirectState.Redirecting;
			}
		}

		internal void FinishRedirecting()
		{
			_state.CloseWriter();
			//Pop previous state if it exists
			if (_states.Count != 0)
			{
				_state = (OutputState) _states.Pop();
				_redirectState = RedirectState.Redirecting;
			}
			else
			{
				_state = null;
				_redirectState = RedirectState.Relaying;
			}
		}

		public override void WriteComment(string text)
		{
			CheckContentStart();
			if (_redirectState == RedirectState.Redirecting)
			{
				if (_state.Method == OutputMethod.Text)
				{
					return;
				}
				_state.XmlWriter.WriteComment(text);
			}
			else
			{
				base.WriteComment(text);
			}
		}

		public override void WriteEndAttribute()
		{
			if (_redirectState == RedirectState.WritingRedirectElementAttrValue)
			{
				_redirectState = RedirectState.WritingRedirectElementAttrs;
			}
			else if (_redirectState == RedirectState.Redirecting)
			{
				if (_state.Method == OutputMethod.Text)
				{
					return;
				}
				_state.XmlWriter.WriteEndAttribute();
			}
			else
			{
				base.WriteEndAttribute();
			}
		}

		public override void WriteEndElement()
		{
			CheckContentStart();
			if (_redirectState == RedirectState.Redirecting)
			{
				//Check if that's exsl:document end tag
				if (_state.Depth-- == 0)
				{
					FinishRedirecting();
				}
				else
				{
					if (_state.Method == OutputMethod.Text)
					{
						return;
					}
					_state.XmlWriter.WriteEndElement();
				}
			}
			else
			{
				base.WriteEndElement();
			}
		}

		public override void WriteFullEndElement()
		{
			CheckContentStart();
			if (_redirectState == RedirectState.Redirecting)
			{
				//Check if it's exsl:document end tag
				if (_state.Depth-- == 0)
				{
					FinishRedirecting();
				}
				else
				{
					if (_state.Method == OutputMethod.Text)
					{
						return;
					}
					_state.XmlWriter.WriteFullEndElement();
				}
			}
			else
			{
				base.WriteFullEndElement();
			}
		}

		public override void WriteProcessingInstruction(string name, string text)
		{
			CheckContentStart();
			if (_redirectState == RedirectState.Redirecting)
			{
				if (_state.Method == OutputMethod.Text)
				{
					return;
				}
				_state.XmlWriter.WriteProcessingInstruction(name, text);
			}
			else
			{
				base.WriteProcessingInstruction(name, text);
			}
		}

		public override void WriteStartAttribute(string prefix, string localName, string ns)
		{
			switch (_redirectState)
			{
				case RedirectState.WritingRedirectElementAttrs:
					_redirectState = RedirectState.WritingRedirectElementAttrValue;
					_currentAttributeName = localName;
					break;
				case RedirectState.Redirecting:
					if (_state.Method == OutputMethod.Text)
					{
						return;
					}
					_state.XmlWriter.WriteStartAttribute(prefix, localName, ns);
					break;
				default:
					base.WriteStartAttribute(prefix, localName, ns);
					break;
			}
		}

		public override void WriteStartElement(string prefix, string localName, string ns)
		{
			CheckContentStart();
			//Is it exsl:document redirecting instruction?
			if (ns == RedirectNamespace && localName == RedirectElementName)
			{
				//Lazy stack of states
				if (_states == null)
				{
					_states = new Stack();
				}
				//If we are redirecting already - push the current state into the stack
				if (_redirectState == RedirectState.Redirecting)
				{
					_states.Push(_state);
				}
				//Initialize new state
				_state = new OutputState();
				_redirectState = RedirectState.WritingRedirectElementAttrs;
			}
			else
			{
				if (_redirectState == RedirectState.Redirecting)
				{
					if (_state.Method == OutputMethod.Text)
					{
						_state.Depth++;
						return;
					}
					//Write doctype before the first element
					if (_state.Depth == 0 && _state.SystemDoctype != null)
					{
						if (prefix != String.Empty)
						{
							_state.XmlWriter.WriteDocType(prefix + ":" + localName,
								_state.PublicDoctype, _state.SystemDoctype, null);
						}
						else
						{
							_state.XmlWriter.WriteDocType(localName,
								_state.PublicDoctype, _state.SystemDoctype, null);
						}
					}
					_state.XmlWriter.WriteStartElement(prefix, localName, ns);
					_state.Depth++;
				}
				else
				{
					base.WriteStartElement(prefix, localName, ns);
				}
			}
		}

		public override void WriteString(string text)
		{
			//Possible exsl:document's attribute value
			if (_redirectState == RedirectState.WritingRedirectElementAttrValue)
			{
				switch (_currentAttributeName)
				{
					case "href":
						_state.Href += text;
						break;
					case "method":
						if (text == "text")
						{
							_state.Method = OutputMethod.Text;
						}
						break;
					case "encoding":
						_state.Encoding = Encoding.GetEncoding(text);
						break;
					case "indent":
						if (text == "yes")
						{
							_state.Indent = true;
						}
						break;
					case "doctype-public":
						_state.PublicDoctype = text;
						break;
					case "doctype-system":
						_state.SystemDoctype = text;
						break;
					case "standalone":
						if (text == "yes")
						{
							_state.Standalone = true;
						}
						break;
					case "omit-xml-declaration":
						if (text == "yes")
						{
							_state.OmitXmlDeclaration = true;
						}
						break;
				}
				return;
			}
			CheckContentStart();
			if (_redirectState == RedirectState.Redirecting)
			{
				if (_state.Method == OutputMethod.Text)
				{
					_state.TextWriter.Write(text);
				}
				else
				{
					_state.XmlWriter.WriteString(text);
				}
			}
			else
			{
				base.WriteString(text);
			}
		}
	}
}
