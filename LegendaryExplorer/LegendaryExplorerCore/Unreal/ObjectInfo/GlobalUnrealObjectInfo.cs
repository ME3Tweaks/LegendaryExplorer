﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
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


        // do not remove as other projects outside of LEX use this method
        /// <summary>
        /// Tests if this entry inherits from another class. This should only be used on objects that have a class of 'Class'.
        /// </summary>
        /// <param name="entry"></param>
        /// <param name="baseClass"></param>
        /// <param name="customClassInfos"></param>
        /// <returns></returns>
        public static bool InheritsFrom(this IEntry entry, string baseClass, Dictionary<string, ClassInfo> customClassInfos = null) => IsA(entry.ObjectName.Name, baseClass, entry.FileRef.Game, customClassInfos, (entry as ExportEntry)?.SuperClassName);
        public static bool IsA(this ClassInfo info, string baseClass, MEGame game, Dictionary<string, ClassInfo> customClassInfos = null) => IsA(info.ClassName, baseClass, game, customClassInfos);
        public static bool IsA(this IEntry entry, string baseClass, Dictionary<string, ClassInfo> customClassInfos = null) => IsA(entry.ClassName, baseClass, entry.Game, customClassInfos);
        public static bool IsA(string className, string baseClass, MEGame game, Dictionary<string, ClassInfo> customClassInfos = null, string knownSuperClass = null)
        {
            if (className == baseClass) return true;
            if (baseClass == @"Object") return true; //Everything inherits from Object
            if (knownSuperClass != null && baseClass == knownSuperClass) return true; // We already know it's a direct descendant
            var classes = game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Classes,
                MEGame.ME2 => ME2UnrealObjectInfo.Classes,
                MEGame.ME3 => ME3UnrealObjectInfo.Classes,
                MEGame.UDK => ME3UnrealObjectInfo.Classes,
                MEGame.LE1 => LE1UnrealObjectInfo.Classes,
                MEGame.LE2 => LE2UnrealObjectInfo.Classes,
                MEGame.LE3 => LE3UnrealObjectInfo.Classes,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
            };
            while (true)
            {
                if (className == baseClass)
                {
                    return true;
                }

                if (customClassInfos != null && customClassInfos.TryGetValue(className, out ClassInfo info))
                {
                    className = info.baseClass;
                }
                else if (classes.TryGetValue(className, out info))
                {
                    className = info.baseClass;
                }
                else if (knownSuperClass != null && classes.TryGetValue(knownSuperClass, out info))
                {
                    // We don't have this class in DB but we have super class (e.g. this is custom class without custom class info generated).
                    // We will just ignore this class and jump to our known super class
                    className = info.baseClass;
                    knownSuperClass = null; // Don't use it again
                }
                else
                {
                    break;
                }
                //if baseClass were Object, this method would have already returned.
                //That we're here means we're at the root of the hierarchy, no need to TryGet Object's ClassInfo
                if (className == "Object")
                {
                    break;
                }
            }
            return false;
        }

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
            if (IsAKnownNativeClassGlobally(fullPathName)) return true;
            switch (game)
            {
                case MEGame.ME1: return ME1UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.ME2: return ME2UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.ME3: return ME3UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.UDK: return ME3UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.LE1: return LE1UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.LE2: return LE2UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                case MEGame.LE3: return LE3UnrealObjectInfo.IsAKnownGameSpecificNativeClass(fullPathName);
                default: return false;
            };
        }

        /// <summary>
        /// List of all known classes that are only defined in native code.
        /// These are not able to be handled for things like InheritsFrom as they are not in the property info database.
        /// </summary>
        public static readonly string[] KnownGlobalNativeClasses =
        {
            // We should verify these don't exist by fetching the path
            // of the objects out of an ObjectDB. If the result is null
            // then no object of that name exists.

            @"Engine.CodecMovieBink",
            @"Engine.Level",
            @"Engine.LightMapTexture2D",
            @"Engine.Model",
            @"Engine.Polys",
            @"Engine.ShadowMap1D",
            @"Engine.StaticMesh",
            @"Engine.World",
            @"Engine.ShaderCache",
            @"Core.ObjectProperty",
            @"Core.Function",
            @"Core.ClassProperty",
            @"Core.IntProperty",
            @"Core.Class",
            @"Core.BoolProperty",
            @"Core.FloatProperty",
            @"Core.ArrayProperty",
            @"Core.DelegateProperty",
            @"Core.StructProperty",
            @"Core.ScriptStruct",
            @"Core.StringRefProperty",
            @"Core.StrProperty",
            @"Core.NameProperty",
            @"Core.ByteProperty",
            @"Core.Enum",
            @"Core.Const",
            @"Core.ComponentProperty",
            @"Core.InterfaceProperty",
            @"Core.MapProperty",
            @"Core.Property",
            @"Core.State",
        };

        /// <summary>
        /// If this is a known native class that exists natively only across all supported games
        /// </summary>
        /// <param name="fullPathName">The path to check</param>
        /// <returns>True if in the list, false otherwise</returns>
        public static bool IsAKnownNativeClassGlobally(string fullPathName)
        {
            return KnownGlobalNativeClasses.Contains(fullPathName, StringComparer.CurrentCultureIgnoreCase);
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

        public static bool IsValidEnum(MEGame game, string enumName) => GetEnums(game)?.ContainsKey(enumName) ?? false;


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
                            using IMEPackage importPcc = MEPackageHandler.OpenMEPackageFromStream(loadStream);
                            ExportEntry classExport = importPcc.GetUExport(info.exportIndex);
                            var classBin = ObjectBinary.From<UClass>(classExport);
                            ExportEntry defaults = importPcc.GetUExport(classBin.Defaults);

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
        /// <param name="packageCache"></param>
        /// <returns></returns>
        public static ArrayType GetArrayType(MEGame game, NameReference propName, string className, IEntry parsingEntry = null, PackageCache packageCache = null)
        {
            var export = parsingEntry as ExportEntry;
            PropertyInfo p = GetPropertyInfo(game, propName, className, containingExport: export, packageCache: packageCache);
            if (p is null && export is not null)
            {
                if (!export.IsClass && export.Class is ExportEntry classExport)
                {
                    export = classExport;
                }
                if (export.IsClass)
                {
                    ClassInfo currentInfo = generateClassInfo(export, packageCache: packageCache);
                    p = GetPropertyInfo(game, propName, className, currentInfo, export, packageCache);
                }
            }
            if (p is null)
            {
                Debug.WriteLine($"[{game}] Array type lookup failed for {propName.Instanced} in class {className} in export {parsingEntry?.FileRef.GetEntryString(parsingEntry.UIndex)} in {parsingEntry?.FileRef.FilePath}");
            }
            return GetArrayType(game, p);
        }

        internal static ArrayType GetArrayType(MEGame game, PropertyInfo p)
        {
            if (p is not null)
            {
                switch (p.Reference)
                {
                    case "NameProperty":
                        return ArrayType.Name;
                    case "BoolProperty":
                        return ArrayType.Bool;
                    case "ByteProperty":
                        return ArrayType.Byte;
                    case "StrProperty":
                        return ArrayType.String;
                    case "FloatProperty":
                        return ArrayType.Float;
                    case "IntProperty":
                        return ArrayType.Int;
                    case "StringRefProperty":
                        return ArrayType.StringRef;
                }

                if (GetEnums(game).ContainsKey(p.Reference))
                {
                    return ArrayType.Enum;
                }

                if (GetStructs(game).ContainsKey(p.Reference))
                {
                    return ArrayType.Struct;
                }

                return ArrayType.Object;
            }
            Debug.WriteLine("Array type lookup failed due to no info provided, defaulting to int");
            return LegendaryExplorerCoreLibSettings.Instance.ParseUnknownArrayTypesAsObject ? ArrayType.Object : ArrayType.Int;
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
        /// <param name="structName">Struct type name</param>
        /// <param name="stripTransients">Strip transients from the struct</param>
        /// <param name="packageCache"></param>
        /// <param name="shouldReturnClone">Return a deep copy of the struct</param>
        /// <returns></returns>
        public static PropertyCollection getDefaultStructValue(MEGame game, string structName, bool stripTransients, PackageCache packageCache = null, bool shouldReturnClone = true)
        {
            if (game == MEGame.UDK)
            {
                game = MEGame.ME3;
            }
            var defaultStructValues = game switch
            {
                MEGame.ME1 => stripTransients ? ME1UnrealObjectInfo.defaultStructValuesME1 : DefaultStructValuesWithTransientsME1,
                MEGame.ME2 => stripTransients ? ME2UnrealObjectInfo.defaultStructValuesME2 : DefaultStructValuesWithTransientsME2,
                MEGame.ME3 => stripTransients ? ME3UnrealObjectInfo.defaultStructValuesME3 : DefaultStructValuesWithTransientsME3,
                MEGame.LE1 => stripTransients ? LE1UnrealObjectInfo.defaultStructValuesLE1 : DefaultStructValuesWithTransientsLE1,
                MEGame.LE2 => stripTransients ? LE2UnrealObjectInfo.defaultStructValuesLE2 : DefaultStructValuesWithTransientsLE2,
                MEGame.LE3 => stripTransients ? LE3UnrealObjectInfo.defaultStructValuesLE3 : DefaultStructValuesWithTransientsLE3,
                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
            };
            if (defaultStructValues.TryGetValue(structName, out var cachedProps))
            {
                return shouldReturnClone ? cachedProps.DeepClone() : cachedProps;
            }
            var structs = GetStructs(game);
            bool isImmutable = IsImmutable(structName, game);
            if (structs.TryGetValue(structName, out ClassInfo info))
            {
                try
                {
                    PropertyCollection props = new();
                    var infoStack = new Stack<ClassInfo>();
                    while (info is not null)
                    {
                        foreach ((NameReference propName, PropertyInfo propInfo) in info.properties)
                        {
                            if (stripTransients && propInfo.Transient)
                            {
                                continue;
                            }

                            if (getDefaultProperty(game, propName, propInfo, packageCache, stripTransients, isImmutable) is Property prop)
                            {
                                props.Add(prop);
                                if (propInfo.IsStaticArray())
                                {
                                    for (int i = 1; i < propInfo.StaticArrayLength; i++)
                                    {
                                        prop = getDefaultProperty(game, propName, propInfo, packageCache, stripTransients, isImmutable);
                                        prop.StaticArrayIndex = i;
                                        props.Add(prop);
                                    }
                                }
                            }
                        }
                        string filepath = null;
                        if (MEDirectories.GetBioGamePath(game) is string bioGamePath)
                        {
                            filepath = Path.Combine(bioGamePath, info.pccPath);
                        }

                        Stream loadStream = null;
                        IMEPackage cachedPackage = null;
                        if (packageCache != null)
                        {
                            packageCache.TryGetCachedPackage(filepath, true, out cachedPackage);
                            if (cachedPackage == null)
                                packageCache.TryGetCachedPackage(info.pccPath, true, out cachedPackage); // some cache types may have different behavior (such as relative package cache)

                            if (cachedPackage != null)
                            {
                                // Use this one
                                readDefaultProps(cachedPackage, props, packageCache: packageCache);
                            }
                        }
                        else if (filepath != null && MEPackageHandler.TryGetPackageFromCache(filepath, out cachedPackage))
                        {
                            readDefaultProps(cachedPackage, props, packageCache: packageCache);
                        }
                        else if (File.Exists(info.pccPath))
                        {
                            filepath = info.pccPath;
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(info.pccPath);
                        }
                        else if (info.pccPath == Me3ExplorerCustomNativeAdditionsName)
                        {
                            filepath = game switch
                            {
                                MEGame.ME1 => "GAMERESOURCES_ME1",
                                MEGame.ME2 => "GAMERESOURCES_ME2",
                                MEGame.ME3 => "GAMERESOURCES_ME3",
                                MEGame.LE1 => "GAMERESOURCES_LE1",
                                MEGame.LE2 => "GAMERESOURCES_LE2",
                                MEGame.LE3 => "GAMERESOURCES_LE3",
                                _ => throw new ArgumentOutOfRangeException(nameof(game), game, null)
                            };
                            loadStream = LegendaryExplorerCoreUtilities.LoadFileFromCompressedResource("GameResources.zip", LegendaryExplorerCoreLib.CustomResourceFileName(game));
                        }
                        else if (filepath != null && File.Exists(filepath))
                        {
                            loadStream = MEPackageHandler.ReadAllFileBytesIntoMemoryStream(filepath);
                        }

                        if (cachedPackage == null && loadStream != null)
                        {
                            using IMEPackage importPcc = MEPackageHandler.OpenMEPackageFromStream(loadStream, filepath, useSharedPackageCache: true);
                            readDefaultProps(importPcc, props, packageCache);
                        }
                        structs.TryGetValue(info.baseClass, out info);
                    }
                    props.Add(new NoneProperty());

                    defaultStructValues.TryAdd(structName, props);
                    return shouldReturnClone ? props.DeepClone() : props;
                }
                catch (Exception e)
                {
                    LECLog.Warning($@"Exception getting default {game} struct property for {structName}: {e.Message}");
                    return null;
                }
            }
            return null;

            void readDefaultProps(IMEPackage impPackage, PropertyCollection defaultProps, PackageCache packageCache)
            {
                var exportToRead = impPackage.GetUExport(info.exportIndex);
                foreach (var prop in exportToRead.GetBinaryData<UScriptStruct>(packageCache).Defaults)
                {
                    if (prop is NoneProperty)
                    {
                        continue;
                    }
                    defaultProps.TryReplaceProp(prop);
                }
            }
        }

        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsME1 = new();
        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsME2 = new();
        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsME3 = new();
        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsLE1 = new();
        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsLE2 = new();
        private static readonly ConcurrentDictionary<string, PropertyCollection> DefaultStructValuesWithTransientsLE3 = new();

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
            return game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Classes,
                MEGame.ME2 => ME2UnrealObjectInfo.Classes,
                MEGame.UDK => ME3UnrealObjectInfo.Classes,
                MEGame.ME3 => ME3UnrealObjectInfo.Classes,
                MEGame.LE1 => LE1UnrealObjectInfo.Classes,
                MEGame.LE2 => LE2UnrealObjectInfo.Classes,
                MEGame.LE3 => LE3UnrealObjectInfo.Classes,
                _ => null
            };
        }

        public static Dictionary<string, ClassInfo> GetStructs(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Structs,
                MEGame.ME2 => ME2UnrealObjectInfo.Structs,
                MEGame.UDK => ME3UnrealObjectInfo.Structs,
                MEGame.ME3 => ME3UnrealObjectInfo.Structs,
                MEGame.LE1 => LE1UnrealObjectInfo.Structs,
                MEGame.LE2 => LE2UnrealObjectInfo.Structs,
                MEGame.LE3 => LE3UnrealObjectInfo.Structs,
                _ => null
            };
        }

        public static Dictionary<string, List<NameReference>> GetEnums(MEGame game)
        {
            return game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Enums,
                MEGame.ME2 => ME2UnrealObjectInfo.Enums,
                MEGame.UDK => ME3UnrealObjectInfo.Enums,
                MEGame.ME3 => ME3UnrealObjectInfo.Enums,
                MEGame.LE1 => LE1UnrealObjectInfo.Enums,
                MEGame.LE2 => LE2UnrealObjectInfo.Enums,
                MEGame.LE3 => LE3UnrealObjectInfo.Enums,
                _ => null
            };
        }

        public static ClassInfo generateClassInfo(ExportEntry export, bool isStruct = false, PackageCache packageCache = null)
        {
            if (export.ClassName != "ScriptStruct")
            {
                ExportEntry classExport = export.Class switch
                {
                    ExportEntry exportEntry => exportEntry,
                    ImportEntry importEntry => EntryImporter.ResolveImport(importEntry, packageCache),
                    _ => export
                };
                if (classExport is not null)
                {
                    return AddOrReplaceClassInDB(classExport.GetBinaryData<UClass>(packageCache), packageCache);
                }
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

        internal static ClassInfo AddOrReplaceClassInDB(UClass uClass, PackageCache packageCache = null)
        {
            ExportEntry export = uClass.Export;
            IMEPackage pcc = export.FileRef;
            MEGame game = pcc.Game;
            var classInfo = new ClassInfo
            {
                baseClass = export.SuperClassName,
                exportIndex = export.UIndex,
                ClassName = export.ObjectName.Instanced,
                isAbstract = uClass.ClassFlags.Has(UnrealFlags.EClassFlags.Abstract),
                pccPath = pcc.FilePath.Contains("BioGame", StringComparison.InvariantCultureIgnoreCase)
                    ? pcc.FilePath[(pcc.FilePath.LastIndexOf("BIOGame", StringComparison.InvariantCultureIgnoreCase) + 8)..]
                    : pcc.FilePath
            };

            Dictionary<string, ClassInfo> classInfos = GetClasses(game);
            if (export.SuperClass is not null && !classInfos.ContainsKey(classInfo.baseClass))
            {
                ExportEntry classExport = export.SuperClass switch
                {
                    ExportEntry exportEntry => exportEntry,
                    ImportEntry importEntry => EntryImporter.ResolveImport(importEntry, packageCache),
                    _ => throw new ArgumentOutOfRangeException()
                };
                if (classExport is not null)
                {
                    return AddOrReplaceClassInDB(classExport.GetBinaryData<UClass>());
                }
            }

            ParseChildren(uClass, classInfo, GetStructs(game), GetEnums(game));

            classInfos[classInfo.ClassName] = classInfo;

            return classInfo;

            static void ParseChildren(UStruct uStruct, ClassInfo info, Dictionary<string, ClassInfo> structs, Dictionary<string, List<NameReference>> enums)
            {
                IMEPackage pcc = uStruct.Export.FileRef;
                int childUIndex = uStruct.Children;
                while (childUIndex > 0)
                {
                    var childExport = pcc.GetUExport(childUIndex);
                    var field = (UField)ObjectBinary.From(childExport);
                    switch (field)
                    {
                        case UEnum uEnum:
                            enums[childExport.ObjectName.Instanced] = uEnum.Names[..^1].ToList();
                            break;
                        case UScriptStruct uScriptStruct:
                            var structInfo = new ClassInfo
                            {
                                baseClass = childExport.SuperClassName,
                                exportIndex = childUIndex,
                                ClassName = childExport.ObjectName.Instanced,
                                pccPath = info.pccPath
                            };
                            ParseChildren(uScriptStruct, structInfo, structs, enums);
                            structs[structInfo.ClassName] = structInfo;
                            break;
                        case UProperty uProperty:
                            PropertyType? type = null;
                            string reference = null;
                            switch (uProperty)
                            {
                                case UBoolProperty:
                                    type = PropertyType.BoolProperty;
                                    break;
                                case UFloatProperty:
                                    type = PropertyType.FloatProperty;
                                    break;
                                case UIntProperty:
                                    type = PropertyType.IntProperty;
                                    break;
                                case UStringRefProperty:
                                    type = PropertyType.StringRefProperty;
                                    break;
                                case UStrProperty:
                                    type = PropertyType.StrProperty;
                                    break;
                                case UNameProperty:
                                    type = PropertyType.NameProperty;
                                    break;
                                case UDelegateProperty:
                                    type = PropertyType.DelegateProperty;
                                    break;
                                //case UBioMask4Property:
                                case UByteProperty uByteProperty:
                                    type = PropertyType.ByteProperty;
                                    reference = pcc.getObjectName(uByteProperty.Enum);
                                    break;
                                //case UClassProperty:
                                //case UComponentProperty:
                                //case UInterfaceProperty:
                                case UObjectProperty uObjectProperty:
                                    type = PropertyType.ObjectProperty;
                                    reference = pcc.getObjectName(uObjectProperty.ObjectRef);
                                    break;
                                case UStructProperty uStructProperty:
                                    type = PropertyType.StructProperty;
                                    reference = pcc.getObjectName(uStructProperty.Struct);
                                    break;
                                case UArrayProperty uArrayProperty:
                                    type = PropertyType.ArrayProperty;
                                    switch (ObjectBinary.From(pcc.GetUExport(uArrayProperty.ElementType)))
                                    {
                                        case UBoolProperty:
                                            reference = nameof(PropertyType.BoolProperty);
                                            break;
                                        case UDelegateProperty:
                                            reference = nameof(PropertyType.DelegateProperty);
                                            break;
                                        case UFloatProperty:
                                            reference = nameof(PropertyType.FloatProperty);
                                            break;
                                        case UIntProperty:
                                            reference = nameof(PropertyType.IntProperty);
                                            break;
                                        case UNameProperty:
                                            reference = nameof(PropertyType.NameProperty);
                                            break;
                                        case UStringRefProperty:
                                            reference = nameof(PropertyType.StringRefProperty);
                                            break;
                                        case UStrProperty:
                                            reference = nameof(PropertyType.StrProperty);
                                            break;
                                        case UByteProperty uByteProperty:
                                            int enumUIdx = uByteProperty.Enum;
                                            reference = enumUIdx == 0 ? nameof(PropertyType.ByteProperty) : pcc.getObjectName(enumUIdx);
                                            break;
                                        case UObjectProperty uObjectProperty:
                                            reference = pcc.getObjectName(uObjectProperty.ObjectRef);
                                            break;
                                        case UStructProperty uStructProperty:
                                            reference = pcc.getObjectName(uStructProperty.Struct);
                                            break;
                                        default:
                                            throw new ArgumentOutOfRangeException();
                                    }
                                    break;
                            }
                            if (type is not null)
                            {
                                bool transient = uProperty.PropertyFlags.Has(UnrealFlags.EPropertyFlags.Transient);
                                info.properties.Add(childExport.ObjectName.Instanced, new PropertyInfo((PropertyType)type, reference, transient, uProperty.ArraySize));
                            }
                            break;
                    }

                    childUIndex = field.Next;
                }
            }
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



        // Shared global methods for loading custom data

        /// <summary>
        /// Generates sequence object information from a sequence object's class DEFAULTS. The information is installed into the infos object if not present already.
        /// </summary>
        /// <param name="exportEntry"></param>
        /// <param name="infos"></param>
        public static void GenerateSequenceObjectInfoForClassDefaults(ExportEntry exportEntry, Dictionary<string, SequenceObjectInfo> infos = null)
        {
            if (infos == null)
            {
                infos = exportEntry.Game switch
                {
                    MEGame.ME1 => ME1UnrealObjectInfo.SequenceObjects,
                    MEGame.ME2 => ME2UnrealObjectInfo.SequenceObjects,
                    MEGame.ME3 => ME3UnrealObjectInfo.SequenceObjects,
                    MEGame.UDK => ME3UnrealObjectInfo.SequenceObjects,
                    MEGame.LE1 => LE1UnrealObjectInfo.SequenceObjects,
                    MEGame.LE2 => LE2UnrealObjectInfo.SequenceObjects,
                    MEGame.LE3 => LE3UnrealObjectInfo.SequenceObjects,
                    _ => throw new ArgumentOutOfRangeException($"GenerateSequenceObjectInfoForClassDefaults() does not accept export for game {exportEntry.Game}")
                };
            }


            string className = exportEntry.ClassName;
            if (!infos.TryGetValue(className, out SequenceObjectInfo seqObjInfo))
            {
                seqObjInfo = new SequenceObjectInfo();
                infos.Add(className, seqObjInfo);
            }

            int objInstanceVersion = exportEntry.GetProperty<IntProperty>("ObjInstanceVersion");
            if (objInstanceVersion > seqObjInfo.ObjInstanceVersion)
            {
                seqObjInfo.ObjInstanceVersion = objInstanceVersion;
            }

            if (seqObjInfo.inputLinks is null && exportEntry.IsDefaultObject)
            {
                List<string> inputLinks = generateSequenceObjectInfo(exportEntry);
                seqObjInfo.inputLinks = inputLinks;
            }
        }

        //call on the _Default object
        private static List<string> generateSequenceObjectInfo(ExportEntry export)
        {
            var inLinks = export.GetProperty<ArrayProperty<StructProperty>>("InputLinks");
            if (inLinks != null)
            {
                var inputLinks = new List<string>();
                foreach (var seqOpInputLink in inLinks)
                {
                    inputLinks.Add(seqOpInputLink.GetProp<StrProperty>("LinkDesc").Value);
                }
                return inputLinks;
            }

            return null;
        }

        /// <summary>
        /// Installs a ClassInfo object into the respective game's Classes list. Overwrites the existing one if it's already defined.
        /// </summary>
        /// <param name="className"></param>
        /// <param name="info"></param>
        /// <param name="game"></param>
        public static void InstallCustomClassInfo(string className, ClassInfo info, MEGame game)
        {
            var classes = game switch
            {
                MEGame.ME1 => ME1UnrealObjectInfo.Classes,
                MEGame.ME2 => ME2UnrealObjectInfo.Classes,
                MEGame.ME3 => ME3UnrealObjectInfo.Classes,
                MEGame.UDK => ME3UnrealObjectInfo.Classes,
                MEGame.LE1 => LE1UnrealObjectInfo.Classes,
                MEGame.LE2 => LE2UnrealObjectInfo.Classes,
                MEGame.LE3 => LE3UnrealObjectInfo.Classes,
                _ => throw new ArgumentOutOfRangeException($"InstallCustomClassInfo() does not accept game {game}")
            };
            classes[className] = info;
        }
    }
}
