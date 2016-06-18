using System;
using System.Collections.Generic;
using System.Linq;
using ME1Explorer;

namespace ME1Explorer.Unreal
{
    static class Bytecode
    {
        public struct Token
        {
            public byte[] raw;
            public string text;
            public bool stop;
        }

        public static PCCObject pcc;
        public static byte[] memory;
        public static int memsize;
        #region NormalToken
        private const int EX_LocalVariable = 0x00;
        private const int EX_InstanceVariable = 0x01;
        private const int EX_DefaultVariable = 0x02;
        private const int EX_Return = 0x04;
        private const int EX_Switch = 0x05;
        private const int EX_Jump = 0x06;
        private const int EX_JumpIfNot = 0x07;
        private const int EX_Stop = 0x08;
        private const int EX_Assert = 0x09;
        private const int EX_Case = 0x0A;
        private const int EX_Nothing = 0x0B;
        private const int EX_LabelTable = 0x0C;
        private const int EX_GotoLabel = 0x0D;
        private const int EX_EatReturnValue = 0x0E;
        private const int EX_Let = 0x0F;
        private const int EX_DynArrayElement = 0x10;
        private const int EX_New = 0x11;
        private const int EX_ClassContext = 0x12;
        private const int EX_Metacast = 0x13;
        private const int EX_LetBool = 0x14;
        private const int EX_EndParmValue = 0x15;
        private const int EX_EndFunctionParms = 0x16;
        private const int EX_Self = 0x17;
        private const int EX_Skip = 0x18;
        private const int EX_Context = 0x19;
        private const int EX_ArrayElement = 0x1A;
        private const int EX_VirtualFunction = 0x1B;
        private const int EX_FinalFunction = 0x1C;
        private const int EX_IntConst = 0x1D;
        private const int EX_FloatConst = 0x1E;
        private const int EX_StringConst = 0x1F;
        private const int EX_ObjectConst = 0x20;
        private const int EX_NameConst = 0x21;
        private const int EX_RotationConst = 0x22;
        private const int EX_VectorConst = 0x23;
        private const int EX_ByteConst = 0x24;
        private const int EX_IntZero = 0x25;
        private const int EX_IntOne = 0x26;
        private const int EX_True = 0x27;
        private const int EX_False = 0x28;
        private const int EX_NativeParm = 0x29;
        private const int EX_NoObject = 0x2A;
        private const int EX_IntConstByte = 0x2C;
        private const int EX_BoolVariable = 0x2D;
        private const int EX_DynamicCast = 0x2E;
        private const int EX_Iterator = 0x2F;
        private const int EX_IteratorPop = 0x30;
        private const int EX_IteratorNext = 0x31;
        private const int EX_StructCmpEq = 0x32;
        private const int EX_StructCmpNe = 0x33;
        private const int EX_UnicodeStringConst = 0x34;
        private const int EX_StructMember = 0x35;
        private const int EX_DynArrayLength = 0x36;
        private const int EX_GlobalFunction = 0x37;
        private const int EX_PrimitiveCast = 0x38;
        private const int EX_DynArrayInsert = 0x39;
        private const int EX_ByteToInt = 0x3A;        // EX_ReturnNothing = 0x3A
        private const int EX_EqualEqual_DelDel = 0x3B;
        private const int EX_NotEqual_DelDel = 0x3C;

        public static string ToRawText(byte[] raw, PCCObject Pcc, bool debug, int headerdiff)
        {
            ME1Explorer.BitConverter.IsLittleEndian = true;
            string s = "";
            pcc = Pcc;
            memory = raw;
            memsize = raw.Length;
            DebugCounter = 0;
            _debug = new List<DbgMsg>();
            List<Token> t = ReadAll(0);
            int pos = 32;
            for (int i = 0; i < t.Count; i++)
            {
                s += pos.ToString("X2") + " : " + t[i].text + "\n";
                pos += t[i].raw.Length;
            }
            if (debug)
            {
                s += "\nDebug print:\n\n";
                SortDbgMsg();
                for (int i = 0; i < _debug.Count(); i++)
                    s += _debug[i].count.ToString() + " : " + _debug[i].msg;
            }
            return s;

        }

        private const int EX_EqualEqual_DelFunc = 0x3D;
        private const int EX_NotEqual_DelFunc = 0x3E;
        private const int EX_EmptyDelegate = 0x3F;
        private const int EX_DynArrayRemove = 0x40;
        private const int EX_DebugInfo = 0x41;
        private const int EX_DelegateFunction = 0x42;
        private const int EX_DelegateProperty = 0x43;
        private const int EX_LetDelegate = 0x44;
        private const int EX_Conditional = 0x45;
        private const int EX_DynArrayFind = 0x46;
        private const int EX_DynArrayFindStruct = 0x47;
        private const int EX_LocalOutVariable = 0x48;
        private const int EX_DefaultParmValue = 0x49;
        private const int EX_EmptyParmValue = 0x4A;
        private const int EX_InstanceDelegate = 0x4B;
        private const int EX_GoW_DefaultValue = 0x50;
        private const int EX_InterfaceContext = 0x51;
        private const int EX_InterfaceCast = 0x52;
        private const int EX_EndOfScript = 0x53;
        private const int EX_DynArrayAdd = 0x54;
        private const int EX_DynArrayAddItem = 0x55;
        private const int EX_DynArrayRemoveItem = 0x56;
        private const int EX_DynArrayInsertItem = 0x57;
        private const int EX_DynArrayIterator = 0x58;


        private const int EX_Unkn1 = 0x5E;
        private const int EX_Unkn2 = 0x5B;
        private const int EX_Unkn3 = 0x61;
        private const int EX_Unkn4 = 0x62;
        private const int EX_Unkn5 = 0x5C;
        private const int EX_Unkn6 = 0x65;
        private const int EX_Unkn7 = 0x64;
        private const int EX_Unkn8 = 0x63;
        private const int EX_Unkn9 = 0x5D;
        private const int EX_Unkn10 = 0x5F;
        private const int EX_Unkn11 = 0x60;
        private const int EX_Unkn12 = 0x6A;
        private const int EX_Unkn13 = 0x6E;
        private const int EX_Unkn14 = 0x5A;
        private const int EX_Unkn15 = 0x59;

        public struct DbgMsg
        {
            public int count;
            public string msg;
        }

        static List<DbgMsg> _debug;
        static int DebugCounter;

        #endregion

        enum ENatives
        {
            NATIVE_Not_PreBool = 0x81,
            NATIVE_EqualEqual_BoolBool = 0xF2,
            NATIVE_NotEqual_BoolBool = 0xF3,
            NATIVE_AndAnd_BoolBool = 0x82,
            NATIVE_XorXor_BoolBool = 0x83,
            NATIVE_OrOr_BoolBool = 0x84,
            NATIVE_MultiplyEqual_ByteByte = 0x85,
            NATIVE_MultiplyEqual_ByteFloat = 0xC6,
            NATIVE_DivideEqual_ByteByte = 0x86,
            NATIVE_AddEqual_ByteByte = 0x87,
            NATIVE_SubtractEqual_ByteByte = 0x88,
            NATIVE_AddAdd_PreByte = 0x89,
            NATIVE_SubtractSubtract_PreByte = 0x8A,
            NATIVE_AddAdd_Byte = 0x8B,
            NATIVE_SubtractSubtract_Byte = 0x8C,
            NATIVE_Complement_PreInt = 0x8D,
            NATIVE_Subtract_PreInt = 0x8F,
            NATIVE_Multiply_IntInt = 0x90,
            NATIVE_Divide_IntInt = 0x91,
            NATIVE_Add_IntInt = 0x92,
            NATIVE_Subtract_IntInt = 0x93,
            NATIVE_LessLess_IntInt = 0x94,
            NATIVE_GreaterGreater_IntInt = 0x95,
            NATIVE_GreaterGreaterGreater_IntInt = 0xC4,
            NATIVE_Less_IntInt = 0x96,
            NATIVE_Greater_IntInt = 0x97,
            NATIVE_LessEqual_IntInt = 0x98,
            NATIVE_GreaterEqual_IntInt = 0x99,
            NATIVE_EqualEqual_IntInt = 0x9A,
            NATIVE_NotEqual_IntInt = 0x9B,
            NATIVE_And_IntInt = 0x9C,
            NATIVE_Xor_IntInt = 0x9D,
            NATIVE_Or_IntInt = 0x9E,
            NATIVE_MultiplyEqual_IntFloat = 0x9F,
            NATIVE_DivideEqual_IntFloat = 0xA0,
            NATIVE_AddEqual_IntInt = 0xA1,
            NATIVE_SubtractEqual_IntInt = 0xA2,
            NATIVE_AddAdd_PreInt = 0xA3,
            NATIVE_SubtractSubtract_PreInt = 0xA4,
            NATIVE_AddAdd_Int = 0xA5,
            NATIVE_SubtractSubtract_Int = 0xA6,
            NATIVE_Rand = 0xA7,
            NATIVE_Min = 0xF9,
            NATIVE_Max = 0xFA,
            NATIVE_Clamp = 0xFB,
            NATIVE_Subtract_PreFloat = 0xA9,
            NATIVE_MultiplyMultiply_FloatFloat = 0xAA,
            NATIVE_Multiply_FloatFloat = 0xAB,
            NATIVE_Divide_FloatFloat = 0xAC,
            NATIVE_Percent_FloatFloat = 0xAD,
            NATIVE_Add_FloatFloat = 0xAE,
            NATIVE_Subtract_FloatFloat = 0xAF,
            NATIVE_Less_FloatFloat = 0xB0,
            NATIVE_Greater_FloatFloat = 0xB1,
            NATIVE_LessEqual_FloatFloat = 0xB2,
            NATIVE_GreaterEqual_FloatFloat = 0xB3,
            NATIVE_EqualEqual_FloatFloat = 0xB4,
            NATIVE_ComplementEqual_FloatFloat = 0xD2,
            NATIVE_NotEqual_FloatFloat = 0xB5,
            NATIVE_MultiplyEqual_FloatFloat = 0xB6,
            NATIVE_DivideEqual_FloatFloat = 0xB7,
            NATIVE_AddEqual_FloatFloat = 0xB8,
            NATIVE_SubtractEqual_FloatFloat = 0xB9,
            NATIVE_Abs = 0xBA,
            NATIVE_Sin = 0xBB,
            NATIVE_Cos = 0xBC,
            NATIVE_Tan = 0xBD,
            NATIVE_Atan = 0xBE,
            NATIVE_Exp = 0xBF,
            NATIVE_Loge = 0xC0,
            NATIVE_Sqrt = 0xC1,
            NATIVE_Square = 0xC2,
            NATIVE_FRand = 0xC3,
            NATIVE_FMin = 0xF4,
            NATIVE_FMax = 0xF5,
            NATIVE_FClamp = 0xF6,
            NATIVE_Lerp = 0xF7,
            NATIVE_Subtract_PreVector = 0xD3,
            NATIVE_Multiply_VectorFloat = 0xD4,
            NATIVE_Multiply_FloatVector = 0xD5,
            NATIVE_Multiply_VectorVector = 0x128,
            NATIVE_Divide_VectorFloat = 0xD6,
            NATIVE_Add_VectorVector = 0xD7,
            NATIVE_Subtract_VectorVector = 0xD8,
            NATIVE_LessLess_VectorRotator = 0x113,
            NATIVE_GreaterGreater_VectorRotator = 0x114,
            NATIVE_EqualEqual_VectorVector = 0xD9,
            NATIVE_NotEqual_VectorVector = 0xDA,
            NATIVE_Dot_VectorVector = 0xDB,
            NATIVE_Cross_VectorVector = 0xDC,
            NATIVE_MultiplyEqual_VectorFloat = 0xDD,
            NATIVE_MultiplyEqual_VectorVector = 0x129,
            NATIVE_DivideEqual_VectorFloat = 0xDE,
            NATIVE_AddEqual_VectorVector = 0xDF,
            NATIVE_SubtractEqual_VectorVector = 0xE0,
            NATIVE_VSize = 0xE1,
            NATIVE_Normal = 0xE2,
            NATIVE_VRand = 0xFC,
            NATIVE_MirrorVectorByNormal = 0x12C,
            NATIVE_ProjectOnTo = 0x5DC,
            NATIVE_IsZero = 0x5DD,
            NATIVE_EqualEqual_RotatorRotator = 0x8E,
            NATIVE_NotEqual_RotatorRotator = 0xCB,
            NATIVE_Multiply_RotatorFloat = 0x11F,
            NATIVE_Multiply_FloatRotator = 0x120,
            NATIVE_Divide_RotatorFloat = 0x121,
            NATIVE_MultiplyEqual_RotatorFloat = 0x122,
            NATIVE_DivideEqual_RotatorFloat = 0x123,
            NATIVE_Add_RotatorRotator = 0x13C,
            NATIVE_Subtract_RotatorRotator = 0x13D,
            NATIVE_AddEqual_RotatorRotator = 0x13E,
            NATIVE_SubtractEqual_RotatorRotator = 0x13F,
            NATIVE_GetAxes = 0xE5,
            NATIVE_GetUnAxes = 0xE6,
            NATIVE_RotRand = 0x140,
            NATIVE_Concat_StrStr = 0x70,
            NATIVE_At_StrStr = 0xA8,
            NATIVE_Less_StrStr = 0x73,
            NATIVE_Greater_StrStr = 0x74,
            NATIVE_LessEqual_StrStr = 0x78,
            NATIVE_GreaterEqual_StrStr = 0x79,
            NATIVE_EqualEqual_StrStr = 0x7A,
            NATIVE_NotEqual_StrStr = 0x7B,
            NATIVE_ComplementEqual_StrStr = 0x7C,
            NATIVE_ConcatEqual_StrStr = 0x142,
            NATIVE_AtEqual_StrStr = 0x143,
            NATIVE_SubtractEqual_StrStr = 0x144,
            NATIVE_Len = 0x7D,
            NATIVE_InStr = 0x7E,
            NATIVE_Mid = 0x7F,
            NATIVE_Left = 0x80,
            NATIVE_Right = 0xEA,
            NATIVE_Caps = 0xEB,
            NATIVE_Locs = 0xEE,
            NATIVE_Chr = 0xEC,
            NATIVE_Asc = 0xED,
            NATIVE_Repl = 0xC9,
            NATIVE_EqualEqual_ObjectObject = 0x72,
            NATIVE_NotEqual_ObjectObject = 0x77,
            NATIVE_ClassIsChildOf = 0x102,
            NATIVE_IsA = 0xC5,
            NATIVE_EqualEqual_NameName = 0xFE,
            NATIVE_NotEqual_NameName = 0xFF,
            NATIVE_Add_QuatQuat = 0x10E,
            NATIVE_Subtract_QuatQuat = 0x10F,
            NATIVE_EqualEqual_StringRefStringRef = 0x3E8,
            NATIVE_EqualEqual_StringRefInt = 0x3E9,
            NATIVE_EqualEqual_IntStringRef = 0x3EA,
            NATIVE_NotEqual_StringRefStringRef = 0x3EB,
            NATIVE_NotEqual_StringRefInt = 0x3EC,
            NATIVE_NotEqual_IntStringRef = 0x3ED,
            NATIVE_LogInternal = 0xE7,
            NATIVE_WarnInternal = 0xE8,
            NATIVE_GotoState = 0x71,
            NATIVE_IsInState = 0x119,
            NATIVE_GetStateName = 0x11C,
            NATIVE_Enable = 0x75,
            NATIVE_Disable = 0x76,
            NATIVE_SaveConfig = 0x218,
        };

        public static string ToRawText(byte[] raw, PCCObject Pcc, bool debug = false)
        {
            ME1Explorer.BitConverter.IsLittleEndian = true;
            string s = "";
            pcc = Pcc;
            memory = raw;
            memsize = raw.Length;
            DebugCounter = 0;
            _debug = new List<DbgMsg>();
            List<Token> t = ReadAll(0);
            int pos = 32;
            for (int i = 0; i < t.Count; i++)
            {
                s += pos.ToString("X2") + " : " + t[i].text + "\n";
                pos += t[i].raw.Length;
            }
            if (debug)
            {
                s += "\nDebug print:\n\n";
                SortDbgMsg();
                for (int i = 0; i < _debug.Count(); i++)
                    s += _debug[i].count.ToString() + " : " + _debug[i].msg;
            }
            return s;
        }

        private static void SortDbgMsg()
        {
            bool done = false;
            while (!done)
            {
                done = true;
                for (int i = 0; i < _debug.Count() - 1; i++)
                    if (_debug[i].count > _debug[i + 1].count)
                    {
                        DbgMsg t = _debug[i];
                        _debug[i] = _debug[i + 1];
                        _debug[i + 1] = t;
                        done = false;
                    }
            }
        }

        public static List<Token> ReadAll(int start)
        {
            List<Token> res = new List<Token>();
            int pos = start;
            Token t = ReadToken(pos);
            res.Add(t);
            while (!t.stop)
            {
                pos += t.raw.Length;
                t = ReadToken(pos);
                res.Add(t);
            }
            return res;
        }

        public static Token ReadToken(int start)
        {
            int thiscount = DebugCounter;
            DebugCounter++;
            Token res = new Token();
            res.text = "";
            res.raw = new byte[1];
            res.stop = true;
            Token newTok;
            if (start >= memsize)
                return res;
            byte t = memory[start];
            int end = start;
            if (t <= 0x60)
                switch (t)
                {
                    case EX_LocalVariable: //0x00
                        newTok = ReadLocalVar(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DefaultVariable: //0x02
                    case EX_InstanceVariable: //0x01
                        newTok = ReadInstanceVar(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Return: //0x04
                        newTok = ReadReturn(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Switch: //0x05
                        newTok = ReadSwitch(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Jump: //0x06
                        newTok = ReadJump(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_JumpIfNot: //0x07
                        newTok = ReadJumpIfNot(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Assert: // 0x09
                        newTok = ReadAssert(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Case: // 0x0A
                        newTok = ReadCase(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Nothing: // 0x0B
                        newTok = ReadNothing(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_LabelTable: //0x0C
                        newTok = ReadLableTable(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EatReturnValue: // 0x0E
                        newTok = ReadEatReturn(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Let://0x0F
                    case EX_LetBool: //0x14
                    case EX_LetDelegate: //0x44
                        newTok = ReadLet(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ArrayElement: // 0x1A
                    case EX_DynArrayElement: // 0x10
                        newTok = ReadDynArrayElement(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_New: //0x11
                        newTok = ReadNew(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Metacast: // 0x13
                        newTok = ReadMetacast(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndParmValue: // 0x15
                        newTok = ReadEndParmVal(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndFunctionParms: // 0x16
                        newTok = ReadEndFuncParm(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Self: //  0x17
                        newTok = ReadSelf(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Skip: // 0x18
                        newTok = ReadSkip(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ClassContext://0x12
                    case EX_Context: // 0x19
                        newTok = ReadContext(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_VirtualFunction: // 0x1B
                        newTok = ReadVirtualFunc(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_FinalFunction: // 0x1C
                        newTok = ReadFinalFunc(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntConst: // 0x1D
                        newTok = ReadIntConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_FloatConst: // 0x1E
                        newTok = ReadStatFloat(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StringConst: //0x1F
                        newTok = ReadStringConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ObjectConst: // 0x20
                        newTok = ReadObjectConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NameConst: // 0x21
                        newTok = ReadNameConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_RotationConst: // 0x22
                        newTok = ReadRotatorConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_VectorConst: // 0x23
                        newTok = ReadVectorConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntConstByte: // 0x2C
                    case EX_ByteConst: //0x24
                        newTok = ReadByteConst(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntZero: // 0x25
                        newTok = ReadZero(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntOne: //0x26
                        newTok = ReadIntOne(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_True: // 0x27
                        newTok = ReadTrue(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_False: // 0x28
                        newTok = ReadFalse(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NativeParm: //0x29
                        newTok = ReadNativeParm(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NoObject: // 0x2A
                        newTok = ReadNone(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_BoolVariable: // 0x2D
                        newTok = ReadBoolExp(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynamicCast: // 0x2E
                        newTok = ReadDynCast(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Iterator: //0x2F
                        newTok = ReadIterator(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IteratorPop: // 0x30
                        newTok = ReadIterPop(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IteratorNext: //0x31
                        newTok = ReadIterNext(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructCmpEq: // 0x32
                        newTok = ReadCompareStructs(start, "==");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructCmpNe: // 0x33
                        newTok = ReadCompareStructs(start, "!=");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructMember: //0x35
                        newTok = ReadStruct(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayLength: //0x36
                        newTok = ReadDynArrayLen(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_GlobalFunction: // 0x37
                        newTok = ReadGlobalFunc(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_PrimitiveCast: //0x38
                        newTok = ReadPrimitiveCast(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayInsert: // 0x39
                        newTok = ReadArrayArg2(start, "Insert", false);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ByteToInt: // 0x3A
                        newTok = ReadByteToInt(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EqualEqual_DelDel: // 0x3B
                        newTok = ReadEqual(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NotEqual_DelDel: // 0x3C
                        newTok = ReadCompareDel(start, "!=");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EqualEqual_DelFunc: // 0x3D
                        newTok = ReadCompareDel2(start, "==");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EmptyDelegate: // 0x3F
                        newTok = ReadEmptyDel(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayRemove: // 0x40
                        newTok = ReadArrayArg2(start, "Remove", false);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DelegateFunction: // 0x42
                        newTok = ReadDelFunc(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DelegateProperty: // 0x43
                        newTok = ReadDelegateProp(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Conditional: // 0x45
                        newTok = ReadConditional(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayFind:// 0x46
                        return ReadArrayArg(start, "Find");
                    case EX_DynArrayFindStruct: //0x47
                        return ReadArrayArg2(start, "Find", true);
                    case EX_LocalOutVariable: //0x48
                        newTok = ReadLocOutVar(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DefaultParmValue: // 0x49
                        newTok = ReadDefaultParmVal(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EmptyParmValue: //0x4A                    
                        newTok = ReadEmptyParm(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InstanceDelegate: // 0x4B
                        newTok = ReadInstDelegate(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case 0x4F:
                        newTok = Read4FAdd(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_GoW_DefaultValue: //0x50
                        newTok = ReadGoWVal(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InterfaceContext:// 0x51
                        newTok = ReadInterfaceContext(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InterfaceCast: // 0x52
                        newTok = ReadUnkn1(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndOfScript:    //0x53                    
                        newTok = ReadEndOfScript(start);
                        newTok.stop = true;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayAdd: // 0x54
                        newTok = ReadAdd(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayAddItem: //0x55
                        newTok = ReadArrayArg(start, "Add");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayRemoveItem: //0x56
                        newTok = ReadArrayArg(start, "Remove");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayInsertItem: //0x57
                        newTok = ReadArrayArg(start, "Insert");
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayIterator: // 0x58
                        newTok = ReadDynArrayItr(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn15: // 0x59
                        newTok = ReadUnkn15(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn14: // 0x5A
                        newTok = ReadUnkn14(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn1: // 0x5E
                        newTok = ReadUnkn1b(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn2: // 0x5B
                    case EX_Unkn5: // 0x5C                                    
                    case EX_Unkn9: // 0x5D
                    case EX_Unkn10: // 0x5F
                    case EX_Unkn11: // 0x60
                        newTok = ReadUnkn1(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    default:
                        newTok = ReadUnknown(start);
                        newTok.stop = true;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                }
            else
            {
                switch (t)
                {
                    case EX_Unkn3: //0x61
                        newTok = ReadUnkn3(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn4: //0x62
                        newTok = ReadUnkn4(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn8: //0x63
                        newTok = ReadUnkn8(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn7: //0x64
                        newTok = ReadUnkn7(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn6: //0x65
                        newTok = ReadUnkn6(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn12: // 0x6A
                        newTok = ReadUnkn12(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Unkn13: // 0x6E
                        newTok = ReadUnkn13(start);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    default:
                        if (t < 0x70)
                        {
                            newTok = ReadExtNative(start);
                            newTok.stop = false;
                            end = start + newTok.raw.Length;
                            res = newTok;
                        }
                        else
                        {
                            newTok = ReadNative(start);
                            newTok.stop = false;
                            end = start + newTok.raw.Length;
                            res = newTok;
                        }
                        break;
                }
            }
            DbgMsg msg = new DbgMsg();
            msg.msg = "Read token[0x" + t.ToString("X") + "] at 0x" + (start + 32).ToString("X") + ": \"" + res.text + "\" STOPTOKEN:" + res.stop + "\n";
            msg.count = thiscount;
            _debug.Add(msg);
            return res;
        }

        private static Token ReadNative(int start)
        {
            Token t = new Token();
            Token a, b, c;
            int count;
            byte byte1 = memory[start];
            byte byte2 = memory[start + 1];
            int index;
            if ((byte1 & 0xF0) == 0x70)
                index = ((byte1 - 0x70) << 8) + byte2;
            else
                index = byte1;
            int pos = start;
            if (index == byte1)
                pos++;
            else
                pos += 2;
            switch (index)
            {
                case (int)ENatives.NATIVE_Not_PreBool: //0x0081
                    t.text = "!";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    break;
                case (int)ENatives.NATIVE_AndAnd_BoolBool: //0x0082
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + " && " + b.text;
                    break;
                case (int)ENatives.NATIVE_XorXor_BoolBool:  //0x0083
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + " ^^ " + b.text;
                    break;
                case (int)ENatives.NATIVE_OrOr_BoolBool://0x0084
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + " || " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_ByteByte://  0x0085
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_ByteByte: // 0x0086
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_ByteByte: // 0x0087
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_ByteByte: // 0x0088
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractSubtract_PreByte: // 0x008A
                case (int)ENatives.NATIVE_SubtractSubtract_Byte: // 0x008C
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + "--";
                    break;
                case (int)ENatives.NATIVE_AddAdd_Byte: // 0x008B
                case (int)ENatives.NATIVE_AddAdd_PreByte: // 0x0089
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + "++";
                    break;
                case (int)ENatives.NATIVE_Complement_PreInt: // 0x008D
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "~(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_RotatorRotator: // 0x008E
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreInt: // 0x008F
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_Multiply_IntInt:// 0x0090
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " * " + b.text;
                    break;
                case (int)ENatives.NATIVE_Divide_IntInt:// 0x0091
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " / " + b.text;
                    break;
                case (int)ENatives.NATIVE_Add_IntInt:// 0x0092
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " + " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_IntInt:// 0x0093
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " - " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessLess_IntInt: // 0x0094
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " << " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterGreater_IntInt: // 0x0095
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " >> " + b.text;
                    break;
                case (int)ENatives.NATIVE_Less_IntInt: // 0x0096
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " < " + b.text;
                    break;
                case (int)ENatives.NATIVE_Greater_IntInt://0x0097
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " > " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessEqual_IntInt: // 0x0098
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " <= " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterEqual_IntInt: // 0x0099
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " >= " + b.text;
                    break;
                case (int)ENatives.NATIVE_EqualEqual_IntInt: // 0x009A
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_IntInt: // 0x009B
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_And_IntInt: // 0x009C
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " & " + b.text;
                    break;
                case (int)ENatives.NATIVE_Xor_IntInt: // 0x009D
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " ^ " + b.text;
                    break;
                case (int)ENatives.NATIVE_Or_IntInt: // 0x009E
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " | " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_IntFloat: // 0x009F
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_IntFloat: // 0x00A0
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_IntInt: // 0x00A1
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_IntInt: // 0x00A2
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddAdd_PreInt: // 0x00A3
                case (int)ENatives.NATIVE_AddAdd_Int: // 0x00A5
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + "++";
                    break;
                case (int)ENatives.NATIVE_SubtractSubtract_PreInt: // 0x00A4
                case (int)ENatives.NATIVE_SubtractSubtract_Int: // 0x00A6
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text + "--";
                    break;
                case (int)ENatives.NATIVE_Rand: // 0x00A7
                    t.text = "Rand(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_At_StrStr: // 0x00A8
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " @ " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreFloat: // 0x00A9
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyMultiply_FloatFloat: // 0x00AA
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " ** " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Multiply_FloatFloat: // 0x00AB
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " * " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Divide_FloatFloat: // 0x00AC
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " / " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Add_FloatFloat: // 0x00AE
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " + " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Subtract_FloatFloat: // 0x00AF
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " - " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Less_FloatFloat: // 0x00B0
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " < " + b.text;
                    break;
                case (int)ENatives.NATIVE_Greater_FloatFloat: // 0x00B1
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " > " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessEqual_FloatFloat: // 0x00B2
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " <= " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterEqual_FloatFloat: // 0x00B3
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " >= " + b.text;
                    break;
                case (int)ENatives.NATIVE_EqualEqual_FloatFloat: // 0x00B4
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_FloatFloat: // 0x00B5
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_FloatFloat: // 0x00B6
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_FloatFloat: // 0x00B7
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_FloatFloat: // 0x00B8
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_FloatFloat: // 0x00B9
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_Abs: // 0x00BA
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Abs(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Sin: // 0x00BB
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Sin(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Cos: // 0x00BC
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Cos(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Tan: // 0x00BD
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Tan(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Atan: // 0x00BE
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "ATan(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Exp: // 0x00BF
                    t.text = "Exp(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Loge: // 0x00C0
                    t.text = "Loge(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Sqrt: // 0x00C1
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Sqrt(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Square: // 0x00C2
                    t.text = "Sqr(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FRand:// 0x00C3
                    t.text = "FRand()";
                    break;
                case (int)ENatives.NATIVE_IsA: // 0x00C5
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "IsA(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_ByteFloat: // 0x00C6
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case 0xC7: //unknown
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = a.text;
                    break;
                case (int)ENatives.NATIVE_Repl: // 0x00C9
                    t.text = "Repl(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_NotEqual_RotatorRotator: // 0x00CB
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_ComplementEqual_FloatFloat: // 0x00D2
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " ~= " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreVector: // 0x00D3
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_Divide_VectorFloat: // 0x00D6
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " / " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Multiply_VectorFloat: // 0x00D4
                case (int)ENatives.NATIVE_Multiply_VectorVector: // 0x0128
                case (int)ENatives.NATIVE_Multiply_FloatVector: // 0x00D5
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " * " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Add_VectorVector: // 0x00D7
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " + " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Subtract_VectorVector: //0x00D8
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " - " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_VectorVector: // 0x00D9
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_VectorVector: // 0x00DA
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_Dot_VectorVector: // 0x00DB
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " dot " + b.text;
                    break;
                case (int)ENatives.NATIVE_Cross_VectorVector: // 0x00DC
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " cross " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_VectorFloat: // 0x00DE
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_VectorFloat: // 0x00DD
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_VectorVector: // 0x00DF
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_VectorVector: // 0x00E0
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_VSize: // 0x00E1
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "VSize(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Normal: // 0x00E2
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    t.text = "Normal(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_GetAxes: // 0x00E5
                    t.text = "GetAxes(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;//
                case (int)ENatives.NATIVE_Right: // 0x00EA
                    t.text = "Right(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Caps: // 0x00EB
                    t.text = "Caps(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Chr: // 0x00EC
                    t.text = "Chr(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Asc: // 0x00ED
                    t.text = "Asc(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Locs: // 0x00EE
                    t.text = "Locs(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_BoolBool://0x00F2
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_BoolBool://0x00F3
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_FMin: // 0x00F4
                    t.text = "Min(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FMax: //  0x00F5
                    t.text = "Max(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FClamp: //  0x00F6
                    t.text = "Clamp(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Lerp: // 0x00F7
                    t.text = "Lerp(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Min: // 0x00F9
                    t.text = "Min(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Max: // 0x00FA
                    t.text = "Max(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Clamp: // 0x00FB
                    t.text = "Clamp(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_VRand: // 0x00FC
                    t.text = "VRand(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Percent_FloatFloat: // 0x00FD
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " % " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_NameName: // 0x00FE
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_NameName: // 0x00FF
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_ClassIsChildOf:// 0x0102
                    t.text = "ClassIsChildOf(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Concat_StrStr: // 0x0258
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " $ " + b.text;
                    break;
                case (int)ENatives.NATIVE_Less_StrStr: // 0x0259
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " < " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Greater_StrStr: // 0x025A
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " > " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_LessEqual_StrStr: // 0x025B
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " <= " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_GreaterEqual_StrStr: // 0x025C
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " >= " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_StrStr: // 0x025D
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " == " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_NotEqual_StrStr: // 0x025E
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " != " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_ComplementEqual_StrStr: //0x025F  
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " ~= " + b.text;
                    break;
                case (int)ENatives.NATIVE_GotoState: // 0x026C
                    t.text = "GotoState(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0)
                            t.text += a.text;
                        else
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_ObjectObject: // 0x0280
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " == " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_NotEqual_ObjectObject: // 0x0281
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " != " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Len: // 0x028A
                    t.text = "Len(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_InStr: // 0x028B
                    t.text = "InStr(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Mid: // 0x028C
                    t.text = "Mid(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case 0x028D: //unkown == (int)ENatives.NATIVE_Mid: // 0x028C
                    t.text = "Left(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_StringRefStringRef: // 0x03E8
                case (int)ENatives.NATIVE_EqualEqual_StringRefInt: // 0x03E9
                case (int)ENatives.NATIVE_EqualEqual_IntStringRef: // 0x03EA
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_StringRefStringRef: // 0x03EB
                case (int)ENatives.NATIVE_NotEqual_StringRefInt: // 0x03EC
                case (int)ENatives.NATIVE_NotEqual_IntStringRef: // 0x03ED
                    a = ReadToken(pos);
                    pos += a.raw.Length;
                    b = ReadToken(pos);
                    pos += b.raw.Length;
                    c = ReadToken(pos);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_ProjectOnTo: // 0x05DC
                    t.text = "ProjectOnTo(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_IsZero: // 0x05DD
                    t.text = "IsZero(";
                    count = 0;
                    while (pos < memsize - 6)
                    {
                        a = ReadToken(pos);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == 0x16)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                    }
                    t.text += ")";
                    break;
                default:
                    t.text = "UnknownNative(" + index + ")";
                    break;
            }
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        //private static Token ReadClassContext(int start)
        //{
        //    Token t = new Token();
        //    Token a = ReadToken(start + 2);
        //    int pos = start + a.raw.Length + 2;
        //    ME1Explorer.BitConverter.IsLittleEndian = true;
        //    //int index = ME1Explorer.BitConverter.ToInt32(memory, pos);
        //    //string s = pcc.getObjectName(index);
        //    //pos += 4;
        //    Token b = ReadToken(pos);
        //    pos += b.raw.Length;
        //    int len = pos - start;
        //    t.text = a.text + "." + b.text;
        //    t.raw = new byte[len];
        //    if (start + len <= memsize)
        //        for (int i = 0; i < len; i++)
        //            t.raw[i] = memory[start + i];
        //    return t;
        //}

        private static Token ReadUnkn15(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length + 2;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            t.text = b.text + "(" + a.text + ");";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn14(int start)
        {
            Token t = new Token();
            int pos = start + 3;
            Token a = ReadToken(pos);
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn13(int start)
        {
            Token t = new Token();
            int pos = start + 2;
            Token a = ReadToken(pos);
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadGoWVal(int start)
        {
            Token t = new Token();
            int pos = start + 2;
            Token a = ReadToken(pos);
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token Read4FAdd(int start)
        {
            Token t = new Token();
            int pos = start + 5;
            Token a = ReadToken(pos);
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadAdd(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            t.text = "Add(";
            int count = 0;
            while (pos < memsize - 6)
            {
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + t2.text;
                else
                    t.text += t2.text;
                count++;

            }
            t.text += ");";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn12(int start)
        {
            Token t = new Token();
            int pos = start + 3;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadCompareDel(int start, string arg)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length + 2;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            t.text = a.text + " " + arg + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadCompareDel2(int start, string arg)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            t.text = a.text + " " + arg + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDelFunc(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, pos);
            pos += 8;
            string s = pcc.getNameEntry(index);
            t.text = a.text + "." + s + "(";
            int count = 0;
            while (pos < memsize - 6)
            {
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + t2.text;
                else
                    t.text += t2.text;
                count++;

            }
            t.text += ");";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len-1; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadGlobalFunc(int start)
        {
            Token t = new Token();
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = "Global." + pcc.getNameEntry(index) + "(";
            int pos = start + 9;
            int count = 0;
            while (pos < memsize - 6)
            {
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + t2.text;
                else
                    t.text += t2.text;
                count++;

            }
            t.text += ");";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadCompareStructs(int start, string s)
        {
            Token t = new Token();
            int pos = start + 5;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            t.text = a.text + " " + s + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadAssert(int start)
        {
            Token t = new Token();
            int pos = start + 4;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = "assert(" + a.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadInterfaceContext(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadNew(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            Token d = ReadToken(pos);
            pos += d.raw.Length;
            t.text = "new(" + a.text + "," + b.text + "," + c.text + "," + d.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadArrayArg2(int start, string arg, bool skip2byte)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            if (skip2byte) pos += 2;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            t.text = a.text + "." + arg + "(" + b.text + "," + c.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadArrayArg(int start, string arg)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length + 2;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            t.text = a.text + "." + arg + "(" + b.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn1b(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            //string s = "";
            //if (index > 0 && index <= pcc.ExportCount)
            //    s = pcc.Exports[index - 1].ClassName;
            //if (index * -1 > 0 && index * -1 <= pcc.ImportCount)
            //    s = pcc.Imports[index * -1 - 1].ClassName;
            //s = s.Replace("Property", "");
            t.text = pcc.getObjectName(index);
            int pos = start + 5;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadCase(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            int size = ME1Explorer.BitConverter.ToInt16(memory, pos);
            pos += 2;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = "Case " + a.text + ":";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadMetacast(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            int index = ME1Explorer.BitConverter.ToInt32(memory, pos);
            string s = "";
            if (index > 0 && index <= pcc.Exports.Count)
                s = pcc.Exports[index - 1].ObjectName;
            if (index * -1 > 0 && index * -1 <= pcc.Imports.Count)
                s = pcc.Imports[index * -1 - 1].ObjectName;
            pos += 4;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = "Class<" + s + ">(" + a.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadVectorConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            float f1 = ME1Explorer.BitConverter.ToSingle(memory, pos);
            float f2 = ME1Explorer.BitConverter.ToSingle(memory, pos + 4);
            float f3 = ME1Explorer.BitConverter.ToSingle(memory, pos + 8);
            t.text = "vect(" + f1 + ", " + f2 + ", " + f3 + ")";
            int len = 13;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadRotatorConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            int i1 = ME1Explorer.BitConverter.ToInt32(memory, pos);
            int i2 = ME1Explorer.BitConverter.ToInt32(memory, pos + 4);
            int i3 = ME1Explorer.BitConverter.ToInt32(memory, pos + 8);
            t.text = "rot(" + i1 + ", " + i2 + ", " + i3 + ")";
            int len = 13;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadConditional(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            pos += 2;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            pos += 2;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            //Token d = ReadToken(pos);
            //pos += d.raw.Length;
            t.text = "(" + a.text + ") ? " + b.text + " : " + c.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadEatReturn(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 5);
            t.text = a.text;
            int len = 5 + a.raw.Length;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn8(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = "If(" + pcc.getObjectName(index) + "){";
            int pos = start + 8;
            Token a = ReadToken(pos);
            t.text += a.text + "}";
            pos += a.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadIntOne(int start)
        {
            Token t = new Token();
            t.text = "1";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadUnkn7(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = "If(" + pcc.getObjectName(index) + ")\n\t{\n";
            int pos = start + 8;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text += "\t\t" + a.text;
            t.text += "\n\t}";
            if (memory[pos] == 0x06) //jump
            {
                int offset = ME1Explorer.BitConverter.ToInt16(memory, pos + 1);
                t.text += "\nelse\\\\Jump 0x" + offset.ToString("X") + "\n{\n";
                pos += 3;
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                t.text += t2.text + "\n}";
            }
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadEmptyDel(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadStruct(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int field = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            int type = ME1Explorer.BitConverter.ToInt32(memory, start + 5);
            int skip = ME1Explorer.BitConverter.ToInt16(memory, start + 7);
            int pos = start + 11;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            t.text = a.text + "." + pcc.getObjectName(field);
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadPrimitiveCast(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 2);
            t.text = a.text;
            int pos = start + a.raw.Length + 2;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadSkip(int start)
        {
            Token t = new Token();
            Token a;
            int skip = ME1Explorer.BitConverter.ToInt16(memory, start + 1);
            t.text = "";
            int count = 0;
            int pos = start + 3;
            while (pos < memsize - 6)
            {
                a = ReadToken(pos);
                pos += a.raw.Length;
                if (a.raw != null && a.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + a.text;
                else
                    t.text += a.text;
                count++;
            }
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn6(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = pcc.getNameEntry(index);
            int pos = start + 11;
            t.text += "(";
            int count = 0;
            while (pos < memsize - 6)
            {
                Token a = ReadToken(pos);
                pos += a.raw.Length;
                if (a.raw != null && a.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + a.text;
                else
                    t.text += a.text;
                count++;
            }
            t.text += ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadExtNative(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = pcc.getObjectName(index);
            int pos = start + 11;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDynArrayElement(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int pos = start + 1;
            t.text = "[" + a.text + "]";
            pos += a.raw.Length;
            Token b = ReadToken(pos);
            t.text = b.text + t.text;
            pos += b.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDelLet(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 2);
            int pos = start + 2;
            t.text = "\t{\n\t\t" + a.text + "\n\t}";
            pos += a.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadIterNext(int start)
        {
            Token t = new Token();
            t.text = "\\\\Next";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadIterPop(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int len = a.raw.Length + 1;
            t.text = a.text;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadIterator(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length + 2;
            t.text = "foreach " + a.text;
            int len = pos - start;
            t.raw = new byte[len];
            t.text += " )";
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadStringConst(int start)
        {
            Token t = new Token();
            t.text = "";
            int pos = start + 1;
            while (memory[pos] != 0)
                t.text += (char)memory[pos++];
            int len = pos - start + 1;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            t.text = "'" + t.text + "'";
            return t;
        }

        private static Token ReadDynCast(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int idx = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            Token a = ReadToken(start + 5);
            t.text = "(" + pcc.getObjectName(idx) + ")" + a.text;
            int len = a.raw.Length + 5;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn4(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDynArrayItr(int start)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos);
            pos += a.raw.Length;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            pos += 1;
            Token c = ReadToken(pos);
            pos += c.raw.Length;
            pos += 2;
            t.text = "foreach " + c.text + "(" + b.text + " IN " + a.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadBoolExp(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            t.text = a.text;
            int len = a.raw.Length + 1;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn3(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadLocOutVar(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDynArrayLen(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int pos = start + a.raw.Length + 1;
            int len = pos - start;
            t.text = a.text + ".Length";
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadByteConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int n = memory[start + 1];
            t.text = n.ToString();
            t.raw = new byte[2];
            for (int i = 0; i < 2; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadIntConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int n = (Int32)ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = n.ToString();
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadEmptyParm(int start)
        {
            Token t = new Token();
            t.text = "";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadDelegateProp(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = pcc.getNameEntry(index);
            t.raw = new byte[13];
            for (int i = 0; i < 13; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadInstDelegate(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = pcc.getNameEntry(index);
            t.raw = new byte[9];
            for (int i = 0; i < 9; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadObjectConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = " '" + pcc.getObjectName(index) + "'";
            if (index > 0 && index <= pcc.Exports.Count)
                t.text = pcc.Exports[index - 1].ClassName + t.text;
            if (index * -1 > 0 && index * -1 <= pcc.Imports.Count)
                t.text = pcc.Imports[index * -1 - 1].ClassName + t.text;
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadFinalFunc(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index) + "(";
            int pos = start + 5;
            int count = 0;
            while (pos < memsize - 6)
            {
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + t2.text;
                else
                    t.text += t2.text;
                count++;
            }
            t.text += ")";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnkn1(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadNameConst(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = "'" + pcc.getNameEntry(index) + "'";
            t.raw = new byte[9];
            for (int i = 0; i < 9; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadNone(int start)
        {
            Token t = new Token();
            t.text = "None";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadZero(int start)
        {
            Token t = new Token();
            t.text = "0";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadVirtualFunc(int start)
        {
            Token t = new Token();
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = pcc.getNameEntry(index) + "(";
            int pos = start + 9;
            int count = 0;
            while (pos < memsize - 6)
            {
                Token t2 = ReadToken(pos);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == 0x16)
                    break;
                if (count != 0)
                    t.text += "," + t2.text;
                else
                    t.text += t2.text;
                count++;

            }
            t.text += ");";
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadSelf(int start)
        {
            Token t = new Token();
            t.text = "this";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadByteToInt(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = (Int32)ME1Explorer.BitConverter.ToInt64(memory, start + 1);
            t.text = "ByteToInt(" + pcc.getObjectName(index) + ")";
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadStatFloat(int start)
        {
            Token t = new Token();
            float f = ME1Explorer.BitConverter.ToSingle(memory, start + 1);
            t.text = f.ToString() + "f";
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadEndParmVal(int start)
        {
            Token t = new Token();
            t.text = "//EndParmVal";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadEndFuncParm(int start)
        {
            Token t = new Token();
            t.text = "";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadTrue(int start)
        {
            Token t = new Token();
            t.text = "True";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadFalse(int start)
        {
            Token t = new Token();
            t.text = "False";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadContext(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int pos = start + a.raw.Length + 1;
            int expSize = ME1Explorer.BitConverter.ToInt16(memory, pos);
            pos += 2;
            int bSize = memory[pos];
            pos += 5;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            int len = pos - start;
            t.text = a.text + "." + b.text;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadDefaultParmVal(int start)
        {
            Token t = new Token();
            int size = ME1Explorer.BitConverter.ToInt16(memory, start + 1);
            Token a = ReadToken(start + 3);
            int pos = start + a.raw.Length + 3;
            int len = pos - start;
            t.text = "DefaultParameterValue(" + a.text + ")";
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadEqual(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int pos = start + a.raw.Length + 1;
            Token b = ReadToken(pos);
            pos += b.raw.Length;
            int len = pos - start;
            t.text = "(" + a.text + " == " + b.text + ")";
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadSwitch(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 6);
            t.text = "switch (" + a.text + ")";
            int len = a.raw.Length + 6;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadJumpIfNot(int start)
        {
            Token t = new Token();
            int offset = ME1Explorer.BitConverter.ToInt16(memory, start + 1);
            Token a = ReadToken(start + 3);
            t.text = "If (!(" + a.text + ")) Goto(0x" + offset.ToString("X") + ");";
            int pos = start + 3 + a.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadLet(int start)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1);
            int pos = start + 1;
            t.text = a.text;
            pos += a.raw.Length;
            t.text += " = ";
            Token b = ReadToken(pos);
            t.text += b.text;
            pos += b.raw.Length;
            t.text += ";";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadReturn(int start)
        {
            Token t = new Token();
            t.text = "Return (";
            Token a = ReadToken(start + 1);
            t.text += a.text + ");";
            int len = 1 + a.raw.Length;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadNativeParm(int start)
        {
            Token t = new Token();
            t.text = "";
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            if (index > 0 && index <= pcc.Exports.Count)
            {
                string name = pcc.getObjectName(index);
                string clas = pcc.Exports[index - 1].ClassName;
                clas = clas.Replace("Property", "");
                t.text += clas + " " + name + ";";
            }
            if (index * -1 > 0 && index * -1 <= pcc.Exports.Count)
            {
                string name = pcc.getObjectName(index);
                string clas = pcc.Imports[index * -1 - 1].ClassName;
                clas = clas.Replace("Property", "");
                t.text += clas + " " + name + ";";
            }
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadLocalVar(int start)
        {
            Token t = new Token();
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 < memsize)
            {
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            }
            return t;
        }

        private static Token ReadInstanceVar(int start)
        {
            Token t = new Token();
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            t.text = pcc.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadJump(int start)
        {
            Token t = new Token();
            t.text = "Goto(0x";
            ME1Explorer.BitConverter.IsLittleEndian = true;
            int index = ME1Explorer.BitConverter.ToInt16(memory, start + 1);
            t.text += index.ToString("X") + ")";
            t.raw = new byte[3];
            for (int i = 0; i < 3; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadLableTable(int start)
        {
            Token t = new Token();
            t.text = "";
            ME1Explorer.BitConverter.IsLittleEndian = false;
            int index = ME1Explorer.BitConverter.ToInt32(memory, start + 1);
            ME1Explorer.BitConverter.IsLittleEndian = true;
            if (index >= 0 && index < pcc.Names.Count)
                t.text = pcc.getNameEntry(index);
            else
                t.text = "Label (" + index.ToString("X2") + ");";
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private static Token ReadUnknown(int start)
        {
            Token t = new Token();
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            t.text = "\\\\Unknown Token (0x" + t.raw[0].ToString("X") + ")\\\\";
            return t;
        }

        private static Token ReadNothing(int start)
        {
            Token t = new Token();
            t.text = "";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private static Token ReadEndOfScript(int start)
        {
            Token t = new Token();
            t.text = "\\\\End of Script";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

    }
}
