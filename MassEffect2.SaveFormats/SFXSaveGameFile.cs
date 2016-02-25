using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Gammtek.Conduit.Extensions;
using Gammtek.Conduit.Extensions.IO;
using Gammtek.Conduit.IO;
using MassEffect3.FileFormats;
using MassEffect3.FileFormats.Unreal;

namespace MassEffect2.SaveFormats
{
	[TypeConverter(typeof (ExpandableObjectConverter))]
	[OriginalName("SFXSaveGame")]
	public class SFXSaveGameFile : ISerializable, INotifyPropertyChanged
	{
		private uint _checksum;
		private ByteOrder _endian;
		private uint _version;

		#region Fields

		[OriginalName("BaseLevelName")]
		private string _baseLevelName;

		//[OriginalName("BaseLevelNameDisplayOverrideAsRead")]
		//private string _baseLevelNameDisplayOverrideAsRead;

		//[OriginalName("ConversationMode")]
		//private AutoReplyModeOptions _conversationMode;

		[OriginalName("CurrentLoadingTip")]
		private int _currentLoadingTip;

		[OriginalName("DebugName")]
		private string _debugName;

		[OriginalName("DependentDLC")]
		private List<DependentDLC> _dependentDlc = new List<DependentDLC>();

		[OriginalName("Difficulty")]
		private DifficultyOptions _difficulty;

		[OriginalName("Disc")]
		private int _disc;

		[OriginalName("DoorRecords")]
		private List<Door> _doors = new List<Door>();

		[OriginalName("EndGameState")]
		private EndGameState _endGameState;

		[OriginalName("GalaxyMapRecord")]
		private GalaxyMap _galaxyMap = new GalaxyMap();

		[OriginalName("HenchmanRecords")]
		private List<Henchman> _henchmen = new List<Henchman>();

		[OriginalName("KismetRecords")]
		private List<KismetBool> _kismetRecords = new List<KismetBool>();

		[OriginalName("LevelRecords")]
		private List<Level> _levels = new List<Level>();

		[OriginalName("SaveLocation")]
		private Vector _location = new Vector();

		[OriginalName("ME1PlotRecord")]
		private ME1PlotTable _me1Plot = new ME1PlotTable();

		//[OriginalName("ObjectiveMarkerRecords")]
		//private List<ObjectiveMarker> _objectiveMarkers = new List<ObjectiveMarker>();

		[OriginalName("PawnRecords")]
		private List<Guid> _pawns = new List<Guid>();

		//[OriginalName("PlaceableRecords")]
		//private List<Placeable> _placeables = new List<Placeable>();

		[OriginalName("PlayerRecord")]
		private Player _player = new Player();

		//[OriginalName("PlayerVariableRecords")]
		//private List<PlayerVariable> _playerVariables = new List<PlayerVariable>();

		[OriginalName("PlotRecord")]
		private PlotTable _plot = new PlotTable();

		[OriginalName("SaveRotation")]
		private Rotator _rotation = new Rotator();

		[OriginalName("SavedObjectiveText")]
		private int _savedObjectiveText;

		[OriginalName("SecondsPlayed")]
		private float _secondsPlayed;

		[OriginalName("StreamingRecords")]
		private List<StreamingState> _streamingRecords = new List<StreamingState>();

		[OriginalName("TimeStamp")]
		private SaveTimeStamp _timeStamp = new SaveTimeStamp();

		//[OriginalName("TreasureRecords")]
		//private List<LevelTreasure> _treasures = new List<LevelTreasure>();

		//[OriginalName("UseModuleRecords")]
		//private List<Guid> _useModules = new List<Guid>();

		#endregion

		public void Serialize(ISerializer stream)
		{
			stream.Serialize(ref _debugName);
			stream.Serialize(ref _secondsPlayed);
			stream.Serialize(ref _disc);
			stream.Serialize(ref _baseLevelName);
			stream.SerializeEnum(ref _difficulty);
			stream.SerializeEnum(ref _endGameState);
			stream.Serialize(ref _timeStamp);
			stream.Serialize(ref _location);
			stream.Serialize(ref _rotation);
			stream.Serialize(ref _currentLoadingTip);
			stream.Serialize(ref _levels);
			stream.Serialize(ref _streamingRecords);
			stream.Serialize(ref _kismetRecords);
			stream.Serialize(ref _doors);
			stream.Serialize(ref _pawns);
			stream.Serialize(ref _player);
			stream.Serialize(ref _henchmen);
			stream.Serialize(ref _plot);
			stream.Serialize(ref _me1Plot);
			stream.Serialize(ref _galaxyMap);
			stream.Serialize(ref _dependentDlc);
		}

		#region Properties

		[Browsable(false)]
		public ByteOrder Endian
		{
			get { return _endian; }
			set
			{
				if (value != _endian)
				{
					_endian = value;
					NotifyPropertyChanged("Endian");
				}
			}
		}

		[Browsable(false)]
		public uint Version
		{
			get { return _version; }
			set
			{
				if (value != _version)
				{
					_version = value;
					NotifyPropertyChanged("Version");
				}
			}
		}

		[Browsable(false)]
		public uint Checksum
		{
			get { return _checksum; }
			set
			{
				if (value != _checksum)
				{
					_checksum = value;
					NotifyPropertyChanged("Checksum");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("DebugName", typeof (Localization.SFXSaveGameFile))]
		public string DebugName
		{
			get { return _debugName; }
			set
			{
				if (value != _debugName)
				{
					_debugName = value;
					NotifyPropertyChanged("DebugName");
				}
			}
		}

		[LocalizedCategory(Categories.Basic, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("SecondsPlayed", typeof (Localization.SFXSaveGameFile))]
		public float SecondsPlayed
		{
			get { return _secondsPlayed; }
			set
			{
				if (Equals(value, _secondsPlayed) == false)
				{
					_secondsPlayed = value;
					NotifyPropertyChanged("SecondsPlayed");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Disc", typeof (Localization.SFXSaveGameFile))]
		public int Disc
		{
			get { return _disc; }
			set
			{
				if (value != _disc)
				{
					_disc = value;
					NotifyPropertyChanged("Disc");
				}
			}
		}

		[LocalizedCategory(Categories.Location, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("BaseLevelName", typeof (Localization.SFXSaveGameFile))]
		public string BaseLevelName
		{
			get { return _baseLevelName; }
			set
			{
				if (value != _baseLevelName)
				{
					_baseLevelName = value;
					NotifyPropertyChanged("BaseLevelName");
				}
			}
		}

		/*[LocalizedCategory(Categories.Location, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("BaseLevelNameDisplayOverrideAsRead", typeof (Localization.SFXSaveGameFile))]
		public string BaseLevelNameDisplayOverrideAsRead
		{
			get { return _baseLevelNameDisplayOverrideAsRead; }
			set
			{
				if (value != _baseLevelNameDisplayOverrideAsRead)
				{
					_baseLevelNameDisplayOverrideAsRead = value;
					NotifyPropertyChanged("BaseLevelNameDisplayOverrideAsRead");
				}
			}
		}*/

		[LocalizedCategory(Categories.Basic, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Difficulty", typeof (Localization.SFXSaveGameFile))]
		public DifficultyOptions Difficulty
		{
			get { return _difficulty; }
			set
			{
				if (value != _difficulty)
				{
					_difficulty = value;
					NotifyPropertyChanged("Difficulty");
				}
			}
		}

		[LocalizedCategory(Categories.Basic, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("EndGameState", typeof (Localization.SFXSaveGameFile))]
		[Description(
			"Note: this value was re-used from Mass Effect 2, and the value of 'LivedToFightAgain' is what indicates that the save can be imported. It has nothing to do with your ending of Mass Effect 3."
			)]
		public EndGameState EndGameState
		{
			get { return _endGameState; }
			set
			{
				if (value != _endGameState)
				{
					_endGameState = value;
					NotifyPropertyChanged("EndGameState");
				}
			}
		}

		[LocalizedCategory(Categories.Basic, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("TimeStamp", typeof (Localization.SFXSaveGameFile))]
		public SaveTimeStamp TimeStamp
		{
			get { return _timeStamp; }
			set
			{
				if (value != _timeStamp)
				{
					_timeStamp = value;
					NotifyPropertyChanged("TimeStamp");
				}
			}
		}

		[LocalizedCategory(Categories.Location, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Location", typeof (Localization.SFXSaveGameFile))]
		public Vector Location
		{
			get { return _location; }
			set
			{
				if (value != _location)
				{
					_location = value;
					NotifyPropertyChanged("Location");
				}
			}
		}

		[LocalizedCategory(Categories.Location, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Rotation", typeof (Localization.SFXSaveGameFile))]
		public Rotator Rotation
		{
			get { return _rotation; }
			set
			{
				if (value != _rotation)
				{
					_rotation = value;
					NotifyPropertyChanged("Rotation");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("CurrentLoadingTip", typeof (Localization.SFXSaveGameFile))]
		public int CurrentLoadingTip
		{
			get { return _currentLoadingTip; }
			set
			{
				if (value != _currentLoadingTip)
				{
					_currentLoadingTip = value;
					NotifyPropertyChanged("CurrentLoadingTip");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Levels", typeof (Localization.SFXSaveGameFile))]
		public List<Level> Levels
		{
			get { return _levels; }
			set
			{
				if (value != _levels)
				{
					_levels = value;
					NotifyPropertyChanged("Levels");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("StreamingRecords", typeof (Localization.SFXSaveGameFile))]
		public List<StreamingState> StreamingRecords
		{
			get { return _streamingRecords; }
			set
			{
				if (value != _streamingRecords)
				{
					_streamingRecords = value;
					NotifyPropertyChanged("StreamingRecords");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("KismetRecords", typeof (Localization.SFXSaveGameFile))]
		public List<KismetBool> KismetRecords
		{
			get { return _kismetRecords; }
			set
			{
				if (value != _kismetRecords)
				{
					_kismetRecords = value;
					NotifyPropertyChanged("KismetRecords");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Doors", typeof (Localization.SFXSaveGameFile))]
		public List<Door> Doors
		{
			get { return _doors; }
			set
			{
				if (value != _doors)
				{
					_doors = value;
					NotifyPropertyChanged("Doors");
				}
			}
		}

		/*[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Placeables", typeof (Localization.SFXSaveGameFile))]
		public List<Placeable> Placeables
		{
			get { return _placeables; }
			set
			{
				if (value != _placeables)
				{
					_placeables = value;
					NotifyPropertyChanged("Placeables");
				}
			}
		}*/

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Pawns", typeof (Localization.SFXSaveGameFile))]
		public List<Guid> Pawns
		{
			get { return _pawns; }
			set
			{
				if (value != _pawns)
				{
					_pawns = value;
					NotifyPropertyChanged("Pawns");
				}
			}
		}

		[LocalizedCategory(Categories.Squad, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Player", typeof (Localization.SFXSaveGameFile))]
		public Player Player
		{
			get { return _player; }
			set
			{
				if (value != _player)
				{
					_player = value;
					NotifyPropertyChanged("Player");
				}
			}
		}

		[LocalizedCategory(Categories.Squad, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Henchmen", typeof (Localization.SFXSaveGameFile))]
		public List<Henchman> Henchmen
		{
			get { return _henchmen; }
			set
			{
				if (value != _henchmen)
				{
					_henchmen = value;
					NotifyPropertyChanged("Henchmen");
				}
			}
		}

		[LocalizedCategory(Categories.Plot, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Plot", typeof (Localization.SFXSaveGameFile))]
		public PlotTable Plot
		{
			get { return _plot; }
			set
			{
				if (value != _plot)
				{
					_plot = value;
					NotifyPropertyChanged("Plot");
				}
			}
		}

		[Browsable(false)]
		[LocalizedCategory(Categories.Plot, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("ME1Plot", typeof (Localization.SFXSaveGameFile))]
		// ReSharper disable InconsistentNaming
		public ME1PlotTable ME1Plot
			// ReSharper restore InconsistentNaming
		{
			get { return _me1Plot; }
			set
			{
				if (value != _me1Plot)
				{
					_me1Plot = value;
					NotifyPropertyChanged("ME1Plot");
				}
			}
		}

		/*[LocalizedCategory(Categories.Plot, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("PlayerVariables", typeof (Localization.SFXSaveGameFile))]
		public List<PlayerVariable> PlayerVariables
		{
			get { return _playerVariables; }
			set
			{
				if (value != _playerVariables)
				{
					_playerVariables = value;
					NotifyPropertyChanged("PlayerVariables");
				}
			}
		}*/

		[LocalizedCategory(Categories.Plot, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("GalaxyMap", typeof (Localization.SFXSaveGameFile))]
		public GalaxyMap GalaxyMap
		{
			get { return _galaxyMap; }
			set
			{
				if (value != _galaxyMap)
				{
					_galaxyMap = value;
					NotifyPropertyChanged("GalaxyMap");
				}
			}
		}

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("DependentDLC", typeof (Localization.SFXSaveGameFile))]
		public List<DependentDLC> DependentDLC
		{
			get { return _dependentDlc; }
			set
			{
				if (value != _dependentDlc)
				{
					_dependentDlc = value;
					NotifyPropertyChanged("DependentDLC");
				}
			}
		}

		/*[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("Treasures", typeof (Localization.SFXSaveGameFile))]
		public List<LevelTreasure> Treasures
		{
			get { return _treasures; }
			set
			{
				if (value != _treasures)
				{
					_treasures = value;
					NotifyPropertyChanged("Treasures");
				}
			}
		}*/

		/*[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("UseModules", typeof (Localization.SFXSaveGameFile))]
		public List<Guid> UseModules
		{
			get { return _useModules; }
			set
			{
				if (value != _useModules)
				{
					_useModules = value;
					NotifyPropertyChanged("UseModules");
				}
			}
		}*/

		/*[LocalizedCategory(Categories.Basic, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("ConversationMode", typeof (Localization.SFXSaveGameFile))]
		public AutoReplyModeOptions ConversationMode
		{
			get { return _conversationMode; }
			set
			{
				if (value != _conversationMode)
				{
					_conversationMode = value;
					NotifyPropertyChanged("ConversationMode");
				}
			}
		}*/

		/*[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("ObjectiveMarkers", typeof (Localization.SFXSaveGameFile))]
		public List<ObjectiveMarker> ObjectiveMarkers
		{
			get { return _objectiveMarkers; }
			set
			{
				if (value != _objectiveMarkers)
				{
					_objectiveMarkers = value;
					NotifyPropertyChanged("ObjectiveMarkers");
				}
			}
		}*/

		[LocalizedCategory(Categories.Uncategorized, typeof (Localization.SFXSaveGameFile))]
		[LocalizedDisplayName("SavedObjectiveText", typeof (Localization.SFXSaveGameFile))]
		public int SavedObjectiveText
		{
			get { return _savedObjectiveText; }
			set
			{
				if (value != _savedObjectiveText)
				{
					_savedObjectiveText = value;
					NotifyPropertyChanged("SavedObjectiveText");
				}
			}
		}

		private static class Categories
		{
			public const string Basic = "Basic";
			public const string Location = "Location";
			public const string Squad = "Squad";
			public const string Plot = "Plot";
			public const string Uncategorized = "Uncategorized";
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

		public static SFXSaveGameFile Read(Stream input)
		{
			if (input == null)
			{
				throw new ArgumentNullException("input");
			}

			var save = new SFXSaveGameFile
			{
				_version = input.ReadUInt32()
			};

			/*if (save._version != 29 && save._version.Swap() != 29 &&
				save._version != 59 && save._version.Swap() != 59)*/
			if (save._version != 29 && save._version.Swap() != 29)
			{
				throw new FormatException("unexpected version");
			}

			//var endian = save._version == 29 || save._version == 59
			var endian = save._version == 29 ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

			if (endian == ByteOrder.BigEndian)
			{
				save._version = save._version.Swap();
			}

			var reader = new FileReader(input, save._version, endian);
			save.Serialize(reader);

			if (save._version >= 27)
			{
				if (input.Position != input.Length - 4)
				{
					throw new FormatException("bad checksum position");
				}

				save._checksum = input.ReadUInt32();
			}

			if (input.Position != input.Length)
			{
				throw new FormatException("did not consume entire file");
			}

			save.Endian = endian;

			return save;
		}

		public static void Write(SFXSaveGameFile save, Stream output)
		{
			if (save == null)
			{
				throw new ArgumentNullException("save");
			}

			if (output == null)
			{
				throw new ArgumentNullException("output");
			}

			using (var memory = new MemoryStream())
			{
				memory.WriteUInt32(save.Version, save._endian);

				var writer = new FileWriter(memory, save._version, save._endian);
				save.Serialize(writer);

				if (save._version >= 27)
				{
					memory.Position = 0;
					uint checksum = 0;

					var buffer = new byte[1024];

					while (memory.Position < memory.Length)
					{
						var read = memory.Read(buffer, 0, 1024);
						checksum = Crc32.Compute(buffer, 0, read, checksum);
					}

					save._checksum = checksum;
					memory.WriteUInt32(checksum, save._endian);
				}

				memory.Position = 0;
				output.WriteFromStream(memory, memory.Length);
			}
		}
	}
}