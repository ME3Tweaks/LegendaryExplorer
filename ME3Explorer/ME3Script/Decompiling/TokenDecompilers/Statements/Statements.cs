using ME3Script.Analysis.Visitors;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ME3Explorer;
using ME3Explorer.Packages;
using ME3Explorer.Unreal.BinaryConverters;
using ME3Script.Analysis.Symbols;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {
        public Statement DecompileStatement(ushort? startPosition = null)
        {
            StartPositions.Push(startPosition ?? (ushort)Position);
            var token = PeekByte;

            switch (token)
            {
                // return [expression];
                case (byte)StandardByteCodes.Return:
                    return DecompileReturn();

                // switch (expression)
                case (byte)StandardByteCodes.Switch:
                    return DecompileSwitch();

                // case expression :
                case (byte)StandardByteCodes.Case:
                    return DecompileCase();

                // if (expression) // while / for / do until
                case (byte)StandardByteCodes.JumpIfNot:
                {
                    PopByte();
                    var jump = new IfNotJump(ReadUInt16(), DecompileExpression(), Position - StartPositions.Peek());
                    StatementLocations.Add(StartPositions.Pop(), jump);
                    return jump;
                }

                case (byte)StandardByteCodes.Jump:
                {
                    PopByte();
                    ushort jumpLoc = ReadUInt16();
                    if (ForEachScopes.Count > 0 && jumpLoc == ForEachScopes.Peek())
                    {
                        var brk = new BreakStatement();
                        StatementLocations.Add(StartPositions.Pop(), brk);
                        return brk;
                    }
                    var jump = new UnconditionalJump(jumpLoc);
                    StatementLocations.Add(StartPositions.Pop(), jump);
                    return jump;
                }
                // continue (iterator)
                case (byte)StandardByteCodes.IteratorNext:
                {
                    PopByte();
                    var itNext = new ContinueStatement();
                    StatementLocations.Add(StartPositions.Pop(), itNext);
                    return itNext;
                }
                // break;
                case (byte)StandardByteCodes.IteratorPop:
                {
                    PopByte();
                    return DecompileStatement(StartPositions.Pop());
                }
                // stop;
                case (byte)StandardByteCodes.Stop:
                    PopByte();
                    var stopStatement = new StopStatement(null, null);
                    StatementLocations.Add(StartPositions.Pop(), stopStatement);
                    return stopStatement;

                // Goto label
                case (byte)StandardByteCodes.GotoLabel: //TODO: make got astnode
                    PopByte();
                    var labelExpr = DecompileExpression();
                    var func = new SymbolReference(null, "goto");
                    var call = new FunctionCall(func, new List<Expression> { labelExpr }, null, null);
                    var gotoLabel = new ExpressionOnlyStatement(call);
                    StatementLocations.Add(StartPositions.Pop(), gotoLabel);
                    return gotoLabel;

                // assignable expression = expression;
                case (byte)StandardByteCodes.Let:
                case (byte)StandardByteCodes.LetBool:
                case (byte)StandardByteCodes.LetDelegate:
                    return DecompileAssign();

                // [skip x bytes]
                case (byte)StandardByteCodes.Skip: // TODO: this should never occur as statement, possibly remove?
                    PopByte();
                    ReadUInt16();
                    StartPositions.Pop();
                    return DecompileStatement();

                case (byte)StandardByteCodes.Nothing:
                    PopByte();
                    StartPositions.Pop();
                    return DecompileStatement(); // TODO, should probably have a nothing expression or statement, this is ugly

                // foreach IteratorFunction(...)
                case (byte)StandardByteCodes.Iterator:
                    return DecompileForEach();

                // foreach arrayName(valuevariable[, indexvariable])
                case (byte)StandardByteCodes.DynArrayIterator:
                    return DecompileForEach(true);

                case (byte)StandardByteCodes.LabelTable:
                    DecompileLabelTable();
                    StartPositions.Pop();
                    return DecompileStatement();

                case (byte)StandardByteCodes.OptIfLocal:
                case (byte)StandardByteCodes.OptIfInstance:
                {
                    PopByte();
                    IEntry obj = ReadObject();
                    var condition = new SymbolReference(null, obj.ObjectName.Instanced);
                    bool not = Convert.ToBoolean(ReadByte());
                    if (obj.ClassName == "BoolProperty")
                    {
                        var ifJump = new IfNotJump(
                            ReadUInt16(), not ? (Expression)condition : new PreOpReference(new PreOpDeclaration("!", SymbolTable.BoolType, 0, null), condition),
                            Position - StartPositions.Peek());
                        StatementLocations.Add(StartPositions.Pop(), ifJump);
                        return ifJump;
                    }

                    var nullJump = new NullJump(ReadUInt16(), condition, not);
                    StatementLocations.Add(StartPositions.Pop(), nullJump);
                    return nullJump;
                }
                case (byte)StandardByteCodes.FilterEditorOnly:
                {
                    PopByte();
                    var edFilter = new InEditorJump(ReadUInt16());
                    StatementLocations.Add(StartPositions.Pop(), edFilter);
                    return edFilter;
                }
                case (byte)StandardByteCodes.EatReturnValue:
                    PopByte();
                    ReadObject();
                    return DecompileStatement(StartPositions.Pop());

                case (byte)StandardByteCodes.Assert:
                    return DecompileAssert();
                default:
                    var expr = DecompileExpression();
                    if (expr != null)
                    {
                        var statement = new ExpressionOnlyStatement(expr, null, null);
                        StatementLocations.Add(StartPositions.Pop(), statement);
                        return statement;
                    }

                    // ERROR!
                    return null;
            }
        }

        private void DecompileLabelTable()
        {
            PopByte();
            var name = ReadNameReference();
            var ofs = ReadUInt32();
            while (ofs != 0x0000FFFF) // ends with non-ref + max-offset
            {
                LabelTable.Add(new LabelTableEntry
                {
                    NameRef = name,
                    Offset = ofs
                });

                name = ReadNameReference();
                ofs = ReadUInt32();
            }
        }

        #region Decompilers
        public ReturnStatement DecompileReturn()
        {
            PopByte();

            Expression expr = null;
            if (CurrentIs(StandardByteCodes.ReturnNullValue))
            {
                PopByte();
                var retVal = ReadObject();
                var returnNothingStatement = new ReturnNothingStatement();
                StatementLocations.Add(StartPositions.Pop(), returnNothingStatement);
                return returnNothingStatement;
            }
            else if(CurrentIs(StandardByteCodes.Nothing))
            {
                PopByte();
            }
            else
            {
                expr = DecompileExpression();
                if (expr == null && PopByte() != (byte)StandardByteCodes.Nothing)
                    return null; //ERROR ?
            }

            var statement = new ReturnStatement(expr);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileForEach(bool isDynArray = false)
        {
            PopByte();
            var scopeStatements = new List<Statement>();

            var iteratorFunc = DecompileExpression();
            if (iteratorFunc == null)
                return null;

            Expression dynArrVar = null;
            Expression dynArrIndex = null;
            bool hasIndex = false;
            if (isDynArray)
            {
                dynArrVar = DecompileExpression();
                hasIndex = Convert.ToBoolean(ReadByte());
                dynArrIndex = DecompileExpression();
            }

            var scopeEnd = ReadUInt16(); // MemOff
            ForEachScopes.Push(scopeEnd);

            Scopes.Add(scopeStatements);
            CurrentScope.Push(Scopes.Count - 1);
            while (Position < Size)
            {
                if (CurrentIs(StandardByteCodes.IteratorNext))
                {
                    PopByte(); // IteratorNext
                    if (PeekByte == (byte)StandardByteCodes.IteratorPop)
                    {
                        StatementLocations[(ushort)(Position - 1)] = new IteratorNext();
                        StatementLocations[(ushort)Position] = new IteratorPop();
                        PopByte(); // IteratorPop
                        break;
                    }
                    Position--;
                }

                var current = DecompileStatement();
                if (current == null)
                    return null; // ERROR ?

                scopeStatements.Add(current);
            }
            CurrentScope.Pop();
            ForEachScopes.Pop();

            if (isDynArray)
            {
                iteratorFunc = new DynArrayIterator(iteratorFunc, dynArrVar, dynArrIndex);
            }

            var statement = new ForEachLoop(iteratorFunc, new CodeBody(scopeStatements));
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public SwitchStatement DecompileSwitch()
        {
            PopByte();
            var objIndex = ReadObject();
            var unknByte = ReadByte();
            var expr = DecompileExpression();
            //var scopeStatements = new List<Statement>();
            //ushort endOffset = 0; // set it at max to begin with, so we can begin looping

            //Scopes.Add(scopeStatements);
            //CurrentScope.Push(Scopes.Count - 1);
            //while (Position < endOffset && Position < Size)
            //{
            //    //if (CurrentIs(StandardByteCodes.Jump)) // break detected, save the endOffset
            //    //{                                    // executes for all occurences, to handle them all.
            //    //    StartPositions.Push((ushort)Position);
            //    //    PopByte();
            //    //    endOffset = ReadUInt16();
            //    //    var breakStatement = new BreakStatement();
            //    //    StatementLocations.Add(StartPositions.Pop(), breakStatement);
            //    //    scopeStatements.Add(breakStatement);
            //    //    continue;
            //    //}

            //    var current = DecompileStatement();
            //    if (current == null)
            //        return null; // ERROR ?
            //    if (scopeStatements.Count > 0 && current is UnconditionalJump uj)
            //    {
            //        endOffset = Math.Max(endOffset, uj.JumpLoc);
            //    }

            //    if (current is DefaultStatement && endOffset == 0xFFFF)
            //        break; // If no break was detected, we end the switch rather than include the rest of ALL code in the default.
            //    scopeStatements.Add(current);
            //}
            //CurrentScope.Pop();

            var statement = new SwitchStatement(expr, null, null, null);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileCase()
        {
            PopByte();
            var offs = ReadUInt16(); // MemOff
            Statement statement = null;

            if (offs == (ushort)0xFFFF)
            {
                statement = new DefaultCaseStatement(null, null);
            }
            else 
            {
                var expr = DecompileExpression();
                if (expr == null)
                    return null; //ERROR ?

                statement = new CaseStatement(expr, null, null);
            }

            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public AssignStatement DecompileAssign()
        {
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; //ERROR ?

            var right = DecompileExpression();
            if (right == null)
                return null; //ERROR ?

            var statement = new AssignStatement(left, right);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileJump()
        {
            PopByte();
            var jumpOffs = ReadUInt16(); // discard jump destination
            Statement statement;

            if (ForEachScopes.Count != 0 && jumpOffs == ForEachScopes.Peek()) // A jump to the IteratorPop of a ForEach means break afaik.
                statement = new BreakStatement(null, null);
            else
                statement = new ContinueStatement(null, null);

            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileAssert()
        {
            PopByte();
            ReadUInt16(); // source line
            ReadByte(); // in debug mode
            var expr = DecompileExpression();

            var statement = new AssertStatement(expr);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileIteratorPop()
        {
            PopByte();
            if (CurrentIs(StandardByteCodes.Return)) // Any return inside a ForEach seems to call IteratorPop before the return, maybe breaks the loop?
            {
                return DecompileReturn();
            }
            else
            {
                var statement = new BreakStatement(null, null);
                StatementLocations.Add(StartPositions.Pop(), statement);
                return statement;
            }
        }

        #endregion

        #region Unsupported Decompilers

        #endregion
    }
}
