using System.Collections.Generic;
using System.Web.UI;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	internal class AuxExslt
	{
		public static void FindDocs(List<XPathNavigator> arNodes, List<Pair> arDocs)
		{
			var count = arNodes.Count;
			var startDoc = 0;

			while (startDoc < count)
			{
				var start = startDoc;
				var endDoc = count - 1;
				var end = endDoc;
				var mid = (start + end) / 2;

				while (end > start)
				{
					if (
						arNodes[start].ComparePosition(arNodes[mid])
							==
							XmlNodeOrder.Unknown
						)
					{
						end = mid - 1;
						if (arNodes[start].ComparePosition(arNodes[end])
							!=
							XmlNodeOrder.Unknown)
						{
							endDoc = end;
							break;
						}
					}
					else
					{
						if (arNodes[mid].ComparePosition(arNodes[mid + 1])
							==
							XmlNodeOrder.Unknown)
						{
							endDoc = mid;
							break;
						}
						start = mid + 1;
					}
					mid = (start + end) / 2;
				}

				//here we know startDoc and endDoc
				var docRange = new Pair(startDoc, endDoc);
				arDocs.Add(docRange);
				startDoc = endDoc + 1;
			}
		}

		public static bool FindNode(List<XPathNavigator> arNodes1, List<Pair> arDocs, XPathNavigator currNode)
		{
			// 1. First, find the document of this node, return false if not found
			// 2. If the document for the node is found and the node is not an immediate
			//    hit, then look for it using binsearch.

			int start = -1, end = -1;

			foreach (var p in arDocs)
			{
				var xOrder =
					arNodes1[(int) p.First].ComparePosition(currNode);

				if (xOrder == XmlNodeOrder.Same)
				{
					return true;
				}
				//else
				if (xOrder == XmlNodeOrder.Unknown)
				{
					continue;
				}
				start = (int) p.First;
				end = (int) p.Second;
				break;
			}

			if (start == -1)
			{
				return false;
			}
			//else perform a binary search in the range [start, end]

			while (end >= start)
			{
				var mid = (start + end) / 2;

				var xOrder =
					arNodes1[mid].ComparePosition(currNode);

				switch (xOrder)
				{
					case XmlNodeOrder.Before:
						start = mid + 1;
						break;
					case XmlNodeOrder.After:
						end = mid - 1;
						break;
					default:
						return true;
				}
			}

			return false;
		}
	}
}
