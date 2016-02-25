using System;
using System.Diagnostics.Contracts;

namespace NDepend.Path
{
	/// <summary>
	///     Represents a relative file path.
	/// </summary>
	/// <remarks>
	///     The extension method <see cref="PathHelpers.ToRelativeFilePath(string)" /> can be called to create a new IRelativeFilePath object from a string.
	/// </remarks>
	public interface IRelativeFilePath : IFilePath, IRelativePath
	{
		/// <summary>
		///     Resolve this relative file from <paramref name="pivotDirectory" />. If this file is "..\Dir2\File.txt" and <paramref name="pivotDirectory" /> is
		///     "C:\Dir1\Dir3", the returned absolute file is "C:\Dir1\Dir2\File.txt".
		/// </summary>
		/// <remarks>
		///     The returned file nor <paramref name="pivotDirectory" /> need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pivotDirectory">The pivot directory from which the absolute path is computed.</param>
		/// <exception cref="ArgumentException">
		///     An absolute path cannot be resolved from <paramref name="pivotDirectory" />.
		///     This can happen for example if <paramref name="pivotDirectory" /> is "C:\Dir1" and this relative file path is "..\..\Dir2\File.txt".
		/// </exception>
		/// <returns>A new absolute file path representing this relative file resolved from <paramref name="pivotDirectory" />.</returns>
		new IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory);

		/// <summary>
		///     Returns a new relative directory path representing a directory with name <paramref name="directoryName" />, located in the same directory as this
		///     file.
		/// </summary>
		/// <param name="directoryName">The brother directory name.</param>
		new IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName);

		/// <summary>
		///     Returns a new relative file path refering to a file with name <paramref name="fileName" />, located in the same directory as this file.
		/// </summary>
		/// <param name="fileName">The brother file name</param>
		new IRelativeFilePath GetBrotherFileWithName(string fileName);


		/// <summary>
		///     Returns a new relative file path representing this file with its file name extension updated to <paramref name="extension" />.
		/// </summary>
		/// <param name="extension">The new file extension. It must begin with a dot followed by one or many characters.</param>
		new IRelativeFilePath UpdateExtension(string extension);
	}
	
	internal abstract class RelativeFilePathContract : IRelativeFilePath
	{
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

		IRelativeDirectoryPath IRelativePath.ParentDirectoryPath
		{
			get { throw new NotImplementedException(); }
		}

		public abstract bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path);

		public abstract bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path, out string failureMessage);

		public IAbsoluteFilePath GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
		{
			Contract.Requires(pivotDirectory != null, "pivotDirectory must not be null");
			throw new NotImplementedException();
		}

		public IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName)
		{
			Contract.Requires(directoryName != null, "directoryName must not be null");
			Contract.Requires(directoryName.Length > 0, "directoryName must not be empty");
			throw new NotImplementedException();
		}

		public IRelativeFilePath GetBrotherFileWithName(string fileName)
		{
			Contract.Requires(fileName != null, "fileName must not be null");
			Contract.Requires(fileName.Length > 0, "fileName must not be empty");
			throw new NotImplementedException();
		}

		public abstract bool HasExtension(string extension);

		public abstract bool IsChildOf(IDirectoryPath parentDirectory);

		public abstract bool NotEquals(object obj);

		public IRelativeFilePath UpdateExtension(string extension)
		{
			Contract.Requires(extension != null, "newExtension must not be null");
			Contract.Requires(extension.Length >= 2, "newExtension must have at least two characters");
			Contract.Requires(extension[0] == '.', "newExtension first character must be a dot");
			throw new NotImplementedException();
		}


		IAbsolutePath IRelativePath.GetAbsolutePathFrom(IAbsoluteDirectoryPath pivotDirectory)
		{
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
