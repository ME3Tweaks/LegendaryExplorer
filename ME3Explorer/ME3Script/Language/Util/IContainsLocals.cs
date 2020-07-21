using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Util
{
    interface IContainsLocals
    {
        List<VariableDeclaration> Locals
        {
            get;
            set;
        }
    }
}
