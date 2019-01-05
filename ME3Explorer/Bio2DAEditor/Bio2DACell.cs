﻿using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public class Bio2DACell : INotifyPropertyChanged
    {
        public const byte TYPE_INT = 0;
        public const byte TYPE_NAME = 1;
        public const byte TYPE_FLOAT = 2;
        public byte[] Data { get; set; }
        public int Offset { get; private set; }
        public IMEPackage Pcc { get; private set; }
        private bool _isModified = false;
        public bool IsModified
        {
            get { return _isModified; }
            set
            {
                if (value != _isModified) { SetProperty(ref _isModified, value); }
            }
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

        #region Property Changed Notification
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
        #endregion
    }
}
