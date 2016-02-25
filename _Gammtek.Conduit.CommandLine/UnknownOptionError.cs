namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when an unknown option is detected.
	/// </summary>
	public sealed class UnknownOptionError : TokenError
	{
		internal UnknownOptionError(string token)
			: base(ErrorType.UnknownOptionError, token) {}
	}
}
