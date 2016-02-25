using System;
using System.Globalization;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class GdnDatesAndTimes : ExsltDatesAndTimes
	{
		public string Avg(XPathNodeIterator iterator)
		{
			var sum = new TimeSpan(0, 0, 0, 0);
			var count = iterator.Count;
			if (count == 0)
			{
				return "";
			}
			try
			{
				while (iterator.MoveNext())
				{
					sum = XmlConvert.ToTimeSpan(iterator.Current.Value).Add(sum);
				}
			}
			catch (FormatException)
			{
				return "";
			}

			return Duration(sum.TotalSeconds / count);
		}

		public new string DayAbbreviation(string culture)
		{
			try
			{
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetAbbreviatedDayName(System.DateTime.Now.DayOfWeek);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string DayAbbreviation(string d, string culture)
		{
			try
			{
				var date = new DateTz(d);
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetAbbreviatedDayName(date.D.DayOfWeek);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string DayName(string d, string culture)
		{
			try
			{
				var date = new DateTz(d);
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetDayName(date.D.DayOfWeek);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public new string DayName(string culture)
		{
			try
			{
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetDayName(System.DateTime.Now.DayOfWeek);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string Max(XPathNodeIterator iterator)
		{
			TimeSpan max;

			if (iterator.Count == 0)
			{
				return "";
			}

			try
			{
				iterator.MoveNext();
				max = XmlConvert.ToTimeSpan(iterator.Current.Value);

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToTimeSpan(iterator.Current.Value);
					max = (t > max) ? t : max;
				}
			}
			catch (FormatException)
			{
				return "";
			}

			return XmlConvert.ToString(max);
		}

		public string Min(XPathNodeIterator iterator)
		{
			TimeSpan min;

			if (iterator.Count == 0)
			{
				return "";
			}

			try
			{
				iterator.MoveNext();
				min = XmlConvert.ToTimeSpan(iterator.Current.Value);

				while (iterator.MoveNext())
				{
					var t = XmlConvert.ToTimeSpan(iterator.Current.Value);
					min = (t < min) ? t : min;
				}
			}
			catch (FormatException)
			{
				return "";
			}

			return XmlConvert.ToString(min);
		}

		public new string MonthAbbreviation(string culture)
		{
			try
			{
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetAbbreviatedMonthName(System.DateTime.Now.Month);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string MonthAbbreviation(string d, string culture)
		{
			try
			{
				var date = new DateTz(d);
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetAbbreviatedMonthName(date.D.Month);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string MonthName(string d, string culture)
		{
			try
			{
				var date = new DateTz(d);
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetMonthName(date.D.Month);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public new string MonthName(string culture)
		{
			try
			{
				var ci = new CultureInfo(culture);
				return ci.DateTimeFormat.GetMonthName(System.DateTime.Now.Month);
			}
			catch (Exception)
			{
				return "";
			}
		}

		public string dayAbbreviation_RENAME_ME(string d, string c)
		{
			return DayAbbreviation(d, c);
		}

		public new string dayAbbreviation_RENAME_ME(string c)
		{
			return DayAbbreviation(c);
		}

		public string dayName_RENAME_ME(string d, string c)
		{
			return DayName(d, c);
		}

		public new string dayName_RENAME_ME(string c)
		{
			return DayName(c);
		}

		public string monthAbbreviation_RENAME_ME(string d, string c)
		{
			return MonthAbbreviation(d, c);
		}

		public new string monthAbbreviation_RENAME_ME(string c)
		{
			return MonthAbbreviation(c);
		}

		public string monthName_RENAME_ME(string d, string c)
		{
			return MonthName(d, c);
		}

		public new string monthName_RENAME_ME(string c)
		{
			return MonthName(c);
		}
	}
}
