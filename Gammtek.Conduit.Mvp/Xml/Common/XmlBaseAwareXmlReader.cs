using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	public class XmlBaseAwareXmlReader : XmlWrappingReader
	{
		private XmlBaseState _state = new XmlBaseState();
		private Stack<XmlBaseState> _states;

		public XmlBaseAwareXmlReader(string uri)
			: base(Create(uri, CreateReaderSettings()))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(string uri, XmlResolver resolver)
			: base(Create(uri, CreateReaderSettings(resolver)))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(string uri, XmlNameTable nt)
			: base(Create(uri, CreateReaderSettings(nt)))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(TextReader reader)
			: base(Create(reader, CreateReaderSettings())) {}

		public XmlBaseAwareXmlReader(string uri, TextReader reader)
			: base(Create(reader, CreateReaderSettings(), uri))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(TextReader reader, XmlNameTable nt)
			: base(Create(reader, CreateReaderSettings(nt))) {}

		public XmlBaseAwareXmlReader(string uri, TextReader reader, XmlNameTable nt)
			: base(Create(reader, CreateReaderSettings(nt), uri))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(Stream stream)
			: base(Create(stream, CreateReaderSettings())) {}

		public XmlBaseAwareXmlReader(string uri, Stream stream)
			: base(Create(stream, CreateReaderSettings(), uri))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(string uri, Stream stream, XmlResolver resolver)
			: base(Create(stream, CreateReaderSettings(resolver), uri))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(Stream stream, XmlNameTable nt)
			: base(Create(stream, CreateReaderSettings(nt))) {}

		public XmlBaseAwareXmlReader(string uri, Stream stream, XmlNameTable nt)
			: base(Create(stream, CreateReaderSettings(nt), uri))
		{
			var uriString = base.BaseURI;
			if (uriString != null)
			{
				_state.BaseUri = new Uri(uriString);
			}
		}

		public XmlBaseAwareXmlReader(string uri, XmlReaderSettings settings)
			: base(Create(uri, settings)) {}

		public XmlBaseAwareXmlReader(TextReader reader, XmlReaderSettings settings)
			: base(Create(reader, settings)) {}

		public XmlBaseAwareXmlReader(Stream stream, XmlReaderSettings settings)
			: base(Create(stream, settings)) {}

		public XmlBaseAwareXmlReader(XmlReader reader, XmlReaderSettings settings)
			: base(Create(reader, settings)) {}

		public XmlBaseAwareXmlReader(TextReader reader, XmlReaderSettings settings, string baseUri)
			: base(Create(reader, settings, baseUri)) {}

		public XmlBaseAwareXmlReader(Stream stream, XmlReaderSettings settings, string baseUri)
			: base(Create(stream, settings, baseUri)) {}

		public override string BaseURI
		{
			get { return _state.BaseUri == null ? "" : _state.BaseUri.AbsoluteUri; }
		}

		private static XmlReaderSettings CreateReaderSettings()
		{
			var settings = new XmlReaderSettings
			{
				DtdProcessing = DtdProcessing.Parse,
				CloseInput = true
			};

			return settings;
		}

		private static XmlReaderSettings CreateReaderSettings(XmlResolver resolver)
		{
			var settings = CreateReaderSettings();
			settings.XmlResolver = resolver;
			settings.CloseInput = true;

			return settings;
		}

		private static XmlReaderSettings CreateReaderSettings(XmlNameTable nt)
		{
			var settings = CreateReaderSettings();
			settings.NameTable = nt;
			settings.CloseInput = true;

			return settings;
		}

		public override bool Read()
		{
			var baseRead = base.Read();
			if (!baseRead)
			{
				return false;
			}
			if (base.NodeType == XmlNodeType.Element &&
				base.HasAttributes)
			{
				var baseAttr = GetAttribute("xml:base");
				if (baseAttr == null)
				{
					return true;
				}
				var newBaseUri = _state.BaseUri == null ? new Uri(baseAttr) : new Uri(_state.BaseUri, baseAttr);
				if (_states == null)
				{
					_states = new Stack<XmlBaseState>();
				}
				//Push current state and allocate new one
				_states.Push(_state);
				_state = new XmlBaseState(newBaseUri, base.Depth);
			}
			else if (base.NodeType == XmlNodeType.EndElement)
			{
				if (base.Depth == _state.Depth && _states != null && _states.Count > 0)
				{
					//Pop previous state
					_state = _states.Pop();
				}
			}
			return true;
		}
	}
}
