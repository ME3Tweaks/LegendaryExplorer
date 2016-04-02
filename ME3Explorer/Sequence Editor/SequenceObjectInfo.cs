using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using KFreonLib.MEDirectories;

namespace ME3Explorer.SequenceObjects
{
    public static class SequenceObjectInfo
    {
        public class Info
        {
            public List<string> inputLinks;

            public Info()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, Info> objects = new Dictionary<string, Info>();

        public static void loadfromJSON()
        {
            string path = System.Windows.Forms.Application.StartupPath + "//exec//SequenceObjectInfo.json";

            try
            {
                if (objects.Count == 0 && System.IO.File.Exists(path))
                {
                    objects = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, Info>>(System.IO.File.ReadAllText(path)); 
                }
            }
            catch (Exception)
            {
            }
        }

        public static Info getInfo(string objectName, PCCObject pcc)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (objectName == "")
            {
                return null;
            }
            else if (objects.ContainsKey(objectName))
            {
                return objects[objectName];
            }
            //looks for default class instance to get input links. made everything slow, so I cached the information in SequenceObjectInfo.json
            /*PCCObject[] pccs = new PCCObject[] { pcc, new PCCObject(ME3Directory.cookedPath + "Engine.pcc"), new PCCObject(ME3Directory.cookedPath + "SFXGame.pcc"), new PCCObject(ME3Directory.cookedPath + "WwiseAudio.pcc") };
            foreach (PCCObject pccRef in pccs)
            {
                for (int i = 0; i < pccRef.Exports.Count; i++)
                {
                    List<PCCObject.ExportEntry> Exports = pccRef.Exports;
                    if (Exports[i].ClassName == "Class" && Exports[i].ObjectName == objectName)
                    {
                        Info info = new Info();
                        PropertyReader.Property inputLinks = PropertyReader.getPropOrNull(pccRef, Exports[i + 1], "InputLinks");
                        if (inputLinks == null)
                        {
                            string superClass = Exports[i].ClassParent;
                            if (superClass == "Class")
                            {
                                objects.Add(objectName, null);
                                return null;
                            }
                            info = getInfo(Exports[i].ClassParent, pccRef);
                            objects.Add(objectName, info);
                            return info;
                        }
                        else
                        {
                            int pos = 28;
                            byte[] global = inputLinks.raw;
                            int count = BitConverter.ToInt32(global, 24);
                            for (int j = 0; j < count; j++)
                            {
                                List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pccRef, global, pos);

                                info.inputLinks.Add(p2[0].Value.StringValue);
                                for (int k = 0; k < p2.Count(); k++)
                                    pos += p2[k].raw.Length;
                            }
                            objects.Add(objectName, info);
                            return info;
                        }
                    }
                }
            }*/
            return null;
        }
    }
}
