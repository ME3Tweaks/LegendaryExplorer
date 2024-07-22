using System;
using System.Globalization;
using LegendaryExplorerCore.Packages;
using PropertyChanged;

namespace LegendaryExplorerCore.Unreal.Classes
{
    [AddINotifyPropertyChangedInterface]
    public partial class Bio2DACell
    {
        /// <summary>
        /// Package that is used for name lookups so the correct name index can be determined.
        /// </summary>
        public IMEPackage package { get; init; }

        // Fody weaves setters for these below
        [AlsoNotifyFor(nameof(DisplayableValue))]
        public int IntValue { get; set; } = int.MinValue; // 11/13/2023 - Change to min value as default value is 0, which may yield no change of type when assigned

        [AlsoNotifyFor(nameof(DisplayableValue))]
        public float FloatValue { get; set; } = float.MinValue; // 11/13/2023 - Change to min value as default value is 0, which may yield no change of type when assigned

        private NameReference _nameValue;
        
        [DoNotNotify]
        public NameReference NameValue
        {
            get => _nameValue;
            set
            {
                if (_nameValue.Name != null)
                {
                    var oldName = _nameValue;
                    _nameValue = value;
                    IsModified = oldName != _nameValue;
                }
                else
                {
                    _nameValue = value;
                }
                Type = Bio2DADataType.TYPE_NAME;
            }
        }

        public int NameIndex
        {
            get
            {
                if (package == null)
                    // Should this throw an exception?
                    return 0;
                var result = package.findName(NameValue);
                if (result < 0)
                    return 0; // This is to prevent index out of bounds
                return result;
            }
            set
            {
                if (package == null)
                    // Should this throw an exception?
                    return; // Do nothing.
                NameValue = package.GetNameEntry(value);
            }
        }

        /// <summary>
        /// If the cell has been populated once (loaded)
        /// </summary>
        private bool Initialized;

        #region DO NOT REMOVE THESE - THEY ARE WEAVED BY FODY
        private void OnIntValueChanged()
        {
            Type = Bio2DADataType.TYPE_INT;
        }

        private void OnFloatValueChanged()
        {
            Type = Bio2DADataType.TYPE_FLOAT;
        }

        // This is not called due to [DoNotNotify] on NameValue
        private void OnNameValueChanged()
        {
            Type = Bio2DADataType.TYPE_NAME;
        }
        #endregion

        public int Offset { get; }

        /// <summary>
        /// If the 2DA has been modified
        /// </summary>
        public bool IsModified { get; set; }

        public enum Bio2DADataType : byte
        {
            TYPE_INT = 0,
            TYPE_NAME = 1,
            TYPE_FLOAT = 2,

            /// <summary>
            /// Not actual type. Used to allow us to know a node should be considered null during serialization.
            /// </summary>
            TYPE_NULL = 5
        }

        public Bio2DADataType Type { get; set; }

        private void OnTypeChanged()
        {
            if (Initialized)
                IsModified = true;
        }

        private Bio2DACell(Bio2DADataType type, int offset, IMEPackage pcc = null)
        {
            Offset = offset;
            Type = type;
            // DO NOT INITIALIZE HERE - as value is set in templated constructors
        }

        public Bio2DACell(int intValue, int offset = 0) : this(Bio2DADataType.TYPE_INT, offset)
        {
            IntValue = intValue;
            Initialized = true;
        }

        public Bio2DACell(float floatValue, int offset = 0) : this(Bio2DADataType.TYPE_FLOAT, offset)
        {
            FloatValue = floatValue;
            Initialized = true;
        }

        public Bio2DACell(NameReference nameValue, IMEPackage pcc, int offset = 0) : this(Bio2DADataType.TYPE_NAME, offset, pcc)
        {
            NameValue = nameValue;
            Initialized = true;
        }

        /// <summary>
        /// Generates a new TYPE_NULL Bio2DACell. Cells of this type will not be serialized.
        /// </summary>
        public Bio2DACell() : this(Bio2DADataType.TYPE_NULL, 0)
        {
            Initialized = true;
        }

        /// <summary>
        /// The numerical portion of the Name reference. This is a string because that's what the UI passes here (as it's done via a text editor field)
        /// </summary>
        public string NameNumber
        {
            get => Type != Bio2DADataType.TYPE_NAME ? "-1" : NameValue.Number.ToString();
            set
            {
                if (Type != Bio2DADataType.TYPE_NAME) return;
                if (int.TryParse(value, out int parsed) && parsed >= 0)
                {
                    var oldName = NameValue;
                    NameValue = new NameReference(NameValue.Name, parsed);
                    IsModified = oldName != NameValue;
                    //OnPropertyChanged(nameof(DisplayableValue));
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
                    Bio2DADataType.TYPE_FLOAT => FloatValue.ToString(CultureInfo.InvariantCulture),
                    Bio2DADataType.TYPE_NULL => "",
                    _ => $"Unknown type {Type}"
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
                        throw new Exception("Bio2DA: Cannot set Name through DisplayableValue. Use NameValue instead.");
                    case Bio2DADataType.TYPE_NULL:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public override string ToString() => DisplayableValue;

        internal string GetTypeString() =>
            Type switch
            {
                Bio2DADataType.TYPE_FLOAT => "Float",
                Bio2DADataType.TYPE_NAME => "Name",
                Bio2DADataType.TYPE_INT => "Integer",
                Bio2DADataType.TYPE_NULL => "NULL",
                _ => "Unknown type"
            };
    }
}
