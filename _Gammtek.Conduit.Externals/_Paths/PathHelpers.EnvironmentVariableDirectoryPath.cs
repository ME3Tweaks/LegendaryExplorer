using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class EnvironmentVariableDirectoryPath : EnvironmentVariablePathBase, IEnvironmentVariableDirectoryPath
		{
			internal EnvironmentVariableDirectoryPath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidEnvVarDirectoryPath());
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IEnvironmentVariableDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidEnvVarDirectoryPath());
				return new EnvironmentVariableDirectoryPath(pathString);
			}

			public IEnvironmentVariableFilePath GetChildFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidEnvVarFilePath());
				return new EnvironmentVariableFilePath(pathString);
			}

			public IEnvironmentVariableDirectoryPath GetSisterDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterDirectoryWithName(this, directoryName);
				var pathTyped = path as IEnvironmentVariableDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			//
			//  Path Browsing facilities
			//   
			public IEnvironmentVariableFilePath GetSisterFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IEnvironmentVariableFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			//
			// Path resolving
			//
			public EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteDirectoryPath resolvedPath)
			{
				resolvedPath = null;
				string pathStringResolved;
				if (!TryResolveEnvVar(out pathStringResolved))
				{
					return EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteDirectoryPath())
				{
					return EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath;
				}
				resolvedPath = pathStringResolved.ToAbsoluteDirectoryPath();
				return EnvironmentVariableResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteDirectoryPath resolvedPath, out string failureMessage)
			{
				var resolvingStatus = TryResolve(out resolvedPath);
				switch (resolvingStatus)
				{
					default:
						Debug.Assert(resolvingStatus == EnvironmentVariableResolvingStatus.Success);
						Debug.Assert(resolvedPath != null);
						failureMessage = null;
						return true;
					case EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable:
						failureMessage = GetErrorUnresolvedEnvVarFailureReason();
						return false;
					case EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath:
						failureMessage = GetErrorEnvVarResolvedButCannotConvertToAbsolutePathFailureReason();
						return false;
				}
			}

			public override EnvironmentVariableResolvingStatus TryResolve(out IAbsolutePath resolvedPath)
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
