using System;
using System.Globalization;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.XInclude
{
	public class CircularInclusionException : FatalException
	{
		public CircularInclusionException(Uri uri)
			: base(String.Format(CultureInfo.CurrentCulture, Resources.CircularInclusion, uri.AbsoluteUri)) {}

		public CircularInclusionException(Uri uri, string locationUri, int line, int position)
			: base(String.Format(CultureInfo.CurrentCulture, Resources.CircularInclusionLong, uri.AbsoluteUri, locationUri, line, position)) {}
	}
}
