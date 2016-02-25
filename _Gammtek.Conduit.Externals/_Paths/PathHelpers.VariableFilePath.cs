using System.Collections.Generic;
using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class VariableFilePath : VariablePathBase, IVariableFilePath
		{
			internal VariableFilePath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidVariableFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(CurrentPath);

			//
			//  File Name and File Name Extension
			//
			public string FileName => FileNameHelpers.GetFileName(CurrentPath);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(CurrentPath);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

			public IVariableDirectoryPath GetSisterDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterDirectoryWithName(this, directoryName);
				var pathTyped = path as IVariableDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			//
			//  Path Browsing facilities
			//
			public IVariableFilePath GetSisterFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IVariableFilePath;
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
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath)
			{
				Debug.Assert(variables != null);
				IReadOnlyList<string> unresolvedVariablesUnused;
				return TryResolve(variables, out resolvedPath, out unresolvedVariablesUnused);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				Debug.Assert(variables != null);
				string pathStringResolved;
				if (!base.TryResolve(variables, out pathStringResolved, out unresolvedVariables))
				{
					resolvedPath = null;
					return VariablePathResolvingStatus.ErrorUnresolvedVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteFilePath())
				{
					resolvedPath = null;
					return VariablePathResolvingStatus.ErrorVariableResolvedButCannotConvertToAbsolutePath;
				}
				resolvedPath = pathStringResolved.ToAbsoluteFilePath();
				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteFilePath resolvedPath, out string failureMessage)
			{
				Debug.Assert(variables != null);
				IReadOnlyList<string> unresolvedVariables;
				var status = TryResolve(variables, out resolvedPath, out unresolvedVariables);
				switch (status)
				{
					default:
						Debug.Assert(status == VariablePathResolvingStatus.Success);
						failureMessage = null;
						return true;
					case VariablePathResolvingStatus.ErrorUnresolvedVariable:
						Debug.Assert(unresolvedVariables != null);
						Debug.Assert(unresolvedVariables.Count > 0);
						failureMessage = VariablePathHelpers.GetUnresolvedVariableFailureReason(unresolvedVariables);
						return false;
					case VariablePathResolvingStatus.ErrorVariableResolvedButCannotConvertToAbsolutePath:
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(variables, "file");
						return false;
				}
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath)
			{
				IAbsoluteFilePath pathFileResolved;
				var resolvingStatus = TryResolve(variables, out pathFileResolved);
				resolvedPath = pathFileResolved;
				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteFilePath pathFileResolved;
				var resolvingStatus = TryResolve(variables, out pathFileResolved, out unresolvedVariables);
				resolvedPath = pathFileResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteFilePath pathFileResolved;
				var b = TryResolve(variables, out pathFileResolved, out failureMessage);
				resolvedPath = pathFileResolved;
				return b;
			}

			public IVariableFilePath UpdateExtension(string extension)
			{
				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');
				var pathString = PathBrowsingHelpers.UpdateExtension(this, extension);
				Debug.Assert(pathString.IsValidVariableFilePath());
				return new VariableFilePath(pathString);
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
