using System;
using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;
using Localization = Gammtek.Conduit.MassEffect3.SaveFornats.Localization;

namespace MassEffect3.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	public class PlotTableWrapper : INotifyPropertyChanged, ISerializable
	{
		private readonly IPlotTable _Target;

		public PlotTableWrapper(IPlotTable target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target));
			}

			_Target = target;
		}

		#region Properties

		private const int PersuadeMultiplierId = 10065;

		private const int NewGamePlusCountId = 10475;

		private const int ParagonPointsId = 10159;

		private const int RenegadePointsId = 10160;

		private const int ReputationId = 10297;

		private const int ExtraMedigelId = 10300;

		private const int ReputationPointsId = 10380;

		private const int IsMe2ImportId = 21554;

		private const int IsMe1ImportId = 22226;

		private const int CosmeticSurgeryMe2Id = 5978;

		private const int CosmeticSurgeryMe3Id = 22642;

		[LocalizedDisplayName("PersuadeMultiplier", typeof (Localization.PlotTableWrapper))]
		public float PersuadeMultiplier
		{
			get { return _Target.GetFloatVariable(PersuadeMultiplierId); }
			set
			{
				if (Equals(_Target.GetFloatVariable(PersuadeMultiplierId), value) == false)
				{
					_Target.SetFloatVariable(PersuadeMultiplierId, value);
					NotifyPropertyChanged("PersuadeMultiplier");
				}
			}
		}

		[LocalizedDisplayName("NewGamePlusCount", typeof (Localization.PlotTableWrapper))]
		public int NewGamePlusCount
		{
			get { return _Target.GetIntVariable(NewGamePlusCountId); }
			set
			{
				if (_Target.GetIntVariable(NewGamePlusCountId) != value)
				{
					_Target.SetIntVariable(NewGamePlusCountId, value);
					NotifyPropertyChanged("NewGamePlusCount");
				}
			}
		}

		[LocalizedDisplayName("ParagonPoints", typeof (Localization.PlotTableWrapper))]
		public int ParagonPoints
		{
			get { return _Target.GetIntVariable(ParagonPointsId); }
			set
			{
				if (_Target.GetIntVariable(ParagonPointsId) != value)
				{
					_Target.SetIntVariable(ParagonPointsId, value);
					NotifyPropertyChanged("ParagonPoints");
				}
			}
		}

		[LocalizedDisplayName("RenegadePoints", typeof (Localization.PlotTableWrapper))]
		public int RenegadePoints
		{
			get { return _Target.GetIntVariable(RenegadePointsId); }
			set
			{
				if (_Target.GetIntVariable(RenegadePointsId) != value)
				{
					_Target.SetIntVariable(RenegadePointsId, value);
					NotifyPropertyChanged("RenegadePoints");
				}
			}
		}

		[LocalizedDisplayName("Reputation", typeof (Localization.PlotTableWrapper))]
		public int Reputation
		{
			get { return _Target.GetIntVariable(ReputationId); }
			set
			{
				if (_Target.GetIntVariable(ReputationId) != value)
				{
					_Target.SetIntVariable(ReputationId, value);
					NotifyPropertyChanged("Reputation");
				}
			}
		}

		[LocalizedDisplayName("ExtraMedigel", typeof (Localization.PlotTableWrapper))]
		public int ExtraMedigel
		{
			get { return _Target.GetIntVariable(ExtraMedigelId); }
			set
			{
				if (_Target.GetIntVariable(ExtraMedigelId) != value)
				{
					_Target.SetIntVariable(ExtraMedigelId, value);
					NotifyPropertyChanged("ExtraMedigel");
				}
			}
		}

		[LocalizedDisplayName("ReputationPoints", typeof (Localization.PlotTableWrapper))]
		public int ReputationPoints
		{
			get { return _Target.GetIntVariable(ReputationPointsId); }
			set
			{
				if (_Target.GetIntVariable(ReputationPointsId) != value)
				{
					_Target.SetIntVariable(ReputationPointsId, value);
					NotifyPropertyChanged("ReputationPoints");
				}
			}
		}

		[LocalizedDisplayName("IsMe2Import", typeof (Localization.PlotTableWrapper))]
		public bool IsMe2Import
		{
			get { return _Target.GetBoolVariable(IsMe2ImportId); }
			set
			{
				if (_Target.GetBoolVariable(IsMe2ImportId) != value)
				{
					_Target.SetBoolVariable(IsMe2ImportId, value);
					NotifyPropertyChanged("IsMe2Import");
				}
			}
		}

		[LocalizedDisplayName("IsMe1Import", typeof (Localization.PlotTableWrapper))]
		public bool IsMe1Import
		{
			get { return _Target.GetBoolVariable(IsMe1ImportId); }
			set
			{
				if (_Target.GetBoolVariable(IsMe1ImportId) != value)
				{
					_Target.SetBoolVariable(IsMe1ImportId, value);
					NotifyPropertyChanged("IsMe1Import");
				}
			}
		}

		[LocalizedDisplayName("CosmeticSurgeryMe2", typeof (Localization.PlotTableWrapper))]
		public bool CosmeticSurgeryMe2
		{
			get { return _Target.GetBoolVariable(CosmeticSurgeryMe2Id); }
			set
			{
				if (_Target.GetBoolVariable(CosmeticSurgeryMe2Id) != value)
				{
					_Target.SetBoolVariable(CosmeticSurgeryMe2Id, value);
					NotifyPropertyChanged("CosmeticSurgeryMe2");
				}
			}
		}

		[LocalizedDisplayName("CosmeticSurgeryMe3", typeof (Localization.PlotTableWrapper))]
		public bool CosmeticSurgeryMe3
		{
			get { return _Target.GetBoolVariable(CosmeticSurgeryMe3Id); }
			set
			{
				if (_Target.GetBoolVariable(CosmeticSurgeryMe3Id) != value)
				{
					_Target.SetBoolVariable(CosmeticSurgeryMe3Id, value);
					NotifyPropertyChanged("CosmeticSurgeryMe3");
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

		// for the propertygrid stuff
		public void Serialize(ISerializer stream)
		{
			throw new NotSupportedException();
		}
	}
}