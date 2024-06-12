namespace LegendaryExplorerCore.UnrealScript.Language.ByteCode
{
    public enum OpCodes : byte
    {
        LocalVariable = 0x00,
        InstanceVariable = 0x01,
        DefaultVariable = 0x02,
        //StateVariable = 0x03, // TODO: possibly implemented after ME3's UE version?
        Return = 0x04,
        Switch = 0x05,
        Jump = 0x06,
        JumpIfNot = 0x07,
        Stop = 0x08,
        Assert = 0x09,
        Case = 0x0A,
        Nothing = 0x0B,
        LabelTable = 0x0C,
        GotoLabel = 0x0D,
        EatReturnValue = 0x0E,
        Let = 0x0F,
        DynArrayElement = 0x10,
        New = 0x11,
        ClassContext = 0x12,
        Metacast = 0x13,
        LetBool = 0x14,
        EndParmValue = 0x15,
        EndFunctionParms = 0x16,
        Self = 0x17,
        Skip = 0x18,
        Context = 0x19,
        ArrayElement = 0x1A,
        VirtualFunction = 0x1B,
        FinalFunction = 0x1C,
        IntConst = 0x1D,
        FloatConst = 0x1E,
        StringConst = 0x1F,
        ObjectConst = 0x20,
        NameConst = 0x21,
        RotationConst = 0x22,
        VectorConst = 0x23,
        ByteConst = 0x24,
        IntZero = 0x25,
        IntOne = 0x26,
        True = 0x27,
        False = 0x28,
        NativeParm = 0x29,
        NoObject = 0x2A,
        //Unknown_Deprecated = 0x2B, // seems unused, exe's assert fails.
        IntConstByte = 0x2C,
        BoolVariable = 0x2D,
        DynamicCast = 0x2E,
        Iterator = 0x2F,
        IteratorPop = 0x30,
        IteratorNext = 0x31, //undefined in GNatives?
        StructCmpEq = 0x32,
        StructCmpNe = 0x33,
        //UnicodeStringConst = 0x34, // unused?
        StructMember = 0x35,
        DynArrayLength = 0x36,
        GlobalFunction = 0x37,
        PrimitiveCast = 0x38,
        DynArrayInsert = 0x39,
        ReturnNullValue = 0x3A,        // Was: ByteToInt and ReturnNothing, seems to now be used as a way to se retunr value to the type's null value, eg: 04 3A RetValRef
        EqualEqual_DelDel = 0x3B,      // 3B - 3E seem to be natives, UE3 standard is delegate comparison operations
        NotEqual_DelDel = 0x3C,        // seemingly does not extract the second halfbyte of the instruction, weird.
        EqualEqual_DelFunc = 0x3D,     // They are bound in GNatives though.
        NotEqual_DelFunc = 0x3E,
        EmptyDelegate = 0x3F,
        DynArrayRemove = 0x40,
        //DebugInfo = 0x41,
        DelegateFunction = 0x42,
        DelegateProperty = 0x43,
        LetDelegate = 0x44,
        Conditional = 0x45,
        DynArrayFind = 0x46,
        DynArrayFindStruct = 0x47,
        LocalOutVariable = 0x48,
        DefaultParmValue = 0x49,
        EmptyParmValue = 0x4A,
        InstanceDelegate = 0x4B,
        // 0x4C - 0x4E are unknown, probably invalid, throws assert failure by the looks of it, not defined in GNatives
        StringRefConst = 0x4F,
        //GoW_DefaultValue = 0x50, unused
        InterfaceContext = 0x51,
        InterfaceCast = 0x52,
        EndOfScript = 0x53,
        DynArrayAdd = 0x54,
        DynArrayAddItem = 0x55,
        DynArrayRemoveItem = 0x56,
        DynArrayInsertItem = 0x57,
        DynArrayIterator = 0x58,
        DynArraySort = 0x59,
        FilterEditorOnly = 0x5A,

        //0x5B - 0x62 are not used if the variable is a static array
        LocalFloatVariable = 0x5B,
        LocalIntVariable = 0x5C,
        LocalByteVariable = 0x5D,
        LocalObjectVariable = 0x5E,
        InstanceFloatVariable = 0x5F,
        InstanceIntVariable = 0x60,
        InstanceByteVariable = 0x61,
        InstanceObjectVariable = 0x62,

        OptIfLocal = 0x63,
        OptIfInstance = 0x64,
        NamedFunction = 0x65,
        // 66-6F are unknown, probably invalid, throws assert failure by the looks of it
        // none of them are defined in GNatives

        // 63 and 64 also share

        // 0x00 - 0x6F seem to be standard
        // 0x70 - 0x7F seem to be extended natives
        // 0x80 - 0xFF likely to be natives
    }
}
