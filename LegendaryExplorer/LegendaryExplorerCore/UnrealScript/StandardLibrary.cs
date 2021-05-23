#if DEBUG
//#define DEBUGSCRIPT
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
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
            //TODO: Check if these are correct for LE
            MEGame.LE3 => new[] { "Core.pcc", "Engine.pcc", "GameFramework.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc" },
            MEGame.LE2 => new[] { "Core.pcc", "Engine.pcc", "GFxUI.pcc", "WwiseAudio.pcc", "SFXOnlineFoundation.pcc", "PlotManagerMap.pcc", "SFXGame.pcc", "Startup_INT.pcc" },
            MEGame.LE1 => new[] { "Core.pcc", "Engine.pcc", "GFxUI.pcc", "PlotManagerMap.pcc", "SFXOnlineFoundation.pcc", "SFXGame.pcc", "SFXStrategicAI.pcc" },
            _ => throw new ArgumentOutOfRangeException(nameof(game))
        };

        private class BaseLib
        {
            #region Static

            public static BaseLib ME3BaseLib { get; } = new(MEGame.ME3);
            public static BaseLib ME2BaseLib { get; } = new(MEGame.ME2);
            public static BaseLib ME1BaseLib { get; } = new(MEGame.ME1);
            public static BaseLib LE3BaseLib { get; } = new(MEGame.LE3);
            public static BaseLib LE2BaseLib { get; } = new(MEGame.LE2);
            public static BaseLib LE1BaseLib { get; } = new(MEGame.LE1);

            #endregion

            public readonly MEGame Game;

            private SymbolTable _symbols;
            public SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

            private bool IsInitialized;

            public bool HadInitializationError { get; private set; }

            private readonly object initializationLock = new();

            private BaseLib(MEGame game)
            {
                Game = game;
            }

            public async Task<bool> InitializeStandardLib(MessageLog log, params string[] additionalFiles)
            {
                if (IsInitialized)
                {
                    return true;
                }

                return await Task.Run(() =>
                {
                    bool success;
                    if (IsInitialized)
                    {
                        return true;
                    }
                    lock (initializationLock)
                    {
                        if (IsInitialized)
                        {
                            return true;
                        }
                        success = InternalInitialize(additionalFiles, log);
                        IsInitialized = success;
                        HadInitializationError = !success;
                    }
                    return success;
                });
            }

            private bool InternalInitialize(string[] additionalFiles, MessageLog log)
            {
                try
                {
                    List<string> filePaths = BaseFileNames(Game).Select(f => Path.Combine(MEDirectories.GetCookedPath(Game), f)).ToList();
                    filePaths.AddRange(additionalFiles);
                    if (!filePaths.All(File.Exists))
                    {
                        return false;
                    }
                    using var files = MEPackageHandler.OpenMEPackages(filePaths);

                    return files.All(pcc => ResolveAllClassesInPackage(pcc, ref _symbols, log));
                }
                catch (Exception e) when(!LegendaryExplorerCoreLib.IsDebug)
                {
                    return false;
                }
            }

            public static bool ResolveAllClassesInPackage(IMEPackage pcc, ref SymbolTable symbols, MessageLog log)
            {
                string fileName = Path.GetFileNameWithoutExtension(pcc.FilePath);
#if DEBUGSCRIPT
                string dumpFolderPath = Path.Combine(MEDirectories.GetDefaultGamePath(pcc.Game), "ScriptDump", fileName);
                Directory.CreateDirectory(dumpFolderPath);
#endif
                Debug.WriteLine($"{fileName}: Beginning Parse.");
                var classes = new List<(Class ast, string scriptText)>();
                foreach (ExportEntry export in pcc.Exports.Where(exp => exp.IsClass))
                {
                    Class cls = ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(), false);
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
                Debug.WriteLine($"{fileName}: Finished parse.");
                foreach (var validationPass in Enums.GetValues<ValidationPass>())
                {
                    foreach ((Class ast, string scriptText) in classes)
                    {
                        try
                        {
                            var validator = new ClassValidationVisitor(log, symbols, validationPass);
                            ast.AcceptVisitor(validator);
                            if (log.Content.Any())
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
                    Debug.WriteLine($"{fileName}: Finished validation pass {validationPass}.");
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
            static void DisplayError(string scriptText, string logText)
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
