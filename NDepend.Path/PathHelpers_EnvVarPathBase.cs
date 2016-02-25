using System;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private abstract class EnvVarPathBase : PathBase, IEnvVarPath
		{
			//
			// EnvVar   (env var name with percent like "%TEMP%" )
			//

			protected EnvVarPathBase(string pathString)
				:
					base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);

				EnvVar = ComputeEnvVar();
				Debug.Assert(EnvVar != null);
				Debug.Assert(EnvVar.Length >= 3);
				Debug.Assert(EnvVar[0] == MiscHelpers.ENVVAR_PERCENT);
				Debug.Assert(EnvVar[EnvVar.Length - 1] == MiscHelpers.ENVVAR_PERCENT);
			}

			public string EnvVar { get; }


			public override bool IsAbsolutePath => false;

			public override bool IsEnvVarPath => true;

			public override bool IsRelativePath => false;

			public override bool IsVariablePath => false;

			public override IDirectoryPath ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(m_PathString);
					return parentPath.ToEnvVarDirectoryPath();
				}
			}

			public override PathMode PathMode => PathMode.EnvVar;


			//
			// ParentDirectoryPath 
			//
			IEnvVarDirectoryPath IEnvVarPath.ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(m_PathString);
					return parentPath.ToEnvVarDirectoryPath();
				}
			}


			// This methods are implemented in EnvVarFilePath and EnvVarDirectoryPath.
			public abstract EnvVarPathResolvingStatus TryResolve(out IAbsolutePath resolvedPath);

			public abstract bool TryResolve(out IAbsolutePath resolvedPath, out string failureMessage);

			protected string GetErrorEnvVarResolvedButCannotConvertToAbsolutePathFailureReason()
			{
				string envVarValue;
				var b = TryExpandMyEnvironmentVariables(out envVarValue);
				Debug.Assert(b); // If the error EnvVarResolvedButCanConvertToAbsolutePath occurs

				// it means envVar can be resolved!
				return "The environment variable " + EnvVar + " is resolved into the value {" + envVarValue
					   + "} but this value cannot be the prefix of an absolute path.";
			}

			protected string GetErrorUnresolvedEnvVarFailureReason()
			{
				return "Can't resolve the environment variable " + EnvVar + ".";
			}


			//
			// EnvVar resolving!
			//
			protected bool TryResolveEnvVar(out string pathStringResolved)
			{
				string envVarValue;
				if (!TryExpandMyEnvironmentVariables(out envVarValue))
				{
					pathStringResolved = null;
					return false;
				}
				Debug.Assert(envVarValue != null);
				Debug.Assert(envVarValue.Length > 0);

				var envVarWith2PercentsLength = EnvVar.Length;
				Debug.Assert(m_PathString.Length >= envVarWith2PercentsLength);

				var pathStringWithoutEnvVar = m_PathString.Substring(envVarWith2PercentsLength, m_PathString.Length - envVarWith2PercentsLength);

				pathStringResolved = envVarValue + pathStringWithoutEnvVar;
				return true;
			}

			private string ComputeEnvVar()
			{
				Debug.Assert(MiscHelpers.IsAnEnvVarPath(m_PathString));
				Debug.Assert(m_PathString[0] == MiscHelpers.ENVVAR_PERCENT);
				var indexClose = m_PathString.IndexOf(MiscHelpers.ENVVAR_PERCENT, 1);
				Debug.Assert(indexClose > 1);
				return m_PathString.Substring(0, indexClose + 1);
			}

			private bool TryExpandMyEnvironmentVariables(out string envVarValue)
			{
				envVarValue = Environment.ExpandEnvironmentVariables(EnvVar);
				return // envVarValue != null &&   <--  Resharper tells that this is always true!
					envVarValue.Length > 0 &&
					envVarValue != EnvVar; // Replacement only occurs for environment variables that are set. 
			}
		}
	}
}
