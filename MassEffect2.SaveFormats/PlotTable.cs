using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("PlotTableSaveRecord")]
	public class PlotTable : IPlotTable, ISerializable, INotifyPropertyChanged
	{
		public PlotTable()
		{
			_Helpers = new PlotTableWrapper(this);
			_BoolVariablesWrapper = new BitArrayWrapper(_BoolVariables);
		}

		#region Fields

		private readonly BitArrayWrapper _BoolVariablesWrapper;
		private readonly PlotTableWrapper _Helpers;

		[OriginalName("BoolVariables")]
		private BitArray _BoolVariables = new BitArray(0);

		[OriginalName("CodexEntries")]
		private List<PlotCodex> _CodexEntries = new List<PlotCodex>();

		[OriginalName("CodexIDs")]
		private List<int> _CodexIDs = new List<int>();

		[OriginalName("FloatVariables")]
		private List<float> _FloatVariables = new List<float>();

		[OriginalName("IntVariables")]
		private List<int> _IntVariables = new List<int>();

		[OriginalName("QuestIDs")]
		private List<int> _QuestIDs = new List<int>();

		[OriginalName("QuestProgress")]
		private List<PlotQuest> _QuestProgress = new List<PlotQuest>();

		[OriginalName("QuestProgressCounter")]
		private int _QuestProgressCounter;

		#endregion

		#region Helpers

		public bool GetBoolVariable(int index)
		{
			if (index >= _BoolVariables.Count)
			{
				return false;
			}

			return _BoolVariables[index];
		}

		public void SetBoolVariable(int index, bool value)
		{
			if (index >= _BoolVariables.Count)
			{
				_BoolVariables.Length = index + 1;
			}

			_BoolVariables[index] = value;
		}

		public int GetIntVariable(int index)
		{
			if (index >= IntVariables.Count)
			{
				return 0;
			}

			return IntVariables[index];
		}

		public void SetIntVariable(int index, int value)
		{
			if (index >= IntVariables.Count)
			{
				IntVariables.Capacity = index + 1;
			}

			IntVariables[index] = value;
		}

		public float GetFloatVariable(int index)
		{
			if (index >= FloatVariables.Count)
			{
				return 0;
			}

			return FloatVariables[index];
		}

		public void SetFloatVariable(int index, float value)
		{
			if (index >= IntVariables.Count)
			{
				IntVariables.Capacity = index + 1;
			}

			FloatVariables[index] = value;
		}

		#endregion

		#region Serialize

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _BoolVariables);
			stream.Serialize(ref _IntVariables);
			stream.Serialize(ref _FloatVariables);
			stream.Serialize(ref _QuestProgressCounter);
			stream.Serialize(ref _QuestProgress);
			stream.Serialize(ref _QuestIDs);
			stream.Serialize(ref _CodexEntries);
			stream.Serialize(ref _CodexIDs);
		}

		#endregion

		#region Properties

		[LocalizedDisplayName("Helpers", typeof (Localization.PlotTable))]
		public PlotTableWrapper Helpers
		{
			get { return _Helpers; }
		}

		[Browsable(false)]
		[LocalizedDisplayName("BoolVariables", typeof (Localization.PlotTable))]
		public BitArray BoolVariables
		{
			get { return _BoolVariables; }
		}

		[LocalizedDisplayName("BoolVariablesWrapper", typeof (Localization.PlotTable))]
		public BitArrayWrapper BoolVariablesWrapper
		{
			get { return _BoolVariablesWrapper; }
			/*set
            {
                if (value != this._BoolVariables)
                {
                    this._BoolVariables = value;
                    this.NotifyPropertyChanged("BoolVariables");
                }
            }*/
		}

		[LocalizedDisplayName("IntVariables", typeof (Localization.PlotTable))]
		//public List<IntVariablePair> IntVariables
		public List<int> IntVariables
		{
			get { return _IntVariables; }
			set
			{
				if (value != _IntVariables)
				{
					_IntVariables = value;
					NotifyPropertyChanged("IntVariables");
				}
			}
		}

		[LocalizedDisplayName("FloatVariables", typeof (Localization.PlotTable))]
		//public List<FloatVariablePair> FloatVariables
		public List<float> FloatVariables
		{
			get { return _FloatVariables; }
			set
			{
				if (value != _FloatVariables)
				{
					_FloatVariables = value;
					NotifyPropertyChanged("FloatVariables");
				}
			}
		}

		[LocalizedDisplayName("QuestProgressCounter", typeof (Localization.PlotTable))]
		public int QuestProgressCounter
		{
			get { return _QuestProgressCounter; }
			set
			{
				if (value != _QuestProgressCounter)
				{
					_QuestProgressCounter = value;
					NotifyPropertyChanged("QuestProgressCounter");
				}
			}
		}

		[LocalizedDisplayName("QuestProgress", typeof (Localization.PlotTable))]
		public List<PlotQuest> QuestProgress
		{
			get { return _QuestProgress; }
			set
			{
				if (value != _QuestProgress)
				{
					_QuestProgress = value;
					NotifyPropertyChanged("QuestProgress");
				}
			}
		}

		[LocalizedDisplayName("QuestIDs", typeof (Localization.PlotTable))]
		public List<int> QuestIDs
		{
			get { return _QuestIDs; }
			set
			{
				if (value != _QuestIDs)
				{
					_QuestIDs = value;
					NotifyPropertyChanged("QuestIDs");
				}
			}
		}

		[LocalizedDisplayName("CodexEntries", typeof (Localization.PlotTable))]
		public List<PlotCodex> CodexEntries
		{
			get { return _CodexEntries; }
			set
			{
				if (value != _CodexEntries)
				{
					_CodexEntries = value;
					NotifyPropertyChanged("CodexEntries");
				}
			}
		}

		[LocalizedDisplayName("CodexIDs", typeof (Localization.PlotTable))]
		public List<int> CodexIDs
		{
			get { return _CodexIDs; }
			set
			{
				if (value != _CodexIDs)
				{
					_CodexIDs = value;
					NotifyPropertyChanged("CodexIDs");
				}
			}
		}

		#endregion

		#region PropertyChanged

		public event PropertyChangedEventHandler PropertyChanged;

		private void NotifyPropertyChanged(string propertyName)
		{
			if (PropertyChanged != null)
			{
				PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
			}
		}

		#endregion

		#region Children

		[OriginalName("PlotCodex")]
		public class PlotCodex : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Pages")]
			private List<PlotCodexPage> _Pages = new List<PlotCodexPage>();

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Pages);
			}

			#region Properties

			[LocalizedDisplayName("PlotCodex_Pages", typeof (Localization.PlotTable))]
			public List<PlotCodexPage> Pages
			{
				get { return _Pages; }
				set
				{
					if (value != _Pages)
					{
						_Pages = value;
						NotifyPropertyChanged("Pages");
					}
				}
			}

			#endregion

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}

			#region Children

			[OriginalName("PlotCodexPage")]
			public class PlotCodexPage : ISerializable, INotifyPropertyChanged
			{
				#region Fields

				[OriginalName("bNew")]
				private bool _IsNew;

				[OriginalName("Page")]
				private int _Page;

				#endregion

				public event PropertyChangedEventHandler PropertyChanged;

				public void Serialize(ISerializer stream)
				{
					stream.Serialize(ref _Page);
					stream.Serialize(ref _IsNew);
				}

				#region Properties

				[LocalizedDisplayName("PlotCodex_PlotCodexPage_Page", typeof (Localization.PlotTable))]
				public int Page
				{
					get { return _Page; }
					set
					{
						if (value != _Page)
						{
							_Page = value;
							NotifyPropertyChanged("Page");
						}
					}
				}

				[LocalizedDisplayName("PlotCodex_PlotCodexPage_IsNew", typeof (Localization.PlotTable))]
				public bool IsNew
				{
					get { return _IsNew; }
					set
					{
						if (value != _IsNew)
						{
							_IsNew = value;
							NotifyPropertyChanged("IsNew");
						}
					}
				}

				#endregion

				private void NotifyPropertyChanged(string propertyName)
				{
					if (PropertyChanged != null)
					{
						PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
					}
				}
			}

			#endregion
		}

		[OriginalName("PlotQuest")]
		public class PlotQuest : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			//[OriginalName("ActiveGoal")]
			//private int _ActiveGoal;

			[OriginalName("History")]
			private List<int> _History = new List<int>();

			[OriginalName("QuestCounter")]
			private int _QuestCounter;

			[OriginalName("QuestUpdated")]
			private bool _QuestUpdated;

			#endregion

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _QuestCounter);
				stream.Serialize(ref _QuestUpdated);
				stream.Serialize(ref _History);
			}

			#region Properties

			[LocalizedDisplayName("PlotQuest_QuestCounter", typeof (Localization.PlotTable))]
			public int QuestCounter
			{
				get { return _QuestCounter; }
				set
				{
					if (value != _QuestCounter)
					{
						_QuestCounter = value;
						NotifyPropertyChanged("QuestCounter");
					}
				}
			}

			[LocalizedDisplayName("PlotQuest_QuestUpdated", typeof (Localization.PlotTable))]
			public bool QuestUpdated
			{
				get { return _QuestUpdated; }
				set
				{
					if (value != _QuestUpdated)
					{
						_QuestUpdated = value;
						NotifyPropertyChanged("QuestUpdated");
					}
				}
			}

			/*[LocalizedDisplayName("PlotQuest_ActiveGoal", typeof (Localization.PlotTable))]
			public int ActiveGoal
			{
				get { return _ActiveGoal; }
				set
				{
					if (value != _ActiveGoal)
					{
						_ActiveGoal = value;
						NotifyPropertyChanged("ActiveGoal");
					}
				}
			}*/

			[LocalizedDisplayName("PlotQuest_History", typeof (Localization.PlotTable))]
			public List<int> History
			{
				get { return _History; }
				set
				{
					if (value != _History)
					{
						_History = value;
						NotifyPropertyChanged("History");
					}
				}
			}

			#endregion

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}
		}

		#endregion
	}
}