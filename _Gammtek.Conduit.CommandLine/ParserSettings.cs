// Copyright 2005-2013 Giacomo Stelluti Scala & Contributors. All rights reserved. See doc/License.md in the project root for license information.

using System;
using System.Globalization;
using System.IO;
using Gammtek.Conduit.CommandLine.Infrastructure;

namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Provides settings for <see cref="CommandLine.Parser" />. Once consumed cannot be reused.
	/// </summary>
	public class ParserSettings : IDisposable
	{
		private bool _caseSensitive;
		private bool _disposed;
		private bool _enableDashDash;
		private TextWriter _helpWriter;
		private bool _ignoreUnknownArguments;
		private CultureInfo _parsingCulture;

		/// <summary>
		///     Initializes a new instance of the <see cref="ParserSettings" /> class.
		/// </summary>
		public ParserSettings()
		{
			_caseSensitive = true;
			_parsingCulture = CultureInfo.InvariantCulture;
		}

		/// <summary>
		///     Finalizes an instance of the <see cref="CommandLine.ParserSettings" /> class.
		/// </summary>
		~ParserSettings()
		{
			Dispose(false);
		}

		/// <summary>
		///     Gets or sets a value indicating whether perform case sensitive comparisons.
		/// </summary>
		public bool CaseSensitive
		{
			get { return _caseSensitive; }
			set { PopsicleSetter.Set(Consumed, ref _caseSensitive, value); }
		}

		/// <summary>
		///     Gets or sets a value indicating whether enable double dash '--' syntax,
		///     that forces parsing of all subsequent tokens as values.
		/// </summary>
		public bool EnableDashDash
		{
			get { return _enableDashDash; }
			set { PopsicleSetter.Set(Consumed, ref _enableDashDash, value); }
		}

		/// <summary>
		///     Gets or sets the <see cref="System.IO.TextWriter" /> used for help method output.
		///     Setting this property to null, will disable help screen.
		/// </summary>
		public TextWriter HelpWriter
		{
			get { return _helpWriter; }
			set { PopsicleSetter.Set(Consumed, ref _helpWriter, value); }
		}

		/// <summary>
		///     Gets or sets a value indicating whether the parser shall move on to the next argument and ignore the given argument if it
		///     encounter an unknown arguments
		/// </summary>
		/// <value>
		///     <c>true</c> to allow parsing the arguments with different class options that do not have all the arguments.
		/// </value>
		/// <remarks>
		///     This allows fragmented version class parsing, useful for project with add-on where add-ons also requires command line arguments but
		///     when these are unknown by the main program at build time.
		/// </remarks>
		public bool IgnoreUnknownArguments
		{
			get { return _ignoreUnknownArguments; }
			set { PopsicleSetter.Set(Consumed, ref _ignoreUnknownArguments, value); }
		}

		/// <summary>
		///     Gets or sets the culture used when parsing arguments to typed properties.
		/// </summary>
		/// <remarks>
		///     Default is invariant culture, <see cref="System.Globalization.CultureInfo.InvariantCulture" />.
		/// </remarks>
		public CultureInfo ParsingCulture
		{
			get { return _parsingCulture; }
			set
			{
				if (value == null)
				{
					throw new ArgumentNullException("value");
				}

				PopsicleSetter.Set(Consumed, ref _parsingCulture, value);
			}
		}

		internal bool Consumed { get; set; }

		internal StringComparer NameComparer
		{
			get
			{
				return CaseSensitive
					? StringComparer.Ordinal
					: StringComparer.OrdinalIgnoreCase;
			}
		}

		private void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			if (disposing)
			{
				if (HelpWriter != null)
				{
					_helpWriter.Dispose();
					_helpWriter = null;
				}

				_disposed = true;
			}
		}

		/// <summary>
		///     Frees resources owned by the instance.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);

			GC.SuppressFinalize(this);
		}
	}
}
