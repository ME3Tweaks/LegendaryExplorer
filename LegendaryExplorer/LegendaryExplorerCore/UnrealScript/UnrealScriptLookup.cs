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
                            FindInBytecode(searchFunc, cls, cls.Name, lib, symbols, results);

                            foreach (State state in cls.States)
                            {
                                string stateScope = $"{cls.Name}.{state.Name}";

                                FindInFuncs(state.Functions, stateScope);

                                FindInBytecode(searchFunc, state, stateScope, lib, symbols, results);
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
                foreach (Function func in functions)
                {
                    string funcScope = $"{outerScope}.{func.Name}";
                    FindInVars(func.Parameters, "param", funcScope);

                    if (func.ReturnType is DelegateType { DefaultFunction: Function delFunc } && searchFunc == delFunc)
                    {
                        results.Add(new EntryStringPair(new LEXOpenable(pcc, func.UIndex), $"#{func.UIndex} Default function of delegate return type: '{funcScope}'"));
                    }

                    FindInBytecode(searchFunc, func, funcScope, lib, symbols, results);
                }
            }
        }
        public static List<EntryStringPair> FindUsagesInFile(VariableDeclaration searchDecl, FileLib lib)
        {
            var results = new List<EntryStringPair>();

            lib.ReInitializeFile();
            SymbolTable symbols = lib.GetSymbolTable();

            symbols.RevertToObjectStack();
            if (!symbols.TryGetSymbolFromSpecificScope(searchDecl.Name, out searchDecl, GetOuterScope(searchDecl)))
            {
                results.Add(new EntryStringPair($"Error: could not find definition of'{GetOuterScope(searchDecl)}.{searchDecl.Name}'. Have you compiled it yet?"));
                return results;
            }

            IMEPackage pcc = lib.Pcc;
            string pccFilePath = pcc.FilePath;
            foreach (VariableType type in symbols.Types)
            {
                if (type.FilePath == pccFilePath)
                {
                    if (type is Class cls)
                    {
                        FindInFuncs(cls.Functions, cls.Name);
                        FindInBytecode(searchDecl, cls, cls.Name, lib, symbols, results);

                        foreach (State state in cls.States)
                        {
                            string stateScope = $"{cls.Name}.{state.Name}";

                            FindInFuncs(state.Functions, stateScope);

                            FindInBytecode(searchDecl, state, stateScope, lib, symbols, results);
                        }
                    }
                }
            }

            return results;

            void FindInFuncs(List<Function> functions, string outerScope)
            {
                foreach (Function clsFunc in functions)
                {
                    string funcScope = $"{outerScope}.{clsFunc.Name}";

                    FindInBytecode(searchDecl, clsFunc, funcScope, lib, symbols, results);
                }
            }

            string GetOuterScope(VariableDeclaration varDecl) =>
                varDecl.Outer switch
                {
                    ObjectType objType => objType.GetScope(),
                    Function func => func.GetScope(),
                    _ => throw new Exception("Unexpected outer type!")
                };
        }

        public static List<EntryStringPair> FindUsagesInFile(VariableType searchType, FileLib lib)
        {
            var results = new List<EntryStringPair>();

            lib.ReInitializeFile();
            SymbolTable symbols = lib.GetSymbolTable();

            symbols.RevertToObjectStack();
            if (symbols.GoDirectlyToStack(searchType.GetScope()))
            {
                symbols.PopScope();
            }
            if (!symbols.TryResolveType(ref searchType, searchType is Class))
            {
                results.Add(new EntryStringPair($"Error: could not find definition of'{searchType.GetScope()}'. Have you compiled it yet?"));
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
                        FindInVars(objType.VariableDeclarations, "type of field:", objType.Name);
                        if (objType.Parent == searchType)
                        {
                            results.Add(new EntryStringPair(new LEXOpenable(pcc, objType.UIndex), $"#{objType.UIndex} Super of class: '{objType.Name}'"));
                        }
                        if (objType is Class cls)
                        {
                            if (cls._outerClass == searchType)
                            {
                                results.Add(new EntryStringPair(new LEXOpenable(pcc, cls.UIndex), $"#{cls.UIndex} Outer of class: '{cls.Name}'"));
                            }
                            foreach (VariableType @interface in cls.Interfaces)
                            {
                                if (@interface == searchType)
                                {
                                    results.Add(new EntryStringPair(new LEXOpenable(pcc, cls.UIndex), $"#{cls.UIndex} Implemented by class: '{cls.Name}'"));
                                }
                            }

                            FindInBytecode(searchType, cls, cls.Name, lib, symbols, results);
                            FindInFuncs(cls.Functions, cls.Name);

                            foreach (State state in cls.States)
                            {
                                string stateScope = $"{cls.Name}.{state.Name}";

                                FindInFuncs(state.Functions, stateScope);

                                FindInBytecode(searchType, state, stateScope, lib, symbols, results);
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
                    if (searchType == varDecl.VarType switch
                        {
                            StaticArrayType staticArrayType => staticArrayType.ElementType,
                            ClassType classType => classType.ClassLimiter,
                            DynamicArrayType dynArr => dynArr.ElementType,
                            _ => varDecl.VarType
                        })
                    {
                        results.Add(new EntryStringPair(new LEXOpenable(pcc, varDecl.UIndex), $"#{varDecl.UIndex} {varKind} '{outerScope}.{varDecl.Name}'"));
                    }
                }
            }

            void FindInFuncs(List<Function> functions, string outerScope)
            {
                foreach (Function func in functions)
                {
                    string funcScope = $"{outerScope}.{func.Name}";
                    FindInVars(func.Parameters, "type of param:", funcScope);

                    if (searchType == func.ReturnType switch
                        {
                            StaticArrayType staticArrayType => staticArrayType.ElementType,
                            ClassType classType => classType.ClassLimiter,
                            DynamicArrayType dynArr => dynArr.ElementType,
                            _ => func.ReturnType
                        })
                    {
                        results.Add(new EntryStringPair(new LEXOpenable(pcc, func.UIndex), $"#{func.UIndex} Return type of: '{funcScope}'"));
                    }

                    FindInBytecode(searchType, func, funcScope, lib, symbols, results);
                }
            }
        }

        private static void FindInBytecode(ASTNode search, IContainsByteCode containsBytecode, string scope, FileLib lib, SymbolTable symbols, List<EntryStringPair> results)
        {
            string kind = containsBytecode switch
            {
                Function => "Function",
                State => "State",
                _ => "Class"
            };
            IMEPackage pcc = lib.Pcc;
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
                case Class cls:
                    symbols.GoDirectlyToStack(cls.GetScope());
                    break;
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
                    case Class cls:
                        CodeBodyParser.ParseReplicationBlock(cls, pcc.Game, symbols, log);
                        break;
                }
            }
            catch
            {
                return;
            }
            switch (search)
            {
                case Function searchFunc:
                {
                    foreach ((ASTNode definitionNode, int offset, int _) in tokens.DefinitionLinks)
                    {
                        if (definitionNode is not Function usedFunc)
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
                    break;
                }
                case VariableDeclaration:
                case VariableType:
                {
                    foreach ((ASTNode definitionNode, int offset, int _) in tokens.DefinitionLinks)
                    {
                        if (search == definitionNode)
                        {
                            (int line, int col) = tokens.LineLookup.GetLineandColumnFromCharIndex(offset);
                            results.Add(new EntryStringPair(new LEXOpenable(pcc, containsBytecode.UIndex), $"#{containsBytecode.UIndex} {kind} '{scope}' ({line}, {col})\n{GetContext(script, offset)}"));
                        }
                    }
                    break;
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
