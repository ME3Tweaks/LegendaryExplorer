using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Contracts;

namespace NDepend.Path
{
	/// <summary>
	///     Contains extensions methods to obtain a path object from a string and to check that a string indeed represents a valid path.
	/// </summary>
	public static partial class PathHelpers
	{
		private const string FAILURE_PATHSTRING_IS_EMPTY = "The parameter pathString is empty.";
		private const string FAILURE_PATHSTRING_IS_NULL = "The parameter pathString is null.";
		private const string FAILURE_PATHSTRING_NORMALIZED_IS_EMPTY = "The parameter pathString normalized is empty.";

		private const string INVALID_FILE_PATH_STRING_COZ_NO_FILE_NAME =
			"The parameter pathString is not a file path because it doesn't have a valid file name.";

		//-------------------------------------------------------------------------------------------
		//
		// IsValidFile path check is made on a pattern
		// First pathString must be a valid directory path, and then it must have a parent dir
		//
		//-------------------------------------------------------------------------------------------
		private const string INVALID_FILE_PATH_STRING_COZ_NO_PARENT_DIR =
			"The parameter pathString is not a file path because it doesn't have at least one parent directory.";


		private const string PATH_STRING = "pathString";
		private static readonly char[] s_ForbiddenCharInPath = { '*', '|', '?', '<', '>', '"' };


		/// <summary>
		///     An array of char forbidden in string representing path.
		/// </summary>
		/// <remarks>
		///     Use this string.IndexOfAny(char[]) method to detect the presence of any of this char in a string.
		/// </remarks>
		public static char[] ForbiddenCharInPath
		{
			get
			{
				Contract.Ensures(Contract.Result<char[]>() != null, "returned array is not null");
				return s_ForbiddenCharInPath;
			}
		}

		/// <summary>
		///     Path variables are formatted this way $(VariableName). Hence this getter returns the string "$(".
		/// </summary>
		public static string PathVariableBegin => @"$(";

		/// <summary>
		///     Path variables are formatted this way $(VariableName). Hence this getter returns the string ")".
		/// </summary>
		public static string PathVariableEnd => @")";

		/// <summary>
		///     Returns <i>true</i> if <paramref name="path" /> and <paramref name="pathOther" /> are both <i>null</i>, or if <paramref name="path" />.Equals(
		///     <paramref name="pathOther" />).
		/// </summary>
		/// <param name="path">The first path.</param>
		/// <param name="pathOther">The scond path.</param>
		public static bool EqualsNullSupported(this IPath path, IPath pathOther)
		{
			if (path == null)
			{
				return pathOther == null;
			}
			if (pathOther == null)
			{
				return false;
			}
			return path.Equals(pathOther);
		}


		/// <summary>
		///     Returns <i>true</i> if <paramref name="path" /> is not null, and <paramref name="path" />.<see cref="IAbsolutePath.Exists" /> equals <i>true</i>.
		/// </summary>
		/// <param name="path">The path reference.</param>
		public static bool IsNotNullAndExists(this IAbsolutePath path)
		{
			if (path == null)
			{
				return false;
			}
			return path.Exists;
		}

		//-------------------------------------------------------------------------------------------
		//
		// IsValidDirectory Absolute/Relative/EnvVar/Variable
		//
		//-------------------------------------------------------------------------------------------

		/// <summary>
		///     Determine whether this string is a valid absolute directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteDirectoryPath(this string pathString)
		{
			string reasonUnused;
			return IsValidAbsoluteDirectoryPath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid absolute directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteDirectoryPath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			if (MiscHelpers.IsURLPath(pathStringNormalized))
			{
				failureReason = @"URL paths are not accepted as absolute path.";
				return false;
			}

			if (!AbsoluteRelativePathHelpers.IsAnAbsoluteDriveLetterPath(pathStringNormalized))
			{
				if (!UNCPathHelper.IsAnAbsoluteUNCPath(pathStringNormalized))
				{
					failureReason =
						@"The parameter pathString is not an absolute directory path because it doesn't have a drive letter syntax (like ""C:\"") nor a URN path syntax (like ""\\server\share\"").";
					return false;
				}
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDir(pathStringNormalized))
			{
				string unusedPath;
				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDir(pathStringNormalized, out unusedPath, out failureReason))
				{
					return false;
				}
			}
			failureReason = null;
			return true;
		}

		/// <summary>
		///     Determine whether this string is a valid file absolute path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteFilePath(this string pathString)
		{
			string reasonUnused;
			return IsValidAbsoluteFilePath(pathString, out reasonUnused);
		}

		/// <summary>
		///     Determine whether this string is a valid file absolute path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToAbsoluteFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IAbsoluteFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidAbsoluteFilePath(this string pathString, out string failureReason)
		{
			if (!pathString.IsValidAbsoluteDirectoryPath(out failureReason))
			{
				return false;
			}

			var pathStringNormalized = MiscHelpers.NormalizePath(pathString);
			var bIsUNCPath = UNCPathHelper.StartLikeUNCPath(pathStringNormalized);
			if (bIsUNCPath)
			{
				// We can assret that coz we already validated that IsValidAbsoluteDirectoryPath
				Debug.Assert(UNCPathHelper.IsAnAbsoluteUNCPath(pathStringNormalized));
				string uncServerShareStartUnused;
				pathStringNormalized = UNCPathHelper.TranformUNCIntoDriveLetter(pathStringNormalized, out uncServerShareStartUnused);
			}

			string fileNameUnused;
			return IsThisValidDirectoryPathAValidFilePath(pathStringNormalized, out fileNameUnused, out failureReason);
		}


		/// <summary>
		///     Determine whether this string is a valid directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute directory path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidDirectoryPath(this string pathString)
		{
			string reasonUnused;
			return IsValidDirectoryPath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IDirectoryPath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute directory path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidDirectoryPath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			if (pathStringNormalized.IsValidRelativeDirectoryPath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidAbsoluteDirectoryPath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidEnvVarDirectoryPath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidVariableDirectoryPath())
			{
				return true;
			}

			failureReason = @"The string """ + pathString + @""" is not a valid directory path.";
			return false;
		}

		/// <summary>
		///     Determine whether this string is a valid directory path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvVarDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarDirectoryPath(this string pathString)
		{
			string reasonUnused;
			return IsValidEnvVarDirectoryPath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid directory path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvVarDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarDirectoryPath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			if (!MiscHelpers.IsAnEnvVarPath(pathStringNormalized))
			{
				failureReason = @"The parameter pathString is not prefixed with an environment variable (like ""%USERPROFILE%"")";
				return false;
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDir(pathStringNormalized))
			{
				string unusedPath;
				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDir(pathStringNormalized, out unusedPath, out failureReason))
				{
					return false;
				}
			}
			failureReason = null;
			return true;
		}


		/// <summary>
		///     Determine whether this string is a valid file path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvVarFilePath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarFilePath(this string pathString)
		{
			string reasonUnused;
			return IsValidEnvVarFilePath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid file path prefixed with an environment variable or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToEnvVarFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IEnvVarFilePath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path prefixed with an environment variable, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidEnvVarFilePath(this string pathString, out string failureReason)
		{
			if (!pathString.IsValidEnvVarDirectoryPath(out failureReason))
			{
				return false;
			}

			string fileNameUnused;
			return IsThisValidDirectoryPathAValidFilePath(pathString, out fileNameUnused, out failureReason);
		}

		//----------------------------------------
		//
		// IsValidFile / IsValidDirectory   don't ask for the kind (Absolute/Relative/EnvVar)
		//
		//----------------------------------------

		/// <summary>
		///     Determine whether this string is a valid file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidFilePath(this string pathString)
		{
			string reasonUnused;
			return IsValidFilePath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IFilePath" />.
		///     Notice that this method can return true even if the path represented by this string doesn't exist.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative or absolute file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidFilePath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			if (pathStringNormalized.IsValidRelativeFilePath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidAbsoluteFilePath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidEnvVarFilePath())
			{
				return true;
			}
			if (pathStringNormalized.IsValidVariableFilePath())
			{
				return true;
			}

			failureReason = @"The string """ + pathString + @""" is not a valid file path.";
			return false;
		}


		/// <summary>
		///     Returns <i>true</i> if <paramref name="pathVariableName" /> contains only upper/lower case letters, digits and underscore and has less than 1024
		///     characters. In such case <paramref name="pathVariableName" /> is a valid path variable name.
		/// </summary>
		/// <param name="pathVariableName">The string on which we test if it is a valid path variable name.</param>
		[Pure]
		public static bool IsValidPathVariableName(this string pathVariableName)
		{
			Contract.Requires(pathVariableName != null, "variable string cannot be null");
			Contract.Requires(pathVariableName.Length > 0, "variable string cannot be empty");
			const int MAX_CHARS = 1024;
			var length = pathVariableName.Length;
			if (length > MAX_CHARS)
			{
				return false;
			}
			for (var i = 0; i < length; i++)
			{
				var c = pathVariableName[i];
				if (MiscHelpers.IsCharLetterOrDigitOrUnderscore(c))
				{
					continue;
				}
				return false;
			}
			return true;
		}

		/// <summary>
		///     Returns <i>true</i> if <paramref name="pathVariableValue" /> has less than 1024 characters and has no character in
		///     <see cref="ForbiddenCharInPath" />. In such case <paramref name="pathVariableValue" /> is a valid path variable name.
		/// </summary>
		/// <param name="pathVariableValue">The string on which we test if it is a valid path variable value.</param>
		[Pure]
		public static bool IsValidPathVariableValue(this string pathVariableValue)
		{
			Contract.Requires(pathVariableValue != null, "variable string cannot be null");
			const int MAX_CHARS = 1024;
			var length = pathVariableValue.Length;
			if (length > MAX_CHARS)
			{
				return false;
			}

			if (pathVariableValue.IndexOfAny(s_ForbiddenCharInPath) >= 0)
			{
				return false;
			}

			return true;
		}


		/// <summary>
		///     Determine whether this string is a valid relative directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeDirectoryPath(this string pathString)
		{
			string reasonUnused;
			return IsValidRelativeDirectoryPath(pathString, out reasonUnused);
		}

		/// <summary>
		///     Determine whether this string is a valid relative directory path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeDirectoryPath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			if (!AbsoluteRelativePathHelpers.IsARelativePath(pathStringNormalized))
			{
				failureReason = "The parameter pathString is not a valid relative path.";
				return false;
			}

#if DEBUG // TryResolveInnerSpecialDir() cannot returns false for a relative path, 
			// just assert this in DEBUG mode!
			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDir(pathStringNormalized))
			{
				string unusedPath, failureReasonUnused;
				Debug.Assert(AbsoluteRelativePathHelpers.TryResolveInnerSpecialDir(pathStringNormalized, out unusedPath, out failureReasonUnused));
			}
#endif

			failureReason = null;
			return true;
		}


		/// <summary>
		///     Determine whether this string is a valid relative file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToRelativeFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeFilePath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeFilePath(this string pathString)
		{
			string reasonUnused;
			return pathString.IsValidRelativeFilePath(out reasonUnused);
		}

		/// <summary>
		///     Determine whether this string is a valid relative file path or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true <see cref="ToRelativeFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IRelativeFilePath" />.
		/// </remarks>
		/// <param name="pathString">this string</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid relative file path, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidRelativeFilePath(this string pathString, out string failureReason)
		{
			if (!pathString.IsValidRelativeDirectoryPath(out failureReason))
			{
				return false;
			}
			string fileNameUnused;
			return IsThisValidDirectoryPathAValidFilePath(pathString, out fileNameUnused, out failureReason);
		}


		/// <summary>
		///     Determine whether this string is a valid directory path that contains variables.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableDirectoryPath(this string pathString)
		{
			string reasonUnused;
			return IsValidVariableDirectoryPath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid directory path that contains variables.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableDirectoryPath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableDirectoryPath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableDirectoryPath(this string pathString, out string failureReason)
		{
			string pathStringNormalized;
			if (!pathString.TryGetNotNullNormalizedPath(out pathStringNormalized, out failureReason))
			{
				return false;
			}

			IReadOnlyList<string> variablesUnused;
			if (!VariablePathHelpers.IsAVariablePath(pathStringNormalized, out variablesUnused, out failureReason))
			{
				return false;
			}

			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDir(pathStringNormalized))
			{
				string unusedPath;
				if (!AbsoluteRelativePathHelpers.TryResolveInnerSpecialDir(pathStringNormalized, out unusedPath, out failureReason))
				{
					return false;
				}
			}
			failureReason = null;
			return true;
		}


		/// <summary>
		///     Determine whether this string is a valid file that contains variables, or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableFilePath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableFilePath(this string pathString)
		{
			string reasonUnused;
			return IsValidVariableFilePath(pathString, out reasonUnused);
		}


		/// <summary>
		///     Determine whether this string is a valid file that contains variables, or not.
		/// </summary>
		/// <remarks>
		///     If this method returns true, the extension method <see cref="ToVariableFilePath(string)" /> can be safely invoked on this string to obtain a
		///     <see cref="IVariableFilePath" />.
		/// </remarks>
		/// <param name="pathString">This string from which is determined the path validity.</param>
		/// <param name="failureReason">If false is returned, failureReason contains the plain english description of the failure.</param>
		/// <returns>
		///     <i>true</i> if this string represents a valid file path that contains variables, otherwise <i>false</i>.
		/// </returns>
		public static bool IsValidVariableFilePath(this string pathString, out string failureReason)
		{
			if (!pathString.IsValidVariableDirectoryPath(out failureReason))
			{
				return false;
			}

			string fileName;
			if (!IsThisValidDirectoryPathAValidFilePath(pathString, out fileName, out failureReason))
			{
				return false;
			}

			if (VariablePathHelpers.DoesFileNameContainVariable(fileName))
			{
				failureReason = INVALID_FILE_PATH_STRING_COZ_NO_PARENT_DIR;
				return false;
			}
			return true;
		}


		/// <summary>
		///     Returns a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidAbsoluteDirectoryPath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid absolute directory path.</exception>
		public static IAbsoluteDirectoryPath ToAbsoluteDirectoryPath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IAbsoluteDirectoryPath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IAbsoluteDirectoryPath absoluteDirectoryPath;
			if (!pathString.TryGetAbsoluteDirectoryPath(out absoluteDirectoryPath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(absoluteDirectoryPath != null);
			return absoluteDirectoryPath;
		}

		//---------------------------------------------------
		//
		// string to IPath extension methods
		//
		//---------------------------------------------------

		/// <summary>
		///     Returns a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidAbsoluteFilePath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid absolute file path.</exception>
		public static IAbsoluteFilePath ToAbsoluteFilePath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IAbsoluteFilePath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IAbsoluteFilePath absoluteFilePath;
			if (!pathString.TryGetAbsoluteFilePath(out absoluteFilePath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(absoluteFilePath != null);
			return absoluteFilePath;
		}


		/// <summary>
		///     Returns a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidDirectoryPath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="pathString" /> is empty or doesn't represents a valid relative or absolute directory path or a
		///     valid directory path prefixed with an environment variable.
		/// </exception>
		public static IDirectoryPath ToDirectoryPath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IDirectoryPath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IDirectoryPath directoryPath;
			if (!pathString.TryGetDirectoryPath(out directoryPath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(directoryPath != null);
			return directoryPath;
		}


		/// <summary>
		///     Returns a new <see cref="IEnvVarDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidEnvVarDirectoryPath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="pathString" /> is empty or doesn't represents a valid directory path prefixed with an environment
		///     variable.
		/// </exception>
		public static IEnvVarDirectoryPath ToEnvVarDirectoryPath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IEnvVarDirectoryPath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IEnvVarDirectoryPath envVarDirectoryPath;
			if (!pathString.TryGetEnvVarDirectoryPath(out envVarDirectoryPath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(envVarDirectoryPath != null);
			return envVarDirectoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IEnvVarFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidEnvVarFilePath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="pathString" /> is empty or doesn't represents a valid file path prefixed with an environment
		///     variable.
		/// </exception>
		public static IEnvVarFilePath ToEnvVarFilePath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IEnvVarFilePath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IEnvVarFilePath envVarFilePath;
			if (!pathString.TryGetEnvVarFilePath(out envVarFilePath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(envVarFilePath != null);
			return envVarFilePath;
		}

		/// <summary>
		///     Returns a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidFilePath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this method, and
		///     avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException">
		///     <paramref name="pathString" /> is empty or doesn't represents a valid relative or absolute file path or a valid
		///     file path prefixed with an environment variable.
		/// </exception>
		public static IFilePath ToFilePath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IFilePath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IFilePath filePath;
			if (!pathString.TryGetFilePath(out filePath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(filePath != null);
			return filePath;
		}


		/// <summary>
		///     Returns a new <see cref="IRelativeDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidRelativeDirectoryPath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid relative directory path.</exception>
		public static IRelativeDirectoryPath ToRelativeDirectoryPath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IRelativeDirectoryPath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IRelativeDirectoryPath relativeDirectoryPath;
			if (!pathString.TryGetRelativeDirectoryPath(out relativeDirectoryPath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(relativeDirectoryPath != null);
			return relativeDirectoryPath;
		}


		/// <summary>
		///     Returns a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidRelativeFilePath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid relative file path.</exception>
		public static IRelativeFilePath ToRelativeFilePath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IRelativeFilePath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IRelativeFilePath relativeFilePath;
			if (!pathString.TryGetRelativeFilePath(out relativeFilePath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(relativeFilePath != null);
			return relativeFilePath;
		}


		/// <summary>
		///     Returns <paramref name="path" />.ToString() is path is null, else returns the empty string.
		/// </summary>
		/// <param name="path">The path reference.</param>
		public static string ToStringOrIfNullToEmptyString(this IPath path)
		{
			if (path == null)
			{
				return "";
			}
			return path.ToString();
		}


		/// <summary>
		///     Returns a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidVariableDirectoryPath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling
		///     this method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid directory path that contains variables.</exception>
		public static IVariableDirectoryPath ToVariableDirectoryPath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IVariableDirectoryPath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IVariableDirectoryPath variableDirectoryPath;
			if (!pathString.TryGetVariableDirectoryPath(out variableDirectoryPath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(variableDirectoryPath != null);
			return variableDirectoryPath;
		}

		/// <summary>
		///     Returns a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The method <see cref="IsValidVariableFilePath(string)" /> can be called to enfore <paramref name="pathString" /> validity before calling this
		///     method, and avoid any exception.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <exception cref="ArgumentNullException"><paramref name="pathString" /> is null.</exception>
		/// <exception cref="ArgumentException"><paramref name="pathString" /> is empty or doesn't represents a valid file path that contains variables.</exception>
		public static IVariableFilePath ToVariableFilePath(this string pathString)
		{
			Contract.Ensures(Contract.Result<IVariableFilePath>() != null, "returned reference is not null");
			pathString.EventuallyThrowExOnPathStringNullOrEmpty();
			string failureReason;
			IVariableFilePath variableFilePath;
			if (!pathString.TryGetVariableFilePath(out variableFilePath, out failureReason))
			{
				throw new ArgumentException(failureReason, PATH_STRING);
			}
			Debug.Assert(variableFilePath != null);
			return variableFilePath;
		}


		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid absolute directory path and as a consequence, the returned
		///     <paramref name="absoluteDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="absoluteDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetAbsoluteDirectoryPath(this string pathString, out IAbsoluteDirectoryPath absoluteDirectoryPath, out string failureReason)
		{
			absoluteDirectoryPath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidAbsoluteDirectoryPath(out failureReason))
			{
				return false;
			}
			absoluteDirectoryPath = new AbsoluteDirectoryPath(pathString);
			return true;
		}


		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid absolute directory path and as a consequence, the returned
		///     <paramref name="absoluteDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="absoluteDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetAbsoluteDirectoryPath(this string pathString, out IAbsoluteDirectoryPath absoluteDirectoryPath)
		{
			string failureReasonUnused;
			return pathString.TryGetAbsoluteDirectoryPath(out absoluteDirectoryPath, out failureReasonUnused);
		}

		/// <summary>
		///     Try get a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid absolute file path and as a consequence, the returned
		///     <paramref name="absoluteFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path string.</param>
		/// <param name="absoluteFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetAbsoluteFilePath(this string pathString, out IAbsoluteFilePath absoluteFilePath, out string failureReason)
		{
			absoluteFilePath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidAbsoluteFilePath(out failureReason))
			{
				return false;
			}
			absoluteFilePath = new AbsoluteFilePath(pathString);
			return true;
		}

		//---------------------------------------------------
		//
		// string to IPath TryGet...Path extension methods, withOUT failureReason
		//
		//---------------------------------------------------

		/// <summary>
		///     Try get a new <see cref="IAbsoluteFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid absolute file path and as a consequence, the returned
		///     <paramref name="absoluteFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path string.</param>
		/// <param name="absoluteFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetAbsoluteFilePath(this string pathString, out IAbsoluteFilePath absoluteFilePath)
		{
			string failureReasonUnused;
			return pathString.TryGetAbsoluteFilePath(out absoluteFilePath, out failureReasonUnused);
		}


		/// <summary>
		///     Try get a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path and as a consequence, the returned <paramref name="directoryPath" />
		///     is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetDirectoryPath(this string pathString, out IDirectoryPath directoryPath, out string failureReason)
		{
			directoryPath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}

			if (pathString.IsValidAbsoluteDirectoryPath())
			{
				directoryPath = pathString.ToAbsoluteDirectoryPath();
				return true;
			}
			if (pathString.IsValidRelativeDirectoryPath())
			{
				directoryPath = pathString.ToRelativeDirectoryPath();
				return true;
			}
			if (pathString.IsValidEnvVarDirectoryPath())
			{
				directoryPath = pathString.ToEnvVarDirectoryPath();
				return true;
			}
			if (pathString.IsValidVariableDirectoryPath())
			{
				directoryPath = pathString.ToVariableDirectoryPath();
				return true;
			}


			var b = pathString.IsValidDirectoryPath(out failureReason);
			Debug.Assert(!b);
			failureReason = @"The parameter pathString is not a valid directory path.
" + failureReason;
			return false;
		}


		/// <summary>
		///     Try get a new <see cref="IDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path and as a consequence, the returned <paramref name="directoryPath" />
		///     is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="directoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetDirectoryPath(this string pathString, out IDirectoryPath directoryPath)
		{
			string failureReasonUnused;
			return pathString.TryGetDirectoryPath(out directoryPath, out failureReasonUnused);
		}


		/// <summary>
		///     Try get a new <see cref="IEnvVarDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path prefixed with an environment variable and as a consequence, the
		///     returned <paramref name="envVarDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="envVarDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetEnvVarDirectoryPath(this string pathString, out IEnvVarDirectoryPath envVarDirectoryPath, out string failureReason)
		{
			envVarDirectoryPath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidEnvVarDirectoryPath(out failureReason))
			{
				return false;
			}
			envVarDirectoryPath = new EnvVarDirectoryPath(pathString);
			return true;
		}


		/// <summary>
		///     Try get a new <see cref="IEnvVarDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path prefixed with an environment variable and as a consequence, the
		///     returned <paramref name="envVarDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="envVarDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetEnvVarDirectoryPath(this string pathString, out IEnvVarDirectoryPath envVarDirectoryPath)
		{
			string failureReasonUnused;
			return pathString.TryGetEnvVarDirectoryPath(out envVarDirectoryPath, out failureReasonUnused);
		}

		/// <summary>
		///     Try get a new <see cref="IEnvVarFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path prefixed with an environment variable and as a consequence, the returned
		///     <paramref name="envVarFilePath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="envVarFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetEnvVarFilePath(this string pathString, out IEnvVarFilePath envVarFilePath, out string failureReason)
		{
			envVarFilePath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidEnvVarFilePath(out failureReason))
			{
				return false;
			}
			envVarFilePath = new EnvVarFilePath(pathString);
			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IEnvVarFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path prefixed with an environment variable and as a consequence, the returned
		///     <paramref name="envVarFilePath" /> is not null.
		/// </returns>
		/// <remarks>
		///     The path represented by this string doesn't need to exist for this operation to complete properly.
		///     The environment variable prefixing the path doesn't need to exist for this operation to complete properly.
		/// </remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="envVarFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetEnvVarFilePath(this string pathString, out IEnvVarFilePath envVarFilePath)
		{
			string failureReasonUnused;
			return pathString.TryGetEnvVarFilePath(out envVarFilePath, out failureReasonUnused);
		}

		/// <summary>
		///     Try get a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path and as a consequence, the returned <paramref name="filePath" /> is not
		///     null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetFilePath(this string pathString, out IFilePath filePath, out string failureReason)
		{
			filePath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}

			if (pathString.IsValidAbsoluteFilePath())
			{
				filePath = pathString.ToAbsoluteFilePath();
				return true;
			}
			if (pathString.IsValidRelativeFilePath())
			{
				filePath = pathString.ToRelativeFilePath();
				return true;
			}
			if (pathString.IsValidEnvVarFilePath())
			{
				filePath = pathString.ToEnvVarFilePath();
				return true;
			}
			if (pathString.IsValidVariableFilePath())
			{
				filePath = pathString.ToVariableFilePath();
				return true;
			}

			var b = pathString.IsValidFilePath(out failureReason);
			Debug.Assert(!b);
			failureReason = @"The parameter pathString is not a valid file path.
" + failureReason;
			return false;
		}

		/// <summary>
		///     Try get a new <see cref="IFilePath" /> object object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path and as a consequence, the returned <paramref name="filePath" /> is not
		///     null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="filePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetFilePath(this string pathString, out IFilePath filePath)
		{
			string failureReasonUnused;
			return pathString.TryGetFilePath(out filePath, out failureReasonUnused);
		}


		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid relative directory path and as a consequence, the returned
		///     <paramref name="relativeDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="relativeDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetRelativeDirectoryPath(this string pathString, out IRelativeDirectoryPath relativeDirectoryPath, out string failureReason)
		{
			relativeDirectoryPath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidRelativeDirectoryPath(out failureReason))
			{
				return false;
			}
			relativeDirectoryPath = new RelativeDirectoryPath(pathString);
			return true;
		}


		/// <summary>
		///     Try get a new <see cref="IAbsoluteDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid relative directory path and as a consequence, the returned
		///     <paramref name="relativeDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="relativeDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetRelativeDirectoryPath(this string pathString, out IRelativeDirectoryPath relativeDirectoryPath)
		{
			string failureReasonUnused;
			return pathString.TryGetRelativeDirectoryPath(out relativeDirectoryPath, out failureReasonUnused);
		}

		/// <summary>
		///     Try get a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid relative file path and as a consequence, the returned
		///     <paramref name="relativeFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="relativeFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetRelativeFilePath(this string pathString, out IRelativeFilePath relativeFilePath, out string failureReason)
		{
			relativeFilePath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidRelativeFilePath(out failureReason))
			{
				return false;
			}
			relativeFilePath = new RelativeFilePath(pathString);
			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IRelativeFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid relative file path and as a consequence, the returned
		///     <paramref name="relativeFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="relativeFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetRelativeFilePath(this string pathString, out IRelativeFilePath relativeFilePath)
		{
			string failureReasonUnused;
			return pathString.TryGetRelativeFilePath(out relativeFilePath, out failureReasonUnused);
		}


		/// <summary>
		///     Try get a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path that contains variables and as a consequence, the returned
		///     <paramref name="variableDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="variableDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetVariableDirectoryPath(this string pathString, out IVariableDirectoryPath variableDirectoryPath, out string failureReason)
		{
			variableDirectoryPath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidVariableDirectoryPath(out failureReason))
			{
				return false;
			}
			variableDirectoryPath = new VariableDirectoryPath(pathString);
			return true;
		}


		/// <summary>
		///     Try get a new <see cref="IVariableDirectoryPath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid directory path that contains variables and as a consequence, the returned
		///     <paramref name="variableDirectoryPath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="variableDirectoryPath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetVariableDirectoryPath(this string pathString, out IVariableDirectoryPath variableDirectoryPath)
		{
			string failureReasonUnused;
			return pathString.TryGetVariableDirectoryPath(out variableDirectoryPath, out failureReasonUnused);
		}

		/// <summary>
		///     Try get a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path that contains variables and as a consequence, the returned
		///     <paramref name="variableFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="variableFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		/// <param name="failureReason">If this method returns <i>false</i>, this is the plain english description of the failure.</param>
		public static bool TryGetVariableFilePath(this string pathString, out IVariableFilePath variableFilePath, out string failureReason)
		{
			variableFilePath = null;
			if (pathString.IsPathStringNullOrEmpty(out failureReason))
			{
				return false;
			}
			if (!pathString.IsValidVariableFilePath(out failureReason))
			{
				return false;
			}
			variableFilePath = new VariableFilePath(pathString);
			return true;
		}

		/// <summary>
		///     Try get a new <see cref="IVariableFilePath" /> object from this string.
		/// </summary>
		/// <returns>
		///     <i>true</i> if <paramref name="pathString" /> is a valid file path that contains variables and as a consequence, the returned
		///     <paramref name="variableFilePath" /> is not null.
		/// </returns>
		/// <remarks>The path represented by this string doesn't need to exist for this operation to complete properly.</remarks>
		/// <param name="pathString">Represents the path.</param>
		/// <param name="variableFilePath">If this method returns <i>true</i>, this is the returned path object.</param>
		public static bool TryGetVariableFilePath(this string pathString, out IVariableFilePath variableFilePath)
		{
			string failureReasonUnused;
			return pathString.TryGetVariableFilePath(out variableFilePath, out failureReasonUnused);
		}

		private static void EventuallyThrowExOnPathStringNullOrEmpty(this string pathString)
		{
			if (pathString == null)
			{
				throw new ArgumentNullException("pathString");
			}
			if (pathString.Length == 0)
			{
				throw new ArgumentException(FAILURE_PATHSTRING_IS_EMPTY, "pathString");
			}
		}

		//---------------------------------------------------
		//
		// string to IPath TryGet...Path extension methods, with failureReason
		//
		//---------------------------------------------------

		private static bool IsPathStringNullOrEmpty(this string pathString, out string failureReason)
		{
			if (pathString == null)
			{
				failureReason = FAILURE_PATHSTRING_IS_NULL;
				return true;
			}
			if (pathString.Length == 0)
			{
				failureReason = FAILURE_PATHSTRING_IS_EMPTY;
				return true;
			}
			failureReason = null;
			return false;
		}

		private static bool IsThisValidDirectoryPathAValidFilePath(this string pathString, out string fileName, out string failureReason)
		{
			Debug.Assert(pathString != null);
			Debug.Assert(pathString.Length > 0);
			Debug.Assert(pathString.IsValidDirectoryPath());

			var pathStringNormalized = MiscHelpers.NormalizePath(pathString);

			//
			// Special Invalid PathName!
			//
			if (pathStringNormalized.EndsWith(@"\."))
			{
				failureReason = INVALID_FILE_PATH_STRING_COZ_NO_FILE_NAME;
				fileName = null;
				return false;
			}
			if (pathStringNormalized.EndsWith(@"\.."))
			{
				failureReason = INVALID_FILE_PATH_STRING_COZ_NO_FILE_NAME;
				fileName = null;
				return false;
			}

			//
			// Reolve InnerSpecialDir and check the file HasParentDirectory !
			//
			if (AbsoluteRelativePathHelpers.ContainsInnerSpecialDir(pathStringNormalized))
			{
				string pathStringNormalizedResolved, failureReasonUnused;
				var b = AbsoluteRelativePathHelpers.TryResolveInnerSpecialDir(pathStringNormalized, out pathStringNormalizedResolved, out failureReasonUnused);
				Debug.Assert(b); // Coz already verified in a IsValidPath !
				pathStringNormalized = pathStringNormalizedResolved;
			}

			if (!MiscHelpers.HasParentDirectory(pathStringNormalized))
			{
				failureReason = INVALID_FILE_PATH_STRING_COZ_NO_PARENT_DIR;
				fileName = null;
				return false;
			}

			fileName = MiscHelpers.GetLastName(pathStringNormalized);
			Debug.Assert(fileName != null);
			Debug.Assert(fileName.Length > 0);
			Debug.Assert(fileName != AbsoluteRelativePathHelpers.PARENT_DIR_DOUBLEDOT);
			Debug.Assert(fileName != AbsoluteRelativePathHelpers.CURRENT_DIR_SINGLEDOT);
			failureReason = null;
			return true;
		}

		private static bool TryGetNotNullNormalizedPath(this string pathString, out string pathStringNormalized, out string failureReason)
		{
			pathStringNormalized = null;
			if (pathString == null)
			{
				failureReason = FAILURE_PATHSTRING_IS_NULL;
				return false;
			}
			if (pathString.Length == 0)
			{
				failureReason = FAILURE_PATHSTRING_IS_EMPTY;
				return false;
			}

			var pathStringNormalizedTmp = MiscHelpers.NormalizePath(pathString);
			if (pathStringNormalizedTmp.Length == 0)
			{
				failureReason = FAILURE_PATHSTRING_NORMALIZED_IS_EMPTY;
				return false;
			}
			failureReason = null;
			pathStringNormalized = pathStringNormalizedTmp;
			return true;
		}
	}
}
