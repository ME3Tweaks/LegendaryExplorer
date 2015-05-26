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
        private int level;

        public CodeBuilderVisitor()
        {
            Lines = new List<String>();
            level = 0;
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
            Lines.Add(new String('\t', level) + (args.Length > 0 ? String.Format(text, args) : text));
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
            Write("");
            Write("// Types");
            foreach (VariableType type in node.TypeDeclarations)
                type.AcceptVisitor(this);

            Write("");
            Write("// Variables");
            foreach (VariableDeclaration decl in node.VariableDeclarations.ToList())
                decl.AcceptVisitor(this);

            Write("");
            Write("// Operators");
            foreach (OperatorDeclaration op in node.Operators)
                op.AcceptVisitor(this);

            Write("");
            Write("// Functions");
            foreach (Function func in node.Functions)
                func.AcceptVisitor(this);

            Write("");
            Write("// States");
            foreach (State state in node.States)
                state.AcceptVisitor(this);

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
            level++;

            foreach (Variable member in node.Members)
                member.AcceptVisitor(this);

            level--;
            Write("{0};", "}");

            return true;
        }

        public bool VisitNode(Enumeration node)
        {
            // enum enumname { \n contents \n };
            Write("enum {0} {1}", node.Name, "{");
            level++;

            foreach (VariableIdentifier value in node.Values)
                Write("{0},", value.Name);

            level--;
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
                // print body
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
            level++;

            if (node.Ignores.Count > 0)
                Write("ignores {0};", String.Join(", ", node.Ignores.Select(x => x.Name)));

            foreach (Function func in node.Functions)
                func.AcceptVisitor(this);

            // print body

            level--;
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
                // print body
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
            level++;

            node.Body.AcceptVisitor(this);
            level--;

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

            level++;
            node.Body.AcceptVisitor(this);
            level--;
            Write("{0}", "}");

            return true;
        }

        public bool VisitNode(WhileLoop node)
        {
            // while (condition) { /n contents /n }
            Write("while (");
            node.Condition.AcceptVisitor(this);
            Append(") {0}", "{");

            level++;
            node.Body.AcceptVisitor(this);
            level--;
            Write("{0}", "}");

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

        public bool VisitNode(IfStatement node)
        {
            // if (condition) { /n contents /n } [else...]
            Write("if (");
            node.Condition.AcceptVisitor(this);
            Append(") {0}", "{");

            level++;
            node.Then.AcceptVisitor(this);
            level--;
            Write("{0}", "}");

            if (node.Else != null)
            {
                Append(" else {0}", "{");
                level++;
                node.Else.AcceptVisitor(this);
                level--;
                Write("{0}", "}");
            }

            return true;
        }

        public bool VisitNode(InOpReference node)
        {
            // [(] expression operatorkeyword expression [)]
            Append("(");
            node.LeftOperand.AcceptVisitor(this);
            Append(" {0} ", node.Operator.OperatorKeyword);
            node.RightOperand.AcceptVisitor(this);
            Append(")");

            return true;
        }

        public bool VisitNode(PreOpReference node)
        {
            // operatorkeywordExpression
            Append("{0}", node.Operator.OperatorKeyword);
            node.Operand.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(PostOpReference node)
        {
            // ExpressionOperatorkeyword
            node.Operand.AcceptVisitor(this);
            Append("{0}", node.Operator.OperatorKeyword);

            return true;
        }

        public bool VisitNode(FunctionCall node)
        {
            // functionName( parameter1, parameter2.. )
            Append("{0}(", node.Function.Name);
            foreach(Expression expr in node.Parameters)
            {
                expr.AcceptVisitor(this);
                if (node.Parameters.IndexOf(expr) != node.Parameters.Count - 1)
                    Append(", ");
            }
            Append(")");

            return true;
        }

        public bool VisitNode(ArraySymbolRef node)
        {
            // symbolname[expression]
            Append("{0}[", node.Name);
            node.Index.AcceptVisitor(this);
            Append("]");
            return true;
        }

        public bool VisitNode(CompositeSymbolRef node)
        {
            // outersymbol.innersymbol
            Append("{0}.", node.OuterSymbol.Name);
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
            Append("{0}", node.Value);
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

        public bool VisitNode(VariableDeclaration node)
        { throw new NotImplementedException(); }
        public bool VisitNode(VariableIdentifier node)
        { throw new NotImplementedException(); }

        #endregion
    }
}
