using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    // TODO: this is a hacky way to do it, should later do lookups based on extracted standard library and proper native table.
    public enum NativeType
    {
        Function,
        Operator,
        PreOperator,
        PostOperator
    }

    public struct NativeTableEntry
    {
        public String Name;
        public NativeType Type;
        public int Precedence;
    }

    public partial class ME3ByteCodeDecompiler
    {
        public Dictionary<int, NativeTableEntry> NativeTable = new Dictionary<int, NativeTableEntry>() 
        {
            { 0x81, new NativeTableEntry() { Name="!", Type=NativeType.PreOperator} },
            { 0x82, new NativeTableEntry() { Name="&&", Type=NativeType.Operator, Precedence=30} },
            { 0x83, new NativeTableEntry() { Name="^^", Type=NativeType.Operator, Precedence=30} },
            { 0x84, new NativeTableEntry() { Name="||", Type=NativeType.Operator, Precedence=32} },
            { 0x85, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0x86, new NativeTableEntry() { Name="/=", Type=NativeType.Operator, Precedence=34} },
            { 0x87, new NativeTableEntry() { Name="+=", Type=NativeType.Operator, Precedence=34} },
            { 0x88, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=34} },
            { 0x89, new NativeTableEntry() { Name="++", Type=NativeType.PreOperator} },
            { 0x8A, new NativeTableEntry() { Name="--", Type=NativeType.PreOperator} },
            { 0x8B, new NativeTableEntry() { Name="++", Type=NativeType.PostOperator} },
            { 0x8C, new NativeTableEntry() { Name="--", Type=NativeType.PostOperator} },
            { 0x8D, new NativeTableEntry() { Name="~", Type=NativeType.PreOperator} },
            { 0x8E, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x8F, new NativeTableEntry() { Name="-", Type=NativeType.PreOperator} },
            { 0x90, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0x91, new NativeTableEntry() { Name="/", Type=NativeType.Operator, Precedence=16} },
            { 0x92, new NativeTableEntry() { Name="+", Type=NativeType.Operator, Precedence=20} },
            { 0x93, new NativeTableEntry() { Name="-", Type=NativeType.Operator, Precedence=20} },
            { 0x94, new NativeTableEntry() { Name="<<", Type=NativeType.Operator, Precedence=22} },
            { 0x95, new NativeTableEntry() { Name=">>", Type=NativeType.Operator, Precedence=22} },
            { 0x96, new NativeTableEntry() { Name="<", Type=NativeType.Operator, Precedence=24} },
            { 0x97, new NativeTableEntry() { Name=">", Type=NativeType.Operator, Precedence=24} },
            { 0x98, new NativeTableEntry() { Name="<=", Type=NativeType.Operator, Precedence=24} },
            { 0x99, new NativeTableEntry() { Name=">=", Type=NativeType.Operator, Precedence=24} },
            { 0x9A, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x9B, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x9C, new NativeTableEntry() { Name="&", Type=NativeType.Operator, Precedence=28} },
            { 0x9D, new NativeTableEntry() { Name="^", Type=NativeType.Operator, Precedence=28} },
            { 0x9E, new NativeTableEntry() { Name="|", Type=NativeType.Operator, Precedence=28} },
            { 0x9F, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0xA0, new NativeTableEntry() { Name="/=", Type=NativeType.Operator, Precedence=34} },
            { 0xA1, new NativeTableEntry() { Name="+=", Type=NativeType.Operator, Precedence=34} },
            { 0xA2, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=34} },
            { 0xA3, new NativeTableEntry() { Name="++", Type=NativeType.PreOperator} },
            { 0xA4, new NativeTableEntry() { Name="--", Type=NativeType.PreOperator} },
            { 0xA5, new NativeTableEntry() { Name="++", Type=NativeType.PostOperator} },
            { 0xA6, new NativeTableEntry() { Name="--", Type=NativeType.PostOperator} },
            { 0xA7, new NativeTableEntry() { Name="Rand", Type=NativeType.Function} },
            { 0xA8, new NativeTableEntry() { Name="@", Type=NativeType.Operator, Precedence=40} },
            { 0xA9, new NativeTableEntry() { Name="-", Type=NativeType.PreOperator} },
            { 0xAA, new NativeTableEntry() { Name="**", Type=NativeType.Operator, Precedence=12} },
            { 0xAB, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0xAC, new NativeTableEntry() { Name="/", Type=NativeType.Operator, Precedence=16} },
            { 0xAD, new NativeTableEntry() { Name="%", Type=NativeType.Operator, Precedence=18} },
            { 0xAE, new NativeTableEntry() { Name="+", Type=NativeType.Operator, Precedence=20} },
            { 0xAF, new NativeTableEntry() { Name="-", Type=NativeType.Operator, Precedence=20} },
            { 0xB0, new NativeTableEntry() { Name="<", Type=NativeType.Operator, Precedence=24} },
            { 0xB1, new NativeTableEntry() { Name=">", Type=NativeType.Operator, Precedence=24} },
            { 0xB2, new NativeTableEntry() { Name="<=", Type=NativeType.Operator, Precedence=24} },
            { 0xB3, new NativeTableEntry() { Name=">=", Type=NativeType.Operator, Precedence=24} },
            { 0xB4, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0xB5, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0xB6, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0xB7, new NativeTableEntry() { Name="/=", Type=NativeType.Operator, Precedence=34} },
            { 0xB8, new NativeTableEntry() { Name="+=", Type=NativeType.Operator, Precedence=34} },
            { 0xB9, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=34} },
            { 0xBA, new NativeTableEntry() { Name="Abs", Type=NativeType.Function} },
            { 0xBB, new NativeTableEntry() { Name="Sin", Type=NativeType.Function} },
            { 0xBC, new NativeTableEntry() { Name="Cos", Type=NativeType.Function} },
            { 0xBD, new NativeTableEntry() { Name="Tan", Type=NativeType.Function} },
            { 0xBE, new NativeTableEntry() { Name="Atan", Type=NativeType.Function} },
            { 0xBF, new NativeTableEntry() { Name="Exp", Type=NativeType.Function} },
            { 0xC0, new NativeTableEntry() { Name="Loge", Type=NativeType.Function} },
            { 0xC1, new NativeTableEntry() { Name="Sqrt", Type=NativeType.Function} },
            { 0xC2, new NativeTableEntry() { Name="Square", Type=NativeType.Function} },
            { 0xC3, new NativeTableEntry() { Name="FRand", Type=NativeType.Function} },
            { 0xC4, new NativeTableEntry() { Name=">>>", Type=NativeType.Operator, Precedence=22} },
            { 0xC5, new NativeTableEntry() { Name="IsA", Type=NativeType.Function} },
            { 0xC6, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0xC7, new NativeTableEntry() { Name="Round", Type=NativeType.Function} },
            { 0xC9, new NativeTableEntry() { Name="Repl", Type=NativeType.Function} },
            { 0xCB, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0xD2, new NativeTableEntry() { Name="~=", Type=NativeType.Operator, Precedence=24} },
            { 0xD3, new NativeTableEntry() { Name="-", Type=NativeType.PreOperator} },
            { 0xD4, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0xD5, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0xD6, new NativeTableEntry() { Name="/", Type=NativeType.Operator, Precedence=16} },
            { 0xD7, new NativeTableEntry() { Name="+", Type=NativeType.Operator, Precedence=20} },
            { 0xD8, new NativeTableEntry() { Name="-", Type=NativeType.Operator, Precedence=20} },
            { 0xD9, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0xDA, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0xDB, new NativeTableEntry() { Name="Dot", Type=NativeType.Operator, Precedence=16} },
            { 0xDC, new NativeTableEntry() { Name="Cross", Type=NativeType.Operator, Precedence=16} },
            { 0xDD, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0xDE, new NativeTableEntry() { Name="/=", Type=NativeType.Operator, Precedence=34} },
            { 0xDF, new NativeTableEntry() { Name="+=", Type=NativeType.Operator, Precedence=34} },
            { 0xE0, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=34} },
            { 0xE1, new NativeTableEntry() { Name="VSize", Type=NativeType.Function} },
            { 0xE2, new NativeTableEntry() { Name="Normal", Type=NativeType.Function} },
            { 0xE5, new NativeTableEntry() { Name="GetAxes", Type=NativeType.Function} },
            { 0xE6, new NativeTableEntry() { Name="GetUnAxes", Type=NativeType.Function} },
            { 0xE7, new NativeTableEntry() { Name="LogInternal", Type=NativeType.Function} },
            { 0xE8, new NativeTableEntry() { Name="WarnInternal", Type=NativeType.Function} },
            { 0xEA, new NativeTableEntry() { Name="Right", Type=NativeType.Function} },
            { 0xEB, new NativeTableEntry() { Name="Caps", Type=NativeType.Function} },
            { 0xEC, new NativeTableEntry() { Name="Chr", Type=NativeType.Function} },
            { 0xED, new NativeTableEntry() { Name="Asc", Type=NativeType.Function} },
            { 0xEE, new NativeTableEntry() { Name="Locs", Type=NativeType.Function} },
            { 0xF2, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0xF3, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0xF4, new NativeTableEntry() { Name="FMin", Type=NativeType.Function} },
            { 0xF5, new NativeTableEntry() { Name="FMax", Type=NativeType.Function} },
            { 0xF6, new NativeTableEntry() { Name="FClamp", Type=NativeType.Function} },
            { 0xF7, new NativeTableEntry() { Name="Lerp", Type=NativeType.Function} },
            { 0xF9, new NativeTableEntry() { Name="Min", Type=NativeType.Function} },
            { 0xFA, new NativeTableEntry() { Name="Max", Type=NativeType.Function} },
            { 0xFB, new NativeTableEntry() { Name="Clamp", Type=NativeType.Function} },
            { 0xFC, new NativeTableEntry() { Name="VRand", Type=NativeType.Function} },
            { 0xFD, new NativeTableEntry() { Name="%", Type=NativeType.Operator, Precedence=18} },
            { 0xFE, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0xFF, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x100, new NativeTableEntry() { Name="Sleep", Type=NativeType.Function} },
            { 0x102, new NativeTableEntry() { Name="ClassIsChildOf", Type=NativeType.Function} },
            { 0x105, new NativeTableEntry() { Name="FinishAnim", Type=NativeType.Function} },
            { 0x106, new NativeTableEntry() { Name="SetCollision", Type=NativeType.Function} },
            { 0x10A, new NativeTableEntry() { Name="Move", Type=NativeType.Function} },
            { 0x10B, new NativeTableEntry() { Name="SetLocation", Type=NativeType.Function} },
            { 0x10E, new NativeTableEntry() { Name="+", Type=NativeType.Operator, Precedence=20} },
            { 0x10F, new NativeTableEntry() { Name="-", Type=NativeType.Operator, Precedence=20} },
            { 0x110, new NativeTableEntry() { Name="SetOwner", Type=NativeType.Function} },
            { 0x113, new NativeTableEntry() { Name="<<", Type=NativeType.Operator, Precedence=22} },
            { 0x114, new NativeTableEntry() { Name=">>", Type=NativeType.Operator, Precedence=22} },
            { 0x115, new NativeTableEntry() { Name="Trace", Type=NativeType.Function} },
            { 0x117, new NativeTableEntry() { Name="Destroy", Type=NativeType.Function} },
            { 0x118, new NativeTableEntry() { Name="SetTimer", Type=NativeType.Function} },
            { 0x119, new NativeTableEntry() { Name="IsInState", Type=NativeType.Function} },
            { 0x11B, new NativeTableEntry() { Name="SetCollisionSize", Type=NativeType.Function} },
            { 0x11C, new NativeTableEntry() { Name="GetStateName", Type=NativeType.Function} },
            { 0x11F, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0x120, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0x121, new NativeTableEntry() { Name="/", Type=NativeType.Operator, Precedence=16} },
            { 0x122, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0x123, new NativeTableEntry() { Name="/=", Type=NativeType.Operator, Precedence=34} },
            { 0x128, new NativeTableEntry() { Name="*", Type=NativeType.Operator, Precedence=16} },
            { 0x129, new NativeTableEntry() { Name="*=", Type=NativeType.Operator, Precedence=34} },
            { 0x12A, new NativeTableEntry() { Name="SetBase", Type=NativeType.Function} },
            { 0x12B, new NativeTableEntry() { Name="SetRotation", Type=NativeType.Function} },
            { 0x12C, new NativeTableEntry() { Name="MirrorVectorByNormal", Type=NativeType.Function} },
            { 0x130, new NativeTableEntry() { Name="AllActors", Type=NativeType.Function} },
            { 0x131, new NativeTableEntry() { Name="ChildActors", Type=NativeType.Function} },
            { 0x132, new NativeTableEntry() { Name="BasedActors", Type=NativeType.Function} },
            { 0x133, new NativeTableEntry() { Name="TouchingActors", Type=NativeType.Function} },
            { 0x135, new NativeTableEntry() { Name="TraceActors", Type=NativeType.Function} },
            { 0x137, new NativeTableEntry() { Name="VisibleActors", Type=NativeType.Function} },
            { 0x138, new NativeTableEntry() { Name="VisibleCollidingActors", Type=NativeType.Function} },
            { 0x139, new NativeTableEntry() { Name="DynamicActors", Type=NativeType.Function} },
            { 0x13C, new NativeTableEntry() { Name="+", Type=NativeType.Operator, Precedence=20} },
            { 0x13D, new NativeTableEntry() { Name="-", Type=NativeType.Operator, Precedence=20} },
            { 0x13E, new NativeTableEntry() { Name="+=", Type=NativeType.Operator, Precedence=34} },
            { 0x13F, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=34} },
            { 0x140, new NativeTableEntry() { Name="RotRand", Type=NativeType.Function} },
            { 0x141, new NativeTableEntry() { Name="CollidingActors", Type=NativeType.Function} },
            { 0x142, new NativeTableEntry() { Name="$=", Type=NativeType.Operator, Precedence=44} },
            { 0x143, new NativeTableEntry() { Name="@=", Type=NativeType.Operator, Precedence=44} },
            { 0x144, new NativeTableEntry() { Name="-=", Type=NativeType.Operator, Precedence=45} },
            { 0x1D4, new NativeTableEntry() { Name="DrawTileClipped", Type=NativeType.Function} },
            { 0x1F4, new NativeTableEntry() { Name="MoveTo", Type=NativeType.Function} },
            { 0x1F6, new NativeTableEntry() { Name="MoveToward", Type=NativeType.Function} },
            { 0x1FC, new NativeTableEntry() { Name="FinishRotation", Type=NativeType.Function} },
            { 0x200, new NativeTableEntry() { Name="MakeNoise", Type=NativeType.Function} },
            { 0x202, new NativeTableEntry() { Name="LineOfSightTo", Type=NativeType.Function} },
            { 0x205, new NativeTableEntry() { Name="FindPathToward", Type=NativeType.Function} },
            { 0x206, new NativeTableEntry() { Name="FindPathTo", Type=NativeType.Function} },
            { 0x208, new NativeTableEntry() { Name="ActorReachable", Type=NativeType.Function} },
            { 0x209, new NativeTableEntry() { Name="PointReachable", Type=NativeType.Function} },
            { 0x20C, new NativeTableEntry() { Name="FindStairRotation", Type=NativeType.Function} },
            { 0x20D, new NativeTableEntry() { Name="FindRandomDest", Type=NativeType.Function} },
            { 0x20E, new NativeTableEntry() { Name="PickWallAdjust", Type=NativeType.Function} },
            { 0x20F, new NativeTableEntry() { Name="WaitForLanding", Type=NativeType.Function} },
            { 0x213, new NativeTableEntry() { Name="PickTarget", Type=NativeType.Function} },
            { 0x214, new NativeTableEntry() { Name="PlayerCanSeeMe", Type=NativeType.Function} },
            { 0x218, new NativeTableEntry() { Name="SaveConfig", Type=NativeType.Function} },
            { 0x219, new NativeTableEntry() { Name="CanSeeByPoints", Type=NativeType.Function} },
            { 0x222, new NativeTableEntry() { Name="UpdateURL", Type=NativeType.Function} },
            { 0x223, new NativeTableEntry() { Name="GetURLMap", Type=NativeType.Function} },
            { 0x224, new NativeTableEntry() { Name="FastTrace", Type=NativeType.Function} },
            { 0x258, new NativeTableEntry() { Name="$", Type=NativeType.Operator, Precedence=40} },
            { 0x259, new NativeTableEntry() { Name="<", Type=NativeType.Operator, Precedence=24} },
            { 0x25A, new NativeTableEntry() { Name=">", Type=NativeType.Operator, Precedence=24} },
            { 0x25B, new NativeTableEntry() { Name="<=", Type=NativeType.Operator, Precedence=24} },
            { 0x25C, new NativeTableEntry() { Name=">=", Type=NativeType.Operator, Precedence=24} },
            { 0x25D, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x25E, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x25F, new NativeTableEntry() { Name="~=", Type=NativeType.Operator, Precedence=24} },
            { 0x26C, new NativeTableEntry() { Name="GotoState", Type=NativeType.Function} },
            { 0x280, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x281, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x28A, new NativeTableEntry() { Name="Len", Type=NativeType.Function} },
            { 0x28B, new NativeTableEntry() { Name="InStr", Type=NativeType.Function} },
            { 0x28C, new NativeTableEntry() { Name="Mid", Type=NativeType.Function} },
            { 0x28D, new NativeTableEntry() { Name="Left", Type=NativeType.Function} },
            { 0x3E8, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x3E9, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x3EA, new NativeTableEntry() { Name="==", Type=NativeType.Operator, Precedence=24} },
            { 0x3EB, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x3EC, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x3ED, new NativeTableEntry() { Name="!=", Type=NativeType.Operator, Precedence=26} },
            { 0x5DC, new NativeTableEntry() { Name="ProjectOnTo", Type=NativeType.Function} },
            { 0x5DD, new NativeTableEntry() { Name="IsZero", Type=NativeType.Function} },
            { 0xF81, new NativeTableEntry() { Name="MoveSmooth", Type=NativeType.Function} },
            { 0xF82, new NativeTableEntry() { Name="SetPhysics", Type=NativeType.Function} },
            { 0xF83, new NativeTableEntry() { Name="AutonomousPhysics", Type=NativeType.Function} },
        };


        public Dictionary<int, String> PrimitiveCastTable = new Dictionary<int, String>()
        {
            { 0x36, "bool" }, // InterfaceToBool
            { 0x37, "string" }, // InterfaceToString
            { 0x38, "object" }, // InterfaceToObject
            { 0x39, "vect" }, // RotatorToVector
            { 0x3A, "int" }, // ByteToInt
            { 0x3B, "bool" }, // ByteToBool
            { 0x3C, "float" }, // ByteToFloat
            { 0x3D, "byte" }, // IntToByte
            { 0x3E, "bool" }, // IntToBool
            { 0x3F, "float" }, // IntToFloat
            { 0x40, "byte" }, // BoolToByte
            { 0x41, "int" }, // BoolToInt
            { 0x42, "float" }, // BoolToFloat
            { 0x43, "byte" }, // FloatToByte
            { 0x44, "int" }, // FloatToInt
            { 0x45, "bool" }, // FloatToBool
            { 0x46, "interface" }, // ObjectToInterface
            { 0x47, "bool" }, // ObjectToBool
            { 0x48, "bool" }, // NameToBool
            { 0x49, "byte" }, // StringToByte
            { 0x4A, "int" }, // StringToInt
            { 0x4B, "bool" }, // StringToBool
            { 0x4C, "float" }, // StringToFloat
            { 0x4D, "vect" }, // StringToVector
            { 0x4E, "rot" }, // StringToRotator
            { 0x4F, "bool" }, // VectorToBool
            { 0x50, "rot" }, // VectorToRotator
            { 0x51, "bool" }, // RotatorToBool
            { 0x52, "string" }, // ByteToString
            { 0x53, "string" }, // IntToString
            { 0x54, "string" }, // BoolToString
            { 0x55, "string" }, // FloatToString
            { 0x56, "string" }, // ObjectToString
            { 0x57, "string" }, // NameToString
            { 0x58, "string" }, // VectorToString
            { 0x59, "string" }, // RotatorToString
            { 0x5A, "string" }, // DelegateToString
            { 0x60, "name" }, // StringToName
        };
    }
}
