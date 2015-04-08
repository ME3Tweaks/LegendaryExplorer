using ME3Script.Analysis.Visitors;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StaticArrayVariable : Variable
    {
        public int Size;
        public StaticArrayVariable(String name, int size,
            SourcePosition start, SourcePosition end)
            : base(name, start, end)
        {
            Size = size;
            Type = ASTNodeType.StaticArrayVariable;
        }

        public override bool VisitNode(IASTVisitor visitor)
        {
            throw new NotImplementedException();
        }
    }
}
