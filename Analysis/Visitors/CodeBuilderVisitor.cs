using ME3Script.Analysis.Symbols;
using ME3Script.Compiling.Errors;
using ME3Script.Language.Tree;
using ME3Script.Language.Util;
using ME3Script.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Analysis.Visitors
{
    public class CodeBuilderVisitor : IASTVisitor
    {
        private List<String> Lines;
        private int NestingLevel;
        private Stack<int> ExpressionPrescedence;

        private static int NOPRESCEDENCE = int.MaxValue;

        public CodeBuilderVisitor()
        {
            Lines = new List<String>();
            NestingLevel = 0;
            ExpressionPrescedence = new Stack<int>();
            ExpressionPrescedence.Push(NOPRESCEDENCE);
        }

        public IList<String> GetCodeLines()
        {
            return Lines.AsReadOnly();
        }

        public String GetCodeString()
        {
            return String.Join("\n", Lines.ToArray());
        }

        private void Write(String text, params object[] args)
        {
            Lines.Add(new String('\t', NestingLevel) + (args.Length > 0 ? String.Format(text, args) : text));
        }

        private void Append(String text, params object[] args)
        {
            if (Lines.Count == 0)
                Lines.Add("");
            Lines[Lines.Count - 1] += args.Length > 0 ? String.Format(text, args) : text;
        }

        public bool VisitNode(Class node)
        {
            // class classname extends parentclass [within outerclass] [specifiers] ;
            Write("class {0} extends {1} ", node.Name, node.Parent.Name);
            if (node.OuterClass.Name != node.Parent.Name)
                Append("within {0} ", node.OuterClass.Name);
            if (node.Specifiers.Count > 0)
                Append("{0}", String.Join(" ", node.Specifiers.Select(x => x.Value)));
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
                foreach (VariableDeclaration decl in node.VariableDeclarations.ToList())
                    decl.AcceptVisitor(this);
            }

            if (node.Operators.Count > 0)
            {
                Write("");
                Write("// Operators");
                foreach (OperatorDeclaration op in node.Operators)
                    op.AcceptVisitor(this);
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

            return true;
        }


        public bool VisitNode(VariableDeclaration node)
        {
            return true;
        }

        public bool VisitNode(Variable node)
        {
            String type = "ERROR";
            if (node.Outer.Type == ASTNodeType.Class || node.Outer.Type == ASTNodeType.Struct)
                type = "var";
            else if (node.Outer.Type == ASTNodeType.Function)
                type = "local";

            // var|local [specifiers] variabletype variablename[[staticarraysize]];
            Write("{0} ", type);
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            String staticarray = node.IsStaticArray ? "[" + node.Size + "]" : "";
            Append("{0} {1};", node.VarType.Name, node.Name + staticarray);
            
            return true;
        }

        public bool VisitNode(VariableType node)
        {
            // This should never be called.
            throw new NotImplementedException();
        }

        public bool VisitNode(Struct node)
        {
            // struct [specifiers] structname [extends parentstruct] { \n contents \n };
            Write("struct ");
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            Append("{0} ", node.Name);
            if (node.Parent != null)
                Append("extends {0} ", node.Parent.Name);
            Append("{0}", "{");
            NestingLevel++;

            foreach (Variable member in node.Members)
                member.AcceptVisitor(this);

            NestingLevel--;
            Write("{0};", "}");

            return true;
        }

        public bool VisitNode(Enumeration node)
        {
            // enum enumname { \n contents \n };
            Write("enum {0} {1}", node.Name, "{");
            NestingLevel++;

            foreach (VariableIdentifier value in node.Values)
                Write("{0},", value.Name);

            NestingLevel--;
            Write("{0};", "}");

            return true;
        }

        public bool VisitNode(Function node)
        {
            // [specifiers] function [returntype] functionname ( [parameter declarations] ) body_or_semicolon
            Write("");
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            Append("function {0}{1}( ", (node.ReturnType != null ? node.ReturnType.Name + " " : ""), node.Name);
            foreach (FunctionParameter p in node.Parameters)
            {
                p.AcceptVisitor(this);
                if (node.Parameters.IndexOf(p) != node.Parameters.Count - 1)
                    Append(", ");
            }
            Append(" )");

            if (node.Body.Statements != null)
            {
                Write("{0}", "{");
                NestingLevel++;
                foreach (Variable v in node.Locals)
                    v.AcceptVisitor(this);
                node.Body.AcceptVisitor(this);
                NestingLevel--;
                Write("{0}", "}");
            }
            else
                Append(";");

            return true;
        }

        public bool VisitNode(FunctionParameter node)
        {
            // [specifiers] parametertype parametername[[staticarraysize]]
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            String staticarray = node.Variables[0].Size != -1 ? "[" + node.Variables[0].Size + "]" : "";
            Append("{0} {1}{2}", node.VarType.Name, node.Name, staticarray);

            return true;
        }

        public bool VisitNode(State node)
        {
            // [specifiers] state statename [extends parentstruct] { \n contents \n };
            Write("");
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            Append("state {0} ", node.Name);
            if (node.Parent != null)
                Append("extends {0} ", node.Parent.Name);
            Append("{0}", "{");
            NestingLevel++;

            if (node.Ignores.Count > 0)
                Write("ignores {0};", String.Join(", ", node.Ignores.Select(x => x.Name)));

            foreach (Function func in node.Functions)
                func.AcceptVisitor(this);

            // print body

            NestingLevel--;
            Write("{0};", "}");

            return true;
        }

        public bool VisitNode(OperatorDeclaration node)
        {
            // [specifiers] function [returntype] functionname ( [parameter declarations] ) body_or_semicolon
            Write("");
            if (node.Specifiers.Count > 0)
                Append("{0} ", String.Join(" ", node.Specifiers.Select(x => x.Value)));
            
            if (node.Type == ASTNodeType.InfixOperator)
            {
                var inOp = node as InOpDeclaration;
                Append("operator({0}) {1}{2}( ", inOp.Precedence,
                    (node.ReturnType != null ? node.ReturnType.Name + " " : ""), node.OperatorKeyword);
                inOp.LeftOperand.AcceptVisitor(this);
                Append(", ");
                inOp.RightOperand.AcceptVisitor(this);
            }
            else if (node.Type == ASTNodeType.PrefixOperator)
            {
                var preOp = node as PreOpDeclaration;
                Append("preoperator {0}{1}( ",
                    (node.ReturnType != null ? node.ReturnType.Name + " " : ""), node.OperatorKeyword);
                preOp.Operand.AcceptVisitor(this);
            }
            else if (node.Type == ASTNodeType.PostfixOperator)
            {
                var postOp = node as PostOpDeclaration;
                Append("postoperator {0}{1}( ",
                    (node.ReturnType != null ? node.ReturnType.Name + " " : ""), node.OperatorKeyword);
                postOp.Operand.AcceptVisitor(this);
            }

            Append(" )");

            if (node.Body.Statements != null)
            {
                Write("{0}", "{");
                NestingLevel++;
                foreach (Variable v in node.Locals)
                    v.AcceptVisitor(this);
                node.Body.AcceptVisitor(this);
                NestingLevel--;
                Write("{0}", "}");
            }
            else
                Append(";");

            return true;
        }

        public bool VisitNode(CodeBody node)
        {
            foreach (Statement s in node.Statements)
            {
                s.AcceptVisitor(this);
                if (!GlobalLists.SemicolonExceptions.Contains(s.Type))
                    Append(";");
            }

            return true; 
        }

        public bool VisitNode(DoUntilLoop node)
        { 
            // do { /n contents /n } until(condition);
            Write("do {0}", "{");
            NestingLevel++;

            node.Body.AcceptVisitor(this);
            NestingLevel--;

            Write("{0} until (", "}");
            node.Condition.AcceptVisitor(this);
            Append(");");

            return true;
        }

        public bool VisitNode(ForLoop node)
        {
            // for (initstatement; loopcondition; updatestatement) { /n contents /n }
            Write("for (");
            node.Init.AcceptVisitor(this);
            Append("; ");
            node.Condition.AcceptVisitor(this);
            Append("; ");
            node.Update.AcceptVisitor(this);
            Append(") {0}", "{");

            NestingLevel++;
            node.Body.AcceptVisitor(this);
            NestingLevel--;
            Write("{0}", "}");

            return true;
        }

        public bool VisitNode(WhileLoop node)
        {
            // while (condition) { /n contents /n }
            Write("while (");
            node.Condition.AcceptVisitor(this);
            Append(") {0}", "{");

            NestingLevel++;
            node.Body.AcceptVisitor(this);
            NestingLevel--;
            Write("{0}", "}");

            return true;
        }

        public bool VisitNode(SwitchStatement node)
        {
            // switch (expression) { /n contents /n }
            Write("switch (");
            node.Expression.AcceptVisitor(this);
            Append(") {0}", "{");

            NestingLevel += 2;  // double-indent, only case/default are single-indented
            node.Body.AcceptVisitor(this);
            NestingLevel -= 2;
            Write("{0}", "}");
            return true;
        }

        public bool VisitNode(CaseStatement node)
        {
            // case expression:
            NestingLevel--; // de-indent this line only
            Write("case ");
            node.Value.AcceptVisitor(this);
            Append(":");
            NestingLevel++;
            return true;
        }

        public bool VisitNode(DefaultStatement node)
        {
            // default:
            NestingLevel--; // de-indent this line only
            Write("default:");
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

        public bool VisitNode(BreakStatement node)
        {
            // break;
            Write("break");
            return true;
        }

        public bool VisitNode(ContinueStatement node)
        {
            // continue;
            Write("continue");
            return true;
        }

        public bool VisitNode(StopStatement node)
        {
            // stop;
            Write("stop");
            return true;
        }
        
        public bool VisitNode(ReturnStatement node)
        {
            // return expression;
            Write("return ");
            node.Value.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(ExpressionOnlyStatement node)
        {
            // expression;
            node.Value.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(IfStatement node)
        {
            // if (condition) { /n contents /n } [else...]
            Write("if (");
            node.Condition.AcceptVisitor(this);
            Append(") {0}", "{");

            NestingLevel++;
            node.Then.AcceptVisitor(this);
            NestingLevel--;
            Write("{0}", "}");

            if (node.Else != null)
            {
                Append(" else {0}", "{");
                NestingLevel++;
                node.Else.AcceptVisitor(this);
                NestingLevel--;
                Write("{0}", "}");
            }

            return true;
        }

        public bool VisitNode(ConditionalExpression node)
        {
            // condition ? then : else
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            node.Condition.AcceptVisitor(this);
            Append(" ? ");
            node.TrueExpression.AcceptVisitor(this);
            Append(" : ");
            node.FalseExpression.AcceptVisitor(this);
            ExpressionPrescedence.Pop();

            return true;
        }

        public bool VisitNode(InOpReference node)
        {
            // [(] expression operatorkeyword expression [)]
            bool scopeNeeded = node.Operator.Precedence > ExpressionPrescedence.Peek();
            ExpressionPrescedence.Push(node.Operator.Precedence);

            if (scopeNeeded)
                Append("(");
            node.LeftOperand.AcceptVisitor(this);
            Append(" {0} ", node.Operator.OperatorKeyword);
            node.RightOperand.AcceptVisitor(this);
            if (scopeNeeded)
                Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PreOpReference node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // operatorkeywordExpression
            Append("{0}", node.Operator.OperatorKeyword);
            node.Operand.AcceptVisitor(this);

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PostOpReference node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // ExpressionOperatorkeyword
            node.Operand.AcceptVisitor(this);
            Append("{0}", node.Operator.OperatorKeyword);

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(FunctionCall node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // functionName( parameter1, parameter2.. )
            Append("{0}(", node.Function.Name);
            foreach(Expression expr in node.Parameters)
            {
                expr.AcceptVisitor(this);
                if (node.Parameters.IndexOf(expr) != node.Parameters.Count - 1)
                    Append(", ");
            }
            Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(CastExpression node)
        {
            // type(expr)
            node.CastType.AcceptVisitor(this);
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
            Append(".");
            node.InnerSymbol.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(SymbolReference node)
        {
            // symbolname
            Append("{0}", node.Name);
            return true;
        }

        public bool VisitNode(BooleanLiteral node)
        {
            // true|false
            Append("{0}", node.Value.ToString().ToLower());
            return true;
        }

        public bool VisitNode(FloatLiteral node)
        {
            // floatvalue
            Append("{0}", node.Value);
            return true;
        }

        public bool VisitNode(IntegerLiteral node)
        {
            // integervalue
            Append("{0}", node.Value);
            return true;
        }

        public bool VisitNode(NameLiteral node)
        {
            // 'name'
            Append("'{0}'", node.Value);
            return true;
        }

        public bool VisitNode(StringLiteral node)
        {
            // "string"
            Append("\"{0}\"", node.Value);
            return true;
        }

        #region Unused
        public bool VisitNode(StateLabel node)
        { throw new NotImplementedException(); }

        public bool VisitNode(VariableIdentifier node)
        { throw new NotImplementedException(); }

        #endregion
    }
}
