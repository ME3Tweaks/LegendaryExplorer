using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Parsing
{
    internal sealed class ClassOutlineParser : StringParserBase
    {
        private readonly MEGame Game;

        public ClassOutlineParser(TokenStream tokens, MEGame game, MessageLog log = null)
        {
            Game = game;
            Log = log ?? new MessageLog();
            Tokens = tokens;
        }

        public ASTNode ParseDocument(string parseUnit)
        {
            switch (parseUnit)
            {
                case "Class":
                    return TryParseClass();
                case "Function":
                    return TryParseFunction();
                case "State":
                    return ParseState();
                case "ScriptStruct":
                    return ParseStruct();
                case "Enum":
                    return ParseEnum();
                case "Const":
                    return ParseConstant();
                case "IntProperty":
                case "BoolProperty":
                case "FloatProperty":
                case "NameProperty":
                case "StrProperty":
                case "StringRefProperty":
                case "ByteProperty":
                case "ObjectProperty":
                case "ComponentProperty":
                case "InterfaceProperty":
                case "ArrayProperty":
                case "StructProperty":
                case "BioMask4Property":
                case "MapProperty":
                case "ClassProperty":
                case "DelegateProperty":
                    return ParseVarDecl();
                default:
                    return ParseDefaultProperties();
            }
        }

        public ASTNode ParseDocument()
        {
            if (CurrentIs(CLASS))
            {
                Class cls = TryParseClass();
                if (cls is null) throw ParseError($"Malformed {CLASS} declaration!", CurrentPosition);
                return cls;
            }
            if (CurrentIs(VAR))
            {
                VariableDeclaration variable = ParseVarDecl();
                if (variable == null) throw ParseError("Malformed instance variable!", CurrentPosition);
                return variable;
            }
            if (CurrentIs(STRUCT))
            {
                Struct strct = ParseStruct();
                if (strct is null) throw ParseError($"Malformed {STRUCT} declaration!", CurrentPosition);
                return strct;
            }
            if (CurrentIs(ENUM))
            {
                Enumeration enm = ParseEnum();
                if (enm is null) throw ParseError($"Malformed {ENUM} declaration!", CurrentPosition);
                return enm;
            }
            if (CurrentIs(CONST))
            {
                Const cnst = ParseConstant();
                if (cnst is null) throw ParseError($"Malformed {CONST} declaration!", CurrentPosition);
                return cnst;
            }
            if (CurrentIs(DEFAULTPROPERTIES, "properties"))
            {
                DefaultPropertiesBlock propsBlock = ParseDefaultProperties();
                if (propsBlock is null)
                {
                    throw ParseError($"Malformed {DEFAULTPROPERTIES} block!", CurrentPosition);
                }
                return propsBlock;
            }
            if (IsStartOfStateDeclaration())
            {
                State state = ParseState();
                if (state is null) throw ParseError($"Malformed {STATE} declaration!", CurrentPosition);
                return state;
            }
            if (TryParseFunction() is Function func)
            {
                return func;
            }
            throw ParseError($"Unable to parse a declaration at: {CurrentToken.Value}", CurrentToken);
        }

        #region Parsers
        #region Statements

        private Class TryParseClass()
        {
            var startPos = CurrentPosition;
            if (!Matches(CLASS, EF.Keyword)) throw ParseError("Expected class declaration!");

            var name = Consume(TokenType.Word);
            if (name == null) throw ParseError("Expected class name!");
            name.SyntaxType = EF.Class;

            var parentClass = ParseTheExtendsSpecifier(EF.Class) ?? new VariableType("Object");

            var outerClass = ParseTheWithinSpecifier() ?? new VariableType("Object");

            var interfaces = new List<VariableType>();

            EClassFlags flags = EClassFlags.Compiled | EClassFlags.Parsed;
            string configName = "None";
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("native", EF.Specifier))
                {
                    flags |= EClassFlags.Native;
                }
                else if (Matches("nativeonly", EF.Specifier))
                {
                    flags |= EClassFlags.NativeOnly;
                }
                else if (Matches("noexport", EF.Specifier))
                {
                    flags |= EClassFlags.NoExport;
                }
                else if (Matches("editinlinenew", EF.Specifier))
                {
                    flags |= EClassFlags.EditInlineNew;
                }
                else if (Matches("placeable", EF.Specifier))
                {
                    flags |= EClassFlags.Placeable;
                }
                else if (Matches("hidedropdown", EF.Specifier))
                {
                    flags |= EClassFlags.HideDropDown;
                }
                else if (Matches("nativereplication", EF.Specifier))
                {
                    flags |= EClassFlags.NativeReplication;
                }
                else if (Matches("perobjectconfig", EF.Specifier))
                {
                    flags |= EClassFlags.PerObjectConfig;
                }
                else if (Matches("abstract", EF.Specifier))
                {
                    flags |= EClassFlags.Abstract;
                }
                else if (Matches("deprecated", EF.Specifier))
                {
                    flags |= EClassFlags.Deprecated;
                }
                else if (Matches("transient", EF.Specifier))
                {
                    flags |= EClassFlags.Transient;
                }
                else if (Matches("config", EF.Specifier))
                {
                    flags |= EClassFlags.Config;
                    if (Consume(TokenType.LeftParenth) is null || Consume(TokenType.Word) is null)
                    {
                        throw ParseError("Config specifier is missing name of config file!", CurrentPosition);
                    }

                    configName = PrevToken.Value;
                    if (configName.CaseInsensitiveEquals("None"))
                    {
                        TypeError($"Config name cannot be '{configName}'!", PrevToken);
                    }
                    if (Consume(TokenType.RightParenth) is null)
                    {
                        throw ParseError("Expected ')' after config file name!", CurrentPosition);
                    }
                }
                else if (Matches("safereplace", EF.Specifier))
                {
                    flags |= EClassFlags.SafeReplace;
                }
                else if (Matches("hidden", EF.Specifier))
                {
                    flags |= EClassFlags.Hidden;
                }
                else if (Matches("collapsecategories", EF.Specifier))
                {
                    flags |= EClassFlags.CollapseCategories;
                }
                else if (Matches("implements", EF.Keyword))
                {
                    if (Consume(TokenType.LeftParenth) is null)
                    {
                        throw ParseError("'implements' specifier is missing interface list!", CurrentPosition);
                    }

                    while (Consume(TokenType.Word) is ScriptToken interfaceName)
                    {
                        interfaceName.SyntaxType = EF.Class;
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

            if (flags.Has(EClassFlags.Native))
            {
                if (!flags.Has(EClassFlags.NoExport))
                {
                    flags |= EClassFlags.Exported;
                }
            }
            else
            {
                if (flags.Has(EClassFlags.NoExport))
                {
                    TypeError("noexport is only valid for native classes!", CurrentPosition);
                }
            }

            if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon!", CurrentPosition);

            var variables = new List<VariableDeclaration>();
            var types = new List<VariableType>();
            var funcs = new List<Function>();
            var states = new List<State>();
            DefaultPropertiesBlock defaultPropertiesBlock = null;
            CodeBody replicationBlock = null;
            while (!Tokens.AtEnd())
            {
                if (CurrentIs(VAR))
                {
                    var variable = ParseVarDecl();
                    if (variable == null) throw ParseError("Malformed instance variable!", CurrentPosition);
                    variables.Add(variable);
                }
                else if (CurrentIs(STRUCT))
                {
                    VariableType type = ParseStruct();
                    if (type is null) throw ParseError($"Malformed {STRUCT} declaration!", CurrentPosition);
                    types.Add(type);
                    //optional
                    Matches(TokenType.SemiColon);
                }
                else if (CurrentIs(ENUM))
                {
                    VariableType type = ParseEnum();
                    if (type is null) throw ParseError($"Malformed {ENUM} declaration!", CurrentPosition);
                    types.Add(type);
                    //optional
                    Matches(TokenType.SemiColon);
                }
                else if (CurrentIs(CONST))
                {
                    VariableType type = ParseConstant();
                    if (type is null) throw ParseError($"Malformed {CONST} declaration!", CurrentPosition);
                    types.Add(type);
                    //optional
                    Matches(TokenType.SemiColon);
                }
                else if (CurrentIs(REPLICATION))
                {
                    replicationBlock = ParseReplicationBlock();
                    if (replicationBlock is null)
                    {
                        throw ParseError($"Malformed {REPLICATION} block!", CurrentPosition);
                    }
                }
                else if (CurrentIs(DEFAULTPROPERTIES))
                {
                    defaultPropertiesBlock = ParseDefaultProperties();
                    if (defaultPropertiesBlock is null)
                    {
                        throw ParseError($"Malformed {DEFAULTPROPERTIES} block!", CurrentPosition);
                    }
                }
                else if (IsStartOfStateDeclaration())
                {
                    var state = ParseState();
                    if (state is null) throw ParseError($"Malformed {STATE} declaration!", CurrentPosition);
                    states.Add(state);
                }
                else if (TryParseFunction() is Function func)
                {
                    funcs.Add(func);
                }
                else
                {
                    throw ParseError($"Unexpected token in {CLASS}: {CurrentToken.Value}", CurrentToken);
                }
            }
            replicationBlock ??= new CodeBody(new List<Statement>(), PrevToken.EndPos, CurrentToken.StartPos)
            {
                Tokens = new TokenStream(new List<ScriptToken>(), Tokens)
            };
            defaultPropertiesBlock ??= new DefaultPropertiesBlock(new List<Statement>(), PrevToken.EndPos, CurrentToken.StartPos)
            {
                Tokens = new TokenStream(new List<ScriptToken>(), Tokens)
            };

            var @class = new Class(name.Value, parentClass, outerClass, flags, interfaces, types, variables, funcs, states, defaultPropertiesBlock, replicationBlock, startPos, CurrentToken.StartPos)
            {
                ConfigName = configName
            };
            Tokens.AddDefinitionLink(@class, name);
            return @class;
        }

        private Const ParseConstant()
        {
            var startPos = CurrentPosition;
            if (!Matches(CONST, EF.Keyword)) return null;
            if (Consume(TokenType.Word) is ScriptToken constName)
            {
                if (!Matches(TokenType.Assign, EF.Operator))
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

                    constValue += CurrentTokenType switch
                    {
                        TokenType.NameLiteral => $"'{CurrentToken.Value}'",
                        TokenType.StringLiteral => '"' + CurrentToken.Value + '"',
                        TokenType.StringRefLiteral => '$' + CurrentToken.Value,
                        _ => Tokens.CurrentItem.Value
                    };
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

        private VariableDeclaration ParseVarDecl()
        {
            var startPos = CurrentPosition;
            if (!Matches(VAR, EF.Keyword)) return null;
            string category = null;
            if (CurrentTokenType == TokenType.LeftParenth)
            {
                Tokens.Advance();
                if (Consume(TokenType.Word) is ScriptToken categoryToken)
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
                TypeError("Can only use 'out', 'coerce', 'optional', or 'skip' with function parameters!", CurrentPosition);
            }

            if (category is not null)
            {
                flags |= EPropertyFlags.Editable;
            }
            var type = ParseTypeRef();
            if (type == null) throw ParseError("Expected variable type", CurrentPosition);

            var var = ParseVariableName();
            if (var == null) throw ParseError("Malformed variable name!", CurrentPosition);

            if (CurrentTokenType == TokenType.Comma)
            {
                throw ParseError("All variables must be declared on their own line!", CurrentPosition);
            }

            var semicolon = Consume(TokenType.SemiColon);
            if (semicolon == null) throw ParseError("Expected semi-colon!", CurrentPosition);

            var varDecl = new VariableDeclaration(type, flags, var.Name, var.Size, category, startPos, semicolon.EndPos);

            Tokens.AddDefinitionLink(varDecl, var.StartPos, var.EndPos - var.StartPos);

            return varDecl;
        }

        private Struct ParseStruct()
        {
            var startPos = CurrentToken.StartPos;
            if (!Matches(STRUCT, EF.Keyword)) return null;

            ScriptStructFlags flags = 0;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("native", EF.Specifier))
                {
                    flags |= ScriptStructFlags.Native;
                }
                else if (Matches("export", EF.Specifier))
                {
                    flags |= ScriptStructFlags.Export;
                }
                else if (Matches("transient", EF.Specifier))
                {
                    flags |= ScriptStructFlags.Transient;
                }
                else if (Matches("atomic", EF.Specifier))
                {
                    flags |= ScriptStructFlags.Atomic;
                }
                else if (Matches("immutable", EF.Specifier))
                {
                    flags |= ScriptStructFlags.Immutable | ScriptStructFlags.Atomic;
                }
                else if (Matches("immutablewhencooked", EF.Specifier))
                {
                    flags |= ScriptStructFlags.ImmutableWhenCooked | ScriptStructFlags.AtomicWhenCooked;
                }
                else if (Matches("strictconfig", EF.Specifier))
                {
                    flags |= ScriptStructFlags.StrictConfig;
                }
                else if (Matches(nameof(ScriptStructFlags.UnkStructFlag), EF.Specifier))
                {
                    flags |= ScriptStructFlags.UnkStructFlag;
                }
                else
                {
                    break;
                }
            }

            var name = Consume(TokenType.Word);
            if (name == null) throw ParseError("Expected struct name!", CurrentPosition);
            name.SyntaxType = EF.Struct;

            var parent = ParseTheExtendsSpecifier(EF.Struct);

            if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            var types = new List<VariableType>();
            var vars = new List<VariableDeclaration>();
            DefaultPropertiesBlock defaults = null;
            while (CurrentTokenType != TokenType.RightBracket && !Tokens.AtEnd())
            {
                if (CurrentIs(STRUCT))
                {
                    var variable = ParseStruct();
                    if (variable == null)
                    {
                        throw ParseError("Malformed struct declaration!", CurrentPosition);
                    }
                    if (Consume(TokenType.SemiColon) == null) TypeError("Expected semi-colon after struct declaration!", CurrentPosition);
                    types.Add(variable);
                }
                else if (CurrentIs(VAR))
                {
                    var variable = ParseVarDecl();
                    if (variable is null)
                    {
                        throw ParseError("Malformed variable declaration!", CurrentPosition);
                    }
                    vars.Add(variable);
                }
                else if (Consume(STRUCTDEFAULTPROPERTIES, DEFAULTPROPERTIES) is { } defaultPropsToken)
                {
                    defaultPropsToken.SyntaxType = EF.Keyword;
                    if (defaultPropsToken.Value.CaseInsensitiveEquals(DEFAULTPROPERTIES))
                    {
                        TypeError($"In Structs, use '{STRUCTDEFAULTPROPERTIES}', not '{DEFAULTPROPERTIES}'.", CurrentToken);
                    }
                    if (!ParseScopeSpan(false, out int bodyStart, out int bodyEnd, out List<ScriptToken> scopeTokens))
                    {
                        throw ParseError("Malformed defaultproperties body!", CurrentPosition);
                    }
                    if (defaults is not null)
                    {
                        TypeError($"Cannot have two {STRUCTDEFAULTPROPERTIES} declarations in a struct!", defaultPropsToken);
                    }
                    defaults = new DefaultPropertiesBlock(null, bodyStart, bodyEnd)
                    {
                        Tokens = new TokenStream(scopeTokens, Tokens)
                    };
                }
                else
                {
                    throw ParseError($"Expected an inner {STRUCT}, a var declaration, or '{STRUCTDEFAULTPROPERTIES}'", CurrentPosition);
                }
            }

            if (Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);

            var @struct = new Struct(name.Value, parent, flags, vars, types, defaults, null, startPos, PrevToken.EndPos);

            Tokens.AddDefinitionLink(@struct, name);

            return @struct;
        }

        private Enumeration ParseEnum()
        {
            var startPos = CurrentToken.StartPos;
            if (!Matches(ENUM, EF.Keyword)) return null;

            var name = Consume(TokenType.Word);
            if (name == null) throw ParseError("Expected enumeration name!", CurrentPosition);
            name.SyntaxType = EF.Enum;

            if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            var identifiers = new List<EnumValue>();
            byte i = 0;
            do
            {
                if (identifiers.Count >= 254)
                {
                    TypeError("Enums cannot have more than 254 values!", CurrentPosition);
                }
                ScriptToken ident = Consume(TokenType.Word);
                if (ident == null) throw ParseError("Expected non-empty enumeration!", CurrentPosition);
                if (ident.Value.Length > 63) TypeError("Enum value must be 63 characters or less!", CurrentPosition);

                var enumValue = new EnumValue(ident.Value, i, ident.StartPos, ident.EndPos);

                Tokens.AddDefinitionLink(enumValue, ident);

                identifiers.Add(enumValue);
                if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket) throw ParseError("Malformed enumeration content!", CurrentPosition);
                i++;
            } while (CurrentTokenType != TokenType.RightBracket);

            if (Consume(TokenType.RightBracket) == null) throw ParseError("Expected '}'!", CurrentPosition);
            if (identifiers.IsEmpty())
            {
                TypeError("Enums must have at least 1 value!", name);
            }

            var @enum = new Enumeration(name.Value, identifiers, startPos, PrevToken.EndPos);

            Tokens.AddDefinitionLink(@enum, name);

            return @enum;
        }

        private Function TryParseFunction()
        {
            Tokens.PushSnapshot();
            var start = CurrentPosition;
            ParseFunctionSpecifiers(out int nativeIndex, out EFunctionFlags flags);

            if (!Matches(FUNCTION, EF.Keyword))
            {
                Tokens.PopSnapshot();
                return null;
            }
            Tokens.DiscardSnapshot();

            bool coerceReturn = Matches("coerce", EF.Keyword);
            Tokens.PushSnapshot();
            var returnType = ParseTypeRef();
            if (returnType == null) throw ParseError("Expected function name or return type!", CurrentPosition);

            ScriptToken name = Consume(TokenType.Word);
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
                TypeError("Coerce specifier cannot be applied to a void return type!", CurrentPosition);
            }

            if (Consume(TokenType.LeftParenth) == null) throw ParseError("Expected '('!", CurrentPosition);

            var parameters = new List<FunctionParameter>();
            bool hasOptionalParams = false;
            bool hasOutParms = false;
            while (CurrentTokenType != TokenType.RightParenth)
            {
                var param = ParseParameter();
                if (param == null) throw ParseError("Malformed parameter!", CurrentPosition);
                if (hasOptionalParams && !param.IsOptional)
                {
                    TypeError("Non-optional parameters cannot follow optional parameters!", param.StartPos, param.EndPos);
                }

                hasOptionalParams |= param.IsOptional;
                hasOutParms |= param.IsOut;
                if (param.Name.CaseInsensitiveEquals("ReturnValue"))
                {
                    TypeError("Cannot name a parameter 'ReturnValue'! It is a reserved word!", param.StartPos, param.EndPos);
                }
                parameters.Add(param);
                if (Consume(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth) throw ParseError("Unexpected parameter content!", CurrentPosition);
            }

            if (Game >= MEGame.ME3 && hasOptionalParams)
            {
                flags |= EFunctionFlags.HasOptionalParms; //TODO: does this flag exist in LE1/LE2?
            }

            if (hasOutParms)
            {
                flags |= EFunctionFlags.HasOutParms;
            }
            if (Consume(TokenType.RightParenth) == null) throw ParseError("Expected ')'!", CurrentPosition);

            var body = new CodeBody(null, CurrentPosition, CurrentPosition);
            if (Consume(TokenType.SemiColon) is null)
            {
                if (!ParseScopeSpan(false, out int bodyStart, out int bodyEnd, out List<ScriptToken> scopeTokens))
                {
                    throw ParseError("Malformed function body!", CurrentPosition);
                }

                body = new CodeBody(null, bodyStart, bodyEnd)
                {
                    Tokens = new TokenStream(scopeTokens, Tokens)
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
            var function = new Function(name.Value, flags, returnDeclaration, body, parameters, start, PrevToken.EndPos)
            {
                NativeIndex = nativeIndex,
                Tokens = Tokens
            };

            Tokens.AddDefinitionLink(function, name);

            return function;
        }

        private bool IsStartOfStateDeclaration()
        {
            return CurrentIs(STATE) ||
                   CurrentIs("auto") ||
                   CurrentIs("simulated") && (NextIs("auto") || NextIs(STATE));
        }

        private State ParseState()
        {
            int startPos = CurrentToken.StartPos;
            var flags = EStateFlags.None;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("simulated", EF.Specifier))
                {
                    flags |= EStateFlags.Simulated;
                }
                else if (Matches("auto", EF.Specifier))
                {
                    flags |= EStateFlags.Auto;
                }
                else
                {
                    break;
                }
            }

            if (!Matches(STATE, EF.Keyword)) return null;
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
            name.SyntaxType = EF.State;

            var parent = ParseTheExtendsSpecifier(EF.State);

            if (Consume(TokenType.LeftBracket) == null) throw ParseError("Expected '{'!", CurrentPosition);

            var ignoreMask = (EProbeFunctions)ulong.MaxValue;
            if (Matches(IGNORES, EF.Keyword))
            {
                do
                {
                    if (Consume(TokenType.Word) is not ScriptToken ignore)
                    {
                        throw ParseError("Malformed ignore statement!", CurrentPosition);
                    }
                    ignore.SyntaxType = EF.Function;
                    if (Enum.TryParse(ignore.Value, out EProbeFunctions ignoreFlag))
                    {
                        ignoreMask &= ~ignoreFlag;
                    }
                    else
                    {
                        TypeError("Only probed functions can be ignored! To ignore a non-probe function, declare it with a ; instead of a body.", ignore);
                    }
                } while (Consume(TokenType.Comma) != null);

                if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon!", CurrentPosition);
            }

            var funcs = new List<Function>();
            Function func = TryParseFunction();
            while (func != null)
            {
                if (func.IsStatic)
                {
                    TypeError($"States cannot have '{STATIC}' functions!", func);
                }
                funcs.Add(func);
                func = TryParseFunction();
            }

            if (!ParseScopeSpan(true, out int bodyStart, out int bodyEnd, out List<ScriptToken> scopeTokens))
            {
                throw ParseError("Malformed state body!", CurrentPosition);
            }
            if (Consume(TokenType.SemiColon) == null) throw ParseError("Expected semi-colon at end of state!", CurrentPosition);

            var body = new CodeBody(new List<Statement>(), bodyStart, bodyEnd)
            {
                Tokens = new TokenStream(scopeTokens, Tokens)
            };

            var parentState = parent != null ? new State(parent.Name, null, default, null, null, null, parent.StartPos, parent.EndPos) : null;
            var state = new State(name.Value, body, flags, parentState, funcs, null, startPos, CurrentPosition)
            {
                IgnoreMask = ignoreMask,
                Tokens = Tokens
            };

            Tokens.AddDefinitionLink(state, name);

            return state;
        }

        public DefaultPropertiesBlock ParseDefaultProperties()
        {
            if (!Matches(DEFAULTPROPERTIES, EF.Keyword) && !Matches("properties", EF.Keyword)) return null;

            if (!ParseScopeSpan(false, out int bodyStart, out int bodyEnd, out List<ScriptToken> scopeTokens))
            {
                throw ParseError("Malformed defaultproperties body!", CurrentPosition);
            }

            return new DefaultPropertiesBlock(new List<Statement>(), bodyStart, bodyEnd)
            {
                Tokens = new TokenStream(scopeTokens, Tokens)
            };
        }

        private CodeBody ParseReplicationBlock()
        {
            if (!Matches(REPLICATION, EF.Keyword)) return null;

            if (!ParseScopeSpan(false, out int bodyStart, out int bodyEnd, out List<ScriptToken> scopeTokens))
            {
                throw ParseError("Malformed replication body!", CurrentPosition);
            }

            return new CodeBody(new List<Statement>(), bodyStart, bodyEnd)
            {
                Tokens = new TokenStream(scopeTokens, Tokens)
            };
        }

        #endregion

        #region Misc

        private FunctionParameter ParseParameter()
        {
            ParseVariableSpecifiers(out EPropertyFlags flags);
            if ((flags & ~(EPropertyFlags.CoerceParm | EPropertyFlags.OptionalParm | EPropertyFlags.OutParm | EPropertyFlags.SkipParm | EPropertyFlags.Component | EPropertyFlags.Const | EPropertyFlags.AlwaysInit)) != 0)
            {
                TypeError("The only valid specifiers for function parameters are 'out', 'coerce', 'optional', 'const', 'init' and 'skip'!", CurrentPosition);
            }

            flags |= EPropertyFlags.Parm;

            var type = ParseTypeRef();
            if (type == null) throw ParseError("Expected parameter type!", CurrentPosition);

            var variable = ParseVariableIdentifier();
            if (variable == null) throw ParseError("Expected parameter name!", CurrentPosition);

            var funcParam = new FunctionParameter(type, flags, variable.Name, variable.Size, variable.StartPos, variable.EndPos);

            if (Matches(TokenType.Assign, EF.Operator))
            {
                if (!funcParam.IsOptional)
                {
                    TypeError("Only optional parameters can have default values!", CurrentPosition);
                }

                if (funcParam.IsOut)
                {
                    TypeError("optional out parameters cannot have default values!", CurrentPosition);
                }

                int defaultValueStart = CurrentPosition;
                int parenNest = 0;
                var defaultParamTokens = new List<ScriptToken>();
                while (CurrentTokenType != TokenType.EOF)
                {
                    if (parenNest == 0 && (CurrentTokenType is TokenType.RightParenth or TokenType.Comma))
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
                    defaultParamTokens.Add(CurrentToken);
                    Tokens.Advance();
                }

                if (CurrentPosition.Equals(defaultValueStart))
                {
                    throw ParseError("Expected default parameter value after '='!", CurrentPosition);
                }

                int bodyStart = defaultValueStart;
                int bodyEnd = CurrentPosition;
                funcParam.UnparsedDefaultParam = new CodeBody(null, bodyStart, bodyEnd)
                {
                    Tokens = new TokenStream(defaultParamTokens, Tokens)
                };
            }

            return funcParam;
        }

        private VariableType ParseTheExtendsSpecifier(EF syntaxType)
        {
            if (!Matches(EXTENDS, EF.Keyword)) return null;
            var parentName = Consume(TokenType.Word);
            if (parentName == null)
            {
                throw ParseError($"Expected parent name after '{EXTENDS}'!", CurrentPosition);
            }

            parentName.SyntaxType = syntaxType;

            return new VariableType(parentName.Value, parentName.StartPos, parentName.EndPos);
        }

        private VariableType ParseTheWithinSpecifier()
        {
            if (!Matches(WITHIN, EF.Keyword)) return null;
            var outerName = Consume(TokenType.Word);
            if (outerName == null)
            {
                throw ParseError($"Expected outer class name after '{WITHIN}'!", CurrentPosition);
            }

            outerName.SyntaxType = EF.Class;

            return new VariableType(outerName.Value, outerName.StartPos, outerName.EndPos);
        }

        #endregion
        #endregion
        #region Helpers

        private void ParseVariableSpecifiers(out EPropertyFlags flags)
        {
            flags = EPropertyFlags.None;
            while (CurrentTokenType == TokenType.Word)
            {
                if (Matches("const", EF.Specifier))
                {
                    flags |= EPropertyFlags.Const;
                }
                else if (Matches("config", EF.Specifier))
                {
                    flags |= EPropertyFlags.Config;
                }
                else if (Matches("globalconfig", EF.Specifier))
                {
                    flags |= EPropertyFlags.GlobalConfig | EPropertyFlags.Config;
                }
                else if (Matches(nameof(EPropertyFlags.EditInline), EF.Specifier))
                {
                    flags |= EPropertyFlags.EditInline;
                }
                else if (Matches("localized", EF.Specifier))
                {
                    flags |= EPropertyFlags.Localized | EPropertyFlags.Const;
                }
                //TODO: private, protected, and public are in ObjectFlags, not PropertyFlags 
                else if (Matches("privatewrite", EF.Specifier))
                {
                    flags |= EPropertyFlags.PrivateWrite;
                }
                else if (Matches("protectedwrite", EF.Specifier))
                {
                    flags |= EPropertyFlags.ProtectedWrite;
                }
                else if (Matches("editconst", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditConst;
                }
                else if (Matches("edithide", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditHide;
                }
                else if (Matches("edittextbox", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditTextBox;
                }
                else if (Matches("input", EF.Specifier))
                {
                    flags |= EPropertyFlags.Input;
                }
                else if (Matches("transient", EF.Specifier))
                {
                    flags |= EPropertyFlags.Transient;
                }
                else if (Matches("native", EF.Specifier))
                {
                    flags |= EPropertyFlags.Native;
                }
                else if (Matches("noexport", EF.Specifier))
                {
                    flags |= EPropertyFlags.NoExport;
                }
                else if (Matches("duplicatetransient", EF.Specifier))
                {
                    flags |= EPropertyFlags.DuplicateTransient;
                }
                else if (Matches("noimport", EF.Specifier))
                {
                    flags |= EPropertyFlags.NoImport;
                }
                else if (Matches("out", EF.Specifier))
                {
                    flags |= EPropertyFlags.OutParm;
                }
                else if (Matches("export", EF.Specifier))
                {
                    flags |= EPropertyFlags.ExportObject;
                }
                else if (Matches("editinlineuse", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditInlineUse;
                }
                else if (Matches("noclear", EF.Specifier))
                {
                    flags |= EPropertyFlags.NoClear;
                }
                else if (Matches("editfixedsize", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditFixedSize;
                }
                else if (Matches("repnotify", EF.Specifier))
                {
                    flags |= EPropertyFlags.RepNotify;
                }
                else if (Matches("repretry", EF.Specifier))
                {
                    flags |= EPropertyFlags.RepRetry;
                }
                else if (Matches("interp", EF.Specifier))
                {
                    flags |= EPropertyFlags.Interp | EPropertyFlags.Editable;
                }
                else if (Matches("nontransactional", EF.Specifier))
                {
                    flags |= EPropertyFlags.NonTransactional;
                }
                else if (Matches("deprecated", EF.Specifier))
                {
                    flags |= EPropertyFlags.Deprecated;
                }
                else if (Matches("skip", EF.Specifier))
                {
                    flags |= EPropertyFlags.SkipParm;
                }
                else if (Matches("coerce", EF.Specifier))
                {
                    flags |= EPropertyFlags.CoerceParm;
                }
                else if (Matches("optional", EF.Specifier))
                {
                    flags |= EPropertyFlags.OptionalParm;
                }
                else if (Matches("init", EF.Specifier))
                {
                    flags |= EPropertyFlags.AlwaysInit;
                }
                else if (Matches("databinding", EF.Specifier))
                {
                    flags |= EPropertyFlags.DataBinding;
                }
                else if (Matches("editoronly", EF.Specifier))
                {
                    flags |= EPropertyFlags.EditorOnly;
                }
                else if (Matches("notforconsole", EF.Specifier))
                {
                    flags |= EPropertyFlags.NotForConsole;
                }
                else if (Matches("archetype", EF.Specifier))
                {
                    flags |= EPropertyFlags.Archetype;
                }
                else if (Matches("serializetext", EF.Specifier))
                {
                    flags |= EPropertyFlags.SerializeText;
                }
                else if (Matches("crosslevelactive", EF.Specifier))
                {
                    flags |= EPropertyFlags.CrossLevelActive;
                }
                else if (Matches("crosslevelpassive", EF.Specifier))
                {
                    flags |= EPropertyFlags.CrossLevelPassive;
                }
                else if (Matches("rsxstorage", EF.Specifier))
                {
                    flags |= EPropertyFlags.RsxStorage;
                }
                else if (Matches(nameof(EPropertyFlags.BioDynamicLoad), EF.Specifier))
                {
                    flags |= EPropertyFlags.BioDynamicLoad;
                }
                else if (Matches("loadforcooking", EF.Specifier))
                {
                    flags |= EPropertyFlags.LoadForCooking;
                }
                else if (Matches("biononship", EF.Specifier))
                {
                    flags |= EPropertyFlags.BioNonShip;
                }
                else if (Matches("bioignorepropertyadd", EF.Specifier))
                {
                    flags |= EPropertyFlags.BioIgnorePropertyAdd;
                }
                else if (Matches("sortbarrier", EF.Specifier))
                {
                    flags |= EPropertyFlags.SortBarrier;
                }
                else if (Matches("clearcrosslevel", EF.Specifier))
                {
                    flags |= EPropertyFlags.ClearCrossLevel;
                }
                else if (Matches("biosave", EF.Specifier))
                {
                    flags |= EPropertyFlags.BioSave;
                }
                else if (Matches("bioexpanded", EF.Specifier))
                {
                    flags |= EPropertyFlags.BioExpanded;
                }
                else if (Matches("bioautogrow", EF.Specifier))
                {
                    flags |= EPropertyFlags.BioAutoGrow;
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
                        TypeError("Functions with a native index must be final!", CurrentPosition);
                    }
                }
            }
            else
            {
                if (flags.Has(EFunctionFlags.Latent))
                {
                    {
                        TypeError("Only native functions may use 'latent'!", CurrentPosition);
                    }
                }
                if (flags.Has(EFunctionFlags.Iterator))
                {
                    {
                        TypeError("Only native functions may use 'iterator'!", CurrentPosition);
                    }
                }
            }

            if (flags.Has(EFunctionFlags.Net))
            {
                if (flags.Has(EFunctionFlags.Exec))
                {
                    {
                        TypeError("Exec functions cannot be replicated!", CurrentPosition);
                    }
                }
                if (flags.Has(EFunctionFlags.Static))
                {
                    {
                        TypeError("Static functions can't be replicated!", CurrentPosition);
                    }
                }
                if (!unreliable && !flags.Has(EFunctionFlags.NetReliable))
                {
                    {
                        TypeError("Replicated functions require 'reliable' or 'unreliable'!", CurrentPosition);
                    }
                }
                if (unreliable && flags.Has(EFunctionFlags.NetReliable))
                {
                    {
                        TypeError("'reliable' and 'unreliable' are mutually exclusive!", CurrentPosition);
                    }
                }
            }
            else if (unreliable)
            {
                {
                    TypeError("'unreliable' specified without 'client' or 'server'!", CurrentPosition);
                }
            }
            else if (flags.Has(EFunctionFlags.NetReliable))
            {
                {
                    TypeError("'reliable' specified without 'client' or 'server'!", CurrentPosition);
                }
            }
        }

        private bool ParseScopeSpan(bool isPartialScope, out int startPos, out int endPos, out List<ScriptToken> scopeTokens)
        {
            scopeTokens = new List<ScriptToken>();
            startPos = -1;
            endPos = -1;
            if (!isPartialScope && Consume(TokenType.LeftBracket) == null)
            {
                Log.LogError($"Expected '{TokenType.LeftBracket}'!", CurrentPosition);
                return false;
            }
            startPos = Tokens.CurrentItem.StartPos;

            var tokens = Tokens.GetRestOfScope();
            if (tokens is null)
            {
                Log.LogError("Scope ended prematurely, are your scopes unbalanced?", CurrentPosition);
                return false;
            }
            scopeTokens = tokens;
            endPos = Tokens.CurrentItem.StartPos;
            Tokens.Advance();
            return true;
        }

        #endregion
    }
}
