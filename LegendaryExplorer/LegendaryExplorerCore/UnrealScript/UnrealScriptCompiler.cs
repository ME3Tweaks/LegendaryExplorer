using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using LegendaryExplorerCore.Gammtek.Extensions.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
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
using Microsoft.Toolkit.HighPerformance;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript
{
    public static class UnrealScriptCompiler
    {
        public static (ASTNode node, string text) DecompileExport(ExportEntry export, FileLib lib, UnrealScriptOptionsPackage usop)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            try
            {
                ASTNode astNode = ExportToAstNode(export, lib, usop);

                if (astNode != null)
                {
                    var codeBuilder = new CodeBuilderVisitor();
                    astNode.AcceptVisitor(codeBuilder);
                    return (astNode, codeBuilder.GetOutput());
                }
            }
            catch (Exception e) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                return (null, $"Error occurred while decompiling {export.InstancedFullPath}:\n\n{e.FlattenException()}");
            }

            return (null, $"Could not decompile {export.InstancedFullPath}");
        }

        public static string GetPropertyLiteralValue(Property prop, ExportEntry containingExport, FileLib lib, UnrealScriptOptionsPackage usop)
        {
            Expression literal = ScriptObjectToASTConverter.ConvertToLiteralValue(prop, containingExport, lib);
            return CodeBuilderVisitor.GetOutput(literal);
        }

        [CanBeNull]
        //Used by M3. Do not delete
        public static Property CompileProperty(string propName, string valueliteral, ExportEntry containingExport, FileLib lib, MessageLog log, UnrealScriptOptionsPackage usop)
        {
            if (!lib.IsInitialized)
            {
                log.LogError("FileLib not initialized!");
                return null;
            }
            try
            {
                var fauxDefaultProperties = $"{DEFAULTPROPERTIES}{{{propName}={valueliteral}}}";
                TokenStream tokens = Lexer.Lex(fauxDefaultProperties, log);
                if (log.HasLexErrors)
                {
                    log.LogError("Lexing failed!");
                    return null;
                }
                DefaultPropertiesBlock node = new ClassOutlineParser(tokens, containingExport.Game, log).ParseDefaultProperties();
                SymbolTable symbolTable = lib.GetSymbolTable();
                if (!symbolTable.TryGetType(containingExport.ClassName, out Class exportClass))
                {
                    log.LogError($"FileLib did not contain definition of class: '{exportClass.Name}'");
                    return null;
                }
                node.Outer = exportClass;
                PropertiesBlockParser.Parse(node, containingExport.FileRef, symbolTable, log, containingExport.IsInDefaultsTree(), usop);
                if (log.HasErrors)
                {
                    log.LogError("Parse failed!");
                    return null;
                }
                PropertyCollection props = ScriptPropertiesCompiler.CompileProps(node, containingExport.FileRef, usop);
                return props[0];
            }
            catch (ParseException)
            {
                log.LogError("Parse failed!");
                return null;
            }
            catch (Exception e)
            {
                log.LogError($"Parse failed! Exception: {e}");
                return null;
            }
        }

        public static ASTNode ExportToAstNode(ExportEntry export, FileLib lib, UnrealScriptOptionsPackage usop)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            ASTNode astNode = export.ClassName switch
            {
                "Class" => ScriptObjectToASTConverter.ConvertClass(export.GetBinaryData<UClass>(usop?.Cache), true, lib, usop),
                "Function" => ScriptObjectToASTConverter.ConvertFunction(export.GetBinaryData<UFunction>(usop?.Cache), lib, usop),
                "State" => ScriptObjectToASTConverter.ConvertState(export.GetBinaryData<UState>(usop?.Cache), lib, usop),
                "Enum" => ScriptObjectToASTConverter.ConvertEnum(export.GetBinaryData<UEnum>(usop?.Cache)),
                "ScriptStruct" => ScriptObjectToASTConverter.ConvertStruct(export.GetBinaryData<UScriptStruct>(usop?.Cache), lib, usop),
                "Const" => ScriptObjectToASTConverter.ConvertConst(export.GetBinaryData<UConst>(usop?.Cache)),
                _ when export.ClassName.EndsWith("Property") && ObjectBinary.From(export, usop?.Cache) is UProperty uProp => ScriptObjectToASTConverter.ConvertVariable(uProp, lib, usop),
                _ => ScriptObjectToASTConverter.ConvertExportProperties(export, lib, usop)
            };
            return astNode;
        }

        public static (ASTNode ast, TokenStream tokens) CompileOutlineAST(string script, string type, MessageLog log, MEGame game)
        {
            var tokens = Lexer.Lex(script, log);
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

        public static MessageLog CompileBulkPropertiesFile(string src, IMEPackage pcc, UnrealScriptOptionsPackage usop)
        {
            var log = new MessageLog();
            TokenStream tokens = Lexer.Lex(src, log);
            if (log.HasLexErrors)
            {
                return log;
            }
            var lib = new FileLib(pcc);
            if (!lib.Initialize(usop))
            {
                return lib.InitializationLog;
            }
            SymbolTable symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();
            List<Subobject> subobjects;
            try
            {
                subobjects = PropertiesBlockParser.ParseBulkPropsFile(tokens, pcc, symbols, log, false, usop);
            }
            catch (ParseException)
            {
                log.LogError("Parse failed!");
                return log;
            }
            catch (Exception e)
            {
                log.LogError($"Parse failed! Exception: {e}");
                return log;
            }
            if (log.HasErrors) return log;

            var map = new Dictionary<Subobject, ExportEntry>(subobjects.Count);
            foreach (Subobject obj in subobjects)
            {
                IEntry entry = pcc.FindEntry(obj.NameDeclaration, obj.Class.Name);
                if (entry is ExportEntry export)
                {
                    if (!map.TryAdd(obj, export))
                    {
                        log.LogError($"Duplicate declaration for object of class '{obj.Class.Name}' with full path '{obj.NameDeclaration}'", obj.StartPos);
                    }
                }
                else
                {
                    log.LogError($"Could not find an export of class '{obj.Class.Name}' with full path '{obj.NameDeclaration}'", obj.StartPos);
                }
            }
            if (log.HasErrors) return log;

            try
            {
                foreach ((Subobject obj, ExportEntry export) in map)
                {
                    ScriptPropertiesCompiler.CompilePropertiesForNormalObject(obj.Statements, export, usop);
                }
            }
            catch (Exception e)
            {
                log.LogError($"Exception: {e}");
                return log;
            }

            return log;
        }

        public static string DecompileBulkProps(IMEPackage pcc, out MessageLog log, UnrealScriptOptionsPackage usop)
        {
            var fileLib = new FileLib(pcc);
            if (!fileLib.Initialize(usop))
            {
                log = fileLib.InitializationLog;
                return null;
            }
            var ifps = new HashSet<(string, string)>(pcc.ExportCount);
            var codeBuilder = new CodeBuilderVisitor<PlainTextStringBuilderCodeFormatter>();
            log = new MessageLog();
            foreach (ExportEntry export in pcc.Exports.Where(exp => !exp.IsScriptExport() && !exp.IsInDefaultsTree() && !exp.IsTrash()))
            {
                string exportClassName = export.ClassName;
                string ifp = export.InstancedFullPath;
                if (!ifps.Add((ifp, exportClassName)))
                {
                    log.LogError($"Two exports have the same path and class: {ifp} ({exportClassName})");
                    return null;
                }
                ASTNode ast = ExportToAstNode(export, fileLib, usop);
                if (ast is not DefaultPropertiesBlock propsBlock)
                {
                    log.LogError($"Error decompiling #{export.UIndex} {ifp}");
                    return null;
                }
                codeBuilder.AppendToNewLine($"//#{export.UIndex}");
                ast = new Subobject(ifp, new Class(exportClassName, null, null, default), propsBlock.Statements);
                ast.AcceptVisitor(codeBuilder);
                codeBuilder.AppendToNewLine();
            }

            return codeBuilder.GetOutput();
        }

        //Used by M3. Do not delete
        public static MessageLog AddOrReplaceInClass(ExportEntry classExport, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
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
            (ASTNode decompedNode, string classSource) = DecompileExport(classExport, lib, usop);
            if (decompedNode is null)
            {
                log.LogError(classSource);
                return log;
            }
            (ASTNode classAST, _) = CompileOutlineAST(classSource, "Class", log, pcc.Game);
            if (log.HasErrors || log.HasLexErrors || classAST is not Class cls)
            {
                log.LogError($"Failed to parse class {classExport.InstancedFullPath}");
                return log;
            }

            try
            {
                var tokens = Lexer.Lex(scriptText, log);
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

                classAST = CompileNewClassAST(pcc, cls, log, lib, out _, usop);
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
                ScriptObjectCompiler.Compile(classAST, pcc, classExport.Parent, usop, classExport.GetBinaryData<UClass>());
                log.LogMessage("Compiled!");
            }
            catch (Exception exception) when (!LegendaryExplorerCoreLib.IsDebug)
            {
                log.LogError($"Compilation failed! Exception: {exception}");
            }
            return log;
        }

        public static (ASTNode astNode, MessageLog log) CompileClass(IMEPackage pcc, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop, ExportEntry export = null, IEntry parent = null, PackageCache packageCache = null, string intendedClassName = null)
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
                if (intendedClassName is not null && !cls.Name.CaseInsensitiveEquals(intendedClassName))
                {
                    log.LogError($"Class name was '{cls.Name}', expected '{intendedClassName}'!");
                    return (null, log);
                }
                if (!lib.IsInitialized)
                {
                    log.LogError("FileLib not initialized!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewClassAST(pcc, cls, log, lib, out bool vfTableChanged, usop);
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
                if (log.HasErrors || log.HasLexErrors)
                {
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, pcc, parent, usop, export?.GetBinaryData<UClass>());
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
        public static (ASTNode astNode, MessageLog log) CompileFunction(ExportEntry export, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
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
                        log.LogError("Cannot edit native functions!");
                        return (astNode, log);
                    }
                    try
                    {
                        astNode = CompileNewFunctionBodyAST(parent, func, log, lib, usop);
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
                        if (log.HasErrors || log.HasLexErrors)
                        {
                            return (astNode, log);
                        }
                        try
                        {
                            ScriptObjectCompiler.Compile(funcFullAST, export.FileRef, parent, usop, export.GetBinaryData<UFunction>());
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

        public static (ASTNode astNode, MessageLog log) CompileState(ExportEntry export, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
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
                if (export.Parent is not ExportEntry { IsClass: true } parent)
                {
                    log.LogError(export.InstancedFullPath + " does not have a Class Export as a parent!");
                    return (null, log);
                }

                try
                {
                    astNode = CompileNewStateBodyAST(parent, state, log, lib, usop);
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
                if (log.HasErrors || log.HasLexErrors)
                {
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, usop, export.GetBinaryData<UState>());
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

        public static (ASTNode astNode, MessageLog log) CompileEnum(ExportEntry export, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
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
                    astNode = CompileNewEnumAST(parent, enumeration, log, lib, usop);
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
                if (log.HasErrors || log.HasLexErrors)
                {
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, usop, export.GetBinaryData<UEnum>());
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

        public static (ASTNode astNode, MessageLog log) CompileStruct(ExportEntry export, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
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
                    astNode = CompileNewStructAST(parent, strct, log, lib, usop);
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
                if (log.HasErrors || log.HasLexErrors)
                {
                    return (astNode, log);
                }
                try
                {
                    ScriptObjectCompiler.Compile(astNode, export.FileRef, parent, usop, export.GetBinaryData<UScriptStruct>());
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

        public static (ASTNode astNode, MessageLog log) CompileDefaultProperties(ExportEntry export, string scriptText, FileLib lib, UnrealScriptOptionsPackage usop)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            var log = new MessageLog();
            (ASTNode astNode, _) = CompileOutlineAST(scriptText, export.ClassName, log, export.Game);
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

                try
                {
                    astNode = CompileDefaultPropertiesAST(propBlock, log, lib, export, usop);
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
                if (log.HasErrors || log.HasLexErrors)
                {
                    return (astNode, log);
                }
                try
                {
                    if (export.IsDefaultObject)
                    {
                        if (export.Class is not ExportEntry { IsClass: true } classExport)
                        {
                            log.LogError(export.InstancedFullPath + " does not have a Class Export!");
                            return (null, log);
                        }
                        ScriptPropertiesCompiler.CompileDefault__Object(propBlock, classExport, ref export, usop);
                    }
                    else
                    {
                        ScriptPropertiesCompiler.CompilePropertiesForNormalObject(propBlock.Statements, export, usop);
                    }
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

        public static Class CompileNewClassAST(IMEPackage pcc, Class cls, MessageLog log, FileLib lib, out bool vfTableChanged, UnrealScriptOptionsPackage usop)
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
                        && !existingNativeStruct.IsNativeCompatibleWith(newStruct, pcc.Game, usop))
                    {
                        log.LogError($"Cannot modify native struct: {existingNativeStruct.Name}", newStruct.StartPos, newStruct.EndPos);
                    }
                }
            }
            log.Filter = cls;
            SymbolTable symbols = lib.CreateSymbolTableWithClass(cls, log, usop);
            log.Filter = null;
            if (symbols is null || log.HasErrors)
            {
                return null;
            }
            CompileNewClassASTInternal(pcc, cls, log, symbols, existingClass, ref vfTableChanged, usop);

            return cls;
        }

        private static void CompileNewClassASTInternal(IMEPackage pcc, Class cls, MessageLog log, SymbolTable symbols, Class existingClass, ref bool vfTableChanged, UnrealScriptOptionsPackage usop)
        {
            symbols.RevertToObjectStack();
            symbols.GoDirectlyToStack(cls.GetScope());
            foreach (Struct childStruct in cls.TypeDeclarations.OfType<Struct>())
            {
                PropertiesBlockParser.ParseStructDefaults(childStruct, pcc, symbols, log, usop);
            }
            foreach (Function func in cls.Functions)
            {
                CodeBodyParser.ParseFunction(func, pcc.Game, symbols, log, usop);
            }
            foreach (State state in cls.States)
            {
                CodeBodyParser.ParseState(state, pcc.Game, symbols, usop, log);
            }
            CodeBodyParser.ParseReplicationBlock(cls, pcc.Game, symbols, log, usop);
            PropertiesBlockParser.Parse(cls.DefaultProperties, pcc, symbols, log, true, usop);

            //calculate the virtual function table
            if (pcc.Game.IsGame3())
            {
                var virtualFuncs = cls.Functions.Where(func => func.ShouldBeInVTable).ToList();
                var funcDict = virtualFuncs.ToDictionary(func => func.Name);

                List<string> parentVirtualFuncNames = usop.GetVTableFromDonor?.Invoke(cls.Name) ?? ((Class)cls.Parent).VirtualFunctionNames;
                if (parentVirtualFuncNames is null)
                {
                    parentVirtualFuncNames = GetParentVirtualFuncs((Class)cls.Parent);

                    static List<string> GetParentVirtualFuncs(Class curClass)
                    {
                        var funcs = new List<string>();
                        if (curClass is null)
                        {
                            return funcs;
                        }
                        if (curClass.VirtualFunctionNames is not null)
                        {
                            return curClass.VirtualFunctionNames;
                        }
                        funcs.AddRange(GetParentVirtualFuncs(curClass.Parent as Class));
                        foreach (string funcName in curClass.Functions.Where(func => func.ShouldBeInVTable).Select(func => func.Name))
                        {
                            if (!funcs.Contains(funcName, StringComparer.OrdinalIgnoreCase))
                            {
                                funcs.Add(funcName);
                            }
                        }
                        return funcs;
                    }
                }
                var overrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (string funcName in parentVirtualFuncNames)
                {
                    if (funcDict.Remove(funcName, out Function func))
                    {
                        overrides.Add(funcName);
                        virtualFuncs.Remove(func);
                    }
                }
                cls.VirtualFunctionNames = new List<string>();
                if (!cls.IsInterface)
                {
                    cls.VirtualFunctionNames.AddRange(parentVirtualFuncNames);
                    cls.VirtualFunctionNames.AddRange(virtualFuncs.Select(func => func.Name));
                }

                if (existingClass?.VirtualFunctionNames is not null)
                {
                    var existingNames = new HashSet<string>(existingClass.VirtualFunctionNames, StringComparer.OrdinalIgnoreCase);
                    if (existingNames.SetEquals(cls.VirtualFunctionNames) 
                        //check if the ordering matches the parent where they overlap. If not, existing ordering is broken
                        && parentVirtualFuncNames.AsSpan().SequenceEqual(existingClass.VirtualFunctionNames.AsSpan()[..parentVirtualFuncNames.Count], StringComparer.OrdinalIgnoreCase))
                    {
                        //same functions, so preserve the ordering 
                        cls.VirtualFunctionNames = existingClass.VirtualFunctionNames;

                        //check to see if overrides have changed
                        var existingOverrides = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        var existingFuncDict = existingClass.Functions.Where(func => func.ShouldBeInVTable).ToDictionary(func => func.Name);
                        foreach (string funcName in parentVirtualFuncNames)
                        {
                            if (existingFuncDict.Remove(funcName))
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
                        vfTableChanged = true;
                    }
                }

                cls.VirtualFunctionTable = cls.VirtualFunctionNames.Select(funcName => cls.LookupFunction(funcName) ?? throw new Exception($"'{funcName}' not found on class!")).ToList();
            }
        }

        public static Function CompileNewFunctionBodyAST(ExportEntry parentExport, Function func, MessageLog log, FileLib lib, UnrealScriptOptionsPackage usop)
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
            var validator = new ClassValidationVisitor(log, symbols, ValidationPass.ClassAndStructMembersAndFunctionParams, usop);
            validator.VisitNode(func);
            validator.Pass = ValidationPass.BodyPass;
            validator.VisitNode(func);

            CodeBodyParser.ParseFunction(func, parentExport.Game, symbols, log, usop);
            return func;
        }

        public static State CompileNewStateBodyAST(ExportEntry parentExport, State state, MessageLog log, FileLib lib, UnrealScriptOptionsPackage usop)
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
                ClassValidationVisitor.RunAllPasses(state, log, symbols, usop);
                CodeBodyParser.ParseState(state, parentExport.Game, symbols, usop, log);

                return state;
            }

            return null;
        }

        public static Enumeration CompileNewEnumAST(ExportEntry parentExport, Enumeration enumeration, MessageLog log, FileLib lib, UnrealScriptOptionsPackage usop)
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
                ClassValidationVisitor.RunAllPasses(enumeration, log, symbols, usop);

                return enumeration;
            }

            return null;
        }

        public static VariableDeclaration CompileNewVarDeclAST(ExportEntry parentExport, VariableDeclaration varDecl, MessageLog log, FileLib lib, UnrealScriptOptionsPackage usop)
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
                ClassValidationVisitor.RunAllPasses(varDecl, log, symbols, usop);

                return varDecl;
            }

            return null;
        }

        public static Struct CompileNewStructAST(ExportEntry parentExport, Struct strct, MessageLog log, FileLib lib, UnrealScriptOptionsPackage usop)
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
                ClassValidationVisitor.RunAllPasses(strct, log, symbols, usop);
                PropertiesBlockParser.ParseStructDefaults(strct, parentExport.FileRef, symbols, log, usop);
                return strct;
            }

            return null;
        }

        public static DefaultPropertiesBlock CompileDefaultPropertiesAST(DefaultPropertiesBlock propBlock, MessageLog log, FileLib lib, ExportEntry export, UnrealScriptOptionsPackage usop)
        {
            if (!ReferenceEquals(lib.Pcc, export.FileRef))
            {
                throw new InvalidOperationException("FileLib can only be used with exports from the same file it was created for.");
            }
            SymbolTable symbols = lib.GetSymbolTable();
            symbols.RevertToObjectStack();

            if (symbols.TryGetType(export.ClassName, out Class propsClass))
            {
                if (!propsClass.Name.CaseInsensitiveEquals("Object"))
                {
                    symbols.GoDirectlyToStack(((Class)propsClass.Parent).GetInheritanceString());
                    symbols.PushScope(propsClass.Name);
                }

                propsClass.DefaultProperties = propBlock;
                propBlock.Outer = propsClass;

                PropertiesBlockParser.Parse(propBlock, export.FileRef, symbols, log, export.IsInDefaultsTree(), usop);

                return propBlock;
            }

            log.LogError($"Class '{export.ClassName}' not found in symbol table.");
            return null;
        }

        public record LooseClass(string ClassName, string Source, string SourceFilePath = null)
        {
            internal Class ClassAST;
            internal MessageLog Log;
        }

        public record LooseClassPackage(string PackageName, List<LooseClass> Classes);

        private record CompilationCompletion(UClass UClass, string SuperClassName, Action<UnrealScriptOptionsPackage> Action)
        {
            public bool Completed;
        }

        public static MessageLog CompileLooseClasses(IMEPackage targetPcc, List<LooseClassPackage> looseClasses, UnrealScriptOptionsPackage usop)
        {
            using var packageCache = new PackageCache();
            using var fileLib = new FileLib(targetPcc);

            var classASTs = new List<Class>();
            foreach (LooseClass looseClass in looseClasses.SelectMany(lcp => lcp.Classes))
            {
                var log = new MessageLog();
                (ASTNode node, _) = CompileOutlineAST(looseClass.Source, "Class", log, targetPcc.Game);
                if (node is not Class cls)
                {
                    log.LogError($"'{looseClass.ClassName}' does not contain a parseable class.");
                    return log;
                }
                if (log.HasErrors || log.HasLexErrors)
                {
                    log.LogError($"'{looseClass.ClassName}' had parse errors");
                    return log;
                }
                classASTs.Add(cls);
                looseClass.ClassAST = cls;
                looseClass.Log = log;
            }

            if (!fileLib.InternalInitialize(usop, additionalClasses: classASTs))
            {
                fileLib.InitializationLog.LogError("Could not initialize FileLib.");
                return fileLib.InitializationLog;
            }

            var completions = new Dictionary<string, CompilationCompletion>();
            foreach (LooseClassPackage looseClassPackage in looseClasses)
            {
                ExportEntry classPackage = targetPcc.FindExport(looseClassPackage.PackageName);
                if (classPackage is not null && classPackage.ClassName is not "Package")
                {
                    var log = new MessageLog();
                    log.LogError($"Could not create package '{looseClassPackage.PackageName}', as an existing non-package top-level export of the same name exists.");
                    return log;
                }
                classPackage ??= ExportCreator.CreatePackageExport(targetPcc, looseClassPackage.PackageName, cache: usop.Cache);

                bool vfTableChanged = false;
                SymbolTable symbols = fileLib.ReadonlySymbolTable; //this sign can't stop me because I can't read!
                foreach (LooseClass looseClass in looseClassPackage.Classes)
                {
                    MessageLog log = looseClass.Log;
                    Class cls = looseClass.ClassAST;

                    try
                    {
                        CompileNewClassASTInternal(targetPcc, cls, log, symbols, null, ref vfTableChanged, usop);

                        if (log.HasErrors)
                        {
                            log.LogError($"'{looseClass.ClassName}' had parse errors");
                            return log;
                        }
                        string superClassName = (cls.Parent as Class)?.Name;
                        UClass uClass = null;
                        Action<UnrealScriptOptionsPackage> completionAction = ScriptObjectCompiler.CreateClassStub(cls, targetPcc, classPackage, ref uClass, usop);
                        completions.Add(looseClass.ClassName, new CompilationCompletion(uClass, superClassName, completionAction));
                    }
                    catch (ParseException)
                    {
                        log.LogError($"'{looseClass.ClassName}' had parse errors");
                        return log;
                    }
                    catch (Exception exception)
                    {
                        log.LogError($"Exception while compiling '{looseClass.ClassName}': {exception}");
                        return log;
                    }
                }
            }

            foreach (CompilationCompletion completion in completions.Values)
            {
                try
                {
                    if (completion.Completed)
                    {
                        continue;
                    }
                    FinishCompilation(completion);
                    void FinishCompilation(CompilationCompletion cc)
                    {
                        if (cc.SuperClassName is not null
                            && completions.TryGetValue(cc.SuperClassName, out CompilationCompletion superCompletion)
                            && !superCompletion.Completed)
                        {
                            FinishCompilation(superCompletion);
                        }
                        cc.Action(usop);
                        cc.UClass.Export.WriteBinary(cc.UClass);
                        cc.Completed = true;
                    }
                }
                catch (Exception e)
                {
                    var log = new MessageLog();
                    log.LogError($"Exception while compiling '{completion.UClass.Export.ObjectName}': {e}");
                    return log;
                }
            }

            return new MessageLog();
        }
    }
}
