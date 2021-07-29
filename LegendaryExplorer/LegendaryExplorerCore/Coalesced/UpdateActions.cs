using System;

namespace LegendaryExplorerCore.Coalesced
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
