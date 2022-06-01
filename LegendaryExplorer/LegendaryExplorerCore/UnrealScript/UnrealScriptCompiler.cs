using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Decompiling;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript
{
    public static class UnrealScriptCompiler
    {
        public static (ASTNode node, string text) DecompileExport(ExportEntry export, FileLib lib, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            try
            {
                ASTNode astNode = ExportToAstNode(export, lib, packageCache);

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetOutput());
                }
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return (null, $"Error occured while decompiling {export.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, $"Could not decompile {export.InstancedFullPath}");
        }

        public static ASTNode ExportToAstNode(ExportEntry export, FileLib lib, PackageCache packageCache)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            ASTNode astNode;
            switch (export.ClassName)
            {
                case "Class":
                    astNode = ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(packageCache), true, lib, packageCache);
                    break;
                case "Function":
                    astNode = ScriptObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>(packageCache), lib, packageCache: packageCache);
                    break;
                case "State":
                    astNode = ScriptObjectToASTConverter.ConvertState(export.GetBinaryData<UState>(packageCache), lib, packageCache: packageCache);
                    break;
                case "Enum":
                    astNode = ScriptObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>(packageCache));
                    break;
                case "ScriptStruct":
                    astNode = ScriptObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>(packageCache), true, lib, packageCache);
                    break;
                default:
                    if (export.ClassName.EndsWith("Property") && ObjectBinary.From(export, packageCache) is UProperty uProp)
                    {
                        astNode = ScriptObjectToASTConverter.ConvertVariable(uProp, lib, packageCache);
                    }
                    else
                    {
                        astNode = ScriptObjectToASTConverter.ConvertDefaultProperties(export, lib, packageCache);
                    }
                    break;
            }
            return astNode;
        }

        public static (ASTNode ast, TokenStream tokens) CompileOutlineAST(string script, string type, MessageLog log, MEGame game, bool isDefaultObject = false)
        {
            var tokens = new TokenStream(StringLexer.Lex(script, log));
            var parser = new ClassOutlineParser(tokens, game, log);
            try
            {
                ASTNode ast = isDefaultObject ? parser.ParseDefaultProperties() : parser.ParseDocument(type);
                if (ast is null)
                {
                    log.LogError("Parse failed!");
                }
                else
                {
                    log.LogMessage("Parsed!");
                }
                return (ast, tokens);
            }
            catch (ParseException)
            {
                log.LogError("Parse failed!");
                return (null, tokens);
            }
            catch (Exception e)
            {
                log.LogError($"Parse failed! Exception: {e}");
                return (null, tokens);
            }

        }

        //Used by M3. Do not delete
        public static MessageLog AddOrReplaceInClass(ExportEntry classExport, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            IMEPackage pcc = classExport.FileRef;
            if (!ReferenceEquals(lib.Pcc, pcc))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            if (!classExport.IsClass)
            {
                throw new ArgumentException($"Expected '{classExport.InstancedFullPath}' to be a class definition export!", nameof(classExport));
            }
            var log = new MessageLog();
            if (!lib.IsInitialized)
            {
                log.LogError("FileLib not initialized!");
                return log;
            }
            (ASTNode decompedNode, string classSource) = DecompileExport(classExport, lib, packageCache);
            if (decompedNode is null)
            {
                log.LogError(classSource);
                return log;
            }
            (ASTNode classAST, _) = CompileOutlineAST(classSource, "Class", log, pcc.Game);
            if (log.HasErrors || classAST is not Class cls)
            {
                log.LogError($"Failed to parse class {classExport.InstancedFullPath}");
                return log;
            }

            try
            {
                var tokens = new TokenStream(StringLexer.Lex(scriptText, log));
                var parser = new ClassOutlineParser(tokens, pcc.Game, log);
                ASTNode astNode = parser.ParseDocument();
                if (astNode is null || log.HasErrors)
                {
                    log.LogError("Parse failed!");
                    return log;
                }
                switch (astNode)
                {
                    case Enumeration enumeration:
                        cls.TypeDeclarations.ReplaceFirstOrAdd(t => t is Enumeration && t.Name.CaseInsensitiveEquals(enumeration.Name), enumeration);
                        break;
                    case Struct @struct:
                        cls.TypeDeclarations.ReplaceFirstOrAdd(t => t is Struct && t.Name.CaseInsensitiveEquals(@struct.Name), @struct);
                        break;
                    case VariableDeclaration varDecl:
                        cls.VariableDeclarations.ReplaceFirstOrAdd(v => v.Name.CaseInsensitiveEquals(varDecl.Name), varDecl);
                        break;
                    case Function func:
                        cls.Functions.ReplaceFirstOrAdd(f => f.Name.CaseInsensitiveEquals(func.Name), func);
                        break;
                    case State state:
                        cls.States.ReplaceFirstOrAdd(s => s.Name.CaseInsensitiveEquals(state.Name), state);
                        break;
                    case DefaultPropertiesBlock propsBlock:
                        //cls.DefaultProperties = propsBlock;
                        log.LogError("Replacing default properties is not permitted at this time.");
                        return log;
                    case Class://support whole-class replacement?
                        log.LogError("Replacing an entire class is not permitted at this time.");
                        return log;
                    case Const:
                        log.LogError("Adding or replacing a const is not permitted.");
                        return log;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(astNode));
                }

                classAST = CompileNewClassAST(pcc, cls, log, lib, out _);
                if (classAST is null || log.HasErrors)
                {
                    log.LogError("Parse failed!");
                    return log;
                }
            }
            catch (ParseException)
            {
                log.LogError("Parse failed!");
                return log;
            }
            catch (Exception exception)
            {
                log.LogError($"Parse failed! Exception: {exception}");
                return log;
            }
            try
            {
                ScriptObjectCompiler.Compile(classAST, pcc, classExport.Parent, classExport.GetBinaryData<UClass>(), packageCache);
                log.LogMessage("Compiled!");
            }
            catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                log.LogError($"Compilation failed! Exception: {exception}");
            }
            return log;
        }

        public static (ASTNode astNode, MessageLog log) CompileClass(IMEPackage pcc, string scriptText, FileLib lib, ExportEntry export = null, IEntry parent = null, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(lib.Pcc, pcc))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, "Class", log, pcc.Game);
            if (!log.HasErrors)
            {
                if (astNode is not Class cls)
                {
                    log.LogError("Tried to parse a Class, but no Class was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewClassAST(pcc, cls, log, lib, out bool vfTableChanged);
                    if (astNode is null || log.HasErrors)
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
                try
                {
                    ScriptObjectCompiler.Compile(astNode, pcc, parent, export?.GetBinaryData<UClass>(), packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        //Used by M3. Do not change signature without good cause
        public static (ASTNode astNode, MessageLog log) CompileFunction(ExportEntry export, string scriptText, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game);
            if (astNode != null && !log.HasErrors)
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
                        astNode = CompileNewFunctionBodyAST(parent, func, log, lib);
                        if (log.HasErrors)
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
                            ScriptObjectCompiler.Compile(funcFullAST, export.FileRef, parent, export.GetBinaryData<UFunction>());
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

        public static (ASTNode astNode, MessageLog log) CompileState(ExportEntry export, string scriptText, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game);
            if (!log.HasErrors)
            {
                if (astNode is not State state)
                {
                    log.LogError("Tried to parse a State, but no State was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Parent is not ExportEntry {IsClass: true} parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export as a parent!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewStateBodyAST(parent, state, log, lib);
                    if (astNode is null || log.HasErrors)
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
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, export.GetBinaryData<UState>());
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileEnum(ExportEntry export, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game);
            if (!log.HasErrors)
            {
                if (astNode is not Enumeration enumeration)
                {
                    log.LogError("Tried to parse an Enum, but no Enum was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Parent is not ExportEntry { IsClass: true } parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export as a parent!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewEnumAST(parent, enumeration, log, lib);
                    if (astNode is null || log.HasErrors)
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
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, export.GetBinaryData<UEnum>(), packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileStruct(ExportEntry export, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game);
            if (!log.HasErrors)
            {
                if (astNode is not Struct strct)
                {
                    log.LogError("Tried to parse a Struct, but no Struct was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Parent is not ExportEntry { ClassName: "Class" or "ScriptStruct" } parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class or ScriptStruct Export as a parent!");
                    return (null, log);
                }
                if (strct.IsNative)
                {
                    log.LogMessage("Cannot edit native structs!");
                    return (astNode, log);
                }
                try
                {
                    astNode = CompileNewStructAST(parent, strct, log, lib);
                    if (astNode is null || log.HasErrors)
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
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, export.GetBinaryData<UScriptStruct>(), packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static (ASTNode astNode, MessageLog log) CompileDefaultProperties(ExportEntry export, string scriptText, FileLib lib, PackageCache packageCache = null)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game, true);
            if (!log.HasErrors)
            {
                if (astNode is not DefaultPropertiesBlock propBlock)
                {
                    log.LogError($"Tried to parse a {DEFAULTPROPERTIES}, but no {DEFAULTPROPERTIES} was found!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }
                if (export.Class is not ExportEntry { IsClass: true } classExport)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileDefaultPropertiesAST(classExport, propBlock, log, lib);
                    if (astNode is null || log.HasErrors)
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
                try
                {
                    ScriptPropertiesCompiler.CompileDefault__Object(propBlock, classExport, ref export, packageCache);
                    log.LogMessage("Compiled!");
                    return (astNode, log);
                }
                catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
                {
                    log.LogError($"Compilation failed! Exception: {exception}");
                    return (astNode, log);
                }
            }

            return (null, log);
        }

        public static Class CompileNewClassAST(IMEPackage pcc, Class cls, MessageLog log, FileLib lib, out bool vfTableChanged)
        {
            if (!ReferenceEquals(lib.Pcc, pcc))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            if (cls.Name.CaseInsensitiveEquals("Object"))
            {
                throw new Exception("Cannot compile the root Object class!");
            }
            vfTableChanged = false;
            //get the old version of this class, if it exists
            if (lib.ReadonlySymbolTable.TryGetType(cls.Name, out Class existingClass))
            {
                foreach (Struct existingNativeStruct in existingClass.TypeDeclarations.OfType<Struct>().Where(s => s.IsNative))
                {
                    if (cls.TypeDeclarations.FirstOrDefault(t => t.Name == existingNativeStruct.Name) is Struct newStruct 
                        && !existingNativeStruct.IsNativeCompatibleWith(newStruct, pcc.Game))
                    {
                        log.LogError($"Cannot modify native struct: {existingNativeStruct.Name}", newStruct.StartPos, newStruct.EndPos);
                    }
                }
            }
            log.Filter = cls;
            SymbolTable symbols = lib.CreateSymbolTableWithClass(cls, log);
            log.Filter = null;
            if (symbols is null || log.HasErrors)
            {
                return null;
            }
            symbols.RevertToObjectStack();
            symbols.GoDirectlyToStack(cls.GetScope());
            foreach (Struct childStruct in cls.TypeDeclarations.OfType<Struct>())
            {
                PropertiesBlockParser.ParseStructDefaults(childStruct, pcc, symbols, log);
            }
            foreach (Function func in cls.Functions)
            {
                CodeBodyParser.ParseFunction(func, pcc.Game, symbols, log);
            }
            foreach (State state in cls.States)
            {
                CodeBodyParser.ParseState(state, pcc.Game, symbols, log);
            }
            PropertiesBlockParser.Parse(cls.DefaultProperties, pcc, symbols, log);

            //calculate the virtual function table
            if (pcc.Game.IsGame3())
            {
                var virtualFuncs = cls.Functions.Where(func => func.IsVirtual).ToList();
                var funcDict = virtualFuncs.ToDictionary(func => func.Name);
                List<string> parentVirtualFuncNames = ((Class)cls.Parent).VirtualFunctionNames;
                var overrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string funcName in parentVirtualFuncNames)
                {
                    if (funcDict.Remove(funcName, out Function func))
                    {
                        overrides.Add(funcName);
                        virtualFuncs.Remove(func);
                    }
                }
                cls.VirtualFunctionNames = parentVirtualFuncNames.Concat(virtualFuncs.Select(func => func.Name)).ToList();

                if (existingClass is not null)
                {
                    var existingNames = new HashSet<string>(existingClass.VirtualFunctionNames, StringComparer.OrdinalIgnoreCase);
                    if (existingNames.SetEquals(cls.VirtualFunctionNames))
                    {
                        //same functions, so preserve the ordering 
                        cls.VirtualFunctionNames = existingClass.VirtualFunctionNames;

                        //check to see if overrides have changed
                        var existingOverrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var existingFuncDict = existingClass.Functions.Where(func => func.IsVirtual).ToDictionary(func => func.Name);
                        foreach (string funcName in parentVirtualFuncNames)
                        {
                            if (existingFuncDict.Remove(funcName, out Function func))
                            {
                                existingOverrides.Add(funcName);
                            }
                        }
                        if (!overrides.SetEquals(existingOverrides))
                        {
                            vfTableChanged = true;
                        }
                    }
                    else
                    {
                        existingNames.ExceptWith(cls.VirtualFunctionNames);
                        vfTableChanged = true;
                    }
                }

                cls.VirtualFunctionTable = cls.VirtualFunctionNames.Select(funcName => symbols.TryGetSymbol(funcName, out Function func) ? func : throw new Exception($"'{funcName}' not found on class!")).ToList();
            }

            return cls;
        }

        public static Function CompileNewFunctionBodyAST(ExportEntry parentExport, Function func, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, parentExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            IContainsFunctions stateOrClass;

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName.Instanced, out Class containingClass))
            {
                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                stateOrClass = containingClass;
            }
            //in state
            else if (parentExport.Parent is ExportEntry { IsClass: true } classExport && symbols.TryGetType(classExport.ObjectNameString, out Class cls) &&
                cls.States.FirstOrDefault(s => s.Name.CaseInsensitiveEquals(parentExport.ObjectNameString)) is State state)
            {
                symbols.GoDirectlyToStack(((Class)cls.Parent).GetInheritanceString());
                symbols.PushScope(cls.Name);
                symbols.PushScope(state.Name);

                stateOrClass = state;
            }
            else
            {
                return null;
            }

            int funcIdx = stateOrClass.Functions.FindIndex(fun => fun.Name.CaseInsensitiveEquals(func.Name));
            if (funcIdx == -1)
            {
                symbols.AddSymbol(func.Name, func);
                stateOrClass.Functions.Add(func);
            }
            else
            {
                symbols.ReplaceSymbol(func.Name, func, true);
                stateOrClass.Functions[funcIdx] = func;
            }

            func.Outer = (ASTNode)stateOrClass;
            var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams);
            validator.VisitNode(func);
            validator.Pass = ValidationPass.BodyPass;
            validator.VisitNode(func);

            CodeBodyParser.ParseFunction(func, parentExport.Game, symbols, log);
            return func;
        }

        public static State CompileNewStateBodyAST(ExportEntry parentExport, State state, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, parentExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            if (parentExport.IsClass && symbols.TryGetType(parentExport.ObjectName.Instanced, out Class containingClass))
            {
                if (!containingClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)containingClass.Parent).GetInheritanceString());
                    symbols.PushScope(containingClass.Name);
                }

                int stateIdx = containingClass.States.FindIndex(s => s.Name.CaseInsensitiveEquals(state.Name));
                if (stateIdx == -1)
                {
                    containingClass.States.Add(state);
                }
                else
                {
                    symbols.RemoveSymbol(state.Name);
                    containingClass.States[stateIdx] = state;
                }

                state.Outer = containingClass;
                ClassValidationVisitor.RunAllPasses(state, log, symbols);
                CodeBodyParser.ParseState(state, parentExport.Game, symbols, log);

                return state;
            }

            return null;
        }

        public static Enumeration CompileNewEnumAST(ExportEntry parentExport, Enumeration enumeration, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, parentExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();
            if (symbols.TryGetType(parentExport.ObjectName.Instanced, out ObjectType containingObject))
            {
                if (!containingObject.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(containingObject.GetScope());
                }
                symbols.RemoveSymbol(enumeration.Name);
                symbols.RemoveTypeAndChildTypes(enumeration);

                int enumIdx = containingObject.TypeDeclarations.FindIndex(e => e is Enumeration && e.Name.CaseInsensitiveEquals(enumeration.Name));
                if (enumIdx == -1)
                {
                    containingObject.TypeDeclarations.Add(enumeration);
                }
                else
                {
                    containingObject.TypeDeclarations[enumIdx] = enumeration;
                }

                enumeration.Outer = containingObject;
                ClassValidationVisitor.RunAllPasses(enumeration, log, symbols);

                return enumeration;
            }

            return null;
        }

        public static VariableDeclaration CompileNewVarDeclAST(ExportEntry parentExport, VariableDeclaration varDecl, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, parentExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();
            if (symbols.TryGetType(parentExport.ObjectName.Instanced, out ObjectType containingObject))
            {
                if (!containingObject.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(containingObject.GetScope());
                }
                symbols.RemoveSymbol(varDecl.Name);

                int enumIdx = containingObject.VariableDeclarations.FindIndex(v => v.Name.CaseInsensitiveEquals(varDecl.Name));
                if (enumIdx == -1)
                {
                    containingObject.VariableDeclarations.Add(varDecl);
                }
                else
                {
                    containingObject.VariableDeclarations[enumIdx] = varDecl;
                }

                varDecl.Outer = containingObject;
                ClassValidationVisitor.RunAllPasses(varDecl, log, symbols);

                return varDecl;
            }

            return null;
        }

        public static Struct CompileNewStructAST(ExportEntry parentExport, Struct strct, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, parentExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();
            if (symbols.TryGetType(parentExport.ObjectName.Instanced, out ObjectType containingObject))
            {
                if (!containingObject.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(containingObject.GetScope());
                }
                symbols.RemoveSymbol(strct.Name);
                symbols.RemoveTypeAndChildTypes(strct);

                int structIdx = containingObject.TypeDeclarations.FindIndex(s => s is Struct && s.Name.CaseInsensitiveEquals(strct.Name));
                if (structIdx == -1)
                {
                    containingObject.TypeDeclarations.Add(strct);
                }
                else
                {
                    containingObject.TypeDeclarations[structIdx] = strct;
                }

                strct.Outer = containingObject;
                ClassValidationVisitor.RunAllPasses(strct, log, symbols);
                PropertiesBlockParser.ParseStructDefaults(strct, parentExport.FileRef, symbols, log);
                return strct;
            }

            return null;
        }

        public static DefaultPropertiesBlock CompileDefaultPropertiesAST(ExportEntry classExport, DefaultPropertiesBlock propBlock, MessageLog log, FileLib lib)
        {
            if (!ReferenceEquals(lib.Pcc, classExport.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            SymbolTable symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();


            if (classExport.IsClass && symbols.TryGetType(classExport.ObjectName.Instanced, out Class propsClass))
            {
                if (!propsClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)propsClass.Parent).GetInheritanceString());
                    symbols.PushScope(propsClass.Name);
                }

                propsClass.DefaultProperties = propBlock;
                propBlock.Outer = propsClass;

                PropertiesBlockParser.Parse(propBlock, classExport.FileRef, symbols, log);

                return propBlock;
            }

            return null;
        }
    }
}
