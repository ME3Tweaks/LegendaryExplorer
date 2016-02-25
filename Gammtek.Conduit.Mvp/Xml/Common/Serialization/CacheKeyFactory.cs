using System;
using System.Text;
using System.Xml.Serialization;

namespace Gammtek.Conduit.Mvp.Xml.Common.Serialization
{
	public static class CacheKeyFactory
	{
		public static string MakeKey(Type type, XmlAttributeOverrides overrides, Type[] types, XmlRootAttribute root, String defaultNamespace)
		{
			var keyBuilder = new StringBuilder();
			keyBuilder.Append(type.FullName);
			keyBuilder.Append("??");
			keyBuilder.Append(SignatureExtractor.GetOverridesSignature(overrides));
			keyBuilder.Append("??");
			keyBuilder.Append(SignatureExtractor.GetTypeArraySignature(types));
			keyBuilder.Append("??");
			keyBuilder.Append(SignatureExtractor.GetXmlRootSignature(root));
			keyBuilder.Append("??");
			keyBuilder.Append(SignatureExtractor.GetDefaultNamespaceSignature(defaultNamespace));

			return keyBuilder.ToString();
		}
	}
}
