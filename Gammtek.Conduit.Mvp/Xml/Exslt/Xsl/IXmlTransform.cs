using System.Xml;
using System.Xml.Xsl;

namespace Gammtek.Conduit.Mvp.Xml.Common.Xsl
{
	public interface IXmlTransform
	{
		XmlWriterSettings OutputSettings { get; }

		XmlOutput Transform(XmlInput defaulDocument, XsltArgumentList args, XmlOutput output);
	}
}
