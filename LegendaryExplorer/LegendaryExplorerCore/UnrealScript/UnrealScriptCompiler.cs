using System;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript
{
    public static class UnrealScriptCompiler
    {
        public static (ASTNode node, string text) DecompileExport(ExportEntry export, FileLib lib)
        {
            try
            {
                ASTNode astNode = ExportToAstNode(export, lib);

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetOutput());
                }
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return (null, $"Error occured while decompiling {export?.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, "Could not decompile!");
        }

        public static ASTNode ExportToAstNode(ExportEntry export, FileLib lib)
        {
            ASTNode astNode;
            switch (export.ClassName)
            {
                case "Class":
                    astNode = ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(), true, lib);
                    break;
                case "Function":
                    astNode = ScriptObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>(), lib: lib);
                    break;
                case "State":
                    astNode = ScriptObjectToASTConverter.ConvertState(export.GetBinaryData<UState>(), lib: lib);
                    break;
                case "Enum":
                    astNode = ScriptObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>());
                    break;
                case "ScriptStruct":
                    astNode = ScriptObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>());
                    break;
                default:
                    if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export) is UProperty uProp)
                    {
                        astNode = ScriptObjectToASTConverter.ConvertVariable(uProp);
                    }
                    else
                    {
                        astNode = ScriptObjectToASTConverter.ConvertDefaultProperties(export);
                    }

                    break;
            }

            return astNode;
        }

        public static (Function, TokenStream<string>) CompileFunctionBodyAST(ExportEntry parentExport, string scriptText, Function func, MessageLog log, FileLib lib)
        {
            var symbols = lib.GetSymbolTable();
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

                var tokens = CodeBodyParser.ParseFunction(originalFunction, parentExport.Game, scriptText, symbols, log);
                return (originalFunction, tokens);
            }
            //in state
            if (parentExport.Parent is ExportEntry {IsClass: true} classExport && symbols.TryGetType(classExport.ObjectNameString, out Class cls) &&
                cls.States.FirstOrDefault(s => s.Name.CaseInsensitiveEquals(parentExport.ObjectNameString)) is State state &&
                state.Functions.FirstOrDefault(f => f.Name == func.Name) is Function canonicalFunction)
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

                var tokens = CodeBodyParser.ParseFunction(canonicalFunction, parentExport.Game, scriptText, symbols, log);
                return (canonicalFunction, tokens);
            }

            return (null, null);
        }

        public static (ASTNode ast, MessageLog log, TokenStream<string> tokens) CompileAST(string script, string type, MEGame game)
        {
            var log = new MessageLog();
            var tokens = new TokenStream<string>(new StringLexer(script, log));
            var parser = new ClassOutlineParser(tokens, game, log);
            try
            {
                ASTNode ast = parser.ParseDocument(type);
                if (ast is null)
                {
                    log.LogError("Parse failed!");
                }
                else
                {
                    log.LogMessage("Parsed!");
                }
                return (ast, log, tokens);
            }
            catch (Exception e)
            {
                log.LogError($"Parse failed! Exception: {e}");
                return (null, log, tokens);
            }

        }

        public static (ASTNode astNode, MessageLog log) CompileFunctionBody(ExportEntry export, string scriptText, FileLib lib)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game);
            if (astNode != null && log.AllErrors.IsEmpty())
            {
                if (astNode is Function func && lib.IsInitialized && export.Parent is ExportEntry parent)
                {
                    try
                    {
                        (astNode, _) = CompileFunctionBodyAST(parent, scriptText, func, log, lib);
                        if (log.AllErrors.Count > 0)
                        {
                            log.LogError("Parse failed!");
                            return (astNode, log);
                        }
                    }
                    catch (ParseException)
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
                            else
                            {
                                log.LogError("Compilation failed!");
                            }
                            return (astNode, log);
                        }
                        catch (Exception exception) when(!LegendaryExplorerCoreLib.IsDebug)
                        {
                            log.LogError($"Compilation failed! Exception: {exception}");
                            return (astNode, log);
                        }
                    }
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileFunction(ExportEntry export, string scriptText, FileLib lib)
        {
            (ASTNode astNode, MessageLog log, _) = CompileAST(scriptText, export.ClassName, export.Game);
            if (astNode != null && log.AllErrors.IsEmpty())
            {
                if (astNode is Function func && lib.IsInitialized && export.Parent is ExportEntry parent)
                {
                    if (func.IsNative)
                    {
                        log.LogMessage("Cannot edit native functions!");
                        return (astNode, log);
                    }
                    try
                    {
                        (astNode, _) = CompileNewFunctionBodyAST(parent, scriptText, func, log, lib);
                        if (log.AllErrors.Count > 0)
                        {
                            log.LogError("Parse failed!");
                            return (astNode, log);
                        }
                    }
                    catch (ParseException)
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
                            ScriptObjectCompiler.Compile(funcFullAST, parent, export.GetBinaryData<UFunction>());
                            log.LogMessage("Compiled!");
                            return (astNode, log);
                        }
                        catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                        {
                            log.LogError($"Compilation failed! Exception: {exception}");
                            return (astNode, log);
                        }
                    }
                }
            }

            return (null, log);
        }

        public static (Function, TokenStream<string>) CompileNewFunctionBodyAST(ExportEntry parentExport, string scriptText, Function func, MessageLog log, FileLib lib)
        {
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName, out Class containingClass))
            {
                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                int funcIdx = containingClass.Functions.FindIndex(fun => fun.Name.CaseInsensitiveEquals(func.Name));
                if (funcIdx == -1)
                {
                    symbols.AddSymbol(func.Name, func);
                    containingClass.Functions.Add(func);
                }
                else
                {
                    symbols.ReplaceSymbol(func.Name, func, true);
                    containingClass.Functions[funcIdx] = func;
                }

                func.Outer = containingClass;
                var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
                validator.VisitNode(func);
                validator = new ClassValidationVisitor(log, symbols, ValidationPass.BodyPass);
                validator.VisitNode(func);

                var tokens = CodeBodyParser.ParseFunction(func, parentExport.Game, scriptText, symbols, log);
                return (func, tokens);
            }
            //in state
            if (parentExport.Parent is ExportEntry { IsClass: true } classExport && symbols.TryGetType(classExport.ObjectNameString, out Class cls) &&
                cls.States.FirstOrDefault(s => s.Name.CaseInsensitiveEquals(parentExport.ObjectNameString)) is State state)
            {
                symbols.GoDirectlyToStack(((Class)cls.Parent).GetInheritanceString());
                symbols.PushScope(cls.Name);
                symbols.PushScope(state.Name);

                int funcIdx = state.Functions.FindIndex(fun => fun.Name.CaseInsensitiveEquals(func.Name));
                if (funcIdx == -1)
                {
                    symbols.AddSymbol(func.Name, func);
                    state.Functions.Add(func);
                }
                else
                {
                    symbols.ReplaceSymbol(func.Name, func, true);
                    state.Functions[funcIdx] = func;
                }

                func.Outer = state;
                var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
                validator.VisitNode(func);
                validator = new ClassValidationVisitor(log, symbols, ValidationPass.BodyPass);
                validator.VisitNode(func);

                var tokens = CodeBodyParser.ParseFunction(func, parentExport.Game, scriptText, symbols, log);
                return (func, tokens);
            }

            return (null, null);
        }
    }
}
