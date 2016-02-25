using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private static class VariablePathHelpers
		{
			internal static bool DoesFileNameContainVariable(string fileName)
			{
				Debug.Assert(fileName != null);
				Debug.Assert(fileName.Length > 0);
				Debug.Assert(!fileName.Contains(MiscHelpers.DIR_SEPARATOR_STRING));

				string variableNameUnused, failureReasonUnused;
				int indexOutUnused;
				var result = TryGetNextVariable(
					0,
					fileName,
					out variableNameUnused,
					out indexOutUnused,
					out failureReasonUnused);

				// Don't allow fileName to contains "$(" even if in theory it'd be possible
				// But user shouldn't mess up with variable path containing the string "$(".
				return result != TryGetNextVariableResult.VariableNotFound;
			}

			internal static string GetParentDirectory(string path)
			{
				Debug.Assert(path != null);
				if (!HasParentDirectory(path))
				{
					throw new InvalidOperationException(@"Can't get the parent dir from the pathString """ + path + @"""");
				}
				var index = path.LastIndexOf(MiscHelpers.DIR_SEPARATOR_CHAR);
				Debug.Assert(index >= 0);
				return path.Substring(0, index);
			}

			// Special helper to format failureReason
			internal static string GetUnresolvedVariableFailureReason(IReadOnlyList<string> unresolvedVariables)
			{
				Debug.Assert(unresolvedVariables != null);
				var nbUnresolvedVariables = unresolvedVariables.Count;
				Debug.Assert(nbUnresolvedVariables > 0);
				var sb = new StringBuilder("The following variable");
				sb.Append(unresolvedVariables.Count > 1 ? "s" : "");
				sb.Append(" cannot be resolved: ");

				for (var i = 0; i < nbUnresolvedVariables; i++)
				{
					sb.Append(PathVariableBegin);
					var unresolvedVariableName = unresolvedVariables[i];
					sb.Append(unresolvedVariableName);
					sb.Append(PathVariableEnd);
					if (i < nbUnresolvedVariables - 1)
					{
						sb.Append(" ");
					}
				}
				return sb.ToString();
			}

			// Special HasParentDirectory impl for VariablePaths
			internal static bool HasParentDirectory(string path)
			{
				Debug.Assert(path != null);

				if (!MiscHelpers.HasParentDirectory(path))
				{
					return false;
				}
				var lastName = MiscHelpers.GetLastName(path);

				// Here, we ensure that there is no variable defined into the last fileName or directoryName!
				return !DoesFileNameContainVariable(lastName);
			}

			//-------------------------------------------------------------------------
			//
			// Determine if a string is a variable path and extract variables!
			//
			//-------------------------------------------------------------------------
			internal static bool IsAVariablePath(string pathStringNormalized, out IReadOnlyList<string> variables, out string failureReason)
			{
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(pathStringNormalized.IsNormalized());

				//
				// A variable with the syntax $(variableName) must be defined at first position in string.
				//
				int indexOut;
				string variableName;
				string failureReasonTmp;
				var result = TryGetNextVariable(0, pathStringNormalized, out variableName, out indexOut, out failureReasonTmp);
				switch (result)
				{
					default:
						Debug.Assert(result == TryGetNextVariableResult.VariableFound);
						Debug.Assert(variableName != null);
						Debug.Assert(variableName.Length > 0);
						if (indexOut - variableName.Length - PathVariableBegin.Length - PathVariableEnd.Length != 0)
						{
							failureReason = "The variable $(" + variableName + "} must be located at the beginning of the path string.";
							variables = null;
							return false;
						}

						// variable at first position found!!
						break;

					case TryGetNextVariableResult.VariableNotFound:
						failureReason = @"A variable with the syntax $(variableName) must be defined at the beginning of the path string.";
						variables = null;
						return false;

					case TryGetNextVariableResult.ErrorSyntaxFound:
						goto SYNTAX_ERROR_FOUND;
				}
				var variablesList = new List<string> { variableName };

				//
				// Search for more variables with the syntax $(variableName)
				//
				while (true)
				{
					var indexIn = indexOut;
					result = TryGetNextVariable(indexIn, pathStringNormalized, out variableName, out indexOut, out failureReasonTmp);
					switch (result)
					{
						default:
							Debug.Assert(result == TryGetNextVariableResult.VariableFound);
							Debug.Assert(variableName != null);
							Debug.Assert(variableName.Length > 0);

							// Add the variable name only if it has not already been added (string insensitive)
							// don't use a hashset coz: 1) we don't expect plenty of variables 2) it would disturb the order of variable!
							var bAlreadyAdded = false;
							foreach (var variableNameAlreadyAdded in variablesList)
							{
								if (string.Compare(variableNameAlreadyAdded, variableName, true) != 0)
								{
									// true for ignoreCase!
									continue;
								}
								bAlreadyAdded = true;
							}
							if (!bAlreadyAdded)
							{
								variablesList.Add(variableName);
							}
							continue; // Try find next variable!

						case TryGetNextVariableResult.VariableNotFound:

							// Ok, no more variable!
							variables = variablesList.ToReadOnlyWrappedList();
							failureReason = null;
							return true;

						case TryGetNextVariableResult.ErrorSyntaxFound:
							goto SYNTAX_ERROR_FOUND;
					}
				}

				SYNTAX_ERROR_FOUND:
				Debug.Assert(failureReasonTmp != null);
				Debug.Assert(failureReasonTmp.Length > 0);
				failureReason = @"Variable syntax error : " + failureReasonTmp;
				variables = null;
				return false;
			}

			//-------------------------------------------------------------------------
			//
			// ReplaceVariableWithValue used at resolving time
			//
			//-------------------------------------------------------------------------
			internal static string ReplaceVariableWithValue(string pathString, string variableNameToResolve, string variableValue)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);
				Debug.Assert(variableNameToResolve != null);
				Debug.Assert(variableNameToResolve.Length > 0);
				Debug.Assert(variableValue != null); // variableValue can be empty!

				var variableNameDecorated = PathVariableBegin + variableNameToResolve + PathVariableEnd;

				string pathStringTmp1;
				var index = 0;
				var b = TryReplaceVariableWithValue(index, pathString, variableNameDecorated, variableValue, out pathStringTmp1);
				Debug.Assert(b); // At least one occurence of the variable must be found and replaced!

				while (true)
				{
					string pathStringTmp2;
					if (!TryReplaceVariableWithValue(index, pathStringTmp1, variableNameDecorated, variableValue, out pathStringTmp2))
					{
						break;
					}
					pathStringTmp1 = pathStringTmp2;

					// Do increase index with variableValue.Length.
					// Because we don't want to support recursive scenarios that can end up in infinite loop!
					// like when "$(variableName)" get replaced with "$(variableName)" or even "XYZ$(variableName)XYZ"
					index += variableValue.Length;
				}
				return pathStringTmp1;
			}

			// Search for a variable with name $(variableName) from indexIn in pathStringNormalized
			private static TryGetNextVariableResult TryGetNextVariable(
				int indexIn,
				string pathStringNormalized,
				out string variableName,
				out int indexOut, // represent the index at the end of found $(variableName)
				out string failureReasonTmp)
			{
				Debug.Assert(indexIn >= 0);
				Debug.Assert(pathStringNormalized != null);
				Debug.Assert(pathStringNormalized.Length > 0);
				Debug.Assert(indexIn <= pathStringNormalized.Length);

				var indexBegin = pathStringNormalized.IndexOf(PathVariableBegin, indexIn);
				if (indexBegin == -1)
				{
					indexOut = -1;
					variableName = null;
					failureReasonTmp = null;
					return TryGetNextVariableResult.VariableNotFound;
				}
				var indexVariableNameBegin = indexBegin + PathVariableBegin.Length;

				var indexvariableNameEnd = pathStringNormalized.IndexOf(PathVariableEnd, indexVariableNameBegin);
				if (indexvariableNameEnd == -1)
				{
					indexOut = -1;
					variableName = null;
					failureReasonTmp = @"Found variable opening ""$("" without closing parenthesis, at position " + indexBegin + ".";
					return TryGetNextVariableResult.ErrorSyntaxFound;
				}

				Debug.Assert(indexvariableNameEnd >= indexVariableNameBegin);
				if (indexvariableNameEnd == indexVariableNameBegin)
				{
					indexOut = -1;
					variableName = null;
					failureReasonTmp = @"Found variable with empty name at position " + indexBegin + ".";
					return TryGetNextVariableResult.ErrorSyntaxFound;
				}

				variableName = pathStringNormalized.Substring(indexVariableNameBegin, indexvariableNameEnd - indexVariableNameBegin);
				Debug.Assert(variableName.Length > 0);

				// Allowed char for variableName: Letter (upper/lower) / Number / Underscore
				if (!variableName.IsValidPathVariableName())
				{
					indexOut = -1;
					failureReasonTmp = @"Found variable with name " + PathVariableBegin + variableName + PathVariableEnd
									   + ". A variable name must contain only upper/lower case letters, digits and underscore characters.";
					variableName = null;
					return TryGetNextVariableResult.ErrorSyntaxFound;
				}

				indexOut = indexvariableNameEnd + PathVariableEnd.Length;
				failureReasonTmp = null;
				return TryGetNextVariableResult.VariableFound;
			}

			private static bool TryReplaceVariableWithValue(int index, string pathStringIn, string variableNameDecorated, string variableValue,
				out string pathStringOut)
			{
				Debug.Assert(index >= 0);
				Debug.Assert(pathStringIn != null);
				Debug.Assert(pathStringIn.Length > 0);
				Debug.Assert(index < pathStringIn.Length);
				Debug.Assert(variableNameDecorated != null);
				Debug.Assert(variableNameDecorated.Length > 0);
				Debug.Assert(variableValue != null); // variableValue can be empty!

				// Path variable name are case insensitive.
				var indexVariable = pathStringIn.IndexOf(variableNameDecorated, index, StringComparison.InvariantCultureIgnoreCase);
				if (indexVariable == -1)
				{
					pathStringOut = null;
					return false;
				}

				pathStringOut = pathStringIn.Remove(indexVariable, variableNameDecorated.Length);
				if (variableValue.Length > 0)
				{
					pathStringOut = pathStringOut.Insert(indexVariable, variableValue);
				}
				return true;
			}

			private enum TryGetNextVariableResult
			{
				VariableFound,
				VariableNotFound,
				ErrorSyntaxFound
			}
		}
	}
}
