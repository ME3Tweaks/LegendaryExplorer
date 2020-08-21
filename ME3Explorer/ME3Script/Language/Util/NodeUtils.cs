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
        public static string GetOuterClassScope(ASTNode node)
        {
            var outer = node.Outer;
            while (outer?.Outer != null && outer.Type != ASTNodeType.Class)
                outer = outer.Outer;
            return ((outer as Class)?.OuterClass as Class)?.GetInheritanceString();
        }

        public static string GetParentClassScope(ASTNode node)
        {
            var outer = node.Outer;
            while (outer?.Outer != null && outer.Type != ASTNodeType.Class)
                outer = outer.Outer;
            return ((outer as Class)?.Parent as Class)?.GetInheritanceString();
        }

        public static Class GetContainingClass(ASTNode node)
        {
            if (node is Class cls)
            {
                return cls;
            }

            if (node is ClassType clsType)
            {
                return (Class)clsType.ClassLimiter;
            }
            var outer = node.Outer;
            while (outer?.Outer != null && outer.Type != ASTNodeType.Class)
                outer = outer.Outer;
            return outer as Class;
        }

        public static IObjectType GetContainingScopeObject(ASTNode node)
        {
            var outer = node.Outer;
            while (outer?.Outer != null && !(outer.Outer is IObjectType))
                outer = outer.Outer;
            return outer as IObjectType;
        }
    }
}
