// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using Gammtek.Conduit.CommandLine.Infrastructure;

namespace Gammtek.Conduit.CommandLine.Core
{
	internal sealed class OptionSpecification : Specification
	{
		public OptionSpecification(string shortName, string longName, bool required, string setName, int min, int max, Maybe<object> defaultValue,
			Type conversionType, string helpText, string metaValue)
			: base(SpecificationType.Option, required, min, max, defaultValue, conversionType)
		{
			ShortName = shortName;
			LongName = longName;
			SetName = setName;
			HelpText = helpText;
			MetaValue = metaValue;
		}

		public string HelpText { get; private set; }

		public string LongName { get; private set; }

		public string MetaValue { get; private set; }

		public string SetName { get; private set; }

		public string ShortName { get; private set; }

		public static OptionSpecification FromAttribute(OptionAttribute attribute, Type conversionType)
		{
			return new OptionSpecification(
				attribute.ShortName,
				attribute.LongName,
				attribute.Required,
				attribute.SetName,
				attribute.Min,
				attribute.Max,
				attribute.DefaultValue.ToMaybe(),
				conversionType,
				attribute.HelpText,
				attribute.MetaValue);
		}
	}
}
