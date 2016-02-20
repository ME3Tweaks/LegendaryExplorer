/* Copyright (c) 2012 Rick (rick 'at' gibbed 'dot' us)
 * 
 * This software is provided 'as-is', without any express or implied
 * warranty. In no event will the authors be held liable for any damages
 * arising from the use of this software.
 * 
 * Permission is granted to anyone to use this software for any purpose,
 * including commercial applications, and to alter it and redistribute it
 * freely, subject to the following restrictions:
 * 
 * 1. The origin of this software must not be misrepresented; you must not
 *    claim that you wrote the original software. If you use this software
 *    in a product, an acknowledgment in the product documentation would
 *    be appreciated but is not required.
 * 
 * 2. Altered source versions must be plainly marked as such, and must not
 *    be misrepresented as being the original software.
 * 
 * 3. This notice may not be removed or altered from any source
 *    distribution.
 */

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using Gibbed.IO;
using IdAttribute = DynamicTypeDescriptor.IdAttribute;

namespace Gibbed.MassEffect3.FileFormats
{
    [TypeConverter(typeof(ExpandableObjectConverter))]
    [Save.OriginalName("SFXSaveGame")]
    public class SaveFile : Unreal.ISerializable, INotifyPropertyChanged
    {
        private Endian _Endian;
        private uint _Version;
        private uint _Checksum;

        #region Fields
        [Save.OriginalName("DebugName")]
        private string _DebugName;

        [Save.OriginalName("SecondsPlayed")]
        private float _SecondsPlayed;

        [Save.OriginalName("Disc")]
        private int _Disc;

        [Save.OriginalName("BaseLevelName")]
        private string _BaseLevelName;

        [Save.OriginalName("BaseLevelNameDisplayOverrideAsRead")]
        private string _BaseLevelNameDisplayOverrideAsRead;

        [Save.OriginalName("Difficulty")]
        private Save.DifficultyOptions _Difficulty;

        [Save.OriginalName("EndGameState")]
        private Save.EndGameState _EndGameState;

        [Save.OriginalName("TimeStamp")]
        private Save.SaveTimeStamp _TimeStamp = new Save.SaveTimeStamp();

        [Save.OriginalName("SaveLocation")]
        private Save.Vector _Location = new Save.Vector();

        [Save.OriginalName("SaveRotation")]
        private Save.Rotator _Rotation = new Save.Rotator();

        [Save.OriginalName("CurrentLoadingTip")]
        private int _CurrentLoadingTip;

        [Save.OriginalName("LevelRecords")]
        private List<Save.Level> _Levels = new List<Save.Level>();

        [Save.OriginalName("StreamingRecords")]
        private List<Save.StreamingState> _StreamingRecords = new List<Save.StreamingState>();

        [Save.OriginalName("KismetRecords")]
        private List<Save.KismetBool> _KismetRecords = new List<Save.KismetBool>();

        [Save.OriginalName("DoorRecords")]
        private List<Save.Door> _Doors = new List<Save.Door>();

        [Save.OriginalName("PlaceableRecords")]
        private List<Save.Placeable> _Placeables = new List<Save.Placeable>();

        [Save.OriginalName("PawnRecords")]
        private List<Guid> _Pawns = new List<Guid>();

        [Save.OriginalName("PlayerRecord")]
        private Save.Player _Player = new Save.Player();

        [Save.OriginalName("HenchmanRecords")]
        private List<Save.Henchman> _Henchmen = new List<Save.Henchman>();

        [Save.OriginalName("PlotRecord")]
        private Save.PlotTable _Plot = new Save.PlotTable();

        [Save.OriginalName("ME1PlotRecord")]
        private Save.ME1PlotTable _ME1Plot = new Save.ME1PlotTable();

        [Save.OriginalName("PlayerVariableRecords")]
        private List<Save.PlayerVariable> _PlayerVariables = new List<Save.PlayerVariable>();

        [Save.OriginalName("GalaxyMapRecord")]
        private Save.GalaxyMap _GalaxyMap = new Save.GalaxyMap();

        [Save.OriginalName("DependentDLC")]
        private List<Save.DependentDLC> _DependentDLC = new List<Save.DependentDLC>();

        [Save.OriginalName("TreasureRecords")]
        private List<Save.LevelTreasure> _Treasures = new List<Save.LevelTreasure>();

        [Save.OriginalName("UseModuleRecords")]
        private List<Guid> _UseModules = new List<Guid>();

        [Save.OriginalName("ConversationMode")]
        private Save.AutoReplyModeOptions _ConversationMode;

        [Save.OriginalName("ObjectiveMarkerRecords")]
        private List<Save.ObjectiveMarker> _ObjectiveMarkers = new List<Save.ObjectiveMarker>();

        [Save.OriginalName("SavedObjectiveText")]
        private int _SavedObjectiveText;
        #endregion

        public void Serialize(Unreal.ISerializer stream)
        {
            stream.Serialize(ref this._DebugName);
            stream.Serialize(ref this._SecondsPlayed);
            stream.Serialize(ref this._Disc);
            stream.Serialize(ref this._BaseLevelName);
            stream.Serialize(ref this._BaseLevelNameDisplayOverrideAsRead, s => s.Version < 36, () => "None");
            stream.SerializeEnum(ref this._Difficulty);

            if (stream.Version >= 43 && stream.Version <= 46)
            {
                byte unknown = 0;
                stream.Serialize(ref unknown);
            }

            stream.SerializeEnum(ref this._EndGameState);
            stream.Serialize(ref this._TimeStamp);
            stream.Serialize(ref this._Location);
            stream.Serialize(ref this._Rotation);
            stream.Serialize(ref this._CurrentLoadingTip);
            stream.Serialize(ref this._Levels);
            stream.Serialize(ref this._StreamingRecords);
            stream.Serialize(ref this._KismetRecords);
            stream.Serialize(ref this._Doors);
            stream.Serialize(ref this._Placeables, s => s.Version < 46, () => new List<Save.Placeable>());
            stream.Serialize(ref this._Pawns);
            stream.Serialize(ref this._Player);
            stream.Serialize(ref this._Henchmen);
            stream.Serialize(ref this._Plot);
            stream.Serialize(ref this._ME1Plot);
            stream.Serialize(ref this._PlayerVariables, s => s.Version < 34, () => new List<Save.PlayerVariable>());
            stream.Serialize(ref this._GalaxyMap);
            stream.Serialize(ref this._DependentDLC);
            stream.Serialize(ref this._Treasures, s => s.Version < 35, () => new List<Save.LevelTreasure>());
            stream.Serialize(ref this._UseModules, s => s.Version < 39, () => new List<Guid>());
            stream.SerializeEnum(ref this._ConversationMode,
                                 s => s.Version < 49,
                                 () => Save.AutoReplyModeOptions.AllDecisions);
            stream.Serialize(ref this._ObjectiveMarkers, s => s.Version < 52, () => new List<Save.ObjectiveMarker>());
            stream.Serialize(ref this._SavedObjectiveText, s => s.Version < 52, () => 0);
        }

        #region Properties
        [Browsable(false)]
        public Endian Endian
        {
            get { return this._Endian; }
            set
            {
                if (value != this._Endian)
                {
                    this._Endian = value;
                    this.NotifyPropertyChanged("Endian");
                }
            }
        }

        [Browsable(false)]
        public uint Version
        {
            get { return this._Version; }
            set
            {
                if (value != this._Version)
                {
                    this._Version = value;
                    this.NotifyPropertyChanged("Version");
                }
            }
        }

        [Browsable(false)]
        public uint Checksum
        {
            get { return this._Checksum; }
            set
            {
                if (value != this._Checksum)
                {
                    this._Checksum = value;
                    this.NotifyPropertyChanged("Checksum");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Debug Name")]
        public string DebugName
        {
            get { return this._DebugName; }
            set
            {
                if (value != this._DebugName)
                {
                    this._DebugName = value;
                    this.NotifyPropertyChanged("DebugName");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("Seconds Played")]
        public float SecondsPlayed
        {
            get { return this._SecondsPlayed; }
            set
            {
                if (Equals(value, this._SecondsPlayed) == false)
                {
                    this._SecondsPlayed = value;
                    this.NotifyPropertyChanged("SecondsPlayed");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("Disc")]
        public int Disc
        {
            get { return this._Disc; }
            set
            {
                if (value != this._Disc)
                {
                    this._Disc = value;
                    this.NotifyPropertyChanged("Disc");
                }
            }
        }

        [Category("Location")]
        [DisplayName("Base Level Name")]
        public string BaseLevelName
        {
            get { return this._BaseLevelName; }
            set
            {
                if (value != this._BaseLevelName)
                {
                    this._BaseLevelName = value;
                    this.NotifyPropertyChanged("BaseLevelName");
                }
            }
        }

        [Category("Location")]
        [DisplayName("Base Level Name Display Override As Read")]
        public string BaseLevelNameDisplayOverrideAsRead
        {
            get { return this._BaseLevelNameDisplayOverrideAsRead; }
            set
            {
                if (value != this._BaseLevelNameDisplayOverrideAsRead)
                {
                    this._BaseLevelNameDisplayOverrideAsRead = value;
                    this.NotifyPropertyChanged("BaseLevelNameDisplayOverrideAsRead");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("Difficulty")]
        public Save.DifficultyOptions Difficulty
        {
            get { return this._Difficulty; }
            set
            {
                if (value != this._Difficulty)
                {
                    this._Difficulty = value;
                    this.NotifyPropertyChanged("Difficulty");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("End Game State")]
        public Save.EndGameState EndGameState
        {
            get { return this._EndGameState; }
            set
            {
                if (value != this._EndGameState)
                {
                    this._EndGameState = value;
                    this.NotifyPropertyChanged("EndGameState");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("Timestamp")]
        public Save.SaveTimeStamp TimeStamp
        {
            get { return this._TimeStamp; }
            set
            {
                if (value != this._TimeStamp)
                {
                    this._TimeStamp = value;
                    this.NotifyPropertyChanged("TimeStamp");
                }
            }
        }

        [Category("Location")]
        [DisplayName("Position")]
        public Save.Vector Location
        {
            get { return this._Location; }
            set
            {
                if (value != this._Location)
                {
                    this._Location = value;
                    this.NotifyPropertyChanged("Location");
                }
            }
        }

        [Category("Location")]
        [DisplayName("Rotation")]
        public Save.Rotator Rotation
        {
            get { return this._Rotation; }
            set
            {
                if (value != this._Rotation)
                {
                    this._Rotation = value;
                    this.NotifyPropertyChanged("Rotation");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Current Loading Tip")]
        public int CurrentLoadingTip
        {
            get { return this._CurrentLoadingTip; }
            set
            {
                if (value != this._CurrentLoadingTip)
                {
                    this._CurrentLoadingTip = value;
                    this.NotifyPropertyChanged("CurrentLoadingTip");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Levels")]
        public List<Save.Level> Levels
        {
            get { return this._Levels; }
            set
            {
                if (value != this._Levels)
                {
                    this._Levels = value;
                    this.NotifyPropertyChanged("Levels");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Streaming Records")]
        public List<Save.StreamingState> StreamingRecords
        {
            get { return this._StreamingRecords; }
            set
            {
                if (value != this._StreamingRecords)
                {
                    this._StreamingRecords = value;
                    this.NotifyPropertyChanged("StreamingRecords");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Kismet Records")]
        public List<Save.KismetBool> KismetRecords
        {
            get { return this._KismetRecords; }
            set
            {
                if (value != this._KismetRecords)
                {
                    this._KismetRecords = value;
                    this.NotifyPropertyChanged("KismetRecords");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Doors")]
        public List<Save.Door> Doors
        {
            get { return this._Doors; }
            set
            {
                if (value != this._Doors)
                {
                    this._Doors = value;
                    this.NotifyPropertyChanged("Doors");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Placeables")]
        public List<Save.Placeable> Placeables
        {
            get { return this._Placeables; }
            set
            {
                if (value != this._Placeables)
                {
                    this._Placeables = value;
                    this.NotifyPropertyChanged("Placeables");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Pawns")]
        public List<Guid> Pawns
        {
            get { return this._Pawns; }
            set
            {
                if (value != this._Pawns)
                {
                    this._Pawns = value;
                    this.NotifyPropertyChanged("Pawns");
                }
            }
        }

        [Category("Squad")]
        [DisplayName("Player")]
        public Save.Player Player
        {
            get { return this._Player; }
            set
            {
                if (value != this._Player)
                {
                    this._Player = value;
                    this.NotifyPropertyChanged("Player");
                }
            }
        }

        [Category("Squad")]
        [DisplayName("Henchmen")]
        public List<Save.Henchman> Henchmen
        {
            get { return this._Henchmen; }
            set
            {
                if (value != this._Henchmen)
                {
                    this._Henchmen = value;
                    this.NotifyPropertyChanged("Henchmen");
                }
            }
        }

        [Category("Plot")]
        [DisplayName("Plot")]
        public Save.PlotTable Plot
        {
            get { return this._Plot; }
            set
            {
                if (value != this._Plot)
                {
                    this._Plot = value;
                    this.NotifyPropertyChanged("Plot");
                }
            }
        }

        [Category("Plot")]
        [DisplayName("ME1 Plot")]
        public Save.ME1PlotTable ME1Plot
        {
            get { return this._ME1Plot; }
            set
            {
                if (value != this._ME1Plot)
                {
                    this._ME1Plot = value;
                    this.NotifyPropertyChanged("ME1Plot");
                }
            }
        }

        [Category("Plot")]
        [DisplayName("Player Variables")]
        public List<Save.PlayerVariable> PlayerVariables
        {
            get { return this._PlayerVariables; }
            set
            {
                if (value != this._PlayerVariables)
                {
                    this._PlayerVariables = value;
                    this.NotifyPropertyChanged("PlayerVariables");
                }
            }
        }

        [Category("Plot")]
        [DisplayName("Galaxy Map")]
        public Save.GalaxyMap GalaxyMap
        {
            get { return this._GalaxyMap; }
            set
            {
                if (value != this._GalaxyMap)
                {
                    this._GalaxyMap = value;
                    this.NotifyPropertyChanged("GalaxyMap");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Dependent DLC")]
        public List<Save.DependentDLC> DependentDLC
        {
            get { return this._DependentDLC; }
            set
            {
                if (value != this._DependentDLC)
                {
                    this._DependentDLC = value;
                    this.NotifyPropertyChanged("DependentDLC");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Treasures")]
        public List<Save.LevelTreasure> Treasures
        {
            get { return this._Treasures; }
            set
            {
                if (value != this._Treasures)
                {
                    this._Treasures = value;
                    this.NotifyPropertyChanged("Treasures");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Use Modules")]
        public List<Guid> UseModules
        {
            get { return this._UseModules; }
            set
            {
                if (value != this._UseModules)
                {
                    this._UseModules = value;
                    this.NotifyPropertyChanged("UseModules");
                }
            }
        }

        [Category("Basic")]
        [DisplayName("Conversation Mode")]
        public Save.AutoReplyModeOptions ConversationMode
        {
            get { return this._ConversationMode; }
            set
            {
                if (value != this._ConversationMode)
                {
                    this._ConversationMode = value;
                    this.NotifyPropertyChanged("ConversationMode");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Objective Markers")]
        public List<Save.ObjectiveMarker> ObjectiveMarkers
        {
            get { return this._ObjectiveMarkers; }
            set
            {
                if (value != this._ObjectiveMarkers)
                {
                    this._ObjectiveMarkers = value;
                    this.NotifyPropertyChanged("ObjectiveMarkers");
                }
            }
        }

        [Category("Other")]
        [DisplayName("Saved Objective Text")]
        public int SavedObjectiveText
        {
            get { return this._SavedObjectiveText; }
            set
            {
                if (value != this._SavedObjectiveText)
                {
                    this._SavedObjectiveText = value;
                    this.NotifyPropertyChanged("SavedObjectiveText");
                }
            }
        }
        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        private void NotifyPropertyChanged(string propertyName)
        {
            if (this.PropertyChanged != null)
            {
                this.PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public static SaveFile Read(Stream input)
        {
            if (input == null)
            {
                throw new ArgumentNullException("input");
            }

            var save = new SaveFile()
            {
                _Version = input.ReadValueU32(Endian.Little)
            };

            if (save._Version != 29 && save._Version.Swap() != 29 &&
                save._Version != 59 && save._Version.Swap() != 59)
            {
                throw new FormatException("unexpected version");
            }
            var endian = save._Version == 29 || save._Version == 59
                             ? Endian.Little
                             : Endian.Big;
            if (endian == Endian.Big)
            {
                save._Version = save._Version.Swap();
            }

            var reader = new Unreal.FileReader(input, save._Version, endian);
            save.Serialize(reader);

            if (save._Version >= 27)
            {
                if (input.Position != input.Length - 4)
                {
                    throw new FormatException("bad checksum position");
                }

                save._Checksum = input.ReadValueU32();
            }

            if (input.Position != input.Length)
            {
                throw new FormatException("did not consume entire file");
            }

            save.Endian = endian;
            return save;
        }

        public static void Write(SaveFile save, Stream output)
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
                memory.WriteValueU32(save.Version, save._Endian);

                var writer = new Unreal.FileWriter(memory, save._Version, save._Endian);
                save.Serialize(writer);

                if (save._Version >= 27)
                {
                    memory.Position = 0;
                    uint checksum = 0;

                    var buffer = new byte[1024];
                    while (memory.Position < memory.Length)
                    {
                        int read = memory.Read(buffer, 0, 1024);
                        checksum = Crc32.Compute(buffer, 0, read, checksum);
                    }

                    save._Checksum = checksum;
                    memory.WriteValueU32(checksum, save._Endian);
                }

                memory.Position = 0;
                output.WriteFromStream(memory, memory.Length);
            }
        }
    }
}
