using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;

namespace LegendaryExplorerCore.Unreal.ObjectInfo
{
    public static class GlobalUnrealObjectInfo
    {
        // LEX: Don't change ME3ExplorerTrashPackage, CustomNativeAdditions names. No real point as it will cause some internal issues if package is opened on older toolset
        public const string Me3ExplorerCustomNativeAdditionsName = "ME3Explorer_CustomNativeAdditions";

        public static List<ClassInfo> GetNonAbstractDerivedClassesOf(string baseClassName, MEGame game) =>
            GetClasses(game).Values.Where(info => info.ClassName != baseClassName && !info.isAbstract && IsA(info.ClassName, baseClassName, game)).ToList();

        public static bool IsImmutable(string structType, MEGame game) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.ME2 => ME2UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.ME3 => ME3UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.UDK => ME3UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.LE1 => LE1UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.LE2 => LE2UnrealObjectInfo.IsImmutableStruct(structType),
                MEGame.LE3 => LE3UnrealObjectInfo.IsImmutableStruct(structType),
                _ => false,
            };

        public static bool IsA(this ClassInfo info, string baseClass, MEGame game, Dictionary<string, ClassInfo> customClassInfos = null) => IsA(info.ClassName, baseClass, game, customClassInfos);
        public static bool IsA(this IEntry entry, string baseClass, Dictionary<string, ClassInfo> customClassInfos = null) => IsA(entry.ClassName, baseClass, entry.FileRef.Game, customClassInfos);
        public static bool IsA(string className, string baseClass, MEGame game, Dictionary<string, ClassInfo> customClassInfos = null) =>
            className == baseClass || game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.ME2 => ME2UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.ME3 => ME3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.UDK => ME3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.LE1 => LE1UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.LE2 => LE2UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.LE3 => LE3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                _ => false
            };

        public static bool InheritsFrom(this IEntry entry, string baseClass, Dictionary<string, ClassInfo> customClassInfos = null) => InheritsFrom(entry.ObjectName.Name, baseClass, entry.FileRef.Game, customClassInfos, (entry as ExportEntry)?.SuperClassName);
        public static bool InheritsFrom(string className, string baseClass, MEGame game, Dictionary<string, ClassInfo> customClassInfos = null, string knownSuperClass = null) =>
            className == baseClass || game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                MEGame.ME2 => ME2UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                MEGame.ME3 => ME3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                MEGame.UDK => ME3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos),
                MEGame.LE1 => LE1UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                MEGame.LE2 => LE2UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                MEGame.LE3 => LE3UnrealObjectInfo.InheritsFrom(className, baseClass, customClassInfos, knownSuperClass),
                _ => false
            };

        /// <summary>
        /// Checks if the full path name of this entry is known to be defined in native only
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static bool IsAKnownNativeClass(this IEntry entry) => IsAKnownNativeClass(entry.InstancedFullPath, entry.Game);

        /// <summary>
        /// Checks if the full path name is known to be defined in native only
        /// </summary>
        /// <param name="entry"></param>
        /// <returns></returns>
        public static bool IsAKnownNativeClass(string fullPathName, MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1: return ME1UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.ME2: return ME2UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.ME3: return ME3UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.UDK: return ME3UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.LE1: return LE1UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.LE2: return LE2UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                case MEGame.LE3: return LE3UnrealObjectInfo.IsAKnownNativeClass(fullPathName);
                default: return false;
            };
        }

        public static SequenceObjectInfo getSequenceObjectInfo(MEGame game, string className) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.ME2 => ME2UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.ME3 => ME3UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.UDK => ME3UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.LE1 => LE1UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.LE2 => LE2UnrealObjectInfo.getSequenceObjectInfo(className),
                MEGame.LE3 => LE3UnrealObjectInfo.getSequenceObjectInfo(className),
                _ => null
            };

        public static string GetEnumType(MEGame game, NameReference propName, string typeName, ClassInfo nonVanillaClassInfo = null) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.ME2 => ME2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.ME3 => ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo),
                MEGame.UDK => ME3UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo) ?? UDKUnrealObjectInfo.getEnumTypefromProp(typeName, propName),
                MEGame.LE1 => LE1UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.LE2 => LE2UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo: nonVanillaClassInfo),
                MEGame.LE3 => LE3UnrealObjectInfo.getEnumTypefromProp(typeName, propName, nonVanillaClassInfo),
                _ => null
            };

        public static List<NameReference> GetEnumValues(MEGame game, string enumName, bool includeNone = false) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.ME2 => ME2UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.ME3 => ME3UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.UDK => ME3UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.LE1 => LE1UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.LE2 => LE2UnrealObjectInfo.getEnumValues(enumName, includeNone),
                MEGame.LE3 => LE3UnrealObjectInfo.getEnumValues(enumName, includeNone),
                _ => null
            };

        public static bool IsValidEnum(MEGame game, string enumName) =>
            game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.ME2 => ME2UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.ME3 => ME3UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.UDK => ME3UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.LE1 => LE1UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.LE2 => LE2UnrealObjectInfo.Enums.ContainsKey(enumName),
                MEGame.LE3 => LE3UnrealObjectInfo.Enums.ContainsKey(enumName),
                _ => false
            };


        /// <summary>
        /// Recursively gets class defaults, traveling up inheritiance chain, but stopping at <paramref name="notIncludingClass"></paramref>
        /// </summary>
        /// <param name="game"></param>
        /// <param name="className"></param>
        /// <param name="notIncludingClass"></param>
        /// <returns></returns>
        public static PropertyCollection getClassDefaults(MEGame game, string className, string notIncludingClass = null)
        {
            Dictionary<string, ClassInfo> classes = GetClasses(game);
            if (classes.TryGetValue(className, out ClassInfo info))
            {
                try
                {
                    PropertyCollection props = new();
                    while (info != null && info.ClassName != notIncludingClass)
                    {
                        Stream loadStream = null;
                        string filepathTL = Path.Combine(MEDirectories.GetBioGamePath(game), info.pccPath);
                        if (File.Exists(info.pccPath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(info.pccPath);
                        }
                        else if (info.pccPath == Me3ExplorerCustomNativeAdditionsName)
                        {
                            loadStream = LegendaryExplorerCoreUtilities.GetCustomAppResourceStream(game);
                        }
                        else if (File.Exists(filepathTL))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(filepathTL);
                        }

                        if (game == MEGame.ME1 && !File.Exists(filepathTL))
                        {
                            filepathTL = Path.Combine(ME1Directory.DefaultGamePath, info.pccPath); //for files from ME1 DLC
                            if (File.Exists(filepathTL))
                            {
                                loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(filepathTL);
                            }
                        }
                        if (loadStream != null)
                        {
                            using IMEPackage importPCC = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                            ExportEntry classExport = importPCC.GetUExport(info.exportIndex);
                            UClass classBin = ObjectBinary.From<UClass>(classExport);
                            ExportEntry defaults = importPCC.GetUExport(classBin.Defaults);

                            foreach (var prop in defaults.GetProperties())
                            {
                                if (!props.ContainsNamedProp(prop.Name))
                                {
                                    props.Add(prop);
                                }
                            }
                        }

                        classes.TryGetValue(info.baseClass, out info);
                    }
                    return props;
                }
                catch
                {
                    return null;
                }
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
        public static ArrayType GetArrayType(MEGame game, NameReference propName, string className, IEntry parsingEntry = null)
        {
            switch (game)
            {
                case MEGame.ME1 when parsingEntry == null || parsingEntry.FileRef.Platform != MEPackage.GamePlatform.PS3:
                    return ME1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
                case MEGame.ME2 when parsingEntry == null || parsingEntry.FileRef.Platform != MEPackage.GamePlatform.PS3:
                    var res2 = ME2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res2 == ArrayType.Int && ME2UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        ME2UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                        Debug.WriteLine($"[ME2] Array type lookup failed for {propName.Instanced} in class {className} in export {parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex)} in {parsingEntry.FileRef.FilePath}");
                    }
#endif
                    return res2;
                case MEGame.ME1 when parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME2 when parsingEntry.FileRef.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME3:
                case MEGame.UDK:
                    var res = ME3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
#if DEBUG
                    //For debugging only!
                    if (res == ArrayType.Int && ME3UnrealObjectInfo.ArrayTypeLookupJustFailed)
                    {
                        if (game == MEGame.UDK)
                        {
                            var ures = UDKUnrealObjectInfo.getArrayType(className, propName: propName, export: parsingEntry as ExportEntry);
                            if (ures == ArrayType.Int && UDKUnrealObjectInfo.ArrayTypeLookupJustFailed)
                            {
                                Debug.WriteLine($"[UDK] Array type lookup failed for {propName.Instanced} in class {className} in export {parsingEntry.FileRef.GetEntryString(parsingEntry.UIndex)} in {parsingEntry.FileRef.FilePath}");
                                UDKUnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                            }
                            else
                            {
                                return ures;
                            }
                        }
                        Debug.WriteLine($"[ME3] Array type lookup failed for {propName.Instanced} in class {className} in export {parsingEntry?.FileRef.GetEntryString(parsingEntry.UIndex)} in {parsingEntry?.FileRef.FilePath}");
                        ME3UnrealObjectInfo.ArrayTypeLookupJustFailed = false;
                    }
#endif
                    return res;
                case MEGame.LE1:
                    return LE1UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
                case MEGame.LE2:
                    return LE2UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
                case MEGame.LE3:
                    return LE3UnrealObjectInfo.getArrayType(className, propName, export: parsingEntry as ExportEntry);
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
        public static PropertyInfo GetPropertyInfo(MEGame game, NameReference propname, string containingClassOrStructName, ClassInfo nonVanillaClassInfo = null, ExportEntry containingExport = null, PackageCache packageCache = null)
        {
            bool inStruct = false;
            PropertyInfo p = null;
            switch (game)
            {
                case MEGame.ME1 when containingExport == null || containingExport.FileRef.Platform != MEPackage.GamePlatform.PS3:
                    p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    break;
                case MEGame.ME2 when containingExport == null || containingExport.FileRef.Platform != MEPackage.GamePlatform.PS3:
                    p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    break;
                case MEGame.ME1 when containingExport.FileRef.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME2 when containingExport.FileRef.Platform == MEPackage.GamePlatform.PS3:
                case MEGame.ME3:
                case MEGame.UDK:
                    p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    if (p == null && game == MEGame.UDK)
                    {
                        p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    }
                    break;
                case MEGame.LE1:
                    p = LE1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    break;
                case MEGame.LE2:
                    p = LE2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    break;
                case MEGame.LE3:
                    p = LE3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo, containingExport: containingExport);
                    break;
            }
            if (p == null)
            {
                inStruct = true;
                switch (game)
                {
                    case MEGame.ME1 when containingExport == null || containingExport.FileRef.Platform != MEPackage.GamePlatform.PS3:
                        p = ME1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME2 when containingExport == null || containingExport.FileRef.Platform != MEPackage.GamePlatform.PS3:
                        p = ME2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.ME1 when containingExport.FileRef.Platform == MEPackage.GamePlatform.PS3:
                    case MEGame.ME2 when containingExport.FileRef.Platform == MEPackage.GamePlatform.PS3:
                    case MEGame.ME3:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.UDK:
                        p = ME3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        if (p == null)
                        {
                            p = UDKUnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct, nonVanillaClassInfo);
                        }
                        break;
                    case MEGame.LE1:
                        p = LE1UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.LE2:
                        p = LE2UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
                        break;
                    case MEGame.LE3:
                        p = LE3UnrealObjectInfo.getPropertyInfo(containingClassOrStructName, propname, inStruct);
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
        /// <param name="packageCache"></param>
        /// <param name="shouldReturnClone">Return a deep copy of the struct</param>
        /// <returns></returns>
        public static PropertyCollection getDefaultStructValue(MEGame game, string typeName, bool stripTransients, PackageCache packageCache = null, bool shouldReturnClone = true)
        {
            PropertyCollection props = game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.ME2 => ME2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.ME3 => ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.UDK => ME3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.LE1 => LE1UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.LE2 => LE2UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                MEGame.LE3 => LE3UnrealObjectInfo.getDefaultStructValue(typeName, stripTransients, packageCache),
                _ => null
            };
            if (shouldReturnClone && props is not null)
            {
                return props.DeepClone();
            }
            return props;
        }

        public static OrderedMultiValueDictionary<NameReference, PropertyInfo> GetAllProperties(MEGame game, string typeName)
        {
            var props = new OrderedMultiValueDictionary<NameReference, PropertyInfo>();

            ClassInfo info = GetClassOrStructInfo(game, typeName);
            while (info != null)
            {
                props.AddRange(info.properties);
                info = GetClassOrStructInfo(game, info.baseClass);
            }

            return props;
        }

        public static ClassInfo GetClassOrStructInfo(MEGame game, string typeName)
        {
            ClassInfo result = null;
            switch (game)
            {
                case MEGame.ME1:
                    _ = ME1UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME1UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.ME2:
                    _ = ME2UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME2UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.ME3:
                    _ = ME3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.LE1:
                    _ = LE1UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || LE1UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.LE2:
                    _ = LE2UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || LE2UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.LE3:
                    _ = LE3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || LE3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
                case MEGame.UDK:
                    _ = UDKUnrealObjectInfo.Classes.TryGetValue(typeName, out result) || UDKUnrealObjectInfo.Structs.TryGetValue(typeName, out result)
                     || ME3UnrealObjectInfo.Classes.TryGetValue(typeName, out result) || ME3UnrealObjectInfo.Structs.TryGetValue(typeName, out result);
                    break;
            }

            return result;
        }

        public static Dictionary<string, ClassInfo> GetClasses(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Classes;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Classes;
                case MEGame.UDK:
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Classes;
                case MEGame.LE1:
                    return LE1UnrealObjectInfo.Classes;
                case MEGame.LE2:
                    return LE2UnrealObjectInfo.Classes;
                case MEGame.LE3:
                    return LE3UnrealObjectInfo.Classes;
                default:
                    return null;
            }
        }

        public static Dictionary<string, ClassInfo> GetStructs(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Structs;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Structs;
                case MEGame.UDK:
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Structs;
                case MEGame.LE1:
                    return LE1UnrealObjectInfo.Structs;
                case MEGame.LE2:
                    return LE2UnrealObjectInfo.Structs;
                case MEGame.LE3:
                    return LE3UnrealObjectInfo.Structs;

                default:
                    return null;
            }
        }

        public static Dictionary<string, List<NameReference>> GetEnums(MEGame game)
        {
            switch (game)
            {
                case MEGame.ME1:
                    return ME1UnrealObjectInfo.Enums;
                case MEGame.ME2:
                    return ME2UnrealObjectInfo.Enums;
                case MEGame.UDK:
                case MEGame.ME3:
                    return ME3UnrealObjectInfo.Enums;
                case MEGame.LE1:
                    return LE1UnrealObjectInfo.Enums;
                case MEGame.LE2:
                    return LE2UnrealObjectInfo.Enums;
                case MEGame.LE3:
                    return LE3UnrealObjectInfo.Enums;
                default:
                    return null;
            }
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false)
        {
            if (export.ClassName != "ScriptStruct" && !export.IsClass && export.Class is ExportEntry classExport)
            {
                export = classExport;
            }

            return export.FileRef.Game switch
            {
                MEGame.ME1 when export.FileRef.Platform != MEPackage.GamePlatform.PS3 => ME1UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.ME2 when export.FileRef.Platform != MEPackage.GamePlatform.PS3 => ME2UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.ME1 when export.FileRef.Platform == MEPackage.GamePlatform.PS3 => ME3UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.ME2 when export.FileRef.Platform == MEPackage.GamePlatform.PS3 => ME3UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.ME3 => ME3UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.UDK => ME3UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.LE1 => LE1UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.LE2 => LE2UnrealObjectInfo.generateClassInfo(export, isStruct),
                MEGame.LE3 => LE3UnrealObjectInfo.generateClassInfo(export, isStruct),
                _ => null
            };
        }

        public static Property getDefaultProperty(MEGame game, NameReference propName, PropertyInfo propInfo, PackageCache packageCache, bool stripTransients = true, bool isImmutable = false)
        {
            return game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.ME2 => ME2UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.ME3 => ME3UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.UDK => ME3UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.LE1 => LE1UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.LE2 => LE2UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                MEGame.LE3 => LE3UnrealObjectInfo.getDefaultProperty(propName, propInfo, packageCache, stripTransients, isImmutable),
                _ => null
            };
        }

        public static List<string> GetSequenceObjectInfoInputLinks(MEGame game, string exportClassName)
        {
            return game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.ME2 => ME2UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.ME3 => ME3UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.UDK => ME3UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.LE1 => LE1UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.LE2 => LE2UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                MEGame.LE3 => LE3UnrealObjectInfo.getSequenceObjectInfoInputLinks(exportClassName),
                _ => null
            };
        }
    }
}
