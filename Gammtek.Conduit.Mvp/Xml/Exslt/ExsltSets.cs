using System.Collections.Generic;
using System.Linq;
using System.Web.UI;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltSets
	{
		public XPathNodeIterator Difference(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			if (nodeset2.Count > 166)
			{
				return Difference2(nodeset1, nodeset2);
			}
			//else
			var nodelist1 = new XPathNavigatorIterator(nodeset1, true);
			var nodelist2 = new XPathNavigatorIterator(nodeset2);

			for (var i = 0; i < nodelist1.Count; i++)
			{
				var nav = nodelist1[i];

				if (!nodelist2.Contains(nav))
				{
					continue;
				}
				nodelist1.RemoveAt(i);
				i--;
			}

			nodelist1.Reset();
			return nodelist1;
		}

		public XPathNodeIterator Difference2(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			var arDocs = new List<Pair>();

			var arNodes2 = new List<XPathNavigator>(nodeset2.Count);

			while (nodeset2.MoveNext())
			{
				arNodes2.Add(nodeset2.Current.Clone());
			}

			AuxExslt.FindDocs(arNodes2, arDocs);

			var enlResult = new XPathNavigatorIterator();

			while (nodeset1.MoveNext())
			{
				var currNode = nodeset1.Current;

				if (!AuxExslt.FindNode(arNodes2, arDocs, currNode))
				{
					enlResult.Add(currNode.Clone());
				}
			}

			enlResult.Reset();
			return enlResult;
		}

		public XPathNodeIterator Distinct(XPathNodeIterator nodeset)
		{
			if (nodeset.Count > 15)
			{
				return Distinct2(nodeset);
			}
			//else
			var nodelist = new XPathNavigatorIterator();

			while (nodeset.MoveNext())
			{
				if (!nodelist.ContainsValue(nodeset.Current.Value))
				{
					nodelist.Add(nodeset.Current.Clone());
				}
			}
			nodelist.Reset();
			return nodelist;
		}

		public XPathNodeIterator Distinct2(XPathNodeIterator nodeset)
		{
			var nodelist = new XPathNavigatorIterator();

			var ht = new Dictionary<string, string>(nodeset.Count / 3);

			while (nodeset.MoveNext())
			{
				var strVal = nodeset.Current.Value;

				if (ht.ContainsKey(strVal))
				{
					continue;
				}
				ht.Add(strVal, "");

				nodelist.Add(nodeset.Current.Clone());
			}

			nodelist.Reset();
			return nodelist;
		}

		public bool HasSameNode(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			if (nodeset1.Count >= 250 || nodeset2.Count >= 250)
			{
				return HasSameNode2(nodeset1, nodeset2);
			}
			//else

			var nodelist1 = new XPathNavigatorIterator(nodeset1, true);
			var nodelist2 = new XPathNavigatorIterator(nodeset2, true);

			return nodelist1.Cast<XPathNavigator>().Any(nodelist2.Contains);
		}

		public bool HasSameNode2(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			var it1 =
				(nodeset1.Count > nodeset2.Count)
					? nodeset1
					: nodeset2;

			var it2 =
				(nodeset1.Count > nodeset2.Count)
					? nodeset2
					: nodeset1;

			var arDocs = new List<Pair>();

			var arNodes1 = new List<XPathNavigator>(it1.Count);

			while (it1.MoveNext())
			{
				arNodes1.Add(it1.Current.Clone());
			}

			AuxExslt.FindDocs(arNodes1, arDocs);

			while (it2.MoveNext())
			{
				var currNode = it2.Current;

				if (AuxExslt.FindNode(arNodes1, arDocs, currNode))
				{
					return true;
				}
			}

			return false;
		}

		public XPathNodeIterator Intersection(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			if (nodeset1.Count >= 500 || nodeset2.Count >= 500)
			{
				return Intersection3(nodeset1, nodeset2);
			}
			//else
			var nodelist1 = new XPathNavigatorIterator(nodeset1, true);
			var nodelist2 = new XPathNavigatorIterator(nodeset2);

			for (var i = 0; i < nodelist1.Count; i++)
			{
				var nav = nodelist1[i];

				if (!nodelist2.Contains(nav))
				{
					nodelist1.RemoveAt(i);
					i--;
				}
			}

			nodelist1.Reset();
			return nodelist1;
		}

		private static XPathNodeIterator Intersection3(XPathNodeIterator nodeset1,
			XPathNodeIterator nodeset2)
		{
			var it1 =
				(nodeset1.Count > nodeset2.Count)
					? nodeset1
					: nodeset2;

			var it2 =
				(nodeset1.Count > nodeset2.Count)
					? nodeset2
					: nodeset1;

			var arDocs = new List<Pair>();

			var arNodes1 = new List<XPathNavigator>(it1.Count);

			while (it1.MoveNext())
			{
				arNodes1.Add(it1.Current.Clone());
			}

			AuxExslt.FindDocs(arNodes1, arDocs);

			var enlResult = new XPathNavigatorIterator();

			while (it2.MoveNext())
			{
				var currNode = it2.Current;

				if (AuxExslt.FindNode(arNodes1, arDocs, currNode))
				{
					enlResult.Add(currNode.Clone());
				}
			}

			enlResult.Reset();
			return enlResult;
		}

		public XPathNodeIterator Leading(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			XPathNavigator leader;

			if (nodeset2.MoveNext())
			{
				leader = nodeset2.Current;
			}
			else
			{
				return nodeset1;
			}

			var nodelist1 = new XPathNavigatorIterator();

			while (nodeset1.MoveNext())
			{
				if (nodeset1.Current.ComparePosition(leader) == XmlNodeOrder.Before)
				{
					nodelist1.Add(nodeset1.Current.Clone());
				}
			}

			nodelist1.Reset();
			return nodelist1;
		}

		public XPathNodeIterator Trailing(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			XPathNavigator leader;

			if (nodeset2.MoveNext())
			{
				leader = nodeset2.Current;
			}
			else
			{
				return nodeset1;
			}

			var nodelist1 = new XPathNavigatorIterator();

			while (nodeset1.MoveNext())
			{
				if (nodeset1.Current.ComparePosition(leader) == XmlNodeOrder.After)
				{
					nodelist1.Add(nodeset1.Current.Clone());
				}
			}

			nodelist1.Reset();
			return nodelist1;
		}

		public bool hasSameNode_RENAME_ME(XPathNodeIterator nodeset1, XPathNodeIterator nodeset2)
		{
			return HasSameNode(nodeset1, nodeset2);
		}
	}
}
