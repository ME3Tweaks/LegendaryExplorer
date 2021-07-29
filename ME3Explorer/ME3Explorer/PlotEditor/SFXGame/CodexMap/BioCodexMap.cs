using System.Collections.Generic;
using ME3ExplorerCore.Gammtek.ComponentModel;

namespace Gammtek.Conduit.MassEffect3.SFXGame.CodexMap
{
	public class BioCodexMap : BindableBase
	{
		private IDictionary<int, BioCodexPage> _pages;
		private IDictionary<int, BioCodexSection> _sections;

		public BioCodexMap(IDictionary<int, BioCodexSection> sections = null, IDictionary<int, BioCodexPage> pages = null)
		{
			Pages = pages ?? new Dictionary<int, BioCodexPage>();
			Sections = sections ?? new Dictionary<int, BioCodexSection>();
		}

		public IDictionary<int, BioCodexPage> Pages
		{
			get => _pages;
		    set => SetProperty(ref _pages, value);
		}

		public IDictionary<int, BioCodexSection> Sections
		{
			get => _sections;
		    set => SetProperty(ref _sections, value);
		}
	}
}
