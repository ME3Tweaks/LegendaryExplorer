using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Const : VariableType
    {
        public MEGame game;
        public string Value;

        private Expression _literal;
        public Expression Literal
        {
            init => _literal = value;
            get => _literal ??= new ClassOutlineParser(new TokenStream(StringLexer.Lex(Value)), game).ParseConstValue();
        }

        public Const(string name, string value, SourcePosition start = null, SourcePosition end = null) : base(name, start, end)
        {
            Type = ASTNodeType.Const;
            Value = value;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
