using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace ME3ExplorerCore.Gammtek.Collections.ComponentModel
{
	public class ObservableInt32Collection : ObservableCollection<int>
	{
		public ObservableInt32Collection() {}

		public ObservableInt32Collection(List<int> list)
			: base(list) {}

		public ObservableInt32Collection([NotNull] IEnumerable<int> collection)
			: base(collection) {}
	}
}
