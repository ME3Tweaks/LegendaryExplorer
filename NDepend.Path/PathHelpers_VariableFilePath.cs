using System.Collections.Generic;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private sealed class VariableFilePath : VariablePathBase, IVariableFilePath
		{
			internal VariableFilePath(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(pathString.IsValidVariableFilePath());
			}

			public string FileExtension => FileNameHelpers.GetFileNameExtension(m_PathString);


			//
			//  File Name and File Name Extension
			//
			public string FileName => FileNameHelpers.GetFileName(m_PathString);

			public string FileNameWithoutExtension => FileNameHelpers.GetFileNameWithoutExtension(m_PathString);

			public override bool IsDirectoryPath => false;

			public override bool IsFilePath => true;

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
			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath)
			{
				Debug.Assert(values != null);
				IReadOnlyList<string> unresolvedVariablesUnused;
				return TryResolve(values, out resolvedFilePath, out unresolvedVariablesUnused);
			}

			public VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				Debug.Assert(values != null);
				string pathStringResolved;
				if (!base.TryResolve(values, out pathStringResolved, out unresolvedVariables))
				{
					resolvedFilePath = null;
					return VariablePathResolvingStatus.ErrorUnresolvedVariable;
				}
				if (!pathStringResolved.IsValidAbsoluteFilePath())
				{
					resolvedFilePath = null;
					return VariablePathResolvingStatus.ErrorVariableResolvedButCannotConvertToAbsolutePath;
				}
				resolvedFilePath = pathStringResolved.ToAbsoluteFilePath();
				return VariablePathResolvingStatus.Success;
			}

			public bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsoluteFilePath resolvedFilePath, out string failureMessage)
			{
				Debug.Assert(values != null);
				IReadOnlyList<string> unresolvedVariables;
				var status = TryResolve(values, out resolvedFilePath, out unresolvedVariables);
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
						failureMessage = GetVariableResolvedButCannotConvertToAbsolutePathFailureReason(values, "file");
						return false;
				}
			}


			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath)
			{
				IAbsoluteFilePath pathFileResolved;
				var resolvingStatus = TryResolve(values, out pathFileResolved);
				resolvedPath = pathFileResolved;
				return resolvingStatus;
			}

			public override VariablePathResolvingStatus TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath,
				out IReadOnlyList<string> unresolvedVariables)
			{
				IAbsoluteFilePath pathFileResolved;
				var resolvingStatus = TryResolve(values, out pathFileResolved, out unresolvedVariables);
				resolvedPath = pathFileResolved;
				return resolvingStatus;
			}

			public override bool TryResolve(IEnumerable<KeyValuePair<string, string>> values, out IAbsolutePath resolvedPath, out string failureMessage)
			{
				IAbsoluteFilePath pathFileResolved;
				var b = TryResolve(values, out pathFileResolved, out failureMessage);
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
