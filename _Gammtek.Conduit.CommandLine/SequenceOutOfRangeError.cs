namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a sequence value lacks elements.
	/// </summary>
	public sealed class SequenceOutOfRangeError : NamedError
	{
		internal SequenceOutOfRangeError(NameInfo nameInfo)
			: base(ErrorType.SequenceOutOfRangeError, nameInfo) {}
	}
}
