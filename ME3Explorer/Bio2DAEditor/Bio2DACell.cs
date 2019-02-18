using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public class Bio2DACell : NotifyPropertyChangedBase
    {
        public byte[] Data { get; set; }
        public int Offset { get; private set; }
        public IMEPackage Pcc { get; private set; }
        private bool _isModified = false;
        public bool IsModified
        {
            get => _isModified;
            set => SetProperty(ref _isModified, value);
        }



        public enum Bio2DADataType
        {
            TYPE_INT = 0,
            TYPE_NAME = 1,
            TYPE_FLOAT = 2
        }

        public Bio2DADataType Type { get; set; }
        public Bio2DACell(IMEPackage pcc, int offset, byte type, byte[] data)
        {
            Offset = offset;
            Pcc = pcc;
            Type = (Bio2DADataType)type;
            Data = data;
        }

        public string GetDisplayableValue()
        {
            switch (Type)
            {
                case Bio2DADataType.TYPE_INT:
                    return BitConverter.ToInt32(Data, 0).ToString();
                case Bio2DADataType.TYPE_NAME:
                    int name = BitConverter.ToInt32(Data, 0);
                    return Pcc.getNameEntry(name);
                case Bio2DADataType.TYPE_FLOAT:
                    return BitConverter.ToSingle(Data, 0).ToString();
            }
            return "Unknown type " + Type;
        }

        public string DisplayableValue
        {
            get
            {
                switch (Type)
                {
                    case Bio2DADataType.TYPE_INT:
                        return BitConverter.ToInt32(Data, 0).ToString();
                    case Bio2DADataType.TYPE_NAME:
                        int name = BitConverter.ToInt32(Data, 0);
                        return Pcc.getNameEntry(name);
                    case Bio2DADataType.TYPE_FLOAT:
                        return BitConverter.ToSingle(Data, 0).ToString();
                }
                return "Unknown type " + Type;
            }
            set
            {
                switch (Type)
                {
                    case Bio2DADataType.TYPE_INT:
                        {
                            if (int.TryParse(value, out int parsed) && !Data.SequenceEqual(BitConverter.GetBytes(parsed)))
                            {
                                Data = BitConverter.GetBytes(parsed);
                                IsModified = true;
                            }
                        }
                        break;
                    case Bio2DADataType.TYPE_NAME:
                        {
                            if (int.TryParse(value, out int parsed) && Pcc.isName(parsed) && !Data.SequenceEqual(BitConverter.GetBytes(parsed)))
                            {
                                Data = BitConverter.GetBytes((long)parsed);
                                IsModified = true;
                            }
                        }
                        break;
                    case Bio2DADataType.TYPE_FLOAT:
                        {
                            if (float.TryParse(value, out float parsed) && !Data.SequenceEqual(BitConverter.GetBytes(parsed)))
                            {
                                Data = BitConverter.GetBytes(parsed);
                                IsModified = true;
                            }
                        }
                        break;
                }
            }
        }

        public override string ToString()
        {
            return GetDisplayableValue();
        }

        public int GetIntValue()
        {
            return BitConverter.ToInt32(Data, 0);
        }

        public int ValueAsName
        {
            get => GetIntValue();
            set => Data = BitConverter.GetBytes((long)value);
        }

        public float GetFloatValue()
        {
            return BitConverter.ToSingle(Data, 0);
        }

        internal string GetTypeString()
        {
            switch (Type)
            {
                case Bio2DADataType.TYPE_FLOAT:
                    return "Float";
                case Bio2DADataType.TYPE_NAME:
                    return "Name";
                case Bio2DADataType.TYPE_INT:
                    return "Integer";
                default:
                    return "Unknown type";
            }
        }
    }
}
