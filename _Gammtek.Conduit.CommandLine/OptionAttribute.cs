using System;
using Gammtek.Conduit.CommandLine.Infrastructure;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an option specification.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property)]
	public sealed class OptionAttribute : Attribute
	{
		private object _defaultValue;
		private string _helpText;
		private int _max;
		private string _metaValue;
		private int _min;
		private string _setName;

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.OptionAttribute" /> class.
		///     The default long name will be inferred from target property.
		/// </summary>
		public OptionAttribute()
			: this(string.Empty, string.Empty) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.OptionAttribute" /> class.
		/// </summary>
		/// <param name="longName">The long name of the option.</param>
		public OptionAttribute(string longName)
			: this(string.Empty, longName) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.OptionAttribute" /> class.
		/// </summary>
		/// <param name="shortName">The short name of the option.</param>
		/// <param name="longName">The long name of the option or null if not used.</param>
		public OptionAttribute(char shortName, string longName)
			: this(shortName.ToOneCharString(), longName) {}

		/// <summary>
		///     Initializes a new instance of the <see cref="CommandLine.OptionAttribute" /> class.
		/// </summary>
		/// <param name="shortName">The short name of the option..</param>
		public OptionAttribute(char shortName)
			: this(shortName.ToOneCharString(), string.Empty) {}

		private OptionAttribute(string shortName, string longName)
		{
			if (shortName == null)
			{
				throw new ArgumentNullException("shortName");
			}

			if (longName == null)
			{
				throw new ArgumentNullException("longName");
			}

			ShortName = shortName;
			LongName = longName;
			_setName = string.Empty;
			_min = -1;
			_max = -1;
			_helpText = string.Empty;
			_metaValue = string.Empty;
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
		///     Gets or sets a short description of this command line option. Usually a sentence summary.
		/// </summary>
		public string HelpText
		{
			get { return _helpText; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_helpText = value;
			}
		}

		/// <summary>
		///     Gets long name of this command line option. This name is usually a single english word.
		/// </summary>
		public string LongName { get; private set; }

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
		///     Gets or sets mapped property meta value. Usually an uppercase hint of required value type.
		/// </summary>
		public string MetaValue
		{
			get { return _metaValue; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_metaValue = value;
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

		/// <summary>
		///     Gets or sets the option's mutually exclusive set name.
		/// </summary>
		public string SetName
		{
			get { return _setName; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				_setName = value;
			}
		}

		/// <summary>
		///     Gets a short name of this command line option, made of one character.
		/// </summary>
		public string ShortName { get; private set; }
	}
}
