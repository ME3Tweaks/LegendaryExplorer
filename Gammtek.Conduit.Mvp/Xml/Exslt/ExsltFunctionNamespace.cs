using System;

namespace Gammtek.Conduit.Mvp.Xml.Exslt
{
	[Flags]
	public enum ExsltFunctionNamespace
	{
		None = 0,
		DatesAndTimes = 1,
		Math = 2,
		RegularExpressions = 4,
		Sets = 8,
		Strings = 16,
		GdnDatesAndTimes = 32,
		GdnSets = 64,
		GdnMath = 128,
		GdnRegularExpressions = 256,
		GdnStrings = 512,
		Random = 1024,
		GdnDynamic = 2056,

		AllExslt = DatesAndTimes | Math | Random | RegularExpressions | Sets | Strings,

		All = DatesAndTimes | Math | Random | RegularExpressions | Sets | Strings |
			GdnDatesAndTimes | GdnSets | GdnMath | GdnRegularExpressions | GdnStrings | GdnDynamic
	}
}
