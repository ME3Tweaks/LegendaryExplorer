namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a user explicit requests help.
	/// </summary>
	public sealed class HelpRequestedError : Error
	{
		internal HelpRequestedError()
			: base(ErrorType.HelpRequestedError) {}
	}
}
