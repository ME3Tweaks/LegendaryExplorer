using Caliburn.Micro;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor.Models
{
	public class DifficultyLevel : PropertyChangedBase
	{
		private BindableCollection<DifficultySettings> _difficultyData;
		private string _name;

		public DifficultyLevel(string name = null)
		{
			_difficultyData = new BindableCollection<DifficultySettings>();
			_name = name ?? "None";
		}

		public BindableCollection<DifficultySettings> DifficultyData
		{
			get { return _difficultyData; }
			set
			{
				if (Equals(value, _difficultyData))
				{
					return;
				}

				_difficultyData = value;

				NotifyOfPropertyChange(() => DifficultyData);
			}
		}

		public string Name
		{
			get { return _name; }
			set
			{
				if (value == _name)
				{
					return;
				}

				_name = value;

				NotifyOfPropertyChange(() => Name);
			}
		}
	}
}
