using ME3Script.Analysis.Visitors;
using ME3Script.Language.ByteCode;
using ME3Script.Language.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Gammtek.Conduit.IO;
using ME3Explorer;
using ME3Explorer.ME3Script.Utilities;
using ME3ExplorerCore.Packages;
using ME3ExplorerCore.Unreal;
using ME3ExplorerCore.Unreal.BinaryConverters;

namespace ME3Script.Decompiling
{
    //TODO: most likely cleaner to convert to stack-based solution like the tokenstream, investigate.
    public partial class ME3ByteCodeDecompiler : ObjectReader 
    {
        private readonly UStruct DataContainer;
        private readonly UClass ContainingClass;
        private IMEPackage PCC => DataContainer.Export.FileRef;
        private byte PopByte() { return ReadByte(); }

        private byte PeekByte => Position < Size ? _data[Position] : (byte)0;

        private byte PrevByte => (byte)(Position > 0 ? _data[Position - 1] : 0);

        private Dictionary<ushort, Statement> StatementLocations;
        private Stack<ushort> StartPositions;
        private List<List<Statement>> Scopes;
        private Stack<int> CurrentScope;
        private List<ForEachLoop> decompiledForEachLoops = new List<ForEachLoop>();

        private Queue<FunctionParameter> OptionalParams;
        private readonly List<FunctionParameter> Parameters;

        private List<LabelTableEntry> LabelTable;

        private Stack<ushort> ForEachScopes; // For tracking ForEach etc endpoints

        private bool isInContextExpression = false; // For super lookups

        private bool CurrentIs(StandardByteCodes val)
        {
            return PeekByte == (byte)val;
        }

        private int _totalPadding;
        private IEntry ReadObject()
        {
            var index = ReadInt32();
            var remaining = DataContainer.ScriptStorageSize - (Position - _totalPadding);
            Buffer.BlockCopy(_data, Position, _data, Position + 4, remaining); // copy the data forward
            Buffer.BlockCopy(new byte[]{0,0,0,0}, 0, _data, Position, 4); // write 0 padding

            _totalPadding += 4;
            Position += 4;

            return PCC.GetEntry(index);
        }
        public NameReference ReadNameReference()
        {
            return new NameReference(PCC.GetNameEntry(ReadInt32()), ReadInt32());
        }

        public ME3ByteCodeDecompiler(UStruct dataContainer, UClass containingClass, List<FunctionParameter> parameters = null)
            :base(new byte[dataContainer.ScriptBytecodeSize])
        {
            Buffer.BlockCopy(dataContainer.ScriptBytes, 0, _data, 0, dataContainer.ScriptStorageSize);
            DataContainer = dataContainer;
            ContainingClass = containingClass;
            Parameters = parameters;
        }

        public CodeBody Decompile()
        {
            // Skip native funcs
            if (DataContainer is UFunction Func && Func.FunctionFlags.HasFlag(FunctionFlags.Native))
            {
                var comment = new ExpressionOnlyStatement(new SymbolReference(null, "// Native function"));
                return new CodeBody(new List<Statement> { comment });
            }

            Position = 0;
            _totalPadding = 0;
            CurrentScope = new Stack<int>();
            var statements = new List<Statement>();
            var positions = new List<int>();
            StatementLocations = new Dictionary<ushort, Statement>();
            StartPositions = new Stack<ushort>();
            Scopes = new List<List<Statement>>();
            LabelTable = new List<LabelTableEntry>();
            ForEachScopes = new Stack<ushort>();

            DecompileDefaultParameterValues(statements);

            Scopes.Add(statements);
            CurrentScope.Push(Scopes.Count - 1);
            while (Position < Size && !CurrentIs(StandardByteCodes.EndOfScript))
            {
                var statementPos = Position;
                var current = DecompileStatement();
                if (current == null && PeekByte == (byte)StandardByteCodes.EndOfScript)
                    break; // Natural end after label table, no error
                if (current == null)
                {
                    //as well as being eye-catching in generated code, this is totally invalid unrealscript and will cause compilation errors!
                    statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "**************************")));
                    statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*                        *")));
                    statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*  DECOMPILATION ERROR!  *")));
                    statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "*                        *")));
                    statements.Add(new ExpressionOnlyStatement(new SymbolReference(null, "**************************")));
                    positions.Add(statementPos);
                    positions.Add(statementPos);
                    positions.Add(statementPos);
                    positions.Add(statementPos);
                    positions.Add(statementPos);
                    break;
                }

                statements.Add(current);
                positions.Add(statementPos);
            }
            CurrentScope.Pop();
            AddStateLabels();

            Dictionary<Statement, ushort> LocationStatements = StatementLocations.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);
            DecompileLoopsAndIfs(statements, LocationStatements, topLevel:true);

            //a void return at the end of a function is a bytecode implementation detail, get rid of it.
            //This will also get rid of returnnothings, so loop to make sure we get both
            while (statements.Count > 0 && statements.Last() is ReturnStatement ret && ret.Value is null)
            {
                statements.RemoveAt(statements.Count - 1);
            }

            return new CodeBody(statements);
        }

        private void DecompileLoopsAndIfs(List<Statement> statements, Dictionary<Statement, ushort> positions, bool inLoop = false, bool topLevel = false)
        {
            for (int i = statements.Count - 1; i >= 0; i--)
            {
                var cur = statements[i];
                if (!positions.TryGetValue(cur, out ushort curPos)) continue; //default param values, labels

                //detect end of while or for loop
                if (cur is UnconditionalJump loopEnd && loopEnd.JumpLoc < curPos)
                {
                    var continueToPos = curPos;
                    Statement statementBeforeEndOfLoop = statements[i - 1];
                    bool isForLoop = IsIncrementDecrement(statementBeforeEndOfLoop, out Statement update);
                    var loopStart = StatementLocations[loopEnd.JumpLoc];
                    int conditionIdx = statements.IndexOf(loopStart);
                    int skipStart = conditionIdx;
                    int numToSkip = i - conditionIdx + 1;

                    if (!(statements[conditionIdx] is ConditionalJump conditionalJump))
                    {
                        //TODO: replace the unconditional jump with a goto? or perhaps this is a loop with no condition?
                        throw new Exception("Invalid Control Flow!");
                    }
                    CodeBody loopBody = new CodeBody(statements.GetRange(conditionIdx + 1, i - conditionIdx - 1));
                    if (!isForLoop && !(statementBeforeEndOfLoop is Jump))
                    {
                        //check to see if there is an unconditional jump to the statement before the end of the loop. This indicates a continue inside a for loop
                        var checkPos = positions[statementBeforeEndOfLoop];
                        if (loopBody.Statements.OfType<UnconditionalJump>().Any(possibleContinue => possibleContinue.JumpLoc == checkPos))
                        {
                            update = statementBeforeEndOfLoop;
                            isForLoop = true;
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
                    if (conditionalJump is NullJump nullJump)
                    {
                        condition = new InOpReference(new InOpDeclaration(nullJump.Not ? "!=" : "==", 0, 0, null, null, null), nullJump.Condition, new NoneLiteral());
                    }
                    if (isForLoop)
                    {
                        continueToPos = positions[statementBeforeEndOfLoop];
                        loopBody.Statements.RemoveAt(loopBody.Statements.Count - 1);
                        loop = new ForLoop(forInit, condition, update, loopBody);
                    }
                    else
                    {
                        loop = new WhileLoop(condition, loopBody);
                    }

                    //recurse into body of loop
                    DecompileLoopsAndIfs(loopBody.Statements, positions, true);

                    //convert unconditional jumps into continue and break
                    const int sizeOfUnconditionalJump = 3;
                    ConvertJumps(loopBody, continueToPos, loopEnd.JumpLoc, curPos + sizeOfUnconditionalJump);

                    FinishSwitch(loopBody.Statements);

                    statements.RemoveRange(skipStart, numToSkip);
                    statements.Insert(skipStart, loop);
                    StatementLocations[loopLoc] = loop;
                    positions[loop] = loopLoc;

                    i = skipStart;
                }

                //detect end of do until loop
                else if (cur is IfNotJump inj && inj.JumpLoc < curPos)
                {
                    var loopStart = StatementLocations[inj.JumpLoc];
                    var loopStartIdx = statements.IndexOf(loopStart);
                    int loopLength = i - loopStartIdx;
                    DoUntilLoop loop = new DoUntilLoop(inj.Condition, new CodeBody(statements.GetRange(loopStartIdx, loopLength)));

                    //recurse into body of loop
                    DecompileLoopsAndIfs(loop.Body.Statements, positions, inLoop);

                    //convert unconditional jumps into continue and break
                    ConvertJumps(loop.Body, curPos, inj.JumpLoc, curPos + inj.SizeOfExpression);

                    FinishSwitch(loop.Body.Statements);

                    statements.RemoveRange(loopStartIdx, loopLength + 1);
                    statements.Insert(loopStartIdx, loop);
                    StatementLocations[inj.JumpLoc] = loop;
                    positions[loop] = inj.JumpLoc;

                    i = loopStartIdx;
                }

                //Just a plain if (and maybe else)
                else if (cur is ConditionalJump ifJump)
                {
                    var jumpToStatement = StatementLocations[ifJump.JumpLoc];
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
                        if (inLoop && thenBodyLength == 1 && statements[thenBodyStartIdx] is UnconditionalJump possibleContinue && possibleContinue.JumpLoc > positions[statements.Last()])
                        {
                            //this is a continue, not an else. Will be converted elsewhere
                        }
                        else if (statements[jumpToStatementIdx - 1] is UnconditionalJump elseJump)
                        {
                            var elseJumpToStatement = StatementLocations[elseJump.JumpLoc];
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
                    CodeBody thenBody = new CodeBody(statements.GetRange(thenBodyStartIdx, thenBodyLength));

                    DecompileLoopsAndIfs(thenBody.Statements, positions, inLoop);
                    FinishSwitch(thenBody.Statements);
                    if (elseBody != null)
                    {
                        DecompileLoopsAndIfs(elseBody.Statements, positions, inLoop);
                        FinishSwitch(elseBody.Statements);
                    }

                    Expression condition = ifJump.Condition;
                    if (ifJump is NullJump nullJump)
                    {
                        condition = new InOpReference(new InOpDeclaration(nullJump.Not ? "!=" : "==", 0, 0, null, null, null), nullJump.Condition, new NoneLiteral());
                    }
                    statements.RemoveRange(i, totalLength);
                    IfStatement ifStatement = new IfStatement(condition, thenBody, elseBody);
                    statements.Insert(i, ifStatement);
                    StatementLocations[curPos] = ifStatement;
                    positions[ifStatement] = curPos;
                }

                else if (cur is ForEachLoop fel && !decompiledForEachLoops.Contains(fel))
                {
                    decompiledForEachLoops.Add(fel);
                    DecompileLoopsAndIfs(fel.Body.Statements, positions, false);

                    FinishSwitch(fel.Body.Statements);
                }
            }

            if (topLevel)
            {
                FinishSwitch(statements);
            }

            static bool IsIncrementDecrement(Statement stmnt, out Statement update)
            {
                update = stmnt;
                if (stmnt is ExpressionOnlyStatement expStmnt)
                {
                    if (expStmnt.Value is PreOpReference preOp && (preOp.Operator.OperatorKeyword == "++" || preOp.Operator.OperatorKeyword == "--"))
                    {
                        return true;
                    }

                    if (expStmnt.Value is PostOpReference postOp && (postOp.Operator.OperatorKeyword == "++" || postOp.Operator.OperatorKeyword == "--"))
                    {
                        return true;
                    }
                }
                update = null;
                return false;
            }
            void ConvertJumps(CodeBody codeBody, int continueToPos, int loopStartPos, int breakPos, bool inSwitch = false)
            {
                for (int j = 0; j < codeBody.Statements.Count; j++)
                {
                    Statement bodyStatement = codeBody.Statements[j];
                    switch (bodyStatement)
                    {
                        case UnconditionalJump unJump when unJump.JumpLoc == continueToPos || unJump.JumpLoc == loopStartPos:
                        {
                            ContinueStatement continueStatement = new ContinueStatement();
                            ushort position = positions[bodyStatement];
                            StatementLocations[position] = codeBody.Statements[j] = continueStatement;
                            positions[continueStatement] = position;
                            break;
                        }
                        case UnconditionalJump unJump when (!inSwitch && unJump.JumpLoc == breakPos) || (inSwitch && unJump.JumpLoc > positions[codeBody.Statements.Last()]):
                        {
                            BreakStatement breakStatement = new BreakStatement();
                            ushort position = positions[bodyStatement];
                            StatementLocations[position] = codeBody.Statements[j] = breakStatement;
                            positions[breakStatement] = position;
                            break;
                        }
                        //case UnconditionalJump _:
                        //    //replace with goto statement?
                        //    throw new Exception("Invalid Control Flow!");
                        case IfStatement ifStatement:
                            if (ifStatement.Then?.Statements != null) ConvertJumps(ifStatement.Then, continueToPos, loopStartPos, breakPos, inSwitch);
                            if (ifStatement.Else?.Statements != null) ConvertJumps(ifStatement.Else, continueToPos, loopStartPos, breakPos, inSwitch);
                            break;
                    }
                }
            }

            void FinishSwitch(List<Statement> stmnts)
            {
                var defaults = new Stack<int>();
                var jumps = new Dictionary<int, ushort>();
                for (int i = stmnts.Count - 1; i >= 0; i--)
                {
                    var cur = stmnts[i];
                    if (cur is DefaultCaseStatement)
                    {
                        defaults.Push(i);
                    }
                    else if (cur is UnconditionalJump uj)
                    {
                        if (defaults.Count > 0)
                        {
                            jumps[defaults.Count] = uj.JumpLoc;
                        }
                        stmnts[i] = new BreakStatement();
                    }
                    else if (cur is SwitchStatement sw)
                    {
                        int lastStatementIdx = 0;

                        //if this switch has breaks in it, get the end of the switch, else the last statement is the default case
                        if (jumps.TryGetValue(defaults.Count, out ushort jumpLoc))
                        {
                            jumps.Remove(defaults.Count);
                            defaults.Pop();
                            var jumpToStatement = StatementLocations[jumpLoc];
                            lastStatementIdx = stmnts.IndexOf(jumpToStatement) - 1;
                            if (lastStatementIdx < i)
                            {
                                lastStatementIdx = stmnts.Count - 1;
                            }
                        }
                        else
                        {
                            lastStatementIdx = defaults.Pop();
                        }

                        int length = lastStatementIdx - i;

                        sw.Body = new CodeBody(stmnts.GetRange(i + 1, length));
                        stmnts.RemoveRange(i + 1, length);
                        if (defaults.Count > 0)
                        {
                            var newDefaults = defaults.Reverse().Select(idx => idx - length).ToList();
                            defaults = new Stack<int>();
                            foreach (var newDef in newDefaults)
                            {
                                defaults.Push(newDef);
                            }
                        }
                    }
                }
            }
        }

        private void AddStateLabels()
        {
            foreach (var label in LabelTable)
            {
                var node = new StateLabel(label.NameRef, (int)label.Offset, null, null);
                var statement = StatementLocations[(ushort)label.Offset];
                foreach (List<Statement> stmnt in Scopes)
                {
                    var index = stmnt.IndexOf(statement);
                    if (index != -1)
                        stmnt.Insert(index, node);
                }
            }
        }

        private void DecompileDefaultParameterValues(List<Statement> statements)
        {
            OptionalParams = new Queue<FunctionParameter>();
            if (DataContainer is UFunction func) // Gets all optional params for default value parsing
            {
                foreach (FunctionParameter param in Parameters.Where(param => param.IsOptional))
                {
                    OptionalParams.Enqueue(param);
                }
            }

            while (PeekByte == (byte)StandardByteCodes.DefaultParmValue 
                || PeekByte == (byte)StandardByteCodes.Nothing)
            {
                StartPositions.Push((ushort)Position);
                var token = PopByte();
                if (token == (byte)StandardByteCodes.DefaultParmValue) // default value assigned
                {
                    
                    ReadInt16(); //MemLength of value
                    var value = DecompileExpression();
                    PopByte(); // end of value


                    if (OptionalParams.Count != 0)
                    {
                        var parm = OptionalParams.Dequeue();
                        parm.DefaultParameter = value;
                        StartPositions.Pop();
                    }
                    else
                    {       // TODO: weird, research how to deal with this
                        var builder = new CodeBuilderVisitor(); // what a wonderful hack, TODO.
                        value.AcceptVisitor(builder);
                        var comment = new SymbolReference(null, "// Orphaned Default Parm: " + builder.GetCodeString(), null, null);
                        var statement = new ExpressionOnlyStatement(comment, null, null);
                        StatementLocations.Add(StartPositions.Pop(), statement);
                        statements.Add(statement);
                    }
                }
                else
                {
                    OptionalParams.Dequeue();
                }
            }
        }
    }
}
