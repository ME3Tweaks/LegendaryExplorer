using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class GdnSets
	{
		public bool Subset(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			if (nodeset1.Count > 125 || nodeset2.Count > 125)
			{
				return Subset2(nodeset1, nodeset2);
			}
			//else
			var nodelist1 = new XPathNavigatorIterator(nodeset1, true);
			var nodelist2 = new XPathNavigatorIterator(nodeset2, true);

			return nodelist1.Cast<XPathNavigator>().All(nodelist2.Contains);
		}

		public bool Subset2(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			var arDocs = new List<Pair>();

			var arNodes2 = new List<XPathNavigator>(nodeset2.Count);

			while (nodeset2.MoveNext())
			{
				arNodes2.Add(nodeset2.Current.Clone());
			}

			AuxExslt.FindDocs(arNodes2, arDocs);

			while (nodeset1.MoveNext())
			{
				var currNode = nodeset1.Current;

				if (!AuxExslt.FindNode(arNodes2, arDocs, currNode))
				{
					return false;
				}
			}

			return true;
		}
	}
}
