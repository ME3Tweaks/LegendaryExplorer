using System;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class GdnMath
	{
		public double Avg(XPathNodeIterator iterator)
		{
			double sum = 0;
			var count = iterator.Count;

			if (count == 0)
			{
				return Double.NaN;
			}

			try
			{
				while (iterator.MoveNext())
				{
					sum += XmlConvert.ToDouble(iterator.Current.Value);
				}
			}
			catch (FormatException)
			{
				return Double.NaN;
			}

			return sum / count;
		}
	}
}
