using System.Collections.Generic;
using System.Linq;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public class HIRCDisplayObject : NotifyPropertyChangedBase
    {
        public int Index { get; set; }

        public byte ObjType { get; set; }

        public uint ID { get; set; }

        public byte SoundType { get; set; }

        public uint State { get; set; }

        //typeinfo
        public uint unk1, AudioID, SourceID;//scope,atype;
        public List<uint> EventIDs { get; set; }

        private byte[] _data;
        public byte[] Data
        {
            get => _data;
            internal set
            {
                if (_data != null && value != null && _data.SequenceEqual(value))
                {
                    return; //if the data is the same don't write it and trigger the side effects
                }

                bool isFirstLoad = _data == null;
                _data = value;
                if (!isFirstLoad)
                {
                    DataChanged = true;
                }
            }
        }

        private bool _dataChanged;

        public bool DataChanged
        {
            get => _dataChanged;
            internal set => SetProperty(ref _dataChanged, value);
        }

        public HIRCDisplayObject Clone()
        {
            HIRCDisplayObject clone = (HIRCDisplayObject)MemberwiseClone();
            clone.EventIDs = EventIDs?.Clone();
            clone.Data = Data?.ArrayClone();
            return clone;
        }

        public HIRCDisplayObject(int i, WwiseBank.HIRCObject src, MEGame game)
        {
            Data = src.ToBytes(game);
            Index = i;
            ObjType = (byte)src.Type;
            ID = src.ID;
            switch (src)
            {
                case WwiseBank.SoundSFXVoice sfxVoice:
                    unk1 = sfxVoice.Unk1;
                    State = (uint)sfxVoice.State;
                    SourceID = sfxVoice.SourceID;
                    AudioID = sfxVoice.AudioID;
                    SoundType = (byte)sfxVoice.SoundType;
                    break;
                case WwiseBank.Event eventHIRC:
                    EventIDs = eventHIRC.EventActions.Clone();
                    break;
            }
        }
    }
}
