using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Analysis.Visitors
{
    interface IAcceptASTVisitor
    {
        bool AcceptVisitor(IASTVisitor visitor);
    }
}
