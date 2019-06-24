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
        public IMEPackage Pcc { get; set; }
        private bool _isModified;

        public bool IsModified
        {
            get => _isModified;
            set => SetProperty(ref _isModified, value);
        }



        public enum Bio2DADataType
        {
            TYPE_UNDEFINED = -1,
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

        public Bio2DACell()
        {

        }

        public Bio2DACell(Bio2DADataType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        //public string GetDisplayableValue()
        //{
        //    switch (Type)
        //    {
        //        case Bio2DADataType.TYPE_INT:
        //            return BitConverter.ToInt32(Data, 0).ToString();
        //        case Bio2DADataType.TYPE_NAME:
        //            int name = BitConverter.ToInt32(Data, 0);
        //            var nameVal = Pcc.getNameEntry(name);
        //            int index = BitConverter.ToInt32(Data, 4);
        //            if (index > 0)
        //            {
        //                nameVal += "_" + index;
        //            }

        //            return nameVal;
        //        case Bio2DADataType.TYPE_FLOAT:
        //            return BitConverter.ToSingle(Data, 0).ToString();
        //    }

        //    return "Unknown type " + Type;
        //}

        /// <summary>
        /// This is a string because that's what the UI passes here
        /// </summary>
        public string NameIndex
        {
            get => Type != Bio2DADataType.TYPE_NAME ? "-1" : BitConverter.ToInt32(Data, 4).ToString();
            set
            {
                if (Type != Bio2DADataType.TYPE_NAME) return;
                if (int.TryParse(value, out int parsed) && parsed >= 0)
                {
                    BitConverter.GetBytes(parsed).CopyTo(Data, 4); 
                    IsModified = true;
                    OnPropertyChanged(nameof(DisplayableValue));
                }
            }
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
                        var nameVal = Pcc.getNameEntry(name);
                        int index = BitConverter.ToInt32(Data, 4);
                        if (index > 0)
                        {
                            nameVal += "_" + index;
                        }

                        return nameVal;
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
                            if (Data == null)
                            {
                                Data = new byte[8];
                            }
                            if (int.TryParse(value, out int parsed) && Pcc.isName(parsed) && !Data.SequenceEqual(BitConverter.GetBytes((long)parsed))) //has to be cast as long as 4 vs 8 bytes will never be equal
                            {
                                
                                BitConverter.GetBytes(parsed).CopyTo(Data,0);
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

        public override string ToString() => DisplayableValue;

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
