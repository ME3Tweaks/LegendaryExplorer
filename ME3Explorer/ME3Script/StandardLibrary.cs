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
    public class StandardLibrary
    {
        public static SymbolTable Symbols { get; private set; }
        public static readonly CaseInsensitiveDictionary<(Class ast, string scriptText)> Classes = new CaseInsensitiveDictionary<(Class ast, string scriptText)>();


        public static bool BuildStandardLib()
        {
            bool res = ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GameFramework.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GFxUI.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "WwiseAudio.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXOnlineFoundation.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc"));

            return res;
        }

        public static void ParseBodies()
        {
            var log = new MessageLog();
            foreach ((Class ast, string scriptText) in Classes.Values)
            {
                foreach (Function function in ast.Functions)
                {
                    CodeBodyParser.ParseFunction(function, scriptText, Symbols, log);
                    if (log.Content.Any())
                    {
                        DisplayError(scriptText, log.ToString());
                    }
                }
            }
        }

        private static bool ResolveAllClassesInPackage(string filePath)
        {
            var log = new MessageLog();
            string fileName = Path.GetFileName(filePath);
            Debug.WriteLine($"{fileName}: Beginning Parse.");
            using var pcc = MEPackageHandler.OpenMEPackage(filePath);
            var classes = new List<(Class ast, string scriptText)>();
            foreach (ExportEntry export in pcc.Exports.Where(exp => exp.IsClass))
            {
                Class cls = ME3ObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>());
                var codeBuilder = new CodeBuilderVisitor();
                cls.AcceptVisitor(codeBuilder);
                string scriptText = codeBuilder.GetCodeString();
                try
                {
                    var parser = new ClassOutlineParser(new TokenStream<string>(new StringLexer(scriptText, log)), log);
                    cls = parser.ParseDocument();
                    if (cls == null || log.Content.Any())
                    {
                        DisplayError(scriptText, log.ToString());
                        return false;
                    }

                    if (export.ObjectName == "Object")
                    {
                        Symbols = SymbolTable.CreateIntrinsicTable(cls);
                    }
                    else
                    {
                        Symbols.AddType(cls);
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
            var validationPasses = new []
            {
                ValidationPass.TypesAndFunctionNamesAndStateNames,
                ValidationPass.ClassAndStructMembersAndFunctionParams,
                ValidationPass.BodyPass
            };
            int i = 1;
            foreach (var validationPass in validationPasses)
            {
                foreach ((Class ast, string scriptText) in classes)
                {
                    try
                    {
                        var validator = new ClassValidationVisitor(log, Symbols, validationPass);
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
                Debug.WriteLine($"{fileName}: Finished validation pass {i++}.");
            }

            if (fileName == "Core.pcc")
            {
                Symbols.InitializeOperators();
            }

            foreach ((Class ast, string scriptText) in classes)
            {
                Symbols.RevertToObjectStack();
                if (!ast.Name.CaseInsensitiveEquals("Object"))
                {
                    Symbols.GoDirectlyToStack(((Class)ast.Parent).GetInheritanceString());
                    Symbols.PushScope(ast.Name);
                }

                foreach (Function function in ast.Functions.Where(func => !func.IsNative && func.IsDefined))
                {
                    CodeBodyParser.ParseFunction(function, scriptText, Symbols, log);
                    if (log.Content.Any())
                    {
                        DisplayError(scriptText, log.ToString());
                    }
                }
            }
            Symbols.RevertToObjectStack();

            foreach (var tuple in classes)
            {
                Classes.Add(tuple.ast.Name, tuple);
            }
            return true;
        }

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
