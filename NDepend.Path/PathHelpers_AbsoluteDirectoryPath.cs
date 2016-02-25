using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class AbsoluteDirectoryPath : AbsolutePathBase, IAbsoluteDirectoryPath
		{
			internal AbsoluteDirectoryPath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidAbsoluteDirectoryPath());
			}

			public IReadOnlyList<IAbsoluteDirectoryPath> ChildrenDirectoriesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var directoriesInfos = directoryInfo.GetDirectories();
					var childrenDirectoriesPath = new List<IAbsoluteDirectoryPath>();
					foreach (var childDirectoryInfo in directoriesInfos)
					{
						childrenDirectoriesPath.Add(childDirectoryInfo.FullName.ToAbsoluteDirectoryPath());
					}
					return childrenDirectoriesPath.ToReadOnlyWrappedList();
				}
			}

			public IReadOnlyList<IAbsoluteFilePath> ChildrenFilesPath
			{
				get
				{
					var directoryInfo = DirectoryInfo;
					var filesInfos = directoryInfo.GetFiles();
					var childrenFilesPath = new List<IAbsoluteFilePath>();
					foreach (var fileInfo in filesInfos)
					{
						childrenFilesPath.Add(fileInfo.FullName.ToAbsoluteFilePath());
					}
					return childrenFilesPath.ToReadOnlyWrappedList();
				}
			}

			public DirectoryInfo DirectoryInfo
			{
				get
				{
					if (!Exists)
					{
						throw new DirectoryNotFoundException(m_PathString);
					}

					// 4May2011 Need to append DirectorySeparatorChar to get the Directory info
					//          else for example the pathString of "C:" would become "." !!
					var pathForDirectoryInfo = m_PathString + MiscHelpers.DIR_SEPARATOR_CHAR;
					return new DirectoryInfo(pathForDirectoryInfo);
				}
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(m_PathString);


			//
			//  Operations that requires physical access
			//
			public override bool Exists => Directory.Exists(m_PathString);

			//
			//  IsFilePath ; IsDirectoryPath
			//
			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public override bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Debug.Assert(pivotDirectory != null); // Enforced by contract!
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

			public IAbsoluteDirectoryPath GetChildDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidAbsoluteDirectoryPath());
				return new AbsoluteDirectoryPath(pathString);
			}

			public IAbsoluteFilePath GetChildFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidAbsoluteFilePath());
				return new AbsoluteFilePath(pathString);
			}


			public override IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath path)
			{
				return (this as IAbsoluteDirectoryPath).GetRelativePathFrom(path);
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

			//
			//  Absolute/Relative pathString conversion
			//
			IRelativeDirectoryPath IAbsoluteDirectoryPath.GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory)
			{
				Debug.Assert(pivotDirectory != null);
				string pathRelativeString, failureReason;
				if (!AbsoluteRelativePathHelpers.TryGetRelativePath(pivotDirectory, this, out pathRelativeString, out failureReason))
				{
					throw new ArgumentException(failureReason);
				}
				Debug.Assert(pathRelativeString != null);
				Debug.Assert(pathRelativeString.Length > 0);
				return pathRelativeString.ToRelativeDirectoryPath();
			}
		}
	}
}
