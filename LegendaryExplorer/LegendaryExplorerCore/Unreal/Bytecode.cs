using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using LegendaryExplorerCore.Gammtek.IO;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.TLK;

namespace LegendaryExplorerCore.Unreal
{
    public class Bytecode
    {
        // IF ANYONE COMMITS THINGS
        // OUT OF ORDER TO THIS LIST
        // I SWEAR TO GOD
        // I WILL FIND YOU
        // AND BEAT YOU OVER THE HEAD
        // WITH YOUR KEYBOARD
        // - Mgamerz
        public static readonly Dictionary<short, string> byteOpnameMap = new Dictionary<short, string>
        {
            {0x0000, "EX_LocalVariable"},
            {0x0001, "EX_InstanceVariable"},
            {0x0002, "EX_DefaultVariable"},
            {0x0004, "EX_Return"},
            {0x0005, "EX_Switch"},
            {0x0006, "EX_Jump"},
            {0x0007, "EX_JumpIfNot"},
            {0x0008, "EX_Stop"},
            {0x0009, "EX_Assert"},
            {0x000A, "EX_Case"},
            {0x000B, "EX_Nothing"},
            {0x000C, "EX_LabelTable"},
            {0x000D, "EX_GotoLabel"},
            {0x000E, "EX_EatReturnValue"},
            {0x000F, "EX_Let"},
            {0x0010, "EX_DynArrayElement"},
            {0x0011, "EX_New"},
            {0x0012, "EX_ClassContext"},
            {0x0013, "EX_Metacast"},
            {0x0014, "EX_LetBool"},
            {0x0015, "EX_EndParmValue"},
            {0x0016, "EX_EndFunctionParms"},
            {0x0017, "EX_Self"},
            {0x0018, "EX_Skip"},
            {0x0019, "EX_Context"},
            {0x001A, "EX_ArrayElement"},
            {0x001B, "EX_VirtualFunction"},
            {0x001C, "EX_FinalFunction"},
            {0x001D, "EX_IntConst"},
            {0x001E, "EX_FloatConst"},
            {0x001F, "EX_StringConst"},
            {0x0020, "EX_ObjectConst"},
            {0x0021, "EX_NameConst"},
            {0x0022, "EX_RotationConst"},
            {0x0023, "EX_VectorConst"},
            {0x0024, "EX_ByteConst"},
            {0x0025, "EX_IntZero"},
            {0x0026, "EX_IntOne"},
            {0x0027, "EX_True"},
            {0x0028, "EX_False"},
            {0x0029, "EX_NativeParm"},
            {0x002A, "EX_NoObject"},
            {0x002C, "EX_IntConstByte"},
            {0x002D, "EX_BoolVariable"},
            {0x002E, "EX_DynamicCast"},
            {0x002F, "EX_Iterator"},
            {0x0030, "EX_IteratorPop"},
            {0x0031, "EX_IteratorNext"},
            {0x0032, "EX_StructCmpEq"},
            {0x0033, "EX_StructCmpNe"},
            {0x0034, "EX_UnicodeStringConst"},
            {0x0035, "EX_StructMember"},
            {0x0036, "EX_DynArrayLength"},
            {0x0037, "EX_GlobalFunction"},
            {0x0038, "EX_PrimitiveCast"},
            {0x0039, "EX_DynArrayInsert"},
            {0x003A, "EX_ReturnNothing"},
            {0x003B, "EX_EqualEqual_DelDel"},
            {0x003C, "EX_NotEqual_DelDel"},
            {0x003D, "EX_EqualEqual_DelFunc"},
            {0x003E, "EX_NotEqual_DelFunc"},
            {0x003F, "EX_EmptyDelegate"},
            {0x0040, "EX_DynArrayRemove"},
            {0x0041, "EX_DebugInfo"},
            {0x0042, "EX_DelegateFunction"},
            {0x0043, "EX_DelegateProperty"},
            {0x0044, "EX_LetDelegate"},
            {0x0045, "EX_Conditional"},
            {0x0046, "EX_DynArrayFind"},
            {0x0047, "EX_DynArrayFindStruct"},
            {0x0048, "EX_LocalOutVariable"},
            {0x0049, "EX_DefaultParmValue"},
            {0x004A, "EX_EmptyParmValue"},
            {0x004B, "EX_InstanceDelegate"},
            {0x004F, "EX_StringRefConst"},
            {0x0050, "EX_GoW_DefaultValue"},
            {0x0051, "EX_InterfaceContext"},
            {0x0052, "EX_InterfaceCast"},
            {0x0053, "EX_EndOfScript"},
            {0x0054, "EX_DynArrayAdd"},
            {0x0055, "EX_DynArrayAddItem"},
            {0x0056, "EX_DynArrayRemoveItem"},
            {0x0057, "EX_DynArrayInsertItem"},
            {0x0058, "EX_DynArrayIterator"},
            {0x0059, "EX_DynArraySort"},
            {0x005A, "EX_FilterEditorOnly"},
            {0x005B, "EX_LocalFloatVariable"},
            {0x005C, "EX_LocalIntVariable"},
            {0x005D, "EX_LocalByteVariable"},
            {0x005E, "EX_LocalObjectVariable"},
            {0x005F, "EX_InstanceFloatVariable"},
            {0x0060, "EX_InstanceIntVariable"},
            {0x0061, "EX_InstanceByteVariable"},
            {0x0062, "EX_InstanceObjectVariable"},
            {0x0063, "EX_OptIfLocal"},
            {0x0064, "EX_OptIfInstance"},
            {0x0065, "EX_NamedFunction"},
            #region PS3 Only (Only checked against ME1)
            {0x0070, "NATIVE_PS3_Concat_StrStr"},
            {0x0071, "NATIVE_PS3_GotoState"},
            {0x0072, "NATIVE_PS3_EqualEqual_ObjectObject"},
            {0x0073, "NATIVE_PS3_Less_StrStr"},
            {0x0074, "NATIVE_PS3_Greater_StrStr"},
            {0x0075, "NATIVE_PS3_Enable"},
            {0x0076, "NATIVE_PS3_Disable"},
            {0x0077, "NATIVE_PS3_NotEqual_ObjectObject"},
            {0x0078, "NATIVE_PS3_LessEqual_StrStr"},
            {0x0079, "NATIVE_PS3_GreaterEqual_StrStr"},
            {0x007A, "NATIVE_PS3_EqualEqual_StrStr"},
            {0x007B, "NATIVE_PS3_NotEqual_StrStr" },
            {0x007C, "NATIVE_PS3_ComplementEqual_StrStr"},
            {0x007D, "NATIVE_PS3_Len"},
            {0x007E, "NATIVE_PS3_InStr"},
            {0x007F, "NATIVE_PS3_Mid"},
            {0x0080, "NATIVE_PS3_Left"},
            #endregion
            {0x0081, "NATIVE_Not_PreBool"},
            {0x0082, "NATIVE_AndAnd_BoolBool"},
            {0x0083, "NATIVE_XorXor_BoolBool"},
            {0x0084, "NATIVE_OrOr_BoolBool"},
            {0x0085, "NATIVE_MultiplyEqual_ByteByte"},
            {0x0086, "NATIVE_DivideEqual_ByteByte"},
            {0x0087, "NATIVE_AddEqual_ByteByte"},
            {0x0088, "NATIVE_SubtractEqual_ByteByte"},
            {0x0089, "NATIVE_AddAdd_PreByte"},
            {0x008A, "NATIVE_SubtractSubtract_PreByte"},
            {0x008B, "NATIVE_AddAdd_Byte"},
            {0x008C, "NATIVE_SubtractSubtract_Byte"},
            {0x008D, "NATIVE_Complement_PreInt"},
            {0x008E, "NATIVE_EqualEqual_RotatorRotator"},
            {0x008F, "NATIVE_Subtract_PreInt"},
            {0x0090, "NATIVE_Multiply_IntInt"},
            {0x0091, "NATIVE_Divide_IntInt"},
            {0x0092, "NATIVE_Add_IntInt"},
            {0x0093, "NATIVE_Subtract_IntInt"},
            {0x0094, "NATIVE_LessLess_IntInt"},
            {0x0095, "NATIVE_GreaterGreater_IntInt"},
            {0x0096, "NATIVE_Less_IntInt"},
            {0x0097, "NATIVE_Greater_IntInt"},
            {0x0098, "NATIVE_LessEqual_IntInt"},
            {0x0099, "NATIVE_GreaterEqual_IntInt"},
            {0x009A, "NATIVE_EqualEqual_IntInt"},
            {0x009B, "NATIVE_NotEqual_IntInt"},
            {0x009C, "NATIVE_And_IntInt"},
            {0x009D, "NATIVE_Xor_IntInt"},
            {0x009E, "NATIVE_Or_IntInt"},
            {0x009F, "NATIVE_MultiplyEqual_IntFloat"},
            {0x00A0, "NATIVE_DivideEqual_IntFloat"},
            {0x00A1, "NATIVE_AddEqual_IntInt"},
            {0x00A2, "NATIVE_SubtractEqual_IntInt"},
            {0x00A3, "NATIVE_AddAdd_PreInt"},
            {0x00A4, "NATIVE_SubtractSubtract_PreInt"},
            {0x00A5, "NATIVE_AddAdd_Int"},
            {0x00A6, "NATIVE_SubtractSubtract_Int"},
            {0x00A7, "NATIVE_Rand"},
            {0x00A8, "NATIVE_At_StrStr"},
            {0x00A9, "NATIVE_Subtract_PreFloat"},
            {0x00AA, "NATIVE_MultiplyMultiply_FloatFloat"},
            {0x00AB, "NATIVE_Multiply_FloatFloat"},
            {0x00AC, "NATIVE_Divide_FloatFloat"},
            {0x00AD, "NATIVE_Percent_FloatFloat"},
            {0x00AE, "NATIVE_Add_FloatFloat"},
            {0x00AF, "NATIVE_Subtract_FloatFloat"},
            {0x00B0, "NATIVE_Less_FloatFloat"},
            {0x00B1, "NATIVE_Greater_FloatFloat"},
            {0x00B2, "NATIVE_LessEqual_FloatFloat"},
            {0x00B3, "NATIVE_GreaterEqual_FloatFloat"},
            {0x00B4, "NATIVE_EqualEqual_FloatFloat"},
            {0x00B5, "NATIVE_NotEqual_FloatFloat"},
            {0x00B6, "NATIVE_MultiplyEqual_FloatFloat"},
            {0x00B7, "NATIVE_DivideEqual_FloatFloat"},
            {0x00B8, "NATIVE_AddEqual_FloatFloat"},
            {0x00B9, "NATIVE_SubtractEqual_FloatFloat"},
            {0x00BA, "NATIVE_Abs"},
            {0x00BB, "NATIVE_Sin"},
            {0x00BC, "NATIVE_Cos"},
            {0x00BD, "NATIVE_Tan"},
            {0x00BE, "NATIVE_Atan"},
            {0x00BF, "NATIVE_Exp"},
            {0x00C0, "NATIVE_Loge"},
            {0x00C1, "NATIVE_Sqrt"},
            {0x00C2, "NATIVE_Square"},
            {0x00C3, "NATIVE_FRand"},
            {0x00C4, "NATIVE_GreaterGreaterGreater_IntInt"},
            {0x00C5, "NATIVE_IsA"},
            {0x00C6, "NATIVE_MultiplyEqual_ByteFloat"},
            {0x00C7, "NATIVE_Round"},
            {0x00C9, "NATIVE_Repl"},
            {0x00CB, "NATIVE_NotEqual_RotatorRotator"},
            {0x00D2, "NATIVE_ComplementEqual_FloatFloat"},
            {0x00D3, "NATIVE_Subtract_PreVector"},
            {0x00D4, "NATIVE_Multiply_VectorFloat"},
            {0x00D5, "NATIVE_Multiply_FloatVector"},
            {0x00D6, "NATIVE_Divide_VectorFloat"},
            {0x00D7, "NATIVE_Add_VectorVector"},
            {0x00D8, "NATIVE_Subtract_VectorVector"},
            {0x00D9, "NATIVE_EqualEqual_VectorVector"},
            {0x00DA, "NATIVE_NotEqual_VectorVector"},
            {0x00DB, "NATIVE_Dot_VectorVector"},
            {0x00DC, "NATIVE_Cross_VectorVector"},
            {0x00DD, "NATIVE_MultiplyEqual_VectorFloat"},
            {0x00DE, "NATIVE_DivideEqual_VectorFloat"},
            {0x00DF, "NATIVE_AddEqual_VectorVector"},
            {0x00E0, "NATIVE_SubtractEqual_VectorVector"},
            {0x00E1, "NATIVE_VSize"},
            {0x00E2, "NATIVE_Normal"},
            {0x00E5, "NATIVE_GetAxes"},
            {0x00E6, "NATIVE_GetUnAxes"},
            {0x00E7, "NATIVE_LogInternal"},
            {0x00E8, "NATIVE_WarnInternal"},
            {0x00EA, "NATIVE_Right"},
            {0x00EB, "NATIVE_Caps"},
            {0x00EC, "NATIVE_Chr"},
            {0x00ED, "NATIVE_Asc"},
            {0x00EE, "NATIVE_Locs"},
            {0x00F2, "NATIVE_EqualEqual_BoolBool"},
            {0x00F3, "NATIVE_NotEqual_BoolBool"},
            {0x00F4, "NATIVE_FMin"},
            {0x00F5, "NATIVE_FMax"},
            {0x00F6, "NATIVE_FClamp"},
            {0x00F7, "NATIVE_Lerp"},
            {0x00F9, "NATIVE_Min"},
            {0x00FA, "NATIVE_Max"},
            {0x00FB, "NATIVE_Clamp"},
            {0x00FC, "NATIVE_VRand"},
            {0x00FD, "NATIVE_Percent_IntInt"},
            {0x00FE, "NATIVE_EqualEqual_NameName"},
            {0x00FF, "NATIVE_NotEqual_NameName"},
            {0x0100, "NATIVE_Sleep"},
            {0x0102, "NATIVE_ClassIsChildOf"},
            {0x0105, "NATIVE_FinishAnim"},
            {0x0106, "NATIVE_SetCollision"},
            {0x010A, "NATIVE_Move"},
            {0x010B, "NATIVE_SetLocation"},
            {0x010E, "NATIVE_Add_QuatQuat"},
            {0x010F, "NATIVE_Subtract_QuatQuat"},
            {0x0110, "NATIVE_SetOwner"},
            {0x0113, "NATIVE_LessLess_VectorRotator"},
            {0x0114, "NATIVE_GreaterGreater_VectorRotator"},
            {0x0115, "NATIVE_Trace"},
            {0x0116, "NATIVE_Spawn"},
            {0x0117, "NATIVE_Destroy"},
            {0x0118, "NATIVE_SetTimer"},
            {0x0119, "NATIVE_IsInState"},
            {0x011B, "NATIVE_SetCollisionSize"},
            {0x011C, "NATIVE_GetStateName"},
            {0x011F, "NATIVE_Multiply_RotatorFloat"},
            {0x0120, "NATIVE_Multiply_FloatRotator"},
            {0x0121, "NATIVE_Divide_RotatorFloat"},
            {0x0122, "NATIVE_MultiplyEqual_RotatorFloat"},
            {0x0123, "NATIVE_DivideEqual_RotatorFloat"},
            {0x0128, "NATIVE_Multiply_VectorVector"},
            {0x0129, "NATIVE_MultiplyEqual_VectorVector"},
            {0x012A, "NATIVE_SetBase"},
            {0x012B, "NATIVE_SetRotation"},
            {0x012C, "NATIVE_MirrorVectorByNormal"},
            {0x0130, "NATIVE_AllActors"},
            {0x0131, "NATIVE_ChildActors"},
            {0x0132, "NATIVE_BasedActors"},
            {0x0133, "NATIVE_TouchingActors"},
            {0x0135, "NATIVE_TraceActors"},
            {0x0137, "NATIVE_VisibleActors"},
            {0x0138, "NATIVE_VisibleCollidingActors"},
            {0x0139, "NATIVE_DynamicActors"},
            {0x013C, "NATIVE_Add_RotatorRotator"},
            {0x013D, "NATIVE_Subtract_RotatorRotator"},
            {0x013E, "NATIVE_AddEqual_RotatorRotator"},
            {0x013F, "NATIVE_SubtractEqual_RotatorRotator"},
            {0x0140, "NATIVE_RotRand"},
            {0x0141, "NATIVE_CollidingActors"},
            {0x0142, "NATIVE_ConcatEqual_StrStr"},
            {0x0143, "NATIVE_AtEqual_StrStr"},
            {0x0144, "NATIVE_SubtractEqual_StrStr"},
            {0x01F4, "NATIVE_MoveTo"},
            {0x01F6, "NATIVE_MoveToward"},
            {0x01FC, "NATIVE_FinishRotation"},
            {0x0200, "NATIVE_MakeNoise"},
            {0x0202, "NATIVE_LineOfSightTo"},
            {0x0205, "NATIVE_FindPathToward"},
            {0x0206, "NATIVE_FindPathTo"},
            {0x0208, "NATIVE_ActorReachable"},
            {0x0209, "NATIVE_PointReachable"},
            {0x020C, "NATIVE_FindStairRotation"},
            {0x020D, "NATIVE_FindRandomDest"},
            {0x020E, "NATIVE_PickWallAdjust"},
            {0x020F, "NATIVE_WaitForLanding"},
            {0x0213, "NATIVE_PickTarget"},
            {0x0214, "NATIVE_PlayerCanSeeMe"},
            {0x0215, "NATIVE_CanSee"},
            {0x0218, "NATIVE_SaveConfig"},
            {0x0219, "NATIVE_CanSeeByPoints"},
            {0x0222, "NATIVE_UpdateURL"},
            {0x0223, "NATIVE_GetURLMap"},
            {0x0224, "NATIVE_FastTrace"},
            {0x0258, "NATIVE_Concat_StrStr"},
            {0x0259, "NATIVE_Less_StrStr"},
            {0x025A, "NATIVE_Greater_StrStr"},
            {0x025B, "NATIVE_LessEqual_StrStr"},
            {0x025C, "NATIVE_GreaterEqual_StrStr"},
            {0x025D, "NATIVE_EqualEqual_StrStr"},
            {0x025E, "NATIVE_NotEqual_StrStr"},
            {0x025F, "NATIVE_ComplementEqual_StrStr"},
            {0x026C, "NATIVE_GotoState"},
            {0x0280, "NATIVE_EqualEqual_ObjectObject"},
            {0x0281, "NATIVE_NotEqual_ObjectObject"},
            {0x028A, "NATIVE_Len"},
            {0x028B, "NATIVE_InStr"},
            {0x028C, "NATIVE_Mid"},
            {0x028D, "NATIVE_Left"},
            {0x03E8, "NATIVE_EqualEqual_StringRefStringRef"},
            {0x03E9, "NATIVE_EqualEqual_StringRefInt"},
            {0x03EA, "NATIVE_EqualEqual_IntStringRef"},
            {0x03EB, "NATIVE_NotEqual_StringRefStringRef"},
            {0x03EC, "NATIVE_NotEqual_StringRefInt"},
            {0x03ED, "NATIVE_NotEqual_IntStringRef"},
            {0x05DC, "NATIVE_ProjectOnTo"},
            {0x05DD, "NATIVE_IsZero"},
            {0x0F81, "NATIVE_MoveSmooth"},
            {0x0F82, "NATIVE_SetPhysics"},
            {0x0F83, "NATIVE_AutonomousPhysics"},
            };
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
        private const int EX_ReturnNothing = 0x3A;
        private const int EX_EqualEqual_DelDel = 0x3B;
        private const int EX_NotEqual_DelDel = 0x3C;
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
        private const int EX_StringRefConst = 0x4F;
        private const int EX_GoW_DefaultValue = 0x50;
        private const int EX_InterfaceContext = 0x51;
        private const int EX_InterfaceCast = 0x52;
        private const int EX_EndOfScript = 0x53;
        private const int EX_DynArrayAdd = 0x54;
        private const int EX_DynArrayAddItem = 0x55;
        private const int EX_DynArrayRemoveItem = 0x56;
        private const int EX_DynArrayInsertItem = 0x57;
        private const int EX_DynArrayIterator = 0x58;
        private const int EX_DynArraySort = 0x59;
        private const int EX_FilterEditorOnly = 0x5A;
        private const int EX_LocalFloatVariable = 0x5B;
        private const int EX_LocalIntVariable = 0x5C;
        private const int EX_LocalByteVariable = 0x5D;
        private const int EX_LocalObjectVariable = 0x5E;
        private const int EX_InstanceFloatVariable = 0x5F;
        private const int EX_InstanceIntVariable = 0x60;
        private const int EX_InstanceByteVariable = 0x61;
        private const int EX_InstanceObjectVariable = 0x62;
        private const int EX_OptIfLocal = 0x63;
        private const int EX_OptIfInstance = 0x64;
        private const int EX_NamedFunction = 0x65;

        #endregion

        enum ENatives
        {
            #region PS3 Only (Only checked against ME1)
            NATIVE_PS3_Concat_StrStr = 0x70,
            NATIVE_PS3_GotoState = 0x71,
            NATIVE_PS3_EqualEqual_ObjectObject = 0x72,
            NATIVE_PS3_Less_StrStr = 0x73,
            NATIVE_PS3_Greater_StrStr = 0x74,
            //NATIVE_PS3_Enable = 0x75, // Defined below
            //NATIVE_PS3_Disable = 0x76, // Defined below
            NATIVE_PS3_NotEqual_ObjectObject = 0x77,
            NATIVE_PS3_LessEqual_StrStr = 0x78,
            NATIVE_PS3_GreaterEqual_StrStr = 0x79,
            NATIVE_PS3_EqualEqual_StrStr = 0x7A,
            NATIVE_PS3_NotEqual_StrStr = 0x7B,
            NATIVE_PS3_ComplementEqual_StrStr = 0x7C,
            NATIVE_PS3_Len = 0x7D,
            NATIVE_PS3_InStr = 0x7E,
            NATIVE_PS3_Mid = 0x7F,
            NATIVE_PS3_Left = 0x80,
            #endregion

            NATIVE_SaveConfig = 0x0218,
            NATIVE_Disable = 0x0076,
            NATIVE_Enable = 0x0075,
            NATIVE_GetStateName = 0x011C,
            NATIVE_IsInState = 0x0119,
            NATIVE_GotoState = 0x026C,
            NATIVE_WarnInternal = 0x00E8,
            NATIVE_LogInternal = 0x00E7,
            NATIVE_NotEqual_IntStringRef = 0x03ED,
            NATIVE_NotEqual_StringRefInt = 0x03EC,
            NATIVE_NotEqual_StringRefStringRef = 0x03EB,
            NATIVE_EqualEqual_IntStringRef = 0x03EA,
            NATIVE_EqualEqual_StringRefInt = 0x03E9,
            NATIVE_EqualEqual_StringRefStringRef = 0x03E8,
            NATIVE_Subtract_QuatQuat = 0x010F,
            NATIVE_Add_QuatQuat = 0x010E,
            NATIVE_NotEqual_NameName = 0x00FF,
            NATIVE_EqualEqual_NameName = 0x00FE,
            NATIVE_IsA = 0x00C5,

            NATIVE_Sleep = 0x0100, //ME3 Engine.pcc
            NATIVE_ClassIsChildOf = 0x0102,
            NATIVE_PS3_ClassIsChildOf = 0x0258,
            NATIVE_NotEqual_ObjectObject = 0x0281,
            NATIVE_EqualEqual_ObjectObject = 0x0280,
            NATIVE_Repl = 0x00C9,
            NATIVE_Asc = 0x00ED,
            NATIVE_Chr = 0x00EC,
            NATIVE_Locs = 0x00EE,
            NATIVE_Caps = 0x00EB,
            NATIVE_Right = 0x00EA,
            NATIVE_Left = 0x028D,
            NATIVE_Mid = 0x028C,
            NATIVE_InStr = 0x028B,
            NATIVE_Len = 0x028A,
            NATIVE_SubtractEqual_StrStr = 0x0144,
            NATIVE_AtEqual_StrStr = 0x0143,
            NATIVE_ConcatEqual_StrStr = 0x0142,
            NATIVE_ComplementEqual_StrStr = 0x025F,
            NATIVE_NotEqual_StrStr = 0x025E,
            NATIVE_EqualEqual_StrStr = 0x025D,
            NATIVE_GreaterEqual_StrStr = 0x025C,
            NATIVE_LessEqual_StrStr = 0x025B,
            NATIVE_Greater_StrStr = 0x025A,
            NATIVE_Less_StrStr = 0x0259,
            NATIVE_At_StrStr = 0x00A8,
            NATIVE_Concat_StrStr = 0x0258,
            NATIVE_RotRand = 0x0140,
            NATIVE_GetUnAxes = 0x00E6,
            NATIVE_GetAxes = 0x00E5,
            NATIVE_SubtractEqual_RotatorRotator = 0x013F,
            NATIVE_AddEqual_RotatorRotator = 0x013E,
            NATIVE_Subtract_RotatorRotator = 0x013D,
            NATIVE_Add_RotatorRotator = 0x013C,
            NATIVE_DivideEqual_RotatorFloat = 0x0123,
            NATIVE_MultiplyEqual_RotatorFloat = 0x0122,
            NATIVE_Divide_RotatorFloat = 0x0121,
            NATIVE_Multiply_FloatRotator = 0x0120,
            NATIVE_Multiply_RotatorFloat = 0x011F,
            NATIVE_NotEqual_RotatorRotator = 0x00CB,
            NATIVE_EqualEqual_RotatorRotator = 0x008E,
            NATIVE_IsZero = 0x05DD,
            NATIVE_ProjectOnTo = 0x05DC,
            NATIVE_MirrorVectorByNormal = 0x012C,
            NATIVE_VRand = 0x00FC,
            NATIVE_Normal = 0x00E2,
            NATIVE_VSize = 0x00E1,
            NATIVE_SubtractEqual_VectorVector = 0x00E0,
            NATIVE_AddEqual_VectorVector = 0x00DF,
            NATIVE_DivideEqual_VectorFloat = 0x00DE,
            NATIVE_MultiplyEqual_VectorVector = 0x0129,
            NATIVE_MultiplyEqual_VectorFloat = 0x00DD,
            NATIVE_Cross_VectorVector = 0x00DC,
            NATIVE_Dot_VectorVector = 0x00DB,
            NATIVE_NotEqual_VectorVector = 0x00DA,
            NATIVE_EqualEqual_VectorVector = 0x00D9,
            NATIVE_GreaterGreater_VectorRotator = 0x0114,
            NATIVE_LessLess_VectorRotator = 0x0113,
            NATIVE_Subtract_VectorVector = 0x00D8,
            NATIVE_Add_VectorVector = 0x00D7,
            NATIVE_Divide_VectorFloat = 0x00D6,
            NATIVE_Multiply_VectorVector = 0x0128,
            NATIVE_Multiply_FloatVector = 0x00D5,
            NATIVE_Multiply_VectorFloat = 0x00D4,
            NATIVE_Subtract_PreVector = 0x00D3,
            NATIVE_Lerp = 0x00F7,
            NATIVE_FClamp = 0x00F6,
            NATIVE_FMax = 0x00F5,
            NATIVE_FMin = 0x00F4,
            NATIVE_FRand = 0x00C3,
            NATIVE_Round = 0x00C7,
            NATIVE_Square = 0x00C2,
            NATIVE_Sqrt = 0x00C1,
            NATIVE_Loge = 0x00C0,
            NATIVE_Exp = 0x00BF,
            NATIVE_Atan = 0x00BE,
            NATIVE_Tan = 0x00BD,
            NATIVE_Cos = 0x00BC,
            NATIVE_Sin = 0x00BB,
            NATIVE_Abs = 0x00BA,
            NATIVE_SubtractEqual_FloatFloat = 0x00B9,
            NATIVE_AddEqual_FloatFloat = 0x00B8,
            NATIVE_DivideEqual_FloatFloat = 0x00B7,
            NATIVE_MultiplyEqual_FloatFloat = 0x00B6,
            NATIVE_NotEqual_FloatFloat = 0x00B5,
            NATIVE_ComplementEqual_FloatFloat = 0x00D2,
            NATIVE_EqualEqual_FloatFloat = 0x00B4,
            NATIVE_GreaterEqual_FloatFloat = 0x00B3,
            NATIVE_LessEqual_FloatFloat = 0x00B2,
            NATIVE_Greater_FloatFloat = 0x00B1,
            NATIVE_Less_FloatFloat = 0x00B0,
            NATIVE_Subtract_FloatFloat = 0x00AF,
            NATIVE_Add_FloatFloat = 0x00AE,
            NATIVE_Percent_IntInt = 0x00FD,
            NATIVE_Percent_FloatFloat = 0x00AD,
            NATIVE_Divide_FloatFloat = 0x00AC,
            NATIVE_Multiply_FloatFloat = 0x00AB,
            NATIVE_MultiplyMultiply_FloatFloat = 0x00AA,
            NATIVE_Subtract_PreFloat = 0x00A9,
            NATIVE_Clamp = 0x00FB,
            NATIVE_Max = 0x00FA,
            NATIVE_Min = 0x00F9,
            NATIVE_Rand = 0x00A7,
            NATIVE_SubtractSubtract_Int = 0x00A6,
            NATIVE_AddAdd_Int = 0x00A5,
            NATIVE_SubtractSubtract_PreInt = 0x00A4,
            NATIVE_AddAdd_PreInt = 0x00A3,
            NATIVE_SubtractEqual_IntInt = 0x00A2,
            NATIVE_AddEqual_IntInt = 0x00A1,
            NATIVE_DivideEqual_IntFloat = 0x00A0,
            NATIVE_MultiplyEqual_IntFloat = 0x009F,
            NATIVE_Or_IntInt = 0x009E,
            NATIVE_Xor_IntInt = 0x009D,
            NATIVE_And_IntInt = 0x009C,
            NATIVE_NotEqual_IntInt = 0x009B,
            NATIVE_EqualEqual_IntInt = 0x009A,
            NATIVE_GreaterEqual_IntInt = 0x0099,
            NATIVE_LessEqual_IntInt = 0x0098,
            NATIVE_Greater_IntInt = 0x0097,
            NATIVE_Less_IntInt = 0x0096,
            NATIVE_GreaterGreaterGreater_IntInt = 0x00C4,
            NATIVE_GreaterGreater_IntInt = 0x0095,
            NATIVE_LessLess_IntInt = 0x0094,
            NATIVE_Subtract_IntInt = 0x0093,
            NATIVE_Add_IntInt = 0x0092,
            NATIVE_Divide_IntInt = 0x0091,
            NATIVE_Multiply_IntInt = 0x0090,
            NATIVE_Subtract_PreInt = 0x008F,
            NATIVE_Complement_PreInt = 0x008D,
            NATIVE_SubtractSubtract_Byte = 0x008C,
            NATIVE_AddAdd_Byte = 0x008B,
            NATIVE_SubtractSubtract_PreByte = 0x008A,
            NATIVE_AddAdd_PreByte = 0x0089,
            NATIVE_SubtractEqual_ByteByte = 0x0088,
            NATIVE_AddEqual_ByteByte = 0x0087,
            NATIVE_DivideEqual_ByteByte = 0x0086,
            NATIVE_MultiplyEqual_ByteFloat = 0x00C6,
            NATIVE_MultiplyEqual_ByteByte = 0x0085,
            NATIVE_OrOr_BoolBool = 0x0084,
            NATIVE_XorXor_BoolBool = 0x0083,
            NATIVE_AndAnd_BoolBool = 0x0082,
            NATIVE_NotEqual_BoolBool = 0x00F3,
            NATIVE_EqualEqual_BoolBool = 0x00F2,
            NATIVE_Not_PreBool = 0x0081,
            NATIVE_CollidingActors = 0x0141,
            NATIVE_VisibleCollidingActors = 0x0138,
            NATIVE_VisibleActors = 0x0137,
            NATIVE_TraceActors = 0x0135,
            NATIVE_TouchingActors = 0x0133,
            NATIVE_BasedActors = 0x0132,
            NATIVE_ChildActors = 0x0131,
            NATIVE_DynamicActors = 0x0139,
            NATIVE_AllActors = 0x0130,
            NATIVE_GetURLMap = 0x0223,
            NATIVE_PlayerCanSeeMe = 0x0214,
            NATIVE_MakeNoise = 0x0200,
            NATIVE_SetTimer = 0x0118,
            NATIVE_Destroy = 0x0117,
            NATIVE_Spawn = 0x0116,
            NATIVE_FastTrace = 0x0224,
            NATIVE_Trace = 0x0115,
            NATIVE_SetPhysics = 0x0F82,
            NATIVE_SetOwner = 0x0110,
            NATIVE_SetBase = 0x012A,
            NATIVE_AutonomousPhysics = 0x0F83,
            NATIVE_MoveSmooth = 0x0F81,
            NATIVE_SetRotation = 0x012B,
            NATIVE_SetLocation = 0x010B,
            NATIVE_Move = 0x010A,
            NATIVE_SetCollisionSize = 0x011B,
            NATIVE_SetCollision = 0x0106,
            NATIVE_FinishAnim = 0x0105,
            NATIVE_WaitForLanding = 0x020F,
            NATIVE_PickWallAdjust = 0x020E,
            NATIVE_ActorReachable = 0x0208,
            NATIVE_PointReachable = 0x0209,
            NATIVE_FindRandomDest = 0x020D,
            NATIVE_FindPathToward = 0x0205,
            NATIVE_FindPathTo = 0x0206,
            NATIVE_FinishRotation = 0x01FC,
            NATIVE_MoveToward = 0x01F6,
            NATIVE_MoveTo = 0x01F4,
            NATIVE_PickTarget = 0x0213,
            NATIVE_CanSeeByPoints = 0x0219,
            NATIVE_CanSee = 0x0215,
            NATIVE_LineOfSightTo = 0x0202,
            NATIVE_FindStairRotation = 0x020C,
            NATIVE_UpdateURL = 0x0222
        };

        enum ECastToken : byte
        {
            InterfaceToObject = 0x36,
            InterfaceToString = 0x37,
            InterfaceToBool = 0x38,
            RotatorToVector = 0x39,
            ByteToInt = 0x3A,
            ByteToBool = 0x3B,
            ByteToFloat = 0x3C,
            IntToByte = 0x3D,
            IntToBool = 0x3E,
            IntToFloat = 0x3F,
            BoolToByte = 0x40,
            BoolToInt = 0x41,
            BoolToFloat = 0x42,
            FloatToByte = 0x43,
            FloatToInt = 0x44,
            FloatToBool = 0x45,
            ObjectToInterface = 0x46,
            ObjectToBool = 0x47,
            NameToBool = 0x48,
            StringToByte = 0x49,
            StringToInt = 0x4A,
            StringToBool = 0x4B,
            StringToFloat = 0x4C,
            StringToVector = 0x4D,
            StringToRotator = 0x4E,
            VectorToBool = 0x4F,
            VectorToRotator = 0x50,
            RotatorToBool = 0x51,
            ByteToString = 0x52,
            IntToString = 0x53,
            BoolToString = 0x54,
            FloatToString = 0x55,
            ObjectToString = 0x56,
            NameToString = 0x57,
            VectorToString = 0x58,
            RotatorToString = 0x59,
            DelegateToString = 0x5A,
            StringRefToInt = 0x5B,
            StringRefToString = 0x5C,
            IntToStringRef = 0x5D,
            StringToName = 0x60
        };

        public static (List<Token>, List<BytecodeSingularToken>) ParseBytecode(byte[] raw, ExportEntry export)
        {
            int pos = export.Game is MEGame.UDK ? export.IsClass ? 0x28 : 0x30 : export.IsClass ? 0x18 : 0x20;
            var parser = new Bytecode(raw, pos);

            List<Token> tokens = parser.ReadAll(0, export);

            //calculate padding width.
            Token lastToken = tokens.LastOrDefault();
            int totalLength = Math.Max(lastToken?.pos ?? 0, lastToken?.memPos ?? 0);

            //calculate block position and assign paddingwidth.
            int paddingSize = totalLength.ToString("X").Length;
            foreach (Token tok in tokens)
            {
                tok.pos = pos;
                tok.paddingSize = paddingSize;
                pos += tok.raw.Length;
            }
            parser.SingularTokens.Sort();
            return (tokens, parser.SingularTokens);
        }

        private Bytecode(byte[] mem, int byteCodeStart)
        {
            memory = mem;
            memsize = memory.Length;
            DebugCounter = 0;
            SingularTokens = new List<BytecodeSingularToken>();
            ByteCodeStart = byteCodeStart;
        }

        private readonly byte[] memory;
        private readonly int memsize;
        private readonly List<BytecodeSingularToken> SingularTokens;
        private int DebugCounter;
        private readonly int ByteCodeStart;

        private List<Token> ReadAll(int start, ExportEntry export)
        {
            var res = new List<Token>();
            int pos = start;
            try
            {
                Token t = ReadToken(pos, export);
                res.Add(t);

                while (!t.stop)
                {
                    try
                    {
                        pos += t.raw.Length;
                        t = ReadToken(pos, export);
                        res.Add(t);
                    }
                    catch (Exception e)
                    {
                        res.Add(new Token { text = "Exception: " + e.Message, raw = new byte[] { } });
                    }
                }

                // rest is part of footer
            }
            catch (Exception e)
            {
                // ????
                res.Add(new Token() { text = $"Final parsing pos: 0x{pos:X5}, Exception parsing bytecode: {e.Message}", raw = new byte[] { } });
            }

            return res;
        }

        private Token ReadToken(int start, ExportEntry export)
        {
            int thiscount = DebugCounter;
            DebugCounter++;
            Token res = new Token
            {
                text = "",
                raw = new byte[1],
                stop = true
            };
            if (start >= memsize)
                return res;
            byte t = memory[start];
            Token newTok = new Token { op = t };
            int end = start;
            if ((t <= 0x65 && export.Game.IsGame3())
                || (t < 0x60 && (export.FileRef.Platform == MEPackage.GamePlatform.PS3 || (export.Game is MEGame.ME1 or MEGame.ME2 or MEGame.LE1 or MEGame.LE2 or MEGame.UDK)))) //PS3 uses ME3 engine but ME1/ME2 use PC native index which are different
            {
                switch (t)
                {
                    case EX_LocalVariable: //0x00
                        newTok = ReadLocalVar(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DefaultVariable: //0x02
                    case EX_InstanceVariable: //0x01
                        newTok = ReadInstanceVar(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Return: //0x04
                        newTok = ReadReturn(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Switch: //0x05
                        newTok = ReadSwitch(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Jump: //0x06
                        newTok = ReadJump(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_JumpIfNot: //0x07
                        newTok = ReadJumpIfNot(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Stop: //0x08
                        newTok = ReadStopToken(start, export);
                        newTok.stop = false;
                        res = newTok;
                        break;
                    case EX_Assert: // 0x09
                        newTok = ReadAssert(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Case: // 0x0A
                        newTok = ReadCase(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Nothing: // 0x0B
                        newTok = ReadNothing(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_LabelTable: //0x0C
                        newTok = ReadLableTable(start, export);
                        newTok.stop = false; //don't parse more bytecode
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_GotoLabel: //0xD
                        var innerTok = ReadToken(start + 1, export);
                        newTok.text = "GotoLabel: " + innerTok.text;
                        newTok.stop = false;
                        // 06/19/2022 - Copy the inner token inPackageReferences list
                        // so it relinks the token name, since this just discards it it seems.
                        // - Mgamerz
                        newTok.inPackageReferences = innerTok.inPackageReferences;
                        newTok.raw = memory.Slice(start, 1 + innerTok.raw.Length);
                        //newTok.raw = start+ + newTok
                        end = start + 1 + innerTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EatReturnValue: // 0x0E
                        newTok = ReadEatReturn(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Let://0x0F
                    case EX_LetBool: //0x14
                    case EX_LetDelegate: //0x44
                        newTok = ReadLet(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ArrayElement: // 0x1A
                    case EX_DynArrayElement: // 0x10
                        newTok = ReadDynArrayElement(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_New: //0x11
                        newTok = ReadNew(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Metacast: // 0x13
                        newTok = ReadMetacast(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndParmValue: // 0x15
                        newTok = ReadEndParmVal(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndFunctionParms: // 0x16
                        newTok = ReadEndFuncParm(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Self: //  0x17
                        newTok = ReadSelf(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Skip: // 0x18
                        newTok = ReadSkip(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ClassContext://0x12
                    case EX_Context: // 0x19
                        newTok = ReadContext(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_VirtualFunction: // 0x1B
                        newTok = ReadVirtualFunc(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_FinalFunction: // 0x1C
                        newTok = ReadFinalFunc(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntConst: // 0x1D
                        newTok = ReadIntConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_FloatConst: // 0x1E
                        newTok = ReadStatFloat(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StringConst: //0x1F
                        newTok = ReadStringConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ObjectConst: // 0x20
                        newTok = ReadObjectConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NameConst: // 0x21
                        newTok = ReadNameConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_RotationConst: // 0x22
                        newTok = ReadRotatorConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_VectorConst: // 0x23
                        newTok = ReadVectorConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntConstByte: // 0x2C
                    case EX_ByteConst: //0x24
                        newTok = ReadByteConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntZero: // 0x25
                        newTok = ReadZero(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IntOne: //0x26
                        newTok = ReadIntOne(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_True: // 0x27
                        newTok = ReadTrue(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_False: // 0x28
                        newTok = ReadFalse(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NativeParm: //0x29
                        newTok = ReadNativeParm(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NoObject: // 0x2A
                        newTok = ReadNone(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_BoolVariable: // 0x2D
                        newTok = ReadBoolExp(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynamicCast: // 0x2E
                        newTok = ReadDynCast(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Iterator: //0x2F
                        newTok = ReadIterator(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IteratorPop: // 0x30
                        newTok = ReadIterPop(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_IteratorNext: //0x31
                        newTok = ReadIterNext(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructCmpEq: // 0x32
                        newTok = ReadCompareStructs(start, "==", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructCmpNe: // 0x33
                        newTok = ReadCompareStructs(start, "!=", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StructMember: //0x35
                        newTok = ReadStruct(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayLength: //0x36
                        newTok = ReadDynArrayLen(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_GlobalFunction: // 0x37
                        newTok = ReadGlobalFunc(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_PrimitiveCast: //0x38
                        newTok = ReadPrimitiveCast(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayInsert: // 0x39
                        newTok = ReadArrayArg2(start, "Insert", false, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_ReturnNothing: // 0x3A
                        newTok = ReadReturnNothing(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EqualEqual_DelDel: // 0x3B
                        newTok = ReadEqual(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NotEqual_DelDel: // 0x3C
                        newTok = ReadCompareDel(start, "!=", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EqualEqual_DelFunc: // 0x3D
                        newTok = ReadCompareDel2(start, "==", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EmptyDelegate: // 0x3F
                        newTok = ReadEmptyDel(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayRemove: // 0x40
                        newTok = ReadArrayArg2(start, "Remove", false, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DelegateFunction: // 0x42
                        newTok = ReadDelFunc(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DelegateProperty: // 0x43
                        newTok = ReadDelegateProp(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_Conditional: // 0x45
                        newTok = ReadConditional(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayFind:// 0x46
                        newTok = ReadArrayArg(start, "Find", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayFindStruct: //0x47
                        newTok = ReadArrayArg2(start, "Find", true, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_LocalOutVariable: //0x48
                        newTok = ReadLocOutVar(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DefaultParmValue: // 0x49
                        newTok = ReadDefaultParmVal(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EmptyParmValue: //0x4A                    
                        newTok = ReadEmptyParm(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InstanceDelegate: // 0x4B
                        newTok = ReadInstDelegate(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_StringRefConst: // 0x4F
                        newTok = ReadStringRefConst(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_GoW_DefaultValue: //0x50
                        newTok = ReadGoWVal(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InterfaceContext:// 0x51
                        newTok = ReadInterfaceContext(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_InterfaceCast: // 0x52
                        newTok = ReadDynCast(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_EndOfScript:    //0x53                    
                        newTok = ReadEndOfScript(start, export);
                        newTok.stop = true;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayAdd: // 0x54
                        newTok = ReadAdd(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayAddItem: //0x55
                        newTok = ReadArrayArg(start, "Add", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayRemoveItem: //0x56
                        newTok = ReadArrayArg(start, "Remove", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayInsertItem: //0x57
                        newTok = ReadArrayArg(start, "Insert", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArrayIterator: // 0x58
                        newTok = ReadDynArrayItr(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_DynArraySort: // 0x59
                        newTok = ReadArrayArg(start, "Sort", export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_FilterEditorOnly: // 0x5A
                        newTok = ReadFilterEditorOnly(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_LocalObjectVariable: // 0x5E
                    case EX_LocalFloatVariable: // 0x5B
                    case EX_LocalIntVariable: // 0x5C                                    
                    case EX_LocalByteVariable: // 0x5D
                    case EX_InstanceFloatVariable: // 0x5F
                    case EX_InstanceIntVariable: // 0x60
                    case EX_InstanceByteVariable: //0x61
                    case EX_InstanceObjectVariable: //0x62
                        newTok = ReadUnkn4(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_OptIfLocal: //0x63
                        newTok = ReadObjectConditionalJump(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_OptIfInstance: //0x64
                        newTok = ReadObjectConditionalJump(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    case EX_NamedFunction: //0x65
                        newTok = ReadNamedFunction(start, export);
                        newTok.stop = false;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                    default:
                        //throw exception here perhaps? 
                        newTok = ReadUnknown(start, export);
                        newTok.stop = true;
                        end = start + newTok.raw.Length;
                        res = newTok;
                        break;
                }
            }
            else if (t < (export.Game is MEGame.ME1 or MEGame.ME2 or MEGame.LE1 or MEGame.LE2 or MEGame.UDK ? 0x60 : 0x70)) //PS3 uses 0x60 as native table
            {
                //Never reached?
                // Is this right? Extended Native is 0x61
                newTok = ReadExtNative(start, export);
                newTok.stop = false;
                end = start + newTok.raw.Length;
                res = newTok;
            }
            else
            {
                newTok = ReadNative(start, export);
                newTok.stop = false;
                end = start + newTok.raw.Length;
                res = newTok;
            }
            BytecodeSingularToken msg = new BytecodeSingularToken();
            byteOpnameMap.TryGetValue(t < 0x66 ? t : newTok.op, out string opname); // Should this be updated to 0x
            if (string.IsNullOrEmpty(opname))
            {
                Debug.WriteLine("FOUND DEFINED OPCODE, BUT WITHOUT A DEFINED NAME");
                opname = $"UNKNOWN(0x{t:X2})";
            }

            string op = $"[0x{t:X2}] {opname}";
            string data = res.text;
            msg.OpCode = t < 0x66 ? t : newTok.op;
            msg.OpCodeString = op;
            msg.CurrentStack = data;
            msg.TokenIndex = thiscount;
            msg.StartPos = start + ByteCodeStart;
            SingularTokens.Add(msg);
            return res;
        }

        private Token ReadNative(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = null, b = null, c = null;
            int count;

            // This doesn't work as the native call IDs seem to maybe have changed
            // So while some native calls keep same ID, others don't
            int nativeId = export.Game is MEGame.ME1 or MEGame.ME2 or MEGame.LE1 or MEGame.LE2 or MEGame.UDK ? 0x60 : 0x70;

            // Determine if this is an extended native
            byte byte1 = memory[start];
            byte byte2 = memory[start + 1];

            int index;
            if ((byte1 & 0xF0) == nativeId)
                index = ((byte1 - nativeId) << 8) + byte2;
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
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);

                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    break;
                case (int)ENatives.NATIVE_AndAnd_BoolBool: //0x0082
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + " && " + b.text;
                    break;
                case (int)ENatives.NATIVE_XorXor_BoolBool:  //0x0083
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + " ^^ " + b.text;
                    break;
                case (int)ENatives.NATIVE_OrOr_BoolBool://0x0084
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + " || " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_ByteByte://  0x0085
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_ByteByte: // 0x0086
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_ByteByte: // 0x0087
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_ByteByte: // 0x0088
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractSubtract_PreByte: // 0x008A
                case (int)ENatives.NATIVE_SubtractSubtract_Byte: // 0x008C
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + "--";
                    break;
                case (int)ENatives.NATIVE_AddAdd_Byte: // 0x008B
                case (int)ENatives.NATIVE_AddAdd_PreByte: // 0x0089
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + "++";
                    break;
                case (int)ENatives.NATIVE_Complement_PreInt: // 0x008D
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "~(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_RotatorRotator: // 0x008E
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreInt: // 0x008F
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_Multiply_IntInt:// 0x0090
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " * " + b.text;
                    break;
                case (int)ENatives.NATIVE_Divide_IntInt:// 0x0091
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " / " + b.text;
                    break;
                case (int)ENatives.NATIVE_Add_IntInt:// 0x0092
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " + " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_IntInt:// 0x0093
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " - " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessLess_IntInt: // 0x0094
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " << " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterGreater_IntInt: // 0x0095
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " >> " + b.text;
                    break;
                case (int)ENatives.NATIVE_Less_IntInt: // 0x0096
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " < " + b.text;
                    break;
                case (int)ENatives.NATIVE_Greater_IntInt://0x0097
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " > " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessEqual_IntInt: // 0x0098
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " <= " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterEqual_IntInt: // 0x0099
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " >= " + b.text;
                    break;
                case (int)ENatives.NATIVE_EqualEqual_IntInt: // 0x009A
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_IntInt: // 0x009B
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_And_IntInt: // 0x009C
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " & " + b.text;
                    break;
                case (int)ENatives.NATIVE_Xor_IntInt: // 0x009D
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " ^ " + b.text;
                    break;
                case (int)ENatives.NATIVE_Or_IntInt: // 0x009E
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " | " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_IntFloat: // 0x009F
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_IntFloat: // 0x00A0
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_IntInt: // 0x00A1
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_IntInt: // 0x00A2
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddAdd_PreInt: // 0x00A3
                case (int)ENatives.NATIVE_AddAdd_Int: // 0x00A5
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + "++";
                    break;
                case (int)ENatives.NATIVE_SubtractSubtract_PreInt: // 0x00A4
                case (int)ENatives.NATIVE_SubtractSubtract_Int: // 0x00A6
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = a.text + "--";
                    break;
                case (int)ENatives.NATIVE_Rand: // 0x00A7
                    t.text = "Rand(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_At_StrStr: // 0x00A8
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " @ " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreFloat: // 0x00A9
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyMultiply_FloatFloat: // 0x00AA
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " ** " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Multiply_FloatFloat: // 0x00AB
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " * " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Divide_FloatFloat: // 0x00AC
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " / " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Add_FloatFloat: // 0x00AE
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " + " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Subtract_FloatFloat: // 0x00AF
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " - " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Less_FloatFloat: // 0x00B0
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " < " + b.text;
                    break;
                case (int)ENatives.NATIVE_Greater_FloatFloat: // 0x00B1
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " > " + b.text;
                    break;
                case (int)ENatives.NATIVE_LessEqual_FloatFloat: // 0x00B2
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " <= " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterEqual_FloatFloat: // 0x00B3
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " >= " + b.text;
                    break;
                case (int)ENatives.NATIVE_EqualEqual_FloatFloat: // 0x00B4
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_FloatFloat: // 0x00B5
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_FloatFloat: // 0x00B6
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_FloatFloat: // 0x00B7
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_FloatFloat: // 0x00B8
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_FloatFloat: // 0x00B9
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_Abs: // 0x00BA
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Abs(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Sin: // 0x00BB
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Sin(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Cos: // 0x00BC
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Cos(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Tan: // 0x00BD
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Tan(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Atan: // 0x00BE
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "ATan(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Exp: // 0x00BF
                    t.text = "Exp(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Loge: // 0x00C0
                    t.text = "Loge(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Sqrt: // 0x00C1
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Sqrt(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Square: // 0x00C2
                    t.text = "Sqr(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FRand:// 0x00C3
                    t.text = "FRand()";
                    pos++;
                    ReadToken(pos, export); //EndFunctionParms
                    break;
                case (int)ENatives.NATIVE_IsA: // 0x00C5
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "IsA(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_ByteFloat: // 0x00C6
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_Round: //0x00C7
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = $"Round({a.text})";
                    break;
                case (int)ENatives.NATIVE_Repl: // 0x00C9
                    t.text = "Repl(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_NotEqual_RotatorRotator: // 0x00CB
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_ComplementEqual_FloatFloat: // 0x00D2
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " ~= " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_PreVector: // 0x00D3
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "-" + a.text;
                    break;
                case (int)ENatives.NATIVE_Divide_VectorFloat: // 0x00D6
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " / " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Multiply_VectorFloat: // 0x00D4
                case (int)ENatives.NATIVE_Multiply_VectorVector: // 0x0128
                case (int)ENatives.NATIVE_Multiply_FloatVector: // 0x00D5
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " * " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Add_VectorVector: // 0x00D7
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " + " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_Subtract_VectorVector: //0x00D8
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " - " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_VectorVector: // 0x00D9
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_VectorVector: // 0x00DA
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_Dot_VectorVector: // 0x00DB
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " dot " + b.text;
                    break;
                case (int)ENatives.NATIVE_Cross_VectorVector: // 0x00DC
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " cross " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_VectorFloat: // 0x00DE
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_VectorFloat: // 0x00DD
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_VectorVector: // 0x00DF
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_VectorVector: // 0x00E0
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_VSize: // 0x00E1
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "VSize(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_Normal: // 0x00E2
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    t.text = "Normal(" + a.text + ")";
                    break;
                case (int)ENatives.NATIVE_GetAxes: // 0x00E5
                    t.text = "GetAxes(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;//
                case (int)ENatives.NATIVE_Right: // 0x00EA
                    t.text = "Right(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Caps: // 0x00EB
                    t.text = "Caps(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Chr: // 0x00EC
                    t.text = "Chr(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Asc: // 0x00ED
                    t.text = "Asc(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Locs: // 0x00EE
                    t.text = "Locs(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_BoolBool://0x00F2
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_BoolBool://0x00F3
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_FMin: // 0x00F4
                    t.text = "Min(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FMax: //  0x00F5
                    t.text = "Max(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FClamp: //  0x00F6
                    t.text = "Clamp(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Lerp: // 0x00F7
                    t.text = "Lerp(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Min: // 0x00F9
                    t.text = "Min(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Max: // 0x00FA
                    t.text = "Max(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Clamp: // 0x00FB
                    t.text = "Clamp(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_VRand: // 0x00FC
                    t.text = "VRand(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Percent_FloatFloat: // 0x00AD
                case (int)ENatives.NATIVE_Percent_IntInt: // 0x00FD
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " % " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_NameName: // 0x00FE
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_NameName: // 0x00FF
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_Sleep: // 0x0100 - Defined in ME3 Engine.pcc
                    t.text = "Sleep(";
                    a = ReadToken(pos, export);
                    t.text += a.text;
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    if (b.op != EX_EndFunctionParms)
                    {
                        t.text += "Should be EX_EndFunctionParms!";
                    }

                    pos += b.raw.Length;
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FinishRotation: // 0x01FC - Defined in ME3 Engine.pcc
                    t.text = "FinishRotation()";
                    //a = ReadToken(pos, export);
                    //t.text += a.text;
                    //pos += a.raw.Length;
                    //b = ReadToken(pos, export);
                    //if (b.op != EX_EndFunctionParms)
                    //{
                    //    t.text += "Should be EX_EndFunctionParms!";
                    //}

                    pos += 1;//skip EndFuncParams
                             //t.text += ")";
                    break;
                //case (int)ENatives.NATIVE_PS3_ClassIsChildOf when export.FileRef.Platform == MEPackage.GamePlatform.PS3: //0x0258
                case (int)ENatives.NATIVE_ClassIsChildOf:// 0x0102
                    t.text = "ClassIsChildOf(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetCollision: // 0x0106
                    t.text = "SetCollision(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetLocation: // 0x010B
                    t.text = "SetLocation(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetOwner: // 0x0110
                    t.text = "SetOwner(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_LessLess_VectorRotator: // 0x0113
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " << " + b.text;
                    break;
                case (int)ENatives.NATIVE_GreaterGreater_VectorRotator: // 0x0114
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);//EndParam
                    pos += c.raw.Length;
                    t.text = a.text + " >> " + b.text;
                    break;
                case (int)ENatives.NATIVE_Trace: // 0x0115
                    t.text = "Trace(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Destroy:  // 0x0117
                    t.text = "Destroy(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetTimer: //0x0118
                    t.text = "SetTimer(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;//
                case (int)ENatives.NATIVE_IsInState: // 0x0119
                    t.text = "IsInState(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetCollisionSize: // 0x011B
                    t.text = "SetCollisionSize(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_GetStateName: // 0x011C
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    t.text = "GetStatename()";
                    break;
                case (int)ENatives.NATIVE_Enable: // 0x75
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export); // End function parms?
                    pos += b.raw.Length;
                    t.text = $"Enable({a.text})";
                    break;
                case (int)ENatives.NATIVE_Disable: // 0x76
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export); // End function parms?
                    pos += b.raw.Length;
                    t.text = $"Disable({a.text})";
                    break;
                case (int)ENatives.NATIVE_Multiply_RotatorFloat: // 0x011F
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " * " + b.text;
                    break;
                case (int)ENatives.NATIVE_Multiply_FloatRotator: // 0x0120
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " * " + b.text;
                    break;
                case (int)ENatives.NATIVE_Divide_RotatorFloat: // 0x0121
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " / " + b.text;
                    break;
                case (int)ENatives.NATIVE_MultiplyEqual_RotatorFloat: // 0x0122
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " *= " + b.text;
                    break;
                case (int)ENatives.NATIVE_DivideEqual_RotatorFloat: // 0x0123
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " /= " + b.text;
                    break;
                case (int)ENatives.NATIVE_SetBase: // 0x012A
                    t.text = "SetBase(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetRotation: // 0x012B
                    t.text = "SetRotation(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_AllActors:// 0x0130
                    t.text = "AllActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_ChildActors: // 0x0131
                    t.text = "ChildActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_BasedActors: // 0x0132
                    t.text = "BasedActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_TouchingActors: // 0x0133
                    t.text = "TouchingActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_TraceActors: // 0x0135
                    t.text = "TraceActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_VisibleActors: // 0x0137
                    t.text = "VisibleActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_VisibleCollidingActors: // 0x0138
                    t.text = "VisibleCollidingActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_DynamicActors: // 0x0139
                    t.text = "DynamicActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_Add_RotatorRotator: // 0x013C
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " + " + b.text;
                    break;
                case (int)ENatives.NATIVE_Subtract_RotatorRotator: // 0x013D
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " - " + b.text;
                    break;
                case (int)ENatives.NATIVE_AddEqual_RotatorRotator: // 0x013E
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " += " + b.text;
                    break;
                case (int)ENatives.NATIVE_SubtractEqual_RotatorRotator: // 0x013F
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " -= " + b.text;
                    break;
                case (int)ENatives.NATIVE_CollidingActors: // 0x0141
                    t.text = "CollidingActors(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_ConcatEqual_StrStr:// 0x0142
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " $= " + b.text;
                    break;
                case (int)ENatives.NATIVE_AtEqual_StrStr:// 0x0143
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " @= " + b.text;
                    break;
                case (int)ENatives.NATIVE_MakeNoise: // 0x0200
                    t.text = "MakeNoise(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_LineOfSightTo: // 0x0202
                    t.text = "LineOfSightTo(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FindPathToward: // 0x0205
                    t.text = "FindPathToward(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FindPathTo: // 0x0206
                    t.text = "FindPathTo(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_ActorReachable: // 0x0208
                    t.text = "ActorReachable(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PointReachable: // 0x0209
                    t.text = "PointReachable(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PickTarget: // 0x0213
                    t.text = "PickTarget(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PlayerCanSeeMe: // 0x0214
                    t.text = "PlayerCanSeeMe(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SaveConfig: // 0x0218
                    t.text = "SaveConfig();";
                    break;
                case (int)ENatives.NATIVE_UpdateURL: // 0x0222
                    t.text = "UpdateURL(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_GetURLMap: // 0x0223
                    t.text = "GetURLMap(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_FastTrace: // 0x0224
                    t.text = "FastTrace(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += ", " + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_CanSeeByPoints:
                    t.text = "CanSeeByPoints(";
                    int i = 3;
                    while (i > 0)
                    {
                        if (i != 3) t.text += ",";
                        i--;
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        t.text += a.text;
                    }
                    pos += ReadToken(pos, export).raw.Length; // End Function Parms
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PS3_Concat_StrStr:
                case (int)ENatives.NATIVE_Concat_StrStr: // 0x0258
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " $ " + b.text;
                    break;
                case (int)ENatives.NATIVE_PS3_Less_StrStr:
                case (int)ENatives.NATIVE_Less_StrStr: // 0x0259
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " < " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_Greater_StrStr:
                case (int)ENatives.NATIVE_Greater_StrStr: // 0x025A
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " > " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_LessEqual_StrStr:
                case (int)ENatives.NATIVE_LessEqual_StrStr: // 0x025B
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " <= " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_GreaterEqual_StrStr:
                case (int)ENatives.NATIVE_GreaterEqual_StrStr: // 0x025C
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " >= " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_EqualEqual_StrStr:
                case (int)ENatives.NATIVE_EqualEqual_StrStr: // 0x025D
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " == " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_NotEqual_StrStr: // 0x7B
                case (int)ENatives.NATIVE_NotEqual_StrStr: // 0x025E
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " != " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_ComplementEqual_StrStr:
                case (int)ENatives.NATIVE_ComplementEqual_StrStr: //0x025F  
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " ~= " + b.text;
                    break;
                case (int)ENatives.NATIVE_WarnInternal:
                    a = ReadToken(pos, export); //String
                    pos += a.raw.Length;
                    b = ReadToken(pos, export); // End function parms
                    pos += b.raw.Length;
                    t.text = $"WarnInternal({a.text}, {b.text})";
                    break;
                case (int)ENatives.NATIVE_LogInternal:
                    a = ReadToken(pos, export); // String
                    pos += a.raw.Length;
                    b = ReadToken(pos, export); // Log name? Interpolation params?
                    pos += b.raw.Length;
                    c = ReadToken(pos, export); // End function parms
                    pos += c.raw.Length;
                    t.text = $"LogInternal({a.text}, {b.text})";
                    break;
                case (int)ENatives.NATIVE_PS3_GotoState:
                case (int)ENatives.NATIVE_GotoState: // 0x026C
                    t.text = "GotoState(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.text != "null")
                            t.text += a.text;
                        else if (!(a.raw.Length == 1 && a.raw[0] == 0x4A))
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PS3_EqualEqual_ObjectObject:
                case (int)ENatives.NATIVE_EqualEqual_ObjectObject: // 0x0280
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " == " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_NotEqual_ObjectObject:
                case (int)ENatives.NATIVE_NotEqual_ObjectObject: // 0x0281
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = "(" + a.text + " != " + b.text + ")";
                    break;
                case (int)ENatives.NATIVE_PS3_Len:
                case (int)ENatives.NATIVE_Len: // 0x028A
                    t.text = "Len(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PS3_InStr:
                case (int)ENatives.NATIVE_InStr: // 0x028B
                    t.text = "InStr(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PS3_Mid:
                case (int)ENatives.NATIVE_Mid: // 0x028C
                    t.text = "Mid(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_PS3_Left:
                case (int)ENatives.NATIVE_Left: // 0x028D
                    t.text = "Left(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_EqualEqual_StringRefStringRef: // 0x03E8
                case (int)ENatives.NATIVE_EqualEqual_StringRefInt: // 0x03E9
                case (int)ENatives.NATIVE_EqualEqual_IntStringRef: // 0x03EA
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " == " + b.text;
                    break;
                case (int)ENatives.NATIVE_NotEqual_StringRefStringRef: // 0x03EB
                case (int)ENatives.NATIVE_NotEqual_StringRefInt: // 0x03EC
                case (int)ENatives.NATIVE_NotEqual_IntStringRef: // 0x03ED
                    a = ReadToken(pos, export);
                    pos += a.raw.Length;
                    b = ReadToken(pos, export);
                    pos += b.raw.Length;
                    c = ReadToken(pos, export);
                    pos += c.raw.Length;
                    t.text = a.text + " != " + b.text;
                    break;
                case (int)ENatives.NATIVE_ProjectOnTo: // 0x05DC
                    t.text = "ProjectOnTo(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_IsZero: // 0x05DD
                    t.text = "IsZero(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0 && a.raw[0] != 0x4A)
                            t.text += "," + a.text;
                        else if (a.raw[0] != 0x4A)
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_MoveSmooth: // 0x0F81
                    t.text = "MoveSmooth(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_SetPhysics: //  0x0F82
                    t.text = "SetPhysics(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_AutonomousPhysics: // 0x0F83
                    t.text = "AutonomousPhysics(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_MoveToward: // 0x01F6
                    t.text = "MoveToward(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                case (int)ENatives.NATIVE_MoveTo: // 0x01F4
                    t.text = "MoveTo(";
                    count = 0;
                    while (pos < memsize)
                    {
                        a = ReadToken(pos, export);
                        pos += a.raw.Length;
                        if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
                            break;
                        if (count != 0)
                            t.text += "," + a.text;
                        else
                            t.text += a.text;
                        count++;
                        t.inPackageReferences.AddRange(a.inPackageReferences);
                        a = null;
                    }
                    t.text += ")";
                    break;
                default:
                    Debug.WriteLine($"Found an unknown native: {index} 0x{index:X4}!");
                    t.text = "UnknownNative(" + index + ")";
                    break;
            }
            if (a != null)
            {
                t.inPackageReferences.AddRange(a.inPackageReferences);
            }
            if (b != null)
            {
                t.inPackageReferences.AddRange(b.inPackageReferences);
            }
            if (c != null)
            {
                t.inPackageReferences.AddRange(c.inPackageReferences);
            }
            t.op = (short)index;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadFilterEditorOnly(int start, ExportEntry export)
        {
            Token t = new Token();
            int offset = EndianReader.ToInt16(memory, start + 1, export.FileRef.Endian);
            t.text = $"If Not In Editor Goto(0x{offset:X});";
            int pos = start + 3;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadGoWVal(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 2;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            pos += a.raw.Length;
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadStringRefConst(int start, ExportEntry export)
        {
            Token t = new Token();
            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            int pos = start + 5;
            t.text = $"${index}({ME3TalkFiles.FindDataById(index)})";

            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadAdd(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            t.text = "Add(";
            int count = 0;
            while (pos < memsize)
            {
                Token t2 = ReadToken(pos, export);
                t.inPackageReferences.AddRange(t2.inPackageReferences);

                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == EX_EndFunctionParms)
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

        private Token ReadCompareDel(int start, string arg, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            Token b = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            pos += b.raw.Length;
            Token endFunctionParams = ReadToken(pos, export);
            pos += endFunctionParams.raw.Length;
            t.text = a.text + " " + arg + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadCompareDel2(int start, string arg, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            Token b = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            pos += b.raw.Length;
            t.text = a.text + " " + arg + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDelFunc(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            pos += a.raw.Length;
            int index = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            t.inPackageReferences.Add((pos, Token.INPACKAGEREFTYPE_NAME, index));
            pos += 8;
            string s = export.FileRef.GetNameEntry(index);
            t.text = a.text + "." + s + "(";
            int count = 0;
            while (pos < memsize)
            {
                Token t2 = ReadToken(pos, export);
                t.inPackageReferences.AddRange(t2.inPackageReferences);

                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == EX_EndFunctionParms)
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

        private Token ReadGlobalFunc(int start, ExportEntry export)
        {
            Token t = new Token();
            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_NAME, index));

            t.text = "Global." + export.FileRef.GetNameEntry(index) + "(";
            int pos = start + 9;
            int count = 0;
            while (pos < memsize)
            {
                Token t2 = ReadToken(pos, export);
                t.inPackageReferences.AddRange(t2.inPackageReferences);

                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == EX_EndFunctionParms)
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

        private Token ReadCompareStructs(int start, string s, ExportEntry export)
        {
            Token t = new Token();
            int structType = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, structType));
            int pos = start + 5;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            Token b = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);

            pos += b.raw.Length;
            t.text = a.text + " " + s + " " + b.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadAssert(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 4;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = "assert(" + a.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadInterfaceContext(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            pos += a.raw.Length;
            t.text = a.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadNew(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            Token b = ReadToken(pos, export);
            pos += b.raw.Length;
            Token c = ReadToken(pos, export);
            pos += c.raw.Length;
            Token d = ReadToken(pos, export);
            pos += d.raw.Length;
            Token e = ReadToken(pos, export);
            pos += e.raw.Length;
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            t.inPackageReferences.AddRange(c.inPackageReferences);
            t.inPackageReferences.AddRange(d.inPackageReferences);
            t.inPackageReferences.AddRange(e.inPackageReferences);

            t.text = $"new({a.text},{b.text},{c.text},{d.text},{e.text})";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadArrayArg2(int start, string arg, bool skip2byte, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            if (skip2byte) pos += 2;
            Token b = ReadToken(pos, export);
            pos += b.raw.Length;
            Token c = ReadToken(pos, export);
            pos += c.raw.Length;
            pos += ReadToken(pos, export).raw.Length; // EX_EndFunctionParms
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            t.inPackageReferences.AddRange(c.inPackageReferences);

            t.text = a.text + "." + arg + "(" + b.text + "," + c.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadArrayArg(int start, string arg, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length + 2;
            Token b = ReadToken(pos, export);
            pos += b.raw.Length;
            Token c = ReadToken(pos, export);
            pos += c.raw.Length;
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            t.inPackageReferences.AddRange(c.inPackageReferences);

            t.text = a.text + "." + arg + "(" + b.text + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadCase(int start, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            int nextCaseLocation = EndianReader.ToInt16(memory, pos, export.FileRef.Endian);
            pos += 2;
            if (nextCaseLocation != -1)
            {
                Token a = ReadToken(pos, export);
                t.inPackageReferences.AddRange(a.inPackageReferences);

                pos += a.raw.Length;
                t.text = "Case " + a.text + ":";
            }
            else
            {
                t.text = "//End of Switch";
            }
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadMetacast(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            int uIndex = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            t.inPackageReferences.Add((pos, Token.INPACKAGEREFTYPE_ENTRY, uIndex));
            pos += 4;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            pos += a.raw.Length;
            t.text = $"Class<{export.FileRef.getObjectName(uIndex)}>({a.text})";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadVectorConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            float f1 = EndianReader.ToSingle(memory, pos, export.FileRef.Endian);
            float f2 = EndianReader.ToSingle(memory, pos + 4, export.FileRef.Endian);
            float f3 = EndianReader.ToSingle(memory, pos + 8, export.FileRef.Endian);
            t.text = "vect(" + f1 + ", " + f2 + ", " + f3 + ")";
            int len = 13;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadRotatorConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            int i1 = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            int i2 = EndianReader.ToInt32(memory, pos + 4, export.FileRef.Endian);
            int i3 = EndianReader.ToInt32(memory, pos + 8, export.FileRef.Endian);
            t.text = "rot(" + i1 + ", " + i2 + ", " + i3 + ")";
            int len = 13;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadConditional(int start, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            Token a = ReadToken(pos, export);
            pos += a.raw.Length;
            pos += 2;
            Token b = ReadToken(pos, export);
            pos += b.raw.Length;
            pos += 2;
            Token c = ReadToken(pos, export);
            pos += c.raw.Length;

            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);
            t.inPackageReferences.AddRange(c.inPackageReferences);

            //Token d = ReadToken(pos, export);
            //pos += d.raw.Length;
            t.text = "(" + a.text + ") ? " + b.text + " : " + c.text;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadEatReturn(int start, ExportEntry export)
        {
            Token t = new Token();
            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));
            Token a = ReadToken(start + 5, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = a.text;
            int len = 5 + a.raw.Length;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadObjectConditionalJump(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));
            bool check = memory[start + 5] != 0;
            int offset = EndianReader.ToInt16(memory, start + 6, export.FileRef.Endian);

            if (!check)
            {
                t.text = $"If ({export.FileRef.getObjectName(index)}) Goto(0x{offset:X});";
            }
            else
            {
                t.text = $"If (!{export.FileRef.getObjectName(index)}) Goto(0x{offset:X});";
            }
            int pos = start + 8;

            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadIntOne(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "1";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadEmptyDel(int start, ExportEntry export)
        {
            Token t = new Token
            {
                text = "EmptyDelegate",
                raw = new byte[1]
            };

            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadStruct(int start, ExportEntry export)
        {
            Token t = new Token();

            int field = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, field));

            int type = EndianReader.ToInt32(memory, start + 5, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 5, Token.INPACKAGEREFTYPE_ENTRY, type));
            int skip = EndianReader.ToInt16(memory, start + 7, export.FileRef.Endian);
            int pos = start + 11;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            pos += a.raw.Length;
            t.text = a.text + "." + export.FileRef.getObjectName(field);
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadPrimitiveCast(int start, ExportEntry export)
        {
            Token t = new Token();
            ECastToken conversionType = (ECastToken)memory[start + 1];
            Token a = ReadToken(start + 2, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            string castStr;
            if (Enum.IsDefined(typeof(ECastToken), conversionType))
            {
                castStr = conversionType.ToString();
            }
            else
            {
                Debug.WriteLine("FOUND UNKNOWN CAST");
                castStr = "UNKNOWN_CAST";
            }
            t.text = $"{castStr}({a.text})";
            int pos = start + a.raw.Length + 2;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadSkip(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a;
            int skip = EndianReader.ToInt16(memory, start + 1, export.FileRef.Endian);
            t.text = "";
            int count = 0;
            int pos = start + 3;
            while (pos < memsize)
            {
                //Debug.WriteLine("Readskip subtoken at " + pos.ToString("X8"));
                a = ReadToken(pos, export);
                t.inPackageReferences.AddRange(a.inPackageReferences);
                pos += a.raw.Length;
                if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
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

        private Token ReadNamedFunction(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            int nameIdx = EndianReader.ToInt32(memory, pos, export.FileRef.Endian);
            t.inPackageReferences.Add((pos, Token.INPACKAGEREFTYPE_NAME, nameIdx));
            pos += 8;
            int funcIndex = EndianReader.ToUInt16(memory, pos, export.FileRef.Endian); //index of this function in the class' Full Function List
            pos += 2;

            t.text = export.FileRef.GetNameEntry(nameIdx);
            t.text += "(";
            int count = 0;
            while (pos < memsize)
            {
                Token a = ReadToken(pos, export);
                t.inPackageReferences.AddRange(a.inPackageReferences);

                pos += a.raw.Length;
                if (a.raw != null && a.raw[0] == EX_EndFunctionParms)
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

        private Token ReadExtNative(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = export.FileRef.getObjectName(index);
            int pos = start + 11;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDynArrayElement(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            int pos = start + 1;
            t.text = "[" + a.text + "]";
            pos += a.raw.Length;
            Token b = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);

            t.text = b.text + t.text;
            pos += b.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDelLet(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 2, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            int pos = start + 2;
            t.text = "\t{\n\t\t" + a.text + "\n\t}";
            pos += a.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        public const string IterNextText = "//foreach continue";
        public const string IterPopText = "//foreach end";
        private Token ReadIterNext(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = IterNextText;
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadIterPop(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = IterPopText;
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadIterator(int start, ExportEntry export)
        {
            Token t = new Token();

            int pos = start + 1;
            Token a = ReadToken(pos, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            pos += a.raw.Length;
            ushort jumpoffset = EndianReader.ToUInt16(memory, pos, export.FileRef.Endian);
            pos += 2;
            t.text = "foreach(" + a.text + ") Goto(0x" + jumpoffset.ToString("X4") + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadStringConst(int start, ExportEntry export)
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

        private Token ReadDynCast(int start, ExportEntry export)
        {
            Token t = new Token();

            int idx = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, idx));

            Token a = ReadToken(start + 5, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = "(" + export.FileRef.getObjectName(idx) + ")" + a.text;
            int len = a.raw.Length + 5;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadUnkn4(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = export.FileRef.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDynArrayItr(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            Token arrayToken = ReadToken(pos, export);
            pos += arrayToken.raw.Length;

            //??? This appears to be bioware specific - 0x5E is not in unhood
            Token iteratorVariableToken = ReadToken(pos, export);
            pos += iteratorVariableToken.raw.Length;
            pos += 1;

            Token c = ReadToken(pos, export);
            pos += c.raw.Length;

            //Skip jump offset
            ushort jumpoffset = EndianReader.ToUInt16(memory, pos, export.FileRef.Endian);
            pos += 2;
            t.inPackageReferences.AddRange(arrayToken.inPackageReferences);
            t.inPackageReferences.AddRange(iteratorVariableToken.inPackageReferences);
            t.inPackageReferences.AddRange(c.inPackageReferences);

            t.text = "foreach " + (c.text != "null" ? c.text : "") + "(" + iteratorVariableToken.text + " IN " + arrayToken.text + ") //Goto (0x" + jumpoffset.ToString("X4") + ")";
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadBoolExp(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = a.text;
            int len = a.raw.Length + 1;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadLocOutVar(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = export.FileRef.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDynArrayLen(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            int pos = start + a.raw.Length + 1;
            int len = pos - start;
            t.text = a.text + ".Length";
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadByteConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int n = memory[start + 1];
            t.text = n.ToString();
            t.raw = new byte[2];
            for (int i = 0; i < 2; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadIntConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int n = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.text = n.ToString();
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadEmptyParm(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "null"; //normally ""
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadDelegateProp(int start, ExportEntry export)
        {
            Token t = new Token();

            int name = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_NAME, name));
            int uIndex = EndianReader.ToInt32(memory, start + 9, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 9, Token.INPACKAGEREFTYPE_ENTRY, uIndex));

            t.text = export.FileRef.GetNameEntry(name);
            t.raw = new byte[13];
            for (int i = 0; i < 13; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadInstDelegate(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_NAME, index));

            t.text = export.FileRef.GetNameEntry(index);
            t.raw = new byte[9];
            for (int i = 0; i < 9; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadObjectConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = " '" + export.FileRef.getObjectName(index) + "'";
            if (index > 0 && index <= export.FileRef.Exports.Count)
                t.text = export.FileRef.Exports[index - 1].ClassName + t.text;
            if (index * -1 > 0 && index * -1 <= export.FileRef.Imports.Count)
                t.text = export.FileRef.Imports[index * -1 - 1].ClassName + t.text;
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadFinalFunc(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));
            IEntry referenced = export.FileRef.GetEntry(index);
            if (referenced != null)
            {
                if (referenced.ObjectName == export.ObjectName && referenced != export)
                {
                    t.text = referenced.InstancedFullPath + "(";
                }
                else
                {
                    t.text = referenced.ObjectName.Instanced + "(";
                }
            }

            int pos = start + 5;
            int count = 0;
            while (pos < memsize)
            {
                Token t2 = ReadToken(pos, export);
                t.inPackageReferences.AddRange(t2.inPackageReferences);
                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == EX_EndFunctionParms)
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

        private Token ReadNameConst(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            int num = EndianReader.ToInt32(memory, start + 5, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_NAME, index));

            t.text = $"'{new NameReference(export.FileRef.GetNameEntry(index), num).Instanced}'";
            t.raw = new byte[9];
            for (int i = 0; i < 9; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadNone(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "None";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadZero(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "0";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadVirtualFunc(int start, ExportEntry export)
        {
            Token t = new Token();
            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_NAME, index));

            t.text = export.FileRef.GetNameEntry(index) + "(";
            int pos = start + 9;
            int count = 0;
            while (pos < memsize)
            {
                Token t2 = ReadToken(pos, export);
                t.inPackageReferences.AddRange(t2.inPackageReferences);

                pos += t2.raw.Length;
                if (t2.raw != null && t2.raw[0] == EX_EndFunctionParms)
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

        private Token ReadSelf(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "this";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadReturnNothing(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = "ReturnNothing(" + export.FileRef.getObjectName(index) + ")";
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadStatFloat(int start, ExportEntry export)
        {
            Token t = new Token();
            float f = EndianReader.ToSingle(memory, start + 1, export.FileRef.Endian);
            t.text = f + "f";
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadEndParmVal(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "//EndParmVal";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadEndFuncParm(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            t.op = EX_EndFunctionParms;
            return t;
        }

        private Token ReadTrue(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "True";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadFalse(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "False";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadContext(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            int pos = start + a.raw.Length + 1;
            int expSize = EndianReader.ToInt16(memory, pos, export.FileRef.Endian); //mem size to skip if a is null
            pos += 2;
            int unkRef = EndianReader.ToInt32(memory, pos, export.FileRef.Endian); //property corresponding to the return value 
            t.inPackageReferences.Add((pos, Token.INPACKAGEREFTYPE_ENTRY, unkRef));
            pos += 5; //skip trailing byte
            Token b = ReadToken(pos, export);
            t.inPackageReferences.AddRange(b.inPackageReferences);

            pos += b.raw.Length;
            int len = pos - start;
            t.text = a.text + "." + b.text;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadDefaultParmVal(int start, ExportEntry export)
        {
            Token t = new Token();
            int pos = start + 1;
            int size = EndianReader.ToInt16(memory, pos, export.FileRef.Endian);
            pos += 2;
            Token expression = ReadToken(pos, export);
            t.inPackageReferences.AddRange(expression.inPackageReferences);

            pos += expression.raw.Length;

            Token endParmToken = ReadToken(pos, export);
            pos += endParmToken.raw.Length;
            int len = pos - start;
            //var expression = ReadToken(pos, export);
            t.text = "DefaultParameterValue(" + expression.text + ");";
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadEqual(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            int pos = start + a.raw.Length + 1;
            Token b = ReadToken(pos, export);
            pos += b.raw.Length;
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);

            int len = pos - start;
            t.text = "(" + a.text + " == " + b.text + ")";
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadSwitch(int start, ExportEntry export)
        {
            Token t = new Token();
            int uIndex = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, uIndex));
            Token a = ReadToken(start + 6, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = "switch (" + a.text + ")";
            int len = a.raw.Length + 6;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadJumpIfNot(int start, ExportEntry export)
        {
            Token t = new Token();
            int offset = EndianReader.ToInt16(memory, start + 1, export.FileRef.Endian);
            Token a = ReadToken(start + 3, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text = "If (!(" + a.text + ")) Goto(0x" + offset.ToString("X") + ");";
            int pos = start + 3 + a.raw.Length;
            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadLet(int start, ExportEntry export)
        {
            Token t = new Token();
            Token a = ReadToken(start + 1, export);
            int pos = start + 1;
            t.text = a.text;
            pos += a.raw.Length;
            t.text += " = ";
            Token b = ReadToken(pos, export);
            t.text += b.text;
            pos += b.raw.Length;
            t.text += ";";
            t.inPackageReferences.AddRange(a.inPackageReferences);
            t.inPackageReferences.AddRange(b.inPackageReferences);

            int len = pos - start;
            t.raw = new byte[len];
            if (start + len <= memsize)
                for (int i = 0; i < len; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        public const string ReturnText = "Return (";

        private Token ReadReturn(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = ReturnText;
            Token a = ReadToken(start + 1, export);
            t.inPackageReferences.AddRange(a.inPackageReferences);

            t.text += a.text + ");";
            int len = 1 + a.raw.Length;
            t.raw = new byte[len];
            for (int i = 0; i < len; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadNativeParm(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "";

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            if (index > 0 && index <= export.FileRef.Exports.Count)
            {
                string name = export.FileRef.getObjectName(index);
                string clas = export.FileRef.Exports[index - 1].ClassName;
                clas = clas.Replace("Property", "");
                t.text += clas + " " + name + ";";
            }
            if (index * -1 > 0 && index * -1 <= export.FileRef.Exports.Count)
            {
                string name = export.FileRef.getObjectName(index);
                string clas = export.FileRef.Imports[index * -1 - 1].ClassName;
                clas = clas.Replace("Property", "");
                t.text += clas + " " + name + ";";
            }
            t.raw = new byte[5];
            for (int i = 0; i < 5; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadLocalVar(int start, ExportEntry export)
        {
            Token t = new Token();

            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = export.FileRef.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 < memsize)
            {
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            }
            return t;
        }

        private Token ReadInstanceVar(int start, ExportEntry export)
        {
            Token t = new Token();
            int index = EndianReader.ToInt32(memory, start + 1, export.FileRef.Endian);
            t.inPackageReferences.Add((start + 1, Token.INPACKAGEREFTYPE_ENTRY, index));

            t.text = export.FileRef.getObjectName(index);
            t.raw = new byte[5];
            if (start + 5 <= memsize)
                for (int i = 0; i < 5; i++)
                    t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadJump(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "Goto(0x";

            int index = EndianReader.ToInt16(memory, start + 1, export.FileRef.Endian);
            t.text += index.ToString("X") + ")";
            t.raw = new byte[3];
            for (int i = 0; i < 3; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadLableTable(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "Label Table:\n";

            //Labels:
            //NAME (8 bytes) Offset? (4 bytes)
            int endpos = start + 1; //skip opcode
            bool isFirst = true;
            while (endpos + 8 < memory.Length)
            {
                if (isFirst)
                    isFirst = false;
                else
                    t.text += "\n";

                int index = EndianReader.ToInt32(memory, endpos, export.FileRef.Endian);
                int offset = EndianReader.ToInt32(memory, endpos + 8, export.FileRef.Endian);
                if (export.FileRef.IsName(index))
                {
                    var name = export.FileRef.GetNameEntry(index);
                    if (name == "None")
                    {
                        //end of Label Table
                        t.text += "End of label table (None)";
                        t.inPackageReferences.Add((position: endpos, type: Token.INPACKAGEREFTYPE_NAME, value: index));
                        endpos += 8;
                        break;
                    }
                    else
                    {

                        t.text += name + " @ 0x" + offset.ToString("X8") + ")";
                        t.inPackageReferences.Add((position: endpos, type: Token.INPACKAGEREFTYPE_NAME, value: index));
                        endpos += 12;
                    }
                }
                else
                {
                    t.text += "Label (Invalid name " + index.ToString("X2") + " @ 0x" + offset.ToString("X8");
                    endpos += 12;
                }
            }

            var labelTableOffset = EndianReader.ToInt16(memory, endpos, export.FileRef.Endian); //In ME3 I think this is always 0xFFFF, which means there is no label table offset.
            t.text += "\nLabel table offset: 0x" + labelTableOffset.ToString("X4");

            endpos += 4; //Skip extra 2

            t.raw = new byte[endpos - start];
            for (int i = 0; i < endpos - start; i++)
                t.raw[i] = memory[start + i];
            return t;
        }

        private Token ReadUnknown(int start, ExportEntry export)
        {
            Debug.WriteLine($"FOUND UNKNOWN TOKEN IN {export?.InstancedFullPath}");

            Token t = new Token();
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            t.text = "\\\\Unknown Token (0x" + t.raw[0].ToString("X") + ")\\\\";
            return t;
        }

        private Token ReadNothing(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }
        private Token ReadStopToken(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "\\\\Stop?";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }

        private Token ReadEndOfScript(int start, ExportEntry export)
        {
            Token t = new Token();
            t.text = "\\\\End of Script";
            t.raw = new byte[1];
            t.raw[0] = memory[start];
            return t;
        }
    }

    public class BytecodeSingularToken : IComparable<BytecodeSingularToken>
    {
        public int TokenIndex { get; set; }
        public string OpCodeString { get; set; }
        public string CurrentStack { get; set; }
        public int StartPos { get; set; }

        public int OpCode;

        public override string ToString()
        {
            return $"0x{StartPos:X4}: {OpCodeString} {CurrentStack}";
        }

        public int CompareTo(BytecodeSingularToken that)
        {
            return this.TokenIndex.CompareTo(that.TokenIndex);
        }
    }

    public class Token
    {
        public const int INPACKAGEREFTYPE_NAME = 0;
        public const int INPACKAGEREFTYPE_ENTRY = 1;

        public List<(int position, int type, int value)> inPackageReferences = new();
        public byte[] raw;
        public string text { get; set; }
        public bool stop;
        public short op { get; set; }
        public int pos { get; set; }

        public int memPos { get; set; } = -1;
        public string posStr => pos.ToString("X" + paddingSize);
        public string memPosStr => memPos.ToString("X" + paddingSize);

        internal int paddingSize;

        public override string ToString()
        {
            if (memPos >= 0)
            {
                return $"0x{pos.ToString("X" + paddingSize)} (0x{memPos.ToString("X" + paddingSize)}): {text}";
            }
            return $"0x{pos.ToString("X" + paddingSize)} : {text}";
        }
    }

}
