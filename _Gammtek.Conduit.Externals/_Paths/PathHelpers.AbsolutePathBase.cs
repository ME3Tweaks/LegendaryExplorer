using System;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class AbsolutePathBase : PathBase, IAbsolutePath
		{
			protected AbsolutePathBase(string path)
				: base(path)
			{
				//Debug.Assert(path != null);
				//Debug.Assert(path.Length > 0);

				if (UNCPathHelper.IsAnAbsoluteUNCPath(CurrentPath))
				{
					Kind = AbsolutePathKind.UNC;
				}
				else
				{
					//Debug.Assert(AbsoluteRelativePathHelpers.IsAnAbsoluteDriveLetterPath(CurrentPath));

					Kind = AbsolutePathKind.DriveLetter;
				}
			}

			public IDriveLetter DriveLetter
			{
				get
				{
					//Debug.Assert(IsAbsolutePath);
					//Debug.Assert(CurrentPath.Length > 0);

					if (Kind != AbsolutePathKind.DriveLetter)
					{
						throw new InvalidOperationException("The property getter DriveLetter must be called on a pathString of kind DriveLetter.");
					}

					var driveName = CurrentPath[0].ToString();

					//Debug.Assert(PathHelpers.DriveLetter.IsValidDriveName(driveName));

					return new DriveLetter(driveName);
				}
			}

			public abstract bool Exists { get; }

			public override bool HasParentDirectory
			{
				get
				{
					var myPathString = CurrentPath;

					if (Kind != AbsolutePathKind.UNC)
					{
						return MiscHelpers.HasParentDirectory(myPathString);
					}

					string serverShareStart;

					myPathString = UNCPathHelper.TranformUNCIntoDriveLetter(CurrentPath, out serverShareStart);

					return MiscHelpers.HasParentDirectory(myPathString);
				}
			}

			public override bool IsAbsolutePath => true;

			public override bool IsEnvVarPath => false;

			public override bool IsRelativePath => false;

			public override bool IsVariablePath => false;

			public AbsolutePathKind Kind { get; }

			public override IDirectoryPath ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(CurrentPath);

					return parentPath.ToAbsoluteDirectoryPath();
				}
			}

			public override PathMode PathMode => PathMode.Absolute;

			public string UNCServer
			{
				get
				{
					if (Kind != AbsolutePathKind.UNC)
					{
						throw new InvalidOperationException("The property getter UNCServer must be called on a pathString of kind UNC.");
					}

					// Here we already checked the m_PathString is UNC, hence it is like "\\server\share" with maybe a file path after!
					//Debug.Assert(CurrentPath.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal) == 0);

					var twoSeparatorLength = MiscHelpers.TWO_DIR_SEPARATOR_STRING.Length;
					var index = CurrentPath.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, twoSeparatorLength);

					//Debug.Assert(index > twoSeparatorLength);

					var server = CurrentPath.Substring(twoSeparatorLength, index - twoSeparatorLength);

					return server;
				}
			}

			public string UNCShare
			{
				get
				{
					if (Kind != AbsolutePathKind.UNC)
					{
						throw new InvalidOperationException("The property getter UNCShare must be called on a pathString of kind UNC.");
					}

					// Here we already checked the m_PathString is UNC, hence it is like "\\server\share" with maybe a file path after!
					//Debug.Assert(CurrentPath.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING, StringComparison.Ordinal) == 0);

					var indexShareBegin = CurrentPath.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, 2);

					//Debug.Assert(indexShareBegin > 2);

					indexShareBegin++;

					var indexShareEnd = CurrentPath.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, indexShareBegin);

					if (indexShareEnd == -1)
					{
						indexShareEnd = CurrentPath.Length;
					}

					//Debug.Assert(indexShareEnd > indexShareBegin);

					var server = CurrentPath.Substring(indexShareBegin, indexShareEnd - indexShareBegin);

					return server;
				}
			}

			IAbsoluteDirectoryPath IAbsolutePath.ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(CurrentPath);

					return parentPath.ToAbsoluteDirectoryPath();
				}
			}

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage);

			public abstract IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);

			public bool OnSameVolumeThan(IAbsolutePath otherAbsolutePath)
			{
				//Debug.Assert(otherAbsolutePath != null); // Enforced by contract

				if (Kind != otherAbsolutePath.Kind)
				{
					return false;
				}

				switch (Kind)
				{
					case AbsolutePathKind.DriveLetter:
						return DriveLetter.Equals(otherAbsolutePath.DriveLetter);
					default:
						//Debug.Assert(Kind == AbsolutePathKind.UNC);

						// Compare UNC server and share, with ignorcase.
						return string.Compare(UNCServer, otherAbsolutePath.UNCServer, StringComparison.OrdinalIgnoreCase) == 0 &&
							   string.Compare(UNCShare, otherAbsolutePath.UNCShare, StringComparison.OrdinalIgnoreCase) == 0;
				}
			}
		}
	}
}
