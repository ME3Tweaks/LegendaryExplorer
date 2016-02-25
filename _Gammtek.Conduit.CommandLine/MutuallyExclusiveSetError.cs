namespace Gammtek.Conduit.CommandLine
{
	/// <summary>
	///     Models an error generated when a an option from another set is defined.
	/// </summary>
	public sealed class MutuallyExclusiveSetError : NamedError
	{
		internal MutuallyExclusiveSetError(NameInfo nameInfo)
			: base(ErrorType.MutuallyExclusiveSetError, nameInfo) {}
	}
}
