using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class EnvironmentVariableFilePath : EnvironmentVariablePathBase, IEnvironmentVariableFilePath
		{
			internal EnvironmentVariableFilePath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidEnvVarFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);

			//
			//  File Name and File Name Extension
			//
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public IEnvironmentVariableDirectoryPath GetSisterDirectoryWithName(string directoryName)
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
			public IEnvironmentVariableFilePath GetSisterFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IEnvironmentVariableFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public bool HasExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				return FileNameHelpers.HasExtension(CurrentPath, extension);
			}

			//
			// Path resolving
			//
			public EnvironmentVariableResolvingStatus TryResolve(out IAbsoluteFilePath resolvedPath)
			{
				resolvedPath = null;
				string pathStringResolved;
				if (!TryResolveEnvVar(out pathStringResolved))
				{
					return EnvironmentVariableResolvingStatus.UnresolvedEnvironmentVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteFilePath())
				{
					return EnvironmentVariableResolvingStatus.CannotConvertToAbsolutePath;
				}
				resolvedPath = pathStringResolved.ToAbsoluteFilePath();
				return EnvironmentVariableResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteFilePath resolvedPath, out string failureMessage)
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
				IAbsoluteFilePath pathFileResolved;
				var resolvingStatus = TryResolve(out pathFileResolved);
				resolvedPath = pathFileResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteFilePath pathFileResolved;
				var b = TryResolve(out pathFileResolved, out failureMessage);
				resolvedPath = pathFileResolved;
				return b;
			}

			public IEnvironmentVariableFilePath UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);
				Debug.Assert(pathString.IsValidEnvVarFilePath());
				return new EnvironmentVariableFilePath(pathString);
			}

			IDirectoryPath IFilePath.GetSisterDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				return GetSisterDirectoryWithName(directoryName);
			}

			// Explicit Impl methods
			IFilePath IFilePath.GetSisterFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				return GetSisterFileWithName(fileName);
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
