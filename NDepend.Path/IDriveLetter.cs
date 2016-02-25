using System;
using System.Diagnostics.Contracts;
using System.IO;

namespace NDepend.Path
{
	/// <summary>
	///     Represents a drive on file system.
	/// </summary>
	public interface IDriveLetter
	{
		/// <summary>
		///     Returns a DriveInfo object representing this drive.
		/// </summary>
		/// <exception cref="DriveNotFoundException">This drive doesn't refer to an existing drive.</exception>
		/// <seealso cref="P:NDepend.Path.IAbsoluteDirectoryPath.DirectoryInfo" />
		/// <seealso cref="P:NDepend.Path.IAbsoluteFilePath.FileInfo" />
		DriveInfo DriveInfo { get; }

		/// <summary>
		///     Returns the letter character of this drive.
		/// </summary>
		/// <remarks>
		///     The letter returned can be upper or lower case.
		/// </remarks>
		char Letter { get; }

		/// <summary>Returns true if obj is null, is not an IDrive, or is an IDrive representing a different drive than this drive (case insensitive).</summary>
		bool NotEquals(object obj);
	}
	
	internal abstract class DriveLetterContract : IDriveLetter
	{
		public DriveInfo DriveInfo
		{
			get { throw new NotImplementedException(); }
		}

		public char Letter
		{
			get { throw new NotImplementedException(); }
		}

		public bool NotEquals(object obj)
		{
			throw new NotImplementedException();
		}
	}
}
