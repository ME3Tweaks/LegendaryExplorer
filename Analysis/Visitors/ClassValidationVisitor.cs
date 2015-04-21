using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Analysis.Visitors
{
    public class ClassValidationVisitor : IASTVisitor
    {
        private SymbolTable Symbols;
        private MessageLog Log;
        private bool Success;

        public ClassValidationVisitor(MessageLog log, SymbolTable symbols)
        {
            Log = log;
            Symbols = symbols;
            Success = true;
        }

        private bool Error(String msg, SourcePosition start = null, SourcePosition end = null)
        {
            Log.LogError(msg, start, end);
            Success = false;
            return false;
        }

        private String GetOuterScope(ASTNode node)
        {
            return ((node.Outer as Class).OuterClass as Class).GetInheritanceString();
        }

        public bool VisitNode(Class node)
        {
            // TODO: allow duplicate names as long as its in different packages!
            if (Symbols.SymbolExists(node.Name, ""))
                return Error("A class named '" + node.Name + "' already exists!", node.StartPos, node.EndPos);

            Symbols.AddSymbol(node.Name, node);

            ASTNode parent;
            if (!Symbols.TryGetSymbol(node.Parent.Name, out parent, ""))
                Error("No parent class named '" + node.Parent.Name + "' found!", node.Parent.StartPos, node.Parent.EndPos);
            if (parent != null)
            {
                if (parent.Type != ASTNodeType.Class)
                    Error("Parent named '" + node.Parent.Name + "' is not a class!", node.Parent.StartPos, node.Parent.EndPos);
                else if ((parent as Class).SameOrSubClass(node.Name)) // TODO: not needed due to no forward declarations?
                    Error("Extending from '" + node.Parent.Name + "' causes circular extension!", node.Parent.StartPos, node.Parent.EndPos);
                else
                    node.Parent = parent as Class;
            }

            ASTNode outer;
            if (node.OuterClass != null)
            {
                if (!Symbols.TryGetSymbol(node.OuterClass.Name, out outer, ""))
                    Error("No outer class named '" + node.OuterClass.Name + "' found!", node.OuterClass.StartPos, node.OuterClass.EndPos);
                else if (outer.Type != ASTNodeType.Class)
                    Error("Outer named '" + node.OuterClass.Name + "' is not a class!", node.OuterClass.StartPos, node.OuterClass.EndPos);
                else if (node.Parent.Name == "Actor")
                    Error("Classes extending 'Actor' can not be inner classes!", node.OuterClass.StartPos, node.OuterClass.EndPos);
                else if (!(outer as Class).SameOrSubClass((node.Parent as Class).OuterClass.Name))
                    Error("Outer class must be a sub-class of the parents outer class!", node.OuterClass.StartPos, node.OuterClass.EndPos);
            }
            else
            {
                outer = (node.Parent as Class).OuterClass;
            }
            node.OuterClass = outer as Class;

            // TODO(?) validate class specifiers more than the initial parsing?

            Symbols.GoDirectlyToStack((node.Parent as Class).GetInheritanceString());
            Symbols.PushScope(node.Name);

            foreach (VariableType type in node.TypeDeclarations)
            {
                type.Outer = node;
                Success = Success && type.AcceptVisitor(this);
            }
            foreach (VariableDeclaration decl in node.VariableDeclarations)
            {
                decl.Outer = node;
                Success = Success && decl.AcceptVisitor(this);
            }
            foreach (VariableDeclaration decl in node.VariableDeclarations)
            {
                decl.Outer = node;
                Success = Success && decl.AcceptVisitor(this);
            }
            foreach (OperatorDeclaration op in node.Operators)
            {
                op.Outer = node;
                Success = Success && op.AcceptVisitor(this);
            }
            foreach (Function func in node.Functions)
            {
                func.Outer = node;
                Success = Success && func.AcceptVisitor(this);
            }
            foreach (State state in node.States)
            {
                state.Outer = node;
                Success = Success && state.AcceptVisitor(this);
            }

            Symbols.PopScope();
            Symbols.RevertToObjectStack();
            return Success;
        }


        public bool VisitNode(VariableDeclaration node)
        {
            ASTNode nodeType;
            if (node.Type == ASTNodeType.Struct || node.Type == ASTNodeType.Enumeration)
            {
                // Check type, if its a struct or enum, visit that first.
                Success = Success && node.VarType.AcceptVisitor(this);
                // Add the type to the list of types in the class.
                (node.Outer as Class).TypeDeclarations.Add(node.VarType);
                nodeType = node.VarType;
            }
            else if (!Symbols.TryGetSymbol(node.VarType.Name, out nodeType, (node.Outer.Outer as Class).Name))
            {
                return Error("No type named '" + node.VarType.Name + "' exists in this scope!", node.VarType.StartPos, node.VarType.EndPos);
            }
            else if (!nodeType.GetType().IsAssignableFrom(typeof(VariableType)))
            {
                return Error("Invalid variable type, must be a class/struct/enum/primitive.", node.VarType.StartPos, node.VarType.EndPos);
            }

            int index = (node.Outer as Class).VariableDeclarations.IndexOf(node);
            foreach (VariableIdentifier ident in node.Variables)
            {
                if (Symbols.SymbolExistsInCurrentScope(ident.Name))
                    return Error("A member named '" + ident.Name + "' already exists in this class!", ident.StartPos, ident.EndPos);
                Variable variable = new Variable(node.Specifiers, ident, nodeType as VariableType, ident.StartPos, ident.EndPos);
                Symbols.AddSymbol(variable.Name, variable);
                (node.Outer as Class).VariableDeclarations.Insert(index, variable);
            }
            (node.Outer as Class).VariableDeclarations.Remove(node);

            return Success;
        }

        public bool VisitNode(VariableType node)
        {
            // This should never be called.
            throw new NotImplementedException();
        }

        public bool VisitNode(Struct node)
        {
            if (Symbols.SymbolExistsInCurrentScope(node.Name))
                return Error("A member named '" + node.Name + "' already exists in this class!", node.StartPos, node.EndPos);

            Symbols.AddSymbol(node.Name, node);
            // TODO: add in package / global namespace.
            // If a symbol with that name exists, overwrite it with this symbol from now on.
            // damn this language...

            if (node.Parent != null)
            {
                ASTNode parent;
                if (!Symbols.TryGetSymbol(node.Parent.Name, out parent, GetOuterScope(node)))
                    Error("No parent struct named '" + node.Parent.Name + "' found!", node.Parent.StartPos, node.Parent.EndPos);
                if (parent != null)
                {
                    if (parent.Type != ASTNodeType.Struct)
                        Error("Parent named '" + node.Parent.Name + "' is not a struct!", node.Parent.StartPos, node.Parent.EndPos);
                    else if ((parent as Struct).SameOrSubStruct(node.Name)) // TODO: not needed due to no forward declarations?
                        Error("Extending from '" + node.Parent.Name + "' causes circular extension!", node.Parent.StartPos, node.Parent.EndPos);
                    else
                        node.Parent = parent as Struct;
                }
            }

            Symbols.PushScope(node.Name);

            // TODO: can all types of variable declarations be supported in a struct?
            // what does the parser let through?
            foreach (VariableDeclaration decl in node.Members)
            {
                decl.Outer = node;
                Success = Success && decl.AcceptVisitor(this);
            }

            Symbols.PopScope();
            return Success;
        }

        public bool VisitNode(Enumeration node)
        {
            if (Symbols.SymbolExistsInCurrentScope(node.Name))
                return Error("A member named '" + node.Name + "' already exists in this class!", node.StartPos, node.EndPos);

            Symbols.AddSymbol(node.Name, node);
            // TODO: add in package / global namespace.
            // If a symbol with that name exists, overwrite it with this symbol from now on.
            // damn this language...
            Symbols.PushScope(node.Name);

            foreach (VariableIdentifier enumVal in node.Values)
            {
                enumVal.Outer = node;
                if (enumVal.Type != ASTNodeType.VariableIdentifier)
                    Error("An enumeration member must be a simple(name only) variable.", enumVal.StartPos, enumVal.EndPos);
                Symbols.AddSymbol(enumVal.Name, enumVal);
            }

            Symbols.PopScope();

            // Add enum values at the class scope so they can be used without being explicitly qualified.
            foreach (VariableIdentifier enumVal in node.Values)
                Symbols.AddSymbol(enumVal.Name, enumVal);

            return Success;
        }

        public bool VisitNode(Function node)
        {
            if (Symbols.SymbolExistsInCurrentScope(node.Name))
                return Error("The name '" + node.Name + "' is already in use in this class!", node.StartPos, node.EndPos);

            Symbols.AddSymbol(node.Name, node);
            ASTNode returnType = null;
            if (node.ReturnType != null)
            {
                if (!Symbols.TryGetSymbol(node.ReturnType.Name, out returnType, (node.Outer.Outer as Class).Name))
                {
                    return Error("No type named '" + node.ReturnType.Name + "' exists in this scope!", node.ReturnType.StartPos, node.ReturnType.EndPos);
                }
                else if (!returnType.GetType().IsAssignableFrom(typeof(VariableType)))
                {
                    return Error("Invalid return type, must be a class/struct/enum/primitive.", node.ReturnType.StartPos, node.ReturnType.EndPos);
                }
            }

            Symbols.PushScope(node.Name);
            foreach (FunctionParameter param in node.Parameters)
            {
                param.Outer = node;
                Success = Success && param.AcceptVisitor(this);
            }
            Symbols.PopScope();

            ASTNode func;
            if (Symbols.TryGetSymbolInScopeStack(node.Name, out func, (node.Outer.Outer as Class).GetInheritanceString())
                && func.Type == ASTNodeType.Function)
            {   // If there is a function with this name that we should override, validate the new functions declaration
                Function original = func as Function;
                if (original.Specifiers.Contains(new Specifier("final", null, null)))
                    return Error("Function name overrides a function in a parent class, but the parent function is marked as final!", node.StartPos, node.EndPos);
                if (node.ReturnType != original.ReturnType)
                    return Error("Function name overrides a function in a parent class, but the functions do not have the same return types!", node.StartPos, node.EndPos);
                if (node.Parameters.Count != original.Parameters.Count)
                    return Error("Function name overrides a function in a parent class, but the functions do not have the same amount of parameters!", node.StartPos, node.EndPos);
                for (int n = 0; n < node.Parameters.Count; n++)
                {
                    if (node.Parameters[n].Type != original.Parameters[n].Type)
                        return Error("Function name overrides a function in a parent class, but the functions do not ahve the same parameter types!", node.StartPos, node.EndPos);
                }
            }

            return Success;
        }

        public bool VisitNode(FunctionParameter node)
        {
            ASTNode paramType;
            if (!Symbols.TryGetSymbol(node.VarType.Name, out paramType, (node.Outer.Outer.Outer as Class).Name))
            {
                return Error("No type named '" + node.VarType.Name + "' exists in this scope!", node.VarType.StartPos, node.VarType.EndPos);
            }
            else if (!paramType.GetType().IsAssignableFrom(typeof(VariableType)))
            {
                return Error("Invalid parameter type, must be a class/struct/enum/primitive.", node.VarType.StartPos, node.VarType.EndPos);
            }
            node.VarType = paramType as VariableType;

            if (Symbols.SymbolExistsInCurrentScope(node.Name))
                return Error("A parameter named '" + node.Name + "' already exists in this function!", 
                    node.Variables.First().StartPos, node.Variables.First().EndPos);

            Symbols.AddSymbol(node.Variables.First().Name, node);
            return Success;
        }

        public bool VisitNode(State node)
        {
            throw new NotImplementedException();
        }

        public bool VisitNode(OperatorDeclaration node)
        {
            throw new NotImplementedException();
        }
    }
}
