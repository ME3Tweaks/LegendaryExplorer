using Caliburn.Micro;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor.Models
{
	public class AbilityDifficultyData : PropertyChangedBase
	{
		private string _statName;
		private Vector2D _statRange;

		public AbilityDifficultyData(string statName = null, Vector2D statRange = null)
		{
			_statName = statName ?? "None";
			_statRange = statRange ?? new Vector2D();
		}

		public string StatName
		{
			get { return _statName; }
			set
			{
				if (value == _statName)
				{
					return;
				}
				_statName = value;
				NotifyOfPropertyChange(() => StatName);
			}
		}

		public Vector2D StatRange
		{
			get { return _statRange; }
			set
			{
				if (Equals(value, _statRange))
				{
					return;
				}
				_statRange = value;
				NotifyOfPropertyChange(() => StatRange);
			}
		}
	}
}
