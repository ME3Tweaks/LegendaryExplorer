namespace ME3ExplorerCore.UnrealScript.Analysis.Visitors
{
    interface IAcceptASTVisitor
    {
        bool AcceptVisitor(IASTVisitor visitor);
    }
}
