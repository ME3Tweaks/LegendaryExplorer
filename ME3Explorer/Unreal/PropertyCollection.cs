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
using System.Collections;

namespace ME3Explorer.Unreal
{
    public class PropertyCollection : ObservableCollection<UProperty>
    {
        static Dictionary<string, PropertyCollection> defaultStructValues = new Dictionary<string, PropertyCollection>();

        public int endOffset;

        /// <summary>
        /// Gets the UProperty with the specified name, returns null if not found
        /// </summary>
        /// <param name="name"></param>
        /// <returns>specified UProperty or null if not found</returns>
        public T GetProp<T>(string name) where T : UProperty
        {
            foreach (var prop in this)
            {
                if (prop.Name.Name == name)
                {
                    return prop as T;
                }
            }
            return null;
        }

        public void AddOrReplaceProp(UProperty prop)
        {
            for (int i = 0; i < this.Count; i++)
            {
                if (this[i].Name.Name == prop.Name.Name)
                {
                    this[i] = prop;
                    return;
                }
            }
            this.Add(prop);
        }

        public void WriteTo(Stream stream, IMEPackage pcc)
        {
            foreach (var prop in this)
            {
                prop.WriteTo(stream, pcc);
            }
            stream.WriteNoneProperty(pcc);
        }

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
                NameReference nameRef = new NameReference { Name = name, Number = stream.ReadValueS32() };
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
                string namev = pcc.getNameEntry(typeIdx);
                if (Enum.IsDefined(typeof(PropertyType), namev))
                {
                    Enum.TryParse(namev, out type);
                } else
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
                                    enumType.Name = pcc.getNameEntry(stream.ReadValueS32());
                                    enumType.Number = stream.ReadValueS32();
                                }
                                else
                                {
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
                            props.Add(ReadArrayProperty(stream, pcc, typeName, nameRef));
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
                        props.Add(new DelegateProperty(stream, pcc, nameRef));
                        break;
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
            if (props.Count > 0)
            {
                if (props[props.Count - 1].PropType != PropertyType.None)
                {
                    stream.Seek(startPosition, SeekOrigin.Begin);
                    return new PropertyCollection { endOffset = (int)stream.Position };
                }
                //remove None Property
                props.RemoveAt(props.Count - 1);
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
                        defaultProps = ME3UnrealObjectInfo.getDefaultStructValue(structType);
                        if (defaultProps == null)
                        {
                            props.Add(new UnknownProperty(stream, size));
                            return props;
                        }
                        defaultStructValues.Add(structType, defaultProps);
                    }
                    for (int i = 0; i < defaultProps.Count; i++)
                    {
                        UProperty uProperty = ReadSpecialStructProp(pcc, stream, defaultProps[i], structType);
                        if (uProperty.PropType != PropertyType.None)
                        {
                            props.Add(uProperty);
                        }
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
                    props.Add(new IntProperty(stream, labels[i]));
                }
            }
            else if (structType == "Vector2d" || structType == "RwVector2")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "Vector" || structType == "RwVector3")
            {
                string[] labels = { "X", "Y", "Z" };
                for (int i = 0; i < 3; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "Color")
            {
                string[] labels = { "B", "G", "R", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new ByteProperty(stream, labels[i]));
                }
            }
            else if (structType == "LinearColor")
            {
                string[] labels = { "R", "G", "B", "A" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            //uses EndsWith to support RwQuat, RwVector4, and RwPlane
            else if (structType.EndsWith("Quat") || structType.EndsWith("Vector4") || structType.EndsWith("Plane"))
            {
                string[] labels = { "X", "Y", "Z", "W" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
                }
            }
            else if (structType == "TwoVectors")
            {
                string[] labels = { "X", "Y", "Z", "X", "Y", "Z" };
                for (int i = 0; i < 6; i++)
                {
                    props.Add(new FloatProperty(stream, labels[i]));
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
                        structProps.Add(new FloatProperty(stream, labels2[j]));
                    }
                    props.Add(new StructProperty("Plane", structProps, labels[i], true));
                }
            }
            else if (structType == "Guid")
            {
                string[] labels = { "A", "B", "C", "D" };
                for (int i = 0; i < 4; i++)
                {
                    props.Add(new IntProperty(stream, labels[i]));
                }
            }
            else if (structType == "IntPoint")
            {
                string[] labels = { "X", "Y" };
                for (int i = 0; i < 2; i++)
                {
                    props.Add(new IntProperty(stream, labels[i]));
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
                        structProps.Add(new FloatProperty(stream, labels2[j]));
                    }
                    props.Add(new StructProperty("Vector", structProps, labels[i], true));
                }
                props.Add(new ByteProperty(stream, "IsValid"));
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
                        return new EnumProperty(stream, pcc, enumType, template.Name);
                    }
                    return new ByteProperty(stream, template.Name);
                case PropertyType.BioMask4Property:
                    return new BioMask4Property(stream, template.Name);
                case PropertyType.StrProperty:
                    return new StrProperty(stream, template.Name);
                case PropertyType.ArrayProperty:
                    return ReadArrayProperty(stream, pcc, structType, template.Name, true);
                case PropertyType.StructProperty:
                    PropertyCollection structProps = ReadSpecialStruct(pcc, stream, UnrealObjectInfo.GetPropertyInfo(pcc.Game, template.Name, structType).reference, 0);
                    return new StructProperty(structType, structProps, template.Name, true);
                case PropertyType.None:
                    return new NoneProperty(template.Name);
                case PropertyType.DelegateProperty:
                    throw new NotImplementedException("cannot read Delegate property of Immutable struct");
                case PropertyType.Unknown:
                    throw new NotImplementedException("cannot read Unknown property of Immutable struct");
            }
            throw new NotImplementedException("cannot read Unknown property of Immutable struct");
        }

        public static UProperty ReadArrayProperty(MemoryStream stream, IMEPackage pcc, string enclosingType, NameReference name, bool IsInImmutable = false)
        {
            long arrayOffset = stream.Position - 24;
            ArrayType arrayType = UnrealObjectInfo.GetArrayType(pcc.Game, name, enclosingType);
            int count = stream.ReadValueS32();
            switch (arrayType)
            {
                case ArrayType.Object:
                    {
                        var props = new List<ObjectProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new ObjectProperty(stream));
                        }
                        return new ArrayProperty<ObjectProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Name:
                    {
                        var props = new List<NameProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new NameProperty(stream, pcc));
                        }
                        return new ArrayProperty<NameProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Enum:
                    {
                        var props = new List<EnumProperty>();
                        NameReference enumType = new NameReference { Name = UnrealObjectInfo.GetEnumType(pcc.Game, name, enclosingType) };
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new EnumProperty(stream, pcc, enumType));
                        }
                        return new ArrayProperty<EnumProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Struct:
                    {
                        var props = new List<StructProperty>();
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
                                PropertyCollection structProps = ReadSpecialStruct(pcc, stream, arrayStructType, arraySize / count);
                                props.Add(new StructProperty(arrayStructType, structProps, isImmutable: true));
                            }
                        }
                        else
                        {
                            for (int i = 0; i < count; i++)
                            {
                                PropertyCollection structProps = ReadProps(pcc, stream, arrayStructType);
                                props.Add(new StructProperty(arrayStructType, structProps));
                            }
                        }
                        return new ArrayProperty<StructProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Bool:
                    {
                        var props = new List<BoolProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new BoolProperty(stream, pcc.Game));
                        }
                        return new ArrayProperty<BoolProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.String:
                    {
                        var props = new List<StrProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new StrProperty(stream));
                        }
                        return new ArrayProperty<StrProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Float:
                    {
                        var props = new List<FloatProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new FloatProperty(stream));
                        }
                        return new ArrayProperty<FloatProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Byte:
                    {
                        var props = new List<ByteProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new ByteProperty(stream));
                        }
                        return new ArrayProperty<ByteProperty>(arrayOffset, props, arrayType, name);
                    }
                case ArrayType.Int:
                default:
                    {
                        var props = new List<IntProperty>();
                        for (int i = 0; i < count; i++)
                        {
                            props.Add(new IntProperty(stream));
                        }
                        return new ArrayProperty<IntProperty>(arrayOffset, props, arrayType, name);
                    }
            }
        }

    }

    public abstract class UProperty : ViewModelBase
    {
        public PropertyType PropType;
        private NameReference _name;
        public long Offset;

        public NameReference Name
        {
            get { return _name; }
            set { SetProperty(ref _name, value); }
        }

        protected UProperty(NameReference? name)
        {
            _name = name ?? new NameReference();
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

        public StructProperty(string structType, bool isImmutable, params UProperty[] props) : base(null)
        {
            StructType = structType;
            IsImmutable = isImmutable;
            PropType = PropertyType.StructProperty;
            Properties = new PropertyCollection();
            foreach (var prop in props)
            {
                Properties.Add(prop);
            }

        }

        public T GetProp<T>(string name) where T : UProperty
        {
            return Properties.GetProp<T>(name);
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (valueOnly)
            {
                foreach (var prop in Properties)
                {
                    prop.WriteTo(stream, pcc, IsImmutable);
                }
                if (!IsImmutable)
                {
                    stream.WriteNoneProperty(pcc);
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
                    if (!IsImmutable)
                    {
                        m.WriteNoneProperty(pcc);
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
            Offset = stream.Position;
            Value = stream.ReadValueS32();
            PropType = PropertyType.IntProperty;
        }

        public IntProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
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

        public static implicit operator IntProperty(int n)
        {
            return new IntProperty(n);
        }

        public static implicit operator int(IntProperty p)
        {
            return p.Value;
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
            Offset = stream.Position;
            Value = stream.ReadValueF32();
            PropType = PropertyType.FloatProperty;
        }

        public FloatProperty(float val, NameReference? name = null) : base(name)
        {
            Value = val;
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

        public static implicit operator FloatProperty(float n)
        {
            return new FloatProperty(n);
        }

        public static implicit operator float(FloatProperty p)
        {
            return p.Value;
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
            Offset = stream.Position;
            Value = stream.ReadValueS32();
            PropType = PropertyType.ObjectProperty;
        }

        public ObjectProperty(int val, NameReference? name = null) : base(name)
        {
            Value = val;
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
            Offset = stream.Position;
            NameReference nameRef = new NameReference();
            nameRef.Name = pcc.getNameEntry(stream.ReadValueS32());
            nameRef.Number = stream.ReadValueS32();
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
                stream.WriteValueS32(Value.Number);
            }
        }

        public override string ToString()
        {
            return Value;
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
            Offset = stream.Position;
            Value = game == MEGame.ME3 ? stream.ReadValueB8() : stream.ReadValueB32();
            PropType = PropertyType.BoolProperty;
        }

        public BoolProperty(bool val, NameReference? name = null) : base(name)
        {
            Value = val;
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

        public static implicit operator BoolProperty(bool n)
        {
            return new BoolProperty(n);
        }

        public static implicit operator bool(BoolProperty p)
        {
            return p.Value;
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
            Offset = stream.Position;
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
            Offset = stream.Position;
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
        public List<string> EnumValues { get; private set; }

        public EnumProperty(MemoryStream stream, IMEPackage pcc, NameReference enumType, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
            EnumType = enumType;
            NameReference enumVal = new NameReference();
            enumVal.Name = pcc.getNameEntry(stream.ReadValueS32());
            enumVal.Number = stream.ReadValueS32();
            Value = enumVal;
            EnumValues = UnrealObjectInfo.GetEnumValues(pcc.Game, enumType, true);
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
                stream.WriteValueS32(Value.Number);
            }
        }
    }

    public abstract class ArrayPropertyBase : UProperty
    {
        public abstract IEnumerable<UProperty> ValuesAsProperties { get; }

        protected ArrayPropertyBase(NameReference? name) : base(name)
        {
        }
    }

    public class ArrayProperty<T> : ArrayPropertyBase, IEnumerable<T>, IList<T> where T : UProperty
    {
        public List<T> Values { get; private set; }
        public override IEnumerable<UProperty> ValuesAsProperties => Values.Cast<UProperty>();
        public readonly ArrayType arrayType;

        public ArrayProperty(long startOffset, List<T> values, ArrayType type, NameReference name) : base(name)
        {
            Offset = startOffset;
            PropType = PropertyType.ArrayProperty;
            arrayType = type;
            Values = values;
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

        #region IEnumerable<T>
        public IEnumerator<T> GetEnumerator()
        {
            return Values.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return Values.GetEnumerator();
        }
        #endregion

        #region IList<T>
        public int Count { get { return Values.Count; } }
        public bool IsReadOnly { get { return ((ICollection<T>)Values).IsReadOnly; } }

        public T this[int index]
        {
            get { return Values[index]; }
            set { Values[index] = value; }
        }

        public void Add(T item)
        {
            Values.Add(item);
        }

        public void Clear()
        {
            Values.Clear();
        }

        public bool Contains(T item)
        {
            return Values.Contains(item);
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            Values.CopyTo(array, arrayIndex);
        }

        public bool Remove(T item)
        {
            return Values.Remove(item);
        }

        public void RemoveAt(int index)
        {
            Values.RemoveAt(index);
        }

        public int IndexOf(T item)
        {
            return Values.IndexOf(item);
        }

        public void Insert(int index, T item)
        {
            Values.Insert(index, item);
        }
        #endregion
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
            Offset = stream.Position;
            int count = stream.ReadValueS32();
            var streamPos = stream.Position;

            if (count < 0)
            {
                count *= -2;
                Value = stream.ReadString(count, true, Encoding.Unicode);
            }
            else if (count > 0)
            {
                Value = stream.ReadString(count, true, Encoding.ASCII);
            }
            else
            {
                Value = string.Empty;
            }

            //for when the end of the string has multiple nulls at the end
            if (stream.Position < streamPos + count)
            {
                stream.Seek(streamPos + count, SeekOrigin.Begin);
            }

            PropType = PropertyType.StrProperty;
        }

        public StrProperty(string val, NameReference? name = null) : base(name)
        {
            Value = val ?? string.Empty;
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
                if (pcc.Game == MEGame.ME3)
                {
                    stream.WriteStringUnicode(Value);
                }
                else
                {
                    stream.WriteStringASCII(Value);
                }
            }
        }

        public static implicit operator StrProperty(string s)
        {
            return new StrProperty(s);
        }

        public static implicit operator string(StrProperty p)
        {
            return p.Value;
        }

        public override string ToString()
        {
            return Value;
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
            Offset = stream.Position;
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

    public class DelegateProperty : UProperty
    {
        public int unk;
        public NameReference Value;

        public DelegateProperty(MemoryStream stream, IMEPackage pcc, NameReference? name = null) : base(name)
        {
            unk = stream.ReadValueS32();
            NameReference val = new NameReference();
            val.Name = pcc.getNameEntry(stream.ReadValueS32());
            val.Number = stream.ReadValueS32();
            Value = val;
            PropType = PropertyType.DelegateProperty;
        }

        public override void WriteTo(Stream stream, IMEPackage pcc, bool valueOnly = false)
        {
            if (!valueOnly)
            {
                stream.WriteDelegateProperty(pcc, Name, unk, Value);
            }
            else
            {
                stream.WriteValueS32(unk);
                stream.WriteValueS32(pcc.FindNameOrAdd(Value.Name));
                stream.WriteValueS32(Value.Number);
            }
        }
    }

    public class UnknownProperty : UProperty
    {
        public byte[] raw;
        public readonly string TypeName;

        public UnknownProperty(MemoryStream stream, int size, string typeName = null, NameReference? name = null) : base(name)
        {
            Offset = stream.Position;
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
