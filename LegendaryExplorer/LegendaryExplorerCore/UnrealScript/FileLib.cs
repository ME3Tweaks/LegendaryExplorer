﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using LegendaryExplorerCore.DebugTools;
using LegendaryExplorerCore.GameFilesystem;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Packages.CloningImportingAndRelinking;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript
{
    /// <summary>
    /// Contains a symbol table for an IMEPackage that is used for compilation and decompilation of UnrealScript.
    /// Must be initialized before use (with <see cref="Initialize"/> or <see cref="InitializeAsync"/>).
    /// Once initialized, use <see cref="ReInitializeFile"/> to update the symbol table to reflect changes made to the IMEPackage.
    /// A <see cref="FileLib"/> can only be used in the compilation or decompilation of objects in the same IMEPackage that is was created for.
    /// </summary>
    public class FileLib : IWeakPackageUser, IDisposable
    {
        private SymbolTable _symbols;
        internal SymbolTable GetSymbolTable()
        {
            lock (_initializationLock)
            {
                return _isInitialized ? _symbols?.Clone() : null;
            }
        }

        internal SymbolTable ReadonlySymbolTable
        {
            get
            {
                lock (_initializationLock)
                {
                    return _isInitialized ? _symbols : null;
                }
            }
        }

        private readonly object _initializationLock = new();

        private SymbolTable _baseSymbols;

        private bool _isInitialized;
        /// <summary>
        /// If this is false, the <see cref="FileLib"/> cannot be used.
        /// </summary>
        public bool IsInitialized
        {
            get
            {
                lock (_initializationLock)
                {
                    return _isInitialized;
                }
            }
        }

        /// <summary>
        /// True if initialization failed. Can, in combination wtih <see cref="IsInitialized"/>, be used to distinguish between a <see cref="FileLib"/> that hasn't been initialized, and one that failed to initialize.
        /// </summary>
        public bool HadInitializationError { get; private set; }

        /// <summary>
        /// A log of warnings or errors that occured during initialization.
        /// </summary>
        public MessageLog InitializationLog;

        public event Action<bool> InitializationStatusChange;

        /// <summary>
        /// The <see cref="IMEPackage"/> that this <see cref="FileLib"/> is associated with.
        /// </summary>
        public IMEPackage Pcc { get; private set; }

        /// <summary>
        /// Creates a <see cref="FileLib"/> for an <see cref="IMEPackage"/>.
        /// </summary>
        /// <param name="pcc">The <see cref="IMEPackage"/> this <see cref="FileLib"/> is for.</param>
        /// <param name="useAutoReinitialization">Optional: Use the <see cref="IWeakPackageUser"/> package change notification system
        /// to automatically call <see cref="ReInitializeFile"/>. Should only be used in the context of a UI compiler. If in doubt, leave it false.</param>
        public FileLib(IMEPackage pcc, bool useAutoReinitialization = false)
        {
            Pcc = pcc;
            if (useAutoReinitialization)
            {
                pcc.WeakUsers.Add(this);
            }
            if (!pcc.Game.IsLEGame() && !pcc.Game.IsOTGame())
            {
                throw new ArgumentOutOfRangeException(nameof(pcc), $"Cannot compile scripts for this game version: {pcc.Game}");
            }
        }

        /// <summary>
        /// Initializes the FileLib asynchronously.
        /// </summary>
        /// <returns>A Task that represents the asynchronous initialization operation and wraps a <see cref="bool"/> indicating whether initialization was succesful.</returns>
        /// <inheritdoc cref="Initialize"/>
        public async Task<bool> InitializeAsync(PackageCache packageCache = null, string gameRootPath = null, bool canUseBinaryCache = true)
        {
            if (IsInitialized)
            {
                return true;
            }

            return await Task.Run(() => Initialize(packageCache, gameRootPath, canUseBinaryCache));
        }

        /// <summary>
        /// Initializes the FileLib. This can potentially take a few seconds.
        /// </summary>
        /// <param name="packageCache">Optional: A <see cref="PackageCache"/> that will be used during initialization.</param>
        /// <param name="gameRootPath">Optional: Use a custom path to look up core files.</param>
        /// <param name="canUseBinaryCache">Optional: Cache <see cref="ObjectBinary"/>s during initialization. Defaults to <c>true</c>.
        /// Caching speeds up initialization and any decompilation operations using this <see cref="FileLib"/>, at the cost of greater memory usage.</param>
        /// <returns>A <see cref="bool"/> indicating whether initialization was succesful. This value will also be in <see cref="IsInitialized"/>.</returns>
        public bool Initialize(PackageCache packageCache = null, string gameRootPath = null, bool canUseBinaryCache = true)
        {
            if (IsInitialized) return true;

            bool success = false;
            lock (_initializationLock)
            {
                if (_isInitialized)
                {
                    return true;
                }

                if (InternalInitialize(packageCache, gameRootPath, canUseBinaryCache))
                {
                    HadInitializationError = false;
                    _isInitialized = true;

                    success = true;
                }
                else
                {
                    HadInitializationError = true;
                    _isInitialized = false;
                    _symbols = null;
                    _baseSymbols = null;
                    _cacheEnabled = false;
                    objBinCache.Clear();
                }
            }

            InitializationStatusChange?.Invoke(true);
            return success;
        }

        /// <summary>
        /// Disposes this <see cref="FileLib"/>. Non-critical, nothing will leak if this isn't disposed.
        /// </summary>
        public void Dispose()
        {
            Pcc?.WeakUsers.Remove(this);
            objBinCache.Clear();
        }

        public static string[] BaseFileNames(MEGame game) => game switch
        {
            MEGame.ME3 => new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc" },
            MEGame.ME2 => new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc" },
            MEGame.ME1 => new[] { "Core.u", "Engine.u", "GameFramework.u", "PlotManagerMap.u", "BIOC_Base.u" },
            MEGame.LE3 => new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc" },
            MEGame.LE2 => new[] { "Core.pcc", "Engine.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc" },
            MEGame.LE1 => new[] { "Core.pcc", "Engine.pcc", "GFxUI.pcc", "PlotManagerMap.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc", "SFXStrategicAI.pcc" },
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };

        public static IEnumerable<string> PackagesWithTopLevelClasses(MEGame game)
        {
            var basefiles = BaseFileNames(game);
            if (game is MEGame.LE1)
            {
                return basefiles.Concat(new[] { "SFXGameContent_Powers.pcc", "SFXVehicleResources.pcc", "SFXWorldResources.pcc" });
            }
            return basefiles;
        }

        [Obsolete("Filelib architecture has changed, and this no longer does anything.")]
        public static void FreeLibs() { }

        //only use from within _initializationLock!
        private bool InternalInitialize(PackageCache packageCache, string gameRootPath = null, bool canUseCache = true)
        {
            bool packageCacheIsLocal = false;
            try
            {
                if (packageCache == null)
                {
                    packageCache = new PackageCache{AlwaysOpenFromDisk = false};
                    packageCacheIsLocal = true;
                }
                LECLog.Information($@"Game Root Path for FileLib Init: {gameRootPath ?? "null"}. Has package cache: {!packageCacheIsLocal}");
                InitializationLog = new MessageLog();
                _cacheEnabled = false; // defaults to false, can be enabled if init works.
                _baseSymbols = null;
                var gameFiles = MELoadedFiles.GetFilesLoadedInGame(Pcc.Game, gameRootOverride: gameRootPath);
                string[] baseFileNames = BaseFileNames(Pcc.Game);
                bool isBaseFile = false;
                if (baseFileNames.IndexOf(Path.GetFileName(Pcc.FilePath)) is var fileNameIdx and >= 0)
                {
                    isBaseFile = true;
                    baseFileNames = baseFileNames.Slice(0, fileNameIdx);
                }
                foreach (string fileName in baseFileNames)
                {
                    if (gameFiles.TryGetValue(fileName, out string path) && File.Exists(path))
                    {
                        IMEPackage pcc = packageCache.GetCachedPackage(path, true);
                        if (!ResolveAllClassesInPackage(pcc, ref _baseSymbols, InitializationLog, packageCache))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        InitializationLog.LogError($"Could not find required base file: {fileName}");
                    }
                }
                if (!isBaseFile)
                {
                    var associatedFiles = EntryImporter.GetPossibleAssociatedFiles(Pcc, includeNonBioPRelated: false);
                    if (Pcc.Game is MEGame.ME3)
                    {
                        associatedFiles.Remove("BIOP_MP_COMMON.pcc");
                        if (Pcc.FindEntry("SFXGameMPContent") is { ClassName: "Package" } mpContentPackage && mpContentPackage.GetChildren<ImportEntry>().Any())
                        {
                            associatedFiles.Add("BIOP_MP_COMMON.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP2") is { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP2_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP3") is { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP3_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP4") is { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP4_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP5") is { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP5_INT.pcc");
                        }
                    }
                    foreach (string fileName in Enumerable.Reverse(associatedFiles))
                    {
                        if (gameFiles.TryGetValue(fileName, out string path) && File.Exists(path))
                        {
                            IMEPackage pcc = packageCache.GetCachedPackage(path, true);
                            if (!ResolveAllClassesInPackage(pcc, ref _baseSymbols, InitializationLog, packageCache))
                            {
                                return false;
                            }
                        }
                    }
                }
                _symbols = _baseSymbols?.Clone();
                _cacheEnabled = canUseCache;
                bool hadError = ResolveAllClassesInPackage(Pcc, ref _symbols, InitializationLog, packageCache);
                if (packageCacheIsLocal)
                {
                    packageCache.Dispose();
                }
                return hadError;
            }
            catch when (!LegendaryExplorerCoreLib.IsDebug)
            {
                if (packageCacheIsLocal)
                {
                    packageCache.Dispose();
                }
                return false;
            }
        }

        /// <summary>
        /// Re-Initializes the <see cref="FileLib"/> to reflect changes made to the <see cref="IMEPackage"/> since Initialization.
        /// If this <see cref="FileLib"/> is used for multiple compilation operations, this method should be called between each one.
        /// (There are some situations where it may not be strictly neccesary to re-initialize between compilations,
        /// but you should only do that if you understand the compiler well enough to have figured out what those situations are.)
        /// </summary>
        /// <returns>A <see cref="bool"/> indicating whether initialization was succesful. This value will also be in <see cref="IsInitialized"/>.</returns>
        public bool ReInitializeFile()
        {
            lock (_initializationLock)
            {
                objBinCache.Clear();
                _symbols = _baseSymbols?.Clone();
                if (ResolveAllClassesInPackage(Pcc, ref _symbols, InitializationLog))
                {
                    HadInitializationError = false;
                    _isInitialized = true;
                }
                else
                {
                    HadInitializationError = true;
                    _isInitialized = false;
                    _symbols = null;
                    objBinCache.Clear();
                }
            }
            InitializationStatusChange?.Invoke(true);
            return IsInitialized;
        }

        internal SymbolTable CreateSymbolTableWithClass(Class classOverride, MessageLog logOverride)
        {
            SymbolTable symbols = _baseSymbols?.Clone();
            var packageCache = new PackageCache();
            ResolveAllClassesInPackage(Pcc, ref symbols, logOverride, packageCache, classOverride);
            return symbols;
        }

        private static bool IsScriptExport(ExportEntry exp)
        {
            switch (exp.ClassName)
            {
                case "Class":
                case "State":
                case "Enum":
                case "Const":
                case "Function":
                case "ScriptStruct":
                case "IntProperty":
                case "BoolProperty":
                case "FloatProperty":
                case "NameProperty":
                case "StrProperty":
                case "StringRefProperty":
                case "ByteProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "InterfaceProperty":
                case "ArrayProperty":
                case "StructProperty":
                case "BioMask4Property":
                case "MapProperty":
                case "ClassProperty":
                case "DelegateProperty":
                    return true;
                default:
                    return false;
            }
        }

        void IWeakPackageUser.HandleUpdate(List<PackageUpdate> updates)
        {
            if (_symbols is null)
            {
                return;
            }
            foreach (PackageUpdate update in updates.Where(u => u.Change.Has(PackageChange.Export)))
            {
                if (Pcc.GetEntry(update.Index) is ExportEntry exp && IsScriptExport(exp))
                {
                    ReInitializeFile();
                    return;
                }
            }
        }

        private bool ResolveAllClassesInPackage(IMEPackage pcc, ref SymbolTable symbols, MessageLog log, PackageCache packageCache = null, Class classOverride = null)
        {
            objBinCache.Clear();
            var realPcc = Pcc;
            Pcc = pcc;
            try
            {
                string fileName = Path.GetFileNameWithoutExtension(pcc.FilePath);
#if DEBUGSCRIPT
                string dumpFolderPath = Path.Combine(MEDirectories.GetDefaultGamePath(pcc.Game), "ScriptDump", fileName);
                Directory.CreateDirectory(dumpFolderPath);
#endif
                LECLog.Debug($"{fileName}: Beginning Parse.");
                var classes = new List<(Class ast, string scriptText)>();
                foreach (ExportEntry export in pcc.Exports.Where(exp => exp.IsClass))
                {
                    string scriptText = "";
                    try
                    {
                        Class cls;
                        bool isClassOverride = false;
                        if (classOverride != null && export.ObjectNameString.CaseInsensitiveEquals(classOverride.Name))
                        {
                            cls = classOverride;
                            classOverride = null;
                            isClassOverride = true;
                        }
                        else
                        {
                            cls = ScriptObjectToASTConverter.ConvertClass(GetCachedObjectBinary<UClass>(export, packageCache), false, this, packageCache);
                        }
                        log.CurrentClass = cls;
                        if (!cls.IsFullyDefined)
                        {
                            continue;
                        }
#if DEBUGSCRIPT
                        var codeBuilder = new CodeBuilderVisitor();
                        cls.AcceptVisitor(codeBuilder);
                        scriptText = codeBuilder.GetOutput();
                        File.WriteAllText(Path.Combine(dumpFolderPath, $"{cls.Name}.uc"), scriptText);
                        var parser = new ClassOutlineParser(new TokenStream<string>(new StringLexer(scriptText, log)), log);
                        cls = parser.TryParseClass();
                        if (cls == null || log.Content.Any())
                        {
                            DisplayError(scriptText, log.ToString());
                            return false;
                        }
#endif
                        if (pcc.Game <= MEGame.ME2 && export.ObjectName == "Package")
                        {
                            symbols.TryGetType("Class", out Class classClass);
                            classClass.OuterClass = cls;
                        }
                        if (export.ObjectName == "Object")
                        {
                            symbols = SymbolTable.CreateIntrinsicTable(cls, pcc.Game);
                        }
                        else if (!symbols.AddType(cls))
                        {
                            if (isClassOverride)
                            {
                                symbols.TryGetType(cls.Name, out Class existingClass);
                                log.CurrentClass = cls;
                                log.LogError($"A class named '{existingClass.Name}' already exists: #{existingClass.UIndex} in {existingClass.FilePath}");
                                return false;
                            }
                            continue; //class already defined
                        }

                        classes.Add(cls, scriptText);
                    }
                    catch (Exception e)// when (!LegendaryExplorerCoreLib.IsDebug)
                    {
                        log.LogError(e.FlattenException());
                        DisplayError(scriptText, log.ToString());
                        return false;
                    }
                }
                if (classOverride is not null)
                {
                    if (symbols.TryGetType(classOverride.Name, out Class existingClass))
                    {
                        log.CurrentClass = classOverride;
                        log.LogError($"A class named '{existingClass.Name}' already exists: #{existingClass.UIndex} in {existingClass.FilePath}");
                        return false;
                    }
                    classes.Add(classOverride, "");
                }
                LECLog.Debug($"{fileName}: Finished parse.");
                var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
                foreach (ValidationPass validationPass in Enums.GetValues<ValidationPass>())
                {
                    foreach ((Class cls, string scriptText) in classes)
                    {
                        log.CurrentClass = cls;
                        try
                        {
                            validator.Pass = validationPass;
                            cls.AcceptVisitor(validator);
                            if (log.HasErrors)
                            {
                                DisplayError(scriptText, log.ToString());
                                return false;
                            }
                        }
                        catch (Exception e)// when(!ME3ExplorerCoreLib.IsDebug)
                        {
                            log.LogError(e.FlattenException());
                            DisplayError(scriptText, log.ToString());
                            return false;
                        }
                    }
                    LECLog.Debug($"{fileName}: Finished validation pass {validationPass}.");
                }
                log.CurrentClass = null;
                switch (fileName)
                {
                    case "Core" when pcc.Game.IsGame3():
                        symbols.InitializeME3LE3Operators();
                        break;
                    case "Engine":
                        symbols.ValidateIntrinsics();
                        break;
                }

#if DEBUGSCRIPT
                //parse function bodies for testing purposes
                foreach ((Class ast, string scriptText) in classes)
                {
                    symbols.RevertToObjectStack();
                    if (!ast.Name.CaseInsensitiveEquals("Object"))
                    {
                        symbols.GoDirectlyToStack(((Class)ast.Parent).GetInheritanceString());
                        symbols.PushScope(ast.Name);
                    }

                    foreach (Function function in ast.Functions.Where(func => !func.IsNative && func.IsDefined))
                    {
                        CodeBodyParser.ParseFunction(function, pcc.Game, scriptText, symbols, log);
                        if (log.Content.Any())
                        {
                            DisplayError(scriptText, log.ToString());
                        }
                    }
                }
#endif

                symbols.RevertToObjectStack();

                return true;
            }
            finally
            {
                Pcc = realPcc;
            }
        }

        private bool _cacheEnabled;
        private readonly Dictionary<int, ObjectBinary> objBinCache = new();

        internal ObjectBinary GetCachedObjectBinary(ExportEntry export, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            // packages without filepaths cannot be uniquely fingerprinted and will not use this this system
            if (_cacheEnabled && export.FileRef.FilePath != null)
            {
                if (!objBinCache.TryGetValue(export.UIndex, out ObjectBinary bin))
                {
                    bin = ObjectBinary.From(export, packageCache);
                    objBinCache[export.UIndex] = bin;
                }
                return bin;
            }
            return ObjectBinary.From(export, packageCache);
        }

        internal T GetCachedObjectBinary<T>(ExportEntry export, PackageCache packageCache = null) where T : ObjectBinary, new()
        {
            if (!ReferenceEquals(Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            // packages without filepaths cannot be uniquely fingerprinted and will not use this this system
            if (_cacheEnabled && export.FileRef.FilePath != null)
            {
                if (!objBinCache.TryGetValue(export.UIndex, out ObjectBinary bin))
                {
                    bin = ObjectBinary.From(export, packageCache);
                    objBinCache[export.UIndex] = bin;
                }
                return (T)bin;
            }
            return ObjectBinary.From<T>(export, packageCache);
        }


        [Conditional("DEBUGSCRIPT")]
        private static void DisplayError(string scriptText, string logText)
        {
            string scriptFile = Path.Combine("TEMPME3Script.txt");
            string logFile = Path.Combine("TEMPME3Script.log");
            File.WriteAllText(scriptFile, scriptText);
            File.WriteAllText(logFile, logText);
            Process.Start("notepad++", $"\"{scriptFile}\" \"{logFile}\"");
        }
    }
}
