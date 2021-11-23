using System;
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
    public class FileLib : IPackageUser, IDisposable
    {
        private SymbolTable _symbols;
        public SymbolTable GetSymbolTable()
        {
            lock (_initializationLock)
            {
                return _isInitialized ? _symbols?.Clone() : null;
            }
        }

        public SymbolTable ReadonlySymbolTable
        {
            get
            {
                lock (_initializationLock)
                {
                    return _isInitialized ? _symbols : null;
                }
            }
        }

        private bool _isInitialized;
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

        public bool HadInitializationError { get; private set; }

        public MessageLog InitializationLog;

        public event Action<bool> InitializationStatusChange;

        private readonly object _initializationLock = new();
        
        private SymbolTable _baseSymbols;

        /// <summary>
        /// Initializes the FileLib asynchronously.
        /// </summary>
        /// <param name="packageCache"></param>
        /// <param name="gameRootPath"></param>
        /// <returns></returns>
        public async Task<bool> InitializeAsync(PackageCache packageCache = null, string gameRootPath = null)
        {
            if (IsInitialized)
            {
                return true;
            }

            return await Task.Run(() => Initialize(packageCache, gameRootPath));
        }

        /// <summary>
        /// Initializes the FileLib on the current thread. This may take some time.
        /// </summary>
        /// <returns></returns>
        public bool Initialize(PackageCache packageCache = null, string gameRootPath = null)
        {
            if (IsInitialized) return true;

            bool success = false;
            lock (_initializationLock)
            {
                if (_isInitialized)
                {
                    return true;
                }

                if (InternalInitialize(packageCache, gameRootPath))
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

        //only use from within _initializationLock!
        private bool InternalInitialize(PackageCache packageCache, string gameRootPath = null)
        {
            try
            {
                LECLog.Information($@"Game Root Path for FileLib Init: {gameRootPath}. Has package cache: {packageCache != null}");
                InitializationLog = new MessageLog();
                _cacheEnabled = false;
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
                        using var pcc = MEPackageHandler.OpenMEPackage(path);
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
                        if (Pcc.FindEntry("SFXGameMPContent") is IEntry { ClassName: "Package" } && !associatedFiles.Contains("BIOP_MP_COMMON.pcc"))
                        {
                            associatedFiles.Add("BIOP_MP_COMMON.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP2") is IEntry { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP2_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP3") is IEntry { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP3_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP4") is IEntry { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP4_INT.pcc");
                        }
                        if (Pcc.FindEntry("SFXGameContentDLC_CON_MP5") is IEntry { ClassName: "Package" })
                        {
                            associatedFiles.Add("Startup_DLC_CON_MP5_INT.pcc");
                        }
                    }
                    foreach (var fileName in Enumerable.Reverse(associatedFiles))
                    {
                        if (gameFiles.TryGetValue(fileName, out string path) && File.Exists(path))
                        {
                            using var pcc = MEPackageHandler.OpenMEPackage(path);
                            if (!ResolveAllClassesInPackage(pcc, ref _baseSymbols, InitializationLog, packageCache))
                            {
                                return false;
                            }
                        }
                    }
                }
                _symbols = _baseSymbols?.Clone();
                _cacheEnabled = true;
                return ResolveAllClassesInPackage(Pcc, ref _symbols, InitializationLog, packageCache);
            }
            catch when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return false;
            }
        }

        public void ReInitializeFile()
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
        }

        public SymbolTable CreateSymbolTableWithClass(Class classOverride, MessageLog logOverride)
        {
            SymbolTable symbols = _baseSymbols?.Clone();
            var packageCache = new PackageCache();
            ResolveAllClassesInPackage(Pcc, ref symbols, logOverride, packageCache, classOverride);
            return symbols;
        }

        public IMEPackage Pcc { get; }

        public FileLib(IMEPackage pcc)
        {
            Pcc = pcc;
            pcc.WeakUsers.Add(this);
            if (!pcc.Game.IsLEGame() && !Pcc.Game.IsOTGame())
            {
                throw new ArgumentOutOfRangeException(nameof(pcc), $"Cannot compile scripts for this game version: {pcc.Game}");
            }
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

        void IPackageUser.handleUpdate(List<PackageUpdate> updates)
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

        void IPackageUser.RegisterClosed(Action handler)
        {
        }

        void IPackageUser.ReleaseUse()
        {
        }

        public void HandleSaveStateChange(bool isSaving)
        {
        }

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

        [Obsolete("Filelib architecture has changed, and this no longer does anything.")]
        public static void FreeLibs() { }

        private bool ResolveAllClassesInPackage(IMEPackage pcc, ref SymbolTable symbols, MessageLog log, PackageCache packageCache = null, Class classOverride = null)
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
                Class cls;
                if (classOverride != null && export.ObjectNameString.CaseInsensitiveEquals(classOverride.Name))
                {
                    cls = classOverride;
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
                string scriptText = "";
                try
                {
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
            LECLog.Debug($"{fileName}: Finished parse.");
            foreach (var validationPass in Enums.GetValues<ValidationPass>())
            {
                foreach ((Class cls, string scriptText) in classes)
                {
                    log.CurrentClass = cls;
                    try
                    {
                        var validator = new ClassValidationVisitor(log, symbols, validationPass);
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

        private bool _cacheEnabled;
        private readonly Dictionary<int, ObjectBinary> objBinCache = new();

        public ObjectBinary GetCachedObjectBinary(ExportEntry export, PackageCache packageCache = null)
        {
            if (_cacheEnabled)
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

        public T GetCachedObjectBinary<T>(ExportEntry export, PackageCache packageCache = null) where T : ObjectBinary, new()
        {
            if (_cacheEnabled)
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
