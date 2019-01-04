using ME3Explorer.Packages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace ME3Explorer.ME1.Unreal.UnhoodBytecode
{

    public class BytecodeToken
    {
        private string _text;

        public BytecodeToken(string text)
        {
            _text = text;
        }

        public override string ToString()
        {
            return _text;
        }
    }

    class ReturnToken : BytecodeToken
    {
        private readonly BytecodeToken _returnValue;

        public ReturnToken(BytecodeToken returnValue)
            : base(returnValue.ToString().Length > 0 ? "return " + returnValue : "return")
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
        public SwitchToken(string text, BytecodeToken expr) : base("switch (" + text + ")")
        {
            Expr = expr;
        }

        public BytecodeToken Expr { get; private set; }
    }

    class CaseToken : BytecodeToken
    {
        public CaseToken(string text) : base("case " + text)
        {
        }
    }

    class DefaultToken : BytecodeToken
    {
        public DefaultToken() : base("default")
        {
        }
    }

    abstract class JumpToken : BytecodeToken
    {
        private readonly int _targetOffset;

        protected JumpToken(string text, int targetOffset) : base(text)
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
        public UncondJumpToken(int targetOffset) : base("jump " + targetOffset, targetOffset)
        {
        }
    }

    class JumpIfNotToken : JumpToken
    {
        private readonly BytecodeToken _condition;

        public JumpIfNotToken(int targetOffset, BytecodeToken condition)
            : base("if (!" + condition + ") jump " + targetOffset, targetOffset)
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
        public ErrorBytecodeToken(string text, int unknownBytecode, byte[] subsequentBytes) : base(text)
        {
            UnknownBytecode = unknownBytecode;
            SubsequentBytes = subsequentBytes;
        }

        public int UnknownBytecode { get; private set; }
        public byte[] SubsequentBytes { get; private set; }
    }

    class EndParmsToken : BytecodeToken
    {
        public EndParmsToken(string text) : base(text)
        {
        }
    }

    class DefaultValueToken : BytecodeToken
    {
        public DefaultValueToken(string text) : base(text) { }
    }

    class DefaultParamValueToken : BytecodeToken
    {
        public DefaultParamValueToken(string text) : base(text)
        {
        }
    }

    class ForeachToken : JumpToken
    {
        internal ForeachToken(int targetOffset, BytecodeToken expr) : this(targetOffset, expr, null)
        {
        }

        internal ForeachToken(int targetOffset, BytecodeToken expr, BytecodeToken iteratorExpr)
            : base("foreach (" + expr + ") end " + targetOffset, targetOffset)
        {
            Expr = expr;
            IteratorExpr = iteratorExpr;
        }

        public BytecodeToken Expr { get; private set; }
        public BytecodeToken IteratorExpr { get; private set; }
    }

    class IteratorNextToken : BytecodeToken
    {
        public IteratorNextToken() : base("IteratorNext") { }
    }

    class IteratorPopToken : BytecodeToken
    {
        public IteratorPopToken() : base("IteratorPop") { }
    }

    class NothingToken : BytecodeToken
    {
        public NothingToken() : base("") { }
    }

    class EndOfScriptToken : BytecodeToken
    {
        public EndOfScriptToken() : base("") { }
    }

    public class LabelTableToken : BytecodeToken
    {
        private readonly Dictionary<int, string> _labels = new Dictionary<int, string>();

        public LabelTableToken() : base("") { }

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

    class BytecodeReader
    {
        private const int EX_LocalVariable = 0x00;
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
        private const int EX_FirstNative = 0x70;

        private readonly IMEPackage _package;
        private readonly BinaryReader _reader;

        public BytecodeReader(IMEPackage package, BinaryReader reader)
        {
            _package = package;
            _reader = reader;
        }

        internal BytecodeToken ReadNext()
        {
            byte b = _reader.ReadByte();
            switch (b)
            {
                case EX_LocalVariable:
                case EX_InstanceVariable:
                case EX_NativeParm:
                    return ReadRef(r => r.ObjectName);

                case EX_DefaultVariable:
                    return ReadRef(r => "Default." + r.ObjectName);

                case EX_Return:
                    BytecodeToken returnValue = ReadNext();
                    return new ReturnToken(returnValue);

                case EX_Assert:
                    _reader.ReadInt16();
                    _reader.ReadByte();
                    return WrapNextBytecode(c => new BytecodeToken("assert(" + c + ")"));

                case EX_Switch:
                    byte b1 = _reader.ReadByte();
                    BytecodeToken switchExpr = ReadNext();
                    return new SwitchToken(switchExpr.ToString(), switchExpr);


                case EX_Case:
                    {
                        short offset = _reader.ReadInt16();
                        if (offset == -1) return new DefaultToken();
                        BytecodeToken caseExpr = ReadNext();
                        return new CaseToken(caseExpr.ToString());
                    }

                case EX_Jump:
                    {
                        int offset = _reader.ReadInt16();
                        return new UncondJumpToken(offset);
                    }

                case EX_JumpIfNot:
                    {
                        short offset = _reader.ReadInt16();
                        BytecodeToken condition = ReadNext();
                        if (IsInvalid(condition)) return WrapErrToken("if (!" + condition, condition);
                        return new JumpIfNotToken(offset, condition);
                    }

                case EX_LabelTable:
                    {
                        var token = new LabelTableToken();
                        while (true)
                        {
                            string labelName = ReadName();
                            if (labelName == "None") break;
                            int offset = _reader.ReadInt32();
                            token.AddLabel(labelName, offset);
                        }
                        return token;
                    }

                case EX_GotoLabel:
                    return WrapNextBytecode(op => Token("goto " + op));

                case EX_Self:
                    return Token("self");

                case EX_Skip:
                    _reader.ReadInt16();
                    return ReadNext();

                case EX_EatReturnValue:
                    _reader.ReadInt32();
                    return ReadNext();

                case EX_Nothing:
                    return new NothingToken();

                case EX_Stop:
                    _reader.ReadInt16();
                    return new NothingToken();

                case EX_IntZero:
                    return Token("0");

                case EX_IntOne:
                    return Token("1");

                case EX_True:
                    return Token("true");

                case EX_False:
                    return Token("false");

                case EX_NoObject:
                case EX_EmptyDelegate:
                    return Token("None");

                case EX_Let:
                case EX_LetBool:
                case EX_LetDelegate:
                    BytecodeToken lhs = ReadNext();
                    if (IsInvalid(lhs)) return lhs;
                    BytecodeToken rhs = ReadNext();
                    if (IsInvalid(rhs)) return WrapErrToken(lhs + " = " + rhs, rhs);
                    return Token(lhs + " = " + rhs);

                case EX_IntConst:
                    return Token(_reader.ReadInt32().ToString());

                case EX_FloatConst:
                    return Token(_reader.ReadSingle().ToString());

                case EX_StringConst:
                    {
                        var s = ReadAsciiz().Replace("\n", "\\n").Replace("\t", "\\t");
                        return Token("\"" + s + "\"");
                    }

                case EX_ByteConst:
                case EX_IntConstByte:
                    return Token(_reader.ReadByte().ToString());

                case EX_ObjectConst:
                    {
                        int objectIndex = _reader.ReadInt32();
                        var item = _package.getEntry(objectIndex);
                        if (item == null) return ErrToken("Unresolved class item " + objectIndex);
                        return Token(item.ClassName + "'" + item.ObjectName + "'");
                    }

                case EX_NameConst:
                    return Token("'" + _package.getNameEntry((int)_reader.ReadInt64()) + "'");

                case EX_EndFunctionParms:
                    return new EndParmsToken(")");

                case EX_ClassContext:
                case EX_Context:
                    {
                        var context = ReadNext();
                        if (IsInvalid(context)) return context;
                        int exprSize = _reader.ReadInt16();
                        int bSize = _reader.ReadByte();
                        var value = ReadNext();
                        if (IsInvalid(value)) return WrapErrToken(context + "." + value, value);
                        return Token(context + "." + value);
                    }

                case EX_InterfaceContext:
                    return ReadNext();

                case EX_FinalFunction:
                    {
                        int functionIndex = _reader.ReadInt32();
                        var item = _package.getEntry(functionIndex);
                        if (item == null) return ErrToken("Unresolved function item " + item);
                        string functionName = item.ObjectName;
                        return ReadCall(functionName);
                    }

                case EX_PrimitiveCast:
                    {
                        var prefix = _reader.ReadByte();
                        var v = ReadNext();
                        return v;
                    }

                case EX_VirtualFunction:
                    return ReadCall(ReadName());

                case EX_GlobalFunction:
                    return ReadCall("Global." + ReadName());

                case EX_BoolVariable:
                case EX_ByteToInt:
                    return ReadNext();

                case EX_DynamicCast:
                    {
                        int typeIndex = _reader.ReadInt32();
                        var item = _package.getEntry(typeIndex);
                        return WrapNextBytecode(op => Token(item.ObjectName + "(" + op + ")"));
                    }

                case EX_Metacast:
                    {
                        int typeIndex = _reader.ReadInt32();
                        var item = _package.getEntry(typeIndex);
                        if (item == null) return ErrToken("Unresolved class item " + typeIndex);
                        return WrapNextBytecode(op => Token("Class<" + item.ObjectName + ">(" + op + ")"));
                    }

                case EX_StructMember:
                    {
                        var field = ReadRef();
                        var structType = ReadRef();
                        int wSkip = _reader.ReadInt16();
                        var token = ReadNext();
                        if (IsInvalid(token)) return token;
                        return Token(token + "." + field.ObjectName);
                    }

                case EX_ArrayElement:
                case EX_DynArrayElement:
                    {
                        var index = ReadNext();
                        if (IsInvalid(index)) return index;
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        return Token(array + "[" + index + "]");
                    }

                case EX_DynArrayLength:
                    return WrapNextBytecode(op => Token(op + ".Length"));

                case EX_StructCmpEq:
                    return CompareStructs("==");

                case EX_StructCmpNe:
                    return CompareStructs("!=");

                case EX_EndOfScript:
                    return new EndOfScriptToken();

                case EX_EmptyParmValue:
                case EX_GoW_DefaultValue:
                    return new DefaultValueToken("");

                case EX_DefaultParmValue:
                    {
                        var size = _reader.ReadInt16();
                        var offset = _reader.BaseStream.Position;
                        var defaultValueExpr = ReadNext();
                        _reader.BaseStream.Position = offset + size;
                        return new DefaultParamValueToken(defaultValueExpr.ToString());
                    }

                case EX_LocalOutVariable:
                    int valueIndex = _reader.ReadInt32();
                    var packageItem = _package.getEntry(valueIndex);
                    if (packageItem == null) return ErrToken("Unresolved package item " + packageItem);
                    return Token(packageItem.ObjectName);

                case EX_Iterator:
                    var expr = ReadNext();
                    int loopEnd = _reader.ReadInt16();
                    if (IsInvalid(expr)) return WrapErrToken("foreach " + expr, expr);
                    return new ForeachToken(loopEnd, expr);

                case EX_IteratorPop:
                    return new IteratorPopToken();

                case EX_IteratorNext:
                    return new IteratorNextToken();

                case EX_New:
                    var outer = ReadNext();
                    if (IsInvalid(outer)) return outer;
                    var name = ReadNext();
                    if (IsInvalid(name)) return name;
                    var flags = ReadNext();
                    if (IsInvalid(flags)) return flags;
                    var cls = ReadNext();
                    if (IsInvalid(cls)) return cls;
                    return Token("new(" + JoinTokens(outer, name, flags, cls) + ")");

                case EX_VectorConst:
                    var f1 = _reader.ReadSingle();
                    var f2 = _reader.ReadSingle();
                    var f3 = _reader.ReadSingle();
                    return Token("vect(" + f1 + "," + f2 + "," + f3 + ")");

                case EX_RotationConst:
                    var i1 = _reader.ReadInt32();
                    var i2 = _reader.ReadInt32();
                    var i3 = _reader.ReadInt32();
                    return Token("rot(" + i1 + "," + i2 + "," + i3 + ")");

                case EX_InterfaceCast:
                    {
                        var interfaceName = ReadRef();
                        return WrapNextBytecode(op => Token(interfaceName.ObjectName + "(" + op + ")"));
                    }

                case EX_Conditional:
                    {
                        var condition = ReadNext();
                        if (IsInvalid(condition)) return condition;
                        var trueSize = _reader.ReadInt16();
                        var pos = _reader.BaseStream.Position;
                        var truePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken(condition + " ? " + truePart, truePart);
                        if (_reader.BaseStream.Position != pos + trueSize)
                            return ErrToken("conditional true part size mismatch");
                        var falseSize = _reader.ReadInt16();
                        pos = _reader.BaseStream.Position;
                        var falsePart = ReadNext();
                        if (IsInvalid(truePart)) return WrapErrToken(condition + " ? " + truePart + " : " + falsePart, falsePart);
                        Debug.Assert(_reader.BaseStream.Position == pos + falseSize);
                        return Token(condition + " ? " + truePart + " : " + falsePart);
                    }

                case EX_DynArrayFind:
                    return ReadDynArray1ArgMethod("Find");

                case EX_DynArrayFindStruct:
                    return ReadDynArray2ArgMethod("Find", true);

                case EX_DynArrayRemove:
                    return ReadDynArray2ArgMethod("Remove", false);

                case EX_DynArrayInsert:
                    return ReadDynArray2ArgMethod("Insert", false);

                case EX_DynArrayAddItem:
                    return ReadDynArray1ArgMethod("AddItem");

                case EX_DynArrayRemoveItem:
                    return ReadDynArray1ArgMethod("RemoveItem");

                case EX_DynArrayInsertItem:
                    return ReadDynArray2ArgMethod("InsertItem", true);

                case EX_DynArrayIterator:
                    {
                        var array = ReadNext();
                        if (IsInvalid(array)) return array;
                        var iteratorVar = ReadNext();
                        if (IsInvalid(iteratorVar)) return iteratorVar;
                        _reader.ReadInt16();
                        var endOffset = _reader.ReadInt16();
                        return new ForeachToken(endOffset, array, iteratorVar);
                    }

                case EX_DelegateProperty:
                case EX_InstanceDelegate:
                    return Token(ReadName());

                case EX_DelegateFunction:
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

                case EX_EqualEqual_DelDel:
                case EX_EqualEqual_DelFunc:
                    return CompareDelegates("==");

                case EX_NotEqual_DelDel:
                    return CompareDelegates("!=");

                default:
                    if (b >= 0x60)
                    {
                        return ReadNativeCall(b);
                    }
                    return ErrToken("// unknown bytecode " + b.ToString("X2"), b);
            }
        }

        private BytecodeToken CompareDelegates(string op)
        {
            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            ReadNext();  // close paren
            return Token(operand1 + " " + op + " " + operand2);
        }

        private BytecodeToken ReadDynArray1ArgMethod(string methodName)
        {
            var array = ReadNext();
            if (IsInvalid(array)) return array;
            var exprSize = _reader.ReadInt16();
            var indexer = ReadNext();
            if (IsInvalid(indexer)) return WrapErrToken(array + "." + methodName + "(" + indexer, indexer);
            return Token(array + "." + methodName + "(" + indexer + ")");
        }

        private BytecodeToken ReadDynArray2ArgMethod(string methodName, bool skip2Bytes)
        {
            var array = ReadNext();
            if (IsInvalid(array)) return array;
            if (skip2Bytes) _reader.ReadInt16();
            var index = ReadNext();
            if (IsInvalid(index)) return index;
            var count = ReadNext();
            if (IsInvalid(count)) return count;
            return Token(array + "." + methodName + "(" + index + ", " + count + ")");
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
            int structIndex = _reader.ReadInt32();
            var operand1 = ReadNext();
            if (IsInvalid(operand1)) return operand1;
            var operand2 = ReadNext();
            if (IsInvalid(operand2)) return operand2;
            return Token(operand1 + " " + op + " " + operand2);
        }

        internal static BytecodeToken Token(string text)
        {
            return new BytecodeToken(text);
        }

        internal BytecodeToken ErrToken(string text, int invalidBytecode)
        {
            int length = (int)(_reader.BaseStream.Length - _reader.BaseStream.Position);
            byte[] subsequentBytes = _reader.ReadBytes(length);
            return new ErrorBytecodeToken(text + " " + DumpBytes(subsequentBytes, 0, -1), invalidBytecode, subsequentBytes);
        }

        internal BytecodeToken ErrToken(string text)
        {
            return ErrToken(text, -1);
        }

        internal BytecodeToken WrapErrToken(string text, BytecodeToken token)
        {
            var errorBytecodeToken = token as ErrorBytecodeToken;
            if (errorBytecodeToken != null)
            {
                return new ErrorBytecodeToken(text, errorBytecodeToken.UnknownBytecode,
                                              errorBytecodeToken.SubsequentBytes);
            }
            return ErrToken(text);
        }

        internal string ReadName()
        {
            var nameIndex = _reader.ReadInt64();
            return _package.getNameEntry((int)nameIndex);
        }

        internal IEntry ReadRef()
        {
            return _package.getEntry(_reader.ReadInt32());
        }

        internal BytecodeToken ReadRef(Func<IEntry, string> func)
        {
            int index = _reader.ReadInt32();
            IEntry item = _package.getEntry(index);
            if (item == null)
            {
                return ErrToken("// unresolved reference " + index);
            }
            return Token(func(item));
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
            return Token(builder.ToString());
        }

        private BytecodeToken ReadNativeCall(byte b)
        {
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
                    return Token(function.Name + p);
                return Token(p + function.Name);
            }
            if (function.Operator)
            {
                var p1 = ReadNext();
                if (IsInvalid(p1)) return WrapErrToken(function.Name + "(" + p1, p1);
                var p2 = ReadNext();
                if (IsInvalid(p2)) return WrapErrToken(p1 + " " + function.Name + " " + p2, p2);
                ReadNext();  // end of parms
                return Token(p1 + " " + function.Name + " " + p2);
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
