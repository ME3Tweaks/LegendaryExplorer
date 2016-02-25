namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when an invalid token is detected.
	/// </summary>
	public sealed class BadFormatTokenError : TokenError
	{
		internal BadFormatTokenError(string token)
			: base(ErrorType.BadFormatTokenError, token) {}
	}
}
