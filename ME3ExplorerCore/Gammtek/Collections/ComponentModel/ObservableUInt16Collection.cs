using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ME3ExplorerCore.Gammtek.Collections.ComponentModel
{
	public class ObservableInt16Collection : ObservableCollection<short>
	{
		public ObservableInt16Collection() {}

		public ObservableInt16Collection(List<short> list)
			: base(list) {}

		public ObservableInt16Collection([NotNull] IEnumerable<short> collection)
			: base(collection) {}
	}
}
