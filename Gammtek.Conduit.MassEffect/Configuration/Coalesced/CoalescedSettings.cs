using System.Collections.Generic;

namespace Gammtek.Conduit.MassEffect.Configuration.Coalesced
{
	public class CoalescedSettings : SettingsBase
	{
		public CoalescedSettings(IDictionary<string, object> properties = null)
		{
			Properties = properties ?? new SortedList<string, object>();
		}
	}
}
