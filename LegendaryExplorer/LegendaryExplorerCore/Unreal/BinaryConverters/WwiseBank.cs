using System;
using System.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using ME3Tweaks.Wwiser;
using Wwiser = ME3Tweaks.Wwiser.WwiseBank;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class WwiseBank : ObjectBinary
    {
        public uint Unk1; //Game2
        public uint Unk2; //Game2
        public byte[] BnkFile; // Raw Bank file
        
        public uint Id => Bank.BKHD.SoundBankId;
        public uint Version => Bank.BKHD.BankGeneratorVersion;
        
        public Wwiser Bank { get; set; }

        protected override void Serialize(SerializingContainer sc)
        {
            if (!sc.Game.IsGame2() && !sc.Game.IsGame3())
            {
                throw new Exception($"WwiseBank is not a valid class for {sc.Game}!");
            }

            if (sc.Game.IsGame2())
            {
                sc.Serialize(ref Unk1);
                sc.Serialize(ref Unk2);
                if (Unk1 == 0 && Unk2 == 0)
                {
                    return; //not sure what's going on here
                }
            }
            
            sc.SerializeConstInt(0); // bulk data flags
            var dataSizePos = sc.ms.Position; // come back to write size at the end
            sc.ms.BaseStream.Position += sizeof(int) * 3;
            var dataStartPos = sc.ms.Position;
            if (sc.IsLoading)
            {
                Bank = WwiseBankParser.Deserialize(sc.ms.BaseStream);
            }
            if (sc.IsSaving)
            {
                if(Bank is null) throw new Exception("WwiseBank is null, cannot serialize!");
                WwiseBankParser.Serialize(Bank, sc.ms.BaseStream);
                var size = sc.ms.Position - dataStartPos;
                sc.ms.JumpTo(dataSizePos);
                sc.ms.Writer.WriteInt32((int)size);
                sc.ms.Writer.WriteInt32((int)size);
                sc.SerializeFileOffset();
            }
        }
        
        public static WwiseBank Create()
        {
            return new()
            {
                BnkFile = []
            };
        }
        
        /// <summary>
        /// Utility method: Writes the raw bytes of a bank to an export's binary.
        /// </summary>
        /// <param name="bankData"></param>
        /// <param name="exp"></param>
        public static void WriteBankRaw(byte[] bankData, ExportEntry exp)
        {
            MemoryStream outStream = new MemoryStream((exp.Game == MEGame.LE2 ? 24 : 16) + bankData.Length); // This must exist or GetBuffer() will return the wrong size.

            if (exp.Game == MEGame.LE2)
            {
                // Write Bulk Data header
                outStream.WriteInt32(0x1); // Unknown
                outStream.WriteInt32(0x1); // Unknown
            }

            // Write Bulk Data header
            outStream.WriteInt32(0); // Local
            outStream.WriteInt32((int)bankData.Length); // Compressed size
            outStream.WriteInt32((int)bankData.Length); // Decompressed size
            outStream.WriteInt32(0); // Data offset - this is not external so this is not used

            outStream.Write(bankData);
            exp.WriteBinary(outStream.GetBuffer());
        }
    }
}
