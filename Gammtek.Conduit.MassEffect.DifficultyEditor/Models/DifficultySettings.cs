using Caliburn.Micro;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor.Models
{
	public class DifficultySettings : PropertyChangedBase
	{
		private string _category;
		private BindableCollection<AbilityDifficultyData> _categoryData;

		public DifficultySettings(string category = null)
		{
			_category = category ?? "None";
			_categoryData = new BindableCollection<AbilityDifficultyData>();
		}

		public string Category
		{
			get { return _category; }
			set
			{
				if (value == _category)
				{
					return;
				}

				_category = value;

				NotifyOfPropertyChange(() => Category);
			}
		}

		public BindableCollection<AbilityDifficultyData> CategoryData
		{
			get { return _categoryData; }
			set
			{
				if (Equals(value, _categoryData))
				{
					return;
				}

				_categoryData = value;

				NotifyOfPropertyChange(() => CategoryData);
			}
		}
	}
}
