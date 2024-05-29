using System;
using System.Runtime.CompilerServices;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Utilities
{
    public static class CastHelper
    {
        private const ECast AC = ECast.AutoConvert;
        private const ECast TAC = ECast.AutoConvert | ECast.Truncate;
        private static readonly ECast[][] Conversions =
        {
            	/*      None       Byte                  Int                   Bool                Float                  Object               Name               Delegate                    Interface          Struct             Vector                 Rotator                String                Map                 StringRef      */
			/*          --------   ------------------    ------------------    ----------------    -------------------    -----------------    ---------------    -------------------         ---------------    ----------------   -------------------    --------------------   -----------------     ----------------    -------------- */
/* None     */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* Byte     */ new []{ ECast.Max,  ECast.Max,            ECast.IntToByte|TAC,  ECast.BoolToByte,   ECast.FloatToByte|TAC, ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.StringToByte,   ECast.Max,          ECast.Max,          },
/* Int      */ new []{ ECast.Max,  ECast.ByteToInt|AC,   ECast.Max,            ECast.BoolToInt,    ECast.FloatToInt|TAC,  ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.StringToInt,    ECast.Max,         ECast.StringRefToInt,},
/* Bool     */ new []{ ECast.Max,  ECast.ByteToBool,     ECast.IntToBool,      ECast.Max,          ECast.FloatToBool,     ECast.ObjectToBool,  ECast.NameToBool,  ECast.Max,      ECast.InterfaceToBool,         ECast.Max,         ECast.VectorToBool,    ECast.RotatorToBool,   ECast.StringToBool,   ECast.Max,          ECast.Max,          },
/* Float    */ new []{ ECast.Max,  ECast.ByteToFloat|AC, ECast.IntToFloat|AC,  ECast.BoolToFloat,  ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.StringToFloat,  ECast.Max,          ECast.Max,          },
/* Object   */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max, ECast.InterfaceToObject|AC,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* Name     */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.StringToName,   ECast.Max,          ECast.Max,          },
/* Delegate */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* Interface*/ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,       AC|ECast.ObjectToInterface,ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* Struct   */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* Vector   */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.RotatorToVector, ECast.StringToVector, ECast.Max,          ECast.Max,          },
/* Rotator  */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.VectorToRotator, ECast.Max,             ECast.StringToRotator,ECast.Max,          ECast.Max,          },
/* String   */ new []{ ECast.Max,  ECast.ByteToString,   ECast.IntToString,    ECast.BoolToString, ECast.FloatToString, ECast.ObjectToString, ECast.NameToString, ECast.DelegateToString,ECast.InterfaceToString,ECast.Max,         ECast.VectorToString,  ECast.RotatorToString, ECast.Max,            ECast.Max,      ECast.StringRefToString,},
/* Map      */ new []{ ECast.Max,  ECast.Max,            ECast.Max,            ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
/* StringRef*/ new []{ ECast.Max,  ECast.Max,            ECast.IntToStringRef, ECast.Max,          ECast.Max,             ECast.Max,           ECast.Max,         ECast.Max,                  ECast.Max,         ECast.Max,         ECast.Max,             ECast.Max,             ECast.Max,            ECast.Max,          ECast.Max,          },
        };

        public static ECast GetConversion(VariableType dest, VariableType src)
        {
            var destType = dest?.PropertyType ?? EPropertyType.None;
            var srcType = src?.PropertyType ?? EPropertyType.None;

            if (dest is Class dCls && dCls.SameAsOrSubClassOf("Interface"))
            {
                destType = EPropertyType.Interface;
            }
            if (src is Class sCls && sCls.SameAsOrSubClassOf("Interface"))
            {
                srcType = EPropertyType.Interface;
            }

            if (src is null && (destType is EPropertyType.Object or EPropertyType.Delegate))
            {
                return ECast.AutoConvert;
            }

            if (src is null && destType is EPropertyType.String)
            {
                return ECast.ObjectToString;
            }

            return Conversions[(int)destType][(int)srcType];
        }

        public static ECast PureCastType(ECast castType) => castType & ~(ECast.AutoConvert | ECast.Truncate);

        public static int ConversionCost(FunctionParameter dest, VariableType src)
        {
            if (dest.VarType == src)
            {
                return 0; //exact match
            }

            if (src?.PropertyType == EPropertyType.Vector && dest.VarType?.PropertyType == EPropertyType.Vector 
             || src?.PropertyType == EPropertyType.Rotator && dest.VarType?.PropertyType == EPropertyType.Rotator)
            {
                return 0;
            }
            if (dest.VarType is Class c && (src is null || src is ClassType && !c.IsInterface))
            {
                return 0;
            }
            if (dest.VarType is DelegateType && src is null)
            {
                return 0;
            }
            if (dest.IsOut)
            {
                return int.MaxValue;
            }
            if (INTERFACE.CaseInsensitiveEquals(dest.VarType?.Name) && src is Class cls && cls.SameAsOrSubClassOf(INTERFACE))
            {
                return 1; //Interface subclass
            }
            if (!INTERFACE.CaseInsensitiveEquals(dest.VarType?.Name) && dest.VarType is Class && src is Class)
            {
                return 2;
            }
            ECast conversion = GetConversion(dest.VarType, src);
            //if it has 'coerce', any valid conversion is acceptable, otherwise only autoconversions are acceptable
            if (dest.Flags.Has(UnrealFlags.EPropertyFlags.CoerceParm) ? conversion != ECast.Max : conversion.Has(ECast.AutoConvert))
            {
                if (conversion.Has(ECast.Truncate))
                {
                    return 104; //lossy conversion
                }

                if (dest.VarType == SymbolTable.FloatType && (src == SymbolTable.IntType || src?.PropertyType == EPropertyType.Byte))
                {
                    return 103; //int to float conversion
                }

                return 101; //lossless conversion
            }

            return int.MaxValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool Has(this ECast enumValue, ECast flag)
        {
            return (enumValue & flag) == flag;
        }
    }

    public enum EPropertyType
    {
        None,
        Byte,
        Int,
        Bool,
        Float,
        Object,
        Name,
        Delegate,
        Interface,
        Struct,
        Vector,
        Rotator,
        String,
        Map,
        StringRef
    }

    [Flags]
    public enum ECast
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
        StringToName = 0x60,

        Max = 0xFF,
        AutoConvert = 0x100,
        Truncate = 0x200
    }
}
