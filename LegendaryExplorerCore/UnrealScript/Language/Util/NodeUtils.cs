using System;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Utilities;

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
            while (outer?.Outer != null && !(outer is IObjectType))
                outer = outer.Outer;
            return outer as IObjectType;
        }

        public static bool TypeCompatible(VariableType dest, VariableType src, bool coerce = false)
        {
            if (dest is DynamicArrayType destArr && src is DynamicArrayType srcArr)
            {
                return TypeCompatible(destArr.ElementType, srcArr.ElementType);
            }

            if (dest is ClassType destClassType && src is ClassType srcClassType)
            {
                return destClassType.ClassLimiter == srcClassType.ClassLimiter || ((Class)srcClassType.ClassLimiter).SameAsOrSubClassOf(destClassType.ClassLimiter.Name);
            }

            if (dest.PropertyType == EPropertyType.Byte && src.PropertyType == EPropertyType.Byte)
            {
                return true;
            }

            if (dest is DelegateType destDel && src is DelegateType srcDel)
            {
                return true;
                // this seems like how it ought to be done, but there is bioware code that would have only compiled if all delegates are considered the same type
                // maybe log a warning here instead of an error?
                //return destDel.DefaultFunction.SignatureEquals(srcDel.DefaultFunction);
            }

            if (dest is Class destClass)
            {
                if (src is Class srcClass)
                {
                    bool sameAsOrSubClassOf = srcClass.SameAsOrSubClassOf(destClass.Name);
                    if (srcClass.IsInterface)
                    {
                        return sameAsOrSubClassOf || destClass.Implements(srcClass);
                    }

                    if (destClass.IsInterface)
                    {
                        return sameAsOrSubClassOf || srcClass.Implements(destClass);
                    }
                    return sameAsOrSubClassOf 
                        //this seems super wrong obviously. A sane type system would require an explicit downcast.
                        //But to make this work with existing bioware code, it's this, or write a control-flow analyzer that implicitly downcasts based on typecheck conditional gates
                        //I have chosen the lazy path
                        || destClass.SameAsOrSubClassOf(srcClass.Name);
                }

                if (destClass.Name.CaseInsensitiveEquals("Object") && src is ClassType)
                {
                    return true;
                }

                if (src is null)
                {
                    return true;
                }
            }

            if (dest.Name.CaseInsensitiveEquals(src?.Name)) return true;
            ECast cast = CastHelper.GetConversion(dest, src);
            if (coerce)
            {
                return cast != ECast.Max;
            }
            return cast.Has(ECast.AutoConvert);
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
                || (a?.PropertyType == EPropertyType.Vector || a?.PropertyType == EPropertyType.Rotator) && a.PropertyType == b.PropertyType;
        }
    }
}
