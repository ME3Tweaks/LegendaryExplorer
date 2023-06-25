using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.Unreal.BinaryConverters
{
    public class UFunction : UStruct
    {
        public ushort NativeIndex;
        public byte OperatorPrecedence; //ME1/2/UDK
        public EFunctionFlags FunctionFlags;
        public ushort ReplicationOffset; //ME1/2/UDK
        public NameReference FriendlyName; //ME1/2/UDK
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NativeIndex);
            if (sc.Game.IsGame1() || sc.Game.IsGame2() || sc.Game is MEGame.UDK) //This is present on PS3 ME1/ME2 but not ME3 for some reason.
            {
                sc.Serialize(ref OperatorPrecedence);
            }
            sc.Serialize(ref FunctionFlags);
            if (sc.Game is MEGame.ME1 or MEGame.ME2 or MEGame.UDK && sc.Pcc.Platform != MEPackage.GamePlatform.PS3 && FunctionFlags.Has(EFunctionFlags.Net))
            {
                sc.Serialize(ref ReplicationOffset);
            }
            if ((sc.Game.IsGame1() || sc.Game.IsGame2() || sc.Game is MEGame.UDK) && sc.Pcc.Platform != MEPackage.GamePlatform.PS3)
            {
                sc.Serialize(ref FriendlyName);
            }
            else if (sc.IsLoading)
            {
                FriendlyName = "None";
            }
        }

        public static UFunction Create()
        {
            return new()
            {
                SuperClass = 0,
                Next = 0,
                Children = 0,
                ScriptBytecodeSize = 2,
                ScriptStorageSize = 2,
                ScriptBytes = new byte[] { 0xB, 0x53 },
                FriendlyName = "None"
            };
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.Add((FriendlyName, nameof(FriendlyName)));

            return names;
        }
    }

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref EFunctionFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (EFunctionFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}