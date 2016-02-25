using System;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class RelativeFilePath : RelativePathBase, IRelativeFilePath
		{
			internal RelativeFilePath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidRelativeFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(m_PathString);


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

			public override IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IRelativeFilePath).GetAbsolutePathFrom(path);
			}

			public IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherDirectoryWithName(this, directoryName);
				var pathTyped = path as IRelativeDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}


			//
			//  Path Browsing facilities
			//

			public IRelativeFilePath GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherFileWithName(this, fileName);
				var pathTyped = path as IRelativeFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public bool HasExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				return FileNameHelpers.HasExtension(m_PathString, extension);
			}

			public IRelativeFilePath UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);
				Debug.Assert(pathString.IsValidRelativeFilePath());
				return new RelativeFilePath(pathString);
			}


			//
			//  Absolute/Relative pathString conversion
			//
			IAbsoluteFilePath IRelativeFilePath.GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Debug.Assert(path != null); // Enforced by contracts!
				string pathAbsolute, failureReason;
				if (!AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out pathAbsolute, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}
				Debug.Assert(pathAbsolute != null);
				Debug.Assert(pathAbsolute.Length > 0);
				return (pathAbsolute + MiscHelpers.DIR_SEPARATOR_CHAR + FileName).ToAbsoluteFilePath();
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
