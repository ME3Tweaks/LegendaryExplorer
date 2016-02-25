using System;

namespace Gammtek.Conduit.MassEffect.Coalesce
{
	[Flags]
	public enum UpdateActions
	{
		Add,
		Clear,
		Ignore,
		Remove,
		Update
	}
}
