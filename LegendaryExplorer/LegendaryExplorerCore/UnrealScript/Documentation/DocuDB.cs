using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Misc.ME3Tweaks;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using Newtonsoft.Json;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Documentation
{
    /// <summary>
    /// Root object for documentation of UnrealScript class objects.
    /// </summary>
    public class DocuDB
    {
        /// <summary>
        /// The loaded documentation databases.
        /// </summary>
        private static Dictionary<MEGame, DocuDB> LoadedDBs { get; } = new();

        /// <summary>
        /// Contains a mapping of class names to their documentation objects.
        /// </summary>
        [JsonProperty("classes")]
        public CaseInsensitiveDictionary<DocuClassEntry> ClassDocumentation { get; set; } = new();

        [JsonProperty("enums")]
        public CaseInsensitiveDictionary<DocuEnumEntry> EnumDocumentation { get; set; } = new();

        [JsonProperty("structs")]
        public CaseInsensitiveDictionary<DocuStructEntry> StructDocumentation { get; set; } = new();

        /// <summary>
        /// The time at which this documentation database was last updated.
        /// </summary>
        [JsonProperty("last_updated")]
        public DateTime UpdateTime { get; set; }

        public DocuDB() { }

        public static DocuDB GetEmptyDB(MEGame game)
        {
            return new DocuDB()
            {
                ClassDocumentation = new(),
                EnumDocumentation = new(),
                StructDocumentation = new(),
                Game = game
            };
        }

        public static DocuDB GetLoadedDB(MEGame game)
        {
            if (LoadedDBs.TryGetValue(game, out var db)) return db;
            return null;
        }

        /// <summary>
        /// Fetches a documentation database, loading it if it isn't already loaded.
        /// </summary>
        /// <param name="game">Game to fetch</param>
        /// <returns></returns>
        public static DocuDB LoadDocuDB(MEGame game, string filePath)
        {
            if (LoadedDBs.TryGetValue(game, out var loaded))
            {
                return loaded;
            }

            if (Directory.Exists(filePath))
            {
                // Loading loose DB
                loaded = new DocuDB() { Game = game };

                // Loose load classes
                loaded.ClassDocumentation = new();
                foreach (var f in Directory.GetFiles(Path.Combine(filePath, "classes"), "*.json"))
                {
                    var fname = Path.GetFileNameWithoutExtension(f);
                    loaded.ClassDocumentation[fname] = JsonConvert.DeserializeObject<DocuClassEntry>(File.ReadAllText(f));
                }

                loaded.EnumDocumentation = new();
                foreach (var f in Directory.GetFiles(Path.Combine(filePath, "enums"), "*.json"))
                {
                    var fname = Path.GetFileNameWithoutExtension(f);
                    loaded.EnumDocumentation[fname] = JsonConvert.DeserializeObject<DocuEnumEntry>(File.ReadAllText(f));
                }

                loaded.StructDocumentation = new();
                foreach (var f in Directory.GetFiles(Path.Combine(filePath, "structs"), "*.json"))
                {
                    var fname = Path.GetFileNameWithoutExtension(f);
                    loaded.StructDocumentation[fname] = JsonConvert.DeserializeObject<DocuStructEntry>(File.ReadAllText(f));
                }

            }
            else if (File.Exists(filePath))
            {
                try
                {
                    loaded = JsonConvert.DeserializeObject<DocuDB>(File.ReadAllText(filePath));
                    LoadedDBs[game] = loaded;
                }
                catch (Exception ex)
                {
                    // Failed to load document DB.
                    return null;
                }
            }

            LoadedDBs[game] = loaded;
            return loaded;
        }

        [JsonProperty("game")]
        public MEGame Game { get; set; }

#if DEBUG

        /// <summary>
        /// Generates a documentation-less database based on classes found in the game.
        /// </summary>
        /// <param name="game"></param>
        /// <returns></returns>
        public static DocuDB GenerateInitialDB(MEGame game)
        {
            DocuDB db = new DocuDB();

            // Load cache
            TieredPackageCache tpc = TieredPackageCache.GetGlobalPackageCache(game, gameRootPath: ME3TweaksBackups.GetGameBackupPath(game));

            var toBreak = false;
            foreach (var f in MELoadedFiles.GetFilesLoadedInGame(game, gameRootOverride: ME3TweaksBackups.GetGameBackupPath(game)))
            {
                var quickPackage = MEPackageHandler.UnsafePartialLoad(f.Value, x => false);
                var hasUndocedClasses = quickPackage.Exports.Any(x => x.IsClass && !db.ClassDocumentation.ContainsKey(x.ObjectName));
                if (!hasUndocedClasses)
                    continue;

                using var package = MEPackageHandler.OpenMEPackage(f.Value);
                FileLib fl = null;
                UnrealScriptOptionsPackage usop = null;
                foreach (var cls in package.Exports.Where(x => x.IsClass))
                {
                    if (db.ClassDocumentation.ContainsKey(cls.ObjectName))
                        continue;

                    if (fl == null)
                    {
                        fl = new FileLib(package);
                        var cache = tpc.ChainNewCache();
                        usop = new UnrealScriptOptionsPackage() { Cache = cache };
                        fl.Initialize(usop);
                        if (fl.HadInitializationError)
                            break; // Can't do this file.
                    }

                    foreach (var fx in fl.ReadonlySymbolTable.Types.Where(x => x.Type == ASTNodeType.Class))
                    {
                        if (db.ClassDocumentation.ContainsKey(fx.Name))
                            continue;

                        var classD = GenerateDocuClass(db, fx);
                        db.ClassDocumentation[fx.Name] = classD;
                    }

                    toBreak = true;
                }

                if (toBreak)
                    break;
            }

            return db;
        }

        private static DocuClassEntry GenerateDocuClass(DocuDB db, VariableType classDef)
        {
            DocuClassEntry dce = new DocuClassEntry();
            dce.Variables = new CaseInsensitiveDictionary<DocuMemberEntry>();
            dce.Functions = new CaseInsensitiveDictionary<DocuFunctionEntry>();
            dce.States = new CaseInsensitiveDictionary<DocuStateEntry>();

            if (classDef is Class cls)
            {
                dce.ClassFlags = cls.Flags;
                dce.ConfigName = cls.ConfigName;
                dce.ParentClass = cls.Parent?.Name;
                dce.Package = cls.Package; // is this right?
                // Variables
                foreach (var v in cls.VariableDeclarations)
                {
                    dce.Variables.Add(v.Name, new DocuMemberEntry()
                    {
                        MemberType = v.VarType.Name,
                        Flags = (long)v.Flags
                    });
                }


                // Functions
                foreach (var v in cls.Functions)
                {
                    var func = new DocuFunctionEntry();
                    func.FunctionMembers = new();
                    func.ReturnType = v.ReturnType?.Name ?? "None";
                    func.FunctionSignature = CodeBuilderVisitor.GetFunctionSignature(v);
                    foreach (var param in v.Parameters)
                    {
                        func.FunctionMembers.Add(param.Name, new DocuMemberEntry()
                        {
                            MemberType = param.VarType.Name,
                            Flags = (long)param.Flags
                        });
                    }
                    dce.Functions.Add(v.Name, func);
                }

                // Structs, Consts, Enums
                foreach (var type in cls.TypeDeclarations)
                {
                    // If class has been inventoried, it should not have a dup existing entry...
                    if (type is Struct tStruct)
                    {
                        var structEntry = GenerateDocuStruct(db, tStruct);
                        db.StructDocumentation[type.Name] = structEntry;
                    }
                    else if (type is Const tConst)
                    {
                        // Nothing here. Are consts even used??
                    }
                    else if (type is Enumeration tEnum)
                    {
                        var structEntry = GenerateDocuEnum(tEnum);
                        db.EnumDocumentation[type.Name] = structEntry;
                    }
                }

                // States
                foreach (var state in cls.States)
                {
                    var dState = new DocuStateEntry();
                    dState.Functions = new();
                    foreach (var v in state.Functions)
                    {
                        var func = new DocuFunctionEntry();
                        func.FunctionMembers = new();
                        foreach (var param in v.Parameters)
                        {
                            func.FunctionMembers.Add(param.Name, new DocuMemberEntry()
                            {
                                MemberType = param.VarType.Name,
                                Flags = (long) param.Flags
                            });
                        }
                        dState.Functions.Add(v.Name, func);
                    }
                    dce.States.Add(state.Name, dState);
                }
            }
            return dce;
        }

        private static DocuEnumEntry GenerateDocuEnum(Enumeration tEnum)
        {
            DocuEnumEntry dType = new();
            dType.EnumValues = new();
            foreach (var eValue in tEnum.Values)
            {
                dType.EnumValues.Add(eValue.Name, new DocuEnumValueEntry() { EnumValue = eValue.IntVal });
            }

            return dType;
        }

        private static DocuStructEntry GenerateDocuStruct(DocuDB db, Struct tStruct)
        {
            DocuStructEntry dType = new();
            dType.DefinedInClass = (tStruct.Outer as Class)?.Name; // This should always be not-null...
            dType.Modifiers = tStruct.Flags;
            if (tStruct.Parent != null)
            {
                dType.Extends = tStruct.Parent.Name;
            }
            dType.Members = new();
            foreach (var eValue in tStruct.VariableDeclarations)
            {
                dType.Members[eValue.Name] = new DocuMemberEntry()
                {
                    MemberType = eValue.VarType.Name,
                    Flags = (long)eValue.Flags,
                };
            }

            // Need to enumerate type entries.

            return dType;
        }
#endif
        public virtual string GetDocumentation(string className, string memberName, string subMemberName = null)
        {
            if (ClassDocumentation.TryGetValue(className, out var cls))
            {
                // Member
                if (cls.Variables.TryGetValue(memberName, out var member))
                {
                    return member.MemberDocumentation;
                }

                // Function
                if (cls.Functions.TryGetValue(memberName, out var function))
                {
                    // Are we getting a parameter on the func?
                    if (subMemberName != null)
                    {
                        function.FunctionMembers.TryGetValue(subMemberName, out var subMember);
                        return subMember?.MemberDocumentation;
                    }

                    return function.MemberDocumentation;
                }

                // States
                if (cls.Functions.TryGetValue(memberName, out var state))
                {
                    //    if (subMemberName != null)
                    //    {
                    //        function.FunctionMembers.TryGetValue(subMemberName, out var subMember);
                    //        return subMember?.MemberDocumentation;
                    //    }

                    //    return function.MemberDocumentation;
                }
            }

            return null;
        }

        public virtual string GetDocumentation(IEntry obj)
        {
            // Determine: Are we under a class or struct (including state)?
            // If so, we should return the member documentation.

            // otherwise we should return the class documentation or struct documentation for the type
            // passed in.



            // This is really poor and definitely needs improved

            var classObj = obj;
            while (classObj is { HasParent: true })
            {
                if (classObj.IsClass)
                    break;

                classObj = classObj.Parent;
            }

            if (classObj.IsClass)
            {
                // We found containing class
                if (ClassDocumentation.TryGetValue(classObj.ObjectName, out var classInfo))
                {
                    if (obj == classObj)
                    {
                        // It's the class itself.
                        return classInfo.ClassDocumentation;
                    }

                    if (classInfo.Variables.TryGetValue(obj.ObjectName.Instanced, out var member))
                    {
                        // it's a member on the class
                        return member.MemberDocumentation;
                    }
                }


            }





            //if (obj.ClassName == "StructProperty")
            //{
            //    // Struct
            //    if (db.ClassDocumentation.TryGetValue(CurrentLoadedEntry.ClassName, out var cls))
            //    {
            //        return cls.ClassDocumentation;
            //    }
            //}
            //else if (CurrentLoadedEntry.ClassName == "ByteProperty" && db.ClassDocumentation.TryGetValue(CurrentLoadedEntry.ClassName, out var enumV))
            //{
            //    // ByteProperty might just be member of class...
            //    DocumentationString = enumV.ClassDocumentation;
            //    return;
            //}
            //else if (CurrentLoadedEntry.IsClass)
            //{
            //    // Class
            //    if (db.ClassDocumentation.TryGetValue(CurrentLoadedEntry.ClassName, out var cls))
            //    {
            //        DocumentationString = cls.ClassDocumentation;
            //        return;
            //    }
            //}
            //else
            //{

            //}



            return null;
        }

        public static void AddLoadedDb(MEGame game, DocuDB loadedDb)
        {
            LoadedDBs[game] = loadedDb;
        }
    }

    /// <summary>
    /// Describes the objects in a class.
    /// </summary>
    public class DocuClassEntry
    {
        /// <summary>
        /// Link the parent class or function, for further documentation lookups.
        /// </summary>
        [JsonProperty("parent")]
        public string ParentClass { get; set; }

        /// <summary>
        /// Package this class is part of
        /// </summary>
        [JsonProperty("package")]
        public string Package { get; set; }

        /// <summary>
        /// Variable members of this class
        /// </summary>
        [JsonProperty("variables")]
        public CaseInsensitiveDictionary<DocuMemberEntry> Variables { get; set; }

        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("functions")]
        public CaseInsensitiveDictionary<DocuFunctionEntry> Functions { get; set; }

        /// <summary>
        /// State members of this class
        /// </summary>
        [JsonProperty("states")]
        public CaseInsensitiveDictionary<DocuStateEntry> States { get; set; }

        /// <summary>
        /// Contains the text description of this class.
        /// </summary>
        [JsonProperty("classflags")]
        public EClassFlags ClassFlags { get; set; }

        /// <summary>
        /// If class is marked config, it reads from this file
        /// </summary>
        [JsonProperty("configname")]
        public string ConfigName { get; set; }

        /// <summary>
        /// Contains the text description of this class.
        /// </summary>
        [JsonProperty("documentation")]
        public string ClassDocumentation { get; set; }
    }

    public class DocuStateEntry
    {
        /// <summary>
        /// Function members of this class
        /// </summary>
        [JsonProperty("functions")]
        public CaseInsensitiveDictionary<DocuFunctionEntry> Functions { get; set; }

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }
    }

    public class DocuStructEntry
    {
        // Does this keep order? It matters for immutable structs
        [JsonProperty("members")]
        public CaseInsensitiveDictionary<DocuMemberEntry> Members { get; set; } = new();

        /// <summary>
        /// Which class this struct is defined in
        /// </summary>
        [JsonProperty("classdef")]
        public string DefinedInClass { get; set; }

        /// <summary>
        /// Which struct this one extends, if any
        /// </summary>
        [JsonProperty("extends")]
        public string Extends { get; set; }

        // Const doesn't have any members
        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }

        /// <summary>
        /// Struct modifiers
        /// </summary>
        [JsonProperty("modifiers")]
        public UnrealFlags.ScriptStructFlags Modifiers { get; set; }
    }


    public class DocuEnumEntry
    {
        /// <summary>
        /// Values of the enum
        /// </summary>
        public CaseInsensitiveDictionary<DocuEnumValueEntry> EnumValues { get; set; } = new();

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }
    }

    public class DocuFunctionEntry
    {
        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }


        /// <summary>
        /// Class of the member
        /// </summary>
        [JsonProperty("signature_members")]
        public CaseInsensitiveDictionary<DocuMemberEntry> FunctionMembers { get; set; }

        /// <summary>
        /// The return type for the function. Can technically be found in members...
        /// </summary>
        [JsonProperty("returntype")]
        public string ReturnType { get; set; }


        [JsonProperty("signature")]
        public string FunctionSignature { get; set; }
    }

    public class DocuEnumValueEntry
    {
        [JsonProperty("value")]
        public int EnumValue { get; set; }

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }
    }

    public class DocuMemberEntry
    {
        /// <summary>
        /// Name of the documentation - can be null if not used.
        /// </summary>
        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public string MemberName { get; set; }

        /// <summary>
        /// Type of this member
        /// </summary>
        [JsonProperty("type")]
        public string MemberType { get; set; }

        /// <summary>
        /// Contains the text description of this object.
        /// </summary>
        [JsonProperty("documentation")]
        public string MemberDocumentation { get; set; }

        /// <summary>
        /// Flags on this member, stored as a long.
        /// </summary>
        [JsonProperty("flags")]
        public long Flags { get; set; }
    }
}
