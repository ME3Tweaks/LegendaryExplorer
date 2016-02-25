using System;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private static class FileNameHelpers
		{
			//
			//  FileName and extension
			//
			internal static string GetFileName(string path)
			{
				Debug.Assert(path != null);
				return MiscHelpers.GetLastName(path);
			}

			internal static string GetFileNameExtension(string path)
			{
				Debug.Assert(path != null);
				var fileName = MiscHelpers.GetLastName(path);
				var index = fileName.LastIndexOf('.');
				if (index == -1)
				{
					return String.Empty;
				}
				if (index == fileName.Length - 1)
				{
					return String.Empty;
				}
				return fileName.Substring(index, fileName.Length - index);
			}

			internal static string GetFileNameWithoutExtension(string path)
			{
				Debug.Assert(path != null);
				var fileName = GetFileName(path);
				var extension = GetFileNameExtension(path);
				if (extension == null || extension.Length == 0)
				{
					return fileName;
				}
				Debug.Assert(fileName.Length - extension.Length >= 0);
				return fileName.Substring(0, fileName.Length - extension.Length);
			}

			internal static bool HasExtension(string path, string extension)
			{
				Debug.Assert(path != null);

				// All these 3 assertions have been checked by contract!
				Debug.Assert(extension != null);
				Debug.Assert(extension.Length >= 2);
				Debug.Assert(extension[0] == '.');

				// Ignore case comparison
				var pathExtension = GetFileNameExtension(path);
				return (String.Compare(pathExtension, extension, true /*ignoreCase*/) == 0);
			}
		}
	}
}
