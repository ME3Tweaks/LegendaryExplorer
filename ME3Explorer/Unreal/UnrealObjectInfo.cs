using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Unreal;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        public class SequenceObjectInfo
        {
            public List<string> inputLinks;

            public SequenceObjectInfo()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();

        public static void loadfromJSON()
        {
            string path = System.Windows.Forms.Application.StartupPath + "//exec//ME3ObjectInfo.json";

            try
            {
                if (System.IO.File.Exists(path))
                {
                    string raw = System.IO.File.ReadAllText(path);
                    var blob  = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
            {
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string objectName/*, PCCObject pcc*/)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (objectName == "")
            {
                return null;
            }
            else if (SequenceObjects.ContainsKey(objectName))
            {
                return SequenceObjects[objectName];
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

        public static List<string> getEnumValues(string enumName/*, PCCObject pcc*/)
        {
            if (enumName == "None" || enumName == "")
            {
                return null;
            }
            else if (Enums.ContainsKey(enumName))
            {
                return Enums[enumName];
            }
            /*PCCObject[] pccs = new PCCObject[] { pcc, new PCCObject(ME3Directory.cookedPath + "Engine.pcc"), new PCCObject(ME3Directory.cookedPath + "SFXGame.pcc") };
            foreach (PCCObject pccRef in pccs)
            {
                for (int i = 0; i < pccRef.Exports.Count; i++)
                {
                    List<PCCObject.ExportEntry> Exports = pccRef.Exports;
                    if (Exports[i].ClassName == "Enum" && Exports[i].ObjectName == enumName)
                    {
                        List<string> values = new List<string>();
                        byte[] buff = Exports[i].Data;
                        int count = BitConverter.ToInt32(buff, 20);
                        for (int j = 0; j < count; j++)
                        {
                            values.Add(pccRef.Names[BitConverter.ToInt32(buff, 24 + j * 8)]);
                        }
                        Enums.Add(enumName, values);
                        return values;
                    }
                }
            }*/
            return null;
        }
    }
}
