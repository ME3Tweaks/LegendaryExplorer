using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using KFreonLib.MEDirectories;
using Newtonsoft.Json;
using ME3Explorer.Packages;
using ME2Explorer.Unreal;
using ME1Explorer.Unreal;
using System.Diagnostics;

namespace ME3Explorer.Unreal
{
    public static class UnrealObjectInfo
    {
        public static bool isImmutable(string structType, MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.isImmutableStruct(structType);
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.isImmutableStruct(structType);
            }
            return false; //unknown game
        }

        public static bool inheritsFrom(this IExportEntry entry, string baseClass)
        {
            if (entry.FileRef.Game == MEGame.ME1)
            {
                return ME1UnrealObjectInfo.inheritsFrom(entry as ME1ExportEntry, baseClass);
            }
            else if (entry.FileRef.Game == MEGame.ME2)
            {
                return ME2UnrealObjectInfo.inheritsFrom(entry as ME2ExportEntry, baseClass);
            }
            else if (entry.FileRef.Game == MEGame.ME3)
            {
                return ME3UnrealObjectInfo.inheritsFrom(entry as ME3ExportEntry, baseClass);
            }
            else if (entry.FileRef.Game == MEGame.UDK)
            {
                return ME3UnrealObjectInfo.inheritsFrom(entry as UDKExportEntry, baseClass); //use me3?
            }
            return false;
        }

        public static string GetEnumType(MEGame game, string propName, string typeName, ClassInfo nonVanillaClassInfo = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName);
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName);
            }
            return null;
        }

        public static List<string> GetEnumValues(MEGame game, string enumName, bool includeNone)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, includeNone);
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getEnumValues(enumName, includeNone);
            }
            return null;
        }

        /// <summary>
        /// Gets the type of an array
        /// </summary>
        /// <param name="game">What game we are looking info for</param>
        /// <param name="propName">Name of the array property</param>
        /// <param name="className">Name of the class that should contain the information. If contained in a struct, this will be the name of the struct type</param>
        /// <param name="parsingEntry">Entry that is being parsed. Used for dynamic lookup if it's not in the DB</param>
        /// <returns></returns>
        public static ArrayType GetArrayType(MEGame game, string propName, string className, IEntry parsingEntry = null)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as IExportEntry);
            }
            return ArrayType.Int;
        }

        /// <summary>
        /// Gets property information for a property by name & containing class or struct name
        /// </summary>
        /// <param name="game">Game to lookup informatino from</param>
        /// <param name="propname">Name of property information to look up</param>
        /// <param name="containingClassOrStructName">Name of containing class or struct name</param>
        /// <param name="nonVanillaClassInfo">Dynamically built property info</param>
        /// <returns></returns>
        public static PropertyInfo GetPropertyInfo(MEGame game, string propname, string containingClassOrStructName, ClassInfo nonVanillaClassInfo = null)
        {
            bool inStruct = false;
            PropertyInfo p = null;
            switch (game)
            {
                case MEGame.ME1:
                    p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME2:
                    p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
                case MEGame.ME3:
                case MEGame.UDK:
                    p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                    break;
            }
            if (p == null)
            {
                inStruct = true;
                switch (game)
                {
                    case MEGame.ME1:
                        p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME2:
                        p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME3:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.UDK:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                }
            }
            return p;
        }

        /// <summary>
        /// Gets the default values for a struct
        /// </summary>
        /// <param name="game">Game to pull info from</param>
        /// <param name="typeName">Struct type name</param>
        /// <param name="stripTransients">Strip transients from the struct</param>
        /// <returns></returns>
        internal static PropertyCollection getDefaultStructValue(MEGame game, string typeName, bool stripTransients)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
                case MEGame.ME3:
                case MEGame.UDK:
                    return ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients);
            }
            return null;
        }
    }

    public static class ME3UnrealObjectInfo
    {

        public class SequenceObjectInfo
        {
            public List<string> inputLinks;

            public SequenceObjectInfo()
            {
                inputLinks = new List<string>();
            }
        }

        public static Dictionary<string, ClassInfo> Classes = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, ClassInfo> Structs = new Dictionary<string, ClassInfo>();
        public static Dictionary<string, SequenceObjectInfo> SequenceObjects = new Dictionary<string, SequenceObjectInfo>();
        public static Dictionary<string, List<string>> Enums = new Dictionary<string, List<string>>();

        private static string[] ImmutableStructs = { "Vector", "Color", "LinearColor", "TwoVectors", "Vector4", "Vector2D", "Rotator", "Guid", "Plane", "Box",
            "Quat", "Matrix", "IntPoint", "ActorReference", "ActorReference", "ActorReference", "PolyReference", "AimTransform", "AimTransform", "AimOffsetProfile", "FontCharacter",
            "CoverReference", "CoverInfo", "CoverSlot", "BioRwBox", "BioMask4Property", "RwVector2", "RwVector3", "RwVector4", "BioRwBox44" };

        public static bool isImmutableStruct(string structName)
        {
            return ImmutableStructs.Contains(structName);
        }

        #region struct default values
        private static byte[] CoverReferenceDefault = { 
            //SlotIdx
            0x78, 0x45, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Direction
            0x28, 0x1B, 0, 0, 0, 0, 0, 0, 0xB6, 0x29, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Guid
            0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0x17, 0x48, 0, 0, 0, 0, 0, 0, 0x10, 0, 0, 0, 0, 0, 0, 0, 0xC7, 0x26, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Actor
            0xF7, 0, 0, 0, 0, 0, 0, 0, 0x62, 0x34, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //None
            0x73, 0x33, 0, 0, 0, 0, 0, 0 };

        private static byte[] PlaneDefault = { 
            //X
            0x09, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Y
            0x0D, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //Z
            0x12, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //W
            0x04, 0x03, 0, 0, 0, 0, 0, 0, 0x15, 0x01, 0, 0, 0, 0, 0, 0, 0x04, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
            //None
            0xF0, 0x01, 0, 0, 0, 0, 0, 0 };
        #endregion

        public static void loadfromJSON()
        {
            string path = Application.StartupPath + "//exec//ME3ObjectInfo.json";

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { SequenceObjects, Classes, Structs, Enums });
                    SequenceObjects = blob.SequenceObjects;
                    Classes = blob.Classes;
                    Structs = blob.Structs;
                    Enums = blob.Enums;
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public static SequenceObjectInfo getSequenceObjectInfo(string objectName)
        {
            if (objectName.StartsWith("Default__"))
            {
                objectName = objectName.Substring(9);
            }
            if (SequenceObjects.ContainsKey(objectName))
            {
                if (SequenceObjects[objectName].inputLinks != null && SequenceObjects[objectName].inputLinks.Count > 0)
                {
                    return SequenceObjects[objectName];
                }
                else
                {
                    return getSequenceObjectInfo(Classes[objectName].baseClass);
                }
            }
            return null;
        }

        public static string getEnumTypefromProp(string className, string propName)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            return p?.reference;
        }

        public static List<string> getEnumValues(string enumName, bool includeNone = false)
        {
            if (Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>(Enums[enumName]);
                if (includeNone)
                {
                    values.Insert(0, "None");
                }
                return values;
            }
            return null;
        }

        public static ArrayType getArrayType(string className, string propName, IExportEntry export = null)
        {
            PropertyInfo p = getPropertyInfo(className, propName, false);
            if (p == null)
            {
                p = getPropertyInfo(className, propName, true);
            }
            if (p == null && export != null)
            {
                if (export.ClassName != "Class" && export.idxClass > 0)
                {
                    export = export.FileRef.Exports[export.idxClass - 1]; //make sure you get actual class
                }
                if (export.ClassName == "Class")
                {
                    ClassInfo currentInfo = generateClassInfo(export);
                    currentInfo.baseClass = export.ClassParent;
                    p = getPropertyInfo(className, propName, false, currentInfo);
                    if (p == null)
                    {
                        p = getPropertyInfo(className, propName, true, currentInfo);
                    }
                }
            }
            return getArrayType(p);
        }

        public static ArrayType getArrayType(PropertyInfo p)
        {
            if (p != null)
            {
                if (p.reference == "NameProperty")
                {
                    return ArrayType.Name;
                }
                else if (Enums.ContainsKey(p.reference))
                {
                    return ArrayType.Enum;
                }
                else if (p.reference == "BoolProperty")
                {
                    return ArrayType.Bool;
                }
                else if (p.reference == "ByteProperty")
                {
                    return ArrayType.Byte;
                }
                else if (p.reference == "StrProperty")
                {
                    return ArrayType.String;
                }
                else if (p.reference == "FloatProperty")
                {
                    return ArrayType.Float;
                }
                else if (p.reference == "IntProperty")
                {
                    return ArrayType.Int;
                }
                else if (Structs.ContainsKey(p.reference))
                {
                    return ArrayType.Struct;
                }
                else
                {
                    return ArrayType.Object;
                }
            }
            else
            {
                Debug.WriteLine("ME3 Array type lookup failed due to no info provided, defaulting to int");
                return ArrayType.Int;
            }
        }

        public static PropertyInfo getPropertyInfo(string className, string propName, bool inStruct = false, ClassInfo nonVanillaClassInfo = null, bool reSearch = true)
        {
            if (className.StartsWith("Default__"))
            {
                className = className.Substring(9);
            }
            Dictionary<string, ClassInfo> temp = inStruct ? Structs : Classes;
            ClassInfo info;
            bool infoExists = temp.TryGetValue(className, out info);
            if (!infoExists && nonVanillaClassInfo != null)
            {
                info = nonVanillaClassInfo;
                infoExists = true;
            }
            if (infoExists) //|| (temp = !inStruct ? Structs : Classes).ContainsKey(className))
            {
                //look in class properties
                if (info.properties.ContainsKey(propName))
                {
                    return info.properties[propName];
                }
                //look in structs
                else
                {
                    foreach (PropertyInfo p in info.properties.Values)
                    {
                        if ((p.type == PropertyType.StructProperty || p.type == PropertyType.ArrayProperty) && reSearch)
                        {
                            PropertyInfo val = getPropertyInfo(p.reference, propName, true, nonVanillaClassInfo, reSearch: true);
                            if (val != null)
                            {
                                return val;
                            }
                        }
                    }
                }
                //look in base class
                if (temp.ContainsKey(info.baseClass))
                {
                    PropertyInfo val = getPropertyInfo(info.baseClass, propName, inStruct, nonVanillaClassInfo, reSearch: true);
                    if (val != null)
                    {
                        return val;
                    }
                }
            }

            //if (reSearch)
            //{
            //    PropertyInfo reAttempt = getPropertyInfo(className, propName, !inStruct, nonVanillaClassInfo, reSearch: false);
            //    return reAttempt; //will be null if not found.
            //}
            return null;
        }

        public static byte[] getDefaultClassValue(ME3Package pcc, string className, bool fullProps = false)
        {
            if (Structs.ContainsKey(className))
            {
                bool immutable = UnrealObjectInfo.isImmutable(className, MEGame.ME3);
                ClassInfo info = Structs[className];
                try
                {
                    string filepath = (Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    using (ME3Package importPCC = MEPackageHandler.OpenME3Package(filepath))
                    {
                        byte[] buff;
                        //Plane and CoverReference inherit from other structs, meaning they don't have default values (who knows why)
                        //thus, I have hardcoded what those default values should be 
                        if (className == "Plane")
                        {
                            buff = PlaneDefault;
                        }
                        else if (className == "CoverReference")
                        {
                            buff = CoverReferenceDefault;
                        }
                        else
                        {
                            buff = importPCC.Exports[info.exportIndex].Data.Skip(0x24).ToArray();
                        }
                        List<PropertyReader.Property> Props = PropertyReader.ReadProp(importPCC, buff, 0);
                        MemoryStream m = new MemoryStream();
                        foreach (PropertyReader.Property p in Props)
                        {
                            string propName = importPCC.getNameEntry(p.Name);
                            //check if property is transient, if so, skip (neither of the structs that inherit have transient props)
                            if (info.properties.ContainsKey(propName) || propName == "None" || info.baseClass != "Class")
                            {
                                if (immutable && !fullProps)
                                {
                                    PropertyReader.ImportImmutableProperty(pcc, importPCC, p, className, m, true);
                                }
                                else
                                {
                                    PropertyReader.ImportProperty(pcc, importPCC, p, className, m, true);
                                }
                            }
                        }
                        return m.ToArray();
                    }
                }
                catch (Exception)
                {
                    return null;
                }
            }
            else if (Classes.ContainsKey(className))
            {
                ClassInfo info = Structs[className];
                try
                {
                    string filepath = (Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    using (ME3Package importPCC = MEPackageHandler.OpenME3Package(filepath))
                    {
                        IExportEntry entry = pcc.Exports[info.exportIndex + 1];
                        List<PropertyReader.Property> Props = PropertyReader.getPropList(entry);
                        MemoryStream m = new MemoryStream(entry.DataSize - 4);
                        foreach (PropertyReader.Property p in Props)
                        {
                            if (!info.properties.ContainsKey(importPCC.getNameEntry(p.Name)))
                            {
                                //property is transient
                                continue;
                            }
                            PropertyReader.ImportProperty(pcc, importPCC, p, className, m);
                        }
                        return m.ToArray();
                    }
                }
                catch (Exception)
                {
                    return null;
                }

            }
            return null;
        }

        public static PropertyCollection getDefaultStructValue(string className, bool stripTransients)
        {
            if (Structs.ContainsKey(className))
            {
                bool immutable = UnrealObjectInfo.isImmutable(className, MEGame.ME3);
                ClassInfo info = Structs[className];
                try
                {
                    string filepath = (Path.Combine(ME3Directory.gamePath, @"BIOGame\" + info.pccPath));
                    if (File.Exists(info.pccPath))
                    {
                        filepath = info.pccPath; //Used for dynamic lookup
                    }
                    using (ME3Package importPCC = MEPackageHandler.OpenME3Package(filepath))
                    {
                        byte[] buff;
                        //Plane and CoverReference inherit from other structs, meaning they don't have default values (who knows why)
                        //thus, I have hardcoded what those default values should be 
                        if (className == "Plane")
                        {
                            buff = PlaneDefault;
                        }
                        else if (className == "CoverReference")
                        {
                            buff = CoverReferenceDefault;
                        }
                        else
                        {
                            var exportToRead = importPCC.Exports[info.exportIndex];
                            buff = exportToRead.Data.Skip(0x24).ToArray();
                        }
                        PropertyCollection props = PropertyCollection.ReadProps(importPCC, new MemoryStream(buff), className);
                        if (stripTransients)
                        {
                            List<UProperty> toRemove = new List<UProperty>();
                            foreach (var prop in props)
                            {
                                //remove transient props
                                if (info.properties.TryGetValue(prop.Name, out PropertyInfo propInfo))
                                {
                                    if (propInfo.transient)
                                    {
                                        toRemove.Add(prop);
                                    }
                                }
                                //if (!info.properties.ContainsKey(prop.Name) && info.baseClass == "Class")
                                //{
                                //    toRemove.Add(prop);
                                //}
                            }
                            foreach (var prop in toRemove)
                            {
                                Debug.WriteLine("ME3: Get Default Struct value (" + className + ") - removing transient prop: " + prop.Name);
                                props.Remove(prop);
                            }
                        }
                        return props;
                    }
                }
                catch
                {
                    return null;
                }
            }
            return null;
        }

        public static bool inheritsFrom(ME3ExportEntry entry, string baseClass)
        {
            string className = entry.ClassName;
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }

        public static bool inheritsFrom(UDKExportEntry entry, string baseClass)
        {
            string className = entry.ClassName;
            while (Classes.ContainsKey(className))
            {
                if (className == baseClass)
                {
                    return true;
                }
                className = Classes[className].baseClass;
            }
            return false;
        }

        #region Generating
        //call this method to regenerate ME3ObjectInfo.json
        //Takes a long time (~5 minutes maybe?). Application will be completely unresponsive during that time.
        public static void generateInfo()
        {
            Classes = new Dictionary<string, ClassInfo>();
            Structs = new Dictionary<string, ClassInfo>();
            Enums = new Dictionary<string, List<string>>();

            string path = ME3Directory.gamePath;
            string[] files = Directory.GetFiles(Path.Combine(path, "BIOGame"), "*.pcc", SearchOption.AllDirectories);
            string objectName;
            int length = files.Length;
            for (int i = 0; i < length; i++)
            {
                if (files[i].ToLower().EndsWith(".pcc"))
                {
                    using (ME3Package pcc = MEPackageHandler.OpenME3Package(files[i]))
                    {
                        IReadOnlyList<IExportEntry> Exports = pcc.Exports;
                        IExportEntry exportEntry;
                        for (int j = 0; j < Exports.Count; j++)
                        {
                            exportEntry = Exports[j];
                            if (exportEntry.ClassName == "Enum")
                            {
                                generateEnumValues(j, pcc);
                            }
                            else if (exportEntry.ClassName == "Class")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!Classes.ContainsKey(objectName))
                                {
                                    Classes.Add(objectName, generateClassInfo(j, pcc));
                                }
                                if ((objectName.Contains("SeqAct") || objectName.Contains("SeqCond") || objectName.Contains("SequenceLatentAction") ||
                                    objectName == "SequenceOp" || objectName == "SequenceAction" || objectName == "SequenceCondition") && !SequenceObjects.ContainsKey(objectName))
                                {
                                    SequenceObjects.Add(objectName, generateSequenceObjectInfo(j, pcc));
                                }
                            }
                            else if (exportEntry.ClassName == "ScriptStruct")
                            {
                                objectName = exportEntry.ObjectName;
                                if (!Structs.ContainsKey(objectName))
                                {
                                    Structs.Add(objectName, generateClassInfo(j, pcc));
                                }
                            }
                        }
                    }
                }
                // System.Diagnostics.Debug.WriteLine($"{i} of {length} processed");
            }


            #region CUSTOM ADDITIONS
            //Custom additions
            //Custom additions are tweaks and additional classes either not automatically able to be determined
            //or by new classes designed in the modding scene that must be present in order for parsing to work properly

            //Kinkojiro - New Class - SFXSeqAct_AttachToSocket
            ClassInfo ci = new ClassInfo()
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0
            };
            ci.properties["PSC2Component"] = new PropertyInfo()
            {
                type = PropertyType.ObjectProperty,
                reference = "ParticleSystemComponent"
            };
            ci.properties["PSC1Component"] = new PropertyInfo()
            {
                type = PropertyType.ObjectProperty,
                reference = "ParticleSystemComponent"
            };
            ci.properties["SkMeshComponent"] = new PropertyInfo()
            {
                type = PropertyType.ObjectProperty,
                reference = "SkeletalMeshComponent"
            };
            ci.properties["TargetPawn"] = new PropertyInfo()
            {
                type=PropertyType.ObjectProperty,
                reference = "Actor"
            };
            ci.properties["AttachSocketName"] = new PropertyInfo()
            {
                type = PropertyType.NameProperty
            };
            Classes["SFXSeqAct_AttachToSocket"] = ci;

            //Kinkojiro - New Class - BioSeqAct_ShowMedals
            //Sequence object for showing the medals UI
            ci = new ClassInfo()
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0
            };
            ci.properties["bFromMainMenu"] = new PropertyInfo()
            {
                type = PropertyType.BoolProperty,
            };
            ci.properties["m_oGuiReferenced"] = new PropertyInfo()
            {
                type = PropertyType.ObjectProperty,
                reference = "GFxMovieInfo"
            };
            Classes["BioSeqAct_ShowMedals"] = ci;

            //Kinkojiro - New Class - SFXSeqAct_SetFaceFX
            ci = new ClassInfo()
            {
                baseClass = "SequenceAction",
                pccPath = "ME3Explorer_CustomNativeAdditions",
                exportIndex = 0
            };
            ci.properties["m_aoTargets"] = new PropertyInfo()
            {
                type = PropertyType.ArrayProperty,
                reference = "Actor"
            };
            ci.properties["m_pDefaultFaceFXAsset"] = new PropertyInfo()
            {
                type = PropertyType.ObjectProperty,
                reference = "FaceFXAsset"
            };
            Classes["SFXSeqAct_SetFaceFX"] = ci;

            #endregion

            File.WriteAllText(Application.StartupPath + "//exec//ME3ObjectInfo.json",
                JsonConvert.SerializeObject(new { SequenceObjects = SequenceObjects, Classes = Classes, Structs = Structs, Enums = Enums }, Formatting.Indented));
            MessageBox.Show("Done");
        }

        private static SequenceObjectInfo generateSequenceObjectInfo(int i, ME3Package pcc)
        {
            SequenceObjectInfo info = new SequenceObjectInfo();
            PropertyReader.Property inputLinks = PropertyReader.getPropOrNull(pcc.Exports[i + 1], "InputLinks");
            if (inputLinks != null)
            {
                int pos = 28;
                byte[] global = inputLinks.raw;
                int count = BitConverter.ToInt32(global, 24);
                for (int j = 0; j < count; j++)
                {
                    List<PropertyReader.Property> p2 = PropertyReader.ReadProp(pcc, global, pos);

                    info.inputLinks.Add(p2[0].Value.StringValue);
                    for (int k = 0; k < p2.Count(); k++)
                        pos += p2[k].raw.Length;
                }
            }
            return info;
        }

        private static ClassInfo generateClassInfo(int index, ME3Package pcc)
        {
            ClassInfo info = new ClassInfo
            {
                baseClass = pcc.Exports[index].ClassParent,
                exportIndex = index
            };
            if (pcc.FileName.Contains("BIOGame"))
            {
                info.pccPath = new string(pcc.FileName.Skip(pcc.FileName.LastIndexOf("BIOGame") + 8).ToArray());
            }
            else
            {
                info.pccPath = pcc.FileName; //used for dynamic resolution of files outside the game directory.
            }

            foreach (IExportEntry entry in pcc.Exports)
            {
                if (entry.idxLink - 1 == index && entry.ClassName != "ScriptStruct" && entry.ClassName != "Enum"
                    && entry.ClassName != "Function" && entry.ClassName != "Const" && entry.ClassName != "State")
                {
                    //Skip if property is transient (only used during execution, will never be in game files)
                    if (/*(BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) == 0 &&*/ !info.properties.ContainsKey(entry.ObjectName))
                    {
                        PropertyInfo p = getProperty(pcc, entry);
                        if (p != null)
                        {
                            info.properties.Add(entry.ObjectName, p);
                        }
                    }
                    //else
                    //{
                    //    //Debug.WriteLine("Skipping property due to flag: " + entry.ObjectName);
                    //}
                }
            }
            return info;
        }

        private static void generateEnumValues(int index, ME3Package pcc)
        {
            string enumName = pcc.Exports[index].ObjectName;
            if (!Enums.ContainsKey(enumName))
            {
                List<string> values = new List<string>();
                byte[] buff = pcc.Exports[index].Data;
                //subtract 1 so that we don't get the MAX value, which is an implementation detail
                int count = BitConverter.ToInt32(buff, 20) - 1;
                for (int i = 0; i < count; i++)
                {
                    values.Add(pcc.Names[BitConverter.ToInt32(buff, 24 + i * 8)]);
                }
                Enums.Add(enumName, values);
            }
        }

        private static PropertyInfo getProperty(ME3Package pcc, IExportEntry entry)
        {
            PropertyInfo p = new PropertyInfo();
            switch (entry.ClassName)
            {
                case "IntProperty":
                    p.type = PropertyType.IntProperty;
                    break;
                case "StringRefProperty":
                    p.type = PropertyType.StringRefProperty;
                    break;
                case "FloatProperty":
                    p.type = PropertyType.FloatProperty;
                    break;
                case "BoolProperty":
                    p.type = PropertyType.BoolProperty;
                    break;
                case "StrProperty":
                    p.type = PropertyType.StrProperty;
                    break;
                case "NameProperty":
                    p.type = PropertyType.NameProperty;
                    break;
                case "DelegateProperty":
                    p.type = PropertyType.DelegateProperty;
                    break;
                case "ObjectProperty":
                case "ClassProperty":
                case "ComponentProperty":
                    p.type = PropertyType.ObjectProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "StructProperty":
                    p.type = PropertyType.StructProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "BioMask4Property":
                case "ByteProperty":
                    p.type = PropertyType.ByteProperty;
                    p.reference = pcc.getObjectName(BitConverter.ToInt32(entry.Data, entry.Data.Length - 4));
                    break;
                case "ArrayProperty":
                    p.type = PropertyType.ArrayProperty;
                    PropertyInfo arrayTypeProp = getProperty(pcc, pcc.Exports[BitConverter.ToInt32(entry.Data, 44) - 1]);
                    if (arrayTypeProp != null)
                    {
                        switch (arrayTypeProp.type)
                        {
                            case PropertyType.ObjectProperty:
                            case PropertyType.StructProperty:
                            case PropertyType.ArrayProperty:
                                p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.ByteProperty:
                                if (arrayTypeProp.reference == "Class")
                                    p.reference = arrayTypeProp.type.ToString();
                                else
                                    p.reference = arrayTypeProp.reference;
                                break;
                            case PropertyType.IntProperty:
                            case PropertyType.FloatProperty:
                            case PropertyType.NameProperty:
                            case PropertyType.BoolProperty:
                            case PropertyType.StrProperty:
                            case PropertyType.StringRefProperty:
                            case PropertyType.DelegateProperty:
                                p.reference = arrayTypeProp.type.ToString();
                                break;
                            case PropertyType.None:
                            case PropertyType.Unknown:
                            default:
                                System.Diagnostics.Debugger.Break();
                                p = null;
                                break;
                        }
                    }
                    else
                    {
                        p = null;
                    }
                    break;
                case "InterfaceProperty":
                default:
                    p = null;
                    break;
            }
            if (p != null && (BitConverter.ToUInt64(entry.Data, 24) & 0x0000000000002000) != 0)
            {
                //Transient
                p.transient = true;
            }
            return p;
        }

        internal static ClassInfo generateClassInfo(IExportEntry export)
        {
            return generateClassInfo(export.Index, export.FileRef as ME3Package);
        }
        #endregion
    }
}
