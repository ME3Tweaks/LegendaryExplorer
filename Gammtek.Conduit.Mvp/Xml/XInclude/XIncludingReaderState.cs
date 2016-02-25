namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	internal enum XIncludingReaderState
	{
		//Default state
		Default,
		
		//xml:base attribute is being exposed
		ExposingXmlBaseAttr,
		
		//xml:base attribute value is being exposed
		ExposingXmlBaseAttrValue,
		
		//xml:lang attribute is being exposed
		ExposingXmlLangAttr,
		
		//xml:lang attribute value is being exposed
		ExposingXmlLangAttrValue
	}
}
