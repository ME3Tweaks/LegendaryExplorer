using System.Collections.Generic;
using System.IO;
using System.Linq;
using BinarySerialization;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Helpers;
using ME3Tweaks.Wwiser;
using ME3Tweaks.Wwiser.Model.Hierarchy;
using WwiseBank = ME3Tweaks.Wwiser.WwiseBank;

namespace LegendaryExplorer.UserControls.ExportLoaderControls.Soundpanel
{
    public class HIRCDisplayObject : NotifyPropertyChangedBase
    {
        public HIRCDisplayObject(int i, HircItemContainer item, byte[] data, BankSerializationContext context)
        {
            Index = i;
            Item = item;
            Data = data;
            Context = context;
        }
        
        public int Index { get; set; }
        
        private HircItemContainer _item;

        public HircItemContainer Item
        {
            get => _item;
            set => SetProperty(ref _item, value);
        }

        private byte[] _data;
        public byte[] Data
        {
            get => _data;
            internal set => _data = value;
        }

        private bool _dataChanged;

        public bool DataChanged
        {
            get => _dataChanged;
            internal set => SetProperty(ref _dataChanged, value);
        }
        
        internal BankSerializationContext Context { get; set; }

        public HIRCDisplayObject Clone()
        {
            var clone = new HIRCDisplayObject(Index, Item, Data?.ArrayClone(), Context);

            if (Data != null)
            {
                // Effectively a deep clone of the Item
                var serializer = new BinarySerializer();
                using var stream = new MemoryStream(clone.Data);
                clone.Item = serializer.Deserialize<HircItemContainer>(stream, Context);
                
                // Increment the ID to ensure uniqueness
                clone.Item.Item.Id++;
                
                // Serialize the data back - this is literally just for the updated ID lmao
                stream.SetLength(0);
                serializer.Serialize(stream, clone.Item, clone.Context);
                clone.Data = stream.ToArray();
                clone.DataChanged = true;
            }
            return clone;
        }

        /// <summary>
        /// Commit the hex data back to the HircItemContainer
        /// </summary>
        public void CommitHex(byte[] data)
        {
            if(data is null || data.Length == 0)
            {
                return;
            }
            
            if (_data != null && _data.SequenceEqual(data))
            {
                return; //if the data is the same don't write it and trigger the side effects
            }
            
            Data = data;
            var serializer = new BinarySerializer();
            using var stream = new MemoryStream(Data);
            Item = serializer.Deserialize<HircItemContainer>(stream, Context);
            DataChanged = true;
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
