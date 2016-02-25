using System;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	internal class XmlBaseState
	{
		public Uri BaseUri;
		public int Depth;
		public XmlBaseState() {}

		public XmlBaseState(Uri baseUri, int depth)
		{
			BaseUri = baseUri;
			Depth = depth;
		}
	}
}
