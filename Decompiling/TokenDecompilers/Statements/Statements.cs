using ME3Data.Utility;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ME3Script.Decompiling
{
    public partial class ME3ByteCodeDecompiler
    {
        public Statement DecompileStatement()
        {
            StartPositions.Push((UInt16)Position);
            var token = CurrentByte;
            
            if (token >= 0x80) // native table
            {
                // TODO: native lookup
            }
            else if (token >= 0x71) // extended native table, 0x70 is unused
            {
                // TODO: build extended value, then native lookup
            }
            else 
                switch (token)
                {
                    // return [expression];
                    case (byte)StandardByteCodes.Return:
                        return DecompileReturn();
                        
                    // switch (expression)
                    case (byte)StandardByteCodes.Switch:
                        return DecompileSwitch();

                    // if (expression) // while / for / do until
                    case (byte)StandardByteCodes.JumpIfNot:
                        return DecompileConditionalJump();

                    // stop;
                    case (byte)StandardByteCodes.Stop:
                        PopByte();
                        var statement = new StopStatement(null, null);
                        StatementLocations.Add(StartPositions.Pop(), statement);
                        return statement;

                    // Goto label
                    case (byte)StandardByteCodes.GotoLabel:
                        // TODO
                        break;

                    // assignable expression = expression;
                    case (byte)StandardByteCodes.Let:
                    case (byte)StandardByteCodes.LetBool:
                    case (byte)StandardByteCodes.LetDelegate:
                        return DecompileAssign();

                    // [skip x bytes]
                    case (byte)StandardByteCodes.Skip:
                        PopByte();
                        ReadRawData(ReadUInt16());
                        StartPositions.Pop();
                        return DecompileStatement();

                    // foreach IteratorFunction(...)
                    case (byte)StandardByteCodes.Iterator:
                        // TODO
                        break;

                    // foreach arrayName(valuevariable[, indexvariable])
                    case (byte)StandardByteCodes.DynArrayIterator:
                        // TODO
                        break;

                    // TODO: 0x3B - 0x3E native calls
                    //TODO: unkn4F and GoW_DefaultValue ???
                    // TODO: 0x5A -> 0x65 ???

                    default:
                        var expr = DecompileExpression();
                        if (expr != null)
                            return new ExpressionOnlyStatement(null, null, expr);

                        // ERROR!
                        break;
                }

            return null;
        }

        public ReturnStatement DecompileReturn()
        {
            PopByte();
            var expr = DecompileExpression();
            if (expr == null && PopByte() != (byte)StandardByteCodes.Nothing)
                return null; //ERROR ?

            var statement = new ReturnStatement(null, null, expr);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public Statement DecompileConditionalJump() // TODO: guess for loop, probably requires a large restructure
        {
            PopByte();
            var scopeStartOffset = StartPositions.Peek();
            Statement statement = null;
            var afterScopeOffset = ReadUInt16();
            UInt16 scopeEndJmpOffset;
            bool hasElse = false;
            var conditional = DecompileExpression();
            var scopeStatements = new List<Statement>();

            if (afterScopeOffset < scopeStartOffset) // end of do_until detection
            {
                var outerScope = Scopes.Peek();
                var startStatement = StatementLocations[afterScopeOffset];
                var index = outerScope.IndexOf(startStatement);
                scopeStatements = new List<Statement>(outerScope.Skip(index));
                outerScope.RemoveRange(index, outerScope.Count - index);
                statement = new DoUntilLoop(conditional, new CodeBody(scopeStatements, null, null), null, null);
            }

            Scopes.Push(scopeStatements);
            while (Position < afterScopeOffset)
            {
                if (CurrentIs(StandardByteCodes.Jump))
                {
                    PopByte();
                    scopeEndJmpOffset = ReadUInt16();
                    if (scopeEndJmpOffset == scopeStartOffset)
                        statement = new WhileLoop(conditional, new CodeBody(scopeStatements, null, null), null, null);
                    else
                        hasElse = true;

                    break;
                }

                var current = DecompileStatement();
                if (current == null)
                    return null; // ERROR ?

                scopeStatements.Add(current);
            }
            Scopes.Pop();

            List<Statement> elseStatements = null;
            if (hasElse)
            {
                var endElseOffset = ReadUInt16();
                elseStatements = new List<Statement>();
                Scopes.Push(elseStatements);
                while (Position < afterScopeOffset)
                {
                    var current = DecompileStatement();
                    if (current == null)
                        return null; // ERROR ?

                    scopeStatements.Add(current);
                }
                Scopes.Pop();
            }

            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement 
                ?? new IfStatement(conditional, new CodeBody(scopeStatements, null, null),
                        null, null, new CodeBody(elseStatements, null, null));
        }

        public SwitchStatement DecompileSwitch()
        {
            PopByte();
            var objIndex = ReadIndex();
            var unknByte = ReadByte();
            var expr = DecompileExpression();
            var scopeStatements = new List<Statement>();
            UInt16 endOffset = 0xFFFF; // set it at max to begin with, so we can begin looping

            Scopes.Push(scopeStatements);
            while (Position < endOffset && Position < Size)
            {
                if (CurrentIs(StandardByteCodes.Jump)) // break detected, save the endOffset
                {                                    // executes for all occurences, to handle them all.
                    StartPositions.Push((UInt16)Position);
                    PopByte();
                    endOffset = ReadUInt16();
                    var breakStatement = new BreakStatement(null, null);
                    StatementLocations.Add(StartPositions.Pop(), breakStatement);
                    scopeStatements.Add(breakStatement);
                    continue;
                }

                var current = DecompileStatement();
                if (current == null)
                    return null; // ERROR ?
                
                scopeStatements.Add(current);
                if (current is DefaultStatement && endOffset == 0xFFFF)
                    break; // If no break was detected, we end the switch rather than include the rest of ALL code in the default.
            }
            Scopes.Pop();

            var statement = new SwitchStatement(expr, new CodeBody(scopeStatements, null, null), null, null);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }

        public AssignStatement DecompileAssign()
        {
            PopByte();

            var left = DecompileExpression();
            if (left == null || !typeof(SymbolReference).IsAssignableFrom(left.GetType()))
                return null; //ERROR ?

            var right = DecompileExpression();
            if (right == null)
                return null; //ERROR ?

            var statement = new AssignStatement(left as SymbolReference, right, null, null);
            StatementLocations.Add(StartPositions.Pop(), statement);
            return statement;
        }
    }
}
