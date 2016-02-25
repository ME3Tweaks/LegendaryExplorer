using System.Collections.Generic;
using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private sealed class VariableDirectoryPath : VariablePathBase, IVariableDirectoryPath
		{
			internal VariableDirectoryPath(string path)
				: base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
				Debug.Assert(path.IsValidVariableDirectoryPath());
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(CurrentPath);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IVariableDirectoryPath GetChildDirectoryPath(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidVariableDirectoryPath());
				return new VariableDirectoryPath(pathString);
			}

			public IVariableFilePath GetChildFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidVariableFilePath());
				return new VariableFilePath(pathString);
			}

			public IVariableDirectoryPath GetSisterDirectoryPath(string directoryName)
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
			public IVariableFilePath GetSisterFilePath(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetSisterFileWithName(this, fileName);
				var pathTyped = path as IVariableFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			//
			// Path resolving
			//
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables,
				out IAbsoluteDirectoryPath resolvedPath)
			{
				Debug.Assert(variables != null);
				IReadOnlyList<string> unresolvedVariablesUnused;
				return TryResolve(variables, out resolvedPath, out unresolvedVariablesUnused);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables,
				out IAbsoluteDirectoryPath resolvedPath, out IReadOnlyList<string> unresolvedVariables)
			{
				Debug.Assert(variables != null);
				string pathStringResolved;
				if (!base.TryResolve(variables, out pathStringResolved, out unresolvedVariables))
				{
					resolvedPath = null;
					return VariablePathResolvingStatus.ErrorUnresolvedVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteDirectoryPath())
				{
					resolvedPath = null;
					return VariablePathResolvingStatus.ErrorVariableResolvedButCannotConvertToAbsolutePath;
				}
				resolvedPath = pathStringResolved.ToAbsoluteDirectoryPath();
				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsoluteDirectoryPath resolvedPath,
				out string failureMessage)
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
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(variables, "directory");
						return false;
				}
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var resolvingStatus = TryResolve(variables, out pathDirectoryResolved);
				resolvedPath = pathDirectoryResolved;
				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var resolvingStatus = TryResolve(variables, out pathDirectoryResolved, out unresolvedVariables);
				resolvedPath = pathDirectoryResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> variables, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var b = TryResolve(variables, out pathDirectoryResolved, out failureMessage);
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
