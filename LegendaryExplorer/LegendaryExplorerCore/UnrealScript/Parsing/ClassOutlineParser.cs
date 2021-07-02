using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing.Tokenizing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    public class ClassOutlineParser : StringParserBase
    {
        private readonly MEGame Game;

        public ClassOutlineParser(TokenStream<string> tokens, MEGame game, MessageLog log = null)
        {
            Game = game;
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
                if (!Matches(CLASS)) throw ParseError("Expected class declaration!");

                var name = Consume(TokenType.Word);
                if (name == null) throw ParseError("Expected class name!");

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
                            throw ParseError("Config specifier is missing name of config file!", CurrentPosition);
                        }

                        configName = Tokens.Prev().Value;
                        if (Consume(TokenType.RightParenth) is null)
                        {
                            throw ParseError("Expected ')' after config file name!", CurrentPosition);
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
                            throw ParseError("'implements' specifier is missing interface list!", CurrentPosition);
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
                            throw ParseError("Expected ')' after list of interfaces!", CurrentPosition);
                        }
                    }
                    else
                    {
                        throw ParseError($"Invalid class specifier: '{CurrentToken.Value}'!", CurrentPosition);
                    }
                }

                if (flags.Has(EClassFlags.NoExport) && !flags.Has(EClassFlags.Native))
                {
                    throw ParseError("noexport is only valid for native classes!", CurrentPosition);
                }

                if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon!", CurrentPosition);

                var variables = new List<VariableDeclaration>();
                var types = new List<VariableType>();
                while (CurrentIs(VAR, STRUCT, ENUM, CONST))
                {
                    if (CurrentIs(VAR))
                    {
                        var variable = TryParseVarDecl();
                        if (variable == null) throw ParseError("Malformed instance variable!", CurrentPosition);
                        variables.Add(variable);
                    }
                    else
                    {
                        VariableType type = TryParseEnum() ?? TryParseStruct() ?? TryParseConstant() ?? (VariableType)null;
                        if (type is null) throw ParseError("Malformed type declaration!", CurrentPosition);

                        types.Add(type);

                        if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon!", CurrentPosition);
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
                    throw ParseError("Expected defaultproperties block!", CurrentPosition);
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
                        throw ParseError("Expected '=' after constant name!", CurrentPosition);
                    }

                    Tokens.PushSnapshot();
                    string constValue = null;
                    while (CurrentTokenType != TokenType.SemiColon)
                    {
                        if (CurrentTokenType == TokenType.NewLine)
                        {
                            throw ParseError("Expected ';' after constant value!", CurrentPosition);
                        }

                        constValue += Tokens.CurrentItem.Value;
                        Tokens.Advance();
                    }
                    Tokens.PopSnapshot();
                    if (constValue == null)
                    {
                        throw ParseError($"Expected a value for the constant '{constName.Value}'!");
                    }

                    Expression literal = ParseConstValue();
                    return new Const(constName.Value, constValue, startPos, CurrentPosition)
                    {
                        Literal = literal
                    };
                }

                throw ParseError("Expected name for constant!", CurrentPosition);
            }
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
                        throw ParseError("Expected ')' after category name!", CurrentPosition);
                    }
                }

                ParseVariableSpecifiers(out EPropertyFlags flags);
                if ((flags & (EPropertyFlags.CoerceParm | EPropertyFlags.OptionalParm | EPropertyFlags.OutParm | EPropertyFlags.SkipParm)) != 0)
                {
                    throw ParseError("Can only use 'out', 'coerce', 'optional', or 'skip' with function parameters!", CurrentPosition);
                }

                if (category is not null)
                {
                    flags |= EPropertyFlags.Editable;
                }
                var type = TryParseType();
                if (type == null) throw ParseError("Expected variable type", CurrentPosition);

                var var = ParseVariableName();
                if (var == null) throw ParseError("Malformed variable name!", CurrentPosition);

                if (CurrentTokenType == TokenType.Comma)
                {
                    throw ParseError("All variables must be declared on their own line!", CurrentPosition);
                }

                var semicolon = Consume(TokenType.SemiColon);
                if (semicolon == null) throw ParseError("Expected semi-colon!", CurrentPosition);

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
                if (name == null) throw ParseError("Expected struct name!", CurrentPosition);

                var parent = TryParseParent();

                if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

                var types = new List<VariableType>();
                while (CurrentTokenType != TokenType.RightBracket && !Tokens.AtEnd())
                {
                    var variable = TryParseStruct();
                    if (variable == null) break;
                    if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon after struct declaration!", CurrentPosition);
                    types.Add(variable);
                }

                var vars = new List<VariableDeclaration>();
                while (CurrentTokenType != TokenType.RightBracket && !CurrentIs(STRUCTDEFAULTPROPERTIES) && !Tokens.AtEnd())
                {
                    var variable = TryParseVarDecl();
                    if (variable == null) throw ParseError("Malformed struct content!", CurrentPosition);

                    vars.Add(variable);
                }

                DefaultPropertiesBlock defaults = null;
                if (Matches(STRUCTDEFAULTPROPERTIES))
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                    {
                        throw ParseError("Malformed defaultproperties body!", CurrentPosition);
                    }
                    defaults = new DefaultPropertiesBlock(null, bodyStart, bodyEnd)
                    {
                        Tokens = new TokenStream<string>(() => Tokens.GetTokensInRange(bodyStart, bodyEnd).ToList())
                    };
                }

                if (Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);

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
                if (name == null) throw ParseError("Expected enumeration name!", CurrentPosition);

                if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

                var identifiers = new List<EnumValue>();
                byte i = 0;
                do
                {
                    if (identifiers.Count >= 254)
                    {
                        throw ParseError("Enums cannot have more than 254 values!", CurrentPosition);
                    }
                    Token<string> ident = Consume(TokenType.Word);
                    if (ident == null) throw ParseError("Expected non-empty enumeration!", CurrentPosition);
                    if (ident.Value.Length > 63) throw ParseError("Enum value must be 63 characters or less!", CurrentPosition);

                    identifiers.Add(new EnumValue(ident.Value, i, ident.StartPos, ident.EndPos));
                    if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket) throw ParseError("Malformed enumeration content!", CurrentPosition);
                    i++;
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);

                return new Enumeration(name.Value, identifiers, name.StartPos, name.EndPos);
            }
        }

        public Function TryParseFunction()
        {
            return (Function)Tokens.TryGetTree(StubParser);
            ASTNode StubParser()
            {
                var start = CurrentPosition;
                ParseFunctionSpecifiers(out int nativeIndex, out EFunctionFlags flags);

                if (!Matches(FUNCTION, EF.Keyword))
                {
                    return null;
                }

                bool coerceReturn = Matches("coerce", EF.Keyword);
                Tokens.PushSnapshot();
                var returnType = TryParseType();
                if (returnType == null) throw ParseError("Expected function name or return type!", CurrentPosition);

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

                name.SyntaxType = EF.Function;

                if (coerceReturn && returnType == null)
                {
                    throw ParseError("Coerce specifier cannot be applied to a void return type!", CurrentPosition);
                }

                if (Consume(TokenType.LeftParenth) == null) throw ParseError("Expected '('!", CurrentPosition);

                var parameters = new List<FunctionParameter>();
                bool hasOptionalParams = false;
                bool hasOutParms = false;
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var param = TryParseParameter();
                    if (param == null) throw ParseError("Malformed parameter!", CurrentPosition);
                    if (hasOptionalParams && !param.IsOptional)
                    {
                        throw ParseError("Non-optional parameters cannot follow optional parameters!", param.StartPos, param.EndPos);
                    }

                    hasOptionalParams |= param.IsOptional;
                    hasOutParms |= param.IsOut;
                    if (param.Name.CaseInsensitiveEquals("ReturnValue"))
                    {
                        throw ParseError("Cannot name a parameter 'ReturnValue'! It is a reserved word!", param.StartPos, param.EndPos);
                    }
                    parameters.Add(param);
                    if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth) throw ParseError("Unexpected parameter content!", CurrentPosition);
                }

                if (Game >= MEGame.ME3 && hasOptionalParams)
                {
                    flags |= EFunctionFlags.HasOptionalParms; //TODO: does this flag exist in ME1/ME2?
                }

                if (hasOutParms)
                {
                    flags |= EFunctionFlags.HasOutParms;
                }
                if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

                CodeBody body = new CodeBody(null, CurrentPosition, CurrentPosition);
                if (Consume(TokenType.SemiColon) is null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                    {
                        throw ParseError("Malformed function body!", CurrentPosition);
                    }

                    body = new CodeBody(null, bodyStart, bodyEnd)
                    {
                        Tokens = new TokenStream<string>(() => Tokens.GetTokensInRange(bodyStart, bodyEnd).ToList())
                    };
                    flags |= EFunctionFlags.Defined;
                }

                VariableDeclaration returnDeclaration = null;
                if (returnType is not null)
                {
                    var returnFlags = EPropertyFlags.Parm | EPropertyFlags.OutParm | EPropertyFlags.ReturnParm;
                    if (coerceReturn)
                    {
                        returnFlags |= EPropertyFlags.CoerceParm;
                    }

                    returnDeclaration = new VariableDeclaration(returnType, returnFlags, "ReturnValue");
                }
                return new Function(name.Value, flags, returnDeclaration, body, parameters, start, body.EndPos)
                {
                    NativeIndex = nativeIndex
                };
            }
        }

        public State TryParseState()
        {
            return (State)Tokens.TryGetTree(StateSkeletonParser);
            ASTNode StateSkeletonParser()
            {
                EStateFlags flags = EStateFlags.None;
                while (CurrentTokenType == TokenType.Word)
                {
                    if (Matches("simulated"))
                    {
                        flags |= EStateFlags.Simulated;
                    }
                    else if (Matches("auto"))
                    {
                        flags |= EStateFlags.Auto;
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
                        throw ParseError("Expected ')' after '(' in state declaration!");
                    }

                    flags |= EStateFlags.Editable;
                }

                var name = Consume(TokenType.Word);
                if (name == null) throw ParseError("Expected state name!", CurrentPosition);

                var parent = TryParseParent();

                if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

                var ignores = new List<Function>();
                if (Matches(IGNORES))
                {
                    do
                    {
                        VariableIdentifier variable = TryParseVariable();
                        if (variable == null) throw ParseError("Malformed ignore statement!", CurrentPosition);

                        ignores.Add(new Function(variable.Name, EFunctionFlags.Public, null, null, null, variable.StartPos, variable.EndPos));
                    } while (Consume(TokenType.Comma) != null);

                    if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon!", CurrentPosition);
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
                    throw ParseError("Malformed state body!", CurrentPosition);
                }
                if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon at end of state!", CurrentPosition);

                var body = new CodeBody(new List<Statement>(), bodyStart, bodyEnd)
                {
                    Tokens = new TokenStream<string>(() => Tokens.GetTokensInRange(bodyStart, bodyEnd).ToList())
                };

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
                    throw ParseError("Malformed defaultproperties body!", CurrentPosition);
                }

                return new DefaultPropertiesBlock(new List<Statement>(), bodyStart, bodyEnd)
                {
                    Tokens = new TokenStream<string>(() => Tokens.GetTokensInRange(bodyStart, bodyEnd).ToList())
                };
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
                    throw ParseError("The only valid specifiers for function parameters are 'out', 'coerce', 'optional', 'const', 'alwaysinit' and 'skip'!", CurrentPosition);
                }

                flags |= EPropertyFlags.Parm;

                var type = TryParseType();
                if (type == null) throw ParseError("Expected parameter type!", CurrentPosition);

                var variable = TryParseVariable();
                if (variable == null) throw ParseError("Expected parameter name!", CurrentPosition);

                var funcParam = new FunctionParameter(type, flags, variable.Name, variable.Size, variable.StartPos, variable.EndPos);

                if (Consume(TokenType.Assign) != null)
                {
                    if (!funcParam.IsOptional)
                    {
                        throw ParseError("Only optional parameters can have default values!", CurrentPosition);
                    }

                    if (funcParam.IsOut)
                    {
                        throw ParseError("optional out parameters cannot have default values!", CurrentPosition);
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
                        throw ParseError("Expected default parameter value after '='!", CurrentPosition);
                    }

                    SourcePosition bodyStart = defaultValueStart;
                    SourcePosition bodyEnd = CurrentPosition;
                    funcParam.UnparsedDefaultParam = new CodeBody(null, bodyStart, bodyEnd)
                    {
                        Tokens = new TokenStream<string>(() => Tokens.GetTokensInRange(bodyStart, bodyEnd).ToList())
                    };
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
                if (Matches("const", EF.Keyword))
                {
                    flags |= EPropertyFlags.Const;
                }
                else if (Matches("config", EF.Keyword))
                {
                    flags |= EPropertyFlags.Config;
                }
                else if (Matches("globalconfig", EF.Keyword))
                {
                    flags |= EPropertyFlags.GlobalConfig | EPropertyFlags.Config;
                }
                else if (Matches("localized", EF.Keyword))
                {
                    flags |= EPropertyFlags.Localized | EPropertyFlags.Const;
                }
                //TODO: private, protected, and public are in ObjectFlags, not PropertyFlags 
                else if (Matches("privatewrite", EF.Keyword))
                {
                    flags |= EPropertyFlags.PrivateWrite;
                }
                else if (Matches("protectedwrite", EF.Keyword))
                {
                    flags |= EPropertyFlags.ProtectedWrite;
                }
                else if (Matches("editconst", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditConst;
                }
                else if (Matches("edithide", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditHide;
                }
                else if (Matches("edittextbox", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditTextBox;
                }
                else if (Matches("input", EF.Keyword))
                {
                    flags |= EPropertyFlags.Input;
                }
                else if (Matches("transient", EF.Keyword))
                {
                    flags |= EPropertyFlags.Transient;
                }
                else if (Matches("native", EF.Keyword))
                {
                    flags |= EPropertyFlags.Native;
                }
                else if (Matches("noexport", EF.Keyword))
                {
                    flags |= EPropertyFlags.NoExport;
                }
                else if (Matches("duplicatetransient", EF.Keyword))
                {
                    flags |= EPropertyFlags.DuplicateTransient;
                }
                else if (Matches("noimport", EF.Keyword))
                {
                    flags |= EPropertyFlags.NoImport;
                }
                else if (Matches("out", EF.Keyword))
                {
                    flags |= EPropertyFlags.OutParm;
                }
                else if (Matches("export", EF.Keyword))
                {
                    flags |= EPropertyFlags.ExportObject;
                }
                else if (Matches("editinlineuse", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditInlineUse;
                }
                else if (Matches("noclear", EF.Keyword))
                {
                    flags |= EPropertyFlags.NoClear;
                }
                else if (Matches("editfixedsize", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditFixedSize;
                }
                else if (Matches("repnotify", EF.Keyword))
                {
                    flags |= EPropertyFlags.RepNotify;
                }
                else if (Matches("repretry", EF.Keyword))
                {
                    flags |= EPropertyFlags.RepRetry;
                }
                else if (Matches("interp", EF.Keyword))
                {
                    flags |= EPropertyFlags.Interp | EPropertyFlags.Editable;
                }
                else if (Matches("nontransactional", EF.Keyword))
                {
                    flags |= EPropertyFlags.NonTransactional;
                }
                else if (Matches("deprecated", EF.Keyword))
                {
                    flags |= EPropertyFlags.Deprecated;
                }
                else if (Matches("skip", EF.Keyword))
                {
                    flags |= EPropertyFlags.SkipParm;
                }
                else if (Matches("coerce", EF.Keyword))
                {
                    flags |= EPropertyFlags.CoerceParm;
                }
                else if (Matches("optional", EF.Keyword))
                {
                    flags |= EPropertyFlags.OptionalParm;
                }
                else if (Matches("alwaysinit", EF.Keyword))
                {
                    flags |= EPropertyFlags.AlwaysInit;
                }
                else if (Matches("databinding", EF.Keyword))
                {
                    flags |= EPropertyFlags.DataBinding;
                }
                else if (Matches("editoronly", EF.Keyword))
                {
                    flags |= EPropertyFlags.EditorOnly;
                }
                else if (Matches("notforconsole", EF.Keyword))
                {
                    flags |= EPropertyFlags.NotForConsole;
                }
                else if (Matches("archetype", EF.Keyword))
                {
                    flags |= EPropertyFlags.Archetype;
                }
                else if (Matches("serializetext", EF.Keyword))
                {
                    flags |= EPropertyFlags.SerializeText;
                }
                else if (Matches("crosslevelactive", EF.Keyword))
                {
                    flags |= EPropertyFlags.CrossLevelActive;
                }
                else if (Matches("crosslevelpassive", EF.Keyword))
                {
                    flags |= EPropertyFlags.CrossLevelPassive;
                }
                else
                {
                    break;
                }
            }
        }

        private void ParseFunctionSpecifiers(out int nativeIndex, out EFunctionFlags flags)
        {
            nativeIndex = 0;
            flags = default;
            bool unreliable = false;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("event", EF.Keyword))
                {
                    flags |= EFunctionFlags.Event;
                }
                else if (Matches("delegate", EF.Keyword))
                {
                    flags |= EFunctionFlags.Delegate;
                }
                else if (Matches("operator", EF.Keyword))
                {
                    flags |= EFunctionFlags.Operator;
                }
                else if (Matches("preoperator", EF.Keyword))
                {
                    flags |= EFunctionFlags.PreOperator | EFunctionFlags.Operator;
                }
                else if (Matches("native", EF.Keyword))
                {
                    flags |= EFunctionFlags.Native;
                    if (Consume(TokenType.LeftParenth) != null)
                    {
                        if (Consume(TokenType.IntegerNumber) == null)
                        {
                            {
                                throw ParseError("Expected native index!", CurrentPosition);
                            }
                        }

                        nativeIndex = int.Parse(Tokens.Prev().Value);

                        if (Consume(TokenType.RightParenth) == null)
                        {
                            {
                                throw ParseError("Expected ')' after native index!", CurrentPosition);
                            }
                        }
                    }
                }
                else if (Matches("static", EF.Keyword))
                {
                    flags |= EFunctionFlags.Static;
                }
                else if (Matches("simulated", EF.Keyword))
                {
                    flags |= EFunctionFlags.Simulated;
                }
                else if (Matches("iterator", EF.Keyword))
                {
                    flags |= EFunctionFlags.Iterator;
                }
                else if (Matches("singular", EF.Keyword))
                {
                    flags |= EFunctionFlags.Singular;
                }
                else if (Matches("latent", EF.Keyword))
                {
                    flags |= EFunctionFlags.Latent;
                }
                else if (Matches("exec", EF.Keyword))
                {
                    flags |= EFunctionFlags.Exec;
                }
                else if (Matches("final", EF.Keyword))
                {
                    flags |= EFunctionFlags.Final;
                }
                else if (Matches("server", EF.Keyword))
                {
                    flags |= EFunctionFlags.NetServer | EFunctionFlags.Net;
                }
                else if (Matches("client", EF.Keyword))
                {
                    flags |= EFunctionFlags.NetClient | EFunctionFlags.Net | EFunctionFlags.Simulated;
                }
                else if (Matches("reliable", EF.Keyword))
                {
                    flags |= EFunctionFlags.NetReliable;
                }
                else if (Matches("unreliable", EF.Keyword))
                {
                    unreliable = true;
                }
                else if (Matches("private", EF.Keyword))
                {
                    flags |= EFunctionFlags.Private | EFunctionFlags.Final;
                }
                else if (Matches("protected", EF.Keyword))
                {
                    flags |= EFunctionFlags.Protected;
                }
                else if (Matches("public", EF.Keyword))
                {
                    flags |= EFunctionFlags.Public;
                }
                else
                {
                    break;
                }
            }

            //initial flag validation
            if (flags.Has(EFunctionFlags.Native))
            {
                if (nativeIndex > 0 && !flags.Has(EFunctionFlags.Final))
                {
                    {
                        throw ParseError("Function with a native index must be final!", CurrentPosition);
                    }
                }
            }
            else
            {
                if (flags.Has(EFunctionFlags.Latent))
                {
                    {
                        throw ParseError("Only native functions may use 'latent'!", CurrentPosition);
                    }
                }
                if (flags.Has(EFunctionFlags.Iterator))
                {
                    {
                        throw ParseError("Only native functions may use 'iterator'!", CurrentPosition);
                    }
                }
            }

            if (flags.Has(EFunctionFlags.Net))
            {
                if (flags.Has(EFunctionFlags.Exec))
                {
                    {
                        throw ParseError("Exec functions cannot be replicated!", CurrentPosition);
                    }
                }
                if (flags.Has(EFunctionFlags.Static))
                {
                    {
                        throw ParseError("Static functions can't be replicated!", CurrentPosition);
                    }
                }
                if (!unreliable && !flags.Has(EFunctionFlags.NetReliable))
                {
                    {
                        throw ParseError("Replicated functions require 'reliable' or 'unreliable'!", CurrentPosition);
                    }
                }
                if (unreliable && flags.Has(EFunctionFlags.NetReliable))
                {
                    {
                        throw ParseError("'reliable' and 'unreliable' are mutually exclusive!", CurrentPosition);
                    }
                }
            }
            else if (unreliable)
            {
                {
                    throw ParseError("'unreliable' specified without 'client' or 'server'!", CurrentPosition);
                }
            }
            else if (flags.Has(EFunctionFlags.NetReliable))
            {
                {
                    throw ParseError("'reliable' specified without 'client' or 'server'!", CurrentPosition);
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
