using System;
using System.Linq;
using ME3Explorer;
using ME3Explorer.ME3Script;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Analysis.Visitors;
using ME3Script.Compiling;
using ME3Script.Compiling.Errors;
using ME3Script.Decompiling;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Parsing;

namespace ME3Script
{
    public static class ME3ScriptCompiler
    {
        public static (ASTNode node, string text) DecompileExport(ExportEntry export, FileLib lib = null)
        {
            try
            {
                ASTNode astNode = null;
                switch (export.ClassName)
                {
                    case "Class":
                        astNode = ME3ObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(), true, lib);
                        break;
                    case "Function":
                        astNode = ME3ObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>(), lib: lib);
                        break;
                    case "State":
                        astNode = ME3ObjectToASTConverter.ConvertState(export.GetBinaryData<UState>(), lib: lib);
                        break;
                    case "Enum":
                        astNode = ME3ObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>());
                        break;
                    case "ScriptStruct":
                        astNode = ME3ObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>());
                        break;
                    default:
                        if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export) is UProperty uProp)
                        {
                            astNode = ME3ObjectToASTConverter.ConvertVariable(uProp);
                        }
                        else
                        {
                            astNode = ME3ObjectToASTConverter.ConvertDefaultProperties(export);
                        }

                        break;
                }

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetOutput());
                }
            }
            catch (Exception e) when (!App.IsDebug)
            {
                return (null, $"Error occured while decompiling {export?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, "Could not decompile!");
        }

        public static Function CompileFunctionBodyAST(ExportEntry parentExport, string scriptText, Function func, MessageLog log, FileLib lib = null)
        {
            var symbols = lib?.GetSymbolTable() ?? StandardLibrary.GetSymbolTable();
            symbols.RevertToObjectStack();

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName, out Class containingClass))
            {
                int funcIdx = containingClass.Functions.FindIndex(fun => fun.Name == func.Name);
                Function originalFunction = containingClass.Functions[funcIdx];
                originalFunction.Body = func.Body;
                originalFunction.Body.Outer = originalFunction;
                for (int i = 0; i < func.Parameters.Count && i < originalFunction.Parameters.Count; i++)
                {
                    originalFunction.Parameters[i].UnparsedDefaultParam = func.Parameters[i].UnparsedDefaultParam;
                }
                originalFunction.Locals.Clear();

                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                CodeBodyParser.ParseFunction(originalFunction, scriptText, symbols, log);
                return originalFunction;
            }
            //in state
            if (parentExport.Parent is ExportEntry classExport && classExport.IsClass && symbols.TryGetType(classExport.ObjectNameString, out Class cls)
             && cls.States.FirstOrDefault(s => s.Name.CaseInsensitiveEquals(parentExport.ObjectNameString)) is State state
             && state.Functions.FirstOrDefault(f => f.Name == func.Name) is Function canonicalFunction)
            {
                canonicalFunction.Body = func.Body;
                canonicalFunction.Body.Outer = canonicalFunction;
                for (int i = 0; i < func.Parameters.Count && i < canonicalFunction.Parameters.Count; i++)
                {
                    canonicalFunction.Parameters[i].UnparsedDefaultParam = func.Parameters[i].UnparsedDefaultParam;
                }
                canonicalFunction.Locals.Clear();

                symbols.GoDirectlyToStack(((Class)cls.Parent).GetInheritanceString());
                symbols.PushScope(cls.Name);
                symbols.PushScope(state.Name);

                CodeBodyParser.ParseFunction(canonicalFunction, scriptText, symbols, log);
                return canonicalFunction;
            }

            return null;
        }

        public static (ASTNode ast, MessageLog log) CompileAST(string script, string type)
        {
            var log = new MessageLog();
            var parser = new ClassOutlineParser(new TokenStream<string>(new StringLexer(script, log)), log);
            try
            {
                ASTNode ast = parser.ParseDocument(type);
                log.LogMessage($"Parse{(ast is null ? " failed!" : "d!")}");
                return (ast, log);
            }
            catch (Exception e)
            {
                log.LogMessage($"Parse failed! Exception: {e}");
                return (null, log);
            }

        }

        public static (ASTNode astNode, MessageLog log) CompileFunction(ExportEntry export, string scriptText, FileLib lib = null)
        {
            (ASTNode astNode, MessageLog log) = CompileAST(scriptText, export.ClassName);
            if (astNode != null && log.AllErrors.IsEmpty())
            {
                if (astNode is Function func && (lib?.IsInitialized ?? StandardLibrary.IsInitialized) && export.Parent is ExportEntry parent)
                {
                    try
                    {
                        astNode = CompileFunctionBodyAST(parent, scriptText, func, log, lib);
                    }
                    catch (ParseError)
                    {
                        log.LogError("Parse failed!");
                        return (astNode, log);
                    }
                    catch (Exception exception)
                    {
                        log.LogError($"Parse failed! Exception: {exception}");
                        return (astNode, log);
                    }

                    if (astNode is Function funcFullAST)
                    {
                        try
                        {
                            var bytecodeVisitor = new ByteCodeCompilerVisitor(export.GetBinaryData<UFunction>());
                            bytecodeVisitor.Compile(funcFullAST);
                            if (log.AllErrors.Count == 0)
                            {
                                log.LogMessage("Compiled!");
                            }
                            return (astNode, log);
                        }
                        catch (Exception exception)
                        {
                            log.LogError(exception.Message);
                        }
                    }
                }
            }

            return (null, log);
        }
    }
}
