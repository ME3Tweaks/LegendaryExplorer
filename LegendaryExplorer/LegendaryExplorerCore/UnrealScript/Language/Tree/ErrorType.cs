namespace LegendaryExplorerCore.UnrealScript.Language.Tree
{
    public class ErrorType : VariableType
    {
        public ASTNode Scope;

        public ErrorType(ASTNode scope) : base("ERROR")
        {
            Scope = scope;
        }
    }
}
