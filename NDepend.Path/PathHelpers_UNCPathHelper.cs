using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private static class UNCPathHelper
		{
			//
			// This two methods lets do work with UNCPath by converting them temporarily to drive letter path (easier to handle!)
			//
			internal const string FAKE_DRIVE_LETTER_PREFIX = @"C:";

			internal static bool IsAnAbsoluteUNCPath(string pathStringNormalized)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());
				int endIndexServerShareUnused;
				return IsAnAbsoluteUNCPath(pathStringNormalized, out endIndexServerShareUnused);
			}

			internal static bool IsAnAbsoluteUNCPath(string pathStringNormalized, out int endIndexServerShare)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());
				endIndexServerShare = -1;

				// Minimum URN pathString.Length is 5 ! "\\m\s"
				if (pathStringNormalized.Length < 5)
				{
					return false;
				}

				// Must begin with "\\"
				if (pathStringNormalized.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING) != 0)
				{
					return false;
				}

				// Must have a separator after the double separator with at least a char in the middle
				// means must have a separator after index 3
				var twoDirSeparatorLength = MiscHelpers.TWO_DIR_SEPARATOR_STRING.Length;
				var indexFirstSeparator = pathStringNormalized.IndexOf(MiscHelpers.DIR_SEPARATOR_STRING, twoDirSeparatorLength);
				if (indexFirstSeparator == -1)
				{
					return false;
				}
				Debug.Assert(indexFirstSeparator > twoDirSeparatorLength); // coz path has been normalized

				// Server cannot be "." nor ".."
				var server = pathStringNormalized.Substring(twoDirSeparatorLength, indexFirstSeparator - twoDirSeparatorLength);
				Debug.Assert(server.Length > 0);
				if (server == AbsoluteRelativePathHelpers.CURRENT_DIR_SINGLEDOT)
				{
					return false;
				}
				if (server == AbsoluteRelativePathHelpers.PARENT_DIR_DOUBLEDOT)
				{
					return false;
				}

				// Share cannot be "." nor ".."
				var indexSecondSeparator = pathStringNormalized.IndexOf(MiscHelpers.DIR_SEPARATOR_STRING, indexFirstSeparator + 1);
				if (indexSecondSeparator == -1)
				{
					indexSecondSeparator = pathStringNormalized.Length;
				}
				var indexStartShare = indexFirstSeparator + 1;
				Debug.Assert(indexSecondSeparator > indexStartShare); // Coz path has been normalized!
				var share = pathStringNormalized.Substring(indexStartShare, indexSecondSeparator - indexStartShare);
				Debug.Assert(share.Length > 0);
				if (share == AbsoluteRelativePathHelpers.CURRENT_DIR_SINGLEDOT)
				{
					return false;
				}
				if (share == AbsoluteRelativePathHelpers.PARENT_DIR_DOUBLEDOT)
				{
					return false;
				}

				endIndexServerShare = indexSecondSeparator;
				return true;
			}

			internal static bool StartLikeUNCPath(string pathStringNormalized)
			{
				Debug.Assert(pathStringNormalized != null);
				return pathStringNormalized.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING) == 0;
			}

			internal static string TranformDriveLetterIntoUNC(string driveLetterNormalizedPath, string uncServerShareStart)
			{
				Debug.Assert(driveLetterNormalizedPath != null);
				Debug.Assert(driveLetterNormalizedPath.IsNormalized());
				Debug.Assert(driveLetterNormalizedPath.StartsWith(FAKE_DRIVE_LETTER_PREFIX));
				Debug.Assert(uncServerShareStart != null);
				Debug.Assert(uncServerShareStart.Length > 0);
				var tmp = driveLetterNormalizedPath.Remove(0, FAKE_DRIVE_LETTER_PREFIX.Length);
				var uncNormalizedPath = tmp.Insert(0, uncServerShareStart);
				return uncNormalizedPath;
			}

			internal static string TranformUNCIntoDriveLetter(string uncNormalizedPath, out string uncServerShareStart)
			{
				Debug.Assert(uncNormalizedPath != null);
				Debug.Assert(uncNormalizedPath.IsNormalized());
				int endIndexServerShare;
				var b = IsAnAbsoluteUNCPath(uncNormalizedPath, out endIndexServerShare);
				Debug.Assert(b); // Must have already been checked!
				uncServerShareStart = uncNormalizedPath.Substring(0, endIndexServerShare);
				var tmp = uncNormalizedPath.Remove(0, endIndexServerShare);
				var driveLetterNormalizedPath = tmp.Insert(0, FAKE_DRIVE_LETTER_PREFIX);
				return driveLetterNormalizedPath;
			}
		}
	}
}
