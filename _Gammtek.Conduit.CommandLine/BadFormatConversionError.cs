namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a value conversion fails.
	/// </summary>
	public sealed class BadFormatConversionError : NamedError
	{
		internal BadFormatConversionError(NameInfo nameInfo)
			: base(ErrorType.BadFormatConversionError, nameInfo) {}
	}
}
