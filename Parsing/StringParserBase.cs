using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Lexing.Tokenizing;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Parsing
{
    public abstract class StringParserBase
    {
        protected MessageLog Log;
        protected TokenStream<String> Tokens;
        protected TokenType CurrentTokenType
        { get { return Tokens.CurrentItem.Type; } }
        protected SourcePosition CurrentPosition
        { get { return Tokens.CurrentItem.StartPosition; } }

        protected List<ASTNodeType> SemiColonExceptions = new List<ASTNodeType>
        {
            ASTNodeType.WhileLoop,
            ASTNodeType.ForLoop,
            ASTNodeType.IfStatement
        };

        protected List<ASTNodeType> CompositeTypes = new List<ASTNodeType>
        {
            ASTNodeType.Class,
            ASTNodeType.Struct,
            ASTNodeType.Enumeration
        };

        #region Specifier Categories
        protected List<TokenType> VariableSpecifiers = new List<TokenType>
        {
            TokenType.ConfigSpecifier,
            TokenType.GlobalConfigSpecifier,
            TokenType.LocalizedSpecifier,
            TokenType.ConstSpecifier,
            TokenType.PrivateSpecifier,
            TokenType.ProtectedSpecifier,
            TokenType.PrivateWriteSpecifier,
            TokenType.ProtectedWriteSpecifier,
            TokenType.RepNotifySpecifier,
            TokenType.DeprecatedSpecifier,
            TokenType.InstancedSpecifier,
            TokenType.DatabindingSpecifier,
            TokenType.EditorOnlySpecifier,
            TokenType.NotForConsoleSpecifier,
            TokenType.EditConstSpecifier,
            TokenType.EditFixedSizeSpecifier,
            TokenType.EditInlineSpecifier,
            TokenType.EditInlineUseSpecifier,
            TokenType.NoClearSpecifier,
            TokenType.InterpSpecifier,
            TokenType.InputSpecifier,
            TokenType.TransientSpecifier,
            TokenType.DuplicateTransientSpecifier,
            TokenType.NoImportSpecifier,
            TokenType.NativeSpecifier,
            TokenType.ExportSpecifier,
            TokenType.NoExportSpecifier,
            TokenType.NonTransactionalSpecifier,
            TokenType.PointerSpecifier,
            TokenType.InitSpecifier,
            TokenType.RepRetrySpecifier,
            TokenType.AllowAbstractSpecifier
        };

        protected List<TokenType> ClassSpecifiers = new List<TokenType>
        {
            TokenType.AbstractSpecifier,
            TokenType.ConfigSpecifier,
            TokenType.DependsOnSpecifier,
            TokenType.ImplementsSpecifier,
            TokenType.InstancedSpecifier,
            TokenType.ParseConfigSpecifier,
            TokenType.PerObjectConfigSpecifier,
            TokenType.PerObjectLocalizedSpecifier,
            TokenType.TransientSpecifier,
            TokenType.NonTransientSpecifier,
            TokenType.DeprecatedSpecifier
        };

        protected List<TokenType> StructSpecifiers = new List<TokenType>
        {
            TokenType.ImmutableSpecifier,
            TokenType.ImmutableWhenCookedSpecifier,
            TokenType.AtomicSpecifier,
            TokenType.AtomicWhenCookedSpecifier,
            TokenType.StrictConfigSpecifier,
            TokenType.TransientSpecifier,
            TokenType.NativeSpecifier
        };

        protected List<TokenType> FunctionSpecifiers = new List<TokenType>
        {
            TokenType.PrivateSpecifier,
            TokenType.ProtectedSpecifier,
            TokenType.PublicSpecifier,
            TokenType.StaticSpecifier,
            TokenType.FinalSpecifier,
            TokenType.ExecSpecifier,
            TokenType.K2CallSpecifier,
            TokenType.K2OverrideSpecifier,
            TokenType.K2PureSpecifier,
            TokenType.SimulatedSpecifier,
            TokenType.SingularSpecifier,
            TokenType.ClientSpecifier,
            TokenType.DemoRecordingSpecifier,
            TokenType.ReliableSpecifier,
            TokenType.ServerSpecifier,
            TokenType.UnreliableSpecifier,
            TokenType.ConstSpecifier,
            TokenType.IteratorSpecifier,
            TokenType.LatentSpecifier,
            TokenType.NativeSpecifier,
            TokenType.NoExportSpecifier
        };

        protected List<TokenType> ParameterSpecifiers = new List<TokenType>
        {
            TokenType.CoerceSpecifier,
            TokenType.ConstSpecifier,
            TokenType.InitSpecifier,
            TokenType.OptionalSpecifier,
            TokenType.OutSpecifier,
            TokenType.SkipSpecifier
        };

        protected List<TokenType> StateSpecifiers = new List<TokenType>
        {
            TokenType.AutoSpecifier,
            TokenType.SimulatedSpecifier
        };

        #endregion

        public List<VariableIdentifier> ParseVariableNames()
        {
            List<VariableIdentifier> vars = new List<VariableIdentifier>();
            do
            {
                VariableIdentifier variable = TryParseVariable();
                if (variable == null)
                {
                    Log.LogError("Expected at least one variable name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                vars.Add(variable);
            } while (Tokens.ConsumeToken(TokenType.Comma) != null);
            // TODO: This allows a trailing comma before semicolon, intended?
            return vars;
        }

        public VariableIdentifier TryParseVariable()
        {
            Func<ASTNode> variableParser = () =>
            {
                var name = Tokens.ConsumeToken(TokenType.Word);
                if (name == null)
                    return null;

                if (Tokens.ConsumeToken(TokenType.LeftSqrBracket) != null)
                {
                    var size = Tokens.ConsumeToken(TokenType.IntegerNumber);
                    if (size == null)
                    {
                        Log.LogError("Expected an integer number for size!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }
                    if (Tokens.ConsumeToken(TokenType.RightSqrBracket) != null)
                    {
                        Log.LogError("Expected ']'!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                        return null;
                    }

                    return new VariableIdentifier(name.Value,
                        name.StartPosition, name.EndPosition, Int32.Parse(size.Value));
                }

                return new VariableIdentifier(name.Value, name.StartPosition, name.EndPosition);
            };
            return (VariableIdentifier)Tokens.TryGetTree(variableParser);
        }

        public VariableType TryParseType()
        {
            Func<ASTNode> typeParser = () =>
            {
                // TODO: word or basic datatype? (int float etc)
                var type = Tokens.ConsumeToken(TokenType.Word);
                if (type == null)
                {
                    Log.LogError("Expected type name!", CurrentPosition, CurrentPosition.GetModifiedPosition(0, 1, 1));
                    return null;
                }
                return new VariableType(type.Value, type.StartPosition, type.EndPosition);
            };
            return (VariableType)Tokens.TryGetTree(typeParser);
        }
    }
}
