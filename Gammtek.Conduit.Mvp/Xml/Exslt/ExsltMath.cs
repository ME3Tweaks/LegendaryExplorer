using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.XPath;
using Gammtek.Conduit.Mvp.Xml.Common.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltMath
	{
		public double Abs(double number)
		{
			return Math.Abs(number);
		}

		public double Acos(double x)
		{
			return Math.Acos(x);
		}

		public double Asin(double x)
		{
			return Math.Asin(x);
		}

		public double Atan(double x)
		{
			return Math.Atan(x);
		}

		public double Atan2(double x, double y)
		{
			return Math.Atan2(x, y);
		}

		public double Constant(string c, double precision)
		{
			switch (c.ToUpper())
			{
				case "E":
					return Math.E;
				case "PI":
					return Math.PI;
				case "SQRRT2":
					return Math.Sqrt(2);
				case "LN2":
					return Math.Log(2);
				case "LN10":
					return Math.Log(10);
				case "LOG2E":
					return Math.Log(Math.E, 2);
				case "SQRT1_2":
					return Math.Sqrt(.5);
				default:
					return Double.NaN;
			}
		}

		public double Cos(double x)
		{
			return Math.Cos(x);
		}

		public double Exp(double x)
		{
			return Math.Exp(x);
		}

		public XPathNodeIterator Highest(XPathNodeIterator iterator)
		{
			if (iterator.Count == 0)
			{
				return EmptyXPathNodeIterator.Instance;
			}

			var newList = new List<XPathNavigator>();

			try
			{
				iterator.MoveNext();
				var max = XmlConvert.ToDouble(iterator.Current.Value);
				newList.Add(iterator.Current.Clone());

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToDouble(iterator.Current.Value);

					if (t > max)
					{
						max = t;
						newList.Clear();
						newList.Add(iterator.Current.Clone());
					}
					else if (t == max)
					{
						newList.Add(iterator.Current.Clone());
					}
				}
			}
			catch
			{
				//return empty node set                
				return EmptyXPathNodeIterator.Instance;
			}

			return new XPathNavigatorIterator(newList);
		}

		public double Log(double x)
		{
			return Math.Log(x);
		}

		public XPathNodeIterator Lowest(XPathNodeIterator iterator)
		{
			if (iterator.Count == 0)
			{
				return EmptyXPathNodeIterator.Instance;
			}

			var newList = new List<XPathNavigator>();

			try
			{
				iterator.MoveNext();
				var max = XmlConvert.ToDouble(iterator.Current.Value);
				newList.Add(iterator.Current.Clone());

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToDouble(iterator.Current.Value);

					if (t < max)
					{
						max = t;
						newList.Clear();
						newList.Add(iterator.Current.Clone());
					}
					else if (t == max)
					{
						newList.Add(iterator.Current.Clone());
					}
				}
			}
			catch
			{
				//return empty node set                
				return EmptyXPathNodeIterator.Instance;
			}

			return new XPathNavigatorIterator(newList);
		}

		public double Max(XPathNodeIterator iterator)
		{
			double max;

			if (iterator.Count == 0)
			{
				return Double.NaN;
			}

			try
			{
				iterator.MoveNext();
				max = XmlConvert.ToDouble(iterator.Current.Value);

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToDouble(iterator.Current.Value);
					max = (t > max) ? t : max;
				}
			}
			catch
			{
				return Double.NaN;
			}

			return max;
		}

		public double Min(XPathNodeIterator iterator)
		{
			double min;

			if (iterator.Count == 0)
			{
				return Double.NaN;
			}

			try
			{
				iterator.MoveNext();
				min = XmlConvert.ToDouble(iterator.Current.Value);

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToDouble(iterator.Current.Value);
					min = (t < min) ? t : min;
				}
			}
			catch
			{
				return Double.NaN;
			}

			return min;
		}

		public double Power(double x, double y)
		{
			return Math.Pow(x, y);
		}

		public double Random()
		{
			var rand = new Random((int) DateTime.Now.Ticks);
			return rand.NextDouble();
		}

		public double Sin(double x)
		{
			return Math.Sin(x);
		}

		public double Sqrt(double number)
		{
			return number < 0 ? 0 : Math.Sqrt(number);
		}

		public double Tan(double x)
		{
			return Math.Tan(x);
		}
	}
}
