#if DEBUG
//#define DEBUGSCRIPT
#endif

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Analysis.Symbols;
using ME3Script.Analysis.Visitors;
using ME3Script.Compiling.Errors;
using ME3Script.Decompiling;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Parsing;

namespace ME3Explorer.ME3Script
{

    public static class StandardLibrary
    {
        private static SymbolTable _symbols;
        public static SymbolTable GetSymbolTable() => IsInitialized ? _symbols?.Clone() : null;

        public static SymbolTable ReadonlySymbolTable => IsInitialized ? _symbols : null;

        //public static readonly CaseInsensitiveDictionary<(Class ast, string scriptText)> Classes = new CaseInsensitiveDictionary<(Class ast, string scriptText)>();

        public static bool IsInitialized { get; private set; }

        public static bool HadInitializationError { get; private set; }

        public static event EventHandler Initialized;

        private static readonly object initializationLock = new object();

        public static async Task<bool> InitializeStandardLib()
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
                    success = InternalInitialize();
                    IsInitialized = success;
                    HadInitializationError = !success;
                }
                Initialized?.Invoke(null, EventArgs.Empty);
                return success;
            });
        }

        private static bool InternalInitialize()
        {
            try
            {
                return ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GameFramework.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GFxUI.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "WwiseAudio.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXOnlineFoundation.pcc"), ref _symbols) &&
                       ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc"), ref _symbols);
            }
            catch (Exception e)
            {
                return false;
            }
        }

        public static bool ResolveAllClassesInPackage(string filePath, ref SymbolTable symbols)
        {
#if DEBUGSCRIPT
            string dumpFolderPath = Path.Combine(ME3Directory.gamePath, "ScriptDump", Path.GetFileNameWithoutExtension(filePath));
            Directory.CreateDirectory(dumpFolderPath);
#endif
            var log = new MessageLog();
            string fileName = Path.GetFileName(filePath);
            Debug.WriteLine($"{fileName}: Beginning Parse.");
            using var pcc = MEPackageHandler.OpenMEPackage(filePath);
            var classes = new List<(Class ast, string scriptText)>();
            foreach (ExportEntry export in pcc.Exports.Where(exp => exp.IsClass))
            {
                Class cls = ME3ObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(), false);
                string scriptText = "";
                try
                {
#if DEBUGSCRIPT
                    var codeBuilder = new CodeBuilderVisitor();
                    cls.AcceptVisitor(codeBuilder);
                    scriptText = codeBuilder.GetCodeString();
                    File.WriteAllText(Path.Combine(dumpFolderPath, $"{cls.Name}.uc"), scriptText);
                    var parser = new ClassOutlineParser(new TokenStream<string>(new StringLexer(scriptText, log)), log);
                    cls = parser.TryParseClass();
                    if (cls == null || log.Content.Any())
                    {
                        DisplayError(scriptText, log.ToString());
                        return false;
                    }
#endif

                    if (export.ObjectName == "Object")
                    {
                        symbols = SymbolTable.CreateIntrinsicTable(cls);
                    }
                    else
                    {
                        symbols.AddType(cls);
                    }

                    classes.Add(cls, scriptText);
                }
                catch (Exception e) when (!App.IsDebug)
                {
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
                    catch (Exception e) when(!App.IsDebug)
                    {
                        DisplayError(scriptText, log.ToString());
                        return false;
                    }
                }
                Debug.WriteLine($"{fileName}: Finished validation pass {validationPass}.");
            }

            switch (fileName)
            {
                case "Core.pcc":
                    symbols.InitializeOperators();
                    break;
                case "Engine.pcc":
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
                    CodeBodyParser.ParseFunction(function, scriptText, symbols, log);
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
            string scriptFile = Path.Combine(App.ExecFolder, "TEMPME3Script.txt");
            string logFile = Path.Combine(App.ExecFolder, "TEMPME3Script.log");
            File.WriteAllText(scriptFile, scriptText);
            File.WriteAllText(logFile, logText);
            Process.Start("notepad++", $"\"{scriptFile}\" \"{logFile}\"");
        }
    }
}
