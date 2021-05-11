using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace LegendaryExplorerCore.Gammtek.Collections.ComponentModel
{
	public class ObservableUInt64Collection : ObservableCollection<ulong>
	{
		public ObservableUInt64Collection() {}

		public ObservableUInt64Collection(List<ulong> list)
			: base(list) {}

		public ObservableUInt64Collection([NotNull] IEnumerable<ulong> collection)
			: base(collection) {}
	}
}
