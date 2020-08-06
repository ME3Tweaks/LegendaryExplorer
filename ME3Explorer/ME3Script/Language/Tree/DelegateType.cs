using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;

namespace ME3Script.Language.Tree
{
    public class DelegateType : VariableType
    {
        public string FunctionName;

        public DelegateType(string functionName, SourcePosition start = null, SourcePosition end = null) : base("delegate", start, end)
        {
            FunctionName = functionName;
        }

        public override bool AcceptVisitor(IASTVisitor visitor)
        {
            return visitor.VisitNode(this);
        }
    }
}
