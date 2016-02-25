using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class PathBrowsingHelpers
		{
			internal static string GetChildDirectoryWithName(IDirectoryPath directoryPath, string directoryName)
			{
				Debug.Assert(directoryPath != null);
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				return directoryPath.ToString() + MiscHelpers.DIR_SEPARATOR_CHAR + directoryName;
			}

			internal static string GetChildFileWithName(IDirectoryPath directoryPath, string fileName)
			{
				Debug.Assert(directoryPath != null);
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				return directoryPath.ToString() + MiscHelpers.DIR_SEPARATOR_CHAR + fileName;
			}

			internal static IDirectoryPath GetSisterDirectoryWithName(IPath path, string directoryName)
			{
				Debug.Assert(path != null);
				Debug.Assert(directoryName != null); // Enforced by contract
				Debug.Assert(directoryName.Length > 0); // Enforced by contract
				return path.ParentDirectoryPath.GetChildDirectoryPath(directoryName);
			}

			internal static IFilePath GetSisterFileWithName(IPath path, string fileName)
			{
				Debug.Assert(path != null);
				Debug.Assert(fileName != null); // Enforced by contract
				Debug.Assert(fileName.Length > 0); // Enforced by contract
				return path.ParentDirectoryPath.GetChildFilePath(fileName);
			}

			internal static string UpdateExtension(IFilePath filePath, string newExtension)
			{
				Debug.Assert(filePath != null);

				// All these 3 assertions have been checked by contract!
				Debug.Assert(newExtension != null);
				Debug.Assert(newExtension.Length >= 2);
				Debug.Assert(newExtension[0] == '.');

				var fileNameBefore = filePath.FileName;
				var fileNameAfter = filePath.FileNameWithoutExtension + newExtension;
				var filePathString = filePath.ToString();
				Debug.Assert(filePathString.Length > fileNameBefore.Length);
				var filePathStringWithoutFileName = filePathString.Substring(0, filePathString.Length - fileNameBefore.Length);
				var filePathStringWithFileNameAfter = filePathStringWithoutFileName + fileNameAfter;
				return filePathStringWithFileNameAfter;
			}
		}
	}
}
