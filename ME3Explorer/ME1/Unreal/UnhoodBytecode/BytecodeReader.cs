using ME3Explorer.Packages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ME3Explorer.Unreal;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{

    public class BytecodeToken
    {
        public int NativeIndex;
        private string _text;
        private readonly int _offset;

        public BytecodeReader.ME1OpCodes OpCode { get; internal set; }

        public BytecodeToken(string text, int offset)
        {
            _text = text;
            _offset = offset;
        }

        public int GetOffset()
        {
            return _offset;
        }

        public override string ToString()
        {
            return _text;
        }

        public BytecodeSingularToken ToBytecodeSingularToken(int scriptStartOffset)
        {
            BytecodeSingularToken bcst = new BytecodeSingularToken();
            string opcodetext = OpCode.ToString();

            if (NativeIndex > 0)
            {
                var function = CachedNativeFunctionInfo.GetNativeFunction(NativeIndex); //this could be done dynamically. But if you're able to add native functions to the exe you probably know more about the engine than me
                if (function != null)
                {
                    opcodetext = function.Name;
                }
            }
            opcodetext = $"[{((byte)OpCode):X2}] {opcodetext}";
            bcst.CurrentStack = _text;
            bcst.OpCode = opcodetext;
            bcst.StartPos = _offset + scriptStartOffset;
            return bcst;
        }

    }

    class ReturnToken : BytecodeToken
    {
        private readonly BytecodeToken _returnValue;

        public ReturnToken(BytecodeToken returnValue, int offset)
            : base(returnValue.ToString().Length > 0 ? "return " + returnValue : "return", offset)
        {
            _returnValue = returnValue;
        }

        public BytecodeToken ReturnValue
        {
            get { return _returnValue; }
        }
    }

    class SwitchToken : BytecodeToken
    {
        public SwitchToken(string text, BytecodeToken expr, int offset) : base("switch (" + text + ")", offset)
        {
            Expr = expr;
        }

        public BytecodeToken Expr { get; private set; }
    }

    class CaseToken : BytecodeToken
    {
        public CaseToken(string text, int offset) : base("case " + text, offset)
        {
        }
    }

    class DefaultToken : BytecodeToken
    {
        public DefaultToken(int offset) : base("default", offset)
        {
        }
    }

    abstract class JumpToken : BytecodeToken
    {
        private readonly int _targetOffset;

        protected JumpToken(string text, int targetOffset, int offset) : base(text, offset)
        {
            _targetOffset = targetOffset;
        }

        public int TargetOffset
        {
            get { return _targetOffset; }
        }
    }

    class UncondJumpToken : JumpToken
    {
        public UncondJumpToken(int targetOffset, int offset) : base("jump to 0x" + targetOffset.ToString("X"), targetOffset, offset)
        {
        }
    }

    class JumpIfNotToken : JumpToken
    {
        private readonly BytecodeToken _condition;

        public JumpIfNotToken(int targetOffset, BytecodeToken condition, int offset)
            : base("if (!" + condition + ") jump to 0x" + targetOffset.ToString("X"), targetOffset, offset)
        {
            _condition = condition;
        }

        public BytecodeToken Condition
        {
            get { return _condition; }
        }
    }

    class ErrorBytecodeToken : BytecodeToken
    {
        public ErrorBytecodeToken(string text, int unknownBytecode, byte[] subsequentBytes, int offset) : base(text, offset)
        {
            UnknownBytecode = unknownBytecode;
            SubsequentBytes = subsequentBytes;
        }

        public int UnknownBytecode { get; private set; }
        public byte[] SubsequentBytes { get; private set; }
    }

    class EndParmsToken : BytecodeToken
    {
        public EndParmsToken(string text, int offset) : base(text, offset)
        {
        }
    }

    class DefaultValueToken : BytecodeToken
    {
        public DefaultValueToken(string text, int offset) : base(text, offset) { }
    }

    class DefaultParamValueToken : BytecodeToken
    {
        public DefaultParamValueToken(string text, int offset) : base(text, offset)
        {
        }
    }

    class ForeachToken : JumpToken
    {
        internal ForeachToken(int targetOffset, BytecodeToken expr, int offset) : this(targetOffset, expr, null, offset)
        {
        }

        internal ForeachToken(int targetOffset, BytecodeToken expr, BytecodeToken iteratorExpr, int offset)
            : base("foreach (" + expr + ") end " + targetOffset, targetOffset, offset)
        {
            Expr = expr;
            IteratorExpr = iteratorExpr;
        }

        public BytecodeToken Expr { get; private set; }
        public BytecodeToken IteratorExpr { get; private set; }
    }

    class IteratorNextToken : BytecodeToken
    {
        public IteratorNextToken(int offset) : base("IteratorNext", offset) { }
    }

    class IteratorPopToken : BytecodeToken
    {
        public IteratorPopToken(int offset) : base("IteratorPop", offset) { }
    }

    class NothingToken : BytecodeToken
    {
        public NothingToken(int offset) : base("", offset) { }
    }

    class EndOfScriptToken : BytecodeToken
    {
        public EndOfScriptToken(int offset) : base("[End of script]", offset) { }
    }

    public class LabelTableToken : BytecodeToken
    {
        private readonly Dictionary<int, string> _labels = new Dictionary<int, string>();

        public LabelTableToken(int offset) : base("", offset) { }

        public void AddLabel(string name, int offset)
        {
            _labels[offset] = name;
        }

        public string GetLabel(int offset)
        {
            string result;
            if (_labels.TryGetValue(offset, out result))
                return result;
            return null;
        }
    }

    public class BytecodeReader
    {
        public List<BytecodeToken> ReadTokens = new List<BytecodeToken>();
        public enum ME1OpCodes
        {
            EX_LocalVariable = 0x00,
            EX_InstanceVariable = 0x01,
            EX_DefaultVariable = 0x02,
            EX_Return = 0x04,
            EX_Switch = 0x05,
            EX_Jump = 0x06,
            EX_JumpIfNot = 0x07,
            EX_Stop = 0x08,
            EX_Assert = 0x09,
            EX_Case = 0x0A,
            EX_Nothing = 0x0B,
            EX_LabelTable = 0x0C,
            EX_GotoLabel = 0x0D,
            EX_EatReturnValue = 0x0E,
            EX_Let = 0x0F,
            EX_DynArrayElement = 0x10,
            EX_New = 0x11,
            EX_ClassContext = 0x12,
            EX_Metacast = 0x13,
            EX_LetBool = 0x14,
            // EX_EndParmValue = 0x15?
            EX_EndFunctionParms = 0x16,
            EX_Self = 0x17,
            EX_Skip = 0x18,
            EX_Context = 0x19,
            EX_ArrayElement = 0x1A,
            EX_VirtualFunction = 0x1B,
            EX_FinalFunction = 0x1C,
            EX_IntConst = 0x1D,
            EX_FloatConst = 0x1E,
            EX_StringConst = 0x1F,
            EX_ObjectConst = 0x20,
            EX_NameConst = 0x21,
            EX_RotationConst = 0x22,
            EX_VectorConst = 0x23,
            EX_ByteConst = 0x24,
            EX_IntZero = 0x25,
            EX_IntOne = 0x26,
            EX_True = 0x27,
            EX_False = 0x28,
            EX_NativeParm = 0x29,
            EX_NoObject = 0x2A,
            EX_IntConstByte = 0x2C,
            EX_BoolVariable = 0x2D,
            EX_DynamicCast = 0x2E,
            EX_Iterator = 0x2F,
            EX_IteratorPop = 0x30,
            EX_IteratorNext = 0x31,
            EX_StructCmpEq = 0x32,
            EX_StructCmpNe = 0x33,
            EX_UnicodeStringConst = 0x34,
            EX_StructMember = 0x35,
            EX_DynArrayLength = 0x36,
            EX_GlobalFunction = 0x37,
            EX_PrimitiveCast = 0x38,
            EX_DynArrayInsert = 0x39,
            EX_ByteToInt = 0x3A,        // EX_ReturnNothing = 0x3A
            EX_EqualEqual_DelDel = 0x3B,
            EX_NotEqual_DelDel = 0x3C,
            EX_EqualEqual_DelFunc = 0x3D,
            EX_NotEqual_DelFunc = 0x3E,
            EX_EmptyDelegate = 0x3F,
            EX_DynArrayRemove = 0x40,
            EX_DebugInfo = 0x41,
            EX_DelegateFunction = 0x42,
            EX_DelegateProperty = 0x43,
            EX_LetDelegate = 0x44,
            EX_Conditional = 0x45,
            EX_DynArrayFind = 0x46,
            EX_DynArrayFindStruct = 0x47,
            EX_LocalOutVariable = 0x48,
            EX_DefaultParmValue = 0x49,
            EX_EmptyParmValue = 0x4A,
            EX_InstanceDelegate = 0x4B,
            EX_GoW_DefaultValue = 0x50,
            EX_InterfaceContext = 0x51,
            EX_InterfaceCast = 0x52,
            EX_EndOfScript = 0x53,
            EX_DynArrayAdd = 0x54,
            EX_DynArrayAddItem = 0x55,
            EX_DynArrayRemoveItem = 0x56,
            EX_DynArrayInsertItem = 0x57,
            EX_DynArrayIterator = 0x58,

            EX_ExtendedNative = 0x60,
            EX_FirstNative = 0x70
        }
        /*private const int EX_LocalVariable = 0x00;
        private const int EX_InstanceVariable = 0x01;
        private const int EX_DefaultVariable = 0x02;
        private const int EX_Return = 0x04;
        private const int EX_Switch = 0x05;
        private const int EX_Jump = 0x06;
        private const int EX_JumpIfNot = 0x07;
        private const int EX_Stop = 0x08;
        private const int EX_Assert = 0x09;
        private const int EX_Case = 0x0A;
        private const int EX_Nothing = 0x0B;
        private const int EX_LabelTable = 0x0C;
        private const int EX_GotoLabel = 0x0D;
        private const int EX_EatReturnValue = 0x0E;
        private const int EX_Let = 0x0F;
        private const int EX_DynArrayElement = 0x10;
        private const int EX_New = 0x11;
        private const int EX_ClassContext = 0x12;
        private const int EX_Metacast = 0x13;
        private const int EX_LetBool = 0x14;
        // EX_EndParmValue = 0x15?
        private const int EX_EndFunctionParms = 0x16;
        private const int EX_Self = 0x17;
        private const int EX_Skip = 0x18;
        private const int EX_Context = 0x19;
        private const int EX_ArrayElement = 0x1A;
        private const int EX_VirtualFunction = 0x1B;
        private const int EX_FinalFunction = 0x1C;
        private const int EX_IntConst = 0x1D;
        private const int EX_FloatConst = 0x1E;
        private const int EX_StringConst = 0x1F;
        private const int EX_ObjectConst = 0x20;
        private const int EX_NameConst = 0x21;
        private const int EX_RotationConst = 0x22;
        private const int EX_VectorConst = 0x23;
        private const int EX_ByteConst = 0x24;
        private const int EX_IntZero = 0x25;
        private const int EX_IntOne = 0x26;
        private const int EX_True = 0x27;
        private const int EX_False = 0x28;
        private const int EX_NativeParm = 0x29;
        private const int EX_NoObject = 0x2A;
        private const int EX_IntConstByte = 0x2C;
        private const int EX_BoolVariable = 0x2D;
        private const int EX_DynamicCast = 0x2E;
        private const int EX_Iterator = 0x2F;
        private const int EX_IteratorPop = 0x30;
        private const int EX_IteratorNext = 0x31;
        private const int EX_StructCmpEq = 0x32;
        private const int EX_StructCmpNe = 0x33;
        private const int EX_UnicodeStringConst = 0x34;
        private const int EX_StructMember = 0x35;
        private const int EX_DynArrayLength = 0x36;
        private const int EX_GlobalFunction = 0x37;
        private const int EX_PrimitiveCast = 0x38;
        private const int EX_DynArrayInsert = 0x39;
        private const int EX_ByteToInt = 0x3A;        // EX_ReturnNothing = 0x3A
        private const int EX_EqualEqual_DelDel = 0x3B;
        private const int EX_NotEqual_DelDel = 0x3C;
        private const int EX_EqualEqual_DelFunc = 0x3D;
        private const int EX_NotEqual_DelFunc = 0x3E;
        private const int EX_EmptyDelegate = 0x3F;
        private const int EX_DynArrayRemove = 0x40;
        private const int EX_DebugInfo = 0x41;
        private const int EX_DelegateFunction = 0x42;
        private const int EX_DelegateProperty = 0x43;
        private const int EX_LetDelegate = 0x44;
        private const int EX_Conditional = 0x45;
        private const int EX_DynArrayFind = 0x46;
        private const int EX_DynArrayFindStruct = 0x47;
        private const int EX_LocalOutVariable = 0x48;
        private const int EX_DefaultParmValue = 0x49;
        private const int EX_EmptyParmValue = 0x4A;
        private const int EX_InstanceDelegate = 0x4B;
        private const int EX_GoW_DefaultValue = 0x50;
        private const int EX_InterfaceContext = 0x51;
        private const int EX_InterfaceCast = 0x52;
        private const int EX_EndOfScript = 0x53;
        private const int EX_DynArrayAdd = 0x54;
        private const int EX_DynArrayAddItem = 0x55;
        private const int EX_DynArrayRemoveItem = 0x56;
        private const int EX_DynArrayInsertItem = 0x57;
        private const int EX_DynArrayIterator = 0x58;

        private const int EX_ExtendedNative = 0x60;
        private const int EX_FirstNative = 0x70;*/

        private readonly IMEPackage _package;
        private readonly BinaryReader _reader;

        public BytecodeReader(IMEPackage package, BinaryReader reader)
        {
            _package = package;
            _reader = reader;
        }


        private ME1OpCodes[] OpCodesThatReturnNextToken = new ME1OpCodes[] { ME1OpCodes.EX_Skip, ME1OpCodes.EX_EatReturnValue, ME1OpCodes.EX_ByteToInt, ME1OpCodes.EX_BoolVariable, ME1OpCodes.EX_InterfaceContext };
        /// <summary>
        /// ME3Explorer intercepting function to build the token list. As tokens are read the tokens list will be updated.
        /// This method is used to prevent significant modifications to ReadNextInternal() (originally ReadNext)
        /// </summary>
        /// <returns></returns>
        public BytecodeToken ReadNext(bool dontAddToken = false)
        {
            ME1OpCodes b = (ME1OpCodes)_reader.ReadByte();
            _reader.BaseStream.Position--;

            if (OpCodesThatReturnNextToken.Contains(b))
            {
                BytecodeToken t = new BytecodeToken("", (int)_reader.BaseStream.Position);

                t.OpCode = b;
                ReadTokens.Add(t);
            }

            //if (b == ME1OpCodes.EX_Skip)
            //{
            //    Debugger.Break();
            //}

            //Debug.WriteLine("Read bytecode " + ((byte)b).ToString("X2") + " " + b + " at " + _reader.BaseStream.Position.ToString("X8"));
            BytecodeToken bct = ReadNextInternal();
            //Debug.WriteLine("Bytecode finished: " + ((byte)b).ToString("X2") + " " + b + " from " + _reader.BaseStream.Position.ToString("X8"));

            if (!OpCodesThatReturnNextToken.Contains(b))
            {
                bct.OpCode = b;
                ReadTokens.Add(bct);
            }
            return bct;
        }

        private BytecodeToken ReadNextInternal()
        {
            int readerpos = (int)_reader.BaseStream.Position;
            ME1OpCodes b = (ME1OpCodes)_reader.ReadByte();
            switch (b)
            {
                case ME1OpCodes.EX_LocalVariable:
                case ME1OpCodes.EX_InstanceVariable:
                case ME1OpCodes.EX_NativeParm:
                    return ReadRef(r => r.ObjectName.Instanced);

                case ME1OpCodes.EX_DefaultVariable:
                    return ReadRef(r => $"Default.{r.ObjectName.Instanced}");

                case ME1OpCodes.EX_Return:
                    {
                        BytecodeToken returnValue = ReadNext();
                        return new ReturnToken(returnValue, readerpos);
                    }
                case ME1OpCodes.EX_Assert:
                    {
                        _reader.ReadInt16();
                        _reader.ReadByte();
                        return WrapNextBytecode(c => new BytecodeToken($"assert({c})", readerpos));
                    }

                case ME1OpCodes.EX_Switch:
                    {
                        byte b1 = _reader.ReadByte();
                        BytecodeToken switchExpr = ReadNext();
                        return new SwitchToken(switchExpr.ToString(), switchExpr, readerpos);
                    }
                case ME1OpCodes.EX_Case:
                    {
                        short offset = _reader.ReadInt16();
                        if (offset == -1) return new DefaultToken(readerpos);
                        BytecodeToken caseExpr = ReadNext();
                        return new CaseToken(caseExpr.ToString(), readerpos);
                    }

                case ME1OpCodes.EX_Jump:
                    {
                        int offset = _reader.ReadInt16();
                        return new UncondJumpToken(offset, readerpos);
                    }

                case ME1OpCodes.EX_JumpIfNot:
                    {
                        short offset = _reader.ReadInt16();
                        BytecodeToken condition = ReadNext();
                        if (IsInvalid(condition)) return WrapErrToken("if (!" + condition, condition);
                        return new JumpIfNotToken(offset, condition, readerpos);
                    }

                case ME1OpCodes.EX_LabelTable:
                    {
                        var token = new LabelTableToken(readerpos);
                        while (true)
                        {
                            string labelName = ReadName();
                            if (labelName == "None") break;
                            int offset = _reader.ReadInt32();
                            token.AddLabel(labelName, offset);
                        }
                        return token;
                    }

                case ME1OpCodes.EX_GotoLabel:
                    return WrapNextBytecode(op => Token("goto " + op, readerpos));

                case ME1OpCodes.EX_Self:
                    return Token("self", readerpos);

                case ME1OpCodes.EX_Skip:
                    _reader.ReadInt16();
                    //Returning readnext causes a new token to be read
                    return ReadNext();

                case ME1OpCodes.EX_EatReturnValue:
                    _reader.ReadInt32();
                    return ReadNext();

                case ME1OpCodes.EX_Nothing:
                    return new NothingToken(readerpos);

                case ME1OpCodes.EX_Stop:
                    _reader.ReadInt16();
                    return new NothingToken(readerpos);

                case ME1OpCodes.EX_IntZero:
                    return Token("0", readerpos);

                case ME1OpCodes.EX_IntOne:
                    return Token("1", readerpos);

                case ME1OpCodes.EX_True:
                    return Token("true", readerpos);

                case ME1OpCodes.EX_False:
                    return Token("false", readerpos);

                case ME1OpCodes.EX_NoObject:
                case ME1OpCodes.EX_EmptyDelegate:
                    return Token("None", readerpos);

                case ME1OpCodes.EX_Let:
                case ME1OpCodes.EX_LetBool:
                case ME1OpCodes.EX_LetDelegate:
                    BytecodeToken lhs = ReadNext();
                    if (IsInvalid(lhs)) return lhs;
                    BytecodeToken rhs = ReadNext();
                    if (IsInvalid(rhs)) return WrapErrToken(lhs + " = " + rhs, rhs);
                    return Token(lhs + " = " + rhs, readerpos);

                case ME1OpCodes.EX_IntConst:
                    return Token(_reader.ReadInt32().ToString(), readerpos);

                case ME1OpCodes.EX_FloatConst:
                    return Token(_reader.ReadSingle().ToString(), readerpos);

                case ME1OpCodes.EX_StringConst:
                    {
                        var s = ReadAsciiz().Replace("\n", "\\n").Replace("\t", "\\t");
                        return Token($"\"{s}\"", readerpos);
                    }

                case ME1OpCodes.EX_ByteConst:
                case ME1OpCodes.EX_IntConstByte:
                    return Token(_reader.ReadByte().ToString(), readerpos);

                case ME1OpCodes.EX_ObjectConst:
                    {
                        int objectIndex = _reader.ReadInt32();
                        var item = _package.GetEntry(objectIndex);
                        if (item == null) return ErrToken("Unresolved class item " + objectIndex);
                        return Token($"{item.ClassName}'{item.ObjectName.Instanced}'", readerpos);
                    }

                case ME1OpCodes.EX_NameConst:
                    return Token($"'{ReadName()}'", readerpos);

                case ME1OpCodes.EX_EndFunctionParms:
                    return new EndParmsToken(")", readerpos);

                case ME1OpCodes.EX_ClassContext:
                case ME1OpCodes.EX_Context:
                    {
                        var context = ReadNext();
                        if (IsInvalid(context)) return context;
                        int exprSize = _reader.ReadInt16();
                        int bSize = _reader.ReadByte();
                        var value = ReadNext();
                        if (IsInvalid(value)) return WrapErrToken($"{context}.{value}", value);
                        return Token($"{context}.{value}", readerpos);
                    }

                case ME1OpCodes.EX_InterfaceContext:
                    return ReadNext();

                case ME1OpCodes.EX_FinalFunction:
                    {
                        int functionIndex = _reader.ReadInt32();
                        var item = _package.GetEntry(functionIndex);
                        if (item == null) return ErrToken("Unresolved function item " + item);
                        string functionName = item.ObjectName.Instanced;
                        return ReadCall(functionName);
                    }

                case ME1OpCodes.EX_PrimitiveCast:
                    {
                        var prefix = _reader.ReadByte();
                        var v = ReadNext();
                        return v;
                    }

                case ME1OpCodes.EX_VirtualFunction:
                    return ReadCall(ReadName());

                case ME1OpCodes.EX_GlobalFunction:
                    return ReadCall("Global." + ReadName());

                case ME1OpCodes.EX_BoolVariable:
                    return ReadNext();
                case ME1OpCodes.EX_ByteToInt:
                    int objectRefIdx = _reader.ReadInt32();
                    if (_package.IsEntry(objectRefIdx))
                    {
                        return Token($"ByteToInt({_package.getObjectName(objectRefIdx)})", readerpos);
                    }
                    else
                    {
                        return Token($"ByteToInt(Unknown reference {objectRefIdx})", readerpos);
                    }
                case ME1OpCodes.EX_DynamicCast:
                    {
                        int typeIndex = _reader.ReadInt32();
                        var item = _package.GetEntry(typeIndex);
                        return WrapNextBytecode(op => Token($"{item.ObjectName.Instanced}({op})", readerpos));
                    }

                case ME1OpCodes.EX_Metacast:
                    {
                        int typeIndex = _reader.ReadInt32();
                        var item = _package.GetEntry(typeIndex);
                        if (item == null) return ErrToken("Unresolved class item " + typeIndex);
                        return WrapNextBytecode(op => Token($"Class<{item.ObjectName.Instanced}>({op})", readerpos));
                    }

                case ME1OpCodes.EX_StructMember:
                    {
                        var field = ReadRef();
                        var structType = ReadRef();
                        int wSkip = _reader.ReadInt16();
                        var token = ReadNext();
                        if (IsInvalid(token)) return token;
                        return Token($"{token}.{field.ObjectName.Instanced}", readerpos);
                    }

                case ME1OpCodes.EX_ArrayElement:
                case ME1OpCodes.EX_DynArrayElement:
                    {
                        var index = ReadNext();
                        if (IsInvalid(index)) return index;
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        return Token($"{array}[{index}]", readerpos);
                    }

                case ME1OpCodes.EX_DynArrayLength:
                    return WrapNextBytecode(op => Token($"{op}.Length", readerpos));

                case ME1OpCodes.EX_StructCmpEq:
                    return CompareStructs("==");

                case ME1OpCodes.EX_StructCmpNe:
                    return CompareStructs("!=");

                case ME1OpCodes.EX_EndOfScript:
                    return new EndOfScriptToken(readerpos);

                case ME1OpCodes.EX_EmptyParmValue:
                case ME1OpCodes.EX_GoW_DefaultValue:
                    return new DefaultValueToken("", readerpos);

                case ME1OpCodes.EX_DefaultParmValue:
                    {
                        var size = _reader.ReadInt16();
                        var offset = _reader.BaseStream.Position;
                        var defaultValueExpr = ReadNext();
                        _reader.BaseStream.Position = offset + size;
                        return new DefaultParamValueToken(defaultValueExpr.ToString(), readerpos);
                    }

                case ME1OpCodes.EX_LocalOutVariable:
                    int valueIndex = _reader.ReadInt32();
                    var packageItem = _package.GetEntry(valueIndex);
                    if (packageItem == null) return ErrToken("Unresolved package item " + packageItem);
                    return Token(packageItem.ObjectName.Instanced, readerpos);

                case ME1OpCodes.EX_Iterator:
                    var expr = ReadNext();
                    int loopEnd = _reader.ReadInt16();
                    if (IsInvalid(expr)) return WrapErrToken("foreach " + expr, expr);
                    return new ForeachToken(loopEnd, expr, readerpos);

                case ME1OpCodes.EX_IteratorPop:
                    return new IteratorPopToken(readerpos);

                case ME1OpCodes.EX_IteratorNext:
                    return new IteratorNextToken(readerpos);

                case ME1OpCodes.EX_New:
                    var outer = ReadNext();
                    if (IsInvalid(outer)) return outer;
                    var name = ReadNext();
                    if (IsInvalid(name)) return name;
                    var flags = ReadNext();
                    if (IsInvalid(flags)) return flags;
                    var cls = ReadNext();
                    if (IsInvalid(cls)) return cls;
                    return Token($"new({JoinTokens(outer, name, flags, cls)})", readerpos);

                case ME1OpCodes.EX_VectorConst:
                    var f1 = _reader.ReadSingle();
                    var f2 = _reader.ReadSingle();
                    var f3 = _reader.ReadSingle();
                    return Token($"vect({f1},{f2},{f3})", readerpos);

                case ME1OpCodes.EX_RotationConst:
                    var i1 = _reader.ReadInt32();
                    var i2 = _reader.ReadInt32();
                    var i3 = _reader.ReadInt32();
                    return Token($"rot({i1},{i2},{i3})", readerpos);

                case ME1OpCodes.EX_InterfaceCast:
                    {
                        var interfaceName = ReadRef();
                        return WrapNextBytecode(op => Token($"{interfaceName.ObjectName.Instanced}({op})", readerpos));
                    }

                case ME1OpCodes.EX_Conditional:
                    {
                        var condition = ReadNext();
                        if (IsInvalid(condition)) return condition;
                        var trueSize = _reader.ReadInt16();
                        var pos = _reader.BaseStream.Position;
                        var truePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken($"{condition} ? {truePart}", truePart);
                        if (_reader.BaseStream.Position != pos + trueSize)
                            return ErrToken("conditional true part size mismatch");
                        var falseSize = _reader.ReadInt16();
                        pos = _reader.BaseStream.Position;
                        var falsePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken($"{condition} ? {truePart} : {falsePart}", falsePart);
                        Debug.Assert(_reader.BaseStream.Position == pos + falseSize);
                        return Token($"{condition} ? {truePart} : {falsePart}", readerpos);
                    }

                case ME1OpCodes.EX_DynArrayFind:
                    return ReadDynArray1ArgMethod("Find");

                case ME1OpCodes.EX_DynArrayFindStruct:
                    return ReadDynArray2ArgMethod("Find", true);

                case ME1OpCodes.EX_DynArrayRemove:
                    return ReadDynArray2ArgMethod("Remove", false);

                case ME1OpCodes.EX_DynArrayInsert:
                    return ReadDynArray2ArgMethod("Insert", false);

                case ME1OpCodes.EX_DynArrayAddItem:
                    return ReadDynArray1ArgMethod("AddItem");

                case ME1OpCodes.EX_DynArrayRemoveItem:
                    return ReadDynArray1ArgMethod("RemoveItem");

                case ME1OpCodes.EX_DynArrayInsertItem:
                    return ReadDynArray2ArgMethod("InsertItem", true);

                case ME1OpCodes.EX_DynArrayIterator:
                    {
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        var iteratorVar = ReadNext();
                        if (IsInvalid(iteratorVar)) return iteratorVar;
                        _reader.ReadInt16();
                        var endOffset = _reader.ReadInt16();
                        return new ForeachToken(endOffset, array, iteratorVar, readerpos);
                    }

                case ME1OpCodes.EX_DelegateProperty:
                case ME1OpCodes.EX_InstanceDelegate:
                    return Token(ReadName(), readerpos);

                case ME1OpCodes.EX_DelegateFunction:
                    {
                        var receiver = ReadNext();
                        if (IsInvalid(receiver)) return receiver;
                        var methodName = ReadName();
                        if (receiver.ToString().StartsWith("__") && receiver.ToString().EndsWith("__Delegate"))
                        {
                            return ReadCall(methodName);
                        }
                        return ReadCall(receiver + "." + methodName);
                    }

                case ME1OpCodes.EX_EqualEqual_DelDel:
                case ME1OpCodes.EX_EqualEqual_DelFunc:
                    return CompareDelegates("==");

                case ME1OpCodes.EX_NotEqual_DelDel:
                    return CompareDelegates("!=");

                default:
                    if ((int)b >= 0x60)
                    {
                        return ReadNativeCall((byte)b);
                    }
                    return ErrToken("// unknown bytecode " + ((byte)b).ToString("X2"), (int)b);
            }
        }

        private BytecodeToken CompareDelegates(string op)
        {
            int readerpos = (int)_reader.BaseStream.Position - 1;

            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            ReadNext();  // close paren
            return Token(operand1 + " " + op + " " + operand2, readerpos);
        }

        private BytecodeToken ReadDynArray1ArgMethod(string methodName)
        {
            int readerpos = (int)_reader.BaseStream.Position - 1;

            var array = ReadNext();
            if (IsInvalid(array)) return array;
            //var exprSize = _reader.ReadInt16();
            var indexer = ReadNext();
            if (IsInvalid(indexer)) return WrapErrToken(array + "." + methodName + "(" + indexer, indexer);
            return Token(array + "." + methodName + "(" + indexer + ")", readerpos);
        }

        private BytecodeToken ReadDynArray2ArgMethod(string methodName, bool skip2Bytes)
        {
            int readerpos = (int)_reader.BaseStream.Position;

            var array = ReadNext();
            if (IsInvalid(array)) return array;
            if (skip2Bytes) _reader.ReadInt16();
            var index = ReadNext();
            if (IsInvalid(index)) return index;
            var count = ReadNext();
            if (IsInvalid(count)) return count;
            return Token(array + "." + methodName + "(" + index + ", " + count + ")", readerpos);
        }

        private static string JoinTokens(params BytecodeToken[] tokens)
        {
            var result = new StringBuilder();
            foreach (var token in tokens)
            {
                string s = token.ToString();
                if (s.Length > 0)
                {
                    if (result.Length > 0) result.Append(",");
                    result.Append(s);
                }
            }
            return result.ToString();
        }

        private BytecodeToken CompareStructs(string op)
        {
            int pos = (int)_reader.BaseStream.Position - 1;

            int structIndex = _reader.ReadInt32();
            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            return Token(operand1 + " " + op + " " + operand2, pos);
        }

        internal static BytecodeToken Token(string text, int offset, int nativeindex = 0)
        {
            return new BytecodeToken(text, offset)
            {
                NativeIndex = nativeindex
            };
        }

        internal BytecodeToken ErrToken(string text, int invalidBytecode)
        {
            int pos = (int)_reader.BaseStream.Position;
            int length = (int)(_reader.BaseStream.Length - _reader.BaseStream.Position);
            byte[] subsequentBytes = _reader.ReadBytes(length);
            return new ErrorBytecodeToken(text + " " + DumpBytes(subsequentBytes, 0, -1), invalidBytecode, subsequentBytes, pos);
        }

        internal BytecodeToken ErrToken(string text)
        {
            return ErrToken(text, -1);
        }

        internal BytecodeToken WrapErrToken(string text, BytecodeToken token)
        {
            int pos = (int)_reader.BaseStream.Position;
            var errorBytecodeToken = token as ErrorBytecodeToken;
            if (errorBytecodeToken != null)
            {
                return new ErrorBytecodeToken(text, errorBytecodeToken.UnknownBytecode,
                                              errorBytecodeToken.SubsequentBytes, pos);
            }
            return ErrToken(text);
        }

        internal string ReadName()
        {
            return new NameReference(_package.GetNameEntry(_reader.ReadInt32()), _reader.ReadInt32()).Instanced;
        }

        internal IEntry ReadRef()
        {
            int idx = _reader.ReadInt32();
            return _package.GetEntry(idx);
        }

        internal BytecodeToken ReadRef(Func<IEntry, string> func)
        {
            int pos = (int)_reader.BaseStream.Position - 1; //We already read the bytecode token.

            int index = _reader.ReadInt32();
            IEntry item = _package.GetEntry(index);
            if (item == null)
            {
                return ErrToken("// unresolved reference " + index);
            }
            return Token(func(item), pos);
        }

        private BytecodeToken WrapNextBytecode(Func<BytecodeToken, BytecodeToken> func)
        {
            BytecodeToken operand = ReadNext();
            if (IsInvalid(operand)) return operand;
            return func(operand);
        }

        private string ReadAsciiz()
        {
            var result = new StringBuilder();
            char c;
            while ((c = _reader.ReadChar()) != '\0')
            {
                result.Append(c);
            }
            return result.ToString();
        }

        private static bool IsInvalid(BytecodeToken token)
        {
            return token == null || token is ErrorBytecodeToken;
        }

        public static string DumpBytes(byte[] bytes, int startIndex, int maxCount)
        {
            var result = new StringBuilder();
            for (int i = startIndex; i < bytes.Length && i < startIndex + maxCount; i++)
            {
                result.Append(bytes[i].ToString("X2")).Append(" ");
            }
            return result.ToString();
        }

        private BytecodeToken ReadCall(string functionName)
        {
            int pos = (int)_reader.BaseStream.Position - 1;

            BytecodeToken p;
            var builder = new StringBuilder(functionName + "(");
            int count = 0;
            do
            {
                try
                {
                    p = ReadNext();
                }
                catch (EndOfStreamException)
                {
                    return ErrToken("EOF exception reading call parameters");
                }
                if (IsInvalid(p)) return WrapErrToken(builder + p.ToString(), p);
                if (!(p is DefaultValueToken))
                {
                    if (count > 0 && !(p is EndParmsToken)) builder.Append(", ");
                    builder.Append(p);
                    count++;
                }
            } while (!(p is EndParmsToken));
            return Token(builder.ToString(), pos);
        }

        private BytecodeToken ReadNativeCall(byte b)
        {
            int pos = (int)_reader.BaseStream.Position - 1;

            int nativeIndex;
            if ((b & 0xF0) == 0x60)
            {
                byte b2 = _reader.ReadByte();
                nativeIndex = ((b - 0x60) << 8) + b2;
            }
            else
            {
                nativeIndex = b;
            }

            var function = CachedNativeFunctionInfo.GetNativeFunction(nativeIndex); //have to figure out how to do this, it's looking up name of native function
            if (function == null) return ErrToken("// invalid native function " + nativeIndex);
            if (function.PreOperator || function.PostOperator)
            {
                var p = ReadNext();
                if (IsInvalid(p)) return p;
                ReadNext();   // end of parms
                if (function.PreOperator)
                    return Token((function.HumanReadableControlToken ?? function.Name) + p, pos, nativeIndex);
                return Token(p + (function.HumanReadableControlToken ?? function.Name), pos, nativeIndex);
            }
            if (function.Operator)
            {
                var p1 = ReadNext();
                if (IsInvalid(p1)) return WrapErrToken(function.Name + "(" + p1, p1);
                var p2 = ReadNext();
                if (IsInvalid(p2)) return WrapErrToken(p1 + " " + function.Name + " " + p2, p2);
                ReadNext();  // end of parms
                return Token(p1 + " " + (function.HumanReadableControlToken ?? function.Name) + " " + p2, pos, nativeIndex);
            }
            return ReadCall(function.Name);
        }
    }

    public class CachedNativeFunctionInfo
    {
        public static Dictionary<int, CachedNativeFunctionInfo> NativeFunctionInfo;

        public int nativeIndex;
        public bool PreOperator;
        public bool PostOperator;
        public bool Operator;
        public string Name;
        public string Filename;
        public string HumanReadableControlToken; //like ==, ++, etc

        internal static CachedNativeFunctionInfo GetNativeFunction(int index)
        {
            CachedNativeFunctionInfo result;
            if (NativeFunctionInfo == null)
            {
                LoadME1NativeFunctionsInfo();
            }
            if (NativeFunctionInfo != null) //file check
            {
                if (!NativeFunctionInfo.TryGetValue(index, out result)) return null;
                return result;
            }
            return null;
        }

        internal static void LoadME1NativeFunctionsInfo()
        {
            string path = Application.StartupPath + "//exec//ME1NativeFunctionInfo.json";

            try
            {
                if (File.Exists(path))
                {
                    string raw = File.ReadAllText(path);
                    var blob = JsonConvert.DeserializeAnonymousType(raw, new { NativeFunctionInfo });
                    NativeFunctionInfo = blob.NativeFunctionInfo;
                    //Classes = blob.Classes;
                    //Structs = blob.Structs;
                    //Enums = blob.Enums;
                }
            }
            catch
            {
                return;
            }
        }
    }
}
