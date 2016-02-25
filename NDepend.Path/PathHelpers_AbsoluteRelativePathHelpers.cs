using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private static class AbsoluteRelativePathHelpers
		{
			internal const string CURRENT_DIR_SINGLEDOT = ".";
			internal const string PARENT_DIR_DOUBLEDOT = "..";


			internal static bool ContainsInnerSpecialDir(string path)
			{
				// These cases should have been handled by the calling method and cannot be handled
				Debug.Assert(path != null);
				Debug.Assert(path.Length != 0);
				Debug.Assert(path == MiscHelpers.NormalizePath(path));

				// Analyze if a /./ or a /../ donc come after a valid DirectoryName
				var pathDirs = path.Split(MiscHelpers.DIR_SEPARATOR_CHAR);
				var bNextDoubleDotParentDirIsInnerSpecial = false;
				var bNextSingleDotCurrentDirIsInnerSpecial = false;

				var count = pathDirs.Length;
				for (var i = 0; i < count; i++)
				{
					var pathDir = pathDirs[i];
					if (pathDir == CURRENT_DIR_SINGLEDOT)
					{
						if (bNextSingleDotCurrentDirIsInnerSpecial)
						{
							return true;
						}
					}
					else if (pathDir == PARENT_DIR_DOUBLEDOT)
					{
						if (bNextDoubleDotParentDirIsInnerSpecial)
						{
							return true;
						}
					}
					else
					{
						bNextDoubleDotParentDirIsInnerSpecial = true;
					}
					bNextSingleDotCurrentDirIsInnerSpecial = true;
				}

				return false;
			}

			internal static bool IsAnAbsoluteDriveLetterPath(string pathStringNormalized)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());

				if (pathStringNormalized.Length == 1)
				{
					return false;
				}
				if (!Char.IsLetter(pathStringNormalized[0]))
				{
					return false;
				}
				if (pathStringNormalized[1] != ':')
				{
					return false;
				}
				if (pathStringNormalized.Length >= 3 && pathStringNormalized[2] != '\\')
				{
					return false;
				}
				return true;
			}


			internal static bool IsARelativePath(string pathStringNormalized)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());

				// First char must be a dot
				if (pathStringNormalized[0] != '.')
				{
					return false;
				}
				if (pathStringNormalized.Length == 1)
				{
					return true;
				}

				// Second char must be a dot or a separator!
				var secondChar = pathStringNormalized[1];
				if (secondChar == MiscHelpers.DIR_SEPARATOR_CHAR)
				{
					return true;
				}
				if (secondChar != '.')
				{
					return false;
				}
				if (pathStringNormalized.Length == 2)
				{
					return true;
				}

				// Third char must be a separator!
				var thirdChar = pathStringNormalized[2];
				return thirdChar == MiscHelpers.DIR_SEPARATOR_CHAR;
			}

			//------------------------------------------------
			//
			//  Inner Special dir handling
			//  What we call InnerSpecialDir is when at least one '.' or '..' directory is after a valid directory
			//  For example these paths all contains inner special dir
			//  C:\..  
			//  .\..\Dir2\.\Dir3
			//  .\..\..\Dir2\..\Dir3
			//
			//------------------------------------------------
			internal static string NormalizeAndResolveInnerSpecialDir(string pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				var pathStringNormalized = MiscHelpers.NormalizePath(pathString);
				if (!ContainsInnerSpecialDir(pathStringNormalized))
				{
					return pathStringNormalized;
				}
				string pathStringNormalizedResolved, failureReasonUnused;
				var b = TryResolveInnerSpecialDir(pathStringNormalized, out pathStringNormalizedResolved, out failureReasonUnused);
				Debug.Assert(b); // Coz already verified in a IsValidPath !
				return pathStringNormalizedResolved;
			}

			internal static bool TryGetAbsolutePathFrom(IAbsoluteDirectoryPath pathFrom, IPath pathTo, out string pathResult, out string failureReason)
			{
				Debug.Assert(pathFrom != null);
				Debug.Assert(pathTo != null);
				Debug.Assert(pathTo.IsRelativePath);

				if (pathFrom.Kind == AbsolutePathKind.DriveLetter)
				{
					// Only work with Directory 
					if (pathTo.IsFilePath)
					{
						pathTo = pathTo.ParentDirectoryPath;
					}
					return TryGetAbsolutePath(pathFrom.ToString(), pathTo.ToString(), out pathResult, out failureReason);
				}


				//
				// Special case when a relative path is asked from a UNC path like ".." from "\\Server\Share".
				// In such case we cannot return "\\Server" that is not a valis UNC path
				// To address this we create a temporary drive letter absolute path and do the TryGetAbsolutePathFrom() on it!
				//
				Debug.Assert(pathFrom.Kind == AbsolutePathKind.UNC);
				var pathFromString = pathFrom.ToString();
				string uncServerShareStart;
				var fakePathFromString = UNCPathHelper.TranformUNCIntoDriveLetter(pathFromString, out uncServerShareStart);
				Debug.Assert(fakePathFromString.IsValidAbsoluteDirectoryPath());
				var fakePathFrom = fakePathFromString.ToAbsoluteDirectoryPath();

				// Call me, but this time with a DriveLetter path (no risk of infinite recursion!)
				string pathResultDriveLetter;
				if (!TryGetAbsolutePathFrom(fakePathFrom, pathTo, out pathResultDriveLetter, out failureReason))
				{
					failureReason = failureReason.Replace(UNCPathHelper.FAKE_DRIVE_LETTER_PREFIX, uncServerShareStart);
					pathResult = null;
					return false;
				}

				Debug.Assert(pathResultDriveLetter != null);
				Debug.Assert(pathResultDriveLetter.Length > 0);
				Debug.Assert(pathResultDriveLetter.StartsWith(UNCPathHelper.FAKE_DRIVE_LETTER_PREFIX));
				pathResult = UNCPathHelper.TranformDriveLetterIntoUNC(pathResultDriveLetter, uncServerShareStart);

				return true;
			}

			//
			//  Relative/absolute computation
			//

			internal static bool TryGetRelativePath(IAbsoluteDirectoryPath pathFrom, IAbsolutePath pathTo, out string pathResult, out string failurereason)
			{
				Debug.Assert(pathFrom != null);
				Debug.Assert(pathTo != null);

				if (!pathFrom.OnSameVolumeThan(pathTo))
				{
					failurereason = @"Cannot compute relative path from 2 paths that are not on the same volume 
   PathFrom = """ + pathFrom + @"""
   PathTo   = """ + pathTo + @"""";
					pathResult = null;
					return false;
				}

				// Only work with Directory 
				if (pathTo.IsFilePath)
				{
					pathTo = pathTo.ParentDirectoryPath;
				}
				pathResult = GetPathRelativeTo(pathFrom.ToString(), pathTo.ToString());
				failurereason = null;
				return true;
			}


			internal static bool TryResolveInnerSpecialDir(string pathStringNormalized, out string pathResolved, out string failureReason)
			{
				// These cases should have been handled by the calling method and cannot be handled
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());

				//
				// TryResolveInnerSpecialDirAlgo() is never called without calling first ContainsInnerSpecialDir()
				//
				Debug.Assert(ContainsInnerSpecialDir(pathStringNormalized));

				//
				// The algo behaves differently for a relative path since "..\" can be accumulated at the beginning!!
				//
				var bPathIsRelative = IsARelativePath(pathStringNormalized);

				//
				// Special case of UNC path:  Replace "\\server\share" with "C:" and will do the opposite replacement at the end!
				//
				var originalPathStringNormalized = pathStringNormalized;
				var bIsUNCPath = UNCPathHelper.StartLikeUNCPath(pathStringNormalized);
				string uncServerShareStart = null;
				if (bIsUNCPath)
				{
					// We can assert that coz already checked in IsValidAbsoluteDirectory()
					Debug.Assert(UNCPathHelper.IsAnAbsoluteUNCPath(pathStringNormalized));
					pathStringNormalized = UNCPathHelper.TranformUNCIntoDriveLetter(pathStringNormalized, out uncServerShareStart);
				}


				//
				// Prepare the algo variables...
				//
				var pathDirs = pathStringNormalized.Split(MiscHelpers.DIR_SEPARATOR_CHAR);
				Debug.Assert(pathDirs.Length > 0);

				var rootDir = pathDirs[0];
				if (bIsUNCPath)
				{
					rootDir = uncServerShareStart;
				}


				//
				// ... and call the algo!
				//
				var result = TryResolveInnerSpecialDirAlgo(
					pathDirs,
					bPathIsRelative,
					out pathResolved);
				switch (result)
				{
					default:
						Debug.Assert(result == TryResolveInnerSpecialDirResult.Success);
						break;
					case TryResolveInnerSpecialDirResult.ErrorParentOfRootDirResolved:
						failureReason = @"The pathString {" + originalPathStringNormalized + @"} references the parent dir \..\ of the root dir {" + rootDir
										+ "}, it cannot be resolved.";
						return false;
				}

				//
				// Reverse UNC start transformation!
				//
				Debug.Assert(pathResolved != null);
				Debug.Assert(pathResolved.IsNormalized());
				if (bIsUNCPath)
				{
					pathResolved = UNCPathHelper.TranformDriveLetterIntoUNC(pathResolved, uncServerShareStart);
				}
				failureReason = null;
				return true;
			}

			//--------------------------------------
			//
			//  GetPathRelativeTo()  /  TryGetAbsolutePath()
			//
			//--------------------------------------
			private static string GetPathRelativeTo(string pathFrom, string pathTo)
			{
				Debug.Assert(pathFrom != null);
				Debug.Assert(IsAnAbsolutePath(pathFrom));
				Debug.Assert(pathTo != null);
				Debug.Assert(IsAnAbsolutePath(pathTo));

				// Don't return .\ but just . to remain compliant
				if (String.Compare(pathFrom, pathTo, true) == 0)
				{
					return CURRENT_DIR_SINGLEDOT;
				}

				var relativeDirs = new List<string>();
				var pathFromDirs = pathFrom.Split(MiscHelpers.DIR_SEPARATOR_CHAR);
				var pathToDirs = pathTo.Split(MiscHelpers.DIR_SEPARATOR_CHAR);
				var length = Math.Min(pathFromDirs.Length, pathToDirs.Length);
				var lastCommonRoot = -1;

				// find common root
				for (var i = 0; i < length; i++)
				{
					if (String.Compare(pathFromDirs[i], pathToDirs[i], true) != 0)
					{
						break;
					}
					lastCommonRoot = i;
				}

				// The lastCommon root problem is handled by the calling method and cannot be tested
				Debug.Assert(lastCommonRoot != -1);

				// add relative folders in from pathStringNormalized
				for (var i = lastCommonRoot + 1; i < pathFromDirs.Length; i++)
				{
					if (pathFromDirs[i].Length > 0)
					{
						relativeDirs.Add(PARENT_DIR_DOUBLEDOT);
					}
				}
				if (relativeDirs.Count == 0)
				{
					relativeDirs.Add(CURRENT_DIR_SINGLEDOT);
				}

				// add to folders to pathStringNormalized
				for (var i = lastCommonRoot + 1; i < pathToDirs.Length; i++)
				{
					relativeDirs.Add(pathToDirs[i]);
				}

				// create relative pathStringNormalized
				var relativeParts = new string[relativeDirs.Count];
				relativeDirs.CopyTo(relativeParts);
				var RelativePath = String.Join(MiscHelpers.DIR_SEPARATOR_STRING, relativeParts);
				return RelativePath;
			}

			//-----------------------------------------------------
			//
			//  Is an Absolute/Relative path
			//
			//-----------------------------------------------------
			private static bool IsAnAbsolutePath(string pathStringNormalized)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.IsNormalized());
				return IsAnAbsoluteDriveLetterPath(pathStringNormalized) || UNCPathHelper.IsAnAbsoluteUNCPath(pathStringNormalized);
			}


			//
			//  TryGetAbsolutePath
			//
			private static bool TryGetAbsolutePath(string pathFrom, string pathTo, out string pathResult, out string failureReason)
			{
				Debug.Assert(pathFrom != null);
				Debug.Assert(pathFrom.IsNormalized());
				Debug.Assert(IsAnAbsolutePath(pathFrom));
				Debug.Assert(pathTo != null);
				Debug.Assert(pathTo.IsNormalized());
				Debug.Assert(IsARelativePath(pathTo));

				var pathFromDirs = pathFrom.Split(MiscHelpers.DIR_SEPARATOR_CHAR);
				var pathToDirs = pathTo.Split(MiscHelpers.DIR_SEPARATOR_CHAR);

				// Compute nbParentDirToGoBackInPathFrom
				var nbParentDirToGoBackInPathFrom = 0;
				var nbSpecialDirToGoUpInPathTo = 0;
				for (var i = 0; i < pathToDirs.Length; i++)
				{
					if (pathToDirs[i] == PARENT_DIR_DOUBLEDOT)
					{
						nbParentDirToGoBackInPathFrom++;
						nbSpecialDirToGoUpInPathTo++;
					}
					else if (pathToDirs[i] == CURRENT_DIR_SINGLEDOT)
					{
						nbSpecialDirToGoUpInPathTo++;
					}
					else
					{
						break;
					}
				}

				// check nbParentDirToGoBackInPathFrom is valid
				if (nbParentDirToGoBackInPathFrom >= pathFromDirs.Length)
				{
					failureReason = @"Cannot resolve pathTo.TryGetAbsolutePath(pathFrom) because there are too many parent dirs in pathTo:
   PathFrom = """ + pathFrom + @"""
   PathTo   = """ + pathTo + @"""";
					pathResult = null;
					return false;
				}

				// Apply nbParentDirToGoBackInPathFrom to extract part from pathFrom
				var dirsExtractedFromPathFrom = new string[(pathFromDirs.Length - nbParentDirToGoBackInPathFrom)];
				for (var i = 0; i < pathFromDirs.Length - nbParentDirToGoBackInPathFrom; i++)
				{
					dirsExtractedFromPathFrom[i] = pathFromDirs[i];
				}
				var partExtractedFromPathFrom = String.Join(MiscHelpers.DIR_SEPARATOR_STRING, dirsExtractedFromPathFrom);

				// Apply nbParentDirToGoBackInPathFrom to extract part from pathTo
				var dirsExtractedFromPathTo = new string[(pathToDirs.Length - nbSpecialDirToGoUpInPathTo)];
				for (var i = 0; i < pathToDirs.Length - nbSpecialDirToGoUpInPathTo; i++)
				{
					dirsExtractedFromPathTo[i] = pathToDirs[i + nbSpecialDirToGoUpInPathTo];
				}
				var partExtractedFromPathTo = String.Join(MiscHelpers.DIR_SEPARATOR_STRING, dirsExtractedFromPathTo);

				// Concatenate the 2 parts extracted from pathFrom and pathTo
				pathResult = partExtractedFromPathFrom + MiscHelpers.DIR_SEPARATOR_STRING + partExtractedFromPathTo;
				failureReason = null;
				return true;
			}

			private static TryResolveInnerSpecialDirResult TryResolveInnerSpecialDirAlgo(
				string[] pathDirs,
				bool bPathIsRelative,
				out string pathResolved)
			{
				Debug.Assert(pathDirs != null);
				var nbPathDirs = pathDirs.Length;
				Debug.Assert(nbPathDirs > 0);

				var dirStack = new Stack<string>();
				var bANormalDirHasAlreadyBeenFound = false;

				//
				// Complex algorithm to resolve inner special dir!!!
				//
				for (var i = 0; i < nbPathDirs; i++)
				{
					var dir = pathDirs[i];
					Debug.Assert(dir != null);
					Debug.Assert(dir.Length > 0);
					switch (dir)
					{
						case CURRENT_DIR_SINGLEDOT:
							if (i > 0)
							{
								// Just ignore InnerSpecial SingleDot, except the first one!
								continue;
							}
							Debug.Assert(bPathIsRelative); // "." is pushed only in bPathIsRelative case!
							dirStack.Push(dir);
							break;
						case PARENT_DIR_DOUBLEDOT:
							if (!bANormalDirHasAlreadyBeenFound)
							{
								Debug.Assert(bPathIsRelative); // ".." is pushed only in bPathIsRelative case!
								dirStack.Push(dir);
								continue;
							}

							// We can assert this coz bNextDoubleDotParentDirIsInnerSpecial is true + next test!
							Debug.Assert(dirStack.Count > 0);

							if (!bPathIsRelative && dirStack.Count == 1)
							{
								// Here we reached a problem coz we are trying to get parent dir, of first dir (that can be "C:\"  "%EnvVar%"   "$(SolutionDir)"  )
								pathResolved = null;
								return TryResolveInnerSpecialDirResult.ErrorParentOfRootDirResolved;
							}
							var dirToRemove = dirStack.Peek();

							switch (dirToRemove)
							{
								case CURRENT_DIR_SINGLEDOT:
									Debug.Assert(bPathIsRelative); // "." is pushed only in bPathIsRelative case...
									Debug.Assert(dirStack.Count == 1); // ... when it is at the beginning of the path!
									dirStack.Pop();
									dirStack.Push(PARENT_DIR_DOUBLEDOT);
									continue;
								case PARENT_DIR_DOUBLEDOT:
									Debug.Assert(bPathIsRelative); // ".." is pushed only in bPathIsRelative case!

									// No prob here, we just push the "..\" since the result can be a relative path!
									dirStack.Push(PARENT_DIR_DOUBLEDOT);
									continue;
								default:

									// dirToRemove is a normal named dir, it is not ".\" nor "..\"
									// just pop it!
									dirStack.Pop();
									continue;
							}

						default:

							// dir is a normal named dir, it is not ".\" nor "..\"
							dirStack.Push(dir);
							bANormalDirHasAlreadyBeenFound = true;
							continue;
					}
				}

				//
				// Concatenate the dirs
				//
				var stringBuilder = new StringBuilder();

				// Notice that the dirs are reverse ordered, that's why we use Insert(0,
				foreach (var dir in dirStack)
				{
					stringBuilder.Insert(0, MiscHelpers.DIR_SEPARATOR_STRING);
					stringBuilder.Insert(0, dir);
				}

				// Remove the last DIR_SEPARATOR
				stringBuilder.Length = stringBuilder.Length - 1;
				pathResolved = stringBuilder.ToString();
				Debug.Assert(pathResolved.IsNormalized());
				return TryResolveInnerSpecialDirResult.Success;
			}


			private enum TryResolveInnerSpecialDirResult
			{
				Success,
				ErrorParentOfRootDirResolved
			}
		}
	}
}
