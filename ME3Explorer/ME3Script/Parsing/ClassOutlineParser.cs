using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language;
using ME3Script.Language.Tree;
using ME3Script.Lexing;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public class ClassOutlineParser : StringParserBase
    {
        public ClassOutlineParser(TokenStream<string> tokens, MessageLog log = null)
        {
            Log = log ?? new MessageLog();
            Tokens = tokens;
        }

        public ASTNode ParseDocument()
        {
            return TryParseClass();
        }

        #region Parsers
        #region Statements

        public Class TryParseClass()
        {
            return (Class)Tokens.TryGetTree(ClassParser);
            ASTNode ClassParser()
            {
                if (Tokens.ConsumeToken(TokenType.Class) == null) return Error("Expected class declaration!");

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null) return Error("Expected class name!");

                var parentClass = TryParseParent();
                if (parentClass == null)
                {
                    Log.LogMessage("No parent class specified for " + name.Value + ", interiting from Object");
                    parentClass = new VariableType("Object", null, null);
                }

                var outerClass = TryParseOuter();

                var specs = ParseSpecifiers(GlobalLists.ClassSpecifiers);

                if (Tokens.ConsumeToken(TokenType.SemiColon) == null) return Error("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var variables = new List<VariableDeclaration>();
                var types = new List<VariableType>();
                while (CurrentTokenType == TokenType.InstanceVariable || CurrentTokenType == TokenType.Struct || CurrentTokenType == TokenType.Enumeration)
                {
                    if (CurrentTokenType == TokenType.InstanceVariable)
                    {
                        var variable = TryParseVarDecl();
                        if (variable == null) return Error("Malformed instance variable!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        variables.Add(variable);
                    }
                    else
                    {
                        var type = TryParseEnum() ?? TryParseStruct() ?? new VariableType("INVALID", null, null);
                        if (type.Name == "INVALID") return Error("Malformed type declaration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                        types.Add(type);

                        if (Tokens.ConsumeToken(TokenType.SemiColon) == null) return Error("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    }
                }

                var funcs = new List<Function>();
                var states = new List<State>();
                var ops = new List<OperatorDeclaration>();
                while (!Tokens.AtEnd())
                {
                    ASTNode declaration = TryParseFunction() ?? TryParseOperatorDecl() ?? TryParseState() ?? (ASTNode)null;
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
                        default:
                            ops.Add((OperatorDeclaration)declaration);
                            break;
                    }
                }

                var defaultPropertiesBlock = TryParseDefaultProperties();
                if (defaultPropertiesBlock == null)
                {
                    return Error("Expected defaultproperties block!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                // TODO: should AST-nodes accept null values? should they make sure they dont present any?
                return new Class(name.Value, specs, variables, types, funcs, states, parentClass, outerClass, ops, defaultPropertiesBlock, name.StartPosition, name.EndPosition);
            }
        }

        public VariableDeclaration TryParseVarDecl()
        {
            return (VariableDeclaration)Tokens.TryGetTree(DeclarationParser);
            ASTNode DeclarationParser()
            {
                var startPos = CurrentPosition;
                if (Tokens.ConsumeToken(TokenType.InstanceVariable) == null) return null;
                string category = null;
                if (CurrentTokenType == TokenType.LeftParenth)
                {
                    Tokens.Advance();
                    if (Tokens.ConsumeToken(TokenType.Word) is Token<string> categoryToken)
                    {
                        category = categoryToken.Value;
                    }

                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null)
                    {
                        return Error("Expected ')' after category name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    }
                }

                var specs = ParseSpecifiers(GlobalLists.VariableSpecifiers);

                var type = TryParseEnum() ?? TryParseStruct() ?? TryParseType();
                if (type == null) return Error("Expected variable type or struct/enum type declaration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var var = ParseVariableName();
                if (var == null) return Error("Malformed variable name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (CurrentTokenType == TokenType.Comma)
                {
                    return Error("All variables must be declared on their own line!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                var semicolon = Tokens.ConsumeToken(TokenType.SemiColon);
                if (semicolon == null) return Error("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new VariableDeclaration(type, specs, var, category, startPos, semicolon.EndPosition);
            }
        }

        public Struct TryParseStruct()
        {
            return (Struct)Tokens.TryGetTree(StructParser);
            ASTNode StructParser()
            {
                if (Tokens.ConsumeToken(TokenType.Struct) == null) return null;

                var specs = ParseSpecifiers(GlobalLists.StructSpecifiers);

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null) return Error("Expected struct name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var parent = TryParseParent();

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null) return Error("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var vars = new List<VariableDeclaration>();
                do
                {
                    var variable = TryParseVarDecl();
                    if (variable == null) return Error("Malformed struct content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    vars.Add(variable);
                } while (CurrentTokenType != TokenType.RightBracket && !Tokens.AtEnd());

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null) return Error("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new Struct(name.Value, specs, vars, name.StartPosition, name.EndPosition, parent);
            }
        }

        public Enumeration TryParseEnum()
        {
            return (Enumeration)Tokens.TryGetTree(EnumParser);
            ASTNode EnumParser()
            {
                if (Tokens.ConsumeToken(TokenType.Enumeration) == null) return null;

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null) return Error("Expected enumeration name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null) return Error("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var identifiers = new List<VariableIdentifier>();
                do
                {
                    var ident = Tokens.ConsumeToken(TokenType.Word);
                    if (ident == null) return Error("Expected non-empty enumeration!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    identifiers.Add(new VariableIdentifier(ident.Value, ident.StartPosition, ident.EndPosition));
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightBracket) return Error("Malformed enumeration content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                } while (CurrentTokenType != TokenType.RightBracket);

                if (Tokens.ConsumeToken(TokenType.RightBracket) == null) return Error("Expected '}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new Enumeration(name.Value, identifiers, name.StartPosition, name.EndPosition);
            }
        }

        public Function TryParseFunction()
        {
            return (Function)Tokens.TryGetTree(StubParser);
            ASTNode StubParser()
            {
                var specs = ParseSpecifiers(GlobalLists.FunctionSpecifiers);

                bool isEvent = false;
                if (Tokens.ConsumeToken(TokenType.Event) != null)
                {
                    isEvent = true;
                }
                else if (Tokens.ConsumeToken(TokenType.Function) == null)
                {
                    return null;
                }

                Token<string> returnType = null, name = null;

                var firstString = Tokens.ConsumeToken(TokenType.Word);
                if (firstString == null) return Error("Expected function name or return type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var secondString = Tokens.ConsumeToken(TokenType.Word);
                if (secondString == null)
                    name = firstString;
                else
                {
                    returnType = firstString;
                    name = secondString;
                }

                VariableType retVarType = returnType != null ? new VariableType(returnType.Value, returnType.StartPosition, returnType.EndPosition) : null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null) return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var parameters = new List<FunctionParameter>();
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var param = TryParseParameter();
                    if (param == null) return Error("Malformed parameter!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    parameters.Add(param);
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth) return Error("Unexpected parameter content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null) return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody body = new CodeBody(null, CurrentPosition, CurrentPosition);
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                    {
                        return Error("Malformed function body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    }

                    body = new CodeBody(null, bodyStart, bodyEnd);
                }

                return new Function(name.Value, retVarType, body, specs, parameters, isEvent, name.StartPosition, name.EndPosition);
            }
        }

        public State TryParseState()
        {
            return (State)Tokens.TryGetTree(StateSkeletonParser);
            ASTNode StateSkeletonParser()
            {
                var startPos = CurrentPosition;
                var specs = ParseSpecifiers(GlobalLists.StateSpecifiers);

                if (Tokens.ConsumeToken(TokenType.State) == null) return null;

                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null) return Error("Expected state name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var parent = TryParseParent();

                if (Tokens.ConsumeToken(TokenType.LeftBracket) == null) return Error("Expected '{'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                List<Function> ignores = new List<Function>();
                if (Tokens.ConsumeToken(TokenType.Ignores) != null)
                {
                    do
                    {
                        VariableIdentifier variable = TryParseVariable();
                        if (variable == null) return Error("Malformed ignore statement!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                        ignores.Add(new Function(variable.Name, null, null, null, null, false, variable.StartPos, variable.EndPos));
                    } while (Tokens.ConsumeToken(TokenType.Comma) != null);

                    if (Tokens.ConsumeToken(TokenType.SemiColon) == null) return Error("Expected semi-colon!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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
                    return Error("Malformed state body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null) return Error("Expected semi-colon at end of state!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var body = new CodeBody(new List<Statement>(), bodyStart, bodyEnd);

                var parentState = parent != null ? new State(parent.Name, null, null, null, null, null, null, parent.StartPos, parent.EndPos) : null;
                return new State(name.Value, body, specs, parentState, funcs, ignores, null, name.StartPosition, CurrentPosition);
            }
        }

        public OperatorDeclaration TryParseOperatorDecl()
        {
            return (OperatorDeclaration)Tokens.TryGetTree(OperatorParser);
            ASTNode OperatorParser()
            {
                var specs = ParseSpecifiers(GlobalLists.FunctionSpecifiers);

                var token = Tokens.ConsumeToken(TokenType.Operator) ?? Tokens.ConsumeToken(TokenType.PreOperator) ?? Tokens.ConsumeToken(TokenType.PostOperator) ?? new Token<string>(TokenType.INVALID);

                if (token.Type == TokenType.INVALID) return null;

                Token<string> precedence = null;
                if (token.Type == TokenType.Operator)
                {
                    if (Tokens.ConsumeToken(TokenType.LeftParenth) == null) return Error("Expected '('! (Did you forget to specify operator precedence?)", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    precedence = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (precedence == null) return Error("Expected an integer number!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    if (Tokens.ConsumeToken(TokenType.RightParenth) == null) return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                Token<string> returnType = null, name = null;
                var firstString = TryParseOperatorIdentifier();
                if (firstString == null) return Error("Expected operator name or return type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var secondString = TryParseOperatorIdentifier();
                if (secondString == null)
                    name = firstString;
                else
                {
                    returnType = firstString;
                    name = secondString;
                }

                VariableType retVarType = returnType != null ? new VariableType(returnType.Value, returnType.StartPosition, returnType.EndPosition) : null;

                if (Tokens.ConsumeToken(TokenType.LeftParenth) == null) return Error("Expected '('!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var operands = new List<FunctionParameter>();
                while (CurrentTokenType != TokenType.RightParenth)
                {
                    var operand = TryParseParameter();
                    if (operand == null) return Error("Malformed operand!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    operands.Add(operand);
                    if (Tokens.ConsumeToken(TokenType.Comma) == null && CurrentTokenType != TokenType.RightParenth) return Error("Unexpected operand content!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                }

                if (token.Type == TokenType.Operator && operands.Count != 2)
                    return Error("In-fix operators requires exactly 2 parameters!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                else if (token.Type != TokenType.Operator && operands.Count != 1) return Error("Post/Pre-fix operators requires exactly 1 parameter!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                if (Tokens.ConsumeToken(TokenType.RightParenth) == null) return Error("Expected ')'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                CodeBody body = new CodeBody(null, CurrentPosition, CurrentPosition);
                SourcePosition bodyStart = null, bodyEnd = null;
                if (Tokens.ConsumeToken(TokenType.SemiColon) == null)
                {
                    if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out bodyStart, out bodyEnd)) return Error("Malformed operator body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                    body = new CodeBody(null, bodyStart, bodyEnd);
                }

                // TODO: determine if operator should be a delimiter! (should only symbol-based ones be?)
                if (token.Type == TokenType.PreOperator)
                    return new PreOpDeclaration(name.Value, false, body, retVarType, operands.First(), specs, name.StartPosition, name.EndPosition);
                else if (token.Type == TokenType.PostOperator)
                    return new PostOpDeclaration(name.Value, false, body, retVarType, operands.First(), specs, name.StartPosition, name.EndPosition);
                else
                    return new InOpDeclaration(name.Value, int.Parse(precedence.Value), false, body, retVarType, operands.First(), operands.Last(), specs, name.StartPosition, name.EndPosition);
            }
        }

        public DefaultPropertiesBlock TryParseDefaultProperties()
        {
            return (DefaultPropertiesBlock)Tokens.TryGetTree(DefaultPropertiesParser);
            ASTNode DefaultPropertiesParser()
            {

                if (Tokens.ConsumeToken(TokenType.DefaultProperties) == null) return null;

                if (!ParseScopeSpan(TokenType.LeftBracket, TokenType.RightBracket, false, out SourcePosition bodyStart, out SourcePosition bodyEnd))
                {
                    return Error("Malformed defaultproperties body!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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
                var paramSpecs = ParseSpecifiers(GlobalLists.ParameterSpecifiers);



                var type = TryParseType();
                if (type == null) return Error("Expected parameter type!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                var variable = TryParseVariable();
                if (variable == null) return Error("Expected parameter name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));

                return new FunctionParameter(type, paramSpecs, variable, variable.StartPos, variable.EndPos);
            }
        }

        public VariableType TryParseParent()
        {
            return (VariableType)Tokens.TryGetTree(ParentParser);
            ASTNode ParentParser()
            {
                if (Tokens.ConsumeToken(TokenType.Extends) == null) return null;
                var parentName = Tokens.ConsumeToken(TokenType.Word);
                if (parentName == null)
                {
                    Log.LogError("Expected parent name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new VariableType(parentName.Value, parentName.StartPosition, parentName.EndPosition);
            }
        }

        public VariableType TryParseOuter()
        {
            return (VariableType)Tokens.TryGetTree(OuterParser);
            ASTNode OuterParser()
            {
                if (Tokens.ConsumeToken(TokenType.Within) == null) return null;
                var outerName = Tokens.ConsumeToken(TokenType.Word);
                if (outerName == null)
                {
                    Log.LogError("Expected outer class name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }

                return new VariableType(outerName.Value, outerName.StartPosition, outerName.EndPosition);
            }
        }

        public Specifier TryParseSpecifier(List<TokenType> category)
        {
            return (Specifier)Tokens.TryGetTree(SpecifierParser);
            ASTNode SpecifierParser()
            {
                if (category.Contains(CurrentTokenType))
                {
                    if (CurrentTokenType == TokenType.ConfigSpecifier)
                    {
                        var specNameToken = Tokens.ConsumeToken(CurrentTokenType);
                        if (Tokens.ConsumeToken(TokenType.LeftParenth) != null)
                        {
                            var categoryToken = Tokens.ConsumeToken(TokenType.Word);
                            if (categoryToken != null && Tokens.ConsumeToken(TokenType.RightParenth) != null)
                            {
                                return new ConfigSpecifier(categoryToken.Value, specNameToken.StartPosition, categoryToken.EndPosition.GetModifiedPosition(0, 1, 1));
                            }
                        }
                    }
                    else
                    {
                        var token = Tokens.ConsumeToken(CurrentTokenType);
                        return new Specifier(token.Value, token.StartPosition, token.EndPosition);
                    }
                }

                return null;
            }
        }

        #endregion
        #endregion
        #region Helpers

        public Token<string> TryParseOperatorIdentifier()
        {
            if (GlobalLists.ValidOperatorSymbols.Contains(CurrentTokenType)
                || CurrentTokenType == TokenType.Word)
                return Tokens.ConsumeToken(CurrentTokenType);

            return null;
        }

        public List<Specifier> ParseSpecifiers(List<TokenType> specifierCategory)
        {
            var specs = new List<Specifier>();
            Specifier spec = TryParseSpecifier(specifierCategory);
            while (spec != null)
            {
                specs.Add(spec);
                spec = TryParseSpecifier(specifierCategory);
            }
            return specs;
        }

        //TODO: unused?
        private List<Token<string>> ParseScopedTokens(TokenType scopeStart, TokenType scopeEnd)
        {
            var scopedTokens = new List<Token<string>>();
            if (Tokens.ConsumeToken(scopeStart) == null)
            {
                Log.LogError($"Expected '{scopeStart}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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
            if (!isPartialScope && Tokens.ConsumeToken(scopeStart) == null)
            {
                Log.LogError($"Expected '{scopeStart}'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                return false;
            }
            startPos = Tokens.CurrentItem.StartPosition;

            int nestedLevel = 1;
            while (nestedLevel > 0)
            {
                if (CurrentTokenType == TokenType.EOF)
                {
                    Log.LogError("Scope ended prematurely, are your scopes unbalanced?", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
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
            endPos = Tokens.CurrentItem.StartPosition;
            Tokens.Advance();
            return true;
        }

        #endregion
    }
}
