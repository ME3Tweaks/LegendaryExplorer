namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a required option is required.
	/// </summary>
	public sealed class MissingRequiredOptionError : NamedError
	{
		internal MissingRequiredOptionError(NameInfo nameInfo)
			: base(ErrorType.MissingRequiredOptionError, nameInfo) {}
	}
}
