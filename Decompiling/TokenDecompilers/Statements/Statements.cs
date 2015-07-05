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

                        break;

                    // if (expression) // while / for / do until
                    case (byte)StandardByteCodes.JumpIfNot:
                        return DecompileConditionalJump();

                    // stop;
                    case (byte)StandardByteCodes.Stop:

                        break;

                    // [nothing]
                    case (byte)StandardByteCodes.Nothing:

                        break;

                    // Goto label
                    case (byte)StandardByteCodes.GotoLabel:

                        break;

                    // TODO: eatreturnvalue?

                    // variable = expression;
                    case (byte)StandardByteCodes.Let:

                        break;

                    // new class();
                    case (byte)StandardByteCodes.New:

                        break;

                    // bool = expression
                    case (byte)StandardByteCodes.LetBool:

                        break;

                    // [skip x bytes]
                    case (byte)StandardByteCodes.Skip:

                        break;

                    // object.function(args);
                    case (byte)StandardByteCodes.VirtualFunction:

                        break;

                    // object.function(args);
                    case (byte)StandardByteCodes.FinalFunction:

                        break;

                    // foreach IteratorFunction(...)
                    case (byte)StandardByteCodes.Iterator:

                        break;

                    // global.function(args);
                    case (byte)StandardByteCodes.GlobalFunction:

                        break;

                    // arrayName.Insert(Index, Count);
                    case (byte)StandardByteCodes.DynArrayInsert:

                        break;

                    // TODO: 0x3B - 0x3E native calls here?

                    // arrayName.Remove(Index, Count);
                    case (byte)StandardByteCodes.DynArrayRemove:

                        break;

                    // delegateName(args);
                    case (byte)StandardByteCodes.DelegateFunction:

                        break;

                    // Sort(Delegate) ???
                    case (byte)StandardByteCodes.DelegateProperty:

                        break;

                    // Delegate = expression
                    case (byte)StandardByteCodes.LetDelegate:

                        break;

                    // boolean expression ? expression : expression;
                    case (byte)StandardByteCodes.Conditional:

                        break;

                    //TODO: unkn4F and GoW_DefaultValue ???

                    // arrayName.Add(Count);
                    case (byte)StandardByteCodes.DynArrayAdd:

                        break;

                    // arrayName.AddItem(expression);
                    case (byte)StandardByteCodes.DynArrayAddItem:

                        break;

                    // arrayName.RemoveItem(expression);
                    case (byte)StandardByteCodes.DynArrayRemoveItem:

                        break;

                    // arrayName.InsertItem(Index, expression);
                    case (byte)StandardByteCodes.DynArrayInsertItem:

                        break;

                    // foreach arrayName(valuevariable[, indexvariable])
                    case (byte)StandardByteCodes.DynArrayIterator:

                        break;

                    // arrayName.Sort(SortDelegate);
                    case (byte)StandardByteCodes.DynArraySort:

                        break;

                    // TODO: 0x5A -> 0x65 ???

                    default:
                        // ERROR!

                        break;
                }

            return null;
        }

        public ReturnStatement DecompileReturn()
        {
            PopByte();
            var expr = DecompileExpression();
            if (expr == null && PeekByte != (byte)StandardByteCodes.Nothing)
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
    }
}
