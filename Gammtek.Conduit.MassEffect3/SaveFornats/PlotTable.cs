using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
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
		private List<FloatVariablePair> _FloatVariables = new List<FloatVariablePair>();

		[OriginalName("IntVariables")]
		private List<IntVariablePair> _IntVariables = new List<IntVariablePair>();

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
			var variable = _IntVariables
				.FirstOrDefault(v => v.Index == index);
			if (variable == null)
			{
				return 0;
			}
			return variable.Value;
		}

		public void SetIntVariable(int index, int value)
		{
			var targets = _IntVariables
				.Where(v => v.Index == index)
				.ToArray();

			if (targets.Length == 0)
			{
				_IntVariables.Add(new IntVariablePair
				{
					Index = index,
					Value = value,
				});
				return;
			}

			targets[0].Value = value;

			for (var i = 1; i < targets.Length; i++)
			{
				_IntVariables.Remove(targets[i]);
			}
		}

		public float GetFloatVariable(int index)
		{
			var variable = _FloatVariables
				.FirstOrDefault(v => v.Index == index);
			if (variable == null)
			{
				return 0;
			}
			return variable.Value;
		}

		public void SetFloatVariable(int index, float value)
		{
			var targets = _FloatVariables
				.Where(v => v.Index == index)
				.ToArray();

			if (targets.Length == 0)
			{
				_FloatVariables.Add(new FloatVariablePair
				{
					Index = index,
					Value = value,
				});
				return;
			}

			targets[0].Value = value;

			for (var i = 1; i < targets.Length; i++)
			{
				_FloatVariables.Remove(targets[i]);
			}
		}

		#endregion

		#region Serialize

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _BoolVariables);

			if (stream.Version >= 56)
			{
				stream.Serialize(ref _IntVariables);
				stream.Serialize(ref _FloatVariables);
			}
			else
			{
				if (stream.Mode == SerializeMode.Reading)
				{
					var oldIntVariables = new List<int>();
					stream.Serialize(ref oldIntVariables);
					_IntVariables = new List<IntVariablePair>();
					for (var i = 0; i < oldIntVariables.Count; i++)
					{
						if (oldIntVariables[i] == 0)
						{
							continue;
						}

						_IntVariables.Add(new IntVariablePair
						{
							Index = i,
							Value = oldIntVariables[i],
						});
					}

					var oldFloatVariables = new List<float>();
					stream.Serialize(ref oldFloatVariables);
					_FloatVariables = new List<FloatVariablePair>();
					for (var i = 0; i < oldFloatVariables.Count; i++)
					{
						if (Equals(oldFloatVariables[i], 0.0f))
						{
							continue;
						}

						_FloatVariables.Add(new FloatVariablePair
						{
							Index = i,
							Value = oldFloatVariables[i],
						});
					}
				}
				else if (stream.Mode == SerializeMode.Writing)
				{
					var oldIntVariables = new List<int>();
					if (_IntVariables != null)
					{
						foreach (var intVariable in _IntVariables)
						{
							oldIntVariables[intVariable.Index] = intVariable.Value;
						}
					}
					stream.Serialize(ref oldIntVariables);

					var oldFloatVariables = new List<float>();
					if (_FloatVariables != null)
					{
						foreach (var floatVariable in _FloatVariables)
						{
							oldFloatVariables[floatVariable.Index] = floatVariable.Value;
						}
					}
					stream.Serialize(ref oldFloatVariables);
				}
				else
				{
					throw new NotSupportedException();
				}
			}

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
		public List<IntVariablePair> IntVariables
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
		public List<FloatVariablePair> FloatVariables
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

		[OriginalName("FloatVariablePair")]
		public class FloatVariablePair : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Index")]
			private int _Index;

			[OriginalName("Value")]
			private float _Value;

			#endregion

			// for CollectionEditor
			[Browsable(false)]
			public string Name
			{
				get { return _Index.ToString(CultureInfo.InvariantCulture); }
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Index);
				stream.Serialize(ref _Value);
			}

			public override string ToString()
			{
				return Name ?? "(null)";
			}

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}

			#region Properties

			[LocalizedDisplayName("FloatVariablePair_Index", typeof (Localization.PlotTable))]
			public int Index
			{
				get { return _Index; }
				set
				{
					if (value != _Index)
					{
						_Index = value;
						NotifyPropertyChanged("Index");
					}
				}
			}

			[LocalizedDisplayName("FloatVariablePair_Value", typeof (Localization.PlotTable))]
			public float Value
			{
				get { return _Value; }
				set
				{
					if (Equals(value, _Value) == false)
					{
						_Value = value;
						NotifyPropertyChanged("Value");
					}
				}
			}

			#endregion
		}

		[OriginalName("IntVariablePair")]
		public class IntVariablePair : ISerializable, INotifyPropertyChanged
		{
			#region Fields

			[OriginalName("Index")]
			private int _Index;

			[OriginalName("Value")]
			private int _Value;

			#endregion

			// for CollectionEditor
			[Browsable(false)]
			public string Name
			{
				get { return _Index.ToString(CultureInfo.InvariantCulture); }
			}

			public event PropertyChangedEventHandler PropertyChanged;

			public void Serialize(ISerializer stream)
			{
				stream.Serialize(ref _Index);
				stream.Serialize(ref _Value);
			}

			public override string ToString()
			{
				return Name ?? "(null)";
			}

			private void NotifyPropertyChanged(string propertyName)
			{
				if (PropertyChanged != null)
				{
					PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
				}
			}

			#region Properties

			[LocalizedDisplayName("IntVariablePair_Index", typeof (Localization.PlotTable))]
			public int Index
			{
				get { return _Index; }
				set
				{
					if (value != _Index)
					{
						_Index = value;
						NotifyPropertyChanged("Index");
					}
				}
			}

			[LocalizedDisplayName("IntVariablePair_Value", typeof (Localization.PlotTable))]
			public int Value
			{
				get { return _Value; }
				set
				{
					if (value != _Value)
					{
						_Value = value;
						NotifyPropertyChanged("Value");
					}
				}
			}

			#endregion
		}

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

			[OriginalName("ActiveGoal")]
			private int _ActiveGoal;

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
				stream.Serialize(ref _ActiveGoal, s => s.Version < 57, () => 0);
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

			[LocalizedDisplayName("PlotQuest_ActiveGoal", typeof (Localization.PlotTable))]
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
			}

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