using System;
using System.Diagnostics.Contracts;

namespace NDepend.Path
{
	/// <summary>
	///     Represents a path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IEnvVarPath : IPath
	{
		/// <summary>
		///     Gets the environment variable string, prefixed and suffixed with two percents char.
		/// </summary>
		string EnvVar { get; }

		/// <summary>
		///     Returns a new directory path prefixed with an environment variable, representing the parent directory of this path prefixed with an environment
		///     variable.
		/// </summary>
		/// <exception cref="InvalidOperationException">This path prefixed with an environment variable has no parent directory.</exception>
		new IEnvVarDirectoryPath ParentDirectoryPath { get; }

		/// <summary>
		///     Returns <see cref="EnvVarPathResolvingStatus" />.<see cref="EnvVarPathResolvingStatus.Success" /> if this path is prefixed with an environment
		///     variable that can be resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute path resolved returned by this method.</param>
		EnvVarPathResolvingStatus TryResolve(out IAbsolutePath resolvedPath);

		/// <summary>
		///     Returns <i>true</i> if this path is prefixed with an environment variable that can be resolved into a drive letter or a UNC absolute path.
		/// </summary>
		/// <param name="resolvedPath">It is the absolute path resolved returned by this method.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage);
	}
	
	internal abstract class EnvVarPathContract : IEnvVarPath
	{
		public string EnvVar
		{
			get
			{
				Contract.Ensures(Contract.Result<string>() != null, "returned string is not null");
				Contract.Ensures(Contract.Result<string>().Length > 0, "returned string is not empty");
				throw new NotImplementedException();
			}
		}

		public abstract bool HasParentDirectory { get; }

		public abstract bool IsAbsolutePath { get; }

		public abstract bool IsDirectoryPath { get; }

		public abstract bool IsEnvVarPath { get; }

		public abstract bool IsFilePath { get; }

		public abstract bool IsRelativePath { get; }

		public abstract bool IsVariablePath { get; }

		public abstract IDirectoryPath ParentDirectoryPath { get; }

		public abstract PathMode PathMode { get; }

		IEnvVarDirectoryPath IEnvVarPath.ParentDirectoryPath
		{
			get
			{
				Contract.Ensures(Contract.Result<IEnvVarDirectoryPath>() != null, "returned path is not null");
				throw new NotImplementedException();
			}
		}

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public EnvVarPathResolvingStatus TryResolve(out IAbsolutePath resolvedPath)
		{
			throw new NotImplementedException();
		}

		public bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage)
		{
			throw new NotImplementedException();
		}
	}
}
