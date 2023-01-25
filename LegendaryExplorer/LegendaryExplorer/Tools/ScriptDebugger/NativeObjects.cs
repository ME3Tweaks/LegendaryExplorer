using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.ObjectInfo;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public partial class DebuggerInterface
    {
        private byte[] ReadBytes(IntPtr address, int numBytes)
        {
            var bytes = new byte[numBytes];
            ReadProcessMemory(address, bytes.AsSpan());
            return bytes;
        }

        private readonly Dictionary<IntPtr, NClass> ClassCache = new();
        public NClass ReadClass(IntPtr address)
        {
            if (address == IntPtr.Zero)
            {
                return null;
            }
            if (ClassCache.TryGetValue(address, out NClass nClass))
            {
                return nClass;
            }
            nClass = new NClass(address, Game switch
            {
                MEGame.LE1 => 0x220,
                MEGame.LE2 => 0x1F8,
                _ => 0x1F8
            }, this);
            ClassCache[address] = nClass;
            return nClass;
        }

        //should be cleared on every break
        private readonly Dictionary<IntPtr, NObject> ObjectCache = new();
        public NObject ReadObject(IntPtr address)
        {
            if (address == IntPtr.Zero)
            {
                return null;
            }
            if (ObjectCache.TryGetValue(address, out NObject obj))
            {
                return obj;
            }
            var classPtr = ReadValue<IntPtr>(address + NObject.OFFSET_CLASS);
            NClass nClass = ReadClass(classPtr);
            switch (nClass.Name.Name)
            {
                case "Class":
                    return ReadClass(address);
                case "State":
                    obj = new NState(address, nClass, Game switch
                    {
                        MEGame.LE1 => 0x138,
                        MEGame.LE2 => 0x130,
                        _ => 0x100
                    }, this);
                    break;
                case "ScriptStruct":
                    obj = new NScriptStruct(address, nClass, Game switch
                    {
                        MEGame.LE1 => 0xEC,
                        MEGame.LE2 => 0xE4,
                        _ => 0xEC
                    }, this);
                    break;
                case "Function":
                    obj = new NFunction(address, nClass, Game switch
                    {
                        MEGame.LE1 => 0x100,
                        MEGame.LE2 => 0xF8,
                        _ => 0xF4
                    }, this);
                    break;
                case "Enum":
                    obj = new NEnum(address, nClass, this);
                    break;
                case "Const":
                    obj = new NConst(address, nClass, this);
                    break;
                case "StructProperty":
                    obj = new NStructProperty(address, nClass, this);
                    break;
                case "StringRefProperty":
                    obj = new NStringRefProperty(address, nClass, this);
                    break;
                case "StrProperty":
                    obj = new NStrProperty(address, nClass, this);
                    break;
                case "ObjectProperty":
                    obj = new NObjectProperty(address, nClass, NObjectProperty.OBJECTPROPERTY_SIZE, this);
                    break;
                case "ComponentProperty":
                    obj = new NComponentProperty(address, nClass, this);
                    break;
                case "ClassProperty":
                    obj = new NClassProperty(address, nClass, this);
                    break;
                case "NameProperty":
                    obj = new NNameProperty(address, nClass, this);
                    break;
                case "MapProperty":
                    obj = new NMapProperty(address, nClass, this);
                    break;
                case "InterfaceProperty":
                    obj = new NInterfaceProperty(address, nClass, this);
                    break;
                case "IntProperty":
                    obj = new NIntProperty(address, nClass, this);
                    break;
                case "FloatProperty":
                    obj = new NFloatProperty(address, nClass, this);
                    break;
                case "DelegateProperty":
                    obj = new NDelegateProperty(address, nClass, this);
                    break;
                case "ByteProperty":
                    obj = new NByteProperty(address, nClass, this);
                    break;
                case "BoolProperty":
                    obj = new NBoolProperty(address, nClass, this);
                    break;
                case "BioMask4Property":
                    obj = new NBioMask4Property(address, nClass, this);
                    break;
                case "ArrayProperty":
                    obj = new NArrayProperty(address, nClass, this);
                    break;
                case "Package":
                    obj = new NPackage(address, nClass, this);
                    break;
                default:
                    obj = new NObject(address, nClass, nClass.PropertySize, this);
                    break;
            }

            ObjectCache[address] = obj;
            return obj;
        }


        public class NObject
        {
            private const int OFFSET_LINKER = 0x2C; //ULinkerLoad*
            private const int OFFSET_OUTER = 0x40; //UObject*
            private const int OFFSET_NAME = 0x48; //FName
            public const int OFFSET_CLASS = 0x50; //UClass*
            
            protected readonly DebuggerInterface Debugger;
            private readonly byte[] buff;
            public readonly NClass Class;
            public IntPtr Address;

            public NObject(IntPtr address, NClass nClass, int size, DebuggerInterface debugger)
            {
                Debugger = debugger;
                Class = nClass;
                buff = Debugger.ReadBytes(address, size);
                Address = address;
            }

            public NLinker Linker => ReadObject<NLinker>(OFFSET_LINKER); //likely null
            public NObject Outer => ReadObject<NObject>(OFFSET_OUTER);

            private NameReference? _name;
            public NameReference Name => _name ??= Debugger.GetNameReference(ReadValue<FName>(OFFSET_NAME));

            public string GetFullPath()
            {
                var outer = Outer;
                if (outer is null)
                {
                    return Name.Instanced;
                }
                return $"{outer.GetFullPath()}.{Name.Instanced}";
            }


            protected T ReadValue<T>(int offset) where T : unmanaged => MemoryMarshal.Read<T>(buff.AsSpan(offset));
            protected T ReadObject<T>(int offset) where T : NObject => (T)Debugger.ReadObject(ReadValue<IntPtr>(offset));
            protected NClass ReadClass(int offset) => Debugger.ReadClass(ReadValue<IntPtr>(offset));

            protected string ReadFString(int offset)
            {
                var fString = ReadValue<TArray>(offset);
                return Debugger.ReadUnicodeString(fString.Data, fString.Count - 1);
            }
        }

        public class NPackage : NObject
        {
            private const int OFFSET_FORCEDEXPORTBASEPACKAGENAME = 0xA4; //for LE1. TODO: confirm for LE2 and LE3
            private const int UPACKAGE_SIZE = 0xB0;
            public NPackage(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, UPACKAGE_SIZE, debugger)
            {
            }

            public NameReference ForcedExportBasePackageName => Debugger.GetNameReference(ReadValue<FName>(OFFSET_FORCEDEXPORTBASEPACKAGENAME));
        }

        public class NLinker : NObject
        {
            private const int OFFSET_LINKERROOT = 0x60; //UPackage*
            private const int OFFSET_FILENAME = 0x194; //FString
            private const int ULINKER_SIZE = 0x1AC; //actually 0x1B0 for LE3
            public NLinker(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, ULINKER_SIZE, debugger)
            {
            }

            public NPackage LinkerRoot => ReadObject<NPackage>(OFFSET_LINKERROOT);
            public string Filename => ReadFString(OFFSET_FILENAME);
        }

        public abstract class NField : NObject
        {
            private const int OFFSET_SUPER = 0x60; //UField*
            private const int OFFSET_NEXT = 0x68; //UField*

            public NField(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }

            public NField Super => ReadObject<NField>(OFFSET_SUPER);
            public NField Next => ReadObject<NField>(OFFSET_NEXT);
        }

        public abstract class NStruct : NField
        {
            private const int OFFSET_CHILDREN = 0x70; //UField*
            private const int OFFSET_PROPERTYSIZE = 0x78; //int
            private const int OFFSET_SCRIPT = 0x7C; //TArray<byte>
            private const int OFFSET_MINALIGNMENT = 0x8C; //int

            protected NStruct(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }

            public NField FirstChild => ReadObject<NField>(OFFSET_CHILDREN);
            public int PropertySize => ReadValue<int>(OFFSET_PROPERTYSIZE);
            public TArray Script => ReadValue<TArray>(OFFSET_SCRIPT);
            public int MinAlignment => ReadValue<int>(OFFSET_MINALIGNMENT);

            public List<PropertyValue> GetProperties(IntPtr address)
            {
                var properties = new List<PropertyValue>();

                for (NStruct curStruct = this; curStruct is not null && curStruct.Name.Name != "Class"; curStruct = curStruct.Super as NStruct)
                {
                    for (NField child = curStruct.FirstChild; child is not null; child = child.Next)
                    {
                        if (child is not NProperty prop || prop.PropertyFlags.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                        {
                            continue;
                        }
                        IntPtr propAddr = address + prop.Offset;

                        if (propAddr == IntPtr.Zero)
                        {
                            continue;
                        }
                        prop.ReadProperty(propAddr, properties);
                    }
                }
                if (this is NClass)
                {
                    properties.Sort((val1, val2) => string.Compare(val1.PropName, val2.PropName, StringComparison.OrdinalIgnoreCase));
                }
                return properties;
            }

            public string CPlusPlusName(MEGame game)
            {
                string name = Name;
                return $"{(this is NScriptStruct ? 'F' : GlobalUnrealObjectInfo.IsA(name, "Actor", game) ? 'A' : 'U')}{name}";
            }
        }

        public class NState : NStruct
        {
            public NState(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }
        }

        public class NClass : NState
        {
            public NClass(IntPtr address, int size, DebuggerInterface debugger) : base(address, null, size, debugger)
            {
            }

            public UnrealFlags.EClassFlags ClassFlags => ReadValue<UnrealFlags.EClassFlags>(Debugger.Game switch
            {
                MEGame.LE1 => 0x138,
                MEGame.LE2 => 0x130,
                _ => 0x100
            });
        }

        public class NScriptStruct : NStruct
        {
            public NScriptStruct(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }
            public UnrealFlags.ScriptStructFlags StructFlags => ReadValue<UnrealFlags.ScriptStructFlags>(Debugger.Game switch
            {
                MEGame.LE1 => 0xE8,
                MEGame.LE2 => 0xE0,
                _ => 0xE8
            });
        }

        public class NFunction : NStruct
        {
            public NFunction(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }

            public ushort ParmsSize => ReadValue<ushort>(Debugger.Game switch
            {
                MEGame.LE1 => 0xEA,
                MEGame.LE2 => 0xE2,
                _ => 0xE6
            });
        }

        public class NEnum : NField
        {
            private const int OFFSET_NAMES = 0x70; //TArray<FName>
            private const int ENUM_SIZE = 0x80;
            public NEnum(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, ENUM_SIZE, debugger)
            {
                namesTArray = ReadValue<TArray>(OFFSET_NAMES);
                NameCount = namesTArray.Count - 1;
            }

            private readonly TArray namesTArray;
            public readonly int NameCount;
            private List<FName> _names;

            public List<FName> Names
            {
                get
                {
                    if (_names is null)
                    {
                        if (namesTArray.Count > 1)
                        {
                            var enumValues = new FName[namesTArray.Count - 1];
                            Debugger.ReadProcessMemory(namesTArray.Data, MemoryMarshal.AsBytes(enumValues.AsSpan()));
                            _names = enumValues.ToList();
                        }
                        else
                        {
                            _names = new List<FName>();
                        }
                    }
                    return _names;
                }
            }
        }

        public class NConst : NField
        {
            private const int OFFSET_VALUE = 0x70; //FString
            private const int CONST_SIZE = 0x80;
            public NConst(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, CONST_SIZE, debugger)
            {
            }

            public string Value => ReadFString(OFFSET_VALUE);
        }

        public abstract class NProperty : NField
        {
            private const int OFFSET_ARRAYDIM = 0x70; //int
            private const int OFFSET_ELEMENTSIZE = 0x74; //int
            private const int OFFSET_PROPERTYFLAGS = 0x78; //ulong
            private const int OFFSET_ARRAYSIZEENUM = 0x8C; //UEnum*
            private const int OFFSET_OFFSET = 0x94; //int
            protected const int BASE_PROPERTY_SIZE = 0xD0;

            protected NProperty(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }

            public int ArrayDim => ReadValue<int>(OFFSET_ARRAYDIM);
            public int ElementSize => ReadValue<int>(OFFSET_ELEMENTSIZE);
            public UnrealFlags.EPropertyFlags PropertyFlags => (UnrealFlags.EPropertyFlags)ReadValue<ulong>(OFFSET_PROPERTYFLAGS);
            public NEnum ArraySizeEnum => ReadObject<NEnum>(OFFSET_ARRAYSIZEENUM);
            public int Offset => ReadValue<int>(OFFSET_OFFSET);

            public virtual void ReadProperty(IntPtr address, List<PropertyValue> properties)
            {
                int arrayDim = GetRealArrayDim();
                if (arrayDim > 1)
                {
                    for (int i = 0; i < arrayDim; i++)
                    {
                        properties.Add(ReadSingleValue(address, $"{Name.Instanced}[{i}]"));
                        address += ElementSize;
                    }
                }
                else
                {
                    properties.Add(ReadSingleValue(address, Name.Instanced));
                }
            }

            public abstract PropertyValue ReadSingleValue(IntPtr address, string name);

            public int GetRealArrayDim()
            {
                if (ArraySizeEnum is NEnum nEnum)
                {
                    return nEnum.NameCount;
                }
                return ArrayDim;
            }
        }

        public class NIntProperty : NProperty
        {
            public NIntProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BASE_PROPERTY_SIZE, debugger)
            {
            }

            public override IntPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new IntPropertyValue(Debugger, address, name, Debugger.ReadValue<int>(address));
            }
        }

        public class NFloatProperty : NProperty
        {
            public NFloatProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BASE_PROPERTY_SIZE, debugger)
            {
            }

            public override FloatPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new FloatPropertyValue(Debugger, address, name, Debugger.ReadValue<float>(address));
            }
        }

        public class NStructProperty : NProperty
        {
            private const int OFFSET_STRUCT = 0xD0; //UStruct*
            private const int STRUCTPROPERTY_SIZE = 0xD8;

            public NStructProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, STRUCTPROPERTY_SIZE, debugger)
            {
            }

            public NStruct Struct => ReadObject<NStruct>(OFFSET_STRUCT);
            public override StructPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new StructPropertyValue(Debugger, address, name, Name.Instanced, Struct);
            }
        }

        public class NStringRefProperty : NProperty
        {
            public NStringRefProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BASE_PROPERTY_SIZE, debugger)
            {
            }

            public override StringRefPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new StringRefPropertyValue(Debugger, address, name, Debugger.ReadValue<int>(address));
            }
        }

        public class NStrProperty : NProperty
        {
            public NStrProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BASE_PROPERTY_SIZE, debugger)
            {
            }

            public override StrPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                var fString = Debugger.ReadValue<TArray>(address);
                return new StrPropertyValue(Debugger, address, name, Debugger.ReadUnicodeString(fString.Data, fString.Count - 1), fString.Max - 1);
            }
        }

        public class NObjectProperty : NProperty
        {
            private const int OFFSET_PROPERTYCLASS = 0xD0; //UClass*
            public const int OBJECTPROPERTY_SIZE = 0xD8;
            public NObjectProperty(IntPtr address, NClass nClass, int size, DebuggerInterface debugger) : base(address, nClass, size, debugger)
            {
            }

            public NClass PropertyClass => ReadClass(OFFSET_PROPERTYCLASS);
            public override ObjectPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new ObjectPropertyValue(Debugger, address, name, Debugger.ReadObject(Debugger.ReadValue<IntPtr>(address)));
            }
        }

        public class NComponentProperty : NObjectProperty
        {
            public NComponentProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, OBJECTPROPERTY_SIZE, debugger)
            {
            }
        }

        public class NClassProperty : NObjectProperty
        {
            private const int OFFSET_METACLASS = 0xD8; //UClass*
            private const int CLASSPROPERTY_SIZE = 0xE0;
            public NClassProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, CLASSPROPERTY_SIZE, debugger)
            {
            }

            public NClass MetaClass => ReadClass(OFFSET_METACLASS);
            public override ClassPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new ClassPropertyValue(Debugger, address, name, Debugger.ReadClass(Debugger.ReadValue<IntPtr>(address)));
            }
        }

        public class NNameProperty : NProperty
        {
            public NNameProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BASE_PROPERTY_SIZE, debugger)
            {
            }

            public override NamePropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new NamePropertyValue(Debugger, address, name, Debugger.GetNameReference(Debugger.ReadValue<FName>(address)));
            }
        }

        public class NMapProperty : NProperty
        {
            private const int OFFSET_KEY = 0xD0; //UProperty*
            private const int OFFSET_VALUE = 0xD8; //UProperty*
            private const int MAPPROPERTY_SIZE = 0xE0;
            public NMapProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, MAPPROPERTY_SIZE, debugger)
            {
            }

            public NProperty Key => ReadObject<NProperty>(OFFSET_KEY);
            public NProperty Value => ReadObject<NProperty>(OFFSET_VALUE);

            public override void ReadProperty(IntPtr address, List<PropertyValue> properties)
            {
                //do nothing, we don't read Map Properties
            }

            public override PropertyValue ReadSingleValue(IntPtr address, string name)
            {
                throw new NotImplementedException();
            }
        }

        public class NInterfaceProperty : NProperty
        {
            private const int OFFSET_INTERFACECLASS = 0xD0; //UClass*
            private const int INTERFACEPROPERTY_SIZE = 0xD8;
            public NInterfaceProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, INTERFACEPROPERTY_SIZE, debugger)
            {
            }

            public NClass InterfaceClass => ReadClass(OFFSET_INTERFACECLASS);
            public override InterfacePropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new InterfacePropertyValue(Debugger, address, name, Debugger.ReadValue<FScriptInterface>(address));
            }
        }

        public class NDelegateProperty : NProperty
        {
            private const int OFFSET_FUNCTION = 0xD0; //UFunction*
            private const int OFFSET_SOURCEDELEGATE = 0xD8; //UFunction*
            private const int DELEGATEPROPERTY_SIZE = 0xE0;
            public NDelegateProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, DELEGATEPROPERTY_SIZE, debugger)
            {
            }

            public NFunction Function => ReadObject<NFunction>(OFFSET_FUNCTION);
            public NFunction SourceDelegate => ReadObject<NFunction>(OFFSET_SOURCEDELEGATE);
            public override PropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new DelegatePropertyValue(Debugger, address, name, Debugger.ReadValue<FScriptDelegate>(address));
            }
        }

        public class NByteProperty : NProperty
        {
            private const int OFFSET_ENUM = 0xD0; //UEnum*
            private const int BYTEPROPERTY_SIZE = 0xD8;
            public NByteProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BYTEPROPERTY_SIZE, debugger)
            {
            }

            public NEnum Enum => ReadObject<NEnum>(OFFSET_ENUM);

            public override PropertyValue ReadSingleValue(IntPtr address, string name)
            {
                byte byteVal = Debugger.ReadValue<byte>(address);
                if (Enum is NEnum nEnum)
                {
                    var enumValues = nEnum.Names;
                    if (byteVal < enumValues.Count)
                    {
                        return new EnumPropertyValue(Debugger, address, name, Debugger.GetNameReference(enumValues[byteVal]), enumValues.ToList());
                    }

                }
                return new BytePropertyValue(Debugger, address, name, byteVal);
            }
        }

        public class NBoolProperty : NProperty
        {
            private const int OFFSET_BITMASK = 0xD0; //uint
            private const int BOOLPROPERTY_SIZE = 0xD8;
            public NBoolProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BOOLPROPERTY_SIZE, debugger)
            {
            }

            public uint BitMask => ReadValue<uint>(OFFSET_BITMASK);

            public override BoolPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                uint bitMask = BitMask;
                return new BoolPropertyValue(Debugger, address, name, (Debugger.ReadValue<uint>(address) & bitMask) > 0, bitMask);
            }
        }

        public class NBioMask4Property : NProperty
        {
            private const int BIOMASK4PROPERTY_SIZE = 0xD8;
            public NBioMask4Property(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, BIOMASK4PROPERTY_SIZE, debugger)
            {
            }

            public override BytePropertyValue ReadSingleValue(IntPtr address, string name)
            {
                return new BytePropertyValue(Debugger, address, name, Debugger.ReadValue<byte>(address));
            }
        }

        public class NArrayProperty : NProperty
        {
            private const int OFFSET_INNER = 0xD0; //UProperty*
            private const int ARRAYPROPERTY_SIZE = 0xD8;
            public NArrayProperty(IntPtr address, NClass nClass, DebuggerInterface debugger) : base(address, nClass, ARRAYPROPERTY_SIZE, debugger)
            {
            }

            public NProperty InnerProperty => ReadObject<NProperty>(OFFSET_INNER);

            public override ArrayPropertyValue ReadSingleValue(IntPtr address, string name)
            {
                var tArray = Debugger.ReadValue<TArray>(address);

                var elements = new List<PropertyValue>(tArray.Count);

                var innerProperty = InnerProperty;

                if (innerProperty is not NMapProperty)
                {
                    IntPtr arrayAddress = tArray.Data;
                    for (int i = 0; i < tArray.Count; i++)
                    {
                        elements.Add(innerProperty.ReadSingleValue(arrayAddress, $"[{i}]"));
                        arrayAddress += innerProperty.ElementSize;
                    }
                }

                return new ArrayPropertyValue(Debugger, address, name, elements);
            }
        }
    }
}