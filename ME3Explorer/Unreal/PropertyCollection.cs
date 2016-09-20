using System;
using System.Collections.Generic;
using System.IO;
using Gibbed.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using System.ComponentModel;
using System.Collections.ObjectModel;
using UsefulThings.WPF;

namespace ME3Explorer.Unreal
{
    public class PropertyCollection : ObservableCollection<UProperty>
    {
        static Dictionary<string, PropertyCollection> defaultStructValues = new Dictionary<string, PropertyCollection>();

        public int endOffset;

        public static PropertyCollection ReadProps(IMEPackage pcc, MemoryStream stream, string typeName)
        {
            PropertyCollection props = new PropertyCollection();
            long startPosition = stream.Position;
            while (stream.Position + 8 <= stream.Length)
            {
                int nameIdx = stream.ReadValueS32();
                if (!pcc.isName(nameIdx))
                {
                    stream.Seek(-4, SeekOrigin.Current);
                    break;
                }
                string name = pcc.getNameEntry(nameIdx);
                if (name == "None")
                {
                    props.Add(new NoneProperty { PropType = PropertyType.None });
                    stream.Seek(4, SeekOrigin.Current);
                    break;
                }
                NameReference nameRef = new NameReference { index = nameIdx, Name = name, count = stream.ReadValueS32() };
                int typeIdx = stream.ReadValueS32();
                stream.Seek(4, SeekOrigin.Current);
                int size = stream.ReadValueS32();
                if (!pcc.isName(typeIdx) || size < 0 || size > stream.Length - stream.Position)
                {
                    stream.Seek(-16, SeekOrigin.Current);
                    break;
                }
                stream.Seek(4, SeekOrigin.Current);
                PropertyType type;
                if (!Enum.TryParse(pcc.getNameEntry(typeIdx), out type))
                {
                    type = PropertyType.Unknown;
                }
                switch (type)
                {
                    case PropertyType.StructProperty:
                        string structType = pcc.getNameEntry(stream.ReadValueS32());
                        stream.Seek(4, SeekOrigin.Current);
                        if (ME3UnrealObjectInfo.isImmutable(structType))
                        {
                            PropertyCollection structProps = ReadSpecialStruct(pcc, stream, structType, size);
                            props.Add(new StructProperty(structType, structProps, nameRef, true));
                        }
                        else
                        {
                            PropertyCollection structProps = ReadProps(pcc, stream, structType);
                            props.Add(new StructProperty(structType, structProps, nameRef));
                        }
                        break;
                    case PropertyType.IntProperty:
                        props.Add(new IntProperty(stream, nameRef));
                        break;
                    case PropertyType.FloatProperty:
                        props.Add(new FloatProperty(stream, nameRef));
                        break;
                    case PropertyType.ObjectProperty:
                        props.Add(new ObjectProperty(stream, nameRef));
                        break;
                    case PropertyType.NameProperty:
                        props.Add(new NameProperty(stream, pcc, nameRef));
                        break;
                    case PropertyType.BoolProperty:
                        props.Add(new BoolProperty(stream, pcc.Game, nameRef));
                        break;
                    case PropertyType.BioMask4Property:
                        props.Add(new BioMask4Property(stream, nameRef));
                        break;
                    case PropertyType.ByteProperty:
                        {
                            if (size != 1)
                            {
                                NameReference enumType = new NameReference();
                                if (pcc.Game == MEGame.ME3)
                                {
                                    enumType.index = stream.ReadValueS32();
                                    enumType.count = stream.ReadValueS32();
                                    enumType.Name = pcc.getNameEntry(enumType.index);
                                }
                                else
                                {
                                    enumType.index = -1;
                                    enumType.Name = UnrealObjectInfo.GetEnumType(pcc.Game, name, typeName);
                                }
                                props.Add(new EnumProperty(stream, pcc, enumType, nameRef));
                            }
                            else
                            {
                                if (pcc.Game == MEGame.ME3)
                                {
                                    stream.Seek(8, SeekOrigin.Current);
                                }
                                props.Add(new ByteProperty(stream, nameRef));
                            }
                        }
                        break;
                    case PropertyType.ArrayProperty:
                        {
                            props.Add(new ArrayProperty(stream, pcc, typeName, nameRef));
                        }
                        break;
                    case PropertyType.StrProperty:
                        {
                            props.Add(new StrProperty(stream, nameRef));
                        }
                        break;
                    case PropertyType.StringRefProperty:
                        props.Add(new StringRefProperty(stream, nameRef));
                        break;
                    case PropertyType.DelegateProperty:
                    case PropertyType.Unknown:
                        {
                            props.Add(new UnknownProperty(stream, size, pcc.getNameEntry(typeIdx), nameRef));
                        }
                        break;
                    case PropertyType.None:
                    default:
                        break;
                }
            }
            if (props.Count > 0 && props[props.Count - 1].PropType != PropertyType.None)
            {
                stream.Seek(startPosition, SeekOrigin.Begin);
                return new PropertyCollection { endOffset = (int)stream.Position };
            }
            props.endOffset = (int)stream.Position;
            return props;
        }

        public static PropertyCollection ReadSpecialStruct(IMEPackage pcc, MemoryStream stream, string structType, int size)
        {
            PropertyCollection props = new PropertyCollection();
            if (pcc.Game == MEGame.ME3)
            {
                if (ME3UnrealObjectInfo.Structs.ContainsKey(structType))
                {
                    PropertyCollection defaultProps;
                    //memoize
                    if (defaultStructValues.ContainsKey(structType))
                    {
                        defaultProps = defaultStructValues[structType];
                    }
                    else
                    {
                        byte[] defaultValue = ME3UnrealObjectInfo.getDefaultClassValue(pcc as ME3Package, structType, true);
                        if (defaultValue == null)
                        {
                            props.Add(new UnknownProperty(stream, size));
                            return props;
                        }
                        defaultProps = ReadProps(pcc, new MemoryStream(defaultValue), structType);
                        defaultStructValues.Add(structType, defaultProps);
                    }
                    for (int i = 0; i < defaultProps.Count; i++)
                    {
                        props.Add(ReadSpecialStructProp(pcc, stream, defaultProps[i], structType));
                    }
                    return props;
                }
            }
            //TODO: implement getDefaultClassValue() for ME1 and ME2 so this isn't needed
            if (structType == "Rotator")
            {
                string[] labels = { "Pitch", "Yaw", "Roll" };
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new IntProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "Vector2d" || structType == "RwVector2")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new FloatProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "Vector" || structType == "RwVector3")
            {
                string[] labels = { "X", "Y", "Z" };
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new FloatProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "Color")
            {
                string[] labels = { "B", "G", "R", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new ByteProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "LinearColor")
            {
                string[] labels = { "R", "G", "B", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, (NameReference)labels[i]));
                }
            }
            //uses EndsWith to support RwQuat, RwVector4, and RwPlane
            else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
            {
                string[] labels = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "TwoVectors")
            {
                string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                for (int i = 0; i < 6; i++)
                {
                    props.Add(new FloatProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "Matrix" || structType == "RwMatrix44")
            {
                string[] labels = { "X Plane", "Y Plane", "Z Plane", "W Plane" };
                string[] labels2 = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 3; i++)
                {
                    PropertyCollection structProps = new PropertyCollection();
                    for (int j = 0; j < 4; j++)
                    {
                        structProps.Add(new FloatProperty(stream, (NameReference)labels2[j]));
                    }
                    props.Add(new StructProperty("Plane", structProps, (NameReference)labels[i], true));
                }
            }
            else if (structType == "Guid")
            {
                string[] labels = { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new IntProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "IntPoint")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new IntProperty(stream, (NameReference)labels[i]));
                }
            }
            else if (structType == "Box" || structType == "BioRwBox")
            {
                string[] labels = { "Min", "Max" };
                string[] labels2 = { "X", "Y", "Z" };
                for (int i = 0; i < 2; i++)
                {
                    PropertyCollection structProps = new PropertyCollection();
                    for (int j = 0; j < 3; j++)
                    {
                        structProps.Add(new FloatProperty(stream, (NameReference)labels2[j]));
                    }
                    props.Add(new StructProperty("Vector", structProps, (NameReference)labels[i], true));
                }
                props.Add(new ByteProperty(stream, (NameReference)"IsValid"));
            }
            else
            {
                props.Add(new UnknownProperty(stream, size));
            }
            return props;
        }

        static UProperty ReadSpecialStructProp(IMEPackage pcc, MemoryStream stream, UProperty template, string structType)
        {
            if (stream.Position + 1 >= stream.Length)
            {
                throw new EndOfStreamException("tried to read past bounds of Export Data");
            }
            switch (template.PropType)
            {
                case PropertyType.FloatProperty:
                    return new FloatProperty(stream, template.Name);
                case PropertyType.IntProperty:
                    return new IntProperty(stream, template.Name);
                case PropertyType.ObjectProperty:
                    return new ObjectProperty(stream, template.Name);
                case PropertyType.StringRefProperty:
                    return new StringRefProperty(stream, template.Name);
                case PropertyType.NameProperty:
                    return new NameProperty(stream, pcc, template.Name);
                case PropertyType.BoolProperty:
                    return new BoolProperty(stream, pcc.Game, template.Name);
                case PropertyType.ByteProperty:
                    if (template is EnumProperty)
                    {
                        string enumType = UnrealObjectInfo.GetEnumType(pcc.Game, template.Name, structType);
                        NameReference enumVal = new NameReference();
                        enumVal.index = stream.ReadValueS32();
                        enumVal.count = stream.ReadValueS32();
                        return new EnumProperty(stream, pcc, (NameReference)enumType, template.Name);
                    }
                    return new ByteProperty(stream, template.Name);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(stream, template.Name);
                case PropertyType.StrProperty:
                    return new StrProperty(stream, template.Name);
                case PropertyType.ArrayProperty:
                    return new ArrayProperty(stream, pcc, structType, template.Name, true);
                case PropertyType.StructProperty:
                    PropertyCollection structProps = ReadSpecialStruct(pcc, stream, UnrealObjectInfo.GetPropertyInfo(pcc.Game, template.Name, structType).reference, 0);
                    return new StructProperty(structType, structProps, template.Name, true);
                case PropertyType.DelegateProperty:
                    throw new NotImplementedException("cannot read Delegate property of Immutable struct");
                case PropertyType.None:
                    return new NoneProperty(template.Name);
                case PropertyType.Unknown:
                    throw new NotImplementedException("cannot read Unknown property of Immutable struct");
            }
            throw new NotImplementedException("cannot read Unknown property of Immutable struct");
        }
    }

    public abstract class UProperty : ViewModelBase
    {
        public PropertyType PropType;
        private NameReference _name;
        public NameReference Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        protected UProperty(NameReference? name)
        {
            _name = name ?? new NameReference { index = -1 };
        }
        
        public abstract void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false);
    }

    public class NoneProperty : UProperty
    {
        public NoneProperty(NameReference? name = null) : base(name)
        {
            PropType = PropertyType.None;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNoneProperty(pcc);
            }
        }
    }

    public class StructProperty : UProperty
    {
        public readonly bool IsImmutable;
        public string StructType { get; private set; }
        public PropertyCollection Properties { get; private set; }

        public StructProperty(string structType, PropertyCollection props, NameReference? name = null, bool isImmutable = false) : base(name)
        {
            StructType = structType;
            Properties = props;
            IsImmutable = isImmutable;
            PropType = PropertyType.StructProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false )
        {
            if (valueOnly)
            {
                foreach (var prop in Properties)
                {
                    prop.WriteTo(stream, pcc, IsImmutable);
                }
            }
            else
            {
                stream.WriteStructProperty(pcc, Name, StructType, () =>
                {
                    MemoryStream m = new MemoryStream();
                    foreach (var prop in Properties)
                    {
                        prop.WriteTo(m, pcc, IsImmutable);
                    }
                    return m;
                });
            }
        }
    }

    public class IntProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public IntProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueS32();
            PropType = PropertyType.IntProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteIntProperty(pcc, Name, Value); 
            }
            else
            {
                stream.WriteValueS32(Value);
            }
        }
    }

    public class FloatProperty : UProperty
    {
        float _value;
        public float Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public FloatProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueF32();
            PropType = PropertyType.FloatProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteFloatProperty(pcc, Name, Value); 
            }
            else
            {
                stream.WriteValueF32(Value);
            }
        }
    }

    public class ObjectProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public ObjectProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueS32();
           PropType = PropertyType.ObjectProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteObjectProperty(pcc, Name, Value); 
            }
            else
            {
                stream.WriteValueS32(Value);
            }
        }
    }

    public class NameProperty : UProperty
    {
        NameReference _value;
        public NameReference Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public NameProperty(MemoryStream stream, IMEPackage pcc, NameReference? name = null) : base(name)
        {
            NameReference nameRef = new NameReference();
            nameRef.index = stream.ReadValueS32();
            nameRef.count = stream.ReadValueS32();
            nameRef.Name = pcc.getNameEntry(nameRef.index);
            Value = nameRef;
            PropType = PropertyType.NameProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteNameProperty(pcc, Name, Value); 
            }
            else
            {
                stream.WriteValueS32(pcc.FindNameOrAdd(Value.Name));
                stream.WriteValueS32(Value.count);
            }
        }
    }

    public class BoolProperty : UProperty
    {
        bool _value;
        public bool Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public BoolProperty(MemoryStream stream, MEGame game, NameReference? name = null) : base(name)
        {
            Value = game == MEGame.ME3 ? stream.ReadValueB8() : stream.ReadValueB32();
            PropType = PropertyType.BoolProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteBoolProperty(pcc, Name, Value);
            }
            else
            {
                if (pcc.Game == MEGame.ME3)
                {
                    stream.WriteValueB8(Value);
                }
                else
                {
                    stream.WriteValueB32(Value);
                }
            }
        }
    }

    public class ByteProperty : UProperty
    {
        byte _value;
        public byte Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public ByteProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueU8();
            PropType = PropertyType.ByteProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteByteProperty(pcc, Name, Value); 
            }
            else
            {
                stream.WriteValueU8(Value);
            }
        }
    }

    public class BioMask4Property : UProperty
    {
        byte _value;
        public byte Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public BioMask4Property(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueU8();
            PropType = PropertyType.BioMask4Property;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WritePropHeader(pcc, Name, PropType, 1);
            }
            stream.WriteValueU8(Value);
        }
    }

    public class EnumProperty : UProperty
    {
        public NameReference EnumType { get; private set; }
        NameReference _value;
        public NameReference Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public EnumProperty(MemoryStream stream, IMEPackage pcc, NameReference enumType, NameReference? name = null) : base(name)
        {
            EnumType = enumType;
            NameReference enumVal = new NameReference();
            enumVal.index = stream.ReadValueS32();
            enumVal.count = stream.ReadValueS32();
            enumVal.Name = pcc.getNameEntry(enumVal.index);
            Value = enumVal;
            PropType = PropertyType.ByteProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteEnumProperty(pcc, Name, EnumType, Value);
            }
            else
            {
                stream.WriteValueS32(pcc.FindNameOrAdd(Value.Name));
                stream.WriteValueS32(Value.count);
            }
        }
    }

    public class ArrayProperty : UProperty
    {
        public List<UProperty> Values { get; private set; }
        public readonly ArrayType arrayType;

        public ArrayProperty(MemoryStream stream, IMEPackage pcc, string enclosingType, NameReference name, bool IsInImmutable = false) : base(name)
        {
            PropType = PropertyType.ArrayProperty;
            arrayType = UnrealObjectInfo.GetArrayType(pcc.Game, name, enclosingType);
            int count = stream.ReadValueS32();
            List<UProperty> props = new List<UProperty>();
            switch (arrayType)
            {
                case ArrayType.Object:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new ObjectProperty(stream));
                    }
                    break;
                case ArrayType.Name:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new NameProperty(stream, pcc));
                    }
                    break;
                case ArrayType.Enum:
                    NameReference enumType = new NameReference { Name = UnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType), index = -1 };
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new EnumProperty(stream, pcc, enumType));
                    }
                    break;
                case ArrayType.Struct:
                    string arrayStructType = UnrealObjectInfo.GetPropertyInfo(pcc.Game, name, enclosingType)?.reference;
                    if (IsInImmutable || ME3UnrealObjectInfo.isImmutable(arrayStructType))
                    {
                        int arraySize = 0;
                        if (!IsInImmutable)
                        {
                            stream.Seek(-16, SeekOrigin.Current);
                            arraySize = stream.ReadValueS32();
                            stream.Seek(12, SeekOrigin.Current);
                        }
                        for (int i = 0; i < count; i++)
                        {
                            PropertyCollection structProps = PropertyCollection.ReadSpecialStruct(pcc, stream, arrayStructType, arraySize / count);
                            props.Add(new StructProperty(arrayStructType, structProps, isImmutable: true));
                        }
                    }
                    else
                    {
                        for (int i = 0; i < count; i++)
                        {
                            PropertyCollection structProps = PropertyCollection.ReadProps(pcc, stream, arrayStructType);
                            props.Add(new StructProperty(arrayStructType, structProps));
                        }
                    }
                    break;
                case ArrayType.Bool:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new BoolProperty(stream, pcc.Game));
                    }
                    break;
                case ArrayType.String:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new StrProperty(stream));
                    }
                    break;
                case ArrayType.Float:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new FloatProperty(stream));
                    }
                    break;
                case ArrayType.Int:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new IntProperty(stream));
                    }
                    break;
                case ArrayType.Byte:
                    for (int i = 0; i < count; i++)
                    {
                        props.Add(new ByteProperty(stream));
                    }
                    break;
            }
            Values = props;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteArrayProperty(pcc, Name, Values.Count, () =>
                {
                    MemoryStream m = new MemoryStream();
                    foreach (var prop in Values)
                    {
                        prop.WriteTo(m, pcc, true);
                    }
                    return m;
                });
            }
            else
            {
                foreach (var prop in Values)
                {
                    prop.WriteTo(stream, pcc, true);
                }
            }
        }
    }

    public class StrProperty : UProperty
    {
        string _value;
        public string Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public StrProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            int count = stream.ReadValueS32();
            if (count < 0)
            {
                Value = stream.ReadString(-count * 2, true, Encoding.Unicode);
            }
            else if (count > 0)
            {
                Value = stream.ReadString(count, true, Encoding.ASCII);
            }
            else
            {
                Value = string.Empty;
            }
            PropType = PropertyType.StrProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteStringProperty(pcc, Name, Value);
            }
            else
            {
                int strLen = (Value.Length + 1);
                if (pcc.Game == MEGame.ME3)
                {
                    strLen *= 2;
                    stream.WriteValueS32(strLen);
                    stream.WriteStringUnicode(Value);
                }
                else
                {
                    stream.WriteValueS32(strLen);
                    stream.WriteStringASCII(Value);
                }
            }
        }
    }

    public class StringRefProperty : UProperty
    {
        int _value;
        public int Value
        {
            get { return _value; }
            set { SetProperty(ref _value, value); }
        }

        public StringRefProperty(MemoryStream stream, NameReference? name = null) : base(name)
        {
            Value = stream.ReadValueS32();
            PropType = PropertyType.StringRefProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteStringRefProperty(pcc, Name, Value);
            }
            else
            {
                stream.WriteValueS32(Value);
            }
        }
    }

    public class UnknownProperty : UProperty
    {
        public byte[] raw;
        public readonly string TypeName;

        public UnknownProperty(MemoryStream stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            TypeName = typeName;
            raw = stream.ReadBytes(size);
            PropType = PropertyType.Unknown;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteValueS32(pcc.FindNameOrAdd(Name));
                stream.WriteValueS32(0);
                stream.WriteValueS32(pcc.FindNameOrAdd(TypeName));
                stream.WriteValueS32(0);
                stream.WriteValueS32(raw.Length);
                stream.WriteValueS32(0); 
            }
            stream.WriteBytes(raw);
        }
    }
}
