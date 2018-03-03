using ME3Explorer.Packages;
using System;

namespace ME3Explorer
{

    internal class Bio2DACell
    {
        public const byte TYPE_INT = 0;
        public const byte TYPE_NAME = 1;
        public const byte TYPE_FLOAT = 2;
        byte[] Data { get; set; }
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

        int GetIntValue()
        {
            return BitConverter.ToInt32(Data, 0);
        }

        float GetFloatValue()
        {
            return BitConverter.ToSingle(Data, 0);
        }

        int GetNameIndex()
        {
            return BitConverter.ToInt32(Data, 0);
        }
    }
}