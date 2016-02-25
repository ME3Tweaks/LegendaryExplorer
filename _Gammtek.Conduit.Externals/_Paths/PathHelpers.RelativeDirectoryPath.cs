using System;
using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class RelativeDirectoryPath : RelativePathBase, IRelativeDirectoryPath
		{
			internal RelativeDirectoryPath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidRelativeDirectoryPath());
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			//
			//  IsFilePath ; IsDirectoryPath
			//
			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IRelativeDirectoryPath).GetAbsolutePathFrom(path);
			}

			public IRelativeDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidRelativeDirectoryPath());
				return new RelativeDirectoryPath(pathString);
			}

			public IRelativeFilePath GetChildFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidRelativeFilePath());
				return new RelativeFilePath(pathString);
			}

			public IRelativeDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				var path = PathBrowsingHelpers.GetSisterDirectoryWithName(this, directoryName);
				var pathTyped = path as IRelativeDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			//
			//  Path Browsing facilities
			//

			public IRelativeFilePath GetSisterFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IRelativeFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
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

			IDirectoryPath IDirectoryPath.GetChildDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				return GetChildDirectoryPath(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				return GetChildFilePath(fileName);
			}

			IDirectoryPath IDirectoryPath.GetSisterDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contracts
				Debug.Assert(directoryName.Length > 0); // Enforced by contracts
				return GetSisterDirectoryPath(directoryName);
			}

			// explicit impl from IDirectoryPath
			IFilePath IDirectoryPath.GetSisterFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contracts
				Debug.Assert(fileName.Length > 0); // Enforced by contracts
				return GetSisterFilePath(fileName);
			}
		}
	}
}
