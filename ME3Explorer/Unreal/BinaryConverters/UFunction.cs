using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using StreamHelpers;

namespace ME3Explorer.Unreal.BinaryConverters
{
    public class UFunction : UStruct
    {
        public ushort NativeIndex;
        public byte OperatorPrecedence; //ME1/2
        public FunctionFlags FunctionFlags;
        public ushort ReplicationOffset; //ME1/2
        public NameReference FriendlyName; //ME1/2
        protected override void Serialize(SerializingContainer2 sc)
        {
            base.Serialize(sc);
            sc.Serialize(ref NativeIndex);
            if (sc.Game <= MEGame.ME2)
            {
                sc.Serialize(ref OperatorPrecedence);
            }
            sc.Serialize(ref FunctionFlags);
            if (sc.Game <= MEGame.ME2 && FunctionFlags.HasFlag(FunctionFlags.Net))
            {
                sc.Serialize(ref ReplicationOffset);
            }
            if (sc.Game <= MEGame.ME2)
            {
                sc.Serialize(ref FriendlyName);
            }
        }

        public override List<(NameReference, string)> GetNames(MEGame game)
        {
            var names = base.GetNames(game);

            names.Add((FriendlyName, nameof(FriendlyName)));

            return names;
        }
    }

    public enum FunctionFlags : uint
    {
        Final = 0x00000001,
        Defined = 0x00000002,
        Iterator = 0x00000004,
        Latent = 0x00000008,
        PreOperator = 0x00000010,
        Singular = 0x00000020,
        Net = 0x00000040,
        NetReliable = 0x00000080,
        Simulated = 0x00000100,
        Exec = 0x00000200,
        Native = 0x00000400,
        Event = 0x00000800,
        Operator = 0x00001000,
        Static = 0x00002000,
        HasOptionalParms = 0x00004000,
        Const = 0x00008000,

        Public = 0x00020000,
        Private = 0x00040000,
        Protected = 0x00080000,
        Delegate = 0x00100000,
        NetServer = 0x00200000,
        HasOutParms = 0x00400000,
        HasDefaults = 0x00800000,
        NetClient = 0x01000000,
        DLLImport = 0x02000000,
    }
}

namespace ME3Explorer
{
    using Unreal.BinaryConverters;

    public static partial class SCExt
    {
        public static void Serialize(this SerializingContainer2 sc, ref FunctionFlags flags)
        {
            if (sc.IsLoading)
            {
                flags = (FunctionFlags)sc.ms.ReadUInt32();
            }
            else
            {
                sc.ms.Writer.WriteUInt32((uint)flags);
            }
        }
    }
}
