namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when an option lacks its value.
	/// </summary>
	public sealed class MissingValueOptionError : NamedError
	{
		internal MissingValueOptionError(NameInfo nameInfo)
			: base(ErrorType.MissingValueOptionError, nameInfo) {}
	}
}
