using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Tree
{
    public class StateSkeleton : State
    {
        public SourcePosition BodyStart;
        public SourcePosition BodyEnd;

        public StateSkeleton(String name, SourcePosition bodyStart, 
            SourcePosition bodyEnd, List<Specifier> specs,
            Variable parent, List<Function> funcs, List<Variable> ignores,
            SourcePosition start, SourcePosition end)
            : base(name, null, specs, parent, funcs, ignores, null, start, end)
        {
            BodyStart = bodyStart;
            BodyEnd = bodyEnd;
            Type = ASTNodeType.StateSkeleton;
        }
    }
}
