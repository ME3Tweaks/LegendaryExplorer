using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.XPath;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	public class ExsltDatesAndTimes
	{
		private readonly string[] _dayAbbrevs = { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };

		private readonly string[] _dayNames =
		{
			"Sunday", "Monday", "Tuesday",
			"Wednesday", "Thursday", "Friday", "Saturday"
		};

		private readonly string[] _monthAbbrevs =
		{
			"Jan", "Feb", "Mar", "Apr", "May", "Jun",
			"Jul", "Aug", "Sep", "Oct", "Nov", "Dec"
		};

		private readonly string[] _monthNames =
		{
			"January", "February", "March", "April", "May", "June",
			"July", "August", "September",
			"October", "November", "December"
		};

		public string Add(string datetime, string duration)
		{
			try
			{
				var date = ExsltDateTimeFactory.ParseDate(datetime);
				//TimeSpan timespan = System.Xml.XmlConvert.ToTimeSpan(duration); 

				var durationRe = new Regex(
					@"^(-)?" + // May begin with a - sign
						@"P" + // Must contain P as first or 2nd char
						@"(?=\d+|(?:T\d+))" + // Must contain at least one digit after P or after PT
						@"(?:(\d+)Y)?" + // May contain digits plus Y for year
						@"(?:(\d+)M)?" + // May contain digits plus M for month
						@"(?:(\d+)D)?" + // May contain digits plus D for day
						@"(?=T\d+)?" + // If there is a T here, must be digits afterwards
						@"T?" + // May contain a T
						@"(?:(\d+)H)?" + // May contain digits plus H for hours
						@"(?:(\d+)M)?" + // May contain digits plus M for minutes
						@"(?:(\d+)S)?" + // May contain digits plus S for seconds
						@"$",
					RegexOptions.IgnoreCase | RegexOptions.Singleline
					);

				var m = durationRe.Match(duration);

				int negation = 1,
					years = 0,
					months = 0,
					days = 0,
					hours = 0,
					minutes = 0,
					seconds = 0;

				if (!m.Success)
				{
					return "";
				}
				//date.d = date.d.Add(timespan);
				// According to the XML datetime spec at 
				// http://www.w3.org/TR/xmlschema-2/#adding-durations-to-dateTimes, 
				// we need to first add the year/month part, then we can add the 
				// day/hour/minute/second part

				if (CultureInfo.InvariantCulture.CompareInfo.Compare(m.Groups[1].Value, "-") == 0)
				{
					negation = -1;
				}

				if (m.Groups[2].Length > 0)
				{
					years = negation * int.Parse(m.Groups[2].Value);
				}

				if (m.Groups[3].Length > 0)
				{
					months = negation * int.Parse(m.Groups[3].Value);
				}

				if (m.Groups[4].Length > 0)
				{
					days = negation * int.Parse(m.Groups[4].Value);
				}

				if (m.Groups[5].Length > 0)
				{
					hours = negation * int.Parse(m.Groups[5].Value);
				}

				if (m.Groups[6].Length > 0)
				{
					minutes = negation * int.Parse(m.Groups[6].Value);
				}

				if (m.Groups[7].Length > 0)
				{
					seconds = negation * int.Parse(m.Groups[7].Value);
				}

				date.D = date.D.AddYears(years);
				date.D = date.D.AddMonths(months);
				date.D = date.D.AddDays(days);
				date.D = date.D.AddHours(hours);
				date.D = date.D.AddMinutes(minutes);
				date.D = date.D.AddSeconds(seconds);

				// we should return the same format as passed in

				// return date.ToString("yyyy-MM-dd\"T\"HH:mm:ss");			
				return date.ToString();
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public string AddDuration(string duration1, string duration2)
		{
			try
			{
				var timespan1 = XmlConvert.ToTimeSpan(duration1);
				var timespan2 = XmlConvert.ToTimeSpan(duration2);
				return XmlConvert.ToString(timespan1.Add(timespan2));
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public string Date()
		{
			var dtz = new DateTz();
			return dtz.ToString();
		}

		public string Date(string d)
		{
			try
			{
				var dtz = new DateTz(d);
				return dtz.ToString();
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public string DateTime()
		{
			var d = new DateTimeTz();
			return DateTimeImpl(d);
		}

		public string DateTime(string s)
		{
			try
			{
				var d = new DateTimeTz(s);
				return DateTimeImpl(d);
			}
			catch (FormatException)
			{
				return "";
			}
		}

		internal string DateTimeImpl(DateTimeTz dtz)
		{
			return dtz.ToString();
		}

		private string DayAbbreviation(int dow)
		{
			if (dow < 0 || dow >= _dayAbbrevs.Length)
			{
				return String.Empty;
			}
			return _dayAbbrevs[dow];
		}

		public string DayAbbreviation()
		{
			return DayAbbreviation((int) System.DateTime.Now.DayOfWeek);
		}

		public string DayAbbreviation(string d)
		{
			try
			{
				var date = new DateTz(d);
				return DayAbbreviation((int) date.D.DayOfWeek);
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public double DayInMonth()
		{
			return System.DateTime.Now.Day;
		}

		public double DayInMonth(string d)
		{
			try
			{
				var date = new Day(d);
				return date.D.Day;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public double DayInWeek()
		{
			return ((int) System.DateTime.Now.DayOfWeek) + 1;
		}

		public double DayInWeek(string d)
		{
			try
			{
				var date = new DateTz(d);
				return ((int) date.D.DayOfWeek) + 1;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public double DayInYear()
		{
			return System.DateTime.Now.DayOfYear;
		}

		public double DayInYear(string d)
		{
			try
			{
				var date = new DateTz(d);
				return date.D.DayOfYear;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		private string DayName(int dow)
		{
			if (dow < 0 || dow >= _dayNames.Length)
			{
				return String.Empty;
			}
			return _dayNames[dow];
		}

		public string DayName()
		{
			return DayName((int) System.DateTime.Now.DayOfWeek);
		}

		public string DayName(string d)
		{
			try
			{
				var date = new DateTz(d);
				return DayName((int) date.D.DayOfWeek);
			}
			catch (FormatException)
			{
				return "";
			}
		}

		private double DayOfWeekInMonth(int day)
		{
			// day of week in month = floor(((date-1) / 7)) + 1
			return ((day - 1) / 7) + 1;
		}

		public double DayOfWeekInMonth()
		{
			return DayOfWeekInMonth(System.DateTime.Now.Day);
		}

		public double DayOfWeekInMonth(string d)
		{
			try
			{
				var date = new DateTz(d);
				return DayOfWeekInMonth(date.D.Day);
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public string Difference(string start, string end)
		{
			try
			{
				var startdate = ExsltDateTimeFactory.ParseDate(start);
				var enddate = ExsltDateTimeFactory.ParseDate(end);

				// The rules are pretty tricky.  basically, interpret both strings as the least-
				// specific format
				if (ReferenceEquals(startdate.GetType(), typeof (YearTz)) ||
					ReferenceEquals(enddate.GetType(), typeof (YearTz)))
				{
					var retString = new StringBuilder("");

					var yearDiff = enddate.D.Year - startdate.D.Year;

					if (yearDiff < 0)
					{
						retString.Append('-');
					}

					retString.Append('P');
					retString.Append(Math.Abs(yearDiff));
					retString.Append('Y');

					return retString.ToString();
				}
				if (ReferenceEquals(startdate.GetType(), typeof (YearMonth)) ||
					ReferenceEquals(enddate.GetType(), typeof (YearMonth)))
				{
					var retString = new StringBuilder("");

					var yearDiff = enddate.D.Year - startdate.D.Year;
					var monthDiff = enddate.D.Month - startdate.D.Month;

					// Borrow from the year if necessary
					if ((yearDiff > 0) && (Math.Sign(monthDiff) == -1))
					{
						yearDiff--;
						monthDiff += 12;
					}
					else if ((yearDiff < 0) && (Math.Sign(monthDiff) == 1))
					{
						yearDiff++;
						monthDiff -= 12;
					}

					if ((yearDiff < 0) || ((yearDiff == 0) && (monthDiff < 0)))
					{
						retString.Append('-');
					}
					retString.Append('P');
					if (yearDiff != 0)
					{
						retString.Append(Math.Abs(yearDiff));
						retString.Append('Y');
					}
					retString.Append(Math.Abs(monthDiff));
					retString.Append('M');

					return retString.ToString();
				}
				// Simulate casting to the most truncated format.  i.e. if one 
				// Arg is DateTZ and the other is DateTimeTZ, get rid of the time
				// for both.
				if (ReferenceEquals(startdate.GetType(), typeof (DateTz)) ||
					ReferenceEquals(enddate.GetType(), typeof (DateTz)))
				{
					startdate = new DateTz(startdate.D.ToString("yyyy-MM-dd"));
					enddate = new DateTz(enddate.D.ToString("yyyy-MM-dd"));
				}

				var ts = enddate.D.Subtract(startdate.D);
				return XmlConvert.ToString(ts);
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public string Duration()
		{
			return Duration(Seconds());
		}

		public string Duration(double seconds)
		{
			return XmlConvert.ToString(TimeSpan.FromSeconds(seconds));
		}

		public string FormatDate(string d, string format)
		{
			try
			{
				var oDate = ExsltDateTimeFactory.ParseDateTime(d);
				var retString = new StringBuilder("");

				for (var i = 0; i < format.Length;)
				{
					var s = i;
					switch (format[i])
					{
						case 'G': //        era designator          (Text)              AD
							while (i < format.Length && format[i] == 'G')
							{
								i++;
							}

							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Year < 0 ? "BC" : "AD");
							}
							break;

						case 'y': //        year                    (Number)            1996
							while (i < format.Length && format[i] == 'y')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(i - s == 2
									? (oDate.D.Year % 100).ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0')
									: oDate.D.Year.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'M': //        month in year           (Text &amp; Number)     July &amp; 07
							while (i < format.Length && format[i] == 'M')
							{
								i++;
							}

							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (Month)) ||
								ReferenceEquals(oDate.GetType(), typeof (MonthDay)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								if (i - s <= 2)
								{
									retString.Append(oDate.D.Month.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
								}
								else if (i - s == 3)
								{
									retString.Append(MonthAbbreviation(oDate.D.Month));
								}
								else
								{
									retString.Append(MonthName(oDate.D.Month));
								}
							}
							break;
						case 'd': //        day in month            (Number)            10
							while (i < format.Length && format[i] == 'd')
							{
								i++;
							}

							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (MonthDay)) ||
								ReferenceEquals(oDate.GetType(), typeof (Day)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Day.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'h': //        hour in am/pm (1~12)    (Number)            12
							while (i < format.Length && format[i] == 'h')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								var hour = oDate.D.Hour % 12;
								if (0 == hour)
								{
									hour = 12;
								}
								retString.Append(hour.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'H': //        hour in day (0~23)      (Number)            0
							while (i < format.Length && format[i] == 'H')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Hour.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'm': //        minute in hour          (Number)            30
							while (i < format.Length && format[i] == 'm')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Minute.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 's': //        second in minute        (Number)            55
							while (i < format.Length && format[i] == 's')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Second.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'S': //        millisecond             (Number)            978
							while (i < format.Length && format[i] == 'S')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Millisecond.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'E': //        day in week             (Text)              Tuesday
							while (i < format.Length && format[i] == 'E')
							{
								i++;
							}

							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(i - s <= 3 ? DayAbbreviation((int) oDate.D.DayOfWeek) : DayName((int) oDate.D.DayOfWeek));
							}
							break;
						case 'D': //        day in year             (Number)            189
							while (i < format.Length && format[i] == 'D')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.DayOfYear.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'F': //        day of week in month    (Number)            2 (2nd Wed in July)
							while (i < format.Length && format[i] == 'F')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (MonthDay)) ||
								ReferenceEquals(oDate.GetType(), typeof (Day)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(DayOfWeekInMonth(oDate.D.Day).ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'w': //        week in year            (Number)            27
							while (i < format.Length && format[i] == 'w')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(WeekInYear(oDate.D));
							}
							break;
						case 'W': //        week in month           (Number)            2
							while (i < format.Length && format[i] == 'W')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(WeekInMonth(oDate.D));
							}
							break;
						case 'a': //        am/pm marker            (Text)              PM
							while (i < format.Length && format[i] == 'a')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								retString.Append(oDate.D.Hour < 12 ? "AM" : "PM");
							}
							break;
						case 'k': //        hour in day (1~24)      (Number)            24
							while (i < format.Length && format[i] == 'k')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								var hour = oDate.D.Hour + 1;
								retString.Append(hour.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'K': //        hour in am/pm (0~11)    (Number)            0
							while (i < format.Length && format[i] == 'K')
							{
								i++;
							}
							if (ReferenceEquals(oDate.GetType(), typeof (DateTimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (DateTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearMonth)) ||
								ReferenceEquals(oDate.GetType(), typeof (TimeTz)) ||
								ReferenceEquals(oDate.GetType(), typeof (YearTz)))
							{
								var hour = oDate.D.Hour % 12;
								retString.Append(hour.ToString(CultureInfo.InvariantCulture).PadLeft(i - s, '0'));
							}
							break;
						case 'z': //        time zone               (Text)              Pacific Standard Time
							while (i < format.Length && format[i] == 'z')
							{
								i++;
							}
							//
							// BUGBUG: Need to convert to full timezone names or timezone abbrevs
							// if they are available.  Now cheating by using GMT offsets.
							retString.Append(oDate.GetGmtOffsetTimeZone());
							break;
						case 'Z': //			rfc 822 time zone
							while (i < format.Length && format[i] == 'Z')
							{
								i++;
							}
							retString.Append(oDate.Get822TimeZone());
							break;
						case '\'': //        escape for text         (Delimiter)
							if (i < format.Length && format[i + 1] == '\'')
							{
								i++;
								while (i < format.Length && format[i] == '\'')
								{
									i++;
								}
								retString.Append("'");
							}
							else
							{
								i++;
								while (i < format.Length && format[i] != '\'' && i <= format.Length)
								{
									retString.Append(format.Substring(i++, 1));
								}
								if (i >= format.Length)
								{
									return "";
								}
								i++;
							}
							break;
						default:
							retString.Append(format[i]);
							i++;
							break;
					}
				}

				return retString.ToString();
			}

			catch (FormatException)
			{
				return "";
			}
		}

		public double HourInDay()
		{
			return System.DateTime.Now.Hour;
		}

		public double HourInDay(string d)
		{
			try
			{
				var date = new TimeTz(d);
				return date.D.Hour;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		private static bool IsLeapYear(int year)
		{
			try
			{
				return CultureInfo.CurrentCulture.Calendar.IsLeapYear(year);
			}
			catch
			{
				return false;
			}
		}

		public bool LeapYear()
		{
			return IsLeapYear((int) Year());
		}

		public bool LeapYear(string d)
		{
			var y = Year(d);

			return y != Double.NaN && IsLeapYear((int) y);
		}

		public double MinuteInHour()
		{
			return System.DateTime.Now.Minute;
		}

		public double MinuteInHour(string d)
		{
			try
			{
				var date = new TimeTz(d);
				return date.D.Minute;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		private string MonthAbbreviation(int month)
		{
			if (month < 1 || month > _monthAbbrevs.Length)
			{
				return String.Empty;
			}
			return _monthAbbrevs[month - 1];
		}

		public string MonthAbbreviation()
		{
			return MonthAbbreviation((int) MonthInYear());
		}

		public string MonthAbbreviation(string d)
		{
			var month = MonthInYear(d);
			return month == Double.NaN ? "" : MonthAbbreviation((int) month);
		}

		public double MonthInYear()
		{
			return System.DateTime.Now.Month;
		}

		public double MonthInYear(string d)
		{
			try
			{
				var date = new Month(d);
				return date.D.Month;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		private string MonthName(int month)
		{
			if (month < 1 || month > _monthNames.Length)
			{
				return String.Empty;
			}
			return _monthNames[month - 1];
		}

		public string MonthName()
		{
			return MonthName((int) MonthInYear());
		}

		public string MonthName(string d)
		{
			var month = MonthInYear(d);
			return month == Double.NaN ? "" : MonthName((int) month);
		}

		public string ParseDate(string d, string format)
		{
			try
			{
				var date = System.DateTime.ParseExact(d, format, CultureInfo.CurrentCulture);
				return XmlConvert.ToString(date, XmlDateTimeSerializationMode.RoundtripKind);
			}
			catch (FormatException)
			{
				return "";
			}
		}

		public double SecondInMinute()
		{
			return System.DateTime.Now.Second;
		}

		public double SecondInMinute(string d)
		{
			try
			{
				var date = new TimeTz(d);
				return date.D.Second;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		private static double Seconds(ExsltDateTime d)
		{
			var epoch = new DateTime(1970, 1, 1, 0, 0, 0, 0, CultureInfo.InvariantCulture.Calendar);
			return d.ToUniversalTime().Subtract(epoch).TotalSeconds;
		}

		public double Seconds()
		{
			return Seconds(new DateTimeTz());
		}

		public double Seconds(string datetime)
		{
			try
			{
				return Seconds(ExsltDateTimeFactory.ParseDate(datetime));
			}
			catch (FormatException)
			{
				;
			} //might be a duration

			try
			{
				var duration = XmlConvert.ToTimeSpan(datetime);
				return duration.TotalSeconds;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public string Sum(XPathNodeIterator iterator)
		{
			var sum = new TimeSpan(0, 0, 0, 0);

			if (iterator.Count == 0)
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

			return XmlConvert.ToString(sum); //XmlConvert.ToString(sum);
		}

		public string Time()
		{
			var t = new TimeTz();
			return t.ToString();
		}

		public string Time(string d)
		{
			try
			{
				var t = new TimeTz(d);
				return t.ToString();
			}
			catch (FormatException)
			{
				return "";
			}
		}

		private static double WeekInMonth(DateTime d)
		{
			//
			// mon = 1
			// tue = 2
			// sun = 7
			// week = ceil(((date-day) / 7)) + 1

			var offset = (d.DayOfWeek == DayOfWeek.Sunday) ? 7 : (double) d.DayOfWeek;
			return Math.Ceiling((d.Day - offset) / 7) + 1;
		}

		public double WeekInMonth()
		{
			return WeekInMonth(System.DateTime.Now);
		}

		public double WeekInMonth(string d)
		{
			try
			{
				var date = new DateTz(d);
				return WeekInMonth(date.D);
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public double WeekInYear(DateTime d)
		{
			return CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(d,
				CalendarWeekRule.FirstFourDayWeek,
				DayOfWeek.Monday);
		}

		public double WeekInYear()
		{
			return WeekInYear(System.DateTime.Now);
		}

		public double WeekInYear(string d)
		{
			try
			{
				var dtz = new DateTz(d);
				return WeekInYear(dtz.D);
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public double Year()
		{
			return System.DateTime.Now.Year;
		}

		public double Year(string d)
		{
			try
			{
				var date = new YearTz(d);
				return date.D.Year;
			}
			catch (FormatException)
			{
				return Double.NaN;
			}
		}

		public string addDuration_RENAME_ME(string duration1, string duration2)
		{
			return AddDuration(duration1, duration2);
		}

		public string dateTime_RENAME_ME()
		{
			return DateTime();
		}

		public string dateTime_RENAME_ME(string d)
		{
			return DateTime(d);
		}

		public string dayAbbreviation_RENAME_ME()
		{
			return DayAbbreviation();
		}

		public string dayAbbreviation_RENAME_ME(string d)
		{
			return DayAbbreviation(d);
		}

		public double dayInMonth_RENAME_ME()
		{
			return DayInMonth();
		}

		public double dayInMonth_RENAME_ME(string d)
		{
			return DayInMonth(d);
		}

		public double dayInWeek_RENAME_ME()
		{
			return DayInWeek();
		}

		public double dayInWeek_RENAME_ME(string d)
		{
			return DayInWeek(d);
		}

		public double dayInYear_RENAME_ME()
		{
			return DayInYear();
		}

		public double dayInYear_RENAME_ME(string d)
		{
			return DayInYear(d);
		}

		public string dayName_RENAME_ME()
		{
			return DayName();
		}

		public string dayName_RENAME_ME(string d)
		{
			return DayName(d);
		}

		public double dayOfWeekInMonth_RENAME_ME()
		{
			return DayOfWeekInMonth();
		}

		public double dayOfWeekInMonth_RENAME_ME(string d)
		{
			return DayOfWeekInMonth(d);
		}

		public string formatDate_RENAME_ME(string d, string format)
		{
			return FormatDate(d, format);
		}

		public double hourInDay_RENAME_ME()
		{
			return HourInDay();
		}

		public double hourInDay_RENAME_ME(string d)
		{
			return HourInDay(d);
		}

		public bool leapYear_RENAME_ME()
		{
			return LeapYear();
		}

		public bool leapYear_RENAME_ME(string d)
		{
			return LeapYear(d);
		}

		public double minuteInHour_RENAME_ME()
		{
			return MinuteInHour();
		}

		public double minuteInHour_RENAME_ME(string d)
		{
			return MinuteInHour(d);
		}

		public string monthAbbreviation_RENAME_ME()
		{
			return MonthAbbreviation();
		}

		public string monthAbbreviation_RENAME_ME(string d)
		{
			return MonthAbbreviation(d);
		}

		public double monthInYear_RENAME_ME()
		{
			return MonthInYear();
		}

		public double monthInYear_RENAME_ME(string d)
		{
			return MonthInYear(d);
		}

		public string monthName_RENAME_ME()
		{
			return MonthName();
		}

		public string monthName_RENAME_ME(string d)
		{
			return MonthName(d);
		}

		public string parseDate_RENAME_ME(string d, string format)
		{
			return ParseDate(d, format);
		}

		public double secondInMinute_RENAME_ME()
		{
			return SecondInMinute();
		}

		public double secondInMinute_RENAME_ME(string d)
		{
			return SecondInMinute(d);
		}

		public double weekInMonth_RENAME_ME()
		{
			return WeekInMonth();
		}

		public double weekInMonth_RENAME_ME(string d)
		{
			return WeekInMonth(d);
		}

		public double weekInYear_RENAME_ME()
		{
			return WeekInYear();
		}

		public double weekInYear_RENAME_ME(string d)
		{
			return WeekInYear(d);
		}

		internal class DateTimeTz : ExsltDateTime
		{
			public DateTimeTz() {}

			public DateTimeTz(string inS)
				: base(inS) {}

			public DateTimeTz(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "yyyy-MM-dd\"T\"HH:mm:ss"; }
			}
		}

		internal class DateTz : ExsltDateTime
		{
			public DateTz() {}

			public DateTz(string inS)
				: base(inS) {}

			public DateTz(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-ddzzz",
						"yyyy-MM-ddZ",
						"yyyy-MM-dd"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "yyyy-MM-dd"; }
			}
		}

		internal class Day : ExsltDateTime
		{
			public Day() {}

			public Day(string inS)
				: base(inS) {}

			public Day(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-dd",
						"---dd",
						"--MM-dd"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "---dd"; }
			}
		}

		internal abstract class ExsltDateTime
		{
			protected CultureInfo Ci = new CultureInfo("en-US");
			public DateTime D;
			public TimeSpan Ts = new TimeSpan(TimeSpan.MinValue.Ticks);

			protected ExsltDateTime()
			{
				D = System.DateTime.Now;
				var tz = TimeZone.CurrentTimeZone;
				Ts = tz.GetUtcOffset(D);
			}

			protected ExsltDateTime(string inS)
			{
				var s = inS.Trim();
				D = System.DateTime.ParseExact(s, ExpectedFormats, Ci, DateTimeStyles.AdjustToUniversal);

				if (s.EndsWith("Z"))
				{
					Ts = new TimeSpan(0, 0, 0);
				}
				else if (s.Length > 6)
				{
					var zoneStr = s.Substring(s.Length - 6, 6);
					if (zoneStr[3] != ':')
					{
						return;
					}
					var hours = Int32.Parse(zoneStr.Substring(0, 3));
					var minutes = Int32.Parse(zoneStr.Substring(4, 2));
					if (hours < 0)
					{
						minutes = -minutes;
					}

					Ts = new TimeSpan(hours, minutes, 0);
					D = D.Add(Ts); // Adjust to time zone relative time	
				}
			}

			protected ExsltDateTime(ExsltDateTime inS)
			{
				D = inS.D;
				Ts = inS.Ts;
			}

			protected abstract string[] ExpectedFormats { get; }
			protected abstract string OutputFormat { get; }

			public string Get822TimeZone()
			{
				var retString = new StringBuilder();

				// if no ts specified, output without ts
				if (HasTimeZone())
				{
					if (0 == Ts.Hours && 0 == Ts.Minutes)
					{
						retString.Append("GMT");
					}
					else if (Ts.Hours >= 0 && Ts.Minutes >= 0)
					{
						retString.Append('+');
						retString.Append(Ts.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
						retString.Append(Ts.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
					}
					else
					{
						retString.Append('-');
						retString.Append((-Ts.Hours).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
						retString.Append((-Ts.Minutes).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
					}
				}

				return retString.ToString();
			}

			public string GetGmtOffsetTimeZone()
			{
				var retString = new StringBuilder();

				// if no ts specified, output without ts
				if (!HasTimeZone())
				{
					return retString.ToString();
				}
				retString.Append("GMT");
				if (0 != Ts.Hours || 0 != Ts.Minutes)
				{
					retString.Append(GetTimeZone());
				}

				return retString.ToString();
			}

			public string GetTimeZone()
			{
				var retString = new StringBuilder();

				// if no ts specified, output without ts
				if (!HasTimeZone())
				{
					return retString.ToString();
				}
				if (0 == Ts.Hours && 0 == Ts.Minutes)
				{
					retString.Append('Z');
				}
				else if (Ts.Hours >= 0 && Ts.Minutes >= 0)
				{
					retString.Append('+');
					retString.Append(Ts.Hours.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
					retString.Append(':');
					retString.Append(Ts.Minutes.ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
				}
				else
				{
					retString.Append('-');
					retString.Append((-Ts.Hours).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
					retString.Append(':');
					retString.Append((-Ts.Minutes).ToString(CultureInfo.InvariantCulture).PadLeft(2, '0'));
				}

				return retString.ToString();
			}

			public bool HasTimeZone()
			{
				return TimeSpan.MinValue.Ticks != Ts.Ticks;
			}

			public override string ToString()
			{
				return ToString(OutputFormat);
			}

			public string ToString(String of)
			{
				var retString = new StringBuilder("");

				retString.Append(D.ToString(of));
				retString.Append(GetTimeZone());

				return retString.ToString();
			}

			public DateTime ToUniversalTime()
			{
				if (!HasTimeZone())
				{
					return D;
				}
				return D.Subtract(Ts);
			}
		}

		private static class ExsltDateTimeFactory
		{
			public static ExsltDateTime ParseDate(string d)
			{
				// Try each potential class, from most specific to least specific.

				// First DateTimeTZ
				try
				{
					var t = new DateTimeTz(d);
					return t;
				}
				catch (FormatException) {}

				// Next Date
				try
				{
					var t = new DateTz(d);
					return t;
				}
				catch (FormatException) {}

				// Next YearMonth
				try
				{
					var t = new YearMonth(d);
					return t;
				}
				catch (FormatException) {}

				// Finally Year -- don't catch the exception for the last type
				{
					var t = new YearTz(d);
					return t;
				}
			}

			public static ExsltDateTime ParseDateTime(string d)
			{
				// First try any of the classes in ParseDate
				try
				{
					return ParseDate(d);
				}
				catch (FormatException) {}

				try
				{
					var t = new TimeTz(d);
					return t;
				}
				catch (FormatException) {}

				try
				{
					var t = new MonthDay(d);
					return t;
				}
				catch (FormatException) {}

				try
				{
					var t = new Month(d);
					return t;
				}
				catch (FormatException) {}

				// Finally day -- don't catch the exception
				{
					var t = new Day(d);
					return t;
				}
			}
		}

		internal class Month : ExsltDateTime
		{
			public Month() {}

			public Month(string inS)
				: base(inS) {}

			public Month(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-dd",
						"yyyy-MM",
						"--MM--"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "--MM--"; }
			}
		}

		internal class MonthDay : ExsltDateTime
		{
			public MonthDay() {}

			public MonthDay(string inS)
				: base(inS) {}

			public MonthDay(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-dd",
						"--MM-dd"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "--MM-dd"; }
			}
		}

		internal class TimeTz : ExsltDateTime
		{
			public TimeTz(string inS)
				: base(inS) {}

			public TimeTz() {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"HH:mm:sszzz",
						"HH:mm:ssZ",
						"HH:mm:ss"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "HH:mm:ss"; }
			}
		}

		internal class YearMonth : ExsltDateTime
		{
			public YearMonth() {}

			public YearMonth(string inS)
				: base(inS) {}

			public YearMonth(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-dd",
						"yyyy-MM"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "yyyy-MM"; }
			}
		}

		internal class YearTz : ExsltDateTime
		{
			public YearTz() {}

			public YearTz(string inS)
				: base(inS) {}

			public YearTz(ExsltDateTime inS)
				: base(inS) {}

			protected override string[] ExpectedFormats
			{
				get
				{
					return new[]
					{
						"yyyy-MM-dd\"T\"HH:mm:sszzz",
						"yyyy-MM-dd\"T\"HH:mm:ssZ",
						"yyyy-MM-dd\"T\"HH:mm:ss",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.fff",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.ffZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.ff",
						"yyyy-MM-dd\"T\"HH:mm:ss.fzzz",
						"yyyy-MM-dd\"T\"HH:mm:ss.fZ",
						"yyyy-MM-dd\"T\"HH:mm:ss.f",
						"yyyy-MM-dd",
						"yyyy-MM",
						"yyyy"
					};
				}
			}

			protected override string OutputFormat
			{
				get { return "yyyy"; }
			}
		}
	}
}
