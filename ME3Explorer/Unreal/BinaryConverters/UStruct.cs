using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public abstract class UStruct : UField
    {
        public UIndex Children;
        private int Line; //ME1/ME2
        private int TextPos; //ME1/ME2
        public int ScriptBytecodeSize; //ME3
        public int ScriptStorageSize;
        public byte[] ScriptBytes;
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            if (sc.Game <= MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
            }
            sc.Serialize(ref Children);
            if (sc.Game <= MEGame.ME2)
            {
                int dummy = 0;
                sc.Serialize(ref dummy);
                sc.Serialize(ref Line);
                sc.Serialize(ref TextPos);
            }

            if (sc.Game >= MEGame.ME3)
            {
                sc.Serialize(ref ScriptBytecodeSize);
            }
            if (sc.IsSaving)
            {
                ScriptStorageSize = ScriptBytes.Length;
            }
            sc.Serialize(ref ScriptStorageSize);
            sc.Serialize(ref ScriptBytes, ScriptStorageSize);
        }

        public override List<(UIndex, string)> GetUIndexes(MEGame game)
        {
            List<(UIndex, string)> uIndices = base.GetUIndexes(game);
            uIndices.Add((Children, "ChildListStart"));
            return uIndices;
        }
    }
}
