namespace ME3Script.Analysis.Visitors
{
    interface IAcceptASTVisitor
    {
        bool AcceptVisitor(IASTVisitor visitor);
    }
}
