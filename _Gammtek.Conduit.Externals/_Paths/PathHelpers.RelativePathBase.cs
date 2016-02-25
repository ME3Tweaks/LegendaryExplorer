using System.Diagnostics;

namespace Gammtek.Conduit.Paths
{
	partial class PathHelpers
	{
		private abstract class RelativePathBase : PathBase, IRelativePath
		{
			protected RelativePathBase(string path)
				:
					base(path)
			{
				Debug.Assert(path != null);
				Debug.Assert(path.Length > 0);
			}

			public override bool IsAbsolutePath => false;

			public override bool IsEnvVarPath => false;

			public override bool IsRelativePath => true;

			public override bool IsVariablePath => false;

			public override IDirectoryPath ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(CurrentPath);
					return parentPath.ToRelativeDirectoryPath();
				}
			}

			public override PathMode PathMode => PathMode.Relative;

			IRelativeDirectoryPath IRelativePath.ParentDirectoryPath
			{
				get
				{
					var parentPath = MiscHelpers.GetParentDirectory(CurrentPath);
					return parentPath.ToRelativeDirectoryPath();
				}
			}

			public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path)
			{
				Debug.Assert(path != null); // Enforced by contract
				string pathAbsoluteUnused, failureReasonUnused;
				return AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out pathAbsoluteUnused, out failureReasonUnused);
			}

			public bool CanGetAbsolutePathFrom(IAbsoluteDirectoryPath path, out string failureMessage)
			{
				Debug.Assert(path != null); // Enforced by contract
				string pathAbsoluteUnused;
				return AbsoluteRelativePathHelpers.TryGetAbsolutePathFrom(path, this, out pathAbsoluteUnused, out failureMessage);
			}

			//
			//  Absolute/Relative pathString conversion
			//
			public abstract IAbsolutePath GetAbsolutePathFrom(IAbsoluteDirectoryPath path);
		}
	}
}
