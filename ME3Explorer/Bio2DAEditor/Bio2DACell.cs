using ME3Explorer.Packages;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Explorer
{
    public class Bio2DACell
    {
        public const byte TYPE_INT = 0;
        public const byte TYPE_NAME = 1;
        public const byte TYPE_FLOAT = 2;
        public byte[] Data { get; set; }
        public int Offset { get; private set; }
        public IMEPackage Pcc { get; private set; }
        public byte Type { get; set; }
        public Bio2DACell(IMEPackage pcc, int offset, byte type, byte[] data)
        {
            Offset = offset;
            Pcc = pcc;
            Type = type;
            Data = data;
        }

        public string GetDisplayableValue()
        {
            switch (Type)
            {
                case TYPE_INT:
                    return BitConverter.ToInt32(Data, 0).ToString();
                case TYPE_NAME:
                    int name = BitConverter.ToInt32(Data, 0);
                    return Pcc.getNameEntry(name);
                case TYPE_FLOAT:
                    return BitConverter.ToSingle(Data, 0).ToString();
            }
            return "Unknown type " + Type;
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
    }
}
