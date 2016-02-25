namespace NDepend.Path
{
	/// <summary>
	///     Defines the result of the <see cref="IEnvVarPath" />.<see cref="IEnvVarPath.TryResolve(out NDepend.Path.IAbsolutePath)" /> method.
	/// </summary>
	public enum EnvVarPathResolvingStatus
	{
		/// <summary>
		///     The environment variable has been resolved, and the resulting path is a valid absolute path.
		/// </summary>
		Success,

		/// <summary>
		///     The environment variable cannot be resolved.
		/// </summary>
		ErrorUnresolvedEnvVar,

		/// <summary>
		///     The environment variable has been resolved but the resulting path is not a valid absolute path.
		/// </summary>
		ErrorEnvVarResolvedButCannotConvertToAbsolutePath
	}
}
