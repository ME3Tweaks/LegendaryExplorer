using System;
using LegendaryExplorerCore.Sound.ISACT;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class BioSoundNodeWaveStreamingData : ObjectBinary
    {
        public ISACTBankPair BankPair;

        protected override void Serialize(SerializingContainer2 sc)
        {
            int dataSize = 0;
            sc.Serialize(ref dataSize);
            long startPos = sc.ms.Position;
            int isbOffset = 0;
            sc.Serialize(ref isbOffset);
            if (sc.IsLoading)
            {
                BankPair = new ISACTBankPair();
                BankPair.ICBBank = new ISACTBank(sc.ms.BaseStream);
                if (BankPair.ICBBank.BankType is not ISACTBankType.ICB)
                {
                    throw new Exception($"Expected first bank to be an ICB, not a {BankPair.ICBBank.BankType}");
                }
                BankPair.ISBBank = new ISACTBank(sc.ms.BaseStream);
                if (BankPair.ISBBank.BankType is not ISACTBankType.ISB)
                {
                    throw new Exception($"Expected second bank to be an ISB, not an {BankPair.ICBBank.BankType}");
                }
            }
            else
            {
                BankPair.ICBBank.Write(sc.ms.BaseStream);
                isbOffset = (int)(sc.ms.Position - startPos);
                BankPair.ISBBank.Write(sc.ms.BaseStream);
                dataSize = (int)(sc.ms.Position - startPos);
                long endPos = sc.ms.Position;
                sc.ms.JumpTo(startPos - 4);
                sc.Serialize(ref dataSize);
                sc.Serialize(ref isbOffset);
                sc.ms.JumpTo(endPos);
            }
        }

        public static BioSoundNodeWaveStreamingData Create()
        {
            //Is there a way to create an empty ISACTBankPair in a way that makes any sense?
            return new()
            {
            };
        }
    }
}
