namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when an unknown verb is detected.
	/// </summary>
	public sealed class BadVerbSelectedError : TokenError
	{
		internal BadVerbSelectedError(string token)
			: base(ErrorType.BadVerbSelectedError, token) {}
	}
}
