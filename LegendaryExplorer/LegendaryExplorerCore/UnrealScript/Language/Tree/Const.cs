using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;

namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class Const : VariableType
    {
        public MEGame Game;
        public readonly string Value;

        private Expression _literal;
        public Expression Literal
        {
            init => _literal = value;
            get => _literal ??= new ClassOutlineParser(Lexer.Lex(Value), Game).ParseConstValue();
        }

        public Const(string name, string value, int start = -1, int end = -1) : base(name, start, end)
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
