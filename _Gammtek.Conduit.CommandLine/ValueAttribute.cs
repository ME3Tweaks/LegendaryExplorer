// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an value specification, or better how to handle values not bound to options.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class ValueAttribute : Attribute
	{
		private object _defaultValue;
		private int _max;
		private int _min;

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.ValueAttribute" /> class.
		/// </summary>
		public ValueAttribute(int index)
		{
			Index = index;
			_min = -1;
			_max = -1;
		}

		/// <summary>
		///     Gets or sets mapped property default value.
		/// </summary>
		public object DefaultValue
		{
			get { return _defaultValue; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_defaultValue = value;
			}
		}

		/// <summary>
		///     Gets the position this option has on the command line.
		/// </summary>
		public int Index { get; private set; }

		/// <summary>
		///     When applied to <see cref="System.Collections.Generic.IEnumerable{T}" /> properties defines
		///     the upper range of items.
		/// </summary>
		/// <remarks>If not set, no upper range is enforced.</remarks>
		public int Max
		{
			get { return _max; }
			set
			{
				if (value < 0)
				{
					throw new ArgumentNullException("value");
				}

				_max = value;
			}
		}

		/// <summary>
		///     When applied to <see cref="System.Collections.Generic.IEnumerable{T}" /> properties defines
		///     the lower range of items.
		/// </summary>
		/// <remarks>If not set, no lower range is enforced.</remarks>
		public int Min
		{
			get { return _min; }
			set
			{
				if (value < 0)
				{
					throw new ArgumentNullException("value");
				}

				_min = value;
			}
		}

		/// <summary>
		///     Gets or sets a value indicating whether a command line option is required.
		/// </summary>
		public bool Required { get; set; }
	}
}
