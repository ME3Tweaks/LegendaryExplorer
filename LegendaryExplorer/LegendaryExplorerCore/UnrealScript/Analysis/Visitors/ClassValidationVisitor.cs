using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Compiling.Errors;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Visitors
{
    public enum ValidationPass
    {
        TypesAndFunctionNamesAndStateNames,
        ClassAndStructMembersAndFunctionParams,
        BodyPass
    }

    public class ClassValidationVisitor : IASTVisitor
    {

        private readonly SymbolTable Symbols;
        private readonly MessageLog Log;
        private bool Success;

        public ValidationPass Pass;

        public static void RunAllPasses(ASTNode node, MessageLog log, SymbolTable symbols)
        {
            var validator = new ClassValidationVisitor(log, symbols, ValidationPass.TypesAndFunctionNamesAndStateNames);
            node.AcceptVisitor(validator);
            validator.Pass = ValidationPass.ClassAndStructMembersAndFunctionParams;
            node.AcceptVisitor(validator);
            validator.Pass = ValidationPass.BodyPass;
            node.AcceptVisitor(validator);
        }

        public ClassValidationVisitor(MessageLog log, SymbolTable symbols, ValidationPass pass)
        {
            Log = log ?? new MessageLog();
            Symbols = symbols;
            Success = true;
            Pass = pass;
        }

        private bool Error(string msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            Success = false;
            return false;
        }

        public bool VisitNode(Class node)
        {
            switch (Pass)
            {
                case ValidationPass.TypesAndFunctionNamesAndStateNames:
                {
                    // TODO: allow duplicate names as long as its in different packages!
                    if (node.Name != "Object")//validating Object is a special case, as it is the base class for all classes
                    {
                        //ADD CLASSNAME TO SYMBOLS BEFORE VALIDATION pass!
                        //if (!Symbols.TryAddType(node))
                        //{
                        //    return Error($"A class named '{node.Name}' already exists!", node.StartPos, node.EndPos);
                        //}
                        node.Parent.Outer = node;
                        if (!Symbols.TryResolveType(ref node.Parent))
                            return Error($"No parent class named '{node.Parent.Name}' found!", node.Parent.StartPos, node.Parent.EndPos);
                        if (node.Parent.Type != ASTNodeType.Class)
                            return Error($"Parent named '{node.Parent.Name}' is not a class!", node.Parent.StartPos, node.Parent.EndPos);

                        if (node._outerClass != null)
                        {
                            node._outerClass.Outer = node;
                            if (!Symbols.TryResolveType(ref node._outerClass))
                                return Error($"No outer class named '{node._outerClass.Name}' found!", node._outerClass.StartPos, node._outerClass.EndPos);
                            if (node.OuterClass.Type != ASTNodeType.Class)
                                return Error($"Outer named '{node.OuterClass.Name}' is not a class!", node.OuterClass.StartPos, node.OuterClass.EndPos);
                            if (node.Parent.Name == "Actor" && !node.OuterClass.Name.Equals("Object", StringComparison.OrdinalIgnoreCase))
                                return Error("Classes extending 'Actor' can not be inner classes!", node.OuterClass.StartPos, node.OuterClass.EndPos);
                        }

                        for (int i = 0; i < node.Interfaces.Count; i++)
                        {
                            VariableType nodeInterface = node.Interfaces[i];
                            if (!Symbols.TryResolveType(ref nodeInterface, true))
                            {
                                return Error($"No outer class named '{nodeInterface.Name}' found!", nodeInterface.StartPos, nodeInterface.EndPos);
                            }
                            if (!node.IsNative && ((Class)nodeInterface).IsNative)
                            {
                                return Error($"Only a native class can implement a native interface!", nodeInterface.StartPos, nodeInterface.EndPos);
                            }
                            node.Interfaces[i] = nodeInterface;
                        }

                        //specifier validation
                        if (string.Equals(node.ConfigName, "inherit", StringComparison.OrdinalIgnoreCase) && !((Class)node.Parent).Flags.Has(EClassFlags.Config))
                        {
                            return Error($"Cannot inherit config filename from parent class ({node.Parent.Name}) which is not marked as config!", node.StartPos);
                        }
                        //TODO:propagate/check inheritable class flags from parent and implemented interfaces
                        if (node.IsNative && !((Class)node.Parent).IsNative)
                        {
                            return Error($"A native class cannot inherit from a non-native class!", node.StartPos);
                        }
                        Symbols.GoDirectlyToStack(((Class)node.Parent).GetInheritanceString());
                        Symbols.PushScope(node.Name);
                    }



                    //register all the types this class declares
                    foreach (VariableType type in node.TypeDeclarations)
                    {
                        type.Outer = node;
                        Success &= type.AcceptVisitor(this);
                    }

                    //register all the function names (do this here so that delegates will resolve correctly)
                    foreach (Function func in node.Functions)
                    {
                        func.Outer = node;
                        Success &= func.AcceptVisitor(this);
                    }

                    //register all state names (do this here so that states can extend states that are declared later in the class)
                    foreach (State state in node.States)
                    {
                        state.Outer = node;
                        Success &= state.AcceptVisitor(this);
                    }

                    Symbols.RevertToObjectStack();//pops scope until we're in the 'object' scope

                    return Success;
                }
                case ValidationPass.ClassAndStructMembersAndFunctionParams:
                {
                    if (node.Name != "Object")
                    {
                        if (((Class)node.Parent).SameAsOrSubClassOf(node)) // TODO: not needed due to no forward declarations?
                        {
                            return Error($"Extending from '{node.Parent.Name}' causes circular extension!", node.StartPos);
                        }
                        if (!((Class)node.OuterClass).SameAsOrSubClassOf(((Class)node.Parent).OuterClass.Name))
                        {
                            return Error("Outer class must be a sub-class of the parents outer class!", node.StartPos);
                        }
                        if (node.SameAsOrSubClassOf("Interface"))
                        {
                            node.Flags |= EClassFlags.Interface;
                            node.PropertyType = EPropertyType.Interface;
                        }
                        Symbols.GoDirectlyToStack(((Class)node.Parent).GetInheritanceString());
                        string outerScope = null;
                        if (node.OuterClass != null && !string.Equals(node.OuterClass.Name, "Object", StringComparison.OrdinalIgnoreCase))
                        {
                            outerScope = ((Class)node.OuterClass).GetInheritanceString();
                        }

                        Symbols.PushScope(node.Name); //, outerScope);
                    }

                    //second pass over structs to resolve their members
                    foreach (Struct type in node.TypeDeclarations.OfType<Struct>())
                    {
                        Success &= type.AcceptVisitor(this);
                    }

                    //resolve instance variables
                    foreach (VariableDeclaration decl in node.VariableDeclarations)
                    {
                        decl.Outer = node;
                        Success &= decl.AcceptVisitor(this);

                        if (node.Name != "Object" && Symbols.TryGetSymbolInScopeStack<ASTNode>(decl.Name, out _, node.Parent.GetScope()))
                        {
                            Log.LogWarning($"A symbol named '{decl.Name}' exists in a parent class. Are you sure you want to shadow it?", decl.StartPos, decl.EndPos);
                        }
                    }

                    if (node.Name != "Object")
                    {
                        Symbols.TryGetType("Object", out Class objectClass);
                        Symbols.AddSymbol("Class", new VariableDeclaration(new ClassType(node), EPropertyFlags.Const | EPropertyFlags.Native | EPropertyFlags.EditConst, "Class")
                        {
                            Outer = objectClass
                        });
                        Symbols.AddSymbol("Outer", new VariableDeclaration(node.OuterClass, EPropertyFlags.Const | EPropertyFlags.Native | EPropertyFlags.EditConst, "Outer")
                        {
                            Outer = objectClass
                        });
                    }

                    //second pass over functions to resolve parameters 
                    foreach (Function func in node.Functions)
                    {
                        Success &= func.AcceptVisitor(this);
                    }

                    //second pass over states to resolve 
                    foreach (State state in node.States)
                    {
                        Success &= state.AcceptVisitor(this);
                    }

                    Symbols.RevertToObjectStack();//pops scope until we're in the 'object' scope

                    node.Declaration = node;
                    return Success;
                }
                case ValidationPass.BodyPass:
                {
                    //from UDN: "Implementing multiple interface classes which have a common base is not supported and will result in incorrect vtable offsets"
                    if (node.Interfaces.Count > 1)
                    {
                        var interfaceParents = new HashSet<string>();
                        foreach (VariableType interfaceClass in node.Interfaces)
                        {
                            var parentInterface = (interfaceClass as Class)?.Parent as Class;
                            while (parentInterface is not null && !parentInterface.Name.CaseInsensitiveEquals("Interface"))
                            {
                                if (interfaceParents.Contains(parentInterface.Name))
                                {
                                    return Error("Cannot implement two interfaces that have a common base interface lower than the Interface class", node.StartPos);
                                }
                                parentInterface = parentInterface.Parent as Class;
                            }
                        }
                    }

                    //third pass over structs to check for circular inheritance chains
                    foreach (Struct type in node.TypeDeclarations.OfType<Struct>())
                    {
                        Success &= type.AcceptVisitor(this);
                    }

                    //third pass over functions to check overriding rules
                    foreach (Function func in node.Functions)
                    {
                        Success &= func.AcceptVisitor(this);
                    }

                    //third pass over states to check function overrides 
                    foreach (State state in node.States)
                    {
                        Success &= state.AcceptVisitor(this);
                    }

                    //second pass to resolve EPropertyFlags.NeedCtorLink for Struct Properties
                    foreach (VariableDeclaration decl in node.VariableDeclarations)
                    {
                        Success &= decl.AcceptVisitor(this);
                        if (decl.Flags.Has(EPropertyFlags.Component))
                        {
                            node.Flags |= EClassFlags.HasComponents;
                        }
                        if (decl.Flags.Has(EPropertyFlags.CrossLevel))
                        {
                            node.Flags |= EClassFlags.HasCrossLevelRefs;
                        }
                        if (decl.Flags.Has(EPropertyFlags.Config))
                        {
                            node.Flags |= EClassFlags.Config;
                        }
                        if (decl.Flags.Has(EPropertyFlags.Localized))
                        {
                            node.Flags |= EClassFlags.Localized;
                        }
                        if (decl.IsOrHasInstancedObjectProperty())
                        {
                            node.Flags |= EClassFlags.HasInstancedProps;
                        }
                    }

                    return Success;
                }
                default:
                    return Success;
            }
        }


        public bool VisitNode(VariableDeclaration node) => VisitVarDecl(node);

        public bool VisitNode(FunctionParameter node) => VisitVarDecl(node);

        public bool VisitVarDecl(VariableDeclaration node, bool needsAdd = true)
        {
            if (Pass is ValidationPass.ClassAndStructMembersAndFunctionParams)
            {
                if (needsAdd)
                {
                    node.VarType.Outer = node;
                    if (!Symbols.TryResolveType(ref node.VarType))
                    {
                        return Error($"No type named '{node.VarType.Name}' exists!", node.VarType.StartPos, node.VarType.EndPos);
                    }

                    if (Symbols.SymbolExistsInCurrentScope(node.Name))
                    {
                        return Error($"A {(node is FunctionParameter ? "parameter" : "member")} named '{node.Name}' already exists in this {node.Outer.Type}!", node.StartPos, node.EndPos);
                    }
                    Symbols.AddSymbol(node.Name, node);
                }

                VariableType nodeVarType = (node.VarType as StaticArrayType)?.ElementType ?? node.VarType;
                if (nodeVarType is DelegateType ||
                    !node.Flags.Has(EPropertyFlags.Native) && nodeVarType is DynamicArrayType or { PropertyType: EPropertyType.String })
                {
                    node.Flags |= EPropertyFlags.NeedCtorLink;
                }
            }
            else if (Pass is ValidationPass.BodyPass)
            {
                //should component flag be set when this is a function parameter?
                switch ((node.VarType as StaticArrayType)?.ElementType ?? node.VarType)
                {
                    case DynamicArrayType {ElementType: VariableType elType} dynArrType:
                        if (elType is Class {IsComponent: true})
                        {
                            dynArrType.ElementPropertyFlags |= EPropertyFlags.Component;
                            node.Flags |= EPropertyFlags.Component;
                        }
                        else if (elType is DelegateType ||
                                 !node.Flags.Has(EPropertyFlags.Native) && (elType.PropertyType is EPropertyType.String ||
                                                                                elType is Struct elStruct && StructNeedsCtorLink(elStruct, new Stack<Struct> { elStruct })))
                        {
                            dynArrType.ElementPropertyFlags |= EPropertyFlags.NeedCtorLink;
                        }
                        break;
                    case Class {IsComponent: true}:
                        node.Flags |= EPropertyFlags.Component;
                        break;
                    case Struct strct:
                        if (!node.Flags.Has(EPropertyFlags.Native) && StructNeedsCtorLink(strct, new Stack<Struct> { strct }))
                        {
                            node.Flags |= EPropertyFlags.NeedCtorLink;
                        }
                        break;
                }

                bool StructNeedsCtorLink(Struct s1, Stack<Struct> stack)
                {
                    foreach (VariableDeclaration strctVariableDeclaration in s1.VariableDeclarations)
                    {
                        if (strctVariableDeclaration.Flags.Has(EPropertyFlags.NeedCtorLink))
                        {
                            return true;
                        }
                        else if (strctVariableDeclaration.VarType is Struct s2)
                        {
                            if (stack.Contains(s2))
                            {
                                stack.Push(s2);
                                Error($"Detected circular reference in these structs: {string.Join(" -> ", stack.Reverse().Select(s3 => s3.Name))}");
                                stack.Pop();
                                return true;
                            }
                            stack.Push(s2);
                            if (StructNeedsCtorLink(s2, stack))
                            {
                                return true;
                            }
                            stack.Pop();
                        }
                    }
                    if (s1.Parent is Struct parentStruct)
                    {
                        stack.Push(parentStruct);
                        if (StructNeedsCtorLink(parentStruct, stack))
                        {
                            return true;
                        }
                        stack.Pop();
                    }
                    return false;
                }
            }

            return Success;
        }

        public bool VisitNode(VariableType node)
        {
            // This should never be called.
            throw new NotImplementedException();
        }

        public bool VisitNode(DynamicArrayType node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(StaticArrayType node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DelegateType node)
        {
            throw new NotImplementedException();
        }
        public bool VisitNode(ClassType node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(Struct node)
        {
            if (Pass == ValidationPass.TypesAndFunctionNamesAndStateNames)
            {
                if (!Symbols.TryAddType(node))
                {
                    //Structs do not have to be globally unique, but they do have to be unique within a scope
                    if (node.Outer is ObjectType nodeOuter && nodeOuter.TypeDeclarations.Any(decl => decl != node && decl.Name.CaseInsensitiveEquals(node.Name)))
                    {
                        return Error($"A type named '{node.Name}' already exists in this {nodeOuter.GetType().Name.ToLower()}!", node.StartPos, node.EndPos);
                    }
                }

                Symbols.PushScope(node.Name);

                //register types of inner structs
                foreach (VariableType typeDeclaration in node.TypeDeclarations)
                {
                    typeDeclaration.Outer = node;
                    Success &= typeDeclaration.AcceptVisitor(this);
                }

                Symbols.PopScope();
            }
            else if (Pass == ValidationPass.ClassAndStructMembersAndFunctionParams)
            {
                string parentScope = null;
                if (node.Parent != null)
                {
                    node.Parent.Outer = node;
                    if (!Symbols.TryResolveType(ref node.Parent))
                    {
                        return Error($"No parent struct named '{node.Parent.Name}' found!", node.Parent.StartPos, node.Parent.EndPos);
                    }

                    if (node.Parent.Type != ASTNodeType.Struct)
                        return Error($"Parent named '{node.Parent.Name}' is not a struct!", node.Parent.StartPos, node.Parent.EndPos);

                    parentScope = $"{NodeUtils.GetContainingClass(node.Parent).GetInheritanceString()}.{node.Parent.Name}";
                }

                Symbols.PushScope(node.Name, parentScope);

                //second pass for inner struct members
                foreach (VariableType typeDeclaration in node.TypeDeclarations)
                {
                    Success &= typeDeclaration.AcceptVisitor(this);
                }
                
                foreach (VariableDeclaration decl in node.VariableDeclarations)
                {
                    decl.Outer = node;
                    Success = Success && decl.AcceptVisitor(this);

                    var parentStruct = node.Parent as Struct;
                    while (parentStruct is not null)
                    {
                        if (parentStruct.VariableDeclarations.Any(parentVarDecl => parentVarDecl.Name.CaseInsensitiveEquals(decl.Name)))
                        {
                            Log.LogWarning($"A member name '{decl.Name}' exists in a parent struct. Are you sure you want to shadow it?", decl.StartPos, decl.EndPos);
                        }
                        parentStruct = parentStruct.Parent as Struct;
                    }
                }

                Symbols.PopScope();

                node.Declaration = node;
            }
            else if (Pass == ValidationPass.BodyPass)
            {
                if (node.Parent is Struct parentStruct && parentStruct.SameOrSubStruct(node.Name))
                {
                    return Error($"Extending from '{parentStruct.Name}' causes circular extension!", parentStruct.StartPos, parentStruct.EndPos);
                }

                //second pass to resolve EPropertyFlags.NeedCtorLink for Struct Properties
                foreach (VariableDeclaration decl in node.VariableDeclarations)
                {
                    Success &= decl.AcceptVisitor(this);
                }
                if (HasComponents(node))
                {
                    node.Flags |= ScriptStructFlags.HasComponents;
                }

                static bool HasComponents(Struct strct)
                {
                    bool hasComponents = false;
                    foreach (VariableDeclaration decl in strct.VariableDeclarations)
                    {
                        if (decl.Flags.Has(EPropertyFlags.Component))
                        {
                            hasComponents = true;
                        }
                        var varType = decl.VarType is StaticArrayType staticArrayType ? staticArrayType.ElementType : decl.VarType;
                        if (varType is DynamicArrayType dynArrType)
                        {
                            varType = dynArrType.ElementType;
                        }
                        if (varType is Struct innerStruct && (innerStruct.Flags.Has(ScriptStructFlags.HasComponents) || HasComponents(innerStruct)))
                        {
                            decl.Flags |= EPropertyFlags.Component;
                            hasComponents = true;
                        }
                    }
                    return hasComponents;
                }
            }
            return Success;
        }

        public bool VisitNode(Enumeration node)
        {
            if (Pass == ValidationPass.TypesAndFunctionNamesAndStateNames)
            {
                if (!Symbols.TryAddType(node))
                {
                    //Enums do not have to be globally unique, but they do have to be unique within a scope
                    if (((ObjectType)node.Outer).TypeDeclarations.Any(decl => decl != node && decl.Name.CaseInsensitiveEquals(node.Name)))
                    {
                        return Error($"A type named '{node.Name}' already exists in this {node.Outer.GetType().Name.ToLower()}!", node.StartPos, node.EndPos);
                    }
                }

                Symbols.PushScope(node.Name);

                string maxName = node.GenerateMaxName();

                foreach (EnumValue enumVal in node.Values)
                {
                    enumVal.Outer = node;
                    if (!Symbols.TryAddSymbol(enumVal.Name, enumVal))
                    {
                        return Error($"'{enumVal.Name}' already exists in this enum!", enumVal.StartPos, enumVal.EndPos);
                    }
                    ;
                    if (maxName.CaseInsensitiveEquals(enumVal.Name))
                    {
                        return Error($"'{maxName}' is the autogenerated end value for this enum! It cannot be used as a regular value.", enumVal.StartPos, enumVal.EndPos);
                    }
                }

                Symbols.PopScope();

                node.Declaration = node;
            }

            return Success;
        }

        public bool VisitNode(Const node)
        {
            if (Pass == ValidationPass.TypesAndFunctionNamesAndStateNames)
            {
                if (!Symbols.TryAddType(node))
                {
                    //Consts do not have to be globally unique, but they do have to be unique within a scope
                    if (((ObjectType)node.Outer).TypeDeclarations.Any(decl => decl != node && decl.Name.CaseInsensitiveEquals(node.Name)))
                    {
                        return Error($"A type named '{node.Name}' already exists in this {node.Outer.GetType().Name.ToLower()}!", node.StartPos, node.EndPos);
                    }
                }


                node.Declaration = node;
            }

            return Success;
        }

        public bool VisitNode(Function node)
        {
            if (Pass == ValidationPass.TypesAndFunctionNamesAndStateNames)
            {
                if (Symbols.SymbolExistsInCurrentScope(node.Name))
                    return Error($"The name '{node.Name}' is already in use in this class!", node.StartPos, node.EndPos);

                Symbols.AddSymbol(node.Name, node);
                return Success;
            }

            if (Pass == ValidationPass.ClassAndStructMembersAndFunctionParams)
            {
                Symbols.PushScope(node.Name);

                if (node.ReturnValueDeclaration != null)
                {
                    node.ReturnValueDeclaration.Outer = node;
                    Success &= node.ReturnValueDeclaration.AcceptVisitor(this);
                }

                foreach (FunctionParameter param in node.Parameters)
                {
                    param.Outer = node;
                    Success &= param.AcceptVisitor(this);
                }

                //foreach (VariableDeclaration local in node.Locals)
                //{
                //    local.Outer = node;
                //    Success &= local.AcceptVisitor(this);
                //}
                Symbols.PopScope();

                if (Success == false)
                    return Error("Error in function parameters.", node.StartPos, node.EndPos);


                if (node.FriendlyName is not null //true in ME1, ME2, LE1, and LE2
                 && node.IsOperator)
                {
                    if (node.Flags.Has(EFunctionFlags.PreOperator))
                    {
                        if (node.Parameters.Count != 1)
                        {
                            return Error($"{node.Name} is declared as a prefix operator, so it must have exactly one parameter!", node.StartPos, node.EndPos);
                        }
                        Symbols.AddOperator(new PreOpDeclaration(node.FriendlyName, node.ReturnType, node.NativeIndex, node.Parameters[0]) { Implementer = node });
                    }
                    else
                    {
                        switch (node.Parameters.Count)
                        {
                            case 1:
                                Symbols.AddOperator(new PostOpDeclaration(node.FriendlyName, node.ReturnType, node.NativeIndex, node.Parameters[0]) { Implementer = node });
                                break;
                            case 2:
                                Symbols.AddOperator(new InOpDeclaration(node.FriendlyName, node.OperatorPrecedence, node.NativeIndex, node.ReturnType, node.Parameters[0], node.Parameters[1])
                                {
                                    Implementer = node
                                });
                                Symbols.InFixOperatorSymbols.Add(node.FriendlyName);
                                break;
                            default:
                                return Error($"{node.Name} is declared as an operator, so it must have either 1 or 2 parameters!", node.StartPos, node.EndPos);
                        }
                    }
                }

                return Success;
            }

            //for validating proper override behavior
            if (Pass == ValidationPass.BodyPass)
            {
                Class containingClass = NodeUtils.GetContainingClass(node);
                Function superFunc = null;
                if (node.Outer is State state)
                {
                    state = state.Parent;
                    while (state is not null)
                    {
                        string stateScope = $"{((Class)state.Outer).GetInheritanceString()}.{state.Name}";
                        if (Symbols.TryGetSymbolFromSpecificScope(node.Name, out superFunc, stateScope))
                        {
                            break;
                        }

                        state = state.Parent;
                    }
                }
                if (superFunc is null)
                {
                    Class parentScopeClass = node.Outer is State ? containingClass : containingClass.Parent as Class;
                    if (parentScopeClass is not null)
                    {
                        Symbols.TryGetSymbolInScopeStack(node.Name, out superFunc, parentScopeClass.GetInheritanceString());
                    }
                }

                if (superFunc is not null)
                {
                    if (superFunc.Flags.Has(EFunctionFlags.Private))
                    {
                        superFunc = null;
                    }
                    else
                    {
                        // If there is a function with this name that we should override, validate the new functions declaration
                        if (superFunc.Flags.Has(EFunctionFlags.Final))
                            return Error($"{node.Name} overrides a function in a parent class, but the parent function is marked as final!", node.StartPos, node.EndPos);
                        if (!NodeUtils.TypeEqual(node.ReturnType, superFunc.ReturnType))
                            return Error($"{node.Name} overrides a function in a parent class, but the functions do not have the same return types!", node.StartPos, node.EndPos);

                        if (node.Parameters.Count != superFunc.Parameters.Count)
                        {
                            if (node.Outer is State)
                            {
                                //Contrary to what the unrealscript docs say, states can apparently have functions with the same name as a class function, but with different number of params.
                                superFunc = null; 
                            }
                            else
                            {
                                return Error($"{node.Name} overrides a function in a parent class, but the functions do not have the same number of parameters!", node.StartPos, node.EndPos);
                            }
                        }
                        else
                        {
                            for (int n = 0; n < node.Parameters.Count; n++)
                            {
                                if (node.Parameters[n].Type != superFunc.Parameters[n].Type)
                                    return Error($"{node.Name} overrides a function in a parent class, but the functions do not have the same parameter types!", node.StartPos, node.EndPos);
                            }
                        }

                        node.SuperFunction = superFunc;
                    }
                }
                
                if (superFunc is null && node.Outer is State && node.Flags.Has(EFunctionFlags.Net))
                {
                    return Error("If a state function has the Net flag, it must override a class function", node.StartPos, node.EndPos);
                }


                if (node.ReturnValueDeclaration != null)
                {
                    Success &= node.ReturnValueDeclaration.AcceptVisitor(this);
                }

                foreach (FunctionParameter param in node.Parameters)
                {
                    param.Outer = node;
                    Success &= param.AcceptVisitor(this);
                }

                if (node.ReturnValueDeclaration is not null)
                {
                    //if the return type is > 64 bytes, it can't be allocated on the stack.
                    node.RetValNeedsDestruction = node.ReturnValueDeclaration.Flags.Has(EPropertyFlags.NeedCtorLink) || node.ReturnType.Size(Symbols.Game) > 64;
                }
            }
            return Success;
        }

        public bool VisitNode(State node)
        {
            if (Pass == ValidationPass.TypesAndFunctionNamesAndStateNames)
            {
                if (Symbols.SymbolExistsInCurrentScope(node.Name))
                    return Error($"The name '{node.Name}' is already in use in this class!", node.StartPos, node.EndPos);
                Symbols.AddSymbol(node.Name, node);
                return Success;
            }

            if (Pass == ValidationPass.ClassAndStructMembersAndFunctionParams)
            {
                bool overrides = Symbols.TryGetSymbolInScopeStack(node.Name, out ASTNode overrideState, NodeUtils.GetParentClassScope(node))
                              && overrideState.Type == ASTNodeType.State;

                if (node.Parent is null)
                {
                    if (overrides)
                    {
                        node.Parent = overrideState as State;
                    }
                }
                else
                {
                    if (overrides)
                        Error("A state is not allowed to both override a parent class's state and extend another state at the same time!", node.StartPos, node.EndPos);

                    if (!Symbols.TryGetSymbol(node.Parent.Name, out ASTNode parent))
                    {
                        Error($"No parent state named '{node.Parent.Name}' found in the current class!", node.Parent.StartPos, node.Parent.EndPos);
                        node.Parent = null;
                    }

                    if (parent != null)
                    {
                        if (parent.Type != ASTNodeType.State)
                            Error($"Parent named '{node.Parent.Name}' is not a state!", node.Parent.StartPos, node.Parent.EndPos);
                        else
                            node.Parent = parent as State;
                    }
                }
                
                string parentScope = node.Parent is not null ? $"{NodeUtils.GetContainingClass(node.Parent)?.GetInheritanceString()}.{node.Parent.Name}" : null;
                Symbols.PushScope(node.Name, parentScope);

                foreach (Function func in node.Functions)
                {
                    func.Outer = node;
                    Symbols.AddSymbol(func.Name, func);
                    Success = Success && func.AcceptVisitor(this);
                }
                //TODO: check functions overrides:
                //if the state overrides another state, we should be in that scope as well when we check overrides maybe?
                //if the state has a parent state, we should be in that scope
                //this is a royal mess, check that ignores also look-up from parent/overriding states as we are not sure if symbols are in the scope

                // if the state extends a parent state, use that as outer in the symbol lookup
                // if the state overrides another state, use that as outer
                // both of the above should apply to functions as well as ignores.

                //TODO: state code/labels

                Symbols.PopScope();
                return Success;
            }

            if (Pass == ValidationPass.BodyPass)
            {
                //check overriding rules
                foreach (Function func in node.Functions)
                {
                    Success &= func.AcceptVisitor(this);
                }
            }

            return Success;
        }

        #region Unused
        public bool VisitNode(CodeBody node)
        { throw new NotImplementedException(); }
        public bool VisitNode(Label node)
        { throw new NotImplementedException(); }

        public bool VisitNode(VariableIdentifier node)
        { throw new NotImplementedException(); }
        public bool VisitNode(EnumValue node)
        { throw new NotImplementedException(); }

        public bool VisitNode(DoUntilLoop node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ForLoop node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ForEachLoop node)
        { throw new NotImplementedException(); }
        public bool VisitNode(WhileLoop node)
        { throw new NotImplementedException(); }

        public bool VisitNode(SwitchStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(CaseStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(DefaultCaseStatement node)
        { throw new NotImplementedException(); }

        public bool VisitNode(AssignStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(AssertStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(BreakStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ContinueStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(IfStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ReturnStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ReturnNothingStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StopStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StateGoto node)
        { throw new NotImplementedException(); }
        public bool VisitNode(Goto node)
        { throw new NotImplementedException(); }

        public bool VisitNode(ExpressionOnlyStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ReplicationStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ErrorStatement node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ErrorExpression node)
        { throw new NotImplementedException(); }

        public bool VisitNode(InOpReference node)
        { throw new NotImplementedException(); }
        public bool VisitNode(PreOpReference node)
        { throw new NotImplementedException(); }
        public bool VisitNode(PostOpReference node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StructComparison node)
        { throw new NotImplementedException(); }
        public bool VisitNode(DelegateComparison node)
        { throw new NotImplementedException(); }
        public bool VisitNode(NewOperator node)
        { throw new NotImplementedException(); }

        public bool VisitNode(FunctionCall node)
        { throw new NotImplementedException(); }

        public bool VisitNode(DelegateCall node)
        { throw new NotImplementedException(); }

        public bool VisitNode(ArraySymbolRef node)
        { throw new NotImplementedException(); }
        public bool VisitNode(CompositeSymbolRef node)
        { throw new NotImplementedException(); }
        public bool VisitNode(SymbolReference node)
        { throw new NotImplementedException(); }
        public bool VisitNode(DefaultReference node)
        { throw new NotImplementedException(); }
        public bool VisitNode(DynArrayLength node)
        { throw new NotImplementedException(); }

        public bool VisitNode(DynArrayAdd node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayAddItem node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayInsert node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayInsertItem node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayRemove node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayRemoveItem node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayFind node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArrayFindStructMember node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(DynArraySort node)
        {
            throw new NotImplementedException();
        }
        public bool VisitNode(DynArrayIterator node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(BooleanLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(FloatLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(IntegerLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(NameLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StringLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StringRefLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(StructLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(DynamicArrayLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(ObjectLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(VectorLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(RotatorLiteral node)
        { throw new NotImplementedException(); }
        public bool VisitNode(NoneLiteral node)
        { throw new NotImplementedException(); }

        public bool VisitNode(ConditionalExpression node)
        { throw new NotImplementedException(); }
        public bool VisitNode(CastExpression node)
        { throw new NotImplementedException(); }

        public bool VisitNode(DefaultPropertiesBlock node)
        { throw new NotImplementedException(); }
        public bool VisitNode(Subobject node)
        { throw new NotImplementedException(); }
        #endregion
    }
}
