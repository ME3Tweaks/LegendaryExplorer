using ME3Script.Language.Tree;
using System.Collections.Generic;

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
