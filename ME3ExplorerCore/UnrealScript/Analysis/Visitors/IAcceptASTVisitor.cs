namespace Unrealscript.Analysis.Visitors
{
    interface IAcceptASTVisitor
    {
        bool AcceptVisitor(IASTVisitor visitor);
    }
}
