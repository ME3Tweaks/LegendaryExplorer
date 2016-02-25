using System.ComponentModel;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("AppearanceSaveRecord")]
	public class Appearance : ISerializable, INotifyPropertyChanged
	{
		#region Fields

		[OriginalName("ArmID")]
		private int _ArmId;

		[OriginalName("CasualID")]
		private int _CasualId;

		[OriginalName("CombatAppearance")]
		private PlayerAppearanceType _CombatAppearance;

		//[OriginalName("EmissiveID")]
		//private int _EmissiveId;

		[OriginalName("FullBodyID")]
		private int _FullBodyId;

		[OriginalName("bHasMorphHead")]
		private bool _HasMorphHead;

		[OriginalName("HelmetID")]
		private int _HelmetId;

		[OriginalName("LegID")]
		private int _LegId;

		[OriginalName("MorphHead")]
		private MorphHead _MorphHead = new MorphHead();

		[OriginalName("PatternColorID")]
		private int _PatternColorId;

		[OriginalName("PatternID")]
		private int _PatternId;

		[OriginalName("ShoulderID")]
		private int _ShoulderId;

		[OriginalName("SpecID")]
		private int _SpecId;

		[OriginalName("Tint1ID")]
		private int _Tint1Id;

		[OriginalName("Tint2ID")]
		private int _Tint2Id;

		[OriginalName("Tint3ID")]
		private int _Tint3Id;

		[OriginalName("TorsoID")]
		private int _TorsoId;

		#endregion

		public event PropertyChangedEventHandler PropertyChanged;

		public void Serialize(ISerializer stream)
		{
			stream.SerializeEnum(ref _CombatAppearance);
			stream.Serialize(ref _CasualId);
			stream.Serialize(ref _FullBodyId);
			stream.Serialize(ref _TorsoId);
			stream.Serialize(ref _ShoulderId);
			stream.Serialize(ref _ArmId);
			stream.Serialize(ref _LegId);
			stream.Serialize(ref _SpecId);
			stream.Serialize(ref _Tint1Id);
			stream.Serialize(ref _Tint2Id);
			stream.Serialize(ref _Tint3Id);
			stream.Serialize(ref _PatternId);
			stream.Serialize(ref _PatternColorId);
			stream.Serialize(ref _HelmetId);
			stream.Serialize(ref _HasMorphHead);

			if (_HasMorphHead)
			{
				stream.Serialize(ref _MorphHead);
			}

			//stream.Serialize(ref _EmissiveId, s => s.Version < 55, () => 0);
		}

		#region Properties

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("CombatAppearance", typeof (Localization.Appearance))]
		public PlayerAppearanceType CombatAppearance
		{
			get { return _CombatAppearance; }
			set
			{
				if (value != _CombatAppearance)
				{
					_CombatAppearance = value;
					NotifyPropertyChanged("CombatAppearance");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("CasualId", typeof (Localization.Appearance))]
		public int CasualId
		{
			get { return _CasualId; }
			set
			{
				if (value != _CasualId)
				{
					_CasualId = value;
					NotifyPropertyChanged("CasualId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("FullBodyId", typeof (Localization.Appearance))]
		public int FullBodyId
		{
			get { return _FullBodyId; }
			set
			{
				if (value != _FullBodyId)
				{
					_FullBodyId = value;
					NotifyPropertyChanged("FullBodyId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("TorsoId", typeof (Localization.Appearance))]
		public int TorsoId
		{
			get { return _TorsoId; }
			set
			{
				if (value != _TorsoId)
				{
					_TorsoId = value;
					NotifyPropertyChanged("TorsoId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("ShoulderId", typeof (Localization.Appearance))]
		public int ShoulderId
		{
			get { return _ShoulderId; }
			set
			{
				if (value != _ShoulderId)
				{
					_ShoulderId = value;
					NotifyPropertyChanged("ShoulderId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("ArmId", typeof (Localization.Appearance))]
		public int ArmId
		{
			get { return _ArmId; }
			set
			{
				if (value != _ArmId)
				{
					_ArmId = value;
					NotifyPropertyChanged("ArmId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("LegId", typeof (Localization.Appearance))]
		public int LegId
		{
			get { return _LegId; }
			set
			{
				if (value != _LegId)
				{
					_LegId = value;
					NotifyPropertyChanged("LegId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("SpecId", typeof (Localization.Appearance))]
		public int SpecId
		{
			get { return _SpecId; }
			set
			{
				if (value != _SpecId)
				{
					_SpecId = value;
					NotifyPropertyChanged("SpecId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("Tint1Id", typeof (Localization.Appearance))]
		public int Tint1Id
		{
			get { return _Tint1Id; }
			set
			{
				if (value != _Tint1Id)
				{
					_Tint1Id = value;
					NotifyPropertyChanged("Tint1Id");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("Tint2Id", typeof (Localization.Appearance))]
		public int Tint2Id
		{
			get { return _Tint2Id; }
			set
			{
				if (value != _Tint2Id)
				{
					_Tint2Id = value;
					NotifyPropertyChanged("Tint2Id");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("Tint3Id", typeof (Localization.Appearance))]
		public int Tint3Id
		{
			get { return _Tint3Id; }
			set
			{
				if (value != _Tint3Id)
				{
					_Tint3Id = value;
					NotifyPropertyChanged("Tint3Id");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("PatternId", typeof (Localization.Appearance))]
		public int PatternId
		{
			get { return _PatternId; }
			set
			{
				if (value != _PatternId)
				{
					_PatternId = value;
					NotifyPropertyChanged("PatternId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("PatternColorId", typeof (Localization.Appearance))]
		public int PatternColorId
		{
			get { return _PatternColorId; }
			set
			{
				if (value != _PatternColorId)
				{
					_PatternColorId = value;
					NotifyPropertyChanged("PatternColorId");
				}
			}
		}

		[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("HelmetId", typeof (Localization.Appearance))]
		public int HelmetId
		{
			get { return _HelmetId; }
			set
			{
				if (value != _HelmetId)
				{
					_HelmetId = value;
					NotifyPropertyChanged("HelmetId");
				}
			}
		}

		[LocalizedCategory(Categories.Head, typeof (Localization.Appearance))]
		[LocalizedDisplayName("HasMorphHead", typeof (Localization.Appearance))]
		public bool HasMorphHead
		{
			get { return _HasMorphHead; }
			set
			{
				if (value != _HasMorphHead)
				{
					_HasMorphHead = value;
					NotifyPropertyChanged("HasMorphHead");
				}
			}
		}

		[LocalizedCategory(Categories.Head, typeof (Localization.Appearance))]
		[LocalizedDisplayName("MorphHead", typeof (Localization.Appearance))]
		public MorphHead MorphHead
		{
			get { return _MorphHead; }
			set
			{
				if (value != _MorphHead)
				{
					_MorphHead = value;
					NotifyPropertyChanged("MorphHead");
				}
			}
		}

		/*[LocalizedCategory(Categories.Body, typeof (Localization.Appearance))]
		[LocalizedDisplayName("EmissiveId", typeof (Localization.Appearance))]
		public int EmissiveId
		{
			get { return _EmissiveId; }
			set
			{
				if (value != _EmissiveId)
				{
					_EmissiveId = value;
					NotifyPropertyChanged("EmissiveId");
				}
			}
		}*/

		private static class Categories
		{
			public const string Head = "Head";
			public const string Body = "Body";
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
}