namespace LegendaryExplorer.UserControls.ExportLoaderControls;

public enum NodeType : sbyte
{
    Unknown = -1,
    StructProperty = 0,
    IntProperty = 1,
    FloatProperty = 2,
    ObjectProperty = 3,
    NameProperty = 4,
    BoolProperty = 5,
    ByteProperty = 6,
    ArrayProperty = 7,
    StrProperty = 8,
    StringRefProperty = 9,
    DelegateProperty = 10,
    None,
    BioMask4Property,

    ArrayLeafObject,
    ArrayLeafName,
    ArrayLeafEnum,
    ArrayLeafStruct,
    ArrayLeafBool,
    ArrayLeafString,
    ArrayLeafFloat,
    ArrayLeafInt,
    ArrayLeafByte,

    StructLeafByte,
    StructLeafFloat,
    StructLeafDeg, //indicates this is a StructProperty leaf that is in degrees (actually unreal rotation units)
    StructLeafInt,
    StructLeafObject,
    StructLeafName,
    StructLeafBool,
    StructLeafStr,
    StructLeafArray,
    StructLeafEnum,
    StructLeafStruct,

    // For right clicking things.
    Guid,

    Root,
    ReferenceToOffset
}