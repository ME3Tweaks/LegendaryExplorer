using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Parsing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;
using static LegendaryExplorerCore.UnrealScript.Utilities.Keywords;

namespace LegendaryExplorerCore.UnrealScript.Analysis.Visitors
{
    public enum EF : byte
    {
        None = 0,
        Keyword,
        Specifier,
        Class,
        String,
        Name,
        Number,
        Enum,
        Comment,
        ERROR,
        Function,
        State,
        Label,
        Operator,
        Struct
    }

    public class CodeBuilderVisitor<TFormatter, TOutput> : IASTVisitor where TFormatter : class, ICodeFormatter<TOutput>, new()
    {
        public static TOutput GetOutput(ASTNode node)
        {
            var builder = new CodeBuilderVisitor<TFormatter, TOutput>();
            node.AcceptVisitor(builder);
            return builder.GetOutput();
        }

        public static TOutput GetFunctionSignature(Function func)
        {
            var builder = new CodeBuilderVisitor<TFormatter, TOutput>();
            builder.AppendReturnTypeAndParameters(func);
            return builder.GetOutput();
        }
        public static TOutput GetVariableDeclarationSignature(VariableDeclaration varDecl)
        {
            var builder = new CodeBuilderVisitor<TFormatter, TOutput>();
            builder.AppendVariableTypeAndScopeAndName(varDecl);
            return builder.GetOutput();
        }


        protected readonly TFormatter Formatter = new();
        private readonly Stack<int> ExpressionPrescedence = new(new []{NOPRESCEDENCE});

        private const int NOPRESCEDENCE = int.MaxValue;

        public int NestingLevel
        {
            get => Formatter.NestingLevel;
            set => Formatter.NestingLevel = value;
        }

        private int LabelNest;

        public int ForcedAlignment
        {
            get => Formatter.ForcedAlignment;
            set => Formatter.ForcedAlignment = value;
        }

        public bool ForceNoNewLines
        {
            get => Formatter.ForceNoNewLines;
            set => Formatter.ForceNoNewLines = value;
        }

        public TOutput GetOutput() => Formatter.GetOutput();

        private EF? ForcedFormatType = null;

        private bool ForceComment = false;

        private void Write(string text = "", EF formatType = EF.None)
        {
            if (!ForceComment)
            {
                Formatter.Write(text, ForcedFormatType ?? formatType);
            }
            else
            {
                Formatter.Write($"//{text}", EF.Comment);
            }
        }

        private void Append(string text, EF formatType = EF.None) => Formatter.Append(text, ForcedFormatType ?? formatType);
        private void Space() => Formatter.Space();

        private void ForceAlignment() => Formatter.ForceAlignment();

        public bool VisitNode(Class node)
        {
            Write(CLASS, EF.Keyword);
            Space();
            Append(node.Name, EF.Class);

            if (node.Parent != null && !node.Parent.Name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            {
                Space();
                Append(EXTENDS, EF.Keyword);
                Space();
                Append(node.Parent.Name, EF.Class);
            }
            if (node.OuterClass != null && !node.OuterClass.Name.Equals("Object", StringComparison.OrdinalIgnoreCase))
            {
                Space();
                Append(WITHIN, EF.Keyword);
                Space();
                Append(node.OuterClass.Name, EF.Class);
            }

            NestingLevel++;

            if (node.Interfaces.Any())
            {
                Write("implements", EF.Keyword);
                Append("(");
                Join(node.Interfaces.Select(i => i.Name).ToList(), ", ", EF.Class);
                Append(")");
            }


            EClassFlags flags = node.Flags;
            if (flags.Has(EClassFlags.Native))
            {
                Write("native", EF.Specifier);
            }
            if (flags.Has(EClassFlags.NativeOnly))
            {
                Write("nativeonly", EF.Specifier);
            }
            if (flags.Has(EClassFlags.NoExport))
            {
                Write("noexport", EF.Specifier);
            }
            if (flags.Has(EClassFlags.EditInlineNew))
            {
                Write("editinlinenew", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Placeable))
            {
                Write("placeable", EF.Specifier);
            }
            if (flags.Has(EClassFlags.HideDropDown))
            {
                Write("hidedropdown", EF.Specifier);
            }
            if (flags.Has(EClassFlags.NativeReplication))
            {
                Write("nativereplication", EF.Specifier);
            }
            if (flags.Has(EClassFlags.PerObjectConfig))
            {
                Write("perobjectconfig", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Abstract))
            {
                Write("abstract", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Deprecated))
            {
                Write("deprecated", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Transient))
            {
                Write("transient", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Config))
            {
                Write("config", EF.Specifier);
                Append($"({node.ConfigName})");
            }
            if (flags.Has(EClassFlags.SafeReplace))
            {
                Write("safereplace", EF.Specifier);
            }
            if (flags.Has(EClassFlags.Hidden))
            {
                Write("hidden", EF.Specifier);
            }
            if (flags.Has(EClassFlags.CollapseCategories))
            {
                Write("collapsecategories", EF.Specifier);
            }

            NestingLevel--;
            Append(";");

            // print the rest of the class, according to the standard "anatomy of an unrealscript" article.
            if (node.TypeDeclarations.Count > 0)
            {
                Write();
                Write("// Types", EF.Comment);
                foreach (VariableType type in node.TypeDeclarations)
                    type.AcceptVisitor(this);
            }

            if (node.VariableDeclarations.Count > 0)
            {
                Write();
                Write("// Variables", EF.Comment);
                foreach (VariableDeclaration decl in node.VariableDeclarations)
                    decl.AcceptVisitor(this);
            }

            if (node.Functions.Count > 0)
            {
                Write();
                Write("// Functions", EF.Comment);
                foreach (Function func in node.Functions)
                    func.AcceptVisitor(this);
            }

            if (node.States.Count > 0)
            {
                Write();
                Write("// States", EF.Comment);
                foreach (State state in node.States)
                    state.AcceptVisitor(this);
            }

            if (node.ReplicationBlock?.Statements.Count > 0)
            {
                Write();
                if (node.Flags.Has(EClassFlags.NativeReplication))
                {
                    Write("//Replication conditions for this class are native. This block has no effect", EF.Comment);
                }
                Write(REPLICATION, EF.Keyword);
                Write("{");
                NestingLevel++;
                node.ReplicationBlock.AcceptVisitor(this);
                NestingLevel--;
                Write("}");
            }

            Write();
            Write("//class default properties can be edited in the Properties tab for the class's Default__ object.", EF.Comment);
            node.DefaultProperties?.AcceptVisitor(this);

            return true;
        }


        public bool VisitNode(VariableDeclaration node)
        {
            //node.Outer can be null if we have decompiled a single var and nothing else
            //It only makes sense to have done that for a class field
            if (node.Outer?.Type != ASTNodeType.Function)
            {
                Write(VAR, EF.Keyword);
                if (!string.IsNullOrEmpty(node.Category) && !node.Category.CaseInsensitiveEquals("None"))
                {
                    Append($"({node.Category})");
                }
            }
            else
            {
                Write(LOCAL, EF.Keyword);
            }

            Space();
            WritePropertyFlags(node.Flags);
            AppendTypeNameAndName(node);
            Append(";");

            return true;
        }

        private void AppendTypeNameAndName(VariableDeclaration node)
        {
            AppendTypeName(node.VarType);
            Space();
            Append(node.Name);
            if (node.IsStaticArray)
            {
                Append("[");
                Append($"{node.ArrayLength}", EF.Number);
                Append("]");
            }
        }

        public void AppendVariableTypeAndScopeAndName(VariableDeclaration node)
        {
            AppendTypeName(node.VarType);
            Space();
            if (node.Outer is ObjectType outer)
            {
                Append(outer.Name, outer is Struct ? EF.Struct : EF.Class);
                Append(".");
            }
            Append(node.Name);
            if (node.IsStaticArray)
            {
                Append("[");
                Append($"{node.ArrayLength}", EF.Number);
                Append("]");
            }
        }

        public void AppendTypeName(VariableType node)
        {
            switch (node)
            {
                case StaticArrayType:
                case DynamicArrayType:
                case DelegateType:
                case ClassType:
                    node.AcceptVisitor(this);
                    break;
                case Enumeration:
                    Append(node.Name, EF.Enum);
                    break;
                case Const:
                    Append(node.Name);
                    break;
                case PrimitiveType:
                    Append(node.Name, EF.Keyword);
                    break;
                case Struct:
                    Append(node.Name, EF.Struct);
                    break;
                case Class:
                default:
                    Append(node.Name, EF.Class);
                    break;
            }
        }

        public bool VisitNode(VariableType node)
        {
            Append(node.Name);
            return true;
        }

        public bool VisitNode(StaticArrayType node)
        {
            AppendTypeName(node.ElementType);
            return true;
        }

        public bool VisitNode(DynamicArrayType node)
        {
            Append(ARRAY, EF.Keyword);
            Append("<");
            AppendTypeName(node.ElementType);
            Append(">");
            return true;
        }

        public bool VisitNode(DelegateType node)
        {
            Append(DELEGATE, EF.Keyword);
            Append("<");
            Append(node.DefaultFunction.Name, EF.Function);
            Append(">");
            return true;
        }

        public bool VisitNode(ClassType node)
        {
            Append(CLASS, EF.Keyword);
            Append("<");
            Append(node.ClassLimiter.Name, EF.Class);
            Append(">");
            return true;
        }

        public bool VisitNode(Struct node)
        {
            // struct [specifiers] structname [extends parentstruct] { \n contents \n };
            Write(STRUCT, EF.Keyword);
            Space();
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
            if (flags.Has(ScriptStructFlags.UnkStructFlag))
            {
                specs.Add(nameof(ScriptStructFlags.UnkStructFlag).ToLowerInvariant());
            }

            foreach (string spec in specs)
            {
                Append(spec, EF.Specifier);
                Space();
            }

            Append(node.Name, EF.Struct);
            Space();
            if (node.Parent != null)
            {
                Append(EXTENDS, EF.Keyword);
                Space();
                Append(node.Parent.Name, EF.Struct);
                Space();
            }

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
                Write();
                node.DefaultProperties.AcceptVisitor(this);
            }

            NestingLevel--;
            Write("};");

            return true;
        }

        public bool VisitNode(Enumeration node)
        {
            // enum enumname { \n contents \n };
            Write(ENUM, EF.Keyword);
            Space();
            Append(node.Name, EF.Enum);
            Write("{");
            NestingLevel++;

            foreach (EnumValue value in node.Values)
            {
                Write($"{value.Name},");
            }

            NestingLevel--;
            Write("};");

            return true;
        }

        public bool VisitNode(EnumValue node)
        {
            Append(node.Name);
            return true;
        }

        public bool VisitNode(Const node)
        {
            Write(CONST, EF.Keyword);
            Space();
            Append(node.Name);
            Space();
            Append("=", EF.Operator);
            Space();
            Append(node.Value);
            Append(";");

            return true;
        }

        public bool VisitNode(Function node)
        {
            // [specifiers] function [returntype] functionname ( [parameter declarations] ) body_or_semicolon
            Write();

            var specs = new List<string>();
            EFunctionFlags flags = node.Flags;

            if (flags.Has(EFunctionFlags.Private))
            {
                specs.Add("private");
            }
            if (flags.Has(EFunctionFlags.Protected))
            {
                specs.Add("protected");
            }
            if (flags.Has(EFunctionFlags.Public))
            {
                specs.Add("public");
            }
            if (flags.Has(EFunctionFlags.Static))
            {
                specs.Add("static");
            }
            if (flags.Has(EFunctionFlags.Final))
            {
                specs.Add("final");
            }
            if (flags.Has(EFunctionFlags.Delegate))
            {
                specs.Add("delegate");
            }
            if (flags.Has(EFunctionFlags.Event))
            {
                specs.Add("event");
            }
            if (flags.Has(EFunctionFlags.PreOperator))
            {
                specs.Add("preoperator");
            }
            else if (flags.Has(EFunctionFlags.Operator))
            {
                specs.Add("operator");
            }
            if (flags.Has(EFunctionFlags.Iterator))
            {
                specs.Add("iterator");
            }
            if (flags.Has(EFunctionFlags.Singular))
            {
                specs.Add("singular");
            }
            if (flags.Has(EFunctionFlags.Latent))
            {
                specs.Add("latent");
            }
            if (flags.Has(EFunctionFlags.Exec))
            {
                specs.Add("exec");
            }
            if (flags.Has(EFunctionFlags.NetReliable))
            {
                specs.Add("reliable");
            }
            else if (flags.Has(EFunctionFlags.Net))
            {
                specs.Add("unreliable");
            }
            if (flags.Has(EFunctionFlags.NetServer))
            {
                specs.Add("server");
            }
            if (flags.Has(EFunctionFlags.NetClient))
            {
                specs.Add("client");
            }
            else if (flags.Has(EFunctionFlags.Simulated))
            {
                specs.Add("simulated");
            }

            foreach (string spec in specs)
            {
                Append(spec, EF.Specifier);
                Space();
            }
            if (flags.Has(EFunctionFlags.Native))
            {
                Append("native", EF.Specifier);
                if (node.NativeIndex > 0)
                {
                    Append("(");
                    Append(node.NativeIndex.ToString(), EF.Number);
                    Append(")");
                }
                Space();
            }

            Append(FUNCTION, EF.Keyword);
            Space();
            AppendReturnTypeAndParameters(node);

            if (flags.Has(EFunctionFlags.Defined) && node.Body.Statements != null)
            {
                var tmp = LabelNest;
                LabelNest = NestingLevel;

                Write("{");
                NestingLevel++;
                if (node.Locals.Any())
                {
                    foreach (VariableDeclaration v in node.Locals)
                        v.AcceptVisitor(this);
                    Write();
                }
                node.Body.AcceptVisitor(this);
                NestingLevel--;
                Write("}");

                LabelNest = tmp;
            }
            else
            {
                Append(";");
                Write();
            }

            return true;
        }

        public void AppendReturnTypeAndParameters(Function node)
        {
            if (node.ReturnType != null)
            {
                if (node.CoerceReturn)
                {
                    Append("coerce", EF.Specifier);
                    Space();
                }
                AppendTypeName(node.ReturnType);
                Space();
            }
            Append(node.Name, EF.Function);
            Append("(");
            if (node.Parameters.Any())
            {
                node.Parameters[0].AcceptVisitor(this);
                for (int i = 1; i < node.Parameters.Count; i++)
                {
                    Append(",");
                    Space();
                    node.Parameters[i].AcceptVisitor(this);
                }
            }

            Append(")");
        }

        public bool VisitNode(FunctionParameter node)
        {
            // [specifiers] parametertype parametername[[staticarraysize]]
            WritePropertyFlags(node.Flags);
            AppendTypeNameAndName(node);
            if (node.DefaultParameter != null)
            {
                Space();
                Append("=", EF.Operator);
                Space();
                node.DefaultParameter.AcceptVisitor(this);
            }

            return true;
        }

        public bool VisitNode(State node)
        {
            // [specifiers] state statename [extends parentstruct] { \n contents \n };
            Write();

            var specs = new List<string>();
            EStateFlags flags = node.Flags;

            if (flags.Has(EStateFlags.Simulated))
            {
                specs.Add("simulated");
            }
            if (flags.Has(EStateFlags.Auto))
            {
                specs.Add("auto");
            }

            foreach (string spec in specs)
            {
                Append(spec, EF.Specifier);
                Space();
            }

            Append(STATE, EF.Keyword);
            if (flags.Has(EStateFlags.Editable))
            {
                Append("()");
            }
            Space();
            Append(node.Name, EF.State);
            Space();
            if (node.Parent != null)
            {
                Append(EXTENDS, EF.Keyword);
                Space();
                Append(node.Parent.Name, EF.State);
                Space();
            }

            var nestTmp = LabelNest;
            LabelNest = NestingLevel;

            Write("{");
            NestingLevel++;

            if (node.IgnoreMask != (EProbeFunctions)ulong.MaxValue)
            {
                Write(IGNORES, EF.Keyword);
                Space();
                Join((~node.IgnoreMask).MaskToList().Select(flag => flag.ToString()).ToList(), ", ", EF.Function);
                Write(";");
            }

            Write("// State Functions", EF.Comment);
            foreach (Function func in node.Functions)
                func.AcceptVisitor(this);

            Write();
            Write("// State code", EF.Comment);
            if (node.Body.Statements.Count != 0)
            {
                node.Body.AcceptVisitor(this);
            }

            NestingLevel--;
            Write("};");

            LabelNest = nestTmp;

            return true;
        }

        public bool VisitNode(CodeBody node)
        {
            foreach (Statement s in node.Statements)
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
            bool isStructDefaults = node.Outer is Struct;
            Write(isStructDefaults ? STRUCTDEFAULTPROPERTIES : node.IsNormalExport ? "properties" : DEFAULTPROPERTIES, EF.Keyword);
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
            Write("Begin", EF.Keyword);
            Space();
            Append(node.IsTemplate ? "Template" : "Object", EF.Keyword);
            Space();
            Append("Class", EF.Keyword);
            Append("=", EF.Operator);
            Append(node.Class.Name, EF.Class);
            Space();
            Append("Name", EF.Keyword);
            Append("=", EF.Operator);
            Append(node.NameDeclaration.Name);

            NestingLevel++;
            foreach (Statement s in node.Statements)
            {
                s.AcceptVisitor(this);
            }
            NestingLevel--;
            Write("End", EF.Keyword);
            Space();
            Append(node.IsTemplate ? "Template" : "Object", EF.Keyword);
            return true;
        }

        public bool VisitNode(DoUntilLoop node)
        { 
            // do { /n contents /n } until(condition);
            Write(DO, EF.Keyword);
            Space();
            Append("{");
            NestingLevel++;

            node.Body.AcceptVisitor(this);
            NestingLevel--;

            Write("}");
            Space();
            Append(UNTIL, EF.Keyword);
            Space();
            Append("(");
            node.Condition.AcceptVisitor(this);
            Append(")");

            return true;
        }

        public bool VisitNode(ForLoop node)
        {
            // for (initstatement; loopcondition; updatestatement) { /n contents /n }
            Write(FOR, EF.Keyword);
            Space();
            Append("(");
            ForceNoNewLines = true;
            node.Init?.AcceptVisitor(this);
            Append(";");
            Space();
            node.Condition?.AcceptVisitor(this);
            Append(";");
            Space();
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
            Write(FOREACH, EF.Keyword);
            Space();
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
            Write(WHILE, EF.Keyword);
            Space();
            Append("(");
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
            Write(SWITCH, EF.Keyword);
            Space();
            Append("(");
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
            Write(CASE, EF.Keyword);
            Space();
            node.Value.AcceptVisitor(this);
            Append(":");
            NestingLevel++;
            return true;
        }

        public bool VisitNode(DefaultCaseStatement node)
        {
            // default:
            NestingLevel--; // de-indent this line only
            Write(DEFAULT, EF.Keyword);
            Append(":");
            NestingLevel++;
            return true;
        }

        public bool VisitNode(AssignStatement node)
        {
            // reference = expression;
            Write();
            node.Target.AcceptVisitor(this);
            Space();
            Append("=", EF.Operator);
            Space();
            node.Value.AcceptVisitor(this);

            return true;
        }

        public bool VisitNode(AssertStatement node)
        {
            // assert(condition)
            Write(ASSERT, EF.Keyword);
            Append("(");
            node.Condition.AcceptVisitor(this);
            Append(")");

            return true;
        }

        public bool VisitNode(BreakStatement node)
        {
            // break;
            Write(BREAK, EF.Keyword);
            return true;
        }

        public bool VisitNode(ContinueStatement node)
        {
            // continue;
            Write(CONTINUE, EF.Keyword);
            return true;
        }

        public bool VisitNode(StopStatement node)
        {
            // stop;
            Write(STOP, EF.Keyword);
            return true;
        }

        public bool VisitNode(StateGoto node)
        {
            // goto expression;
            Write(GOTO, EF.Keyword);
            Space();
            node.LabelExpression.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(Goto node)
        {
            // goto labelName;
            Write(GOTO, EF.Keyword);
            Space();
            Append(node.LabelName, EF.Label);
            return true;
        }

        public bool VisitNode(ReturnStatement node)
        {
            // return expression;
            Write(RETURN, EF.Keyword);
            if (node.Value != null)
            {
                Space();
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
            Write();
            node.Value.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(ErrorStatement node)
        {
            // expression;
            Write();
            if (node.InnerStatement != null)
            {
                ForcedFormatType = EF.ERROR;
                node.InnerStatement.AcceptVisitor(this);
                ForcedFormatType = null;
            }
            else if (node.ErrorTokens != null)
            {
                foreach (ScriptToken errorToken in node.ErrorTokens)
                {
                    Append(errorToken.Value, EF.ERROR);
                }
            }
            else
            {
                int len = node.EndPos - node.StartPos;
                Append(new string('_', len), EF.ERROR);
            }

            return true;
        }

        public bool VisitNode(ErrorExpression node)
        {
            if (node.InnerExpression != null)
            {
                ForcedFormatType = EF.ERROR;
                node.InnerExpression.AcceptVisitor(this);
                ForcedFormatType = null;
            }
            else if (node.ErrorTokens != null)
            {
                foreach (ScriptToken errorToken in node.ErrorTokens)
                {
                    Append(errorToken.Value, EF.ERROR);
                }
            }
            else
            {
                int len = node.EndPos - node.StartPos;
                Append(new string('_', len), EF.ERROR);
            }

            return true;
        }

        public bool VisitNode(IfStatement node)
        {
            // if (condition) { /n contents /n } [else...]
            VisitIf(node);
            return true;
        }

        public bool VisitNode(ReplicationStatement node)
        {
            Write(IF, EF.Keyword);
            Space();
            Append("(");
            node.Condition.AcceptVisitor(this);
            Append(")");
            NestingLevel++;
            Write();
            for (int i = 0; i < node.ReplicatedVariables.Count; i++)
            {
                if (i > 0)
                {
                    Append(", ");
                }
                node.ReplicatedVariables[i].AcceptVisitor(this);
            }
            NestingLevel--;
            return true;
        }

        private void VisitIf(IfStatement node, bool ifElse = false)
        {
            bool invalidBlock = !ForceComment && node.Condition is SymbolReference { Name: __IN_EDITOR };
            if (invalidBlock)
            {
                ForceComment = true;
            }

            if (!ifElse)
                Write(); // New line only if we're not chaining
            Append(IF, EF.Keyword);
            Space();
            Append("(");
            node.Condition.AcceptVisitor(this);
            Append(")");
            Write("{");

            NestingLevel++;
            node.Then.AcceptVisitor(this);
            NestingLevel--;
            Write("}");

            if (node.Else != null && node.Else.Statements.Any())
            {
                Write(ELSE, EF.Keyword);
                if (invalidBlock)
                {
                    ForceComment = false;
                }
                if (node.Else.Statements.Count == 1 && node.Else.Statements[0] is IfStatement)
                {
                    Space();
                    VisitIf(node.Else.Statements[0] as IfStatement, !invalidBlock);
                }
                else
                {
                    Write("{");
                    NestingLevel++;
                    node.Else.AcceptVisitor(this);
                    NestingLevel--;
                    Write("}");
                }
            }
            if (invalidBlock)
            {
                ForceComment = false;
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
            Space();
            Append("?", EF.Operator);
            Space();
            node.TrueExpression.AcceptVisitor(this);
            Space();
            Append(":", EF.Operator);
            Space();
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
            if (node.Operator.OperatorType is TokenType.AtSign or TokenType.DollarSign && node.LeftOperand is PrimitiveCast { CastType.Name: "string" } lpc)
            {
                lpc.CastTarget.AcceptVisitor(this);
            }
            else
            {
                node.LeftOperand.AcceptVisitor(this);
            }
            Space();
            Append(OperatorHelper.OperatorTypeToString(node.Operator.OperatorType), EF.Operator);
            Space();
            if (node.Operator.OperatorType is TokenType.AtSign or TokenType.DollarSign or TokenType.StrConcAssSpace or TokenType.StrConcatAssign && node.RightOperand is PrimitiveCast { CastType.Name: "string" } rpc)
            {
                rpc.CastTarget.AcceptVisitor(this);
            }
            else
            {
                node.RightOperand.AcceptVisitor(this);
            }
            if (scopeNeeded) Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PreOpReference node)
        {
            ExpressionPrescedence.Push(1);
            // operatorkeywordExpression
            Append(OperatorHelper.OperatorTypeToString(node.Operator.OperatorType), EF.Operator);
            node.Operand.AcceptVisitor(this);

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(PostOpReference node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // ExpressionOperatorkeyword
            node.Operand.AcceptVisitor(this);
            Append(OperatorHelper.OperatorTypeToString(node.Operator.OperatorType), EF.Operator);

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
            Space();
            Append(node.IsEqual ? "==" : "!=", EF.Operator);
            Space();
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
            Space();
            Append(node.IsEqual ? "==" : "!=", EF.Operator);
            Space();
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

            Append(NEW, EF.Keyword);
            Space();
            if (node.OuterObject != null)
            {
                Append("(");
                node.OuterObject.AcceptVisitor(this);
                if (node.ObjectName != null)
                {
                    Append(",");
                    Space();
                    node.ObjectName.AcceptVisitor(this);
                    if (node.Flags != null)
                    {
                        Append(",");
                        Space();
                        node.Flags.AcceptVisitor(this);
                    }
                }
                Append(") ");
            }

            node.ObjectClass.AcceptVisitor(this);

            if (node.Template != null)
            {
                Space();
                Append("(");
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
            if (node.Function.IsGlobal)
            {
                Append(GLOBAL, EF.Keyword);
                Append(".", EF.Operator);
            }
            else if (node.Function.IsSuper)
            {
                Append(SUPER, EF.Keyword);
                if (node.Function.SuperSpecifier is {} superSpecifier)
                {
                    Append("(");
                    Append(superSpecifier.Name, EF.Class);
                    Append(")");
                }
                Append(".", EF.Operator);
            }
            Append(node.Function.Name, EF.Function);
            Append("(");
            int countOfNonNullArgs = node.Arguments.FindLastIndex(arg => arg is not null) + 1;
            for (int i = 0; i < countOfNonNullArgs; i++)
            {
                node.Arguments[i]?.AcceptVisitor(this);
                if (i < countOfNonNullArgs - 1)
                {
                    Append(",");
                    Space();
                }
            }

            Append(")");

            ExpressionPrescedence.Pop();
            return true;
        }

        public bool VisitNode(DelegateCall node)
        {
            ExpressionPrescedence.Push(NOPRESCEDENCE);
            // functionName( parameter1, parameter2.. )
            Append(node.DelegateReference.Name);
            Append("(");
            for (int i = 0; i < node.Arguments.Count; i++)
            {
                node.Arguments[i]?.AcceptVisitor(this);
                if (i < node.Arguments.Count - 1)
                {
                    Append(",");
                    Space();
                }
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
            bool needsParentheses = node.OuterSymbol is InOpReference or PreOpReference or PostOpReference or NewOperator;
            if (needsParentheses)
            {
                Append("(");
            }
            node.OuterSymbol.AcceptVisitor(this);
            if (needsParentheses)
            {
                Append(")");
            }
            if (node.IsClassContext && node.InnerSymbol is not DefaultReference)
            {
                Append(".", EF.Operator);
                Append(STATIC, EF.Keyword);
            }
            Append(".", EF.Operator);
            node.InnerSymbol.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(SymbolReference node)
        {
            if (node.Node is EnumValue ev)
            {
                Append(ev.Enum.Name, EF.Enum);
                Append(".", EF.Operator);
                Append(ev.Name);
                return true;
            }
            Append(node.Name);
            return true;
        }

        public bool VisitNode(DefaultReference node)
        {
            // symbolname
            Append(DEFAULT, EF.Keyword);
            Append(".", EF.Operator);
            Append(node.Name);
            return true;
        }

        public bool VisitNode(DynArrayLength node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(LENGTH);
            return true;
        }

        public bool VisitNode(DynArrayAdd node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(ADD, EF.Function);
            Append("(");
            node.CountArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayAddItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(ADDITEM, EF.Function);
            Append("(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayInsert node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(INSERT, EF.Function);
            Append("(");
            node.IndexArg.AcceptVisitor(this);
            Append(",");
            Space();
            node.CountArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayInsertItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(INSERTITEM, EF.Function);
            Append("(");
            node.IndexArg.AcceptVisitor(this);
            Append(",");
            Space();
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayRemove node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(REMOVE, EF.Function);
            Append("(");
            node.IndexArg.AcceptVisitor(this);
            Append(",");
            Space();
            node.CountArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayRemoveItem node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(REMOVEITEM, EF.Function);
            Append("(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayFind node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(FIND, EF.Function);
            Append("(");
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArrayFindStructMember node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(FIND, EF.Function);
            Append("(");
            node.MemberNameArg.AcceptVisitor(this);
            Append(",");
            Space();
            node.ValueArg.AcceptVisitor(this);
            Append(")");
            return true;
        }

        public bool VisitNode(DynArraySort node)
        {
            node.DynArrayExpression.AcceptVisitor(this);
            Append(".", EF.Operator);
            Append(SORT, EF.Function);
            Append("(");
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
                Append(",");
                Space();
                node.IndexArg.AcceptVisitor(this);
            }
            Append(")");
            return true;
        }

        public bool VisitNode(BooleanLiteral node)
        {
            // true|false
            Append(node.Value ? TRUE : FALSE, EF.Keyword);
            return true;
        }

        public bool VisitNode(FloatLiteral node)
        {
            Append(FormatFloat(node.Value), EF.Number); //TODO: seperate out the minus?
            return true;
        }

        public bool VisitNode(IntegerLiteral node)
        {
            // integervalue
            Append($"{node.Value}", EF.Number);
            return true;
        }

        public bool VisitNode(NameLiteral node)
        {
            //commented version is unrealscript compliant, but harder to parse
            //Append(node.Outer is StructLiteral ? "\"{EncodeName(node.Value)}\"" : "'{EncodeName(node.Value)}'");
            Append($"'{EncodeName(node.Value)}'", EF.Name);
            return true;
        }

        public bool VisitNode(ObjectLiteral node)
        {
            Append(node.Class.Name, EF.Class);
            node.Name.AcceptVisitor(this);
            return true;
        }

        public bool VisitNode(NoneLiteral node)
        {
            Append(NONE, EF.Keyword);
            return true;
        }

        public bool VisitNode(VectorLiteral node)
        {
            Append(VECT, EF.Keyword);
            Append("(");
            Append(FormatFloat(node.X), EF.Number);
            Append(",");
            Space();
            Append(FormatFloat(node.Y), EF.Number);
            Append(",");
            Space();
            Append(FormatFloat(node.Z), EF.Number);
            Append(")");
            return true;
        }

        public bool VisitNode(RotatorLiteral node)
        {
            Append(ROT, EF.Keyword);
            Append("(");
            Append(FormatRotator(node.Pitch), EF.Number);
            Append(",");
            Space();
            Append(FormatRotator(node.Yaw), EF.Number);
            Append(",");
            Space();
            Append(FormatRotator(node.Roll), EF.Number);
            Append(")");
            return true;

            static string FormatRotator(int n)
            {
                return n.ToString();
                //string s = "";
                //if (n < 0)
                //{
                //    s += '-';
                //}

                //s += $"0x{Math.Abs(n):X8}";
                //return s;
            }
        }

        public bool VisitNode(StringLiteral node)
        {
            // "string"
            Append($"\"{EncodeString(node.Value)}\"", EF.String);
            return true;
        }

        public bool VisitNode(StringRefLiteral node)
        {
            Append($"${node.Value}", EF.Number);
            return true;
        }
        public bool VisitNode(StructLiteral node)
        {
            bool multiLine = !ForceNoNewLines && (node.Statements.Count > 5 || node.Statements.Any(stmnt => stmnt.Value is StructLiteral or DynamicArrayLiteral));

            bool oldForceNoNewLines = ForceNoNewLines;
            int oldForcedAlignment = ForcedAlignment;
            if (multiLine)
            {
                Append("{");
                ForceAlignment();
            }
            else
            {
                ForceNoNewLines = true;
                Append("{");
            }
            for (int i = 0; i < node.Statements.Count; i++)
            {
                if (i > 0)
                {
                    Append(",");
                    Space();
                }
                node.Statements[i].AcceptVisitor(this);
            }

            if (multiLine)
            {
                ForcedAlignment -= 1;
                Write("}");
                ForcedAlignment = oldForcedAlignment;
            }
            else
            {
                Append("}");
                ForceNoNewLines = oldForceNoNewLines;
            }
            return true;
        }

        public bool VisitNode(DynamicArrayLiteral node)
        {
            bool multiLine = !ForceNoNewLines && (node.Values.Any(expr => expr is StructLiteral) || node.Values.Count > 7);

            bool oldForceNoNewLines = ForceNoNewLines;
            int oldForcedAlignment = ForcedAlignment;
            Append("(");
            if (multiLine)
            {
                ForceAlignment();
            }
            else
            {
                ForceNoNewLines = true;
            }
            for (int i = 0; i < node.Values.Count; i++)
            {
                if (i > 0)
                {
                    Append(",");
                    Space();
                    if (multiLine)
                    {
                        Write();
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

        public bool VisitNode(Label node)
        {
            // Label
            var temp = NestingLevel;
            NestingLevel = LabelNest;
            Write(node.Name, EF.Label);
            Append(":");
            NestingLevel = temp;
            return true;
        }

        private void WritePropertyFlags(EPropertyFlags flags)
        {
            var specs = new List<string>();

            if (flags.Has(EPropertyFlags.OptionalParm))
            {
                specs.Add("optional");
            }

            if (flags.Has(EPropertyFlags.Const))
            {
                specs.Add("const");
            }

            if (flags.Has(EPropertyFlags.GlobalConfig))
            {
                specs.Add("globalconfig");
            }
            else if (flags.Has(EPropertyFlags.Config))
            {
                specs.Add("config");
            }

            if (flags.Has(EPropertyFlags.EditInline))
            {
                specs.Add(nameof(EPropertyFlags.EditInline).ToLowerInvariant());
            }

            if (flags.Has(EPropertyFlags.Localized))
            {
                specs.Add("localized");
            }

            //TODO: private, protected, and public are in ObjectFlags, not PropertyFlags 
            if (flags.Has(EPropertyFlags.ProtectedWrite))
            {
                specs.Add("protectedwrite");
            }

            if (flags.Has(EPropertyFlags.PrivateWrite))
            {
                specs.Add("privatewrite");
            }

            if (flags.Has(EPropertyFlags.EditConst))
            {
                specs.Add("editconst");
            }

            if (flags.Has(EPropertyFlags.EditHide))
            {
                specs.Add("edithide");
            }

            if (flags.Has(EPropertyFlags.EditTextBox))
            {
                specs.Add("edittextbox");
            }

            if (flags.Has(EPropertyFlags.Input))
            {
                specs.Add("input");
            }

            if (flags.Has(EPropertyFlags.Transient))
            {
                specs.Add("transient");
            }

            if (flags.Has(EPropertyFlags.Native))
            {
                specs.Add("native");
            }

            if (flags.Has(EPropertyFlags.NoExport))
            {
                specs.Add("noexport");
            }

            if (flags.Has(EPropertyFlags.DuplicateTransient))
            {
                specs.Add("duplicatetransient");
            }

            if (flags.Has(EPropertyFlags.NoImport))
            {
                specs.Add("noimport");
            }

            if (flags.Has(EPropertyFlags.OutParm))
            {
                specs.Add("out");
            }

            if (flags.Has(EPropertyFlags.ExportObject))
            {
                specs.Add("export");
            }

            if (flags.Has(EPropertyFlags.EditInlineUse))
            {
                specs.Add("editinlineuse");
            }

            if (flags.Has(EPropertyFlags.NoClear))
            {
                specs.Add("noclear");
            }

            if (flags.Has(EPropertyFlags.EditFixedSize))
            {
                specs.Add("editfixedsize");
            }

            if (flags.Has(EPropertyFlags.RepNotify))
            {
                specs.Add("repnotify");
            }

            if (flags.Has(EPropertyFlags.RepRetry))
            {
                specs.Add("repretry");
            }

            if (flags.Has(EPropertyFlags.Interp))
            {
                specs.Add("interp");
            }

            if (flags.Has(EPropertyFlags.NonTransactional))
            {
                specs.Add("nontransactional");
            }

            if (flags.Has(EPropertyFlags.Deprecated))
            {
                specs.Add("deprecated");
            }

            if (flags.Has(EPropertyFlags.SkipParm))
            {
                specs.Add("skip");
            }

            if (flags.Has(EPropertyFlags.CoerceParm))
            {
                specs.Add("coerce");
            }

            if (flags.Has(EPropertyFlags.AlwaysInit))
            {
                specs.Add("init");
            }

            if (flags.Has(EPropertyFlags.DataBinding))
            {
                specs.Add("databinding");
            }

            if (flags.Has(EPropertyFlags.EditorOnly))
            {
                specs.Add("editoronly");
            }

            if (flags.Has(EPropertyFlags.NotForConsole))
            {
                specs.Add("notforconsole");
            }

            if (flags.Has(EPropertyFlags.Archetype))
            {
                specs.Add("archetype");
            }

            if (flags.Has(EPropertyFlags.SerializeText))
            {
                specs.Add("serializetext");
            }

            if (flags.Has(EPropertyFlags.CrossLevelActive))
            {
                specs.Add("crosslevelactive");
            }

            if (flags.Has(EPropertyFlags.CrossLevelPassive))
            {
                specs.Add("crosslevelpassive");
            }

            //BioWare specific flags
            if (flags.Has(EPropertyFlags.RsxStorage))
            {
                specs.Add("rsxstorage");
            }
            if (flags.Has(EPropertyFlags.UnkFlag1))
            {
                specs.Add(nameof(EPropertyFlags.UnkFlag1).ToLowerInvariant());
            }
            if (flags.Has(EPropertyFlags.LoadForCooking))
            {
                specs.Add("loadforcooking");
            }
            if (flags.Has(EPropertyFlags.BioNonShip))
            {
                specs.Add("biononship");
            }
            if (flags.Has(EPropertyFlags.BioIgnorePropertyAdd))
            {
                specs.Add("bioignorepropertyadd");
            }
            if (flags.Has(EPropertyFlags.SortBarrier))
            {
                specs.Add("sortbarrier");
            }
            if (flags.Has(EPropertyFlags.ClearCrossLevel))
            {
                specs.Add("clearcrosslevel");
            }
            if (flags.Has(EPropertyFlags.BioSave))
            {
                specs.Add("biosave");
            }
            if (flags.Has(EPropertyFlags.BioExpanded))
            {
                specs.Add("bioexpanded");
            }
            if (flags.Has(EPropertyFlags.BioAutoGrow))
            {
                specs.Add("bioautogrow");
            }

            foreach (string spec in specs)
            {
                Append(spec, EF.Specifier);
                Space();
            }
        }

        public static string FormatFloat(float single)
        {
            //G9 ensures a fully accurate version of the float (no rounding) is written.
            //more details here: https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-numeric-format-strings#the-round-trip-r-format-specifier 
            string floatString = single.ToString("G9", NumberFormatInfo.InvariantInfo).Replace("E+", "e");

            if (floatString.Contains("E-"))
            {
                //unrealscript does not support negative exponents in literals, so we have to format it manually
                //for example, 1.401298E-45 would be formatted as 0.00000000000000000000000000000000000000000000140129846
                //This code assumes there is exactly 1 digit before the decimal point, which will always be the case when formatted as scientific notation with the G specifier
                string minus = null;
                if (floatString[0] == '-')
                {
                    minus = "-";
                    floatString = floatString[1..];
                }
                int ePos = floatString.IndexOf("E-");
                int exponent = int.Parse(floatString[(ePos + 2)..]);
                string digits = floatString[..ePos].Replace(".", "");
                floatString = $"{minus}0.{new string('0', exponent - 1)}{digits}";
            }
            else if (!floatString.Contains(".") && !floatString.Contains("e"))
            {
                //need a decimal place in the float so that it does not get parsed as an int
                floatString += $".0";
            }

            return floatString;
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

        private void Join(List<string> items, string seperator, EF formatType = EF.None)
        {
            Append(items[0], formatType);
            for (int i = 1; i < items.Count; i++)
            {
                Append(seperator);
                Append(items[i], formatType);
            }
        }

        #region Unused

        public bool VisitNode(VariableIdentifier node)
        { throw new NotImplementedException(); }

        #endregion
    }
    public class CodeBuilderVisitor<TFormatter> : CodeBuilderVisitor<TFormatter, string> where TFormatter : class, ICodeFormatter<string>, new()
    { }

    public class CodeBuilderVisitor : CodeBuilderVisitor<PlainTextCodeFormatter>
    { }

    public interface ICodeFormatter<out TOutput>
    {
        TOutput GetOutput();

        void Write(string text, EF formatType);

        void Append(string text, EF formatType);

        void Space();

        void ForceAlignment();

        int NestingLevel { get; set; }
        int ForcedAlignment { get; set; }
        bool ForceNoNewLines { get; set; }
    }

    public class PlainTextCodeFormatter : ICodeFormatter<string>
    {
        public int NestingLevel { get; set; }
        public int ForcedAlignment { get; set; }
        public bool ForceNoNewLines { get; set; }

        protected readonly List<string> Lines = new();
        protected string currentLine;

        public string GetOutput() => string.Join("\n", Lines.Append(currentLine));

        public virtual void Write(string text, EF _)
        {
            if (!ForceNoNewLines)
            {
                if (currentLine != null)
                {
                    Lines.Add(currentLine);
                }

                currentLine = new string(' ', ForcedAlignment + NestingLevel * 4);
            }
            Append(text, _);
        }

        public virtual void Append(string text, EF _)
        {
            currentLine += text;
        }

        public void Space() => Append(" ", EF.None);

        public void ForceAlignment()
        {
            ForcedAlignment = currentLine.Length - NestingLevel * 4;
        }
    }

    public class HTMLCodeFormatter : ICodeFormatter<string>
    {
        public int NestingLevel { get; set; }
        public int ForcedAlignment { get; set; }
        public bool ForceNoNewLines { get; set; }

        private readonly List<string> Lines = new();
        private string currentLine;
        private int lineDisplayLength;

        public string GetOutput()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<html>");
            sb.AppendLine("    <head>");
            sb.AppendLine("        <style>");
            sb.Append(css());
            sb.AppendLine("        </style>");
            sb.AppendLine("    </head>");
            sb.Append("<body><pre><code>");
            foreach (string line in Lines)
            {
                sb.AppendLine(line);
            }
            sb.Append(currentLine);
            sb.AppendLine("</code></pre></body>");
            sb.AppendLine("</html>");
            return sb.ToString();
        }

        private static string css()
        {
            return @"
        html, body, pre { background-color: #1E1E1E;}
        .EF-None, code { color: #DBDBDB; }
        .EF-Keyword { color: #569BBF; }
        .EF-Specifier { color: #569BBF; }
        .EF-TypeName { color: #4EC8AF; }
        .EF-String { color: #D59C7C; }
        .EF-Name { color: #D59C7C; }
        .EF-Number { color: #B1CDA7; }
        .EF-Enum { color: #B7D6A2; }
        .EF-Comment { color: #57A54A; }
        .EF-ERROR { color: #FF0000; }
        .EF-Function { color: #DBDBDB; }
        .EF-State { color: #DBDBDB; }
        .EF-Label { color: #DBDBDB; }
        .EF-Operator { color: #B3B3B3; }
.
";
        }

        public void Write(string text, EF formatType)
        {
            if (!ForceNoNewLines)
            {
                if (currentLine != null)
                {
                    Lines.Add(currentLine);
                }

                currentLine = new string(' ', ForcedAlignment + NestingLevel * 4);
                lineDisplayLength = ForcedAlignment;
            }
            Append(text, formatType);
        }

        public void Append(string text, EF formatType)
        {
            lineDisplayLength += text.Length;
            switch (formatType)
            {
                case EF.None:
                    currentLine += WebUtility.HtmlEncode(text);
                    break;
                case EF.Keyword:
                case EF.Specifier:
                case EF.Class:
                case EF.String:
                case EF.Name:
                case EF.Number:
                case EF.Enum:
                case EF.Comment:
                case EF.ERROR:
                case EF.Function:
                case EF.State:
                case EF.Label:
                case EF.Operator:
                case EF.Struct:
                default:
                    Span(text, formatType);
                    break;
            }
        }

        private void Span(string text, EF formatType)
        {
            currentLine += $"<span class=\"{nameof(EF)}-{formatType}\">{WebUtility.HtmlEncode(text)}</span>";
        }

        public void Space()
        {
            currentLine += " ";
            lineDisplayLength += 1;
        }

        public void ForceAlignment()
        {
            ForcedAlignment = lineDisplayLength;
        }
    }
}
