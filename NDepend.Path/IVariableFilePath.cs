using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace NDepend.Path
{
	/// <summary>
	///     Represents a file path on file system, prefixed with an environment variable.
	/// </summary>
	public interface IVariableFilePath : IFilePath, IVariablePath
	{
		/// <summary>
		///     Returns a new directory path containing variables, representing a directory with name <paramref name="directoryName" />, located in the same
		///     directory as this file.
		/// </summary>
		/// <param name="directoryName">The brother directory name.</param>
		new IVariableDirectoryPath GetBrotherDirectoryWithName(string directoryName);


		/// <summary>
		///     Returns a new file path containing variables, refering to a file with name <paramref name="fileName" />, located in the same directory as this
		///     file.
		/// </summary>
		/// <param name="fileName">The brother file name</param>
		new IVariableFilePath GetBrotherFileWithName(string fileName);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="values" /> and the path can be resolved into
		///     a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="values">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedFilePath">
		///     It is the absolute file path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath);

		/// <summary>
		///     Returns <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.Success" /> if
		///     <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="values" /> and the path can be resolved into
		///     a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="values">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedFilePath">
		///     It is the absolute file path resolved obtained if this method returns <see cref="VariablePathResolvingStatus" />.
		///     <see cref="VariablePathResolvingStatus.Success" />.
		/// </param>
		/// <param name="unresolvedVariables">
		///     This list contains one or several variables names unresolved, if this method returns
		///     <see cref="VariablePathResolvingStatus" />.<see cref="VariablePathResolvingStatus.ErrorUnresolvedVariable" />.
		/// </param>
		VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath,
			out IReadOnlyList<string> unresolvedVariables);

		/// <summary>
		///     Returns <i>true</i> if <see cref="IVariablePath.AllVariables" /> of this path can be resolved from <paramref name="values" /> and the
		///     path can be resolved into a drive letter or a UNC absolute file path.
		/// </summary>
		/// <param name="values">It is the sequence of pairs <i>[variable name/variable value]</i> used to resolve the path.</param>
		/// <param name="resolvedFilePath">It is the absolute file path resolved obtained if this method returns <i>true</i>.</param>
		/// <param name="failureMessage">If <i>false</i> is returned, <paramref name="failureMessage" /> contains the plain english description of the failure.</param>
		bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath, out string failureMessage);


		/// <summary>
		///     Returns a new file path containing variables, representing this file with its file name extension updated to <paramref name="extension" />.
		/// </summary>
		/// <param name="extension">The new file extension. It must begin with a dot followed by one or many characters.</param>
		new IVariableFilePath UpdateExtension(string extension);
	}
	
	internal abstract class VariableFilePathContract : IVariableFilePath
	{
		public abstract IReadOnlyList<string> AllVariables { get; }

		public abstract string FileExtension { get; }

		public abstract string FileName { get; }

		public abstract string FileNameWithoutExtension { get; }

		public abstract bool HasParentDirectory { get; }

		public abstract bool IsAbsolutePath { get; }

		public abstract bool IsDirectoryPath { get; }

		public abstract bool IsEnvVarPath { get; }

		public abstract bool IsFilePath { get; }

		public abstract bool IsRelativePath { get; }

		public abstract bool IsVariablePath { get; }

		public abstract IDirectoryPath ParentDirectoryPath { get; }

		public abstract PathMode PathMode { get; }

		public abstract string PrefixVariable { get; }

		IVariableDirectoryPath IVariablePath.ParentDirectoryPath
		{
			get { throw new NotImplementedException(); }
		}

		public IVariableDirectoryPath GetBrotherDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IVariableFilePath GetBrotherFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public abstract bool HasExtension(string extension);

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath)
		{
			Contract.Requires(values != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath,
			out IReadOnlyList<string> unresolvedVariables)
		{
			Contract.Requires(values != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath, out string failureMessage)
		{
			Contract.Requires(values != null, "variablesValues must not be null");
			throw new NotImplementedException();
		}

		public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath);

		public abstract VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath,
			out IReadOnlyList<string> unresolvedVariables);

		public abstract bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath, out string failureMessage);

		public abstract EnvVarPathResolvingStatus TryResolve(out IAbsolutePath pathResolved);

		public abstract bool TryResolve(out IAbsolutePath pathResolved, out string failureReason);

		public IVariableFilePath UpdateExtension(string extension)
		{
			Contract.Requires(extension != null, "newExtension must not be null");
			Contract.Requires(extension.Length >= 2, "newExtension must have at least two characters");
			Contract.Requires(extension[0] == '.', "newExtension first character must be a dot");
			throw new NotImplementedException();
		}

		IDirectoryPath IFilePath.GetBrotherDirectoryWithName(string directoryName)
		{
			throw new NotImplementedException();
		}

		IFilePath IFilePath.GetBrotherFileWithName(string fileName)
		{
			throw new NotImplementedException();
		}

		IFilePath IFilePath.UpdateExtension(string extension)
		{
			throw new NotImplementedException();
		}
	}
}
