using System;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;
using Microsoft.Toolkit.HighPerformance;

namespace LegendaryExplorerCore.UnrealScript.Language.Util
{
    public static class NodeUtils
    {
        public static Class GetCommonBaseClass(Class a, Class b)
        {
            Class aTemp = a;
            for (; aTemp != null; aTemp = aTemp.Parent as Class)
            {
                Class bTemp = b;
                for (; bTemp != null; bTemp = bTemp.Parent as Class)
                {
                    if (aTemp == bTemp) return aTemp;
                }
            }
            //should never occur, as classes will always have Object in common
            throw new ArgumentException($"Invalid Class objects passed to {nameof(GetCommonBaseClass)}!");
        }

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
            switch (node)
            {
                case Class cls:
                    return cls;
                case ClassType clsType:
                    return (Class)clsType.ClassLimiter;
            }

            var outer = node?.Outer;
            while (outer?.Outer != null && outer is not Class)
                outer = outer.Outer;
            return outer as Class;
        }

        public static ObjectType GetContainingScopeObject(ASTNode node)
        {
            var outer = node.Outer;
            while (outer?.Outer != null && outer is not ObjectType)
                outer = outer.Outer;
            return outer as ObjectType;
        }


        public static bool TypeEqual(VariableType a, VariableType b)
        {
            if (a is DelegateType destDel && b is DelegateType srcDel)
            {
                return destDel.DefaultFunction.SignatureEquals(srcDel.DefaultFunction);
            }
            if (a is DynamicArrayType destArr && b is DynamicArrayType srcArr)
            {
                return TypeEqual(destArr.ElementType, srcArr.ElementType);
            }
            if (a is ClassType aClsType && b is ClassType bClsType)
            {
                return aClsType.ClassLimiter == bClsType.ClassLimiter;
            }

            if (a is StaticArrayType destStaticArr && b is StaticArrayType srcStaticArr)
            {
                return TypeEqual(destStaticArr.ElementType, srcStaticArr.ElementType) && destStaticArr.Length == srcStaticArr.Length;
            }
            return a == b //No type conversion, types must be an exact match
                || a is null && b is Class || a is Class && b is null
                || a is null && b is ClassType || a is ClassType && b is null
                || a is null && b is DelegateType || a is DelegateType && b is null 
                || a is Enumeration && b == SymbolTable.ByteType || a == SymbolTable.ByteType && b is Enumeration
                || (a?.PropertyType is EPropertyType.Vector or EPropertyType.Rotator) && a.PropertyType == b.PropertyType;
        }

        public static Function LookupFunction(this Class @class, string funcName, bool lookInParents = true)
        {
            int firstCharLower = funcName[0] | 0x20;
            while (true)
            {
                foreach (Function func in @class.Functions.AsSpan())
                {
                    string name = func.Name;
                    //will almost always be false, so we want to fail fast 
                    if ((name[0] | 0x20) == firstCharLower && string.Equals(name, funcName, StringComparison.OrdinalIgnoreCase))
                    {
                        return func;
                    }
                }
                if (lookInParents && @class.Parent is Class parentClass)
                {
                    @class = parentClass;
                    continue;
                }
                return null;
            }
        }
    }
}
