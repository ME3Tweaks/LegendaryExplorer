using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Language.Util
{
    public static class NodeUtils
    {
        public static String GetOuterClassScope(ASTNode node)
        {
            var outer = node.Outer;
            while (outer.Type != ASTNodeType.Class)
                outer = outer.Outer;
            return ((outer as Class).OuterClass as Class).GetInheritanceString();
        }

        public static Class GetContainingClass(ASTNode node)
        {
            var outer = node.Outer;
            while (outer.Type != ASTNodeType.Class)
                outer = outer.Outer;
            return outer as Class;
        }
    }
}
