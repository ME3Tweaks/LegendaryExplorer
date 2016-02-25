using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class AbsoluteDirectoryPath : AbsolutePathBase, IAbsoluteDirectoryPath
		{
			internal AbsoluteDirectoryPath(string path)
				: base(path)
			{
				//Debug.Assert(path != null);
				//Debug.Assert(path.Length > 0);
				//Debug.Assert(path.IsValidAbsoluteDirectoryPath());
			}

			public IReadOnlyList<IAbsoluteDirectoryPath> ChildrenDirectoriesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var directoriesInfos = directoryInfo.GetDirectories();
					var childrenDirectoriesPath = directoriesInfos.Select(childDirectoryInfo => ToAbsoluteDirectoryPath(childDirectoryInfo.FullName)).ToList();

					return childrenDirectoriesPath.ToReadOnlyWrappedList();
				}
			}

			public IReadOnlyList<IAbsoluteFilePath> ChildrenFilesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var filesInfos = directoryInfo.GetFiles();
					var childrenFilesPath = filesInfos.Select(fileInfo => fileInfo.FullName.ToAbsoluteFilePath()).ToList();

					return childrenFilesPath.ToReadOnlyWrappedList();
				}
			}

			public DirectoryInfo DirectoryInfo
			{
				get
				{
					if (!Exists)
					{
						throw new DirectoryNotFoundException(CurrentPath);
					}

					var pathForDirectoryInfo = CurrentPath + MiscHelpers.DIR_SEPARATOR_CHAR;

					return new DirectoryInfo(pathForDirectoryInfo);
				}
			}

			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool Exists => Directory.Exists(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				//Debug.Assert(pivotDirectory != null); // Enforced by contract!

				string pathResultUnused, failureReasonUnused;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureReasonUnused);
			}

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage)
			{
				//Debug.Assert(pivotDirectory != null); // Enforced by contract

				string pathResultUnused;

				return AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathResultUnused, out failureMessage);
			}

			public IAbsoluteDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				//Debug.Assert(directoryName != null); // Enforced by contract
				//Debug.Assert(directoryName.Length > 0); // Enforced by contract

				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);

				//Debug.Assert(pathString.IsValidAbsoluteDirectoryPath());

				return new AbsoluteDirectoryPath(pathString);
			}

			public IAbsoluteFilePath GetChildFilePath(string fileName)
			{
				//Debug.Assert(fileName != null); // Enforced by contract
				//Debug.Assert(fileName.Length > 0); // Enforced by contract

				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);

				//Debug.Assert(pathString.IsValidAbsoluteFilePath());

				return new AbsoluteFilePath(pathString);
			}

			public override IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IAbsoluteDirectoryPath).GetRelativePathFrom(path);
			}

			public IAbsoluteDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				//Debug.Assert(directoryName != null); // Enforced by contract
				//Debug.Assert(directoryName.Length > 0); // Enforced by contract

				var path = PathBrowsingHelpers.GetSisterDirectoryWithName(this, directoryName);
				var pathTyped = path as IAbsoluteDirectoryPath;

				//Debug.Assert(pathTyped != null);

				return pathTyped;
			}

			public IAbsoluteFilePath GetSisterFilePath(string fileName)
			{
				//Debug.Assert(fileName != null); // Enforced by contract
				//Debug.Assert(fileName.Length > 0); // Enforced by contract

				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IAbsoluteFilePath;

				//Debug.Assert(pathTyped != null);

				return pathTyped;
			}

			IDirectoryPath IDirectoryPath.GetChildDirectoryPath(string directoryName)
			{
				//Debug.Assert(directoryName != null); // Enforced by contracts
				//Debug.Assert(directoryName.Length > 0); // Enforced by contracts

				return GetChildDirectoryPath(directoryName);
			}

			IFilePath IDirectoryPath.GetChildFilePath(string fileName)
			{
				//Debug.Assert(fileName != null); // Enforced by contracts
				//Debug.Assert(fileName.Length > 0); // Enforced by contracts

				return GetChildFilePath(fileName);
			}

			IRelativeDirectoryPath IAbsoluteDirectoryPath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				//Debug.Assert(pivotDirectory != null);

				string pathRelativeString, failureReason;

				if (!AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathRelativeString, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}

				//Debug.Assert(pathRelativeString != null);
				//Debug.Assert(pathRelativeString.Length > 0);

				return pathRelativeString.ToRelativeDirectoryPath();
			}

			IDirectoryPath IDirectoryPath.GetSisterDirectoryPath(string directoryName)
			{
				//Debug.Assert(directoryName != null); // Enforced by contracts
				//Debug.Assert(directoryName.Length > 0); // Enforced by contracts

				return GetSisterDirectoryPath(directoryName);
			}

			// explicit impl from IDirectoryPath
			IFilePath IDirectoryPath.GetSisterFilePath(string fileName)
			{
				//Debug.Assert(fileName != null); // Enforced by contracts
				//Debug.Assert(fileName.Length > 0); // Enforced by contracts

				return GetSisterFilePath(fileName);
			}
		}
	}
}
