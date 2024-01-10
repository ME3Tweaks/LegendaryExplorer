using System;
using System.Collections.Generic;
using System.Linq;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;
using LegendaryExplorerCore.Unreal.BinaryConverters;
using LegendaryExplorerCore.UnrealScript.Analysis.Symbols;
using LegendaryExplorerCore.UnrealScript.Language.ByteCode;
using LegendaryExplorerCore.UnrealScript.Language.Tree;
using LegendaryExplorerCore.UnrealScript.Lexing;
using LegendaryExplorerCore.UnrealScript.Utilities;
using static LegendaryExplorerCore.Unreal.UnrealFlags;

namespace LegendaryExplorerCore.UnrealScript.Decompiling
{
    internal partial class ByteCodeDecompiler : ObjectReader 
    {
        private readonly UStruct DataContainer;
        private readonly UClass ContainingClass;
        private readonly FileLib FileLib;

        private readonly MEGame Game;
        private readonly byte extNativeIndex;

        private bool LibInitialized => FileLib?.IsInitialized ?? false;
        private SymbolTable ReadOnlySymbolTable => FileLib?.ReadonlySymbolTable;

        private IMEPackage Pcc => DataContainer.Export.FileRef;
        private byte PopByte() { return ReadByte(); }

        private byte PeekByte => Position < Size ? _data[Position] : (byte)0;

        private Dictionary<ushort, Statement> StatementLocations;
        private Stack<ushort> StartPositions;
        private List<List<Statement>> Scopes;
        private Stack<int> CurrentScope;
        private readonly List<ForEachLoop> decompiledForEachLoops = new();
        
        private readonly List<FunctionParameter> Parameters;
        private readonly VariableType ReturnType;

        private List<LabelTableEntry> LabelTable;

        private Stack<ushort> ForEachScopes; // For tracking ForEach etc endpoints

        private bool isInContextExpression; // For super lookups

        private readonly Dictionary<ushort, List<string>> ReplicatedProperties; //for decompiling Class replication blocks

        private bool CurrentIs(OpCodes val)
        {
            return PeekByte == (byte)val;
        }

        private int _totalPadding;
        private IEntry ReadObject()
        {
            var index = ReadInt32();

            //in ME3 and LE, script object references have 4 extra 0 bytes 
            if (Game >= MEGame.ME3)
            {
                var remaining = DataContainer.ScriptStorageSize - (Position - _totalPadding);
                Buffer.BlockCopy(_data, Position, _data, Position + 4, remaining); // copy the data forward
                Buffer.BlockCopy(new byte[]{0,0,0,0}, 0, _data, Position, 4); // write 0 padding

                _totalPadding += 4;
                Position += 4;
            }

            return Pcc.GetEntry(index);
        }

        private NameReference ReadNameReference()
        {
            return new NameReference(Pcc.GetNameEntry(ReadInt32()), ReadInt32());
        }

        public ByteCodeDecompiler(UStruct dataContainer, UClass containingClass, FileLib lib, List<FunctionParameter> parameters = null, VariableType returnType = null, Dictionary<ushort, List<string>> replicatedProperties = null)
            :base(new byte[dataContainer.ScriptBytecodeSize])
        {
            Buffer.BlockCopy(dataContainer.ScriptBytes, 0, _data, 0, dataContainer.ScriptStorageSize);
            DataContainer = dataContainer;
            ContainingClass = containingClass;
            Parameters = parameters;
            ReturnType = returnType;
            FileLib = lib;
            Game = dataContainer.Export.Game;
            extNativeIndex = (byte)(Game.IsGame3() ? 0x70 : 0x60);
            ReplicatedProperties = replicatedProperties;
        }

        public CodeBody Decompile()
        {
            Position = 0;
            _totalPadding = 0;
            CurrentScope = new Stack<int>();
            StatementLocations = new Dictionary<ushort, Statement>();
            StartPositions = new Stack<ushort>();
            Scopes = new List<List<Statement>>();
            LabelTable = new List<LabelTableEntry>();
            ForEachScopes = new Stack<ushort>();
            var statements = new List<Statement>();
            var codeBody = new CodeBody(statements);

            //native funcs can have default params
            if (!DecompileDefaultParameterValues())
            {
                ClearToDecompilationError(statements);
                return codeBody;
            }
            
            if (DataContainer is UFunction func && func.FunctionFlags.Has(EFunctionFlags.Native))
            {
                var comment = new ExpressionOnlyStatement(new SymbolReference(null, "// Native function"));
                return new CodeBody(new List<Statement> { comment });
            }

            if (ContainingClass.Export.ObjectName == "SFXGalaxyMapObject")
            {
                //these functions are broken and cannot be decompiled. instead of trying, we construct a simpler, functionally identical version
                if (DataContainer.Export.ObjectName == "GetEditorLabel") 
                {
                    return new CodeBody(new List<Statement> { new ReturnStatement(new SymbolReference(null, "sLabel")) });
                }
                if (DataContainer.Export.ObjectName == "InitializeAppearance")
                {
                    return new CodeBody(new List<Statement> { new ReturnStatement() });
                }
            }


            Scopes.Add(statements);
            CurrentScope.Push(Scopes.Count - 1);
            while (Position < Size && !CurrentIs(OpCodes.EndOfScript))
            {
                Statement current;
                try
                {
                    current = DecompileStatement();
                    if (current == null && PeekByte == (byte)OpCodes.EndOfScript)
                        break; // Natural end after label table, no error
                }
                catch when(!LegendaryExplorerCoreLib.IsDebug)
                {
                    current = null;
                }

                if (current == null)
                {
                    ClearToDecompilationError(statements);
                    return codeBody;
                }

                statements.Add(current);
            }
            CurrentScope.Pop();

            Dictionary<Statement, ushort> LocationStatements = StatementLocations.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

            if (DataContainer is UClass && ReplicatedProperties is not null)
            {
                var newStatements = new List<Statement>();
                foreach (Statement statement in statements)
                {
                    if (statement is not ExpressionOnlyStatement exprStatement)
                    {
                        ClearToDecompilationError(statements);
                        return codeBody;
                    }
                    ushort loc = LocationStatements[statement];
                    if (!ReplicatedProperties.TryGetValue(loc, out List<string> propNames))
                    {
                        propNames = new() { "#ERROR_MISSING_PROPERTY#" };
                    }
                    List<SymbolReference> replicatedVariables = propNames.Select(s => new SymbolReference(null, s)).ToList();
                    newStatements.Add(new ReplicationStatement(exprStatement.Value, replicatedVariables));
                }
                return new CodeBody(newStatements);
            }

            try
            {
                DecompileLoopsAndIfs(codeBody, LocationStatements);
            }
            catch (Exception e)
            {
                ClearToDecompilationError(statements);
                statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, e.FlattenException())));
                return codeBody;
            }

            //insert state Labels
            foreach (LabelTableEntry label in LabelTable)
            {
                InsertLabel(new Label(label.NameRef, (ushort)label.Offset));
            }

            //insert labels for any (non-state) gotos
            foreach ((ushort labelPos, List<Goto> gotos) in LabelLocationsToFix)
            {
                var label = new Label($"label_0x{labelPos:X}", labelPos);
                bool hasValidGotos = false;
                foreach (Goto g in gotos)
                {
                    //some gotos may have been converted into breaks or continues
                    if (StatementLocations[LocationStatements[g]] == g)
                    {
                        hasValidGotos = true;
                    }
                    g.Label = label;
                    g.LabelName = label.Name;
                }
                if (!hasValidGotos)
                {
                    continue;
                }
                InsertLabel(label);
            }

            //a void return at the end of a function is a bytecode implementation detail, get rid of it.
            //This will also get rid of returnnothings, so loop to make sure we get both
            while (statements.Count > 0 && statements.Last() is ReturnStatement {Value: null})
            {
                statements.RemoveAt(statements.Count - 1);
            }

            return codeBody;
        }

        private static void ClearToDecompilationError(List<Statement> statements)
        {
            //as well as being eye-catching in generated code, this is totally invalid unrealscript and will prevent recompilation :)
            statements.Clear();
            statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "**************************")));
            statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*                        *")));
            statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*  DECOMPILATION ERROR!  *")));
            statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*                        *")));
            statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "**************************")));
        }

        private void InsertLabel(Label label)
        {
            var statementAtLabelLocation = StatementLocations[label.StartOffset];
            while (statementAtLabelLocation is UnconditionalJump unJump)
            {
                statementAtLabelLocation = StatementLocations[unJump.JumpLoc];
            }

            switch (statementAtLabelLocation.Outer)
            {
                case CodeBody cb:
                {
                    int stmntIdx = cb.Statements.IndexOf(statementAtLabelLocation);
                    label.Outer = cb;
                    cb.Statements.Insert(stmntIdx, label);
                    break;
                }
                case ForLoop forLoop:
                    if (statementAtLabelLocation == forLoop.Update)
                    {
                        label.Outer = forLoop.Body;
                        forLoop.Body.Statements.Add(label);
                    }
                    else //statementAtLabelLocation is the initializer
                    {
                        var cb = (CodeBody) forLoop.Outer;
                        int stmntIdx = cb.Statements.IndexOf(forLoop);
                        label.Outer = cb;
                        cb.Statements.Insert(stmntIdx, label);
                    }

                    break;
                case ForEachLoop forEachLoop:
                    if (statementAtLabelLocation is IteratorNext)
                    {
                        label.Outer = forEachLoop.Body;
                        forEachLoop.Body.Statements.Add(label);
                    }
                    else
                    {
                        throw new Exception("Invalid control flow!");
                    }

                    break;
                default:
                    throw new Exception("Invalid control flow!");
            }
        }

        private readonly Dictionary<ushort, List<Goto>> LabelLocationsToFix = new();

        private void DecompileLoopsAndIfs(CodeBody outer, Dictionary<Statement, ushort> positions, bool inLoop = false)
        {
            List<Statement> statements = outer.Statements;
            var defaultCaseStatements = new Stack<Statement>();
            var switchEnds = new Dictionary<int, ushort>();
            for (int i = statements.Count - 1; i >= 0; i--)
            {
                Statement cur = statements[i];
                cur.Outer = outer;
                if (!positions.TryGetValue(cur, out ushort curPos)) continue; //default param values, labels

                if (cur is UnconditionalJump loopEnd && loopEnd.JumpLoc < curPos)
                {
                    //end of while or for loop
                    ushort continueToPos = curPos;
                    Statement statementBeforeEndOfLoop = statements[i - 1];
                    bool isForLoop = IsIncrementDecrement(statementBeforeEndOfLoop, out Statement update);
                    Statement loopStart = StatementLocations[loopEnd.JumpLoc];
                    int conditionIdx = statements.IndexOf(loopStart);
                    int skipStart = conditionIdx;
                    int numToSkip = i - conditionIdx + 1;

                    if (statements[conditionIdx] is not ConditionalJump conditionalJump)
                    {
                        //no loop condition, so this is a backwards goto instead
                        ReplaceWithGoto(loopEnd, outer, i);
                        continue;
                    }
                    var loopBody = new CodeBody(statements.GetRange(conditionIdx + 1, i - conditionIdx - 1));
                    if (!isForLoop && statementBeforeEndOfLoop is not Jump)
                    {
                        //check to see if there is an unconditional jump to the statement before the end of the loop. This indicates a continue inside a for loop
                        ushort checkPos = positions[statementBeforeEndOfLoop];
                        if (loopBody.Statements.OfType<UnconditionalJump>().Any(possibleContinue => possibleContinue.JumpLoc == checkPos))
                        {
                            update = statementBeforeEndOfLoop;
                            isForLoop = true;
                        }
                    }
                    if (isForLoop)
                    {
                        //check to see if there is an unconditional jump to the end of the loop.
                        //This indicates that the loop is NOT a for loop, as there is no way to skip the increment statement in a for loop
                        for (int j = i - 2; j > conditionIdx; j--)
                        {
                            if (statements[j] is UnconditionalJump unj && unj.JumpLoc == curPos)
                            {
                                isForLoop = false;
                                update = null;
                                break;
                            }
                        }
                    }
                    AssignStatement forInit = null;
                    ushort loopLoc = loopEnd.JumpLoc;
                    if (isForLoop && conditionIdx > 0 && statements[conditionIdx - 1] is AssignStatement assignStatement)
                    {
                        forInit = assignStatement;
                        skipStart = conditionIdx - 1;
                        numToSkip++;
                        loopLoc = positions[assignStatement];
                    }

                    Statement loop;
                    Expression condition = conditionalJump.Condition;
                    if (isForLoop)
                    {
                        continueToPos = positions[statementBeforeEndOfLoop];
                        loopBody.Statements.RemoveAt(loopBody.Statements.Count - 1);
                        loop = new ForLoop(forInit, condition, update, loopBody) { Outer = outer };
                    }
                    else
                    {
                        loop = new WhileLoop(condition, loopBody) { Outer = outer };
                    }
                    
                    //recurse into body of loop
                    DecompileLoopsAndIfs(loopBody, positions, true);

                    //convert unconditional jumps into continue and break
                    const int sizeOfUnconditionalJump = 3;
                    ConvertJumps(loopBody, continueToPos, loopEnd.JumpLoc, curPos + sizeOfUnconditionalJump);

                    statements.RemoveRange(skipStart, numToSkip);
                    statements.Insert(skipStart, loop);
                    StatementLocations[loopLoc] = loop;
                    positions[loop] = loopLoc;

                    i = skipStart;
                }
                else if (cur is ConditionalJump inj && inj.JumpLoc < curPos)
                {
                    //end of do until loop
                    Statement loopStart = StatementLocations[inj.JumpLoc];
                    int loopStartIdx = statements.IndexOf(loopStart);
                    int loopLength = i - loopStartIdx;
                    var loop = new DoUntilLoop(inj.Condition, new CodeBody(statements.GetRange(loopStartIdx, loopLength))) { Outer = outer };

                    //recurse into body of loop
                    DecompileLoopsAndIfs(loop.Body, positions, inLoop);

                    //convert unconditional jumps into continue and break
                    ConvertJumps(loop.Body, curPos, inj.JumpLoc, curPos + inj.SizeOfExpression);

                    statements.RemoveRange(loopStartIdx, loopLength + 1);
                    statements.Insert(loopStartIdx, loop);
                    StatementLocations[inj.JumpLoc] = loop;
                    positions[loop] = inj.JumpLoc;

                    i = loopStartIdx;
                }
                else if (cur is ConditionalJump ifJump)
                {
                    //Just a plain if (and maybe else)
                    Statement jumpToStatement = StatementLocations[ifJump.JumpLoc];
                    int jumpToStatementIdx = statements.IndexOf(jumpToStatement);
                    if (jumpToStatementIdx == -1)
                    {
                        jumpToStatementIdx = statements.Count;
                    }

                    int thenBodyStartIdx = i + 1;
                    int thenBodyLength = jumpToStatementIdx - thenBodyStartIdx;

                    int totalLength = thenBodyLength + 1;

                    CodeBody elseBody = null;
                    if (jumpToStatementIdx < statements.Count)
                    {
                        if (inLoop && thenBodyLength == 1 && statements[thenBodyStartIdx] is UnconditionalJump possibleContinue && possibleContinue.JumpLoc > positions[statements[^1]])
                        {
                            //this is a continue, not an else. Will be converted elsewhere
                        }
                        else if (statements[jumpToStatementIdx - 1] is UnconditionalJump elseJump)
                        {
                            if (inLoop && elseJump.JumpLoc > positions[statements[^1]])
                            {
                                //this is a break or goto. Will be converted elsewhere
                            }
                            else
                            {
                                Statement elseJumpToStatement = StatementLocations[elseJump.JumpLoc];
                                int elseJumpToStatementIdx = statements.IndexOf(elseJumpToStatement);
                                if (elseJumpToStatementIdx == -1)
                                {
                                    elseJumpToStatementIdx = statements.Count;
                                }

                                elseBody = new CodeBody(statements.GetRange(jumpToStatementIdx, elseJumpToStatementIdx - jumpToStatementIdx));
                                thenBodyLength--;
                                totalLength += elseBody.Statements.Count;
                            }
                        }
                    }
                    var thenBody = new CodeBody(statements.GetRange(thenBodyStartIdx, thenBodyLength));

                    //shouldn't be neccesary? we're working backwards, so all the statements in the bodies will have been processed already
                    //DecompileLoopsAndIfs(thenBody.Statements, positions, inLoop);
                    //FinishSwitch(thenBody.Statements);
                    //if (elseBody != null)
                    //{
                    //    DecompileLoopsAndIfs(elseBody.Statements, positions, inLoop);
                    //    FinishSwitch(elseBody.Statements);
                    //}

                    Expression condition = ifJump.Condition;
                    statements.RemoveRange(i, totalLength);
                    var ifStatement = new IfStatement(condition, thenBody, elseBody) { Outer = outer };

                    statements.Insert(i, ifStatement);
                    StatementLocations[curPos] = ifStatement;
                    positions[ifStatement] = curPos;
                }
                else if (cur is ForEachLoop fel && !decompiledForEachLoops.Contains(fel))
                {
                    decompiledForEachLoops.Add(fel);
                    DecompileLoopsAndIfs(fel.Body, positions, false);
                    ConvertJumps(fel.Body, fel.iteratorPopPos - 1, -1, fel.iteratorPopPos);
                }
                else if (cur is DefaultCaseStatement)
                {
                    defaultCaseStatements.Push(cur);
                    int breakIdx = i - 1;
                    bool prevStatementWasCase = true;
                    ushort casePos = curPos;
                    while (breakIdx >= 0 && (statements[breakIdx] is not UnconditionalJump uj || uj.JumpLoc <= curPos))
                    {
                        Statement curStatement = statements[breakIdx];
                        if (prevStatementWasCase && curStatement is SwitchStatement)
                        {
                            //reached the start of the switch statement this defaultcase belongs to.
                            //no break was found.
                            breakIdx = -1;
                            break;
                        }
                        if (curStatement is CaseStatement cs && cs.LocationOfNextCase == casePos)
                        {
                            casePos = positions[curStatement];
                            prevStatementWasCase = true;
                        }
                        else
                        {
                            prevStatementWasCase = false;
                        }
                        --breakIdx;
                    }

                    if (breakIdx >= 0)
                    {
                        var switchEndJump = (UnconditionalJump)statements[breakIdx];
                        switchEnds[defaultCaseStatements.Count] = switchEndJump.JumpLoc;
                        var brk = new BreakStatement { Outer = outer };
                        StatementLocations[positions[switchEndJump]] = statements[breakIdx] = brk;
                        positions[brk] = positions[switchEndJump];

                        var defaultBreakIdx = statements.IndexOf(StatementLocations[switchEndJump.JumpLoc]) - 1;
                        if (defaultBreakIdx < 0)
                        {
                            defaultBreakIdx = statements.Count - 1;
                        }

                        if (statements[defaultBreakIdx] is UnconditionalJump defaultBreak && defaultBreak.JumpLoc == switchEndJump.JumpLoc)
                        {
                            var defBrk = new BreakStatement { Outer = outer };
                            StatementLocations[positions[defaultBreak]] = statements[defaultBreakIdx] = defBrk;
                            positions[defBrk] = positions[defaultBreak];
                        }
                    }
                }
                else if (cur is UnconditionalJump possibleBreak && defaultCaseStatements.Count > 0 && switchEnds.ContainsValue(possibleBreak.JumpLoc))
                {
                    var brk = new BreakStatement { Outer = outer };
                    StatementLocations[positions[possibleBreak]] = statements[i] = brk;
                    positions[brk] = positions[possibleBreak];
                }
                else if (cur is SwitchStatement sw)
                {
                    int lastStatementIdx;

                    //if this switch has breaks in it, get the end of the switch, else the last statement is the default case
                    if (switchEnds.TryGetValue(defaultCaseStatements.Count, out ushort jumpLoc))
                    {
                        switchEnds.Remove(defaultCaseStatements.Count);
                        defaultCaseStatements.Pop();
                        Statement jumpToStatement = StatementLocations[jumpLoc];
                        lastStatementIdx = statements.IndexOf(jumpToStatement) - 1;
                        if (lastStatementIdx < i)
                        {
                            lastStatementIdx = statements.Count - 1;
                        }
                    }
                    else
                    {
                        lastStatementIdx = statements.IndexOf(defaultCaseStatements.Pop());
                    }

                    int length = lastStatementIdx - i;

                    sw.Body = new CodeBody(statements.GetRange(i + 1, length)) {Outer = sw};
                    statements.RemoveRange(i + 1, length);

                    //if this is switching on an enum property, we should convert integer literals in case statements to enum values
                    if (sw.Expression is SymbolReference {Node: Enumeration enm})
                    {
                        int valuesCount = enm.Values.Count;
                        foreach (CaseStatement caseStatement in sw.Body.Statements.OfType<CaseStatement>())
                        {
                            if (caseStatement.Value is IntegerLiteral {Value: >= 0} intLit && intLit.Value < valuesCount)
                            {
                                EnumValue enumValue = enm.Values[intLit.Value];
                                caseStatement.Value = new SymbolReference(enumValue, enumValue.Name);
                            }
                        }
                    }
                }
            }

            //convert any remaining unconditionaljumps into gotos
            ConvertJumps(outer, -1, -1, -1);

            static bool IsIncrementDecrement(Statement stmnt, out Statement update)
            {
                update = stmnt;
                if (stmnt is ExpressionOnlyStatement expStmnt)
                {
                    switch (expStmnt.Value)
                    {
                        case PreOpReference preOp when (preOp.Operator.OperatorType is TokenType.Increment or TokenType.Decrement):
                        case PostOpReference postOp when (postOp.Operator.OperatorType is TokenType.Increment or TokenType.Decrement):
                            return true;
                    }
                }
                update = null;
                return false;
            }
            void ConvertJumps(CodeBody codeBody, int continueToPos, int loopStartPos, int breakPos)
            {
                for (int j = 0; j < codeBody.Statements.Count; j++)
                {
                    Statement bodyStatement = codeBody.Statements[j];
                    switch (bodyStatement)
                    {
                        case UnconditionalJump unJump when unJump.JumpLoc == continueToPos || unJump.JumpLoc == loopStartPos:
                        {
                            var continueStatement = new ContinueStatement { Outer = codeBody };
                            ushort position = positions[bodyStatement];
                            StatementLocations[position] = codeBody.Statements[j] = continueStatement;
                            positions[continueStatement] = position;
                            break;
                        }
                        case UnconditionalJump unJump when unJump.JumpLoc == breakPos:
                        {
                            var breakStatement = new BreakStatement { Outer = codeBody };
                            ushort position = positions[bodyStatement];
                            StatementLocations[position] = codeBody.Statements[j] = breakStatement;
                            positions[breakStatement] = position;
                            break;
                        }
                        case UnconditionalJump unJump and not Goto:
                        {
                            ReplaceWithGoto(unJump, codeBody, j);
                            break;
                        }
                        case IfStatement ifStatement:
                            if (ifStatement.Then?.Statements != null) ConvertJumps(ifStatement.Then, continueToPos, loopStartPos, breakPos);
                            if (ifStatement.Else?.Statements != null) ConvertJumps(ifStatement.Else, continueToPos, loopStartPos, breakPos);
                            break;
                        case SwitchStatement switchStatement:
                            if (switchStatement.Body?.Statements != null) ConvertJumps(switchStatement.Body, continueToPos, loopStartPos, breakPos);
                            break;
                    }
                }
            }

            void ReplaceWithGoto(UnconditionalJump unJump, CodeBody codeBody, int j)
            {
                var gotoStatement = new Goto($"{unJump.JumpLoc:X}", jumpLoc: unJump.JumpLoc) {Outer = codeBody};
                ushort position = positions[unJump];
                StatementLocations[position] = codeBody.Statements[j] = gotoStatement;
                positions[gotoStatement] = position;
                LabelLocationsToFix.AddToListAt(unJump.JumpLoc, gotoStatement);
            }
        }

        private bool DecompileDefaultParameterValues()
        {
            if (DataContainer is UFunction) // Gets all optional params for default value parsing
            {
                foreach (FunctionParameter param in Parameters.Where(param => param.IsOptional))
                {
                    StartPositions.Push((ushort)Position);
                    byte token = PopByte();
                    if (token == (byte)OpCodes.DefaultParmValue) // default value assigned
                    {

                        ReadInt16(); //MemLength of value
                        var value = DecompileExpression();
                        PopByte(); // Opcodes.EndParmValue

                        
                        param.DefaultParameter = value;
                        StartPositions.Pop();
                    }
                    else if (token != (byte)OpCodes.Nothing)
                    {
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
