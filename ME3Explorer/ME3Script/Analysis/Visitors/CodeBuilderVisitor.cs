using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ME3ExplorerCore.Unreal.BinaryConverters;
using ME3Script.Language.Tree;
using ME3Script.Parsing;
using static ME3Script.Utilities.Keywords;
using ME3ExplorerCore.Helpers;
using ME3ExplorerCore.Unreal;

namespace ME3Script.Analysis.Visitors
{
    public class CodeBuilderVisitor : IASTVisitor
    {
        private readonly List<string> Lines;
        private int NestingLevel;
        private int ForcedAlignment;
        private bool ForceNoNewLines;
        private readonly Stack<int> ExpressionPrescedence;

        private const int NOPRESCEDENCE = int.MaxValue;

        public CodeBuilderVisitor()
        {
            Lines = new List<string>();
            NestingLevel = 0;
            ExpressionPrescedence = new Stack<int>();
            ExpressionPrescedence.Push(NOPRESCEDENCE);
        }

        public IList<string> GetCodeLines()
        {
            return Lines.AsReadOnly();
        }

        public string GetCodeString()
        {
            return string.Join("\n", Lines);
        }

        private void Write(string text)
        {
            if (ForceNoNewLines)
            {
                Append(text);
            }
            else
            {
                Lines.Add(new string(' ', ForcedAlignment + NestingLevel * 4) + text);
            }
        }

        private void Append(string text)
        {
            if (Lines.Count == 0)
                Lines.Add("");
            Lines[Lines.Count - 1] += text;
        }

        public bool VisitNode(Class node)
        {
            // class classname extends parentclass [within outerclass] [specifiers] ;
            Write($"{CLASS} {node.Name}");

            if (node.Parent != null && !node.Parent.Name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            {
                Append($" {EXTENDS} {node.Parent.Name}");
            }
            if (node.OuterClass != null && !node.OuterClass.Name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            {
                Append($" {WITHIN} {node.OuterClass.Name}");
            }

            NestingLevel++;

            if (node.Interfaces.Any())
            {
                Write($"implements({string.Join(", ", node.Interfaces.Select(i => i.Name))})");
            }

            UnrealFlags.EClassFlags flags = node.Flags;
            if (flags.Has(UnrealFlags.EClassFlags.Native))
            {
                Write("native");
            }
            if (flags.Has(UnrealFlags.EClassFlags.NativeOnly))
            {
                Write("nativeonly");
            }
            if (flags.Has(UnrealFlags.EClassFlags.NoExport))
            {
                Write("noexport");
            }
            if (flags.Has(UnrealFlags.EClassFlags.EditInlineNew))
            {
                Write("editinlinenew");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Placeable))
            {
                Write("placeable");
            }
            if (flags.Has(UnrealFlags.EClassFlags.HideDropDown))
            {
                Write("hidedropdown");
            }
            if (flags.Has(UnrealFlags.EClassFlags.NativeReplication))
            {
                Write("nativereplication");
            }
            if (flags.Has(UnrealFlags.EClassFlags.PerObjectConfig))
            {
                Write("perobjectconfig");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Localized))
            {
                Write("localized");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Abstract))
            {
                Write("abstract");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Deprecated))
            {
                Write("deprecated");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Transient))
            {
                Write("transient");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Config))
            {
                Write($"config({node.ConfigName})");
            }
            if (flags.Has(UnrealFlags.EClassFlags.SafeReplace))
            {
                Write("safereplace");
            }
            if (flags.Has(UnrealFlags.EClassFlags.Hidden))
            {
                Write("hidden");
            }
            if (flags.Has(UnrealFlags.EClassFlags.CollapseCategories))
            {
                Write("collapsecategories");
            }

            NestingLevel--;
            Append(";");

            // print the rest of the class, according to the standard "anatomy of an unrealscript" article.
            if (node.TypeDeclarations.Count > 0)
            {
                Write("");
                Write("// Types");
                foreach (VariableType type in node.TypeDeclarations)
                    type.AcceptVisitor(this);
            }

            if (node.VariableDeclarations.Count > 0)
            {
                Write("");
                Write("// Variables");
                foreach (VariableDeclaration decl in node.VariableDeclarations)
                    decl.AcceptVisitor(this);
            }

            if (node.Functions.Count > 0)
            {
                Write("");
                Write("// Functions");
                foreach (Function func in node.Functions)
                    func.AcceptVisitor(this);
            }

            if (node.States.Count > 0)
            {
                Write("");
                Write("// States");
                foreach (State state in node.States)
                    state.AcceptVisitor(this);
            }

            Write("");
            node.DefaultProperties?.AcceptVisitor(this);

            return true;
        }


        public bool VisitNode(VariableDeclaration node)
        {
            string type = "ERROR";
            if (node.Outer.Type == ASTNodeType.Class || node.Outer.Type == ASTNodeType.Struct)
            {
                type = VAR;
                if (!string.IsNullOrEmpty(node.Category))
                {
                    type += $"({node.Category})";
                }
            }
            else if (node.Outer.Type == ASTNodeType.Function)
                type = LOCAL;

            // var|local [specifiers] variabletype variablename[[staticarraysize]];
            Write($"{type} ");
            WritePropertyFlags(node.Flags);
            string staticarray = node.IsStaticArray ? $"[{node.Size}]" : "";
            AppendTypeName(node.VarType);
            Append($" {node.Name}{staticarray};");
            
            return true;
        }

        void AppendTypeName(VariableType node)
        {
            switch (node)
            {
                case DynamicArrayType _:
                case DelegateType _:
                case ClassType _:
                    node.AcceptVisitor(this);
                    return;
            }
            Append(node.Name);
        }

        public bool VisitNode(VariableType node)
        {
            Append(node.Name);
            return true;
        }

        public bool VisitNode(DynamicArrayType node)
        {
            Append($"{ARRAY}<");
            AppendTypeName(node.ElementType);
            Append(">");
            return true;
        }

        public bool VisitNode(DelegateType node)
        {
            Append($"{DELEGATE}<{node.DefaultFunction.Name}>");
            return true;
        }

        public bool VisitNode(ClassType node)
        {
            Append($"{CLASS}<{node.ClassLimiter.Name}>");
            return true;
        }

        public bool VisitNode(Struct node)
        {
            // struct [specifiers] structname [extends parentstruct] { \n contents \n };
            Write($"{STRUCT} ");
            
            var specs = new List<string>();
            ScriptStructFlags flags = node.Flags;
            if (flags.Has(ScriptStructFlags.Native))
            {
                specs.Add("native");
            }
            if (flags.Has(ScriptStructFlags.Export))
            {
                specs.Add("export");
            }
            if (flags.Has(ScriptStructFlags.Transient))
            {
                specs.Add("transient");
            }
            if (flags.Has(ScriptStructFlags.Immutable))
            {
                specs.Add("immutable");
            }
            else if (flags.Has(ScriptStructFlags.Atomic))
            {
                specs.Add("atomic");
            }
            if (flags.Has(ScriptStructFlags.ImmutableWhenCooked))
            {
                specs.Add("immutablewhencooked");
            }
            if (flags.Has(ScriptStructFlags.StrictConfig))
            {
                specs.Add("strictconfig");
            }

            if (specs.Any())
            {
                Append($"{string.Join(" ", specs)} ");
            }

            Append($"{node.Name} ");
            if (node.Parent != null)
                Append($"{EXTENDS} {node.Parent.Name} ");
            Write("{");
            NestingLevel++;

            foreach (VariableType typeDeclaration in node.TypeDeclarations)
            {
                typeDeclaration.AcceptVisitor(this);
            }

            foreach (VariableDeclaration member in node.VariableDeclarations)
                member.AcceptVisitor(this);

            if (node.DefaultProperties.Statements.Any())
            {
                Write("");
                node.DefaultProperties.AcceptVisitor(this);
            }

            NestingLevel--;
            Write("};");

            return true;
        }

        public bool VisitNode(Enumeration node)
        {
            // enum enumname { \n contents \n };
            Write($"{ENUM} {node.Name}");
            Write("{");
            NestingLevel++;

            foreach (VariableIdentifier value in node.Values)
                Write($"{value.Name},");

            NestingLevel--;
            Write("};");

            return true;
        }

        public bool VisitNode(Const node)
        {
            Write($"{CONST} {node.Name} = {node.Value};");

            return true;
        }

        public bool VisitNode(Function node)
        {
            // [specifiers] function [returntype] functionname ( [parameter declarations] ) body_or_semicolon
            Write("");

            var specs = new List<string>();
            FunctionFlags flags = node.Flags;

            if (flags.Has(FunctionFlags.Private))
            {
                specs.Add("private");
            }
            if (flags.Has(FunctionFlags.Protected))
            {
                specs.Add("protected");
            }
            if (flags.Has(FunctionFlags.Public))
            {
                specs.Add("public");
            }
            if (flags.Has(FunctionFlags.Static))
            {
                specs.Add("static");
            }
            if (flags.Has(FunctionFlags.Final))
            {
                specs.Add("final");
            }
            if (flags.Has(FunctionFlags.Delegate))
            {
                specs.Add("delegate");
            }
            if (flags.Has(FunctionFlags.Event))
            {
                specs.Add("event");
            }
            if (flags.Has(FunctionFlags.PreOperator))
            {
                specs.Add("preoperator");
            }
            else if (flags.Has(FunctionFlags.Operator))
            {
                specs.Add("operator");
            }
            if (flags.Has(FunctionFlags.Native) && node.NativeIndex > 0)
            {
                specs.Add($"native({node.NativeIndex})");
            }
            else if (flags.Has(FunctionFlags.Native))
            {
                specs.Add("native");
            }
            if (flags.Has(FunctionFlags.Iterator))
            {
                specs.Add("iterator");
            }
            if (flags.Has(FunctionFlags.Singular))
            {
                specs.Add("singular");
            }
            if (flags.Has(FunctionFlags.Latent))
            {
                specs.Add("latent");
            }
            if (flags.Has(FunctionFlags.Exec))
            {
                specs.Add("exec");
            }
            if (flags.Has(FunctionFlags.NetReliable))
            {
                specs.Add("reliable");
            }
            else if (flags.Has(FunctionFlags.Net))
            {
                specs.Add("unreliable");
            }
            if (flags.Has(FunctionFlags.NetServer))
            {
                specs.Add("server");
            }
            if (flags.Has(FunctionFlags.NetClient))
            {
                specs.Add("client");
            }
            else if (flags.Has(FunctionFlags.Simulated))
            {
                specs.Add("simulated");
            }

            if (specs.Count > 0)
            {
                Append($"{string.Join(" ", specs)} ");
            }

            Append($"{FUNCTION} ");
            if (node.ReturnType != null)
            {
                if (node.CoerceReturn)
                {
                    Append("coerce ");
                }
                AppendTypeName(node.ReturnType);
                Append(" ");
            }
            Append($"{node.Name}(");
            foreach (FunctionParameter p in node.Parameters)
            {
                p.AcceptVisitor(this);
                if (node.Parameters.IndexOf(p) != node.Parameters.Count - 1)
                    Append(", ");
            }
            Append(")");

            if (flags.Has(FunctionFlags.Defined) && node.Body.Statements != null)
            {
                Write("{");
                NestingLevel++;
                if (node.Locals.Any())
                {
                    foreach (VariableDeclaration v in node.Locals)
                        v.AcceptVisitor(this);
                    Write("");
                }
                node.Body.AcceptVisitor(this);
                NestingLevel--;
                Write("}");
            }
            else
            {
                Append(";");
                Write("");
            }

            return true;
        }

        public bool VisitNode(FunctionParameter node)
        {
            // [specifiers] parametertype parametername[[staticarraysize]]
            WritePropertyFlags(node.Flags);
            string staticarray = node.IsStaticArray ? "[" + node.Size + "]" : "";
            AppendTypeName(node.VarType);
            Append($" {node.Name}{staticarray}");
            if (node.DefaultParameter != null)
            {
                Append(" = ");
                node.DefaultParameter.AcceptVisitor(this);
            }

            return true;
        }

        public bool VisitNode(State node)
        {
            // [specifiers] state statename [extends parentstruct] { \n contents \n };
            Write("");

            var specs = new List<string>();
            StateFlags flags = node.Flags;

            if (flags.Has(StateFlags.Simulated))
            {
                specs.Add("simulated");
            }
            if (flags.Has(StateFlags.Auto))
            {
                specs.Add("auto");
            }

            if (specs.Count > 0)
            {
                Append($"{string.Join(" ", specs)} ");
            }

            Append($"{STATE}");
            if (flags.Has(StateFlags.Editable))
            {
                Append("()");
            }
            Append($" {node.Name} ");
            if (node.Parent != null)
                Append($"{EXTENDS} {node.Parent.Name} ");
            Write("{");
            NestingLevel++;

            if (node.Ignores.Count > 0)
                Write($"{IGNORES} {string.Join(", ", node.Ignores.Select(x => x.Name))};");

            foreach (Function func in node.Functions)
                func.AcceptVisitor(this);


            if (node.Body.Statements.Count != 0)
            {
                Write("");
                Write("// State code");
                node.Body.AcceptVisitor(this);
            }

            NestingLevel--;
            Write("};");

            return true;
        }

        public bool VisitNode(CodeBody node)
        {
            foreach (Statement s in node.Statements) //TODO: make this proper
            {
                if (s.AcceptVisitor(this) && !StringParserBase.SemiColonExceptions.Contains(s.Type))
                {
                    Append(";");
                }
            }

            return true; 
        }

        public bool VisitNode(DefaultPropertiesBlock node)
        {
            Write(node.Outer is Struct ? STRUCTDEFAULTPROPERTIES : DEFAULTPROPERTIES);
            Write("{");
            NestingLevel++;
            foreach (Statement s in node.Statements)
            {
                s.AcceptVisitor(this);
            }
            NestingLevel--;
            Write("}");

            return true;
        }

        public bool VisitNode(Subobject node)
        {
            Write($"Begin Object Class={node.Class} Name={node.Name}");
            NestingLevel++;
            foreach (Statement s in node.Statements)
            {
                s.AcceptVisitor(this);
            }
            NestingLevel--;
            Write("End Object");
            return true;
        }

        public bool VisitNode(DoUntilLoop node)
        { 
            // do { /n contents /n } until(condition);
            Write($"{DO} {{");
            NestingLevel++;

            node.Body.AcceptVisitor(this);
            NestingLevel--;

            Write($"}} {UNTIL} (");
            node.Condition.AcceptVisitor(this);
            Append(")");

            return true;
        }

        public bool VisitNode(ForLoop node)
        {
            // for (initstatement; loopcondition; updatestatement) { /n contents /n }
            Write($"{FOR} (");
            ForceNoNewLines = true;
            node.Init?.AcceptVisitor(this);
            Append("; ");
            node.Condition?.AcceptVisitor(this);
            Append("; ");
            node.Update?.AcceptVisitor(this);
            Append(")");
            ForceNoNewLines = false;
            Write("{");

            NestingLevel++;
            node.Body.AcceptVisitor(this);
            NestingLevel--;
            Write("}");

            return true;
        }

        public bool VisitNode(ForEachLoop node)
        {
            // foreach IteratorFunction(parameters) { /n contents /n }
            Write($"{FOREACH} ");
            node.IteratorCall.AcceptVisitor(this);
            Write("{");

            NestingLevel++;
            node.Body.AcceptVisitor(this);
            NestingLevel--;
            Write("}");

            return true;
        }

        public bool VisitNode(WhileLoop node)
        {
            // while (condition) { /n contents /n }
            Write($"{WHILE} (");
            node.Condition.AcceptVisitor(this);
            Append(")");
            Write("{");

            NestingLevel++;
            node.Body.AcceptVisitor(this);
            NestingLevel--;
            Write("}");

            return true;
        }

        public bool VisitNode(SwitchStatement node)
        {
            // switch (expression) { /n contents /n }
            Write($"{SWITCH} (");
            node.Expression.AcceptVisitor(this);
            Append(")");
            Write("{");

            NestingLevel += 2;  // double-indent, only case/default are single-indented
            node.Body.AcceptVisitor(this);
            NestingLevel -= 2;
            Write("}");
            return true;
        }

        public bool VisitNode(CaseStatement node)
        {
            // case expression:
            NestingLevel--; // de-indent this line only
            Write($"{CASE} ");
            node.Value.AcceptVisitor(this);
            Append(":");
            NestingLevel++;
            return true;
        }

        public bool VisitNode(DefaultCaseStatement node)
        {
            // default:
            NestingLevel--; // de-indent this line only
            Write($"{DEFAULT}:");
            NestingLevel++;
            return true;
        }

        public bool VisitNode(AssignStatement node)
        {
            // reference = expression;
            Write("");
            node.Target.AcceptVisitor(this);
            Append(" = ");
            node.Value.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(AssertStatement node)
        {
            // assert(condition)
            Write($"{ASSERT}(");
            node.Condition.AcceptVisitor(this);
            Append(")");

            return true;
        }

        public bool VisitNode(BreakStatement node)
        {
            // break;
            Write($"{BREAK}");
            return true;
        }

        public bool VisitNode(ContinueStatement node)
        {
            // continue;
            Write($"{CONTINUE}");
            return true;
        }

        public bool VisitNode(StopStatement node)
        {
            // stop;
            Write($"{STOP}");
            return true;
        }
        
        public bool VisitNode(ReturnStatement node)
        {
            // return expression;
            Write(RETURN);
            if (node.Value != null)
            {
                Append(" ");
                node.Value.AcceptVisitor(this);
            }

            return true;
        }

        public bool VisitNode(ReturnNothingStatement node)
        {
            //an implementation detail. no textual representation
            return false;
        }

        public bool VisitNode(ExpressionOnlyStatement node)
        {
            // expression;
            Write("");
            node.Value.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(IfStatement node)
        {
            // if (condition) { /n contents /n } [else...]
            VisitIf(node);

            return true;
        }

        private void VisitIf(IfStatement node, bool ifElse = false)
        {
            if (!ifElse)
                Write(""); // New line only if we're not chaining
            Append($"{IF} (");
            node.Condition.AcceptVisitor(this);
            Append(")");
            Write("{");

            NestingLevel++;
            node.Then.AcceptVisitor(this);
            NestingLevel--;
            Write("}");

            if (node.Else != null && node.Else.Statements.Any())
            {
                if (node.Else.Statements.Count == 1
                    && node.Else.Statements[0] is IfStatement)
                {
                    Write($"{ELSE} ");
                    VisitIf(node.Else.Statements[0] as IfStatement, true);
                }
                else
                {
                    Write(ELSE);
                    Write("{");
                    NestingLevel++;
                    node.Else.AcceptVisitor(this);
                    NestingLevel--;
                    Write("}");
                }
            }
        }

        public bool VisitNode(ConditionalExpression node)
        {
            const int ternaryPrecedence = NOPRESCEDENCE - 1;
            // condition ? then : else
            bool scopeNeeded = ternaryPrecedence > ExpressionPrescedence.Peek();
            ExpressionPrescedence.Push(ternaryPrecedence);

            if (scopeNeeded) Append("(");
            node.Condition.AcceptVisitor(this);
            Append(" ? ");
            node.TrueExpression.AcceptVisitor(this);
            Append(" : ");
            node.FalseExpression.AcceptVisitor(this);
            if (scopeNeeded) Append(")");

            ExpressionPrescedence.Pop();

            return true;
        }

        public bool VisitNode(InOpReference node)
        {
            // [(] expression operatorkeyword expression [)]
            bool scopeNeeded = node.Operator.Precedence > ExpressionPrescedence.Peek();
            ExpressionPrescedence.Push(node.Operator.Precedence);

            if (scopeNeeded) Append("(");
            node.LeftOperand.AcceptVisitor(this);
            Append($" {node.Operator.OperatorKeyword} ");
            node.RightOperand.AcceptVisitor(this);
            if (scopeNeeded) Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PreOpReference node)
        {
            ExpressionPrescedence.Push(1);
            // operatorkeywordExpression
            Append($"{node.Operator.OperatorKeyword}");
            node.Operand.AcceptVisitor(this);

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PostOpReference node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // ExpressionOperatorkeyword
            node.Operand.AcceptVisitor(this);
            Append(node.Operator.OperatorKeyword);

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(StructComparison node)
        {
            // [(] expression operatorkeyword expression [)]
            bool scopeNeeded = node.Precedence > ExpressionPrescedence.Peek();
            ExpressionPrescedence.Push(node.Precedence);

            if (scopeNeeded)
                Append("(");
            node.LeftOperand.AcceptVisitor(this);
            Append($" {(node.IsEqual ? "==" : " != ")} ");
            node.RightOperand.AcceptVisitor(this);
            if (scopeNeeded)
                Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(DelegateComparison node)
        {
            // [(] expression operatorkeyword expression [)]
            bool scopeNeeded = node.Precedence > ExpressionPrescedence.Peek();
            ExpressionPrescedence.Push(node.Precedence);

            if (scopeNeeded)
                Append("(");
            node.LeftOperand.AcceptVisitor(this);
            Append($" {(node.IsEqual ? "==" : " != ")} ");
            node.RightOperand.AcceptVisitor(this);
            if (scopeNeeded)
                Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(NewOperator node)
        {
            // new [( [outer [, name [, flags]]] )] class [( template )]
            ExpressionPrescedence.Push(NOPRESCEDENCE);

            Append("new ");
            if (node.OuterObject != null)
            {
                Append("(");
                node.OuterObject.AcceptVisitor(this);
                if (node.ObjectName != null)
                {
                    Append(", ");
                    node.ObjectName.AcceptVisitor(this);
                    if (node.Flags != null)
                    {
                        Append(", ");
                        node.Flags.AcceptVisitor(this);
                    }
                }
                Append(") ");
            }

            node.ObjectClass.AcceptVisitor(this);

            if (node.Template != null)
            {
                Append(" (");
                node.Template.AcceptVisitor(this);
                Append(")");
            }

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(FunctionCall node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // functionName( parameter1, parameter2.. )
            Append($"{node.Function.Name}(");
            foreach(Expression expr in node.Arguments)
            {
                expr.AcceptVisitor(this);
                if (node.Arguments.IndexOf(expr) != node.Arguments.Count - 1)
                    Append(", ");
            }
            Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(DelegateCall node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // functionName( parameter1, parameter2.. )
            Append($"{node.DelegateReference.Name}(");
            foreach(Expression expr in node.Arguments)
            {
                expr.AcceptVisitor(this);
                if (node.Arguments.IndexOf(expr) != node.Arguments.Count - 1)
                    Append(", ");
            }
            Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(CastExpression node)
        {
            // type(expr)

            AppendTypeName(node.CastType);
            Append("(");
            node.CastTarget.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(ArraySymbolRef node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // symbolname[expression]
            node.Array.AcceptVisitor(this);
            Append("[");
            node.Index.AcceptVisitor(this);
            Append("]");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(CompositeSymbolRef node)
        {
            // outersymbol.innersymbol
            node.OuterSymbol.AcceptVisitor(this);
            if (node.IsClassContext && !(node.InnerSymbol is DefaultReference))
            {
                Append($".{STATIC}");
            }
            Append(".");
            node.InnerSymbol.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(SymbolReference node)
        {
            // symbolname
            Append(node.Name);
            return true;
        }

        public bool VisitNode(DefaultReference node)
        {
            // symbolname
            Append($"{DEFAULT}.{node.Name}");
            return true;
        }

        public bool VisitNode(DynArrayLength node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{LENGTH}");
            return true;
        }

        public bool VisitNode(DynArrayAdd node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{ADD}(");
            node.CountArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayAddItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{ADDITEM}(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayInsert node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{INSERT}(");
            node.CountArg.AcceptVisitor(this);
            Append(", ");
            node.IndexArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayInsertItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{INSERTITEM}(");
            node.IndexArg.AcceptVisitor(this);
            Append(", ");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayRemove node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{REMOVE}(");
            node.CountArg.AcceptVisitor(this);
            Append(", ");
            node.IndexArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayRemoveItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{REMOVEITEM}(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayFind node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{FIND}(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayFindStructMember node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{FIND}(");
            node.MemberNameArg.AcceptVisitor(this);
            Append(", ");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArraySort node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append($".{SORT}(");
            node.CompareFuncArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayIterator node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append("(");
            node.ValueArg.AcceptVisitor(this);
            if (node.IndexArg != null)
            {
                Append(", ");
                node.IndexArg.AcceptVisitor(this);
            }
            Append(")");
            return true;
        }

        public bool VisitNode(BooleanLiteral node)
        {
            // true|false
            Append(node.Value.ToString().ToLower());
            return true;
        }

        public bool VisitNode(FloatLiteral node)
        {
            Append(FormatFloat(node.Value));
            return true;
        }

        private static string FormatFloat(float single)
        {
            //G9 ensures a fully accurate version of the float (no rounding) is written.
            //more details here: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-round-trip-r-format-specifier 
            string floatString = $"{single:G9}".Replace("E+", "e");

            if (floatString.Contains("E-"))
            {
                //unrealscript does not support negative exponents in literals, so we have to format it manually
                //for example, 1.401298E-45 would be formatted as 0.00000000000000000000000000000000000000000000140129846
                //This code assumes there is exactly 1 digit before the decimal point, which will always be the case when formatted as scientific notation with the G specifier
                int ePos = floatString.IndexOf("E-");
                int exponent = int.Parse(floatString.Substring(ePos + 2));
                string digits = floatString.Substring(0, ePos).Replace(".", "");
                floatString = $"0.{new string('0', exponent - 1)}{digits}";
            }
            else if (!floatString.Contains("."))
            {
                //need a decimal place in the float so that it does not get parsed as an int
                floatString += ".0";
            }

            return floatString;
        }

        public bool VisitNode(IntegerLiteral node)
        {
            // integervalue
            Append($"{node.Value}");
            return true;
        }

        public bool VisitNode(NameLiteral node)
        {
            //commented version is unrealscript compliant, but harder to parse
            //Append(node.Outer is StructLiteral ? "\"{EncodeName(node.Value)}\"" : "'{EncodeName(node.Value)}'");
            Append($"'{EncodeName(node.Value)}'");
            return true;
        }

        public bool VisitNode(ObjectLiteral node)
        {
            AppendTypeName(node.Class);
            node.Name.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(NoneLiteral node)
        {
            Append(NONE);
            return true;
        }

        public bool VisitNode(VectorLiteral node)
        {
            Append($"{VECT}({FormatFloat(node.X)}, {FormatFloat(node.Y)}, {FormatFloat(node.Z)})");
            return true;
        }

        public bool VisitNode(RotatorLiteral node)
        {
            Append($"{ROT}(0x{node.Pitch:X8}, 0x{node.Yaw:X8}, 0x{node.Roll:X8})");
            return true;
        }

        public bool VisitNode(StringLiteral node)
        {
            // "string"
            Append($"\"{EncodeString(node.Value)}\"");
            return true;
        }

        public bool VisitNode(StringRefLiteral node)
        {
            Append($"${node.Value}");
            return true;
        }
        public bool VisitNode(StructLiteral node)
        {
            bool multiLine = !ForceNoNewLines && (node.Statements.Count > 5 || node.Statements.Any(stmnt => (stmnt as AssignStatement)?.Value is StructLiteral || (stmnt as AssignStatement)?.Value is DynamicArrayLiteral));

            bool oldForceNoNewLines = ForceNoNewLines;
            int oldForcedAlignment = ForcedAlignment;
            if (multiLine)
            {
                Append("{(");
                ForcedAlignment = Lines.Last().Length - NestingLevel * 4;
            }
            else
            {
                ForceNoNewLines = true;
                Append("(");
            }
            for (int i = 0; i < node.Statements.Count; i++)
            {
                if (i > 0)
                {
                    Append(", ");
                }
                node.Statements[i].AcceptVisitor(this);
            }

            if (multiLine)
            {
                ForcedAlignment -= 2;
                Write(")}");
                ForcedAlignment = oldForcedAlignment;
            }
            else
            {
                Append(")");
                ForceNoNewLines = oldForceNoNewLines;
            }
            return true;
        }

        public bool VisitNode(DynamicArrayLiteral node)
        {
            bool multiLine = !ForceNoNewLines && (node.Values.Any(expr => expr is StructLiteral || expr is DynamicArrayLiteral));

            bool oldForceNoNewLines = ForceNoNewLines;
            int oldForcedAlignment = ForcedAlignment;
            Append("(");
            if (multiLine)
            {
                ForcedAlignment = Lines.Last().Length - NestingLevel * 4;
            }
            else
            {
                ForceNoNewLines = true;
            }
            for (int i = 0; i < node.Values.Count; i++)
            {
                if (i > 0)
                {
                    Append(", ");
                    if (multiLine)
                    {
                        Write("");
                    }
                }
                node.Values[i].AcceptVisitor(this);
            }
            if (multiLine)
            {
                ForcedAlignment -= 1;
                Write(")");
                ForcedAlignment = oldForcedAlignment;
            }
            else
            {
                Append(")");
                ForceNoNewLines = oldForceNoNewLines;
            }
            return true;
        }

        public bool VisitNode(StateLabel node)
        {
            // Label
            var temp = NestingLevel;
            NestingLevel = 0;
            Write(node.Name + ":");
            NestingLevel = temp;

            return true;
        }

        private void WritePropertyFlags(UnrealFlags.EPropertyFlags flags)
        {
            var specs = new List<string>();

            if (flags.Has(UnrealFlags.EPropertyFlags.OptionalParm))
            {
                specs.Add("optional");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Const))
            {
                specs.Add("const");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.GlobalConfig))
            {
                specs.Add("globalconfig");
            }
            else if (flags.Has(UnrealFlags.EPropertyFlags.Config))
            {
                specs.Add("config");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Localized))
            {
                specs.Add("localized");
            }

            //TODO: private, protected, and public are in ObjectFlags, not PropertyFlags 
            if (flags.Has(UnrealFlags.EPropertyFlags.ProtectedWrite))
            {
                specs.Add("protectedwrite");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.PrivateWrite))
            {
                specs.Add("privatewrite");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditConst))
            {
                specs.Add("editconst");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditHide))
            {
                specs.Add("edithide");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditTextBox))
            {
                specs.Add("edittextbox");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Input))
            {
                specs.Add("input");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Transient))
            {
                specs.Add("transient");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Native))
            {
                specs.Add("native");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.NoExport))
            {
                specs.Add("noexport");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.DuplicateTransient))
            {
                specs.Add("duplicatetransient");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.NoImport))
            {
                specs.Add("noimport");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.OutParm))
            {
                specs.Add("out");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.ExportObject))
            {
                specs.Add("export");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditInlineUse))
            {
                specs.Add("editinlineuse");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.NoClear))
            {
                specs.Add("noclear");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditFixedSize))
            {
                specs.Add("editfixedsize");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.RepNotify))
            {
                specs.Add("repnotify");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.RepRetry))
            {
                specs.Add("repretry");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Interp))
            {
                specs.Add("interp");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.NonTransactional))
            {
                specs.Add("nontransactional");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Deprecated))
            {
                specs.Add("deprecated");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.SkipParm))
            {
                specs.Add("skip");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.CoerceParm))
            {
                specs.Add("coerce");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.AlwaysInit))
            {
                specs.Add("alwaysinit");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.DataBinding))
            {
                specs.Add("databinding");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.EditorOnly))
            {
                specs.Add("editoronly");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.NotForConsole))
            {
                specs.Add("notforconsole");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.Archetype))
            {
                specs.Add("archetype");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.SerializeText))
            {
                specs.Add("serializetext");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.CrossLevelActive))
            {
                specs.Add("crosslevelactive");
            }

            if (flags.Has(UnrealFlags.EPropertyFlags.CrossLevelPassive))
            {
                specs.Add("crosslevelpassive");
            }

            if (specs.Any())
            {
                Append($"{string.Join(" ", specs)} ");
            }
        }

        public static string EncodeString(string original)
        {
            var sb = new StringBuilder();
            foreach (char c in original)
            {
                switch (c)
                {
                    case '\r':
                        sb.Append("\\r");
                        break;
                    case '\t':
                        sb.Append("\\t");
                        break;
                    case '\"':
                        sb.Append("\\\"");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    case '\n':
                        sb.Append("\\n");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        public static string EncodeName(string original)
        {
            var sb = new StringBuilder();
            foreach (char c in original)
            {
                switch (c)
                {
                    case '\'':
                        sb.Append("\\'");
                        break;
                    case '\\':
                        sb.Append("\\\\");
                        break;
                    default:
                        sb.Append(c);
                        break;
                }
            }

            return sb.ToString();
        }

        #region Unused

        public bool VisitNode(VariableIdentifier node)
        { throw new NotImplementedException(); }

        #endregion
    }
}
