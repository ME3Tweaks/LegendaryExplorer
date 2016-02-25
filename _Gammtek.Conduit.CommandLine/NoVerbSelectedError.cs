namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when no verb is selected.
	/// </summary>
	public sealed class NoVerbSelectedError : Error
	{
		internal NoVerbSelectedError()
			: base(ErrorType.NoVerbSelectedError) {}
	}
}
