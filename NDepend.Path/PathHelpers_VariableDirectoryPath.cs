using System.Collections.Generic;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class VariableDirectoryPath : VariablePathBase, IVariableDirectoryPath
		{
			internal VariableDirectoryPath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidVariableDirectoryPath());
			}

			//
			//  DirectoryName
			//
			public string DirectoryName => MiscHelpers.GetLastName(m_PathString);

			public override bool IsDirectoryPath => true;

			public override bool IsFilePath => false;

			public IVariableDirectoryPath GetBrotherDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherDirectoryWithName(this, directoryName);
				var pathTyped = path as IVariableDirectoryPath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}


			//
			//  Path Browsing facilities
			//   
			public IVariableFilePath GetBrotherFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var path = PathBrowsingHelpers.GetBrotherFileWithName(this, fileName);
				var pathTyped = path as IVariableFilePath;
				Debug.Assert(pathTyped != null);
				return pathTyped;
			}

			public IVariableDirectoryPath GetChildDirectoryWithName(string directoryName)
			{
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildDirectoryWithName(this, directoryName);
				Debug.Assert(pathString.IsValidVariableDirectoryPath());
				return new VariableDirectoryPath(pathString);
			}

			public IVariableFilePath GetChildFileWithName(string fileName)
			{
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				var pathString = PathBrowsingHelpers.GetChildFileWithName(this, fileName);
				Debug.Assert(pathString.IsValidVariableFilePath());
				return new VariableFilePath(pathString);
			}


			//
			// Path resolving
			//
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values,
				out IAbsoluteDirectoryPath resolvedDirectoryPath)
			{
				Debug.Assert(values != null);
				IReadOnlyList<string> unresolvedVariablesUnused;
				return TryResolve(values, out resolvedDirectoryPath, out unresolvedVariablesUnused);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values,
				out IAbsoluteDirectoryPath resolvedDirectoryPath, out IReadOnlyList<string> unresolvedVariables)
			{
				Debug.Assert(values != null);
				string pathStringResolved;
				if (!base.TryResolve(values, out pathStringResolved, out unresolvedVariables))
				{
					resolvedDirectoryPath = null;
					return VariablePathResolvingStatus.ErrorUnresolvedVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteDirectoryPath())
				{
					resolvedDirectoryPath = null;
					return VariablePathResolvingStatus.ErrorVariableResolvedButCannotConvertToAbsolutePath;
				}
				resolvedDirectoryPath = pathStringResolved.ToAbsoluteDirectoryPath();
				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteDirectoryPath resolvedDirectoryPath,
				out string failureMessage)
			{
				Debug.Assert(values != null);
				IReadOnlyList<string> unresolvedVariables;
				var status = TryResolve(values, out resolvedDirectoryPath, out unresolvedVariables);
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
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(values, "directory");
						return false;
				}
			}


			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var resolvingStatus = TryResolve(values, out pathDirectoryResolved);
				resolvedPath = pathDirectoryResolved;
				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var resolvingStatus = TryResolve(values, out pathDirectoryResolved, out unresolvedVariables);
				resolvedPath = pathDirectoryResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteDirectoryPath pathDirectoryResolved;
				var b = TryResolve(values, out pathDirectoryResolved, out failureMessage);
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
