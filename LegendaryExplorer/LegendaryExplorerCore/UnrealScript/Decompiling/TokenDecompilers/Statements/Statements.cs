using System;
using System.Collections.Generic;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.ByteCode;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;

namespace LegendaryExplorerCore.UnrealScript.Decompiling
{
    internal partial class ByteCodeDecompiler
    {
        private Statement DecompileStatement(ushort? startPosition = null)
        {
            StartPositions.Push(startPosition ?? (ushort)Position);
            var token = PeekByte;

            switch (token)
            {
                // return [expression];
                case (byte)OpCodes.Return:
                    return DecompileReturn();

                // switch (expression)
                case (byte)OpCodes.Switch:
                    return DecompileSwitch();

                // case expression :
                case (byte)OpCodes.Case:
                    return DecompileCase();

                // if (expression) // while / for / do until
                case (byte)OpCodes.JumpIfNot:
                {
                    PopByte();
                    var jump = new IfNotJump(ReadUInt16(), DecompileExpression(), Position - StartPositions.Peek());
                    StatementLocations.Add(StartPositions.Pop(), jump);
                    return jump;
                }

                case (byte)OpCodes.Jump:
                {
                    PopByte();
                    ushort jumpLoc = ReadUInt16();
                    if (PeekByte is (byte)OpCodes.StringConst)
                    {
                        int savedPos = Position;
                        var comment = new CommentStatement();
                        do
                        {
                            PopByte();
                            comment.CommentLines.Add(ReadNullTerminatedString());
                        } while (PeekByte is (byte)OpCodes.StringConst);

                        if (Position == jumpLoc)
                        {
                            StatementLocations.Add(StartPositions.Pop(), comment);
                            return comment;
                        }
                        //if the jump is not to the end of the strings, this is not a well-formed comment
                        //reset position and read as normal
                        Position = savedPos;
                    }
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
                case (byte)OpCodes.IteratorNext:
                {
                    PopByte();
                    var itNext = new ContinueStatement();
                    StatementLocations.Add(StartPositions.Pop(), itNext);
                    if (PopByte() != (byte)OpCodes.Jump)
                    {
                        return null; //there should always be a jump after an iteratornext that's not at the end of the loop
                    }
                    //skip the jump address
                    PopByte();
                    PopByte();
                    return itNext;
                }
                // break;
                case (byte)OpCodes.IteratorPop:
                {
                    PopByte();
                    return DecompileStatement(StartPositions.Pop());
                }
                // stop;
                case (byte)OpCodes.Stop:
                    PopByte();
                    var stopStatement = new StopStatement(-1, -1);
                    StatementLocations.Add(StartPositions.Pop(), stopStatement);
                    return stopStatement;

                // Goto label
                case (byte)OpCodes.GotoLabel:
                    PopByte();
                    var gotoLabel = new StateGoto(DecompileExpression());
                    StatementLocations.Add(StartPositions.Pop(), gotoLabel);
                    return gotoLabel;

                // assignable expression = expression;
                case (byte)OpCodes.Let:
                case (byte)OpCodes.LetBool:
                case (byte)OpCodes.LetDelegate:
                    return DecompileAssign();

                // [skip x bytes]
                case (byte)OpCodes.Skip: // TODO: this should never occur as statement, possibly remove?
                    PopByte();
                    ReadUInt16();
                    StartPositions.Pop();
                    return DecompileStatement();

                case (byte)OpCodes.Nothing:
                    PopByte();
                    StartPositions.Pop();
                    return DecompileStatement(); // TODO, should probably have a nothing expression or statement, this is ugly

                // foreach IteratorFunction(...)
                case (byte)OpCodes.Iterator:
                    return DecompileForEach();

                // foreach arrayName(valuevariable[, indexvariable])
                case (byte)OpCodes.DynArrayIterator:
                    return DecompileForEach(true);

                case (byte)OpCodes.LabelTable:
                    DecompileLabelTable();
                    StartPositions.Pop();
                    return DecompileStatement();

                case (byte)OpCodes.OptIfLocal:
                case (byte)OpCodes.OptIfInstance:
                {
                    PopByte();
                    IEntry obj = ReadObject();
                    var condition = new SymbolReference(null, obj.ObjectName.Instanced);
                    bool not = Convert.ToBoolean((byte) ReadByte());
                    if (obj.ClassName == "BoolProperty")
                    {
                        var ifJump = new IfNotJump(
                            ReadUInt16(), not ? (Expression)condition : new PreOpReference(new PreOpDeclaration(TokenType.ExclamationMark, SymbolTable.BoolType, 0, null), condition),
                            Position - StartPositions.Peek());
                        StatementLocations.Add(StartPositions.Pop(), ifJump);
                        return ifJump;
                    }

                    var nullJump = new NullJump(ReadUInt16(), condition, not)
                    {
                        SizeOfExpression = Position - StartPositions.Peek()
                    };
                    StatementLocations.Add(StartPositions.Pop(), nullJump);
                    return nullJump;
                }
                case (byte)OpCodes.FilterEditorOnly:
                {
                    PopByte();
                    var edFilter = new InEditorJump(ReadUInt16());
                    StatementLocations.Add(StartPositions.Pop(), edFilter);
                    return edFilter;
                }
                case (byte)OpCodes.EatReturnValue:
                    PopByte();
                    ReadObject();
                    return DecompileStatement(StartPositions.Pop());

                case (byte)OpCodes.Assert:
                    return DecompileAssert();
                default:
                    var expr = DecompileExpression();
                    if (expr != null)
                    {
                        var statement = new ExpressionOnlyStatement(expr);
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

        private ReturnStatement DecompileReturn()
        {
            PopByte();

            Expression expr = null;
            if (CurrentIs(OpCodes.ReturnNullValue))
            {
                PopByte();
                var retVal = ReadObject();
                var returnNothingStatement = new ReturnNothingStatement();
                StatementLocations.Add(StartPositions.Pop(), returnNothingStatement);
                return returnNothingStatement;
            }

            if(CurrentIs(OpCodes.Nothing))
            {
                PopByte();
            }
            else
            {
                expr = DecompileExpression();
                if (expr == null && PopByte() != (byte)OpCodes.Nothing)
                    return null; //ERROR ?
            }

            if (ReturnType is Enumeration enm && expr is IntegerLiteral {Value: >= 0} intLit && intLit.Value < enm.Values.Count)
            {
                expr = new CompositeSymbolRef(new SymbolReference(enm, enm.Name), new SymbolReference(null, enm.Values[intLit.Value].Name));
            }
            var statement = new ReturnStatement(expr);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        private Statement DecompileForEach(bool isDynArray = false)
        {
            PopByte();
            var scopeStatements = new List<Statement>();

            var iteratorFunc = DecompileExpression();
            if (iteratorFunc == null)
                return null;

            if (isDynArray)
            {
                Expression dynArrVar = DecompileExpression();
                bool hasIndex = Convert.ToBoolean(ReadByte());
                Expression dynArrIndex = DecompileExpression();
                iteratorFunc = new DynArrayIterator(iteratorFunc, dynArrVar, dynArrIndex);
            }

            var scopeEnd = ReadUInt16(); // MemOff
            ForEachScopes.Push(scopeEnd);

            Scopes.Add(scopeStatements);
            CurrentScope.Push(Scopes.Count - 1);
            IteratorNext finalIteratorNext = null;
            while (Position < Size)
            {
                if (CurrentIs(OpCodes.IteratorNext))
                {
                    PopByte(); // IteratorNext
                    if (PeekByte == (byte)OpCodes.IteratorPop)
                    {
                        StatementLocations[(ushort)(Position - 1)] = finalIteratorNext = new IteratorNext();
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

            var statement = new ForEachLoop(iteratorFunc, new CodeBody(scopeStatements))
            {
                iteratorPopPos = Position - 1
            };
            if (finalIteratorNext is not null) finalIteratorNext.Outer = statement;
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        private SwitchStatement DecompileSwitch()
        {
            PopByte();
            if (Game >= MEGame.ME3)
            {
                var objIndex = ReadObject();
            }
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

            var statement = new SwitchStatement(expr, null, -1, -1);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        private Statement DecompileCase()
        {
            PopByte();
            ushort offs = ReadUInt16(); // MemOff
            Statement statement;

            if (offs == (ushort)0xFFFF)
            {
                statement = new DefaultCaseStatement(-1, -1);
            }
            else 
            {
                var expr = DecompileExpression();
                if (expr == null)
                    return null; //ERROR ?

                statement = new CaseStatement(expr, -1, -1) { LocationOfNextCase = offs};
            }

            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        private AssignStatement DecompileAssign()
        {
            PopByte();

            var left = DecompileExpression();
            if (left == null)
                return null; //ERROR ?

            var right = DecompileExpression();
            if (right == null)
                return null; //ERROR ?
            ResolveEnumValues(ref left, ref right);
            var statement = new AssignStatement(left, right);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        private Statement DecompileAssert()
        {
            PopByte();
            ReadUInt16(); // source line
            ReadByte(); // in debug mode
            var expr = DecompileExpression();

            var statement = new AssertStatement(expr);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        #endregion
    }
}
