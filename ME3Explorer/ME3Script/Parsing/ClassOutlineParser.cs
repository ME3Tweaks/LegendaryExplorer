using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System.Collections.Generic;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal.BinaryConverters;
using static ME3ExplorerCore.Unreal.UnrealFlags;
using static ME3Script.Utilities.Keywords;

namespace ME3Script.Parsing
{
    public class ClassOutlineParser : StringParserBase
    {
        public ClassOutlineParser(TokenStream<string> tokens, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Tokens = tokens;
        }

        public ASTNode ParseDocument(string parseUnit = "Class")
        {
            return parseUnit switch
            {
                "Class" => TryParseClass(),
                "Function" => TryParseFunction(),
                "State" => TryParseState(),
                "ScriptStruct" => TryParseStruct(),
                "Enum" => TryParseEnum(),
                "Const" => TryParseConstant(),
                _ when parseUnit.EndsWith("Property") => TryParseVarDecl(),
                _ => TryParseDefaultProperties()
            };
        }

        #region Parsers
        #region Statements

        public Class TryParseClass()
        {
            return (Class)Tokens.TryGetTree(ClassParser);
            ASTNode ClassParser()
            {
                if (!Matches(CLASS)) throw Error("Expected class declaration!");

                var name = Consume(TokenType.Word);
                if (name == null) throw Error("Expected class name!");

                var parentClass = TryParseParent() ?? new VariableType("Object");

                var outerClass = TryParseOuter() ?? new VariableType("Object");

                var interfaces = new List<VariableType>();

                EClassFlags flags = EClassFlags.Compiled | EClassFlags.Parsed;
                string configName = "None";
                while (CurrentTokenType == TokenType.Word)
                {
                    if (Matches("native"))
                    {
                        flags |= EClassFlags.Native;
                    }
                    else if (Matches("nativeonly"))
                    {
                        flags |= EClassFlags.NativeOnly;
                    }
                    else if (Matches("noexport"))
                    {
                        flags |= EClassFlags.NoExport;
                    }
                    else if (Matches("editinlinenew"))
                    {
                        flags |= EClassFlags.EditInlineNew;
                    }
                    else if (Matches("placeable"))
                    {
                        flags |= EClassFlags.Placeable;
                    }
                    else if (Matches("hidedropdown"))
                    {
                        flags |= EClassFlags.HideDropDown;
                    }
                    else if (Matches("nativereplication"))
                    {
                        flags |= EClassFlags.NativeReplication;
                    }
                    else if (Matches("perobjectconfig"))
                    {
                        flags |= EClassFlags.PerObjectConfig;
                    }
                    else if (Matches("localized"))
                    {
                        flags |= EClassFlags.Localized;
                    }
                    else if (Matches("abstract"))
                    {
                        flags |= EClassFlags.Abstract;
                    }
                    else if (Matches("deprecated"))
                    {
                        flags |= EClassFlags.Deprecated;
                    }
                    else if (Matches("transient"))
                    {
                        flags |= EClassFlags.Transient;
                    }
                    else if (Matches("config"))
                    {
                        flags |= EClassFlags.Config;
                        if (Consume(TokenType.LeftParenth) is null || Consume(TokenType.Word) is null)
                        {
                            throw Error("Config specifier is missing name of config file!", CurrentPosition);
                        }

                        configName = Tokens.Prev().Value;
                        if (Consume(TokenType.RightParenth) is null)
                        {
                            throw Error("Expected ')' after config file name!", CurrentPosition);
                        }
                    }
                    else if (Matches("safereplace"))
                    {
                        flags |= EClassFlags.SafeReplace;
                    }
                    else if (Matches("hidden"))
                    {
                        flags |= EClassFlags.Hidden;
                    }
                    else if (Matches("collapsecategories"))
                    {
                        flags |= EClassFlags.CollapseCategories;
                    }
                    else if (Matches("implements"))
                    {
                        if (Consume(TokenType.LeftParenth) is null)
                        {
                            throw Error("'implements' specifier is missing interface list!", CurrentPosition);
                        }

                        while (Consume(TokenType.Word) is Token<string> interfaceName)
                        {
                            interfaces.Add(new VariableType(interfaceName.Value, interfaceName.StartPos, interfaceName.EndPos));
                            if (Consume(TokenType.Comma) is null)
                            {
                                break;
                            }
                        }
                        if (Consume(TokenType.RightParenth) is null)
                        {
                            throw Error("Expected ')' after list of interfaces!", CurrentPosition);
                        }
                    }
                    else
                    {
                        throw Error($"Invalid class specifier: '{CurrentToken.Value}'!", CurrentPosition);
                    }
                }

                if (flags.Has(EClassFlags.NoExport) && !flags.Has(EClassFlags.Native))
                {
                    throw Error("noexport is only valid for native classes!", CurrentPosition);
                }

                if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon!", CurrentPosition);

                var variables = new List<VariableDeclaration>();
                var types = new List<VariableType>();
                while (CurrentIs(VAR, STRUCT, ENUM, CONST))
                {
                    if (CurrentIs(VAR))
                    {
                        var variable = TryParseVarDecl();
                        if (variable == null) throw Error("Malformed instance variable!", CurrentPosition);
                        variables.Add(variable);
                    }
                    else
                    {
                        VariableType type = TryParseEnum() ?? TryParseStruct() ?? TryParseConstant() ?? (VariableType)null;
                        if (type is null) throw Error("Malformed type declaration!", CurrentPosition);

                        types.Add(type);

                        if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon!", CurrentPosition);
                    }
                }

                var funcs = new List<Function>();
                var states = new List<State>();
                while (!Tokens.AtEnd())
                {
                    ASTNode declaration = TryParseFunction() ?? TryParseState() ?? (ASTNode)null;
                    if (declaration == null)
                    {
                        break;
                    }

                    switch (declaration.Type)
                    {
                        case ASTNodeType.Function:
                            funcs.Add((Function)declaration);
                            break;
                        case ASTNodeType.State:
                            states.Add((State)declaration);
                            break;
                    }
                }

                var defaultPropertiesBlock = TryParseDefaultProperties();
                if (defaultPropertiesBlock == null)
                {
                    throw Error("Expected defaultproperties block!", CurrentPosition);
                }

                // TODO: should AST-nodes accept null values? should they make sure they dont present any?
                return new Class(name.Value, parentClass, outerClass, flags, interfaces, types, variables, funcs, states, defaultPropertiesBlock, start: name.StartPos, end: name.EndPos)
                {
                    ConfigName = configName
                };
            }
        }

        private Const TryParseConstant()
        {
            return (Const)Tokens.TryGetTree(ConstantParser);
            ASTNode ConstantParser()
            {
                var startPos = CurrentPosition;
                if (!Matches(CONST)) return null;
                if (Consume(TokenType.Word) is Token<string> constName)
                {
                    if (Consume(TokenType.Assign) == null)
                    {
                        throw Error("Expected '=' after constant name!", CurrentPosition);
                    }

                    Tokens.PushSnapshot();
                    string constValue = null;
                    while (CurrentTokenType != TokenType.SemiColon)
                    {
                        if (CurrentTokenType == TokenType.NewLine)
                        {
                            throw Error("Expected ';' after constant value!", CurrentPosition);
                        }

                        constValue += Tokens.CurrentItem.Value;
                        Tokens.Advance();
                    }
                    Tokens.PopSnapshot();
                    if (constValue == null)
                    {
                        throw Error($"Expected a value for the constant '{constName.Value}'!");
                    }

                    Expression literal = ParseConstValue();
                    return new Const(constName.Value, constValue, startPos, CurrentPosition)
                    {
                        Literal = literal
                    };
                }

                throw Error("Expected name for constant!", CurrentPosition);
            }
        }

        public Expression ParseConstValue()
        {
            //minus sign is not parsed as part of a literal, so do it manually
            bool isNegative = Matches(TokenType.MinusSign);

            Expression literal = ParseLiteral();
            if (literal is null)
            {
                throw Error("Expected a literal value for the constant!", CurrentPosition);
            }

            if (isNegative)
            {
                switch (literal)
                {
                    case FloatLiteral floatLiteral:
                        floatLiteral.Value *= -1;
                        break;
                    case IntegerLiteral integerLiteral:
                        integerLiteral.Value *= -1;
                        break;
                    default:
                        throw Error("Malformed constant value!", CurrentPosition);
                }
            }

            return literal;
        }

        public VariableDeclaration TryParseVarDecl()
        {
            return (VariableDeclaration)Tokens.TryGetTree(DeclarationParser);
            ASTNode DeclarationParser()
            {
                var startPos = CurrentPosition;
                if (!Matches(VAR)) return null;
                string category = null;
                if (CurrentTokenType == TokenType.LeftParenth)
                {
                    Tokens.Advance();
                    if (Consume(TokenType.Word) is Token<string> categoryToken)
                    {
                        category = categoryToken.Value;
                    }

                    if (Consume(TokenType.RightParenth) == null)
                    {
                        throw Error("Expected ')' after category name!", CurrentPosition);
                    }
                }

                ParseVariableSpecifiers(out EPropertyFlags flags);
                if ((flags & (EPropertyFlags.CoerceParm | EPropertyFlags.OptionalParm | EPropertyFlags.OutParm | EPropertyFlags.SkipParm)) != 0)
                {
                    throw Error("Can only use 'out', 'coerce', 'optional', or 'skip' with function parameters!", CurrentPosition);
                }

                var type = TryParseType();
                if (type == null) throw Error("Expected variable type", CurrentPosition);

                var var = ParseVariableName();
                if (var == null) throw Error("Malformed variable name!", CurrentPosition);

                if (CurrentTokenType == TokenType.Comma)
                {
                    throw Error("All variables must be declared on their own line!", CurrentPosition);
                }

                var semicolon = Consume(TokenType.SemiColon);
                if (semicolon == null) throw Error("Expected semi-colon!", CurrentPosition);

                return new VariableDeclaration(type, flags, var.Name, var.Size, category, startPos, semicolon.EndPos);
            }
        }

        public Struct TryParseStruct()
        {
            return (Struct)Tokens.TryGetTree(StructParser);
            ASTNode StructParser()
            {
                if (!Matches(STRUCT)) return null;

                ScriptStructFlags flags = 0;
                while (CurrentTokenType == TokenType.Word)
                {
                    if (Matches("native"))
                    {
                        flags |= ScriptStructFlags.Native;
                    }
                    else if (Matches("export"))
                    {
                        flags |= ScriptStructFlags.Export;
                    }
                    else if (Matches("transient"))
                    {
                        flags |= ScriptStructFlags.Transient;
                    }
                    else if (Matches("atomic"))
                    {
                        flags |= ScriptStructFlags.Atomic;
                    }
                    else if (Matches("immutable"))
                    {
                        flags |= ScriptStructFlags.Immutable | ScriptStructFlags.Atomic;
                    }
                    else if (Matches("immutablewhencooked"))
                    {
                        flags |= ScriptStructFlags.ImmutableWhenCooked | ScriptStructFlags.AtomicWhenCooked;
                    }
                    else if (Matches("strictconfig"))
                    {
                        flags |= ScriptStructFlags.StrictConfig;
                    }
                    else
                    {
                        break;
                    }
                }

                var name = Consume(TokenType.Word);
                if (name == null) throw Error("Expected struct name!", CurrentPosition);

                var parent = TryParseParent();

                if (Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition);

                var types = new List<VariableType>();
                while (CurrentTokenType != TokenType.RightBracket && !Tokens.AtEnd())
                {
                    var variable = TryParseStruct();
                    if (variable == null) break;
                    if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon after struct declaration!", CurrentPosition);
                    types.Add(variable);
                }

                var vars = new List<VariableDeclaration>();
                while (CurrentTokenType != TokenType.RightBracket && !CurrentIs(STRUCTDEFAULTPROPERTIES) && !Tokens.AtEnd())
                {
                    var variable = TryParseVarDecl();
                    if (variable == null) throw Error("Malformed struct content!", CurrentPosition);

                    vars.Add(variable);
                }

                DefaultPropertiesBlock defaults = null;
                if (Matches(STRUCTDEFAULTPROPERTIES))
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                    {
                        throw Error("Malformed defaultproperties body!", CurrentPosition);
                    }
                    defaults = new DefaultPropertiesBlock(null, bodyStart, bodyEnd);
                }

                if (Consume(TokenType.RightBracket) == null) throw Error("Expected '}'!", CurrentPosition);

                return new Struct(name.Value, parent, flags, vars, types, defaults, name.StartPos, name.EndPos);
            }
        }

        public Enumeration TryParseEnum()
        {
            return (Enumeration)Tokens.TryGetTree(EnumParser);
            ASTNode EnumParser()
            {
                if (!Matches(ENUM)) return null;

                var name = Consume(TokenType.Word);
                if (name == null) throw Error("Expected enumeration name!", CurrentPosition);

                if (Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition);

                var identifiers = new List<EnumValue>();
                byte i = 0;
                do
                {
                    if (identifiers.Count >= 254)
                    {
                        throw Error("Enums cannot have more than 254 values!", CurrentPosition);
                    }
                    Token<string> ident = Consume(TokenType.Word);
                    if (ident == null) throw Error("Expected non-empty enumeration!", CurrentPosition);
                    if (ident.Value.Length > 63) throw Error("Enum value must be 63 characters or less!", CurrentPosition);

                    identifiers.Add(new EnumValue(ident.Value, i, ident.StartPos, ident.EndPos));
                    if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket) throw Error("Malformed enumeration content!", CurrentPosition);
                    i++;
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Consume(TokenType.RightBracket) == null) throw Error("Expected '}'!", CurrentPosition);

                return new Enumeration(name.Value, identifiers, name.StartPos, name.EndPos);
            }
        }

        public Function TryParseFunction()
        {
            return (Function)Tokens.TryGetTree(StubParser);
            ASTNode StubParser()
            {
                var start = CurrentPosition;
                ParseFunctionSpecifiers(out int nativeIndex, out FunctionFlags flags);

                if (!Matches(FUNCTION))
                {
                    return null;
                }

                bool coerceReturn = Matches("coerce");
                Tokens.PushSnapshot();
                var returnType = TryParseType();
                if (returnType == null) throw Error("Expected function name or return type!", CurrentPosition);

                Token<string> name = Consume(TokenType.Word);
                if (name == null)
                {
                    Tokens.PopSnapshot();
                    name = Consume(TokenType.Word);
                    returnType = null;
                }
                else
                {
                    Tokens.DiscardSnapshot();
                }

                if (coerceReturn && returnType == null)
                {
                    throw Error("Coerce specifier cannot be applied to a void return type!", CurrentPosition);
                }

                if (Consume(TokenType.LeftParenth) == null) throw Error("Expected '('!", CurrentPosition);

                var parameters = new List<FunctionParameter>();
                bool hasOptionalParams = false;
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var param = TryParseParameter();
                    if (param == null) throw Error("Malformed parameter!", CurrentPosition);
                    if (hasOptionalParams && !param.IsOptional)
                    {
                        throw Error("Non-optional parameters cannot follow optional parameters!", param.StartPos, param.EndPos);
                    }

                    hasOptionalParams |= param.IsOptional;
                    parameters.Add(param);
                    if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth) throw Error("Unexpected parameter content!", CurrentPosition);
                }

                if (hasOptionalParams)
                {
                    flags |= FunctionFlags.HasOptionalParms;
                }
                if (Consume(TokenType.RightParenth) == null) throw Error("Expected ')'!", CurrentPosition);

                CodeBody body = new CodeBody(null, CurrentPosition, CurrentPosition);
                if (Consume(TokenType.SemiColon) is null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                    {
                        throw Error("Malformed function body!", CurrentPosition);
                    }

                    body = new CodeBody(null, bodyStart, bodyEnd);
                    flags |= FunctionFlags.Defined;
                }

                return new Function(name.Value, flags, returnType, body, parameters, start, body.EndPos)
                {
                    NativeIndex = nativeIndex,
                    CoerceReturn = coerceReturn
                };
            }
        }

        public State TryParseState()
        {
            return (State)Tokens.TryGetTree(StateSkeletonParser);
            ASTNode StateSkeletonParser()
            {
                StateFlags flags = StateFlags.None;
                while (CurrentTokenType == TokenType.Word)
                {
                    if (Matches("simulated"))
                    {
                        flags |= StateFlags.Simulated;
                    }
                    else if (Matches("auto"))
                    {
                        flags |= StateFlags.Auto;
                    }
                    else
                    {
                        break;
                    }
                }

                if (!Matches(STATE)) return null;
                if (Consume(TokenType.LeftParenth) != null)
                {
                    if (Consume(TokenType.RightParenth) is null)
                    {
                        throw Error("Expected ')' after '(' in state declaration!");
                    }

                    flags |= StateFlags.Editable;
                }

                var name = Consume(TokenType.Word);
                if (name == null) throw Error("Expected state name!", CurrentPosition);

                var parent = TryParseParent();

                if (Consume(TokenType.LeftBracket) == null) throw Error("Expected '{'!", CurrentPosition);

                var ignores = new List<Function>();
                if (Matches(IGNORES))
                {
                    do
                    {
                        VariableIdentifier variable = TryParseVariable();
                        if (variable == null) throw Error("Malformed ignore statement!", CurrentPosition);

                        ignores.Add(new Function(variable.Name, FunctionFlags.Public, null, null, null, variable.StartPos, variable.EndPos));
                    } while (Consume(TokenType.Comma) != null);

                    if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon!", CurrentPosition);
                }

                var funcs = new List<Function>();
                Function func = TryParseFunction();
                while (func != null)
                {
                    funcs.Add(func);
                    func = TryParseFunction();
                }


                if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, true, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                {
                    throw Error("Malformed state body!", CurrentPosition);
                }
                if (Consume(TokenType.SemiColon) == null) throw Error("Expected semi-colon at end of state!", CurrentPosition);

                var body = new CodeBody(new List<Statement>(), bodyStart, bodyEnd);

                var parentState = parent != null ? new State(parent.Name, null, default, null, null, null, null, parent.StartPos, parent.EndPos) : null;
                return new State(name.Value, body, flags, parentState, funcs, ignores, null, name.StartPos, CurrentPosition);
            }
        }

        public DefaultPropertiesBlock TryParseDefaultProperties()
        {
            return (DefaultPropertiesBlock)Tokens.TryGetTree(DefaultPropertiesParser);
            ASTNode DefaultPropertiesParser()
            {

                if (!Matches(DEFAULTPROPERTIES)) return null;

                if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                {
                    throw Error("Malformed defaultproperties body!", CurrentPosition);
                }

                return new DefaultPropertiesBlock(new List<Statement>(), bodyStart, bodyEnd);
            }
        }

        #endregion

        #region Misc

        public FunctionParameter TryParseParameter()
        {
            return (FunctionParameter)Tokens.TryGetTree(ParamParser);
            ASTNode ParamParser()
            {
                ParseVariableSpecifiers(out EPropertyFlags flags);
                if ((flags & ~(EPropertyFlags.CoerceParm | EPropertyFlags.OptionalParm | EPropertyFlags.OutParm | EPropertyFlags.SkipParm | EPropertyFlags.Component | EPropertyFlags.Const | EPropertyFlags.AlwaysInit)) != 0)
                {
                    throw Error("The only valid specifiers for function parameters are 'out', 'coerce', 'optional', 'const', 'alwaysinit' and 'skip'!", CurrentPosition);
                }

                flags |= EPropertyFlags.Parm;

                var type = TryParseType();
                if (type == null) throw Error("Expected parameter type!", CurrentPosition);

                var variable = TryParseVariable();
                if (variable == null) throw Error("Expected parameter name!", CurrentPosition);

                var funcParam = new FunctionParameter(type, flags, variable.Name, variable.Size, variable.StartPos, variable.EndPos);

                if (Consume(TokenType.Assign) != null)
                {
                    if (!funcParam.IsOptional)
                    {
                        throw Error("Only optional parameters can have default values!", CurrentPosition);
                    }

                    if (funcParam.IsOut)
                    {
                        throw Error("optional out parameters cannot have default values!", CurrentPosition);
                    }

                    var defaultValueStart = CurrentPosition;
                    int parenNest = 0;
                    while (CurrentTokenType != TokenType.EOF)
                    {
                        if (parenNest == 0 && (CurrentTokenType == TokenType.RightParenth || CurrentTokenType == TokenType.Comma))
                        {
                            break;
                        }

                        switch (CurrentTokenType)
                        {
                            case TokenType.LeftParenth:
                                parenNest++;
                                break;
                            case TokenType.RightParenth:
                                parenNest--;
                                break;
                        }
                        Tokens.Advance();
                    }

                    if (CurrentPosition.Equals(defaultValueStart))
                    {
                        throw Error("Expected default parameter value after '='!", CurrentPosition);
                    }
                    funcParam.UnparsedDefaultParam = new CodeBody(null, defaultValueStart, CurrentPosition);
                }

                return funcParam;
            }
        }

        public VariableType TryParseParent()
        {
            return (VariableType)Tokens.TryGetTree(ParentParser);
            ASTNode ParentParser()
            {
                if (!Matches(EXTENDS)) return null;
                var parentName = Consume(TokenType.Word);
                if (parentName == null)
                {
                    Log.LogError("Expected parent name!", CurrentPosition);
                    return null;
                }

                return new VariableType(parentName.Value, parentName.StartPos, parentName.EndPos);
            }
        }

        public VariableType TryParseOuter()
        {
            return (VariableType)Tokens.TryGetTree(OuterParser);
            ASTNode OuterParser()
            {
                if (!Matches(WITHIN)) return null;
                var outerName = Consume(TokenType.Word);
                if (outerName == null)
                {
                    Log.LogError("Expected outer class name!", CurrentPosition);
                    return null;
                }

                return new VariableType(outerName.Value, outerName.StartPos, outerName.EndPos);
            }
        }

        #endregion
        #endregion
        #region Helpers

        public void ParseVariableSpecifiers(out EPropertyFlags flags)
        {
            flags = EPropertyFlags.None;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("const"))
                {
                    flags |= EPropertyFlags.Const;
                }
                else if (Matches("config"))
                {
                    flags |= EPropertyFlags.Config;
                }
                else if (Matches("globalconfig"))
                {
                    flags |= EPropertyFlags.GlobalConfig | EPropertyFlags.Config;
                }
                else if (Matches("localized"))
                {
                    flags |= EPropertyFlags.Localized | EPropertyFlags.Const;
                }
                //TODO: private, protected, and public are in ObjectFlags, not PropertyFlags 
                else if (Matches("privatewrite"))
                {
                    flags |= EPropertyFlags.PrivateWrite;
                }
                else if (Matches("protectedwrite"))
                {
                    flags |= EPropertyFlags.ProtectedWrite;
                }
                else if (Matches("editconst"))
                {
                    flags |= EPropertyFlags.EditConst;
                }
                else if (Matches("edithide"))
                {
                    flags |= EPropertyFlags.EditHide;
                }
                else if (Matches("edittextbox"))
                {
                    flags |= EPropertyFlags.EditTextBox;
                }
                else if (Matches("input"))
                {
                    flags |= EPropertyFlags.Input;
                }
                else if (Matches("transient"))
                {
                    flags |= EPropertyFlags.Transient;
                }
                else if (Matches("native"))
                {
                    flags |= EPropertyFlags.Native;
                }
                else if (Matches("noexport"))
                {
                    flags |= EPropertyFlags.NoExport;
                }
                else if (Matches("duplicatetransient"))
                {
                    flags |= EPropertyFlags.DuplicateTransient;
                }
                else if (Matches("noimport"))
                {
                    flags |= EPropertyFlags.NoImport;
                }
                else if (Matches("out"))
                {
                    flags |= EPropertyFlags.OutParm;
                }
                else if (Matches("export"))
                {
                    flags |= EPropertyFlags.ExportObject;
                }
                else if (Matches("editinlineuse"))
                {
                    flags |= EPropertyFlags.EditInlineUse;
                }
                else if (Matches("noclear"))
                {
                    flags |= EPropertyFlags.NoClear;
                }
                else if (Matches("editfixedsize"))
                {
                    flags |= EPropertyFlags.EditFixedSize;
                }
                else if (Matches("repnotify"))
                {
                    flags |= EPropertyFlags.RepNotify;
                }
                else if (Matches("repretry"))
                {
                    flags |= EPropertyFlags.RepRetry;
                }
                else if (Matches("interp"))
                {
                    flags |= EPropertyFlags.Interp | EPropertyFlags.Editable;
                }
                else if (Matches("nontransactional"))
                {
                    flags |= EPropertyFlags.NonTransactional;
                }
                else if (Matches("deprecated"))
                {
                    flags |= EPropertyFlags.Deprecated;
                }
                else if (Matches("skip"))
                {
                    flags |= EPropertyFlags.SkipParm;
                }
                else if (Matches("coerce"))
                {
                    flags |= EPropertyFlags.CoerceParm;
                }
                else if (Matches("optional"))
                {
                    flags |= EPropertyFlags.OptionalParm;
                }
                else if (Matches("alwaysinit"))
                {
                    flags |= EPropertyFlags.AlwaysInit;
                }
                else if (Matches("databinding"))
                {
                    flags |= EPropertyFlags.DataBinding;
                }
                else if (Matches("editoronly"))
                {
                    flags |= EPropertyFlags.EditorOnly;
                }
                else if (Matches("notforconsole"))
                {
                    flags |= EPropertyFlags.NotForConsole;
                }
                else if (Matches("archetype"))
                {
                    flags |= EPropertyFlags.Archetype;
                }
                else if (Matches("serializetext"))
                {
                    flags |= EPropertyFlags.SerializeText;
                }
                else if (Matches("crosslevelactive"))
                {
                    flags |= EPropertyFlags.CrossLevelActive;
                }
                else if (Matches("crosslevelpassive"))
                {
                    flags |= EPropertyFlags.CrossLevelPassive;
                }
                else
                {
                    break;
                }
            }
        }

        private void ParseFunctionSpecifiers(out int nativeIndex, out FunctionFlags flags)
        {
            nativeIndex = 0;
            flags = default;
            bool unreliable = false;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("event"))
                {
                    flags |= FunctionFlags.Event;
                }
                else if (Matches("delegate"))
                {
                    flags |= FunctionFlags.Delegate;
                }
                else if (Matches("operator"))
                {
                    flags |= FunctionFlags.Operator;
                }
                else if (Matches("preoperator"))
                {
                    flags |= FunctionFlags.PreOperator | FunctionFlags.Operator;
                }
                else if (Matches("native"))
                {
                    flags |= FunctionFlags.Native;
                    if (Consume(TokenType.LeftParenth) != null)
                    {
                        if (Consume(TokenType.IntegerNumber) == null)
                        {
                            {
                                throw Error("Expected native index!", CurrentPosition);
                            }
                        }

                        nativeIndex = int.Parse(Tokens.Prev().Value);

                        if (Consume(TokenType.RightParenth) == null)
                        {
                            {
                                throw Error("Expected ')' after native index!", CurrentPosition);
                            }
                        }
                    }
                }
                else if (Matches("static"))
                {
                    flags |= FunctionFlags.Static;
                }
                else if (Matches("simulated"))
                {
                    flags |= FunctionFlags.Simulated;
                }
                else if (Matches("iterator"))
                {
                    flags |= FunctionFlags.Iterator;
                }
                else if (Matches("singular"))
                {
                    flags |= FunctionFlags.Singular;
                }
                else if (Matches("latent"))
                {
                    flags |= FunctionFlags.Latent;
                }
                else if (Matches("exec"))
                {
                    flags |= FunctionFlags.Exec;
                }
                else if (Matches("final"))
                {
                    flags |= FunctionFlags.Final;
                }
                else if (Matches("server"))
                {
                    flags |= FunctionFlags.NetServer | FunctionFlags.Net;
                }
                else if (Matches("client"))
                {
                    flags |= FunctionFlags.NetClient | FunctionFlags.Net | FunctionFlags.Simulated;
                }
                else if (Matches("reliable"))
                {
                    flags |= FunctionFlags.NetReliable;
                }
                else if (Matches("unreliable"))
                {
                    unreliable = true;
                }
                else if (Matches("private"))
                {
                    flags |= FunctionFlags.Private | FunctionFlags.Final;
                }
                else if (Matches("protected"))
                {
                    flags |= FunctionFlags.Protected;
                }
                else if (Matches("public"))
                {
                    flags |= FunctionFlags.Public;
                }
                else
                {
                    break;
                }
            }

            //initial flag validation
            if (flags.Has(FunctionFlags.Native))
            {
                if (nativeIndex > 0 && !flags.Has(FunctionFlags.Final))
                {
                    {
                        throw Error("Function with a native index must be final!", CurrentPosition);
                    }
                }
            }
            else
            {
                if (flags.Has(FunctionFlags.Latent))
                {
                    {
                        throw Error("Only native functions may use 'latent'!", CurrentPosition);
                    }
                }
                if (flags.Has(FunctionFlags.Iterator))
                {
                    {
                        throw Error("Only native functions may use 'iterator'!", CurrentPosition);
                    }
                }
            }

            if (flags.Has(FunctionFlags.Net))
            {
                if (flags.Has(FunctionFlags.Exec))
                {
                    {
                        throw Error("Exec functions cannot be replicated!", CurrentPosition);
                    }
                }
                if (flags.Has(FunctionFlags.Static))
                {
                    {
                        throw Error("Static functions can't be replicated!", CurrentPosition);
                    }
                }
                if (!unreliable && !flags.Has(FunctionFlags.NetReliable))
                {
                    {
                        throw Error("Replicated functions require 'reliable' or 'unreliable'!", CurrentPosition);
                    }
                }
                if (unreliable && flags.Has(FunctionFlags.NetReliable))
                {
                    {
                        throw Error("'reliable' and 'unreliable' are mutually exclusive!", CurrentPosition);
                    }
                }
            }
            else if (unreliable)
            {
                {
                    throw Error("'unreliable' specified without 'client' or 'server'!", CurrentPosition);
                }
            }
            else if (flags.Has(FunctionFlags.NetReliable))
            {
                {
                    throw Error("'reliable' specified without 'client' or 'server'!", CurrentPosition);
                }
            }
        }

        //TODO: unused?
        private List<Token<string>> ParseScopedTokens(TokenType scopeStart, TokenType scopeEnd)
        {
            var scopedTokens = new List<Token<string>>();
            if (Consume(scopeStart) == null)
            {
                Log.LogError($"Expected '{scopeStart}'!", CurrentPosition);
                return null;
            }

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                    return null; // ERROR: Scope ended prematurely, are your scopes unbalanced?
                if (CurrentTokenType == scopeStart)
                    nestedLevel++;
                else if (CurrentTokenType == scopeEnd)
                    nestedLevel--;

                scopedTokens.Add(Tokens.CurrentItem);
                Tokens.Advance();
            }
            // Remove the ending scope token:
            scopedTokens.RemoveAt(scopedTokens.Count - 1);
            return scopedTokens;
        }

        private bool ParseScopeSpan(TokenType scopeStart, TokenType scopeEnd,
                                    bool isPartialScope,
                                    out SourcePosition startPos, out SourcePosition endPos)
        {
            startPos = null;
            endPos = null;
            if (!isPartialScope && Consume(scopeStart) == null)
            {
                Log.LogError($"Expected '{scopeStart}'!", CurrentPosition);
                return false;
            }
            startPos = Tokens.CurrentItem.StartPos;

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                {
                    Log.LogError("Scope ended prematurely, are your scopes unbalanced?", CurrentPosition);
                    return false;
                }
                if (CurrentTokenType == scopeStart)
                    nestedLevel++;
                else if (CurrentTokenType == scopeEnd)
                    nestedLevel--;

                // If we're at the end token, don't advance so we can check the position properly.
                if (nestedLevel > 0)
                    Tokens.Advance();
            }
            endPos = Tokens.CurrentItem.StartPos;
            Tokens.Advance();
            return true;
        }

        #endregion
    }
}
