using System;
using LegendaryExplorer.Misc;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;

namespace LegendaryExplorer.UserControls.ExportLoaderControls
{
    public class Bio2DACell : NotifyPropertyChangedBase
    {
        public int IntValue;

        public float FloatValue;

        public NameReference NameValue;
        public int Offset { get; }
        private readonly IMEPackage Pcc;
        private bool _isModified;

        public bool IsModified
        {
            get => _isModified;
            set => SetProperty(ref _isModified, value);
        }



        public enum Bio2DADataType : byte
        {
            TYPE_INT = 0,
            TYPE_NAME = 1,
            TYPE_FLOAT = 2
        }

        public Bio2DADataType Type { get; }

        private Bio2DACell(Bio2DADataType type, int offset, IMEPackage pcc = null)
        {
            Offset = offset;
            Pcc = pcc;
            Type = type;
        }

        public Bio2DACell(int intValue, int offset = 0) : this(Bio2DADataType.TYPE_INT, offset)
        {
            IntValue = intValue;
        }

        public Bio2DACell(float floatValue, int offset = 0) : this(Bio2DADataType.TYPE_FLOAT, offset)
        {
            FloatValue = floatValue;
        }

        public Bio2DACell(NameReference nameValue, IMEPackage pcc, int offset = 0) : this(Bio2DADataType.TYPE_NAME, offset, pcc)
        {
            NameValue = nameValue;
        }

        /// <summary>
        /// This is a string because that's what the UI passes here
        /// </summary>
        public string NameIndex
        {
            get => Type != Bio2DADataType.TYPE_NAME ? "-1" : NameValue.Number.ToString();
            set
            {
                if (Type != Bio2DADataType.TYPE_NAME) return;
                if (int.TryParse(value, out int parsed) && parsed >= 0)
                {
                    NameValue = new NameReference(NameValue.Name, parsed);
                    IsModified = true;
                    OnPropertyChanged(nameof(DisplayableValue));
                }
            }
        }

        public string DisplayableValue
        {
            get =>
                Type switch
                {
                    Bio2DADataType.TYPE_INT => IntValue.ToString(),
                    Bio2DADataType.TYPE_NAME => NameValue.Instanced,
                    Bio2DADataType.TYPE_FLOAT => FloatValue.ToString(),
                    _ => "Unknown type " + Type
                };
            set
            {
                switch (Type)
                {
                    case Bio2DADataType.TYPE_INT:
                        {
                            if (int.TryParse(value, out int parsed) && parsed != IntValue)
                            {
                                IntValue = parsed;
                                IsModified = true;
                            }
                        }
                        break;
                    case Bio2DADataType.TYPE_FLOAT:
                        {
                            if (float.TryParse(value, out float parsed) && parsed != FloatValue)
                            {
                                FloatValue = parsed;
                                IsModified = true;
                            }
                        }
                        break;
                    case Bio2DADataType.TYPE_NAME: //This is set through ValueAsName
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public override string ToString() => DisplayableValue;

        public int ValueAsName
        {
            get => Pcc.findName(NameValue.Name);
            set
            {
                if (value != ValueAsName)
                {
                    NameValue = Pcc.GetNameEntry(value);
                    IsModified = true;
                    OnPropertyChanged(nameof(ValueAsName));
                }
            }

        }

        internal string GetTypeString() =>
            Type switch
            {
                Bio2DADataType.TYPE_FLOAT => "Float",
                Bio2DADataType.TYPE_NAME => "Name",
                Bio2DADataType.TYPE_INT => "Integer",
                _ => "Unknown type"
            };
    }
}
