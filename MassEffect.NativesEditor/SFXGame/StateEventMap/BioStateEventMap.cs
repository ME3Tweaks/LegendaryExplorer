using System.Collections.Generic;
using Gammtek.Conduit.ComponentModel;

namespace Gammtek.Conduit.MassEffect3.SFXGame.StateEventMap
{
	public class BioStateEventMap : BindableBase
	{
		private IList<int> _galaxyAtWarBoolVarIds;
		private IList<int> _galaxyAtWarFloatVarIds;
		private IList<int> _galaxyAtWarIntVarIds;
		private IDictionary<int, BioStateEvent> _stateEvents;

		public BioStateEventMap(IDictionary<int, BioStateEvent> events = null)
		{
			StateEvents = events ?? new Dictionary<int, BioStateEvent>();
		}

		public IList<int> GalaxyAtWarBoolVarIds
		{
			get { return _galaxyAtWarBoolVarIds; }
			set { SetProperty(ref _galaxyAtWarBoolVarIds, value); }
		}

		public IList<int> GalaxyAtWarFloatVarIds
		{
			get { return _galaxyAtWarFloatVarIds; }
			set { SetProperty(ref _galaxyAtWarFloatVarIds, value); }
		}

		public IList<int> GalaxyAtWarIntVarIds
		{
			get { return _galaxyAtWarIntVarIds; }
			set { SetProperty(ref _galaxyAtWarIntVarIds, value); }
		}

		public IDictionary<int, BioStateEvent> StateEvents
		{
			get { return _stateEvents; }
			set { SetProperty(ref _stateEvents, value); }
		}
	}
}
