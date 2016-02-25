using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class EnvVarDirectoryPath : EnvVarPathBase, IEnvVarDirectoryPath
		{
			internal EnvVarDirectoryPath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidEnvVarDirectoryPath());
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(m_PathString);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IEnvVarDirectoryPath GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherDirectoryWithName(this, directoryName);
				var pathTyped = path as IEnvVarDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}


			//
			//  Path Browsing facilities
			//   
			public IEnvVarFilePath GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherFileWithName(this, fileName);
				var pathTyped = path as IEnvVarFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public IEnvVarDirectoryPath GetChildDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidEnvVarDirectoryPath());
				return new EnvVarDirectoryPath(pathString);
			}

			public IEnvVarFilePath GetChildFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidEnvVarFilePath());
				return new EnvVarFilePath(pathString);
			}


			//
			// Path resolving
			//
			public EnvVarPathResolvingStatus TryResolve(out IAbsoluteDirectoryPath resolvedDirectoryPath)
			{
				resolvedDirectoryPath = null;
				string pathStringResolved;
				if (!TryResolveEnvVar(out pathStringResolved))
				{
					return EnvVarPathResolvingStatus.ErrorUnresolvedEnvVar;
				}
				if (!pathStringResolved.IsValidAbsoluteDirectoryPath())
				{
					return EnvVarPathResolvingStatus.ErrorEnvVarResolvedButCannotConvertToAbsolutePath;
				}
				resolvedDirectoryPath = pathStringResolved.ToAbsoluteDirectoryPath();
				return EnvVarPathResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteDirectoryPath resolvedDirectoryPath, out string failureMessage)
			{
				var resolvingStatus = TryResolve(out resolvedDirectoryPath);
				switch (resolvingStatus)
				{
					default:
						Debug.Assert(resolvingStatus == EnvVarPathResolvingStatus.Success);
						Debug.Assert(resolvedDirectoryPath != null);
						failureMessage = null;
						return true;
					case EnvVarPathResolvingStatus.ErrorUnresolvedEnvVar:
						failureMessage = GetErrorUnresolvedEnvVarFailureReason();
						return false;
					case EnvVarPathResolvingStatus.ErrorEnvVarResolvedButCannotConvertToAbsolutePath:
						failureMessage = GetErrorEnvVarResolvedButCannotConvertToAbsolutePathFailureReason();
						return false;
				}
			}

			public override EnvVarPathResolvingStatus TryResolve(out IAbsolutePath resolvedPath)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var resolvingStatus = TryResolve(out pathDirectoryResolved);
				resolvedPath = pathDirectoryResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var b = TryResolve(out pathDirectoryResolved, out failureMessage);
				resolvedPath = pathDirectoryResolved;
				return b;
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
