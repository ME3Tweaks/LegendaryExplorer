namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Discriminator enumeration of <see cref="CommandLine.Error" /> derivates.
	/// </summary>
	public enum ErrorType
	{
		/// <summary>
		///     Value of <see cref="CommandLine.BadFormatTokenError" /> type.
		/// </summary>
		BadFormatTokenError,

		/// <summary>
		///     Value of <see cref="CommandLine.MissingValueOptionError" /> type.
		/// </summary>
		MissingValueOptionError,

		/// <summary>
		///     Value of <see cref="CommandLine.UnknownOptionError" /> type.
		/// </summary>
		UnknownOptionError,

		/// <summary>
		///     Value of <see cref="CommandLine.MissingRequiredOptionError" /> type.
		/// </summary>
		MissingRequiredOptionError,

		/// <summary>
		///     Value of <see cref="CommandLine.MutuallyExclusiveSetError" /> type.
		/// </summary>
		MutuallyExclusiveSetError,

		/// <summary>
		///     Value of <see cref="CommandLine.BadFormatConversionError" /> type.
		/// </summary>
		BadFormatConversionError,

		/// <summary>
		///     Value of <see cref="CommandLine.SequenceOutOfRangeError" /> type.
		/// </summary>
		SequenceOutOfRangeError,

		/// <summary>
		///     Value of <see cref="CommandLine.NoVerbSelectedError" /> type.
		/// </summary>
		NoVerbSelectedError,

		/// <summary>
		///     Value of <see cref="CommandLine.BadVerbSelectedError" /> type.
		/// </summary>
		BadVerbSelectedError,

		/// <summary>
		///     Value of <see cref="CommandLine.HelpRequestedError" /> type.
		/// </summary>
		HelpRequestedError,

		/// <summary>
		///     Value of <see cref="CommandLine.HelpVerbRequestedError" /> type.
		/// </summary>
		HelpVerbRequestedError
	}
}
