using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Packages;

namespace ME3Explorer.Unreal.Classes
{
    public class Sequence
    {
        public IMEPackage pcc;
        public int index;
        public List<PropertyReader.Property> props;
        public List<int> SequenceObjects;

        public Sequence(IMEPackage Pcc, int export)
        {
            pcc = Pcc;
            index = export;
            Deserialize();
        }

        public void Deserialize()
        {
            props = PropertyReader.getPropList(pcc, pcc.IExports[index]);
            getSequenceObjects();
        }

        public void getSequenceObjects()
        {
            if (props == null || props.Count == 0)
                return;
            for (int i = 0; i < props.Count(); i++)
                if (pcc.getNameEntry(props[i].Name) == "SequenceObjects")
                {
                    SequenceObjects = new List<int>();
                    byte[] buff = props[i].raw;
                    BitConverter.IsLittleEndian = true;
                    int count = BitConverter.ToInt32(buff, 24);
                    for (int j = 0; j < count; j++)
                        SequenceObjects.Add(BitConverter.ToInt32(buff, 28 + j * 4));
                    SequenceObjects.Sort();
                }
        }
    }
}
