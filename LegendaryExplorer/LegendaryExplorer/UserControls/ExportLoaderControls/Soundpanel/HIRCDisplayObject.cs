using System.Collections.Generic;
using System.IO;
using System.Linq;
using BinarySerialization;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using ME3Tweaks.Wwiser;
using ME3Tweaks.Wwiser.Model.Hierarchy;
using WwiseBank = ME3Tweaks.Wwiser.WwiseBank;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.Soundpanel
{
    public class HIRCDisplayObject : NotifyPropertyChangedBase
    {
        private HircItemContainer _item;

        public HircItemContainer Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }
        
        private BankSerializationContext _context;
        
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

        /*public HIRCDisplayObject(int i, WwiseBankParsed.HIRCObject src, MEGame game)
        {
            Data = src.ToBytes(game);
            Index = i;
            ObjType = (byte)src.Type;
            ID = src.ID;
            switch (src)
            {
                case WwiseBankParsed.SoundSFXVoice sfxVoice:
                    unk1 = sfxVoice.Unk1;
                    SourceID = sfxVoice.SourceID;
                    AudioID = sfxVoice.AudioID;
                    SoundType = (byte)sfxVoice.SoundType;
                    break;
                case WwiseBankParsed.Event eventHIRC:
                    EventIDs = eventHIRC.EventActions.Clone();
                    break;
            }
        }*/

        public HIRCDisplayObject(int i, HircItemContainer item, byte[] data, BankSerializationContext context)
        {
            Index = i;
            Item = item;
            Data = data;
            _context = context;
        }

        public void SaveData(byte[] toArray)
        {
            Data = toArray;
            DataChanged = true;
        }

        public void Commit()
        {
            if (DataChanged)
            {
                var serializer = new BinarySerializer();
                using var stream = new MemoryStream(Data);
                Item = serializer.Deserialize<HircItemContainer>(stream, _context);
                DataChanged = false;
            }
        }

        public static IEnumerable<HIRCDisplayObject> CreateFromBank(WwiseBank bank)
        {
            if (bank.HIRC is null || !bank.HIRC.Items.Any()) yield break;
            
            var context = new BankSerializationContext(bank.BKHD.BankGeneratorVersion, false, bank.BKHD.FeedbackInBank);
            var serializer = new BinarySerializer();
            using var stream = new MemoryStream();
            for (int i = 0; i < bank.HIRC.Items.Count; i++)
            {
                var item = bank.HIRC.Items[i];
                serializer.Serialize(stream, item, context);
                yield return new HIRCDisplayObject(i, item, stream.ToArray(), context);
                stream.SetLength(0); // Reset the stream for the next item
            }
        }
    }
}
