using System;
using System.Collections.Generic;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Unreal;
using static LegendaryExplorer.Tools.ScriptDebugger.DebuggerInterface;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public abstract class PropertyValue : NotifyPropertyChangedBase
    {
        protected readonly IntPtr Address;

        protected readonly DebuggerInterface Debugger;

        public string PropName { get; }

        protected PropertyValue(DebuggerInterface debugger, IntPtr address, string propName)
        {
            PropName = propName;
            Address = address;
            Debugger = debugger;
        }
    }

    public class IntPropertyValue : PropertyValue
    {
        private int _value;
        public int Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public IntPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, int value) : base(debugger, address, propName)
        {
            _value = value;
        }

        private void WriteValue()
        {
            Debugger.WriteValue(Address, _value);
        }
    }

    public class FloatPropertyValue : PropertyValue
    {
        private float _value;
        public float Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public FloatPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, float value) : base(debugger, address, propName)
        {
            _value = value;
        }

        private void WriteValue()
        {
            Debugger.WriteValue(Address, _value);
        }
    }

    public class StringRefPropertyValue : PropertyValue
    {
        private int _value;
        public int Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public StringRefPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, int value) : base(debugger, address, propName)
        {
            _value = value;
        }

        private void WriteValue()
        {
            Debugger.WriteValue(Address, _value);
        }
    }

    public class BoolPropertyValue : PropertyValue
    {
        private bool _value;
        public bool Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        private readonly uint BitMask;

        public BoolPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, bool value, uint bitmask) : base(debugger, address, propName)
        {
            _value = value;
            BitMask = bitmask;
        }

        private void WriteValue()
        {
            uint fullVal = Debugger.ReadValue<uint>(Address);
            if (_value)
            {
                fullVal |= BitMask;
            }
            else
            {
                fullVal &= ~BitMask;
            }
            Debugger.WriteValue(Address, fullVal);
        }
    }

    public class StrPropertyValue : PropertyValue
    {
        private string oldVal;
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                oldVal = _value;
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        //NOT including the trailing null. This is the max C# string length
        public int MaxStringLength { get; }

        public StrPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, string value, int maxLen) : base(debugger, address, propName)
        {
            oldVal =_value = value;
            MaxStringLength = maxLen;
        }

        private void WriteValue()
        {
            var fString = Debugger.ReadValue<TArray>(Address);
            if (Debugger.WriteUnicodeString(fString.Data, _value, fString.Max))
            {
                fString.Count = _value.Length + 1;
                Debugger.WriteValue(Address, fString);
            }
            else
            {
                _value = oldVal;
                OnPropertyChanged(nameof(Value));
            }
        }
    }

    public class BytePropertyValue : PropertyValue
    {
        private byte _value;
        public byte Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public BytePropertyValue(DebuggerInterface debugger, IntPtr address, string propName, byte value) : base(debugger, address, propName)
        {
            _value = value;
        }

        private void WriteValue()
        {
            Debugger.WriteValue(Address, _value);
        }
    }

    public class EnumPropertyValue : PropertyValue
    {
        private NameReference _value;
        public NameReference Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public List<NameReference> EnumValues { get; }

        private readonly List<FName> FNames;

        public EnumPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, NameReference value, List<FName> fNames) : base(debugger, address, propName)
        {
            _value = value;
            FNames = fNames;
            EnumValues = new List<NameReference>(fNames.Count);
            foreach (FName fName in fNames)
            {
                EnumValues.Add(Debugger.GetNameReference(fName));
            }
        }

        private void WriteValue()
        {
            Debugger.WriteValue(Address, FNames[EnumValues.IndexOf(_value)]);
        }
    }

    public class DelegatePropertyValue : PropertyValue
    {
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public DelegatePropertyValue(DebuggerInterface debugger, IntPtr address, string propName, FScriptDelegate value) : base(debugger, address, propName)
        {
            var obj = Debugger.ReadObject(value.Object);
            var name = Debugger.GetNameReference(value.FunctionName);
            _value = obj is null ? name.Instanced : $"{obj.GetFullPath()}.{name.Instanced}";
        }

        private void WriteValue()
        {
            //Not sure how to implement editing of this one
        }
    }

    public class NamePropertyValue : PropertyValue
    {
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        public NamePropertyValue(DebuggerInterface debugger, IntPtr address, string propName, NameReference value) : base(debugger, address, propName)
        {
            _value = value.Instanced;
        }

        private void WriteValue()
        {
            //No idea how to make this editable 
        }
    }

    public class ObjectPropertyValue : PropertyValue
    {
        private string _value;
        public string Value
        {
            get => _value;
            set
            {
                if (SetProperty(ref _value, value))
                {
                    WriteValue();
                }
            }
        }

        private readonly NObject Object;

        public ObservableCollectionExtended<PropertyValue> Properties { get; } = new();

        public ObjectPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, NObject value) : base(debugger, address, propName)
        {
            _value = value?.GetFullPath() ?? "None";
            Object = value;
            if (value is not null)
            {
                Properties.Add(new LoadingPropertyValue());
            }
        }

        public void LoadProperties()
        {
            Properties.ClearEx();
            //class will be null if the object is a UClass
            if (Object.Class is not null)
            {
                Properties.ReplaceAll(Object.Class.GetProperties(Object.Address));
            }
        }

        private void WriteValue()
        {
            //No idea how to make this editable 
        }
    }

    public class ClassPropertyValue : ObjectPropertyValue
    {
        public ClassPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, NClass value) : base(debugger, address, propName, value)
        {
            Properties.ClearEx();
        }
    }

    public class InterfacePropertyValue : ObjectPropertyValue
    {
        public InterfacePropertyValue(DebuggerInterface debugger, IntPtr address, string propName, FScriptInterface value) : base(debugger, address, propName, debugger.ReadObject(value.Object))
        {
        }
    }

    public class ArrayPropertyValue : PropertyValue
    {
        public string Value { get; }

        public ObservableCollectionExtended<PropertyValue> Elements { get; } = new();

        public ArrayPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, List<PropertyValue> elements) : base(debugger, address, propName)
        {
            Value = $"{elements.Count} elements";
            Elements.AddRange(elements);
        }
    }

    public class StructPropertyValue : PropertyValue
    {
        public string Value { get; }

        public ObservableCollectionExtended<PropertyValue> Properties { get; } = new();

        private readonly NStruct NStruct;

        public StructPropertyValue(DebuggerInterface debugger, IntPtr address, string propName, string structName, NStruct nStruct) : base(debugger, address, propName)
        {
            Value = structName;
            NStruct = nStruct;
            if (nStruct.FirstChild is not null)
            {
                Properties.Add(new LoadingPropertyValue());
            }
        }

        public void LoadProperties()
        {
            Properties.ClearEx();
            Properties.ReplaceAll(NStruct.GetProperties(Address));
        }
    }

    public class LoadingPropertyValue : PropertyValue
    {
        public LoadingPropertyValue() : base(null, IntPtr.Zero, "Loading...")
        {
        }
    }
}