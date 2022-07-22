using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript
{
    public static class UnrealScriptLookup
    {
        public static List<EntryStringPair> FindUsagesInFile(Function searchFunc, FileLib lib)
        {
            var results = new List<EntryStringPair>();

            lib.ReInitializeFile();
            SymbolTable symbols = lib.GetSymbolTable();

            symbols.RevertToObjectStack();
            if (!symbols.TryGetSymbolFromSpecificScope(searchFunc.Name, out searchFunc, searchFunc.GetOuterScope()))
            {
                results.Add(new EntryStringPair($"Error: could not find definition of'{searchFunc.GetScope()}'. Have you compiled it yet?"));
                return results;
            }

            IMEPackage pcc = lib.Pcc;
            string pccFilePath = pcc.FilePath;
            foreach (VariableType type in symbols.Types)
            {
                if (type.FilePath == pccFilePath)
                {
                    if (type is ObjectType objType)
                    {
                        FindInVars(objType.VariableDeclarations, "field", objType.Name);

                        if (objType is Class cls)
                        {
                            FindInFuncs(cls.Functions, cls.Name);

                            foreach (State state in cls.States)
                            {
                                string stateScope = $"{cls.Name}.{state.Name}";

                                FindInFuncs(state.Functions, stateScope);

                                FindInBytecode(state, stateScope);
                            }
                        }
                    }
                }
            }

            return results;

            void FindInVars(IEnumerable<VariableDeclaration> vars, string varKind, string outerScope)
            {
                foreach (VariableDeclaration varDecl in vars)
                {
                    if (varDecl.VarType is DelegateType { DefaultFunction: Function delFunc} && searchFunc == delFunc)
                    {
                        results.Add(new EntryStringPair(new LEXOpenable(pcc, varDecl.UIndex), $"#{varDecl.UIndex} Default function of delegate {varKind}: '{outerScope}.{varDecl.Name}'"));
                    }
                }
            }

            void FindInFuncs(List<Function> functions, string outerScope)
            {
                foreach (Function clsFunc in functions)
                {
                    string funcScope = $"{outerScope}.{clsFunc.Name}";
                    FindInVars(clsFunc.Parameters, "param", funcScope);
                    FindInVars(clsFunc.Locals, "local", funcScope);

                    FindInBytecode(clsFunc, funcScope);
                }
            }

            void FindInBytecode(IContainsByteCode containsBytecode, string scope)
            {
                string kind = containsBytecode switch
                {
                    Function => "Function",
                    State => "State",
                    _ => "Class"
                };
                ExportEntry export = pcc.GetUExport(containsBytecode.UIndex);
                (_, string script) = UnrealScriptCompiler.DecompileExport(export, lib);
                var log = new MessageLog();
                (ASTNode ast, TokenStream tokens) = UnrealScriptCompiler.CompileOutlineAST(script, kind, log, pcc.Game);
                if (log.HasErrors)
                {
                    return;
                }
                containsBytecode.Body = ((IContainsByteCode)ast).Body;
                containsBytecode.Body.Outer = (ASTNode)containsBytecode;
                symbols.RevertToObjectStack();
                switch (containsBytecode)
                {
                    case Function function:
                        symbols.GoDirectlyToStack(function.GetOuterScope());
                        break;
                    case State state:
                        symbols.GoDirectlyToStack(((Class)state.Outer).GetScope());
                        break;
                    //case Class cls:
                    //    symbols.GoDirectlyToStack(cls.GetScope());
                    //    break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(containsBytecode));
                }
                try
                {
                    switch (containsBytecode)
                    {
                        case Function function:
                            CodeBodyParser.ParseFunction(function, pcc.Game, symbols, log);
                            break;
                        case State state:
                            CodeBodyParser.ParseState(state, pcc.Game, symbols, log, false);
                            break;
                    }
                }
                catch
                {
                    return;
                }
                foreach ((ASTNode definitionNode, int offset, int _) in tokens.DefinitionLinks)
                {
                    if (definitionNode is not Function usedFunc )
                    {
                        continue;
                    }
                    if (searchFunc == usedFunc)
                    {
                        (int line, int col) = tokens.LineLookup.GetLineandColumnFromCharIndex(offset);
                        results.Add(new EntryStringPair(new LEXOpenable(pcc, containsBytecode.UIndex), $"#{containsBytecode.UIndex} {kind} '{scope}' ({line}, {col})\n{GetContext(script, offset)}"));
                    }
                    else if (searchFunc.IsVirtual && searchFunc.Name == usedFunc.Name && searchFunc.SignatureEquals(usedFunc))
                    {
                        Function curFunc = searchFunc.SuperFunction;
                        while (curFunc is not null)
                        {
                            if (curFunc == usedFunc)
                            {
                                (int line, int col) = tokens.LineLookup.GetLineandColumnFromCharIndex(offset);
                                results.Add(new EntryStringPair(new LEXOpenable(pcc, containsBytecode.UIndex), $"#{containsBytecode.UIndex} {kind} '{scope}' ({line}, {col}) (Virtual call)\n{GetContext(script, offset)}"));
                                break;
                            }
                            curFunc = curFunc.SuperFunction;
                        }
                    }
                }
            }
        }

        //currently gets entire line. Is that the best way to do it?
        private static string GetContext(string script, int offset)
        {
            if (offset < 0 || offset >= script.Length)
            {
                return "";
            }
            int start = offset;
            int end = offset;
            for (; start > 0; start--)
            {
                if (script[start] == '\n')
                {
                    start++;
                    break;
                }
            }
            for (; end < script.Length; end++)
            {
                if (script[end] == '\n')
                {
                    break;
                }
            }
            if (start >= end)
            {
                return "";
            }
            return script[start..end];
        }
    }
}
