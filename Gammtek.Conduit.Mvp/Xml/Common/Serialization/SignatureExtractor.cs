using System;
using System.Text;
using System.Xml.Serialization;

namespace Gammtek.Conduit.Mvp.Xml.Common.Serialization
{
	public static class SignatureExtractor
	{
		public static string GetDefaultNamespaceSignature(string defaultNamespace)
		{
			return defaultNamespace;
		}

		public static string GetOverridesSignature(XmlAttributeOverrides overrides)
		{
			// GetHashCode looks at something other than the content
			// of XmlOverrideAttributes and comes up with different
			// hash values for two instances that would produce
			// the same XML is the were applied to the XmlSerializer.
			// Therefore I need to do something more intelligent
			// to normalize the content of XmlAttributeOverrides
			// or extract a thumbpront that only accounts for the
			// attributes in XmlAttributeOverrides.

			// The main problems is that 
			// I can only access the hashtables that store the 
			// overriding attributes through reflection, i.e.
			// I can't run the XmlSerializerCache in a partially
			// trusted security context.
			//
			// Also, the extra computation to create a purely
			// content-based thumbprint not offset the savings.
			// 
			// If none if these were an issue I'd simply say:
			// return overrides.GetHashCode().ToString();

			string thumbPrint = null;
			if (null != overrides)
			{
				thumbPrint = XmlAttributeOverridesThumbprinter.GetThumbprint(overrides);
			}
			return thumbPrint;
		}

		public static string GetTypeArraySignature(Type[] types)
		{
			if (null == types || types.Length <= 0)
			{
				return null;
			}

			// to make sure we don't account for the order
			// of the types in the array, we create one SortedList 
			// with the type names, concatenate them and hash that.
			var sorter = new StringSorter();
			foreach (var t in types)
			{
				sorter.AddString(t.AssemblyQualifiedName);
			}
			var thumbPrint = string.Join(":", sorter.GetOrderedArray());
			return thumbPrint;
		}

		public static string GetXmlRootSignature(XmlRootAttribute root)
		{
			var sb = new StringBuilder();
			XmlAttributeOverridesThumbprinter.AddXmlRootPrint(root, sb);
			return sb.ToString();
		}
	}
}
