using System;
using System.IO;
using FontAwesome5;
using LegendaryExplorer.Misc;
using LegendaryExplorer.Tools.TlkManagerNS;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.Audio;
using LegendaryExplorerCore.Sound.ISACT;
using WwiseStream = LegendaryExplorerCore.Unreal.BinaryConverters.WwiseStream;

namespace LegendaryExplorer.Tools.Soundplorer
{
    public class AFCFileEntry : NotifyPropertyChangedBase
    {
        public short WwiseVersionMaybe;
        public bool ME2 => WwiseVersionMaybe == 0x28;

        private string _afcpath;
        private readonly Endian Endian;

        public string AFCPath
        {
            get => _afcpath;
            set => SetProperty(ref _afcpath, value);
        }

        private int _datasize;
        public int DataSize
        {
            get => _datasize;
            set => SetProperty(ref _datasize, value);
        }

        private int _offset;
        public int Offset
        {
            get => _offset;
            set => SetProperty(ref _offset, value);
        }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public AFCFileEntry(string afcpath, int offset, int size, short wwiseVersion, Endian endian)
        {
            Endian = endian;
            AFCPath = afcpath;
            DataSize = size;
            WwiseVersionMaybe = wwiseVersion;
            Offset = offset;
            SubText = "Calculating length";
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = $"AFC Entry @ 0x{Offset:X6}";
        }

        public void LoadData()
        {
            using FileStream _rawRiff = new FileStream(AFCPath, FileMode.Open);
            EndianReader reader = new EndianReader(_rawRiff) { Endian = Endian };
            reader.Position = Offset;
            //Parse RIFF header a bit
            var riffTag = reader.ReadStringASCII(4); //RIFF
            reader.ReadInt32();//size
            var wavetype = reader.ReadStringASCII(4);
            reader.ReadInt32();//'fmt '/
            var fmtsize = reader.ReadInt32(); //data should directly follow fmt
            var fmtPos = reader.Position;
            var riffFormat = reader.ReadUInt16();
            var channels = reader.ReadInt16();
            var sampleRate = reader.ReadInt32();
            var averageBPS = reader.ReadInt32();
            var blockAlign = reader.ReadInt16();
            var bitsPerSample = reader.ReadInt16();
            var extraSize = reader.ReadInt16(); //gonna need some testing on this cause there's a lot of header formats for wwise
            if (riffFormat == 0xFFFF)
            {
                double seconds = 0;

                //if (extraSize == 0x30 || extraSize == 0x06) //0x30 on PC, 0x06 on PS3 ?
                //{
                //find 'vorb' chunk (ME2)
                reader.Seek(extraSize, SeekOrigin.Current);
                var chunkName = reader.ReadStringASCII(4);
                uint numSamples = 1; //to prevent division by 0
                if (chunkName == "vorb")
                {
                    //ME2 Vorbis
                    var vorbsize = reader.ReadInt32();
                    numSamples = reader.ReadUInt32();
                }
                else if (chunkName == "data")
                {
                    //ME3 Vorbis
                    var numSamplesOffset = reader.Position = fmtPos + 0x18;
                    numSamples = reader.ReadUInt32();
                }

                seconds = (double)numSamples / sampleRate;
                //}
                //else
                //{
                //    // !!??
                //    Debug.WriteLine($"Unknown extra size in wwiseheader: 0x{extraSize:X2}");
                //}

                SubText = TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss\:fff");
            }
            else
            {
                //placeholder?
                SubText = new TimeSpan(132045).ToString(@"mm\:ss\:fff");
            }
            /*
            Stream waveStream = WwiseStream.CreateWaveStreamFromRaw(AFCPath, Offset, DataSize, ME2);
            if (waveStream != null)
            {
                //Check it is RIFF
                byte[] riffHeaderBytes = new byte[4];
                waveStream.Read(riffHeaderBytes, 0, 4);
                string wemHeader = "" + (char)riffHeaderBytes[0] + (char)riffHeaderBytes[1] + (char)riffHeaderBytes[2] + (char)riffHeaderBytes[3];
                if (wemHeader == "RIFF")
                {
                    waveStream.Position = 0;
                    WaveFileReader wf = new WaveFileReader(waveStream);
                    SubText = wf.TotalTime.ToString(@"mm\:ss\:fff");
                }
                else
                {
                    SubText = "Error getting length, may be unsupported";
                }
            }*/
            NeedsLoading = false;
            Icon = EFontAwesomeIcon.Solid_VolumeUp;
        }

        public bool IsLE()
        {
            using FileStream _rawRiff = new FileStream(AFCPath, FileMode.Open);
            EndianReader reader = new EndianReader(_rawRiff) { Endian = Endian };
            reader.Position = Offset;
            //Parse RIFF header a bit
            var riffTag = reader.ReadStringASCII(4); //RIFF
            reader.ReadInt32();//size
            var wavetype = reader.ReadStringASCII(4);
            reader.ReadInt32();//'fmt '/
            var fmtsize = reader.ReadInt32(); //data should directly follow fmt
            var fmtPos = reader.Position;
            var riffFormat = reader.ReadUInt16();
            var channels = reader.ReadInt16();
            var sampleRate = reader.ReadInt32();
            var averageBPS = reader.ReadInt32();
            var blockAlign = reader.ReadInt16();
            var bitsPerSample = reader.ReadInt16();
            var extraSize = reader.ReadInt16(); //gonna need some testing on this cause there's a lot of header formats for wwise
            return (reader.ReadInt16() == 0x00);
        }
    }

    /// <summary>
    /// Wrapper class for a sound sample in ISACT, for play in SoundPanel
    /// </summary>
    public class ISACTFileEntry : NotifyPropertyChangedBase
    {
        public ISACTListBankChunk Entry { get; set; }

        public string TLKString { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public ISACTFileEntry(ISACTListBankChunk entry)
        {
            Entry = entry;
            SubText = "Calculating stream length";
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            DisplayString = Entry.TitleInfo.Value;
        }

        public void LoadData()
        {
            // Check if there is TLK string in the export name
            var splits = Entry.TitleInfo.Value.Split('_', ',');
            for (int i = splits.Length - 1; i > 0; i--)
            {
                //backwards is faster
                if (int.TryParse(splits[i], out var parsed))
                {
                    //Lookup TLK
                    // TODO: Get some way of determining ME1/LE1 in isb entry and do it here
                    var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, MEGame.ME1, null);
                    if (data != "No Data")
                    {
                        TLKString = data;
                    }
                }
            }

            if (Entry.SampleInfo != null)
            {
                SubText = (Entry.SampleInfo.TimeLength / 1000.0f).ToString(@"mm\:ss\:fff");
                ////Debug.WriteLine("getting time for " + Entry.FileName + " Ogg: " + Entry.isOgg);
                //TimeSpan? time = Entry.GetLength();
                //if (time != null)
                //{
                //    //here backslash must be present to tell that parser colon is
                //    //not the part of format, it just a character that we want in output
                //    SubText = time.Value.ToString(@"mm\:ss\:fff");
                //}
                //else
                //{
                //    SubText = "Error getting length, may be unsupported";
                //}
            }
            else
            {
                SubText = "Sound stub only";
            }
            NeedsLoading = false;
            Icon = EFontAwesomeIcon.Solid_VolumeUp;
        }
    }

    /// <summary>
    /// Container for Soundplorer items 
    /// </summary>
    public class SoundplorerExport : NotifyPropertyChangedBase
    {
        public ExportEntry Export { get; set; }

        private bool _needsLoading;
        public bool NeedsLoading
        {
            get => _needsLoading;
            set => SetProperty(ref _needsLoading, value);
        }

        private EFontAwesomeIcon _icon;
        public EFontAwesomeIcon Icon
        {
            get => _icon;
            set => SetProperty(ref _icon, value);
        }

        private string _timeString;
        public string SubText
        {
            get => _timeString;
            set => SetProperty(ref _timeString, value);
        }

        private string _displayString;
        public string DisplayString
        {
            get => _displayString;
            set => SetProperty(ref _displayString, value);
        }

        public string _tlkString;
        public string TLKString
        {
            get => _tlkString;
            set => SetProperty(ref _tlkString, value);
        }

        public SoundplorerExport(ExportEntry export)
        {
            Export = export;
            if (Export.ClassName == "WwiseStream")
            {
                SubText = "Calculating stream length";
            }
            else
            {
                SubText = "Calculating number of embedded WEMs";
            }
            Icon = EFontAwesomeIcon.Solid_Spinner;
            NeedsLoading = true;
            UpdateDisplay();
        }

        private void UpdateDisplay()
        {
            int paddingSize = Export.FileRef.ExportCount.ToString().Length;
            DisplayString = $"{Export.UIndex.ToString("d" + paddingSize)}: {Export.ObjectName.Instanced}";
        }

        public void LoadData()
        {
            switch (Export.ClassName)
            {
                case "WwiseStream":
                    {
                        // Check if there is TLK string in the export name
                        var splits = Export.ObjectName.Name.Split('_', ',');
                        for (int i = splits.Length - 1; i > 0; i--)
                        {
                            //backwards is faster
                            if (int.TryParse(splits[i], out var parsed))
                            {
                                //Lookup TLK
                                var data = TLKManagerWPF.GlobalFindStrRefbyID(parsed, Export.FileRef);
                                if (data != "No Data")
                                {
                                    TLKString = data;
                                }
                            }
                        }

                        WwiseStream w = Export.GetBinaryData<WwiseStream>();
                        if (!w.IsPCCStored && w.GetPathToAFC() == "")
                        {
                            //AFC not found.
                            SubText = $"AFC unavailable: {w.Filename}";
                        }
                        else
                        {
                            var length = w.GetAudioInfo()?.GetLength();
                            if (length != null)
                            {
                                //here backslash must be present to tell that parser colon is
                                //not the part of format, it just a character that we want in output
                                SubText = length.Value.ToString(@"mm\:ss\:fff");
                            }
                            else
                            {
                                SubText = "Error getting length, may be unsupported";
                            }
                        }

                        //string afcPath = w.GetPathToAFC();
                        //if (afcPath == "")
                        //{
                        //    SubText = "Could not find AFC";
                        //}
                        //else
                        //{
                        //    TimeSpan? time = w.GetSoundLength();
                        //    if (time != null)
                        //    {
                        //        //here backslash must be present to tell that parser colon is
                        //        //not the part of format, it just a character that we want in output
                        //        SubText = time.Value.ToString(@"mm\:ss\:fff");
                        //    }
                        //    else
                        //    {
                        //        SubText = "Error getting length, may be unsupported";
                        //    }
                        //}
                        NeedsLoading = false;
                        Icon = EFontAwesomeIcon.Solid_VolumeUp;
                        break;
                    }
                case "WwiseBank":
                    {
                        var bank = Export.GetBinaryData<WwiseBank>();
                        SubText = $"{bank.EmbeddedFiles.Count} embedded WEM{(bank.EmbeddedFiles.Count != 1 ? "s" : "")}";
                        NeedsLoading = false;
                        Icon = EFontAwesomeIcon.Solid_University;
                        break;
                    }
                case "SoundNodeWave":
                    SubText = "";
                    NeedsLoading = false;
                    Icon = EFontAwesomeIcon.Solid_VolumeUp;
                    break;
            }
        }
    }
}
