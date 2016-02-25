using System;
using System.IO;
using System.Xml;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public class OutputResolver : XmlUrlResolver
	{
		private readonly Uri _baseUri;

		public OutputResolver(string baseUri)
		{
			if (string.IsNullOrEmpty(baseUri))
			{
				baseUri = ".";
			}
			_baseUri = new Uri(new Uri(Directory.GetCurrentDirectory() + "/"), baseUri + "/");
		}

		public override Uri ResolveUri(Uri baseUri, string relativeUri)
		{
			return base.ResolveUri(_baseUri, relativeUri);
		}
	}
}
