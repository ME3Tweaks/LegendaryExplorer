using System;
using System.Diagnostics;

namespace NDepend.Path
{
	partial class PathHelpers
	{
		private abstract class AbsolutePathBase : PathBase, IAbsolutePath
		{
			protected AbsolutePathBase(string pathString)
				: base(pathString)
			{
				Debug.Assert(pathString != null);
				Debug.Assert(pathString.Length > 0);

				if (UNCPathHelper.IsAnAbsoluteUNCPath(m_PathString))
				{
					Kind = AbsolutePathKind.UNC;
				}
				else
				{
					Debug.Assert(AbsoluteRelativePathHelpers.IsAnAbsoluteDriveLetterPath(m_PathString));
					Kind = AbsolutePathKind.DriveLetter;
				}
			}


			//
			//  DriveLetter
			//
			public IDriveLetter DriveLetter
			{
				get
				{
					Debug.Assert(IsAbsolutePath);
					Debug.Assert(m_PathString.Length > 0);
					if (Kind != AbsolutePathKind.DriveLetter)
					{
						throw new InvalidOperationException("The property getter DriveLetter must be called on a pathString of kind DriveLetter.");
					}
					var driveName = m_PathString[0].ToString();
					Debug.Assert(PathHelpers.DriveLetter.IsValidDriveName(driveName));
					return new DriveLetter(driveName);
				}
			}

			public abstract bool Exists { get; }

			public override bool HasParentDirectory
			{
				get
				{
					var myPathString = m_PathString;
					if (Kind == AbsolutePathKind.UNC)
					{
						string unused;
						myPathString = UNCPathHelper.TranformUNCIntoDriveLetter(m_PathString, out unused);
					}
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
					var parentPath = MiscHelpers.GetParentDirectory(m_PathString);
					return parentPath.ToAbsoluteDirectoryPath();
				}
			}

			public override PathMode PathMode => PathMode.Absolute;


			//
			// UNC  Universal Naming Convention  http://compnetworking.about.com/od/windowsnetworking/g/unc-name.htm 
			// \\server\share\file_path   (share seems mandatory)
			//
			public string UNCServer
			{
				get
				{
					if (Kind != AbsolutePathKind.UNC)
					{
						throw new InvalidOperationException("The property getter UNCServer must be called on a pathString of kind UNC.");
					}

					// Here we already checked the m_PathString is UNC, hence it is like "\\server\share" with maybe a file path after!
					Debug.Assert(m_PathString.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING) == 0);
					var twoSeparatorLength = MiscHelpers.TWO_DIR_SEPARATOR_STRING.Length;
					var index = m_PathString.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, twoSeparatorLength);
					Debug.Assert(index > twoSeparatorLength);
					var server = m_PathString.Substring(twoSeparatorLength, index - twoSeparatorLength);
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
					Debug.Assert(m_PathString.IndexOf(MiscHelpers.TWO_DIR_SEPARATOR_STRING) == 0);
					var indexShareBegin = m_PathString.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, 2);
					Debug.Assert(indexShareBegin > 2);
					indexShareBegin++;
					var indexShareEnd = m_PathString.IndexOf(MiscHelpers.DIR_SEPARATOR_CHAR, indexShareBegin);
					if (indexShareEnd == -1)
					{
						indexShareEnd = m_PathString.Length;
					}
					Debug.Assert(indexShareEnd > indexShareBegin);
					var server = m_PathString.Substring(indexShareBegin, indexShareEnd - indexShareBegin);
					return server;
				}
			}

			IAbsoluteDirectoryPath IAbsolutePath.ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(m_PathString);
					return parentPath.ToAbsoluteDirectoryPath();
				}
			}

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);

			public abstract bool CanGetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory, out string failureMessage);


			//
			// These methods are abstract at this level and are implemented at File and Directory level!
			//
			public abstract IRelativePath GetRelativePathFrom(IAbsoluteDirectoryPath pivotDirectory);


			public bool OnSameVolumeThan(IAbsolutePath otherAbsolutePath)
			{
				Debug.Assert(otherAbsolutePath != null); // Enforced by contract
				if (Kind != otherAbsolutePath.Kind)
				{
					return false;
				}
				switch (Kind)
				{
					case AbsolutePathKind.DriveLetter:
						return DriveLetter.Equals(otherAbsolutePath.DriveLetter);
					default:
						Debug.Assert(Kind == AbsolutePathKind.UNC);

						// Compare UNC server and share, with ignorcase.
						return String.Compare(UNCServer, otherAbsolutePath.UNCServer, true) == 0 &&
							   String.Compare(UNCShare, otherAbsolutePath.UNCShare, true) == 0;
				}
			}
		}
	}
}
