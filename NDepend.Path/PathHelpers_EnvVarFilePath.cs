using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class EnvVarFilePath : EnvVarPathBase, IEnvVarFilePath
		{
			internal EnvVarFilePath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidEnvVarFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(m_PathString);


			//
			//  File Name and File Name Extension
			//
			public string FileName => FileNameHelpers.GetFileName(m_PathString);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(m_PathString);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

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

			public bool HasExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				return FileNameHelpers.HasExtension(m_PathString, extension);
			}


			//
			// Path resolving
			//
			public EnvVarPathResolvingStatus TryResolve(out IAbsoluteFilePath resolvedFilePath)
			{
				resolvedFilePath = null;
				string pathStringResolved;
				if (!TryResolveEnvVar(out pathStringResolved))
				{
					return EnvVarPathResolvingStatus.ErrorUnresolvedEnvVar;
				}
				if (!pathStringResolved.IsValidAbsoluteFilePath())
				{
					return EnvVarPathResolvingStatus.ErrorEnvVarResolvedButCannotConvertToAbsolutePath;
				}
				resolvedFilePath = pathStringResolved.ToAbsoluteFilePath();
				return EnvVarPathResolvingStatus.Success;
			}

			public bool TryResolve(out IAbsoluteFilePath resolvedFilePath, out string failureMessage)
			{
				var resolvingStatus = TryResolve(out resolvedFilePath);
				switch (resolvingStatus)
				{
					default:
						Debug.Assert(resolvingStatus == EnvVarPathResolvingStatus.Success);
						Debug.Assert(resolvedFilePath != null);
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

			public IEnvVarFilePath UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);
				Debug.Assert(pathString.IsValidEnvVarFilePath());
				return new EnvVarFilePath(pathString);
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
