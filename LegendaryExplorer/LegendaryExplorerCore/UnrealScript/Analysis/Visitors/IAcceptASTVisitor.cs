namespace LegendaryExplorerCore.UnrealScript.Analysis.Visitors
{
    interface IAcceptASTVisitor
    {
        bool AcceptVisitor(IASTVisitor visitor, UnrealScriptOptionsPackage usop);
    }
}
