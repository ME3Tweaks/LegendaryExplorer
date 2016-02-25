using Caliburn.Micro;
using Gammtek.Conduit.MassEffect.DifficultyEditor.Models;

namespace Gammtek.Conduit.MassEffect.DifficultyEditor.ViewModels
{
	public class ShellViewModel : PropertyChangedBase, IShell
	{
		private BindableCollection<DifficultyLevel> _difficultyLevels;
		private AbilityDifficultyData _selectedDifficultyCategoryData;
		private DifficultyLevel _selectedDifficultyLevel;
		private DifficultySettings _selectedDifficultySetting;

		public ShellViewModel()
		{
			DifficultyLevels = new BindableCollection<DifficultyLevel>();

			/*var difficultyLevel1 = new DifficultyLevel("DifficultyLevel1");
            var difficultySettings1 = new DifficultySettings("DifficultyLevel1");
			difficultySettings1.CategoryData.AddRange(new BindableCollection<AbilityDifficultyData>()
			{
				new AbilityDifficultyData("AbilityDifficultyData1", new Vector2D(0.5f, 0.5f)),
				new AbilityDifficultyData("AbilityDifficultyData2", new Vector2D(2.0f, 2.0f)),
				new AbilityDifficultyData("AbilityDifficultyData3", new Vector2D(5.0f, 5.0f)),
				new AbilityDifficultyData("AbilityDifficultyData4", new Vector2D()),
				new AbilityDifficultyData("AbilityDifficultyData5", new Vector2D(-1.0f, -1.0f))
			});
			difficultyLevel1.DifficultyData.Add(difficultySettings1);
			DifficultyLevels.Add(difficultyLevel1);
			DifficultyLevels.Add(new DifficultyLevel("DifficultyLevel2"));
			DifficultyLevels.Add(new DifficultyLevel("DifficultyLevel3"));
			DifficultyLevels.Add(new DifficultyLevel("DifficultyLevel4"));
			DifficultyLevels.Add(new DifficultyLevel("DifficultyLevel5"));
			DifficultyLevels.Add(new DifficultyLevel("DifficultyLevel6"));*/
        }

		public BindableCollection<DifficultyLevel> DifficultyLevels
		{
			get { return _difficultyLevels; }
			set
			{
				if (Equals(value, _difficultyLevels))
				{
					return;
				}
				_difficultyLevels = value;
				NotifyOfPropertyChange(() => DifficultyLevels);
			}
		}

		public AbilityDifficultyData SelectedDifficultyCategoryData
		{
			get { return _selectedDifficultyCategoryData; }
			set
			{
				if (Equals(value, _selectedDifficultyCategoryData))
				{
					return;
				}
				_selectedDifficultyCategoryData = value;
				NotifyOfPropertyChange(() => SelectedDifficultyCategoryData);
			}
		}

		public DifficultyLevel SelectedDifficultyLevel
		{
			get { return _selectedDifficultyLevel; }
			set
			{
				if (Equals(value, _selectedDifficultyLevel))
				{
					return;
				}
				_selectedDifficultyLevel = value;
				NotifyOfPropertyChange(() => SelectedDifficultyLevel);
			}
		}

		public DifficultySettings SelectedDifficultySetting
		{
			get { return _selectedDifficultySetting; }
			set
			{
				if (Equals(value, _selectedDifficultySetting))
				{
					return;
				}
				_selectedDifficultySetting = value;
				NotifyOfPropertyChange(() => SelectedDifficultySetting);
			}
		}
	}
}
