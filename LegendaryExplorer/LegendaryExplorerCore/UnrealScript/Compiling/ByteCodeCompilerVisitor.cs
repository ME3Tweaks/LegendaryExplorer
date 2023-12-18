using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using LegendaryExplorerCore.Gammtek.Extensions;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Misc;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Analysis.Visitors;
using LegendaryExplorerCore.UnrealScript.Language.ByteCode;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Language.Util;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Compiling
{
    internal class ByteCodeCompilerVisitor : BytecodeWriter, IASTVisitor
    {
        private readonly UStruct Target;
        private IContainsByteCode CompilationUnit;
        private readonly IEntry ContainingClass;

        private readonly CaseInsensitiveDictionary<ExportEntry> parameters = new();
        private readonly CaseInsensitiveDictionary<ExportEntry> locals = new();

        private bool inAssignTarget;
        private bool inIteratorCall;
        private bool useInstanceDelegate;
        private SkipPlaceholder iteratorCallSkip;

        private readonly Dictionary<Label, List<JumpPlaceholder>> LabelJumps = new();

        // private Func<IMEPackage, string, IEntry> MissingObjectResolver;

        [Flags]
        private enum NestType
        {
            Loop = 1,
            For = 2 | Loop,
            While = 4 | Loop,
            DoUntil = 8 | Loop,
            ForEach = 16 | Loop,
            Switch = 32,
        }
        private class Nest : List<JumpPlaceholder>
        {
            public readonly NestType Type;
            public JumpPlaceholder CaseJumpPlaceholder;
            public VariableType SwitchType;

            public Nest(NestType type)
            {
                Type = type;
            }

            public void SetPositionFor(JumpType jumpType)
            {
                foreach (JumpPlaceholder jumpPlaceholder in this.Where(j => j.Type == jumpType))
                {
                    jumpPlaceholder.End();
                }
            }
        }

        private readonly Stack<Nest> Nests = new();

        private class NestedContextFlag
        {
            public bool HasNestedContext;
        }

        private readonly Stack<NestedContextFlag> InContext = new() { null };

        private ByteCodeCompilerVisitor(UStruct target) : base(target.Export.FileRef)
        {
            Target = target;
            IEntry containingClass = Target.Export;
            while (containingClass.ClassName != "Class")
            {
                containingClass = containingClass.Parent;
            }

            ContainingClass = containingClass;
        }

        public static void Compile(Function func, UFunction target, UnrealScriptOptionsPackage usop)
        {
            var bytecodeCompiler = new ByteCodeCompilerVisitor(target)
            {
                // MissingObjectResolver = usop.MissingObjectResolver
            };
            bytecodeCompiler.Compile(func, usop);
        }

        private void Compile(Function func, UnrealScriptOptionsPackage usop)
        {
            if (Target is UFunction uFunction)
            {
                CompilationUnit = func;
                var nextItem = uFunction.Children;
                UProperty returnValue = null;
                while (uFunction.Export.FileRef.TryGetUExport(nextItem, out ExportEntry nextChild))
                {
                    var objBin = ObjectBinary.From(nextChild);
                    switch (objBin)
                    {
                        case UProperty uProperty:
                            if (uProperty.PropertyFlags.Has(EPropertyFlags.ReturnParm))
                            {
                                returnValue = uProperty;
                            }
                            else if (uProperty.PropertyFlags.Has(EPropertyFlags.Parm))
                            {
                                parameters.Add(nextChild.ObjectName.Instanced, uProperty.Export);
                            }
                            else
                            {
                                locals.Add(nextChild.ObjectName.Instanced, uProperty.Export);
                            }
                            nextItem = uProperty.Next;
                            break;
                        default:
                            nextItem = 0;
                            break;
                    }
                }


                foreach (FunctionParameter parameter in func.Parameters.Where(param => param.IsOptional))
                {
                    if (parameter.DefaultParameter is Expression expr)
                    {
                        WriteOpCode(OpCodes.DefaultParmValue);

                        using (WriteSkipPlaceholder())
                        {
                            Emit(AddConversion(parameter.VarType, expr), usop);
                            WriteOpCode(OpCodes.EndParmValue);
                        }
                    }
                    else
                    {
                        WriteOpCode(OpCodes.Nothing);
                    }
                }


                if (func.IsNative)
                {
                    foreach (FunctionParameter functionParameter in func.Parameters)
                    {
                        WriteOpCode(OpCodes.NativeParm);
                        WriteObjectRef(ResolveSymbol(functionParameter, usop));
                    }
                    WriteOpCode(OpCodes.Nothing);
                }
                else
                {
                    Emit(func.Body, usop);

                    WriteOpCode(OpCodes.Return);
                    if (returnValue != null)
                    {
                        WriteOpCode(OpCodes.ReturnNullValue);
                        WriteObjectRef(returnValue.Export);
                    }
                    else
                    {
                        WriteOpCode(OpCodes.Nothing);
                    }
                }

                WriteOpCode(OpCodes.EndOfScript);

                foreach ((Label label, List<JumpPlaceholder> jumpPlaceholders) in LabelJumps)
                {
                    foreach (JumpPlaceholder jumpPlaceholder in jumpPlaceholders)
                    {
                        jumpPlaceholder.End(label.StartOffset);
                    }
                }

                Target.ScriptBytecodeSize = GetMemLength();
                Target.ScriptBytes = GetByteCode();
            }
            else
            {
                throw new Exception("Cannot compile a function to a state!");
            }
        }


        public static void Compile(State state, UState target, UnrealScriptOptionsPackage uosp)
        {
            var bytecodeCompiler = new ByteCodeCompilerVisitor(target)
            {
                // MissingObjectResolver = missingObjectResolver
            };
            bytecodeCompiler.Compile(state, uosp);
        }

        private void Compile(State state, UnrealScriptOptionsPackage usop)
        {
            if (Target is UState uState)
            {
                CompilationUnit = state;
                uState.LabelTableOffset = ushort.MaxValue;
                Emit(state.Body, usop);

                WriteOpCode(OpCodes.Stop);

                if (state.Labels.Any())
                {
                    int paddingNeeded = (Position + 1).Align(4) - (Position + 1);
                    for (; paddingNeeded > 0; paddingNeeded--)
                    {
                        WriteOpCode(OpCodes.Nothing);
                    }
                    WriteOpCode(OpCodes.LabelTable);
                    uState.LabelTableOffset = Position;
                    foreach (Label label in Enumerable.Reverse(state.Labels))
                    {
                        WriteName(label.Name);
                        WriteInt(label.StartOffset);
                    }
                    WriteName("None");
                    WriteInt(ushort.MaxValue);
                }

                WriteOpCode(OpCodes.EndOfScript);

                foreach ((Label label, List<JumpPlaceholder> jumpPlaceholders) in LabelJumps)
                {
                    foreach (JumpPlaceholder jumpPlaceholder in jumpPlaceholders)
                    {
                        jumpPlaceholder.End(label.StartOffset);
                    }
                }

                Target.ScriptBytecodeSize = GetMemLength();
                Target.ScriptBytes = GetByteCode();
            }
            else
            {
                throw new Exception("Cannot compile a state to a function!");
            }
        }

        public static void Compile(Class cls, UClass target, UnrealScriptOptionsPackage uosp)
        {
            var bytecodeCompiler = new ByteCodeCompilerVisitor(target)
            {
                // MissingObjectResolver = missingObjectResolver
            };
            bytecodeCompiler.Compile(cls, uosp);
        }

        private void Compile(Class cls, UnrealScriptOptionsPackage usop)
        {
            if (Target is not UClass)
            {
                throw new Exception("Cannot compile a replication block to a non-class");
            }
            CompilationUnit = cls;

            Emit(cls.ReplicationBlock, usop);
            WriteOpCode(OpCodes.EndOfScript);
            Target.ScriptBytecodeSize = GetMemLength();
            Target.ScriptBytes = GetByteCode();
        }

        public bool VisitNode(CodeBody node, UnrealScriptOptionsPackage usop)
        {
            foreach (Statement statement in node.Statements)
            {
                Emit(statement, usop);
            }
            return true;
        }

        public bool VisitNode(DoUntilLoop node, UnrealScriptOptionsPackage usop)
        {
            var loopStartPos = Position;
            Nests.Push(new Nest(NestType.DoUntil));
            Emit(node.Body, usop);
            Nest nest = Nests.Pop();
            nest.SetPositionFor(JumpType.Continue);
            WriteOpCode(OpCodes.JumpIfNot);
            WriteUShort(loopStartPos);
            Emit(node.Condition, usop);
            nest.SetPositionFor(JumpType.Break);
            return true;
        }

        public bool VisitNode(ForLoop node, UnrealScriptOptionsPackage usop)
        {
            node.Init?.AcceptVisitor(this, usop);
            var loopStartPos = Position;
            JumpPlaceholder endJump = EmitConditionalJump(node.Condition, usop);
            Nests.Push(new Nest(NestType.For));
            Emit(node.Body, usop);
            Nest nest = Nests.Pop();
            nest.SetPositionFor(JumpType.Continue);
            node.Update?.AcceptVisitor(this, usop);
            WriteOpCode(OpCodes.Jump);
            WriteUShort(loopStartPos);
            endJump.End();
            nest.SetPositionFor(JumpType.Break);
            return true;
        }

        public bool VisitNode(ForEachLoop node, UnrealScriptOptionsPackage usop)
        {
            if (node.IteratorCall is DynArrayIterator or CompositeSymbolRef { InnerSymbol: DynArrayIterator })
            {
                WriteOpCode(OpCodes.DynArrayIterator);
            }
            else
            {
                WriteOpCode(OpCodes.Iterator);
            }

            inIteratorCall = true;
            Emit(node.IteratorCall, usop);
            inIteratorCall = false;
            var loopSkip = iteratorCallSkip;
            var endJump = WriteJumpPlaceholder(JumpType.Break);
            Nests.Push(new Nest(NestType.ForEach));
            Emit(node.Body, usop);
            Nest nest = Nests.Pop();
            WriteOpCode(OpCodes.IteratorNext);
            endJump.End();
            nest.SetPositionFor(JumpType.Break);
            if (Game <= MEGame.ME2)
            {
                loopSkip?.End();
                WriteOpCode(OpCodes.IteratorPop);
            }
            else
            {
                WriteOpCode(OpCodes.IteratorPop);
                loopSkip?.End();
            }
            return true;
        }

        public bool VisitNode(WhileLoop node, UnrealScriptOptionsPackage usop)
        {
            var loopStartPos = Position;
            JumpPlaceholder endJump = EmitConditionalJump(node.Condition, usop);
            Nests.Push(new Nest(NestType.While));
            Emit(node.Body, usop);
            Nest nest = Nests.Pop();
            nest.SetPositionFor(JumpType.Continue);
            WriteOpCode(OpCodes.Jump);
            WriteUShort(loopStartPos);
            endJump.End();
            nest.SetPositionFor(JumpType.Break);
            return true;
        }

        public bool VisitNode(SwitchStatement node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Switch);
            EmitVariableSize(node.Expression, usop);
            Emit(node.Expression, usop);
            Nests.Push(new Nest(NestType.Switch) { SwitchType = node.Expression.ResolveType() });
            Emit(node.Body, usop);
            Nest nest = Nests.Pop();
            nest.CaseJumpPlaceholder?.End();
            nest.SetPositionFor(JumpType.Break);
            return true;
        }

        public bool VisitNode(CaseStatement node, UnrealScriptOptionsPackage usop)
        {
            Nest nest = Nests.Peek();
            nest.CaseJumpPlaceholder?.End();
            WriteOpCode(OpCodes.Case);
            nest.CaseJumpPlaceholder = WriteJumpPlaceholder(JumpType.Case);
            Emit(AddConversion(nest.SwitchType, node.Value), usop);
            return true;
        }

        public bool VisitNode(DefaultCaseStatement node, UnrealScriptOptionsPackage usop)
        {
            Nest nest = Nests.Peek();
            nest.CaseJumpPlaceholder?.End();
            nest.CaseJumpPlaceholder = null;
            WriteOpCode(OpCodes.Case);
            WriteUShort(0xFFFF);
            return true;
        }

        public bool VisitNode(AssignStatement node, UnrealScriptOptionsPackage usop)
        {
            VariableType targetType = node.Target.ResolveType();
            if (targetType == SymbolTable.BoolType)
            {
                WriteOpCode(OpCodes.LetBool);
            }
            else if (targetType is DelegateType)
            {
                WriteOpCode(OpCodes.LetDelegate);
            }
            else
            {
                WriteOpCode(OpCodes.Let);
            }

            inAssignTarget = true;
            Emit(node.Target, usop);
            inAssignTarget = false;
            Emit(AddConversion(targetType, node.Value), usop);
            return true;
        }

        public bool VisitNode(AssertStatement node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Assert);
            //why you would have a source file longer than 65,535 lines I truly do not know
            //better for it to emit an incorrect line number in the assert than to crash the compiler though
            unchecked
            {
                WriteUShort((ushort)CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos));
            }
            WriteByte(0);//bool debug mode - true: crash, false: log warning
            Emit(node.Condition, usop);
            return true;
        }

        public bool VisitNode(BreakStatement node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Jump);
            Nests.Peek().Add(WriteJumpPlaceholder(JumpType.Break));
            return true;
        }

        public bool VisitNode(ContinueStatement node, UnrealScriptOptionsPackage usop)
        {
            Nest nest = Nests.First(n => n.Type.Has(NestType.Loop));
            if (nest.Type == NestType.ForEach)
            {
                WriteOpCode(OpCodes.IteratorNext);
                WriteOpCode(OpCodes.Jump);
                nest.Add(WriteJumpPlaceholder(JumpType.Break));
            }
            else
            {
                WriteOpCode(OpCodes.Jump);
                nest.Add(WriteJumpPlaceholder(JumpType.Continue));
            }

            return true;
        }

        public bool VisitNode(IfStatement node, UnrealScriptOptionsPackage usop)
        {
            JumpPlaceholder jumpToElse = EmitConditionalJump(node.Condition, usop);
            Emit(node.Then, usop);
            JumpPlaceholder jumpPastElse = null;
            if (node.Else?.Statements.Any() == true)
            {
                WriteOpCode(OpCodes.Jump);
                jumpPastElse = WriteJumpPlaceholder();
                jumpToElse.End();
                Emit(node.Else, usop);
            }
            else
            {
                jumpToElse.End();
            }
            jumpPastElse?.End();
            return true;
        }

        private JumpPlaceholder EmitConditionalJump(Expression condition, UnrealScriptOptionsPackage usop)
        {
            JumpPlaceholder jump;
            if (Game.IsGame3() && condition is InOpReference inOp && inOp.LeftOperand.ResolveType()?.PropertyType == EPropertyType.Object
                                                                      && inOp.LeftOperand.GetType() == typeof(SymbolReference) && inOp.RightOperand is NoneLiteral
                                                                      && inOp.Operator.OperatorType is TokenType.Equals or TokenType.NotEquals)
            {
                var symRef = (SymbolReference)inOp.LeftOperand;
                WriteOpCode(symRef.Node.Outer is Function ? OpCodes.OptIfLocal : OpCodes.OptIfInstance);
                WriteObjectRef(ResolveSymbol(symRef.Node, usop));
                WriteByte((byte)(inOp.Operator.OperatorType is TokenType.NotEquals ? 1 : 0));
                jump = WriteJumpPlaceholder(JumpType.Conditional);
            }
            else if (Game.IsGame3() && condition is PreOpReference preOp && preOp.Operator.OperatorType is TokenType.ExclamationMark
                                                                             && preOp.Operand.GetType() == typeof(SymbolReference) && preOp.Operand.ResolveType() == SymbolTable.BoolType)
            {
                var symRef = (SymbolReference)preOp.Operand;
                WriteOpCode(symRef.Node.Outer is Function ? OpCodes.OptIfLocal : OpCodes.OptIfInstance);
                WriteObjectRef(ResolveSymbol(symRef.Node, usop));
                WriteByte(0);
                jump = WriteJumpPlaceholder(JumpType.Conditional);
            }
            else if (Game.IsGame3() && condition is SymbolReference sr && sr.GetType() == typeof(SymbolReference) && sr.ResolveType() == SymbolTable.BoolType)
            {
                WriteOpCode(sr.Node.Outer is Function ? OpCodes.OptIfLocal : OpCodes.OptIfInstance);
                WriteObjectRef(ResolveSymbol(sr.Node, usop));
                WriteByte(1);
                jump = WriteJumpPlaceholder(JumpType.Conditional);
            }
            else
            {
                WriteOpCode(OpCodes.JumpIfNot);
                jump = WriteJumpPlaceholder(JumpType.Conditional);
                Emit(condition, usop);
            }

            return jump;
        }

        public bool VisitNode(ReturnStatement node, UnrealScriptOptionsPackage usop)
        {
            foreach (Nest nest in Nests)
            {
                if (nest.Type is NestType.ForEach)
                {
                    WriteOpCode(OpCodes.IteratorPop);
                }
            }
            WriteOpCode(OpCodes.Return);
            if (node.Value is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(AddConversion(((Function)CompilationUnit).ReturnType, node.Value), usop);
            }

            return true;
        }

        public bool VisitNode(StopStatement node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Stop);
            return true;
        }

        public bool VisitNode(StateGoto node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.GotoLabel);
            Emit(node.LabelExpression, usop);
            return true;
        }

        public bool VisitNode(Goto node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Jump);
            LabelJumps.AddToListAt(node.Label, WriteJumpPlaceholder());
            return true;
        }

        public bool VisitNode(ExpressionOnlyStatement node, UnrealScriptOptionsPackage usop)
        {
            if (NeedsToEatReturnValue(node.Value, out IEntry returnProp, usop))
            {
                WriteOpCode(OpCodes.EatReturnValue);
                WriteObjectRef(returnProp);
            }
            Emit(node.Value, usop);
            return true;
        }

        private bool NeedsToEatReturnValue(Expression expr, [NotNullWhen(true)] out IEntry returnProp, UnrealScriptOptionsPackage usop)
        {
            while (expr is CompositeSymbolRef csr)
            {
                expr = csr.InnerSymbol;
            }
            if (expr is DynArraySort dynArrayOp)
            {
                expr = dynArrayOp.DynArrayExpression;
                while (expr is CompositeSymbolRef csr)
                {
                    expr = csr.InnerSymbol;
                }
                if (GetAffector(expr) is Function func)
                {
                    if (func.RetValNeedsDestruction)
                    {
                        returnProp = Pcc.getEntryOrAddImport($"{ResolveReturnValue(func, usop).InstancedFullPath}.ReturnValue", PropertyTypeName(((DynamicArrayType)func.ReturnType).ElementType));
                        return true;
                    }
                    returnProp = null;
                    return false;
                }
                if (expr is SymbolReference { Node: VariableDeclaration varDecl })
                {
                    if (varDecl.Flags.Has(EPropertyFlags.NeedCtorLink) || varDecl.VarType.Size(Game) > 64)
                    {
                        returnProp = Pcc.getEntryOrAddImport($"{ResolveProperty(varDecl, usop).InstancedFullPath}.{varDecl.Name}", PropertyTypeName(((DynamicArrayType)varDecl.VarType).ElementType));
                        return true;
                    }
                }
                throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(expr.StartPos)}: Cannot resolve property for dynamic array sort! Please report this error to LEX devs.");
            }
            else if (expr switch
            {
                DelegateCall delegateCall => delegateCall.DefaultFunction,
                FunctionCall functionCall => (Function)functionCall.Function.Node,
                InOpReference inOpReference => inOpReference.Operator.Implementer,
                PreOpReference preOpReference => preOpReference.Operator.Implementer,
                _ => null
            } is { RetValNeedsDestruction: true } func)
            {
                returnProp = ResolveReturnValue(func, usop);
                return true;
            }
            returnProp = null;
            return false;
        }

        public bool VisitNode(ReplicationStatement node, UnrealScriptOptionsPackage usop)
        {
            foreach (SymbolReference varRef in node.ReplicatedVariables)
            {
                if (varRef.Node is not VariableDeclaration varDecl)
                {
                    throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(varRef.StartPos)}: '{varRef.Name}' was not resolved to a variable! Please report this error to LEX devs.");
                }
                varDecl.ReplicationOffset = Position;
            }
            Emit(node.Condition, usop);
            return true;
        }

        public bool VisitNode(ErrorStatement node, UnrealScriptOptionsPackage usop)
        {
            //an ast with errors should never be passed to the compiler
            throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos)}: Cannot compile an error!");
        }

        public bool VisitNode(ErrorExpression node, UnrealScriptOptionsPackage usop)
        {
            //an ast with errors should never be passed to the compiler
            throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos)}: Cannot compile an error!");
        }

        private static Function GetAffector(Expression expr) =>
            expr switch
            {
                DelegateCall delegateCall => delegateCall.DefaultFunction,
                FunctionCall functionCall => (Function)functionCall.Function.Node,
                InOpReference inOpReference => inOpReference.Operator.Implementer,
                PreOpReference preOpReference => preOpReference.Operator.Implementer,
                CompositeSymbolRef compositeSymbolRef => GetAffector(compositeSymbolRef.InnerSymbol),
                _ => null
            };

        public bool VisitNode(DynArrayIterator node, UnrealScriptOptionsPackage usop)
        {
            Emit(node.DynArrayExpression, usop);
            InContext.Push(null);
            Emit(node.ValueArg, usop);
            if (node.IndexArg is null)
            {
                WriteByte(0);
                WriteOpCode(OpCodes.EmptyParmValue);
            }
            else
            {
                WriteByte(1);
                Emit(node.IndexArg, usop);
            }
            InContext.Pop();

            return true;
        }

        public bool VisitNode(InOpReference node, UnrealScriptOptionsPackage usop)
        {
            InOpDeclaration op = node.Operator;
            if (op.NativeIndex > 0)
            {
                WriteNativeOpCode(op.NativeIndex);
            }
            else
            {
                WriteOpCode(OpCodes.FinalFunction);
                WriteObjectRef(ResolveFunction(op.Implementer, usop));
            }

            VariableType lType = node.Operator.LeftOperand.VarType;
            VariableType rType = node.Operator.RightOperand.VarType;
            if (node.Operator.LeftOperand.VarType is Class { IsInterface: true } c)
            {
                lType = rType = node.LeftOperand.ResolveType() ?? node.RightOperand.ResolveType() ?? c;
            }

            if (op.LeftOperand.IsOut)
            {
                inAssignTarget = true;
            }
            Emit(AddConversion(lType, node.LeftOperand), usop);
            inAssignTarget = false;
            SkipPlaceholder skip = null;
            if (op.RightOperand.Flags.Has(EPropertyFlags.SkipParm))
            {
                WriteOpCode(OpCodes.Skip);
                skip = WriteSkipPlaceholder();
            }

            Emit(AddConversion(rType, node.RightOperand), usop);
            WriteOpCode(OpCodes.EndFunctionParms);
            skip?.End();
            return true;

        }

        public bool VisitNode(PreOpReference node, UnrealScriptOptionsPackage usop)
        {
            WriteNativeOpCode(node.Operator.NativeIndex);
            Emit(node.Operand, usop);
            WriteOpCode(OpCodes.EndFunctionParms);
            return true;
        }

        public bool VisitNode(PostOpReference node, UnrealScriptOptionsPackage usop)
        {
            WriteNativeOpCode(node.Operator.NativeIndex);
            Emit(node.Operand, usop);
            WriteOpCode(OpCodes.EndFunctionParms);
            return true;
        }

        public bool VisitNode(StructComparison node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(node.IsEqual ? OpCodes.StructCmpEq : OpCodes.StructCmpNe);
            WriteObjectRef(ResolveStruct(node.Struct, usop));
            Emit(node.LeftOperand, usop);
            Emit(node.RightOperand, usop);
            return true;
        }

        public bool VisitNode(DelegateComparison node, UnrealScriptOptionsPackage usop)
        {
            useInstanceDelegate = true;
            WriteOpCode(node.RightOperand.ResolveType() is DelegateType { IsFunction: true }
                            ? node.IsEqual
                                ? OpCodes.EqualEqual_DelFunc
                                : OpCodes.NotEqual_DelFunc
                            : node.IsEqual
                                ? OpCodes.EqualEqual_DelDel
                                : OpCodes.NotEqual_DelDel);
            Emit(node.LeftOperand, usop);
            Emit(node.RightOperand, usop);
            WriteOpCode(OpCodes.EndFunctionParms);
            useInstanceDelegate = false;
            return true;
        }

        public bool VisitNode(NewOperator node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.New);
            if (node.OuterObject is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(node.OuterObject, usop);
            }
            if (node.ObjectName is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(node.ObjectName, usop);
            }
            if (node.Flags is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(node.Flags, usop);
            }
            if (node.ObjectClass is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(node.ObjectClass, usop);
            }

            if (node.Template is null)
            {
                WriteOpCode(OpCodes.Nothing);
            }
            else
            {
                Emit(node.Template, usop);
            }
            return true;
        }

        public bool VisitNode(FunctionCall node, UnrealScriptOptionsPackage usop)
        {
            var func = (Function)node.Function.Node;
            if (func.NativeIndex > 0)
            {
                WriteNativeOpCode(func.NativeIndex);
            }
            else if (node.Function.IsGlobal)
            {
                WriteOpCode(OpCodes.GlobalFunction);
                WriteName(func.Name);
            }
            else if (func.Flags.Has(EFunctionFlags.Final) || node.Function.IsSuper)
            {
                WriteOpCode(OpCodes.FinalFunction);
                WriteObjectRef(ResolveFunction(func, usop));
            }
            else if (!Game.IsGame3() || node.IsCalledOnInterface || func.Outer is State)
            {
                WriteOpCode(OpCodes.VirtualFunction);
                WriteName(func.Name);
            }
            else
            {
                WriteOpCode(OpCodes.NamedFunction);
                WriteName(func.Name);
                if (NodeUtils.GetContainingClass(func).VirtualFunctionNames.IndexOf(func.Name) is var idx and >= 0)
                {
                    WriteUShort((ushort)idx);
                }
                else
                {
                    throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos)}: Could not find '{func.Name}' in #{ContainingClass.UIndex} {ContainingClass.ObjectName}'s Virtual Function Table!");
                }
            }
            InContext.Push(null);
            CompileArguments(node.Arguments, func.Parameters, usop);
            InContext.Pop();
            return true;
        }

        public bool VisitNode(DelegateCall node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DelegateFunction);
            var varDecl = (VariableDeclaration)node.DelegateReference.Node;
            if (varDecl.Outer is Function)
            {
                WriteByte(1);
            }
            else
            {
                WriteByte(0);
            }
            WriteObjectRef(ResolveSymbol(varDecl, usop));
            WriteName(node.DefaultFunction.Name);
            InContext.Push(null);
            CompileArguments(node.Arguments, node.DefaultFunction.Parameters, usop);
            InContext.Pop();
            return true;
        }

        private void CompileArguments(List<Expression> args, List<FunctionParameter> funcParams, UnrealScriptOptionsPackage usop)
        {
            for (int i = 0; i < args.Count; i++)
            {
                Expression arg = args[i];
                if (arg is null)
                {
                    WriteOpCode(OpCodes.EmptyParmValue);
                }
                else
                {
                    FunctionParameter funcParam = funcParams[i];
                    if (funcParam.IsOut)
                    {
                        inAssignTarget = true;
                    }
                    Emit(AddConversion(funcParam.VarType, arg), usop);
                    inAssignTarget = false;
                }
            }

            WriteOpCode(OpCodes.EndFunctionParms);
        }

        public bool VisitNode(ArraySymbolRef node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(node.IsDynamic ? OpCodes.DynArrayElement : OpCodes.ArrayElement);
            InContext.Push(null);
            Emit(node.Index, usop);
            InContext.Pop();
            Emit(node.Array, usop);
            return true;
        }

        public bool VisitNode(CompositeSymbolRef node, UnrealScriptOptionsPackage usop)
        {
            bool forEachJump = inIteratorCall;
            inIteratorCall = false;
            Expression innerSymbol = node.InnerSymbol;
            if (node.IsStructMemberExpression)
            {
                if (innerSymbol.GetType() == typeof(SymbolReference) && node.Node is VariableDeclaration decl && decl.VarType == SymbolTable.BoolType)
                {
                    WriteOpCode(OpCodes.BoolVariable);
                }
                WriteOpCode(OpCodes.StructMember);
                WriteObjectRef(ResolveSymbol(node.Node, usop));
                WriteObjectRef(ResolveStruct((Struct)node.OuterSymbol.ResolveType(), usop));

                switch (node.OuterSymbol)
                {
                    //struct is being accessed through an rvalue
                    case FunctionCall:
                    case DelegateCall:
                    //case ArraySymbolRef: doesn't seem to count as an rvalue for dynamic arrays. does it for static arrays?
                    case CompositeSymbolRef csr when ContainsFunctionCall(csr):
                    case VectorLiteral:
                    case RotatorLiteral:
                    case InOpReference:
                    case PreOpReference:
                    case PostOpReference:
                        WriteByte(1);
                        break;
                    default:
                        WriteByte(0);
                        break;
                }

                //this is being modified, and this is the base struct.
                if (inAssignTarget && node.OuterSymbol is not CompositeSymbolRef { IsStructMemberExpression: true })
                {
                    WriteByte(1);
                }
                else
                {
                    WriteByte(0);
                }
                Emit(node.OuterSymbol, usop);
                return true;
            }

            //inAssignTarget = false;
            WriteOpCode(node.IsClassContext ? OpCodes.ClassContext : OpCodes.Context);
            if (node.OuterSymbol.ResolveType().PropertyType == EPropertyType.Interface)
            {
                WriteOpCode(OpCodes.InterfaceContext);
            }
            if (InContext.Peek() is NestedContextFlag flag)
            {
                flag.HasNestedContext = true;
            }

            InContext.Push(new NestedContextFlag());
            Emit(node.OuterSymbol, usop);
            bool hasInnerContext = InContext.Pop().HasNestedContext;
            SkipPlaceholder skip = WriteSkipPlaceholder();
            EmitVariableSize(innerSymbol, usop);

            skip.ResetStart();

            Emit(innerSymbol, usop);
            if (forEachJump)
            {
                //todo: not correct
                iteratorCallSkip = skip;
            }
            else
            {
                if (node.IsClassContext || hasInnerContext || Game.IsLEGame())
                {
                    skip.End();
                }
                else
                {
                    skip.SetExplicit((ushort)(Game >= MEGame.ME3 ? 9 : 5));
                }
            }
            return true;

            static bool ContainsFunctionCall(CompositeSymbolRef csr)
            {
                while (csr != null)
                {
                    if (csr.InnerSymbol is FunctionCall or DelegateCall)
                    {
                        return true;
                    }
                    csr = csr.OuterSymbol as CompositeSymbolRef;
                }

                return false;
            }
        }

        public bool VisitNode(SymbolReference node, UnrealScriptOptionsPackage usop)
        {
            switch (node.Node)
            {
                case EnumValue enumVal:
                    WriteOpCode(OpCodes.ByteConst);
                    WriteByte(enumVal.IntVal);
                    return true;
                case Function func when useInstanceDelegate:
                    WriteOpCode(OpCodes.InstanceDelegate);
                    WriteName(func.Name);
                    return true;
                case Function func:
                    WriteOpCode(OpCodes.DelegateProperty);
                    WriteName(func.Name);
                    if (Game >= MEGame.ME3)
                    {
                        WriteObjectRef(func.Flags.Has(EFunctionFlags.Delegate) ? ResolveDelegateProperty(func, usop) : null);
                    }
                    return true;
            }

            var varDecl = (VariableDeclaration)node.Node;
            if (varDecl.Name == "Self")
            {
                WriteOpCode(OpCodes.Self);
                return true;
            }
            if (varDecl.VarType == SymbolTable.BoolType)
            {
                WriteOpCode(OpCodes.BoolVariable);
            }
            if (varDecl.Outer is Function)
            {
                if (varDecl.Flags.Has(EPropertyFlags.OutParm))
                {
                    WriteOpCode(OpCodes.LocalOutVariable);
                }
                else if (varDecl.IsStaticArray || !Game.IsGame3())
                {
                    WriteOpCode(OpCodes.LocalVariable);
                }
                else
                {
                    switch (varDecl.VarType.PropertyType)
                    {
                        case EPropertyType.Float:
                            WriteOpCode(OpCodes.LocalFloatVariable);
                            break;
                        case EPropertyType.Int:
                            WriteOpCode(OpCodes.LocalIntVariable);
                            break;
                        case EPropertyType.Byte:
                            WriteOpCode(OpCodes.LocalByteVariable);
                            break;
                        case EPropertyType.Object:
                            WriteOpCode(OpCodes.LocalObjectVariable);
                            break;
                        default:
                            WriteOpCode(OpCodes.LocalVariable);
                            break;
                    }
                }
            }
            else
            {
                if (varDecl.IsStaticArray || !Game.IsGame3())
                {
                    WriteOpCode(OpCodes.InstanceVariable);
                }
                else
                {
                    switch (varDecl.VarType.PropertyType)
                    {
                        case EPropertyType.Float:
                            WriteOpCode(OpCodes.InstanceFloatVariable);
                            break;
                        case EPropertyType.Int:
                            WriteOpCode(OpCodes.InstanceIntVariable);
                            break;
                        case EPropertyType.Byte:
                            WriteOpCode(OpCodes.InstanceByteVariable);
                            break;
                        case EPropertyType.Object:
                            WriteOpCode(OpCodes.InstanceObjectVariable);
                            break;
                        default:
                            WriteOpCode(OpCodes.InstanceVariable);
                            break;
                    }
                }
            }
            WriteObjectRef(ResolveSymbol(varDecl, usop));
            return true;
        }

        public bool VisitNode(DefaultReference node, UnrealScriptOptionsPackage usop)
        {
            if (node.Node is VariableDeclaration decl && decl.VarType == SymbolTable.BoolType)
            {
                WriteOpCode(OpCodes.BoolVariable);
            }
            WriteOpCode(OpCodes.DefaultVariable);
            WriteObjectRef(ResolveSymbol(node.Node, usop));
            return true;
        }

        public bool VisitNode(ConditionalExpression node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.Conditional);
            Emit(node.Condition, usop);
            using (WriteSkipPlaceholder())
            {
                Emit(node.TrueExpression, usop);
            }

            using (WriteSkipPlaceholder())
            {
                Emit(node.FalseExpression, usop);
            }

            return true;
        }

        public bool VisitNode(CastExpression node, UnrealScriptOptionsPackage usop)
        {
            if (node is PrimitiveCast prim)
            {
                if (prim.Cast == ECast.ObjectToInterface)
                {
                    WriteOpCode(OpCodes.InterfaceCast);
                    WriteObjectRef(CompilerUtils.ResolveClass((Class)node.CastType, Pcc, usop));
                }
                else
                {
                    WriteOpCode(OpCodes.PrimitiveCast);
                    WriteCast(prim.Cast);
                }
            }
            else if (node.CastType is ClassType clsType)
            {
                WriteOpCode(OpCodes.Metacast);
                WriteObjectRef(CompilerUtils.ResolveClass((Class)clsType.ClassLimiter, Pcc, usop));
            }
            else
            {
                WriteOpCode(OpCodes.DynamicCast);
                WriteObjectRef(CompilerUtils.ResolveClass((Class)node.CastType, Pcc, usop));
            }
            Emit(node.CastTarget, usop);
            return true;
        }


        public bool VisitNode(DynArrayLength node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayLength);
            Emit(node.DynArrayExpression, usop);
            return true;
        }

        public bool VisitNode(DynArrayAdd node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayAdd);
            Emit(node.DynArrayExpression, usop);
            Emit(AddConversion(SymbolTable.IntType, node.CountArg), usop);
            if (Game >= MEGame.ME3)
            {
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            return true;
        }

        public bool VisitNode(DynArrayAddItem node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayAddItem);
            Emit(node.DynArrayExpression, usop);
            SkipPlaceholder placeholder = null;
            if (Game >= MEGame.ME2)
            {
                placeholder = WriteSkipPlaceholder();
            }
            DynamicArrayType dynArrType = (DynamicArrayType)node.DynArrayExpression.ResolveType();
            Emit(AddConversion(dynArrType.ElementType, node.ValueArg), usop);
            if (Game >= MEGame.ME3)
            {
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            placeholder?.End();

            return true;
        }

        public bool VisitNode(DynArrayInsert node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayInsert);
            Emit(node.DynArrayExpression, usop);
            Emit(AddConversion(SymbolTable.IntType, node.IndexArg), usop);
            Emit(AddConversion(SymbolTable.IntType, node.CountArg), usop);
            if (Game >= MEGame.ME3)
            {
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            return true;
        }

        public bool VisitNode(DynArrayInsertItem node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayInsertItem);
            Emit(node.DynArrayExpression, usop);
            using (WriteSkipPlaceholder())
            {
                DynamicArrayType dynArrType = (DynamicArrayType)node.DynArrayExpression.ResolveType();
                Emit(AddConversion(SymbolTable.IntType, node.IndexArg), usop);
                Emit(AddConversion(dynArrType.ElementType, node.ValueArg), usop);
                if (Game >= MEGame.ME3)
                {
                    WriteOpCode(OpCodes.EndFunctionParms);
                }
            }
            return true;
        }

        public bool VisitNode(DynArrayRemove node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayRemove);
            Emit(node.DynArrayExpression, usop);
            Emit(AddConversion(SymbolTable.IntType, node.IndexArg), usop);
            Emit(AddConversion(SymbolTable.IntType, node.CountArg), usop);
            if (Game >= MEGame.ME3)
            {
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            return true;
        }

        public bool VisitNode(DynArrayRemoveItem node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayRemoveItem);
            Emit(node.DynArrayExpression, usop);
            SkipPlaceholder placeholder = null;
            if (Game >= MEGame.ME2)
            {
                placeholder = WriteSkipPlaceholder();
            }
            DynamicArrayType dynArrType = (DynamicArrayType)node.DynArrayExpression.ResolveType();
            Emit(AddConversion(dynArrType.ElementType, node.ValueArg), usop);
            if (Game >= MEGame.ME3)
            {
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            placeholder?.End();
            return true;
        }

        public bool VisitNode(DynArrayFind node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayFind);
            Emit(node.DynArrayExpression, usop);
            using (WriteSkipPlaceholder())
            {
                DynamicArrayType dynArrType = (DynamicArrayType)node.DynArrayExpression.ResolveType();
                Emit(AddConversion(dynArrType.ElementType, node.ValueArg), usop);
                if (Game >= MEGame.ME3)
                {
                    WriteOpCode(OpCodes.EndFunctionParms);
                }
            }
            return true;
        }

        public bool VisitNode(DynArrayFindStructMember node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArrayFindStruct);
            Emit(node.DynArrayExpression, usop);
            using (WriteSkipPlaceholder())
            {
                Emit(node.MemberNameArg, usop);
                Emit(AddConversion(node.MemberType, node.ValueArg), usop);
                if (Game >= MEGame.ME3)
                {
                    WriteOpCode(OpCodes.EndFunctionParms);
                }
            }
            return true;
        }

        public bool VisitNode(DynArraySort node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.DynArraySort);
            Emit(node.DynArrayExpression, usop);
            using (WriteSkipPlaceholder())
            {
                Emit(node.CompareFuncArg, usop);
                WriteOpCode(OpCodes.EndFunctionParms);
            }
            return true;
        }


        public bool VisitNode(Label node, UnrealScriptOptionsPackage usop)
        {
            node.StartOffset = Position;
            return true;
        }

        public bool VisitNode(BooleanLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(node.Value ? OpCodes.True : OpCodes.False);
            return true;
        }

        public bool VisitNode(FloatLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.FloatConst);
            WriteFloat(node.Value);
            return true;
        }

        public bool VisitNode(IntegerLiteral node, UnrealScriptOptionsPackage usop)
        {
            int i = node.Value;
            if (node.NumType != Keywords.INT && i is >= 0 and < 265)
            {
                WriteOpCode(OpCodes.ByteConst);
                WriteByte((byte)i);
            }
            else switch (i)
                {
                    case 0:
                        WriteOpCode(OpCodes.IntZero);
                        break;
                    case 1:
                        WriteOpCode(OpCodes.IntOne);
                        break;
                    case >= 0 and < 256:
                        WriteOpCode(OpCodes.IntConstByte);
                        WriteByte((byte)i);
                        break;
                    default:
                        WriteOpCode(OpCodes.IntConst);
                        WriteInt(i);
                        break;
                }

            return true;
        }

        public bool VisitNode(NameLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.NameConst);
            WriteName(node.Value);
            return true;
        }

        public bool VisitNode(StringLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.StringConst);
            WriteBytes(node.Value.Select(c => (byte)c).ToArray());
            WriteByte(0);
            return true;
        }

        public bool VisitNode(StringRefLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.StringRefConst);
            WriteInt(node.Value);
            return true;
        }

        public bool VisitNode(ObjectLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.ObjectConst);
            if (node.Class is ClassType clsType)
            {
                WriteObjectRef(CompilerUtils.ResolveClass((Class)clsType.ClassLimiter, Pcc, usop));
            }
            else
            {
                IEntry entry = ResolveObject($"{ContainingClass.InstancedFullPath}.{node.Name.Value}") ?? ResolveObject(node.Name.Value) ?? usop.MissingObjectResolver?.Invoke(Pcc, node.Name.Value);
                if (entry is null)
                {
                    throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos)}: Could not find '{node.Name.Value}' in {Pcc.FilePath}!");
                }

                if (!entry.ClassName.CaseInsensitiveEquals(node.Class.Name))
                {
                    throw new Exception($"Line {CompilationUnit.Tokens.LineLookup.GetLineFromCharIndex(node.StartPos)}: Expected '{node.Name.Value}' to be a '{node.Class.Name}'!");
                }
                WriteObjectRef(entry);
            }

            return true;
        }

        public bool VisitNode(VectorLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.VectorConst);
            WriteFloat(node.X);
            WriteFloat(node.Y);
            WriteFloat(node.Z);
            return true;
        }

        public bool VisitNode(RotatorLiteral node, UnrealScriptOptionsPackage usop)
        {
            WriteOpCode(OpCodes.RotationConst);
            WriteInt(node.Pitch);
            WriteInt(node.Yaw);
            WriteInt(node.Roll);
            return true;
        }

        public bool VisitNode(NoneLiteral node, UnrealScriptOptionsPackage usop)
        {
            if (node.IsDelegateNone)
            {
                if (useInstanceDelegate)
                {
                    WriteOpCode(OpCodes.EmptyDelegate);
                }
                else
                {
                    WriteOpCode(OpCodes.DelegateProperty);
                    WriteName("None");
                    WriteObjectRef(null);
                }
            }
            else
            {
                WriteOpCode(OpCodes.NoObject);
            }
            return true;
        }

        //TODO: remove? alreaty done in parser. doing again should have no effect
        static Expression AddConversion(VariableType destType, Expression expr)
        {

            if (expr is NoneLiteral noneLit)
            {
                if (destType.PropertyType == EPropertyType.Delegate)
                {
                    noneLit.IsDelegateNone = true;
                }
                else if (destType.PropertyType == EPropertyType.Interface)
                {
                    return new PrimitiveCast(ECast.ObjectToInterface, destType, noneLit, noneLit.StartPos, noneLit.EndPos);
                }
            }
            else if (expr?.ResolveType() is { } type && type.PropertyType != destType.PropertyType)
            {
                ECast cast = CastHelper.PureCastType(CastHelper.GetConversion(destType, type));
                switch (expr)
                {
                    case IntegerLiteral intLit:
                        switch (cast)
                        {
                            case ECast.ByteToInt:
                                intLit.NumType = Keywords.INT;
                                return intLit;
                            case ECast.IntToByte:
                                intLit.NumType = Keywords.BYTE;
                                return intLit;
                            case ECast.IntToFloat:
                                return new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
                            case ECast.ByteToFloat:
                                return new FloatLiteral(intLit.Value, intLit.StartPos, intLit.EndPos);
                        }
                        break;
                    case FloatLiteral floatLit:
                        switch (cast)
                        {
                            case ECast.FloatToByte:
                                return new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = Keywords.BYTE };
                            case ECast.FloatToInt:
                                return new IntegerLiteral((int)floatLit.Value, floatLit.StartPos, floatLit.EndPos) { NumType = Keywords.INT };
                        }
                        break;
                    case ConditionalExpression condExpr:
                        //TODO: create new ConditionalExpression to avoid modifying original?
                        condExpr.TrueExpression = AddConversion(destType, condExpr.TrueExpression);
                        condExpr.FalseExpression = AddConversion(destType, condExpr.FalseExpression);
                        return condExpr;
                }
                if ((byte)cast != 0 && cast != ECast.Max)
                {
                    return new PrimitiveCast(cast, destType, expr, expr.StartPos, expr.EndPos);
                }
            }

            return expr;
        }

        private readonly Dictionary<ASTNode, IEntry> resolveSymbolCache = new();

        private IEntry ResolveSymbol(ASTNode node, UnrealScriptOptionsPackage usop)
        {
            switch (node)
            {
                case FunctionParameter param:
                    return parameters[param.Name];
                case VariableDeclaration { Outer: Function } local:
                    return locals[local.Name];
            }
            if (resolveSymbolCache.TryGetValue(node, out IEntry resolvedEntry))
            {
                return resolvedEntry;
            }
            resolvedEntry = node switch
            {
                Class cls => CompilerUtils.ResolveClass(cls, Pcc, usop),
                Struct strct => ResolveStruct(strct, usop),
                State state => ResolveState(state, usop),
                Function func => ResolveFunction(func, usop),
                VariableDeclaration field => ResolveProperty(field, usop),
                SymbolReference symRef => ResolveSymbol(symRef.Node, usop),
                _ => throw new ArgumentOutOfRangeException(nameof(node))
            };
            resolveSymbolCache[node] = resolvedEntry;
            return resolvedEntry;
        }

        private IEntry ResolveProperty(VariableDeclaration decl, UnrealScriptOptionsPackage usop)
        {
            return Pcc.getEntryOrAddImport($"{ResolveSymbol(decl.Outer, usop).InstancedFullPath}.{decl.Name}", PropertyTypeName(decl.VarType));
        }

        private IEntry ResolveStruct(Struct s, UnrealScriptOptionsPackage usop) => Pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, usop).InstancedFullPath}.{s.Name}", "ScriptStruct");

        private IEntry ResolveFunction(Function f, UnrealScriptOptionsPackage usop) => Pcc.getEntryOrAddImport($"{ResolveSymbol(f.Outer, usop).InstancedFullPath}.{f.Name}", "Function");

        private IEntry ResolveDelegateProperty(Function f, UnrealScriptOptionsPackage usop) => Pcc.getEntryOrAddImport($"{ResolveSymbol(f.Outer, usop).InstancedFullPath}.__{f.Name}__Delegate", "DelegateProperty");

        private IEntry ResolveReturnValue(Function f, UnrealScriptOptionsPackage usop) => f.ReturnType is null ? null : Pcc.getEntryOrAddImport($"{ResolveFunction(f, usop).InstancedFullPath}.ReturnValue", PropertyTypeName(f.ReturnType));

        private IEntry ResolveState(State s, UnrealScriptOptionsPackage usop) => Pcc.getEntryOrAddImport($"{ResolveSymbol(s.Outer, usop).InstancedFullPath}.{s.Name}", "State");

        private IEntry ResolveObject(string instancedFullPath) => Pcc.FindEntry(instancedFullPath);

        public static string PropertyTypeName(VariableType type) =>
            type switch
            {
                Class { IsComponent: true } => "ComponentProperty",
                Class { IsInterface: true } => "InterfaceProperty",
                Class _ => "ObjectProperty",
                Struct _ => "StructProperty",
                ClassType _ => "ClassProperty",
                DelegateType _ => "DelegateProperty",
                DynamicArrayType _ => "ArrayProperty",
                Enumeration _ => "ByteProperty",
                StaticArrayType staticArrayType => PropertyTypeName(staticArrayType.ElementType),
                _ when type == SymbolTable.FloatType => "FloatProperty",
                _ when type == SymbolTable.BoolType => "BoolProperty",
                _ when type == SymbolTable.BioMask4Type => "BioMask4Property",
                _ when type == SymbolTable.ByteType => "ByteProperty",
                _ when type == SymbolTable.IntType => "IntProperty",
                _ when type == SymbolTable.NameType => "NameProperty",
                _ when type == SymbolTable.StringRefType => "StringRefProperty",
                _ when type == SymbolTable.StringType => "StrProperty",
                _ => throw new ArgumentOutOfRangeException(nameof(type), type, "")
            };

        private void Emit(ASTNode node, UnrealScriptOptionsPackage usop)
        {
            node.AcceptVisitor(this, usop);
        }

        private void EmitVariableSize(Expression expr, UnrealScriptOptionsPackage usop)
        {
            if (Game <= MEGame.ME2)
            {
                VariableType exprType = expr.ResolveType();
                WriteByte(exprType switch
                {
                    null => 0,
                    { PropertyType: EPropertyType.StringRef } => 0,
                    _ => (byte)exprType.Size(Game)
                });
                return;
            }

            if (GetAffector(expr) is Function f)
            {
                if (Game >= MEGame.ME3)
                {
                    WriteObjectRef(ResolveReturnValue(f, usop));
                }

                WriteByte(0);
            }
            else if (expr is InOpReference or PreOpReference or PostOpReference)
            {
                if (Game >= MEGame.ME3)
                {
                    WriteObjectRef(null);
                }
                WriteByte(0);
            }
            else if (expr is PrimitiveCast p)
            {
                if (Game >= MEGame.ME3)
                {
                    WriteObjectRef(null);
                }
                WriteByte((byte)p.CastType.PropertyType);
            }
            else if (expr is SymbolReference { Node: Function })
            {
                if (Game >= MEGame.ME3)
                {
                    WriteObjectRef(null);
                }
                WriteByte((byte)EPropertyType.Delegate);
            }
            else
            {
                if (Game >= MEGame.ME3)
                {
                    WriteObjectRef(expr is DynArraySort or not DynArrayOperation ? ResolveSymbol(expr, usop) : null);
                }
                switch (expr)
                {
                    case DynArrayOperation dynOp when dynOp.ResolveType() == SymbolTable.IntType:
                        WriteByte((byte)EPropertyType.Int);
                        break;
                    default:
                        WriteByte(0);
                        break;
                }
            }
        }

        #region Not Bytecode

        public bool VisitNode(VariableType node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(StaticArrayType node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(DynamicArrayType node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(DelegateType node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(ClassType node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(EnumValue node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(ReturnNothingStatement node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Class node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Struct node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Enumeration node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Const node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Function node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(State node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(VariableDeclaration node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(FunctionParameter node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(DefaultPropertiesBlock node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(Subobject node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(VariableIdentifier node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(StructLiteral node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        public bool VisitNode(DynamicArrayLiteral node, UnrealScriptOptionsPackage usop)
        {
            throw new InvalidOperationException();
        }

        #endregion
    }
}
