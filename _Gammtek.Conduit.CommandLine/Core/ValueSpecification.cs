// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using Gammtek.Conduit.CommandLine.Infrastructure;

namespace Gammtek.Conduit.CommandLine.Core
{
	internal sealed class ValueSpecification : Specification
	{
		public ValueSpecification(int index, bool required, int min, int max, Maybe<object> defaultValue, Type conversionType)
			: base(SpecificationType.Value, required, min, max, defaultValue, conversionType)
		{
			Index = index;
		}

		public int Index { get; private set; }

		public static ValueSpecification FromAttribute(ValueAttribute attribute, Type conversionType)
		{
			return new ValueSpecification(
				attribute.Index,
				attribute.Required,
				attribute.Min,
				attribute.Max,
				attribute.DefaultValue.ToMaybe(),
				conversionType);
		}
	}
}
