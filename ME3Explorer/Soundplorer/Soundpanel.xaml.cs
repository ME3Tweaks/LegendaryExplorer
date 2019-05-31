using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml;
using System.Xml.Linq;
using Be.Windows.Forms;
using FontAwesome.WPF;
using ME3Explorer.Packages;
using ME3Explorer.SharedUI;
using ME3Explorer.SharedUI.Interfaces;
using ME3Explorer.Soundplorer;
using ME3Explorer.Unreal;
using ME3Explorer.Unreal.Classes;
using Microsoft.Win32;
using NAudio.Wave;
using static ME3Explorer.Unreal.Classes.WwiseBank;

namespace ME3Explorer
{
    /// <summary>
    /// Interaction logic for Soundpanel.xaml
    /// </summary>
    public partial class Soundpanel : ExportLoaderControl
    {
        public ObservableCollectionExtended<object> ExportInformationList { get; } = new ObservableCollectionExtended<object>();
        public ObservableCollectionExtended<HIRCNotableItem> HIRCNotableItems { get; } = new ObservableCollectionExtended<HIRCNotableItem>();
        private readonly List<EmbeddedWEMFile> AllWems = new List<EmbeddedWEMFile>(); //used only for rebuilding soundbank
        WwiseStream wwiseStream;
        public string afcPath = "";
        readonly DispatcherTimer seekbarUpdateTimer = new DispatcherTimer();
        private bool SeekUpdatingDueToTimer = false;
        private bool SeekDragging = false;
        Stream vorbisStream;
        private HexBox SoundpanelHIRC_Hexbox;
        private DynamicByteProvider hircHexProvider;

        public IBusyUIHost HostingControl
        {
            get { return (IBusyUIHost)this.GetValue(HostingControlProperty); }
            set { this.SetValue(HostingControlProperty, value); }
        }
        public static readonly DependencyProperty HostingControlProperty = DependencyProperty.Register(
            "HostingControl", typeof(IBusyUIHost), typeof(Soundpanel));

        private string _quickScanText;
        public string QuickScanText
        {
            get => _quickScanText;
            set => SetProperty(ref _quickScanText, value);
        }

        public ObservableCollectionExtended<HIRCObject> HIRCObjects { get; set; } = new ObservableCollectionExtended<HIRCObject>();


        //IMEPackage CurrentPackage; //used to tell when to update WwiseEvents list
        //private Dictionary<IExportEntry, List<Tuple<string, int, double>>> WemIdsToWwwiseEventIdMapping = new Dictionary<IExportEntry, List<Tuple<string, int, double>>>();

        public override void PopOut()
        {
            if (CurrentLoadedExport != null)
            {
                ExportLoaderHostedWindow elhw = new ExportLoaderHostedWindow(new Soundpanel(), CurrentLoadedExport);
                elhw.Title = $"Sound Player - {CurrentLoadedExport.UIndex} {CurrentLoadedExport.GetFullPath}_{CurrentLoadedExport.indexValue} - {CurrentLoadedExport.FileRef.FileName}";
                elhw.Height = 400;
                elhw.Width = 400;
                elhw.Show();
            }
        }

        public Soundpanel()
        {
            ME3ExpMemoryAnalyzer.MemoryAnalyzer.AddTrackedMemoryItem("Soundpanel Export Loader", new WeakReference(this));

            PlayPauseIcon = FontAwesomeIcon.Play;
            LoadCommands();
            CurrentVolume = 0.65f;
            _playbackState = PlaybackState.Stopped;
            seekbarUpdateTimer.Interval = new TimeSpan(0, 0, 1);
            seekbarUpdateTimer.Tick += UpdateSeekBarPos;
            InitializeComponent();
        }

        public override void LoadExport(IExportEntry exportEntry)
        {
            try
            {
                ExportInformationList.ClearEx();
                AllWems.Clear();
                CurrentLoadedWwisebank = null;
                //Check if we need to first gather wwiseevents for wem IDing
                //Uncomment when HIRC stuff is implemented, if ever...
                /*if (exportEntry.FileRef != CurrentPackage)
                {
                    //update
                    WemIdsToWwwiseEventIdMapping.Clear();
                    List<IExportEntry> wwiseEventExports = exportEntry.FileRef.Exports.Where(x => x.ClassName == "WwiseEvent").ToList();
                    foreach (IExportEntry wwiseEvent in wwiseEventExports)
                    {
                        StructProperty relationships = wwiseEvent.GetProperty<StructProperty>("Relationships");
                        IntProperty id = wwiseEvent.GetProperty<IntProperty>("Id");
                        FloatProperty DurationMilliseconds = wwiseEvent.GetProperty<FloatProperty>("DurationMilliseconds");

                        if (relationships != null)
                        {
                            ObjectProperty bank = relationships.GetProp<ObjectProperty>("Bank");
                            if (bank != null && bank.Value > 0)
                            {
                                //export in this file
                                List<Tuple<string, int, double>> bankWemInfosList;
                                Tuple<string, int, double> newData = new Tuple<string, int, double>(wwiseEvent.ObjectName, id.Value, DurationMilliseconds.Value);
                                if (WemIdsToWwwiseEventIdMapping.TryGetValue(exportEntry.FileRef.Exports[bank.Value - 1], out bankWemInfosList))
                                {
                                    bankWemInfosList.Add(newData);
                                }
                                else
                                {
                                    WemIdsToWwwiseEventIdMapping[exportEntry.FileRef.Exports[bank.Value - 1]] = new List<Tuple<string, int, double>>();
                                    WemIdsToWwwiseEventIdMapping[exportEntry.FileRef.Exports[bank.Value - 1]].Add(newData);
                                }
                            }
                        }
                    }

                }
                CurrentPackage = exportEntry.FileRef;*/
                ExportInformationList.Add($"#{exportEntry.Index} {exportEntry.ClassName} : {exportEntry.ObjectName}");
                if (exportEntry.ClassName == "WwiseStream")
                {
                    SoundPanel_TabsControl.SelectedItem = SoundPanel_PlayerTab;
                    WwiseStream w = new WwiseStream(exportEntry);
                    ExportInformationList.Add($"Filename : {w.FileName ?? "Stored in this PCC"}");
                    ExportInformationList.Add($"Data size: {w.DataSize} bytes");
                    ExportInformationList.Add($"Data offset: 0x{w.DataOffset:X8}");
                    string wemId = $"ID: 0x{w.Id:X8}";
                    if (Properties.Settings.Default.SoundplorerReverseIDDisplayEndianness)
                    {
                        wemId += $" | 0x{ReverseBytes((uint)w.Id):X8} (Reversed)";
                    }
                    ExportInformationList.Add(wemId);

                    if (w.FileName != null)
                    {
                        try
                        {
                            var samefolderpath = Directory.GetParent(exportEntry.FileRef.FileName);
                            string samefolderfilepath = System.IO.Path.Combine(samefolderpath.FullName, w.FileName + ".afc");
                            byte[] headerbytes = new byte[0x56];
                            bool bytesread = false;

                            if (File.Exists(samefolderfilepath))
                            {
                                using (FileStream fs = new FileStream(samefolderfilepath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                                {
                                    fs.Seek(w.DataOffset, SeekOrigin.Begin);
                                    fs.Read(headerbytes, 0, 0x56);
                                    bytesread = true;
                                }
                            }

                            if (bytesread)
                            {
                                //Parse it
                                ExportInformationList.Add($"---------Referenced Audio Header----------");
                                ASCIIEncoding ascii = new ASCIIEncoding();

                                ExportInformationList.Add("0x00 RIFF tag: " + ascii.GetString(headerbytes, 0, 4));
                                ExportInformationList.Add("0x04 File size: " + BitConverter.ToInt32(headerbytes, 4) + " bytes");
                                ExportInformationList.Add("0x08 WAVE tag: " + ascii.GetString(headerbytes, 8, 4));
                                ExportInformationList.Add("0x0C Format tag: " + ascii.GetString(headerbytes, 0xC, 4));
                                ExportInformationList.Add("0x10 Unknown 1: " + GetHexForUI(headerbytes, 0x10, 4));
                                ExportInformationList.Add("0x14 Unknown 2: " + GetHexForUI(headerbytes, 0x14, 2));
                                ExportInformationList.Add("0x16 Unknown 3: " + GetHexForUI(headerbytes, 0x16, 2));
                                ExportInformationList.Add("0x18 Sample rate: " + GetHexForUI(headerbytes, 0x18, 4));
                                ExportInformationList.Add("0x1C Unknown 5: " + GetHexForUI(headerbytes, 0x1C, 4));

                                ExportInformationList.Add("0x20 Unknown 6: " + GetHexForUI(headerbytes, 0x20, 4));
                                ExportInformationList.Add("0x24 Unknown 7: " + GetHexForUI(headerbytes, 0x24, 2));
                                ExportInformationList.Add("0x26 Unknown 8: " + GetHexForUI(headerbytes, 0x26, 2));
                                ExportInformationList.Add("0x28 Unknown 9: " + GetHexForUI(headerbytes, 0x28, 4));
                                ExportInformationList.Add("0x2C Unknown 10: " + GetHexForUI(headerbytes, 0x2C, 2));
                                ExportInformationList.Add("0x2E Unknown 11: " + GetHexForUI(headerbytes, 0x2E, 2));
                                ExportInformationList.Add("0x30 Unknown 12: " + GetHexForUI(headerbytes, 0x30, 4));
                                ExportInformationList.Add("0x34 Unknown 13: " + GetHexForUI(headerbytes, 0x34, 4));
                                ExportInformationList.Add("0x38 Unknown 14: " + GetHexForUI(headerbytes, 0x38, 2));
                                ExportInformationList.Add("0x3A Unknown 15: " + GetHexForUI(headerbytes, 0x3A, 2));
                                ExportInformationList.Add("0x3C Unknown 16: " + GetHexForUI(headerbytes, 0x3C, 4));

                                ExportInformationList.Add("0x40 Unknown 17: " + GetHexForUI(headerbytes, 0x40, 4));
                                ExportInformationList.Add("0x44 Unknown 18: " + GetHexForUI(headerbytes, 0x44, 2));
                                ExportInformationList.Add("0x46 Unknown 19: " + GetHexForUI(headerbytes, 0x46, 2));
                                ExportInformationList.Add("0x48 Unknown 20: " + GetHexForUI(headerbytes, 0x48, 4));
                                ExportInformationList.Add("0x4C Unknown 21: " + GetHexForUI(headerbytes, 0x4C, 4));

                                ExportInformationList.Add("0x50-56 Fully unknown: " + GetHexForUI(headerbytes, 0x50, 6));
                            }
                        }
                        catch (Exception e)
                        {

                        }
                    }
                    CurrentLoadedExport = exportEntry;
                }
                if (exportEntry.ClassName == "WwiseBank")
                {
                    WwiseBank wb = new WwiseBank(exportEntry);

                    if (exportEntry.FileRef.Game == MEGame.ME3)
                    {
                        QuickScanText = wb.QuickScanHirc(wb.GetChunk("HIRC"));
                        List<HIRCObject> hircObjects = wb.ParseHIRCObjects(wb.GetChunk("HIRC"));
                        HIRCObjects.Clear();
                        HIRCObjects.AddRange(hircObjects);
                        CurrentLoadedWwisebank = wb;
                    }
                    else
                    {
                        QuickScanText = "Cannot scan ME2 game files.";
                    }
                    List<(uint, int, int)> embeddedWEMFiles = wb.GetWEMFilesMetadata();
                    byte[] data = wb.GetChunk("DATA");
                    int i = 0;
                    if (embeddedWEMFiles.Count > 0)
                    {
                        foreach ((uint wemID, int offset, int size) singleWemMetadata in embeddedWEMFiles)
                        {
                            var wemData = new byte[singleWemMetadata.size];
                            //copy WEM data to buffer. Add 0x8 to skip DATA and DATASIZE header for this block.
                            Buffer.BlockCopy(data, singleWemMetadata.offset + 0x8, wemData, 0, singleWemMetadata.size);
                            //check for RIFF header as some don't seem to have it and are not playable.
                            string wemHeader = "" + (char)wemData[0] + (char)wemData[1] + (char)wemData[2] + (char)wemData[3];

                            string wemId = singleWemMetadata.wemID.ToString("X8");
                            if (Properties.Settings.Default.SoundplorerReverseIDDisplayEndianness)
                            {
                                wemId = $"{ReverseBytes(singleWemMetadata.wemID):X8} (Reversed)";
                            }
                            string wemName = "Embedded WEM 0x" + wemId;// + "(" + singleWemMetadata.Item1 + ")";

                            /* //HIRC lookup, if I ever get around to supporting HIRC
                            List<Tuple<string, int, double>> wemInfo;
                            if (WemIdsToWwwiseEventIdMapping.TryGetValue(exportEntry, out wemInfo))
                            {
                                var info = wemInfo.FirstOrDefault(x => x.Item2 == singleWemMetadata.Item1); //item2 in x = ID, singleWemMetadata.Item1 = ID
                                if (info != null)
                                {
                                    //have info
                                    wemName = info.Item1;
                                }
                            }*/
                            EmbeddedWEMFile wem = new EmbeddedWEMFile(wemData, i + ": " + wemName, exportEntry.FileRef.Game, singleWemMetadata.wemID);
                            if (wemHeader == "RIFF")
                            {
                                ExportInformationList.Add(wem);
                            }
                            else
                            {
                                ExportInformationList.Add($"{i}: {wemName} - No RIFF header");
                            }
                            AllWems.Add(wem);
                            i++;
                        }
                    }
                    else
                    {
                        ExportInformationList.Add("This soundbank has no embedded WEM files");
                    }
                    CurrentLoadedExport = exportEntry;

                    //This makes the hexbox widen by 1 and then shrink by 1
                    //For some rason it won't calculate the scrollbar again unless you do this
                    //which is very annoying.
                    var currentWidth = HIRC_Hexbox_Host.Width;
                    if (currentWidth > 500)
                    {
                        SoundpanelHIRC_Hexbox.Width -= 1;
                        HIRC_Hexbox_Host.UpdateLayout();
                        SoundpanelHIRC_Hexbox.Width += 1;
                    }
                    else
                    {
                        SoundpanelHIRC_Hexbox.Width += 1;
                        HIRC_Hexbox_Host.UpdateLayout();
                        SoundpanelHIRC_Hexbox.Width -= 1;
                    }

                    HIRC_Hexbox_Host.UpdateLayout();
                    SoundpanelHIRC_Hexbox.Select(0, 1);
                    SoundpanelHIRC_Hexbox.ScrollByteIntoView();
                }
                if (exportEntry.ClassName == "SoundNodeWave")
                {
                    int dataSizeOffset = exportEntry.propsEnd() + 4;
                    int dataLength = BitConverter.ToInt32(exportEntry.Data, dataSizeOffset);
                    if (dataLength > 0)
                    {
                        byte[] binData = exportEntry.Data.Skip(exportEntry.propsEnd() + 20).ToArray();
                        ISBank isb = new ISBank(binData, true);
                        List<ISBankEntry> audios = isb.BankEntries;
                        foreach (ISBankEntry isbe in isb.BankEntries)
                        {
                            if (isbe.DataAsStored != null)
                            {
                                ExportInformationList.Add(isbe);
                            }
                            else
                            {
                                ExportInformationList.Add($"{isbe.FileName} - No data - Data Location: 0x{isbe.DataOffset:X8}");
                            }
                        }
                    }
                    else
                    {
                        ExportInformationList.Add("This export contains no embedded audio");
                    }
                    CurrentLoadedExport = exportEntry;
                }
            }
            catch (Exception e)
            {

            }
        }

        private string GetHexForUI(byte[] bytes, int startoffset, int length)
        {
            string ret = "";

            if (length == 2)
            {
                ret += BitConverter.ToInt16(bytes, startoffset);
            }
            else if (length == 4)
            {
                ret += BitConverter.ToInt32(bytes, startoffset);
            }

            ret += " (";
            for (int i = 0; i < length; i++)
            {
                ret += bytes[startoffset + i].ToString("X2") + " ";
            }
            ret = ret.Trim();
            ret += ")";
            return ret;
        }

        public static UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public override void UnloadExport()
        {
            //throw new NotImplementedException();
            //waveOut.Stop();
            //CurrentVorbisStream.Dispose();
            //_audioPlayer.Dispose();
            //infoTextBox.Text = "Select an export";
            CurrentLoadedExport = null;
        }

        public void FreeAudioResources()
        {
            StopPlaying();
            _audioPlayer?.Dispose();
        }

        public override bool CanParse(IExportEntry exportEntry)
        {
            //            return (/*(exportEntry.FileRef.Game == MEGame.ME1 && exportEntry.ClassName == "SoundNodeWave") || */(exportEntry.FileRef.Game == MEGame.ME2 || exportEntry.FileRef.Game == MEGame.ME3) && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream"));
            return !exportEntry.ObjectName.StartsWith("Default__") && (exportEntry.FileRef.Game == MEGame.ME2 || exportEntry.FileRef.Game == MEGame.ME3) && (exportEntry.ClassName == "WwiseBank" || exportEntry.ClassName == "WwiseStream");
        }

        /// <summary>
        /// Gets a PCM stream of data (WAV) from either teh currently loaded export or selected WEM
        /// </summary>
        /// <param name="forcedWemFile">WEM that we will force to get a stream for</param>
        /// <returns></returns>
        public Stream getPCMStream(IExportEntry forcedWwiseStreamExport = null, EmbeddedWEMFile forcedWemFile = null)
        {
            if (CurrentLoadedISACTEntry != null)
            {
                return CurrentLoadedISACTEntry.GetWaveStream();
            }
            else if (CurrentLoadedAFCFileEntry != null)
            {
                return WwiseStream.CreateWaveStreamFromRaw(CurrentLoadedAFCFileEntry.AFCPath, CurrentLoadedAFCFileEntry.Offset, CurrentLoadedAFCFileEntry.DataSize, CurrentLoadedAFCFileEntry.ME2);
            }
            else
            {
                IExportEntry localCurrentExport = forcedWwiseStreamExport ?? CurrentLoadedExport;
                if (localCurrentExport != null || forcedWemFile != null)
                {
                    if (localCurrentExport != null && localCurrentExport.ClassName == "WwiseStream")
                    {
                        wwiseStream = new WwiseStream(localCurrentExport);
                        string path;
                        if (wwiseStream.IsPCCStored)
                        {
                            path = localCurrentExport.FileRef.FileName;
                        }
                        else
                        {
                            path = wwiseStream.getPathToAFC(); // only to check if AFC exists.
                        }
                        if (path != "")
                        {
                            return wwiseStream.CreateWaveStream(path);
                        }
                    }
                    if (localCurrentExport != null && localCurrentExport.ClassName == "SoundNodeWave")
                    {
                        object currentSelectedItem = ExportInfoListBox.SelectedItem;
                        if (currentSelectedItem == null || !(currentSelectedItem is ISBankEntry))
                        {
                            return null; //nothing selected, or current item is not playable
                        }
                        var bankEntry = (ISBankEntry)currentSelectedItem;
                        return bankEntry.GetWaveStream();
                    }
                    if (forcedWemFile != null || (localCurrentExport != null && localCurrentExport.ClassName == "WwiseBank"))
                    {
                        object currentWEMItem = forcedWemFile ?? ExportInfoListBox.SelectedItem;
                        if (currentWEMItem == null || currentWEMItem is string)
                        {
                            return null; //nothing selected, or current wem is not playable
                        }
                        var wemObject = (EmbeddedWEMFile)currentWEMItem;
                        string basePath = $"{System.IO.Path.GetTempPath()}ME3EXP_SOUND_{Guid.NewGuid()}";
                        File.WriteAllBytes(basePath + ".dat", wemObject.WemData);
                        return WwiseStream.ConvertRiffToWav(basePath + ".dat", wemObject.Game == MEGame.ME2);
                    }
                }
            }
            return null;
        }



        #region MVVM stuff
        private bool _repeating;
        public bool Repeating
        {
            get => _repeating;
            set => SetProperty(ref _repeating, value);
        }

        private FontAwesomeIcon _playPauseImageSource;
        public FontAwesomeIcon PlayPauseIcon
        {
            get => _playPauseImageSource;
            set => SetProperty(ref _playPauseImageSource, value);
        }


        private string _title;
        private double _currentTrackLength;
        private double _currentTrackPosition;
        private float _currentVolume;
        private SoundpanelAudioPlayer _audioPlayer;

        internal void UnloadAFCEntry()
        {
            CurrentLoadedAFCFileEntry = null;
        }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        internal void LoadAFCEntry(AFCFileEntry aEntry)
        {
            ExportInformationList.ClearEx();
            AllWems.Clear();

            ExportInformationList.Add($"Audio file in Audio File Cache");
            ExportInformationList.Add($"Filename : { aEntry.AFCPath}");
            ExportInformationList.Add($"Data size: {aEntry.DataSize} bytes");
            ExportInformationList.Add($"Data offset: 0x{aEntry.Offset:X8}");

            byte[] headerbytes = new byte[0x56];
            bool bytesread = false;

            try
            {
                if (File.Exists(aEntry.AFCPath))
                {
                    using (FileStream fs = new FileStream(aEntry.AFCPath, FileMode.Open))
                    {
                        fs.Seek(aEntry.Offset, SeekOrigin.Begin);
                        fs.Read(headerbytes, 0, 0x56);
                        bytesread = true;
                    }
                }

                if (bytesread)
                {
                    //Parse it
                    ExportInformationList.Add($"---------Wwise Audio Header----------");
                    ASCIIEncoding ascii = new ASCIIEncoding();

                    ExportInformationList.Add("0x00 RIFF tag: " + ascii.GetString(headerbytes, 0, 4));
                    ExportInformationList.Add("0x04 File size: " + BitConverter.ToInt32(headerbytes, 4) + " bytes");
                    ExportInformationList.Add("0x08 WAVE tag: " + ascii.GetString(headerbytes, 8, 4));
                    ExportInformationList.Add("0x0C Format tag: " + ascii.GetString(headerbytes, 0xC, 4));
                    ExportInformationList.Add("0x10 Unknown 1: " + GetHexForUI(headerbytes, 0x10, 4));
                    ExportInformationList.Add("0x14 Unknown 2: " + GetHexForUI(headerbytes, 0x14, 2));
                    ExportInformationList.Add("0x16 Unknown 3: " + GetHexForUI(headerbytes, 0x16, 2));
                    ExportInformationList.Add("0x18 Sample rate: " + GetHexForUI(headerbytes, 0x18, 4));
                    ExportInformationList.Add("0x1C Unknown 5: " + GetHexForUI(headerbytes, 0x1C, 4));

                    ExportInformationList.Add("0x20 Unknown 6: " + GetHexForUI(headerbytes, 0x20, 4));
                    ExportInformationList.Add("0x24 Unknown 7: " + GetHexForUI(headerbytes, 0x24, 2));
                    ExportInformationList.Add("0x26 Unknown 8: " + GetHexForUI(headerbytes, 0x26, 2));
                    ExportInformationList.Add("0x28 Unknown 9: " + GetHexForUI(headerbytes, 0x28, 4));
                    ExportInformationList.Add("0x2C Unknown 10: " + GetHexForUI(headerbytes, 0x2C, 2));
                    ExportInformationList.Add("0x2E Unknown 11: " + GetHexForUI(headerbytes, 0x2E, 2));
                    ExportInformationList.Add("0x30 Unknown 12: " + GetHexForUI(headerbytes, 0x30, 4));
                    ExportInformationList.Add("0x34 Unknown 13: " + GetHexForUI(headerbytes, 0x34, 4));
                    ExportInformationList.Add("0x38 Unknown 14: " + GetHexForUI(headerbytes, 0x38, 2));
                    ExportInformationList.Add("0x3A Unknown 15: " + GetHexForUI(headerbytes, 0x3A, 2));
                    ExportInformationList.Add("0x3C Unknown 16: " + GetHexForUI(headerbytes, 0x3C, 4));

                    ExportInformationList.Add("0x40 Unknown 17: " + GetHexForUI(headerbytes, 0x40, 4));
                    ExportInformationList.Add("0x44 Unknown 18: " + GetHexForUI(headerbytes, 0x44, 2));
                    ExportInformationList.Add("0x46 Unknown 19: " + GetHexForUI(headerbytes, 0x46, 2));
                    ExportInformationList.Add("0x48 Unknown 20: " + GetHexForUI(headerbytes, 0x48, 4));
                    ExportInformationList.Add("0x4C Unknown 21: " + GetHexForUI(headerbytes, 0x4C, 4));

                    ExportInformationList.Add("0x50-56 Fully unknown: " + GetHexForUI(headerbytes, 0x50, 6));
                    CurrentLoadedAFCFileEntry = aEntry;
                }
            }
            catch (Exception e)
            {

            }

        }

        public float CurrentVolume
        {
            get => _currentVolume;
            set => SetProperty(ref _currentVolume, value);
        }

        public double CurrentTrackLength
        {
            get => _currentTrackLength;
            set => SetProperty(ref _currentTrackLength, value);
        }

        public double CurrentTrackPosition
        {
            get => _currentTrackPosition;
            set
            {
                if (value.Equals(_currentTrackPosition)) return;
                _currentTrackPosition = value;
                SeekUpdatingDueToTimer = true;
                OnPropertyChanged(nameof(CurrentTrackPosition));
                SeekUpdatingDueToTimer = false;
            }
        }

        public ICommand ReplaceAudioCommand { get; set; }

        public ICommand ExportAudioCommand { get; set; }
        public ICommand StartPlaybackCommand { get; set; }
        public ICommand StopPlaybackCommand { get; set; }

        public ICommand TrackControlMouseDownCommand { get; set; }
        public ICommand TrackControlMouseUpCommand { get; set; }
        public ICommand VolumeControlValueChangedCommand { get; set; }
        public ICommand CommitCommand { get; set; }
        public ICommand SearchHIRCHexCommand { get; private set; }
        public ICommand SaveHIRCHexCommand { get; private set; }

        /// <summary>
        /// The cached stream source is used to determine if we should unload the current vorbis stream
        /// when pressing play again after playback has been stopped.
        /// </summary>
        private object CachedStreamSource { get; set; }
        public ISBankEntry CurrentLoadedISACTEntry { get; private set; }
        public AFCFileEntry CurrentLoadedAFCFileEntry { get; private set; }
        public WwiseBank CurrentLoadedWwisebank { get; private set; }
        private string _searchStatusText;
        public string SearchStatusText
        {
            get => _searchStatusText;
            private set => SetProperty(ref _searchStatusText, value);
        }

        private enum PlaybackState
        {
            Playing, Stopped, Paused
        }

        private PlaybackState _playbackState;
        private bool RestartingDueToLoop;

        private void LoadCommands()
        {
            // Player commands
            ReplaceAudioCommand = new RelayCommand(ReplaceAudio, CanReplaceAudio);
            ExportAudioCommand = new RelayCommand(ExportAudio, CanExportAudio);
            StartPlaybackCommand = new RelayCommand(StartPlayback, CanStartPlayback);
            StopPlaybackCommand = new RelayCommand(StopPlayback, CanStopPlayback);

            // Event commands
            TrackControlMouseDownCommand = new RelayCommand(TrackControlMouseDown, CanTrackControlMouseDown);
            TrackControlMouseUpCommand = new RelayCommand(TrackControlMouseUp, CanTrackControlMouseUp);
            VolumeControlValueChangedCommand = new RelayCommand(VolumeControlValueChanged, CanVolumeControlValueChanged);

            //WwisebankEditor commands
            CommitCommand = new GenericCommand(CommitBankToFile, CanCommitBankToFile);
            SearchHIRCHexCommand = new GenericCommand(SearchHIRCHex, CanSearchHIRCHex);
            SaveHIRCHexCommand = new GenericCommand(SaveHIRCHex, CanSaveHIRCHex);
        }



        private bool CanSaveHIRCHex() => HIRCHexChanged;

        private void SaveHIRCHex()
        {
            if (HIRC_ListBox.SelectedItem is HIRCObject ho)
            {
                ho.Data = hircHexProvider.Bytes.ToArray();
                HIRCHexChanged = false;
                OnPropertyChanged(nameof(HIRCHexChanged));
            }
        }

        private bool CanSearchHIRCHex()
        {
            string hexString = SearchHIRCHex_TextBox.Text.Replace(" ", string.Empty);
            if (hexString.Length == 0)
                return false;
            if (!isHexString(hexString))
            {
                return false;
            }
            if (hexString.Length % 2 != 0)
            {
                return false;
            }
            return true;
        }

        private void SearchHIRCHex()
        {
            if (CurrentLoadedWwisebank == null)
                return;
            int currentSelectedHIRCIndex = HIRC_ListBox.SelectedIndex;
            if (currentSelectedHIRCIndex == -1)
                currentSelectedHIRCIndex = 0;
            string hexString = SearchHIRCHex_TextBox.Text.Replace(" ", string.Empty);
            if (hexString.Length == 0)
                return;
            if (!isHexString(hexString))
            {
                SearchStatusText = "Illegal characters in Hex String";
                return;
            }
            if (hexString.Length % 2 != 0)
            {
                SearchStatusText = "Odd number of characters in Hex String";
                return;
            }
            byte[] buff = new byte[hexString.Length / 2];
            for (int i = 0; i < hexString.Length / 2; i++)
            {
                buff[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            byte[] hirc;
            int count = HIRCObjects.Count;
            int hexboxIndex = (int)SoundpanelHIRC_Hexbox.SelectionStart + 1;
            for (int i = 0; i < count; i++)
            {
                hirc = HIRCObjects[(i + currentSelectedHIRCIndex) % count].Data; //search from selected index, and loop back around
                int indexIn = hirc.IndexOfArray(buff, hexboxIndex);
                if (indexIn > -1)
                {
                    HIRC_ListBox.SelectedIndex = (i + currentSelectedHIRCIndex) % count;
                    SoundpanelHIRC_Hexbox.Select(indexIn, buff.Length);
                    //searchHexStatus.Text = "";
                    return;
                }
                hexboxIndex = 0;
            }
            SearchStatusText = "Hex not found";
        }

        /// <summary>
        /// Ported from WwiseViewer
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        public static bool isHexString(string s)
        {
            string hexChars = "0123456789abcdefABCDEF";
            for (int i = 0; i < s.Length; i++)
            {
                int f = -1;
                for (int j = 0; j < hexChars.Length; j++)
                    if (s[i] == hexChars[j])
                    {
                        f = j;
                        break;
                    }
                if (f == -1)
                    return false;
            }
            return true;
        }

        private bool CanCommitBankToFile() => HasPendingHIRCChanges;

        private void CommitBankToFile()
        {
            CurrentLoadedExport.Data = CurrentLoadedWwisebank.RecreateBinary(HIRCObjects.Select(x => x.Data).ToList());
        }

        private bool _hircHexChanged;
        public bool HIRCHexChanged
        {
            get => _hircHexChanged;
            private set => SetProperty(ref _hircHexChanged, value);
        }

        internal void LoadISACTEntry(ISBankEntry entry)
        {
            try
            {
                ExportInformationList.Clear();
                AllWems.Clear();

                ExportInformationList.Add(entry.FileName);
                ExportInformationList.Add($"Ogg encoded: {entry.isOgg}");
                ExportInformationList.Add($"PCM encoded: {entry.isPCM}");
                ExportInformationList.Add($"ADPCM encoded: {!entry.isOgg && !entry.isPCM}");
                ExportInformationList.Add($"Header offset: 0x{entry.HeaderOffset:X8}");
                ExportInformationList.Add($"Datastream size: {entry.DataAsStored.Length} bytes");
                ExportInformationList.Add($"Datastream offset: 0x{entry.DataOffset:X8}");

                CurrentLoadedISACTEntry = entry;
            }
            catch (Exception e)
            {

            }
        }

        internal void UnloadISACTEntry()
        {
            CurrentLoadedISACTEntry = null;
        }

        private bool CanReplaceAudio(object obj)
        {
            if (CurrentLoadedExport == null) return false;
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                return CurrentLoadedExport.FileRef.Game == MEGame.ME3;
            }
            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                object currentWEMItem = ExportInfoListBox.SelectedItem;
                bool result = currentWEMItem != null && currentWEMItem is EmbeddedWEMFile && CurrentLoadedExport.FileRef.Game == MEGame.ME3;
                return result;
            }
            return false;
        }

        private async void ReplaceAudio(object obj)
        {
            if (CurrentLoadedExport == null) return;
            if (CurrentLoadedExport.ClassName == "WwiseStream")
            {
                await ReplaceAudioFromWave();
            }
            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                ReplaceWEMAudioFromWave();
            }
        }

        private async void ReplaceWEMAudioFromWave(string sourceFile = null, WwiseConversionSettingsPackage conversionSettings = null)
        {
            if (ExportInfoListBox.SelectedItem is EmbeddedWEMFile wemToReplace && CurrentLoadedExport.FileRef.Game == MEGame.ME3)
            {
                string wwisePath = GetWwiseCLIPath(false);
                if (wwisePath == null) return;
                if (sourceFile == null)
                {
                    OpenFileDialog d = new OpenFileDialog { Filter = "Wave PCM|*.wav" };
                    bool? res = d.ShowDialog();
                    if (res.HasValue && res.Value)
                    {
                        sourceFile = d.FileName;
                    }
                    else
                    {
                        return;
                    }

                    if (conversionSettings == null)
                    {
                        SoundReplaceOptionsDialog srod = new SoundReplaceOptionsDialog(Window.GetWindow(this));
                        if (srod.ShowDialog().Value)
                        {
                            conversionSettings = srod.ChosenSettings;
                        }
                        else
                        {
                            return; //user didn't choose any settings
                        }
                    }
                }

                //Convert and replace
                ReplaceWEMAudioFromWwiseOgg(await RunWwiseConversion(wwisePath, sourceFile, conversionSettings), wemToReplace);
            }
        }

        /// <summary>
        /// Rewrites the soundbank export with new data from the ogg.
        /// </summary>
        /// <param name="oggPath"></param>
        /// <param name="wem"></param>
        private void ReplaceWEMAudioFromWwiseOgg(string oggPath, EmbeddedWEMFile wem)
        {
            WwiseBank w = new WwiseBank(CurrentLoadedExport);
            if (oggPath == null)
            {
                OpenFileDialog d = new OpenFileDialog { Filter = "Wwise Encoded Ogg|*.ogg" };
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    oggPath = d.FileName;
                }
                else
                {
                    return;
                }
            }

            MemoryStream convertedStream = null;
            using (var fileStream = new FileStream(oggPath, FileMode.Open))
            {
                convertedStream = WwiseStream.ConvertWwiseOggToME3Ogg(fileStream);
            }

            //Update the EmbeddedWEMFile. As this is an object it will be updated in the references.
            if (wem.HasBeenFixed)
            {
                wem.OriginalWemData = convertedStream.ToArray();
            }
            else
            {
                wem.WemData = convertedStream.ToArray();
            }

            w.UpdateDataChunk(AllWems); //updates this export's data.
            File.Delete(oggPath);
            MessageBox.Show("Done");
        }

        public async Task ReplaceAudioFromWave(string sourceFile = null, IExportEntry forcedExport = null, WwiseConversionSettingsPackage conversionSettings = null)
        {
            string wwisePath = GetWwiseCLIPath(false);
            if (wwisePath == null) return;
            if (sourceFile == null)
            {
                OpenFileDialog d = new OpenFileDialog { Filter = "Wave PCM|*.wav" };
                bool? res = d.ShowDialog();
                if (res.HasValue && res.Value)
                {
                    sourceFile = d.FileName;
                }
                else
                {
                    return;
                }
            }

            if (conversionSettings == null)
            {
                SoundReplaceOptionsDialog srod = new SoundReplaceOptionsDialog(Window.GetWindow(this));
                if (srod.ShowDialog().Value)
                {
                    conversionSettings = srod.ChosenSettings;
                }
                else
                {
                    return; //user didn't choose any settings
                }
            }

            //Convert and replace
            if (HostingControl != null)
            {
                HostingControl.BusyText = "Converting and replacing audio";
                HostingControl.IsBusy = true;
            }
            var conversion = await Task.Run(async () =>
            {
                return await RunWwiseConversion(wwisePath, sourceFile, conversionSettings);
            });

            ReplaceAudioFromWwiseOgg(conversion, forcedExport);
        }

        /// <summary>
        /// Converts a 
        /// </summary>
        /// <param name="wwiseCLIPath">Path to Wwise CLI executable</param>
        /// <param name="fileOrFolderPath">Path of file or folder to convert</param>
        /// <param name="conversionSettings">Settings to place into the templated project that will be used when CLI runs</param>
        /// <returns></returns>
        public async Task<string> RunWwiseConversion(string wwiseCLIPath, string fileOrFolderPath, WwiseConversionSettingsPackage conversionSettings)
        {
            /* The process for converting is going to be pretty in depth but will make converting files much easier and faster.
                         * 1. User chooses a folder of .wav (or this method is passed a .wav and we will return that)
                         * 2. Conversion takes place
                         * 
                         * Program steps when conversion starts:
                         * 1. Extract the Wwise TemplateProject as it is required for command line. This is extracted to the root of %Temp%.
                         * 2. Generate the external sources file that points to the folder and each item to convert within it
                         * 3. Run the generate command
                         * 4. Move files from OutputFiles directory in the project
                         * 5. Delete the project
                         * */



            //Extract the template project to temp
            var assembly = Assembly.GetExecutingAssembly();
            string[] stuff = assembly.GetManifestResourceNames();
            const string resourceName = "ME3Explorer.Soundplorer.WwiseTemplateProject.zip";
            string templatefolder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject");

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                await TryDeleteDirectory(templatefolder);
                ZipArchive archive = new ZipArchive(stream);
                archive.ExtractToDirectory(System.IO.Path.GetTempPath());
            }

            //Generate the external sources document
            string[] filesToConvert = null;
            string folderParent = null;
            bool isSingleFile = false;
            if (Directory.Exists(fileOrFolderPath))
            {
                //it's a directory
                filesToConvert = Directory.GetFiles(fileOrFolderPath, "*.wav");
                folderParent = fileOrFolderPath;
            }
            else
            {
                //it's a single file
                isSingleFile = true;
                filesToConvert = new string[] { fileOrFolderPath };
                folderParent = Directory.GetParent(fileOrFolderPath).FullName;
            }



            XElement externalSourcesList = new XElement("ExternalSourcesList", new XAttribute("SchemaVersion", 1.ToString()), new XAttribute("Root", folderParent));
            foreach (string file in filesToConvert)
            {
                XElement source = new XElement("Source", new XAttribute("Path", System.IO.Path.GetFileName(file)), new XAttribute("Conversion", "Vorbis"));
                externalSourcesList.Add(source);
            }

            //Write ExternalSources.wsources
            string wsourcesFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "ExternalSources.wsources");

            File.WriteAllText(wsourcesFile, externalSourcesList.ToString());
            Debug.WriteLine(externalSourcesList.ToString());

            string conversionSettingsFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "Conversion Settings", "Default Work Unit.wwu");
            XmlDocument conversionDoc = new XmlDocument();
            conversionDoc.Load(conversionSettingsFile);

            //Samplerate
            XmlNode node = conversionDoc.DocumentElement.SelectSingleNode("/WwiseDocument/Conversions/Conversion/PropertyList/Property[@Name='SampleRate']/ValueList/Value[@Platform='Windows']");
            node.InnerText = conversionSettings.TargetSamplerate.ToString();
            conversionDoc.Save(conversionSettingsFile);
            //Run Conversion

            //uncomment the following lines to view output from wwisecli
            //DebugOutput.StartDebugger("Wwise Wav to Ogg Converter");
            Process process = new Process();
            process.StartInfo.FileName = wwiseCLIPath;
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.RedirectStandardError = true;
            //process.OutputDataReceived += (s, eventArgs) => { Debug.WriteLine(eventArgs.Data); DebugOutput.PrintLn(eventArgs.Data); };
            //process.ErrorDataReceived += (s, eventArgs) => { Debug.WriteLine(eventArgs.Data); DebugOutput.PrintLn(eventArgs.Data); };

            string projFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "TemplateProject.wproj");
            process.StartInfo.Arguments = $"\"{projFile}\" -ConvertExternalSources Windows";

            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.StartInfo.RedirectStandardOutput = true;
            process.Start();
            //process.BeginOutputReadLine();
            process.WaitForExit();
            Debug.WriteLine("Process output: \n" + process.StandardOutput.ReadToEnd());
            process.Close();

            //Files generates
            string outputDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TemplateProject", "OutputFiles");
            string copyToDirectory = System.IO.Path.Combine(folderParent, "Converted");
            Directory.CreateDirectory(copyToDirectory);
            foreach (string file in filesToConvert)
            {
                string basename = System.IO.Path.GetFileNameWithoutExtension(file);
                File.Copy(System.IO.Path.Combine(outputDirectory, basename + ".ogg"), System.IO.Path.Combine(copyToDirectory, basename + ".ogg"), true);
            }
            var deleteResult = await TryDeleteDirectory(templatefolder);
            Debug.WriteLine("Deleted templatedproject: " + deleteResult);

            if (isSingleFile)
            {
                return System.IO.Path.Combine(copyToDirectory, System.IO.Path.GetFileNameWithoutExtension(fileOrFolderPath) + ".ogg");
            }
            else
            {
                return copyToDirectory;
            }
        }


        public static async Task<bool> TryDeleteDirectory(string directoryPath, int maxRetries = 10, int millisecondsDelay = 30)
        {
            if (directoryPath == null)
                throw new ArgumentNullException(directoryPath);
            if (maxRetries < 1)
                throw new ArgumentOutOfRangeException(nameof(maxRetries));
            if (millisecondsDelay < 1)
                throw new ArgumentOutOfRangeException(nameof(millisecondsDelay));

            for (int i = 0; i < maxRetries; ++i)
            {
                try
                {
                    if (Directory.Exists(directoryPath))
                    {
                        Directory.Delete(directoryPath, true);
                    }

                    return true;
                }
                catch (IOException)
                {
                    await Task.Delay(millisecondsDelay);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(millisecondsDelay);
                }
            }

            return false;
        }

        /// <summary>
        /// Checks if Wwwise Build 3773 x64 is installed using the system environment variable. Returns the path if is valid.
        /// </summary>
        /// <param name="silent">Supress dialogs</param>
        /// <returns>Path to WwiseCLI if Wwise Build 3773 x64 is found, null otherwise</returns>
        public static string GetWwiseCLIPath(bool silent)
        {
            string wwisePath = Environment.GetEnvironmentVariable("WWiseRoot");
            if (wwisePath != null)
            {
                wwisePath = System.IO.Path.Combine(wwisePath, @"Authoring\x64\Release\bin\WwiseCLI.exe");
                if (File.Exists(wwisePath))
                {
                    //check that it's a supported version...
                    var versionInfo = FileVersionInfo.GetVersionInfo(wwisePath);
                    string version = versionInfo.ProductVersion; // Will typically return "1.0.0" in your case
                    if (version != "2010.3.3.3773")
                    {
                        //wrong version
                        if (!silent)
                            MessageBox.Show("WwiseCLI.exe found, but it's the wrong version:" + version + ".\nInstall Wwise Build 3773 64bit to use this feature.");
                        return null;
                    }
                    else
                    {
                        return wwisePath;
                    }
                }
                else
                {
                    if (!silent)
                        MessageBox.Show("WwiseCLI.exe was not found on your system.\nInstall Wwise Build 3773 64bit to use this feature.");
                    return null;
                }
            }
            else
            {
                if (!silent)
                    MessageBox.Show("Wwise does not appear to be installed on your system.\nInstall Wwise Build 3773 64bit to use this feature.");
                return null;
            }
        }

        // Player commands
        private void ExportAudio(object p)
        {
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM File|*.wav",
                        FileName = CurrentLoadedExport.ObjectName + ".wav"
                    };
                    if (d.ShowDialog().Value)
                    {
                        WwiseStream w = new WwiseStream(CurrentLoadedExport);
                        string wavPath = w.CreateWave(w.getPathToAFC());
                        if (wavPath != null && File.Exists(wavPath))
                        {
                            File.Copy(wavPath, d.FileName, true);
                        }
                        MessageBox.Show("Done.");
                    }
                }

                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    EmbeddedWEMFile currentWEMItem = (EmbeddedWEMFile)ExportInfoListBox.SelectedItem;
                    SaveFileDialog d = new SaveFileDialog
                    {
                        Filter = "Wave PCM|*.wav",
                        FileName = $"{CurrentLoadedExport.ObjectName}_0x{currentWEMItem.Id:X8}.wav"
                    };
                    if (d.ShowDialog().Value)
                    {
                        Stream ms = getPCMStream();
                        ms.Seek(0, SeekOrigin.Begin);
                        using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                        {
                            ms.CopyTo(fs);
                            fs.Flush();
                        }
                        MessageBox.Show("Done.");
                    }
                }
            }
            if (CurrentLoadedISACTEntry != null)
            {
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = CurrentLoadedISACTEntry.FileName
                };
                if (d.ShowDialog().Value)
                {
                    MemoryStream waveStream = CurrentLoadedISACTEntry.GetWaveStream();
                    waveStream.Seek(0, SeekOrigin.Begin);
                    using (FileStream fs = new FileStream(d.FileName, FileMode.OpenOrCreate))
                    {
                        waveStream.CopyTo(fs);
                        fs.Flush();
                    }
                    MessageBox.Show("Done.");
                }
            }
            if (CurrentLoadedAFCFileEntry != null)
            {
                string presetfilename = $"{System.IO.Path.GetFileNameWithoutExtension(CurrentLoadedAFCFileEntry.AFCPath)}_{CurrentLoadedAFCFileEntry.Offset}.wav";
                SaveFileDialog d = new SaveFileDialog
                {
                    Filter = "Wave PCM File|*.wav",
                    FileName = presetfilename
                };
                if (d.ShowDialog().Value)
                {
                    Stream s = WwiseStream.CreateWaveStreamFromRaw(CurrentLoadedAFCFileEntry.AFCPath, CurrentLoadedAFCFileEntry.Offset, CurrentLoadedAFCFileEntry.DataSize, CurrentLoadedAFCFileEntry.ME2);
                    using (var fileStream = File.Create(d.FileName))
                    {
                        s.Seek(0, SeekOrigin.Begin);
                        s.CopyTo(fileStream);
                    }
                    MessageBox.Show("Done.");
                }
            }
        }

        private bool CanExportAudio(object p)
        {
            if (CurrentLoadedExport == null && CurrentLoadedISACTEntry == null && CurrentLoadedAFCFileEntry == null) return false;
            if (CurrentLoadedISACTEntry != null) return true;
            if (CurrentLoadedAFCFileEntry != null) return true;
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName == "WwiseStream") return true;
                if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    object currentWEMItem = ExportInfoListBox.SelectedItem;
                    return currentWEMItem != null && currentWEMItem is EmbeddedWEMFile;
                }
            }
            return false;
        }

        private void StartPlayback(object p)
        {
            StartOrPausePlaying();
        }

        public void StartOrPausePlaying()
        {
            bool playToggle = true;
            if (_playbackState == PlaybackState.Stopped)
            {
                if (vorbisStream == null)
                {
                    UpdateVorbisStream();
                }
                else
                {
                    if (!RestartingDueToLoop)
                    {
                        if ((CurrentLoadedISACTEntry != null && CachedStreamSource != CurrentLoadedISACTEntry) ||
                            (CurrentLoadedAFCFileEntry != null && CachedStreamSource != CurrentLoadedAFCFileEntry))
                        {
                            //invalidate the cache
                            UpdateVorbisStream();
                        }
                        if (CurrentLoadedExport != null)
                        {
                            //check if cached is the same as what we want to play
                            if (CurrentLoadedExport.ClassName == "WwiseStream" && CachedStreamSource != CurrentLoadedExport)
                            {
                                //invalidate the cache
                                UpdateVorbisStream();
                            }
                            else if (CurrentLoadedExport.ClassName == "WwiseBank" && CachedStreamSource != ExportInfoListBox.SelectedItem)
                            {
                                //Invalidate the cache
                                UpdateVorbisStream();
                            }
                            else if (CurrentLoadedExport.ClassName == "SoundNodeWave" && CachedStreamSource != ExportInfoListBox.SelectedItem)
                            {
                                //Invalidate the cache
                                UpdateVorbisStream();
                            }
                        }
                    }
                }
                //check to make sure stream has loaded before we attempt to play it
                if (vorbisStream != null)
                {
                    try
                    {
                        vorbisStream.Position = 0;
                        _audioPlayer = new SoundpanelAudioPlayer(vorbisStream, CurrentVolume)
                        {
                            PlaybackStopType = SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile
                        };
                        _audioPlayer.PlaybackPaused += _audioPlayer_PlaybackPaused;
                        _audioPlayer.PlaybackResumed += _audioPlayer_PlaybackResumed;
                        _audioPlayer.PlaybackStopped += _audioPlayer_PlaybackStopped;
                        CurrentTrackLength = _audioPlayer.GetLengthInSeconds();
                        playToggle = true;

                        // Start the timer.  Note that this call can be made from any thread.
                        seekbarUpdateTimer.Start();
                        // Timer callback code here...
                    }
                    catch (Exception)
                    {
                        //error playing audio or initializing
                        vorbisStream = null;
                        playToggle = false;
                    }

                    //_audioPlayer.Play(NAudio.Wave.PlaybackState.Stopped, CurrentVolume);
                    //CurrentlyPlayingTrack = CurrentlySelectedTrack;
                }
                else
                {
                    playToggle = false;
                }
            }

            if (playToggle)
            {
                _audioPlayer.TogglePlayPause(CurrentVolume);
            }
        }

        private void UpdateVorbisStream()
        {
            vorbisStream = getPCMStream();
            //if (vorbisStream is MemoryStream ms)
            //{
            //    File.WriteAllBytes(@"C:\users\public\file.wav", ms.ToArray());
            //}
            if (CurrentLoadedISACTEntry != null)
            {
                CachedStreamSource = CurrentLoadedISACTEntry;
            }
            if (CurrentLoadedAFCFileEntry != null)
            {
                CachedStreamSource = CurrentLoadedAFCFileEntry;
            }
            if (CurrentLoadedExport != null)
            {
                if (CurrentLoadedExport.ClassName == "WwiseStream")
                {
                    CachedStreamSource = CurrentLoadedExport;
                }
                else if (CurrentLoadedExport.ClassName == "WwiseBank")
                {
                    CachedStreamSource = ExportInfoListBox.SelectedItem;
                }
                else if (CurrentLoadedExport.ClassName == "SoundNodeWave")
                {
                    CachedStreamSource = ExportInfoListBox.SelectedItem;
                }
            }
        }

        private void UpdateSeekBarPos(object state, EventArgs e)
        {
            if (!SeekDragging)
            {
                CurrentTrackPosition = _audioPlayer?.GetPositionInSeconds() ?? 0;
            }
        }


        public bool CanStartPlayback(object p)
        {
            if (vorbisStream != null) return true; //looping
            if (CurrentLoadedExport == null && CurrentLoadedISACTEntry == null && CurrentLoadedAFCFileEntry == null) return false;
            if (CurrentLoadedISACTEntry != null) return true;
            if (CurrentLoadedAFCFileEntry != null) return true;
            if (CurrentLoadedExport.ClassName == "WwiseStream") return true;

            if (CurrentLoadedExport.ClassName == "WwiseBank")
            {
                object currentWEMItem = ExportInfoListBox.SelectedItem;
                if (currentWEMItem == null || currentWEMItem is string)
                {
                    return false; //nothing selected, or current wem is not playable
                }
                if (currentWEMItem is EmbeddedWEMFile) return true;
            }
            if (CurrentLoadedExport.ClassName == "SoundNodeWave")
            {
                object currentNodeWaveItem = ExportInfoListBox.SelectedItem;
                if (currentNodeWaveItem == null) return false;
                if (currentNodeWaveItem is ISBankEntry isbe)
                {
                    return isbe.DataAsStored != null;
                }
                if (currentNodeWaveItem is EmbeddedWEMFile) return true;
            }

            return false;
        }

        private void StopPlayback(object p)
        {
            StopPlaying();
        }

        public void StopPlaying()
        {
            seekbarUpdateTimer.Stop();
            if (_audioPlayer != null)
            {

                _audioPlayer.PlaybackStopType = SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedByUser;
                _audioPlayer.Stop();
            }
            if (vorbisStream != null)
            {
                //vorbisStream.Dispose();
                vorbisStream = null;
            }
        }

        private bool CanStopPlayback(object p)
        {
            return _playbackState == PlaybackState.Playing || _playbackState == PlaybackState.Paused || vorbisStream != null;
        }

        // Events
        private void TrackControlMouseDown(object p)
        {
            _audioPlayer?.Pause();
        }

        private void TrackControlMouseUp(object p)
        {
            if (_audioPlayer != null)
            {
                _audioPlayer.SetPosition(CurrentTrackPosition);
                _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
            }
        }

        private bool CanTrackControlMouseDown(object p)
        {
            return _playbackState == PlaybackState.Playing;
        }

        private bool CanTrackControlMouseUp(object p)
        {
            return _playbackState == PlaybackState.Paused;
        }

        private void VolumeControlValueChanged(object p)
        {
            _audioPlayer?.SetVolume(CurrentVolume); // set value of the slider to current volume
        }

        private bool CanVolumeControlValueChanged(object p)
        {
            return true;
        }

        private void _audioPlayer_PlaybackStopped()
        {
            _playbackState = PlaybackState.Stopped;
            PlayPauseIcon = FontAwesomeIcon.Play;

            CommandManager.InvalidateRequerySuggested();
            CurrentTrackPosition = 0;

            if (_audioPlayer.PlaybackStopType == SoundpanelAudioPlayer.PlaybackStopTypes.PlaybackStoppedReachingEndOfFile && Properties.Settings.Default.SoundpanelRepeating)
            {
                RestartingDueToLoop = true;
                StartPlayback(null);
                RestartingDueToLoop = false;
            }
        }

        private void _audioPlayer_PlaybackResumed()
        {
            _playbackState = PlaybackState.Playing;
            PlayPauseIcon = FontAwesomeIcon.Pause;
        }

        private void _audioPlayer_PlaybackPaused()
        {
            UpdateSeekBarPos(null, null);
            _playbackState = PlaybackState.Paused;
            PlayPauseIcon = FontAwesomeIcon.Play;
        }

        #endregion

        /// <summary>
        /// Call this method when the soundpanel is being destroyed to release the audio and stop playback.
        /// </summary>
        public void Soundpanel_Unload()
        {
            StopPlaying();
            _audioPlayer?.Dispose();
        }

        private void Seekbar_DragStarted(object sender, System.Windows.Controls.Primitives.DragStartedEventArgs e)
        {
            SeekDragging = true;
        }

        private void Seekbar_DragCompleted(object sender, System.Windows.Controls.Primitives.DragCompletedEventArgs e)
        {
            if (!SeekUpdatingDueToTimer)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }
            SeekDragging = false;
        }

        private void Seekbar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!SeekUpdatingDueToTimer && !SeekDragging)
            {
                if (_audioPlayer != null)
                {
                    _audioPlayer.SetPosition(CurrentTrackPosition);
                    _audioPlayer.Play(NAudio.Wave.PlaybackState.Paused, CurrentVolume);
                }
            }
        }

        /// <summary>
        /// Replaces the audio in the current loaded export, or the forced export. Will prompt user for a Wwise Encoded Ogg file.
        /// </summary>
        /// <param name="forcedExport">Export to update. If null, the currently loadedo ne is used instead.</param>
        public void ReplaceAudioFromWwiseOgg(string oggPath = null, IExportEntry forcedExport = null)
        {
            IExportEntry exportToWorkOn = forcedExport ?? CurrentLoadedExport;
            if (exportToWorkOn != null && exportToWorkOn.ClassName == "WwiseStream")
            {
                WwiseStream w = new WwiseStream(exportToWorkOn);
                if (w.IsPCCStored)
                {
                    //TODO: enable replacing of PCC-stored sounds
                    MessageBox.Show("Cannot replace pcc-stored sounds yet.");
                    return;
                }

                if (oggPath == null)
                {
                    OpenFileDialog d = new OpenFileDialog { Filter = "Wwise Encoded Ogg|*.ogg" };
                    bool? res = d.ShowDialog();
                    if (res.HasValue && res.Value)
                    {
                        oggPath = d.FileName;
                    }
                    else
                    {
                        return;
                    }
                }
                w.ImportFromFile(oggPath, w.getPathToAFC());
                CurrentLoadedExport.Data = w.memory.TypedClone();
                if (HostingControl != null)
                {
                    HostingControl.IsBusy = false;
                }
                MessageBox.Show("Done");
            }
        }

        public static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            //get parent item
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);

            //we've reached the end of the tree
            if (parentObject == null) return null;

            //check if the parent matches the type we're looking for
            T parent = parentObject as T;
            if (parent != null)
                return parent;
            else
                return FindParent<T>(parentObject);
        }

        private void RepeatingButton_Click(object sender, RoutedEventArgs e)
        {
            Properties.Settings.Default.SoundpanelRepeating = !Properties.Settings.Default.SoundpanelRepeating;
            Properties.Settings.Default.Save();
        }

        private void WEMItem_KeyDown(object sender, KeyEventArgs e)
        {
            if (e is KeyEventArgs ke)
            {
                if (ke.Key == Key.Space)
                {
                    if (CanStartPlayback(null))
                    {
                        StartOrPausePlaying();
                    }
                    ke.Handled = true;
                }
                if (ke.Key == Key.Escape)
                {
                    StopPlaying();
                    ke.Handled = true;
                }
            }
        }

        private void ExportInfoListBox_DoubleClick(object sender, MouseButtonEventArgs e)
        {
            object currentSelectedItem = ExportInfoListBox.SelectedItem;
            if (currentSelectedItem != null && currentSelectedItem is EmbeddedWEMFile)
            {
                StopPlaying();
                StartOrPausePlaying();
            }
            if (currentSelectedItem != null && currentSelectedItem is ISBankEntry && (currentSelectedItem as ISBankEntry).DataAsStored != null)
            {
                StopPlaying();
                StartOrPausePlaying();
            }
        }

        private void HIRC_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            HIRCNotableItems.ClearEx();
            if (HIRC_ListBox.SelectedItem is HIRCObject h)
            {
                HIRC_ListBox.ScrollIntoView(h);

                OriginalHIRCHex = h.Data;
                hircHexProvider.ReplaceBytes(h.Data);
                SoundpanelHIRC_Hexbox.Refresh();

                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = 0x0,
                    Header = $"Type: 0x{h.ObjType:X2}",
                    Length = 1
                });

                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = 0x1,
                    Header = $"Size: 0x{h.Size:X8}",
                    Length = 4
                });

                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Offset = 0x5,
                    Header = $"Object ID: 0x{h.ID:X8}",
                    Length = 4
                });

                int start = 0x9;
                switch (h.ObjType)
                {
                    case HIRCObject.TYPE_SOUNDSFXVOICE:
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Unknown 4 bytes: 0x{h.unk1:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"State: {h.State:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Audio ID: {h.unk1:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Source ID: 0x{h.IDsource:X8}",
                            Length = 4
                        });

                        start += 4;
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"Sound Type: {h.SoundType}",
                            Length = 4
                        });
                        break;
                    case HIRCObject.TYPE_EVENT:
                        HIRCNotableItems.Add(new HIRCNotableItem
                        {
                            Offset = start,
                            Header = $"# of event actions to fire: {h.eventIDs.Count}",
                            Length = 4
                        });
                        start += 4;
                        foreach (int eventid in h.eventIDs)
                        {
                            HIRCNotableItems.Add(new HIRCNotableItem
                            {
                                Offset = start,
                                Header = $"Event action to fire: 0x{eventid:X8}",
                                Length = 4
                            });
                            start += 4;
                        }
                        break;
                }
            }
            else
            {
                HIRCNotableItems.Add(new HIRCNotableItem
                {
                    Header = "Select a HIRC object"
                });

                OriginalHIRCHex = null;
                hircHexProvider.ClearBytes();
                SoundpanelHIRC_Hexbox.Refresh();
            }
        }

        private bool ControlLoaded;
        private void Soundpanel_Loaded(object sender, RoutedEventArgs e)
        {
            if (!ControlLoaded)
            {
                SoundpanelHIRC_Hexbox = (HexBox)HIRC_Hexbox_Host.Child;
                hircHexProvider = new DynamicByteProvider();

                SoundpanelHIRC_Hexbox.ByteProvider = hircHexProvider;
                SoundpanelHIRC_Hexbox.ByteProvider.Changed += SoundpanelHIRC_Hexbox_BytesChanged;
                ControlLoaded = true;
            }
        }

        private void SoundpanelHIRC_Hexbox_BytesChanged(object sender, EventArgs e)
        {
            if (OriginalHIRCHex != null)
            {
                HIRCHexChanged = !hircHexProvider.Bytes.SequenceEqual(OriginalHIRCHex);
            }
        }

        private void Soundpanel_HIRCHexbox_SelectionChanged(object sender, EventArgs e)
        {
            if (CurrentLoadedExport != null)
            {
                DynamicByteProvider hbp = SoundpanelHIRC_Hexbox.ByteProvider as DynamicByteProvider;
                byte[] memory = hbp.Bytes.ToArray();
                int start = (int)SoundpanelHIRC_Hexbox.SelectionStart;
                int len = (int)SoundpanelHIRC_Hexbox.SelectionLength;
                int size = (int)SoundpanelHIRC_Hexbox.ByteProvider.Length;
                try
                {
                    if (memory.Length > 0 && start != -1 && start < size)
                    {
                        string s = $"Byte: {memory[start]}"; //if selection is same as size this will crash.
                        if (start <= memory.Length - 4)
                        {
                            int val = BitConverter.ToInt32(memory, start);
                            float fval = BitConverter.ToSingle(memory, start);
                            s += $", Int: {val} (0x{val:X8}) Float: {fval}";
                            HIRCObject referencedHIRCbyID = HIRCObjects.FirstOrDefault(x => x.ID == val);

                            if (referencedHIRCbyID != null)
                            {
                                s += $", HIRC Object (by ID) Index: {referencedHIRCbyID.Index}";
                            }

                            EmbeddedWEMFile referencedWEMbyID = AllWems.FirstOrDefault(x => x.Id == val);

                            if (referencedWEMbyID != null)
                            {
                                s += $", Embedded WEM Object (by ID): {referencedWEMbyID.DisplayString}";
                            }
                            //if (CurrentLoadedExport.FileRef.getEntry(val) is IExportEntry exp)
                            //{
                            //    s += $", Export: {exp.ObjectName}";
                            //}
                            //else if (CurrentLoadedExport.FileRef.getEntry(val) is ImportEntry imp)
                            //{
                            //    s += $", Import: {imp.ObjectName}";
                            //}
                        }
                        s += $" | Start=0x{start:X8} ";
                        if (len > 0)
                        {
                            s += $"Length=0x{len:X8} ";
                            s += $"End=0x{(start + len - 1):X8}";
                        }
                        HIRCStatusBar_LeftMostText.Text = s;
                    }
                    else
                    {
                        HIRCStatusBar_LeftMostText.Text = "Nothing Selected";
                    }
                }
                catch (Exception)
                {
                }
                SoundpanelHIRC_Hexbox.Refresh();
            }
        }

        private void Soundpanel_Unloaded(object sender, RoutedEventArgs e)
        {
            seekbarUpdateTimer?.Stop();
        }

        public override void Dispose()
        {
            FreeAudioResources();
            SoundpanelHIRC_Hexbox = null;
            HIRC_Hexbox_Host.Child.Dispose();
            HIRC_Hexbox_Host.Dispose();
            CurrentLoadedWwisebank = null;
        }

        public bool HasPendingHIRCChanges => HIRCObjects.Any(x => x.DataChanged);

        private byte[] OriginalHIRCHex;

        private void HIRC_ToggleHexboxWidth_Click(object sender, RoutedEventArgs e)
        {
            GridLength len = HexboxColumnDefinition.Width;
            if (len.Value < HexboxColumnDefinition.MaxWidth)
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MaxWidth);
            }
            else
            {
                HexboxColumnDefinition.Width = new GridLength(HexboxColumnDefinition.MinWidth);
            }
        }

        private void Searchbox_OnKeyUpHandler(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return && CanSearchHIRCHex())
            {
                SearchHIRCHex();
            }
        }

        public class HIRCNotableItem
        {
            public int Offset { get; set; }
            public string Header { get; set; }
            public int Length { get; internal set; }
            public override string ToString() => $"0x{Offset:X6}: {Header}";
        }

        private void HIRCNotableItems_ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            SoundpanelHIRC_Hexbox.UnhighlightAll();
            if (HIRCNotableItems_ListBox.SelectedItem is HIRCNotableItem h)
            {
                SoundpanelHIRC_Hexbox.Highlight(h.Offset, h.Length);
                SoundpanelHIRC_Hexbox.SelectionStart = h.Offset;
                SoundpanelHIRC_Hexbox.SelectionLength = 1;
            }
        }
    }

    public class EmbeddedWEMFile
    {
        public uint Id;
        public bool HasBeenFixed;
        public MEGame Game;
        public EmbeddedWEMFile(byte[] WemData, string DisplayString, MEGame game, uint Id = 0)
        {
            this.Id = Id;
            this.Game = game;
            this.WemData = WemData;
            this.DisplayString = DisplayString;


            int size = BitConverter.ToInt32(WemData, 4);
            int subchunk2size = BitConverter.ToInt32(WemData, 0x5A);

            if (size != WemData.Length - 8)
            {
                OriginalWemData = WemData.TypedClone(); //store copy of the original data in the event the user rewrites a WEM

                //Some clips in ME3 are just the intro to the audio. The raw data is literally cutoff and the first ~.5 seconds are inserted into the soundbank.
                //In order to attempt to even listen to these we have to fix the headers for size and subchunk2size.
                size = WemData.Length - 8;
                HasBeenFixed = true;
                this.DisplayString += " - Preloading";
                int offset = 4;
                WemData[offset] = (byte)size; // fourth byte
                WemData[offset + 1] = (byte)(size >> 8); // third byte
                WemData[offset + 2] = (byte)(size >> 16); // second byte
                WemData[offset + 3] = (byte)(size >> 24); // last byte

                offset = 0x5A; //Subchunk2 size offset
                size = WemData.Length - 94; //size of data to follow
                WemData[offset] = (byte)size; // fourth byte
                WemData[offset + 1] = (byte)(size >> 8); // third byte
                WemData[offset + 2] = (byte)(size >> 16); // second byte
                WemData[offset + 3] = (byte)(size >> 24); // last byte
            }
        }

        public byte[] WemData { get; set; }
        public byte[] OriginalWemData { get; set; }
        public string DisplayString { get; set; }
    }

    public class ImportExportSoundEnabledConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value != null;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return false; //don't need this
        }
    }

}
