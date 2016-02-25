using System;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class RelativeDirectoryPath : RelativePathBase, IRelativeDirectoryPath
		{
			internal RelativeDirectoryPath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidRelativeDirectoryPath());
			}


			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(m_PathString);

			//
			//  IsFilePath ; IsDirectoryPath
			//
			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IRelativeDirectoryPath).GetAbsolutePathFrom(path);
			}

			public IRelativeDirectoryPath GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
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
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				var path = PathBrowsingHelpers.GetBrotherFileWithName(this, fileName);
				var pathTyped = path as IRelativeFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public IRelativeDirectoryPath GetChildDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidRelativeDirectoryPath());
				return new RelativeDirectoryPath(pathString);
			}

			public IRelativeFilePath GetChildFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidRelativeFilePath());
				return new RelativeFilePath(pathString);
			}


			//
			//  Absolute/Relative pathString conversion
			//
			IAbsoluteDirectoryPath IRelativeDirectoryPath.GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Debug.Assert(path != null); // Enforced by contracts!
				string pathAbsolute, failureReason;
				if (!AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out pathAbsolute, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}
				Debug.Assert(pathAbsolute != null);
				Debug.Assert(pathAbsolute.Length > 0);
				return pathAbsolute.ToAbsoluteDirectoryPath();
			}

			IDirectoryPath IDirectoryPath.GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				return GetBrotherDirectoryWithName(directoryName);
			}


			// explicit impl from IDirectoryPath
			IFilePath IDirectoryPath.GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				return GetBrotherFileWithName(fileName);
			}

			IDirectoryPath IDirectoryPath.GetChildDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				return GetChildDirectoryWithName(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				return GetChildFileWithName(fileName);
			}
		}
	}
}
