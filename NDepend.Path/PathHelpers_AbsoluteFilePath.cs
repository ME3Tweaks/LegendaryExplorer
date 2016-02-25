using System;
using System.Diagnostics;
using System.IO;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class AbsoluteFilePath : AbsolutePathBase, IAbsoluteFilePath
		{
			internal AbsoluteFilePath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidAbsoluteFilePath());
			}


			//
			//  Operations that requires physical access
			//
			public override bool Exists => File.Exists(m_PathString);

			public string FileExtension => FileNameHelpers.GetFileNameExtension(m_PathString);

			public FileInfo FileInfo
			{
				get
				{
					if (!Exists)
					{
						throw new FileNotFoundException(m_PathString);
					}
					return new FileInfo(m_PathString);
				}
			}

			//
			//  File Name and File Name Extension
			//
			public string FileName => FileNameHelpers.GetFileName(m_PathString);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(m_PathString);

			//
			//  IsFilePath ; IsDirectoryPath
			//
			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Debug.Assert(pivotDirectory != null); // Enforced by contract
				string pathResultUnused, failureReasonUnused;
				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureReasonUnused);
			}

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage)
			{
				Debug.Assert(pivotDirectory != null); // Enforced by contract
				string pathResultUnused;
				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureMessage);
			}

			public IAbsoluteDirectoryPath GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherDirectoryWithName(this, directoryName);
				var pathTyped = path as IAbsoluteDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}


			//
			//  Path Browsing facilities
			//
			public IAbsoluteFilePath GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherFileWithName(this, fileName);
				var pathTyped = path as IAbsoluteFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public override IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Debug.Assert(pivotDirectory != null); // Enforced by contract
				return (this as IAbsoluteFilePath).GetRelativePathFrom(pivotDirectory);
			}

			public bool HasExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				return FileNameHelpers.HasExtension(m_PathString, extension);
			}

			public IAbsoluteFilePath UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);
				Debug.Assert(pathString.IsValidAbsoluteFilePath());
				return new AbsoluteFilePath(pathString);
			}

			IDirectoryPath IFilePath.GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				return GetBrotherDirectoryWithName(directoryName);
			}

			// Explicit Impl methods
			IFilePath IFilePath.GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				return GetBrotherFileWithName(fileName);
			}

			//
			//  Absolute/Relative pathString conversion
			//
			IRelativeFilePath IAbsoluteFilePath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Debug.Assert(pivotDirectory != null); // Enforced by contract
				string pathRelative, failureReason;
				if (!AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathRelative, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}
				Debug.Assert(pathRelative != null);
				Debug.Assert(pathRelative.Length > 0);
				return new RelativeFilePath(pathRelative + MiscHelpers.DIR_SEPARATOR_CHAR + FileName);
			}

			IFilePath IFilePath.UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				return UpdateExtension(extension);
			}
		}
	}
}
