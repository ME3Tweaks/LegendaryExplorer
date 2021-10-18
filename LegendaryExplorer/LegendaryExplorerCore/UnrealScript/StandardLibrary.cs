#if DEBUG
//#define DEBUGSCRIPT
#endif

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
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;

namespace LegendaryExplorerCore.UnrealScript
{
    public partial class FileLib
    {
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

        public static void FreeLibs() => BaseLib.FreeLibs();

        private class BaseLib
        {
            #region Static

            public static void FreeLibs()
            {
                LE1BaseLib = null;
                LE2BaseLib = null;
                LE3BaseLib = null;
                ME1BaseLib = null;
                ME2BaseLib = null;
                ME3BaseLib = null;
            }

            private static BaseLib _le1BaseLib;
            private static BaseLib _le2BaseLib;
            private static BaseLib _le3BaseLib;
            private static BaseLib _me1BaseLib;
            private static BaseLib _me2BaseLib;
            private static BaseLib _me3BaseLib;

            public static BaseLib ME3BaseLib
            {
                get => _me3BaseLib ??= new BaseLib(MEGame.ME3);
                private set => _me3BaseLib = value;
            }

            public static BaseLib ME2BaseLib
            {
                get => _me2BaseLib ??= new BaseLib(MEGame.ME2);
                private set => _me2BaseLib = value;
            }

            public static BaseLib ME1BaseLib
            {
                get => _me1BaseLib ??= new BaseLib(MEGame.ME1);
                private set => _me1BaseLib = value;
            }

            public static BaseLib LE3BaseLib
            {
                get => _le3BaseLib ??= new BaseLib(MEGame.LE3);
                private set => _le3BaseLib = value;
            }

            public static BaseLib LE2BaseLib
            {
                get => _le2BaseLib ??= new BaseLib(MEGame.LE2);
                private set => _le2BaseLib = value;
            }

            public static BaseLib LE1BaseLib
            {
                get => _le1BaseLib ??= new BaseLib(MEGame.LE1);
                private set => _le1BaseLib = value;
            }
            #endregion

            public readonly MEGame Game;

            private SymbolTable _symbols;
            public SymbolTable GetSymbolTable()
            {
                lock (_initializationLock)
                {
                    return _isInitialized ? _symbols?.Clone() : null;
                }
            }

            private bool _isInitialized;

            private readonly object _initializationLock = new();

            private BaseLib(MEGame game)
            {
                Game = game;
            }

            public void Reset()
            {
                lock (_initializationLock)
                {
                    _isInitialized = false;
                    _symbols = null;
                }
            }

            public async Task<bool> InitializeStandardLibAsync(MessageLog log, PackageCache packageCache, string gameRootPath = null)
            {
                lock (_initializationLock)
                {
                    if (_isInitialized)
                    {
                        return true;
                    }
                }

                return await Task.Run(() =>
                {
                    return InitializeStandardLib(log, packageCache, gameRootPath);
                });
            }

            // Non-async
            public bool InitializeStandardLib(MessageLog log, PackageCache packageCache, string gameRootPath)
            {
                bool success;
                lock (_initializationLock)
                {
                    if (_isInitialized)
                    {
                        return true;
                    }
                    success = InternalInitialize(log, packageCache, gameRootPath);
                    _isInitialized = success;
                }
                return success;
            }

            private bool InternalInitialize(MessageLog log, PackageCache packageCache, string gameRootPath = null)
            {
                try
                {
                    LECLog.Information($@"Game Root Path for FileLib Init: {gameRootPath}. Has package cache: {packageCache != null}");
                    List<string> filePaths = BaseFileNames(Game).Select(f => Path.Combine(MEDirectories.GetCookedPath(Game, gameRootPath), f)).ToList();
                    if (!filePaths.All(File.Exists))
                    {
                        foreach (string filePath in filePaths)
                        {
                            if (!File.Exists(filePath))
                            {
                                log.LogError($"Could not find required base file: {filePath}");
                            }
                        }
                        return false;
                    }
                    using var files = MEPackageHandler.OpenMEPackages(filePaths);
                    packageCache?.InsertIntoCache(files);
                    return files.All(pcc => ResolveAllClassesInPackage(pcc, ref _symbols, log, packageCache));
                }
                catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Exception occurred while compiling BaseLib:\n{e.FlattenException()}");
                    return false;
                }
            }

            public static bool ResolveAllClassesInPackage(IMEPackage pcc, ref SymbolTable symbols, MessageLog log, PackageCache packageCache)
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
                    Class cls = ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(packageCache), false, packageCache: packageCache);
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
                    foreach ((Class ast, string scriptText) in classes)
                    {
                        try
                        {
                            var validator = new ClassValidationVisitor(log, symbols, validationPass);
                            ast.AcceptVisitor(validator);
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
}
