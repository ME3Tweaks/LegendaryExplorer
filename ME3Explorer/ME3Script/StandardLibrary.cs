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
        private static SymbolTable Symbols;
        private static readonly List<(Class ast, string scriptText)> Classes = new List<(Class ast, string scriptText)>();


        public static bool BuildStandardLib()
        {
            return ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Core.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "Engine.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GameFramework.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "GFxUI.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "WwiseAudio.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXOnlineFoundation.pcc")) &&
            ResolveAllClassesInPackage(Path.Combine(ME3Directory.cookedPath, "SFXGame.pcc"));
        }

        private static bool ResolveAllClassesInPackage(string filePath)
        {
            using var corePcc = MEPackageHandler.OpenMEPackage(filePath);
            var classes = new List<(Class ast, string scriptText)>();
            foreach (ExportEntry export in corePcc.Exports.Where(exp => exp.IsClass))
            {
                var log = new MessageLog();
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

            var validationPasses = new []
            {
                ValidationPass.TypesAndFunctionNamesAndStateNames,
                ValidationPass.ClassAndStructMembersAndFunctionParams,
                ValidationPass.BodyPass
            };
            foreach (var validationPass in validationPasses)
            {
                foreach ((Class ast, string scriptText) in classes)
                {
                    var log = new MessageLog();
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
            }
            Classes.AddRange(classes);
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
