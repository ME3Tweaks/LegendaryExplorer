using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using LegendaryExplorerCore.Helpers;

namespace LegendaryExplorerCore.Unreal
{
    public static class ME3ConditionalsCompiler
    {
        public static string Decompile(this CNDFile.ConditionalEntry entry)
        {
            return CondBool(entry.Data);
        }

        public static void Compile(this CNDFile.ConditionalEntry entry, string text)
        {
            entry.Data = Compile(text);
        }

        public static byte[] Compile(string text)
        {
            var compiler = new Compiler(text);
            compiler.Tokenize();
            compiler.Parser();
            try
            {
                return compiler.CodeGen();
            }
            catch (OverflowException e)
            {
                if (e.Message == "Value was either too large or too small for an Int16.")
                {
                    throw new Exception("The maximum value of a 16-bit Integer is 32,767");
                }

                throw;
            }
        }

        //totally arbitrary number, chosen to prevent save file bloat. Can be increased if necessary.
        private const int PLOT_MAX = 250_000;

        //TODO: This is barely modified ancient code and could really use a re-write.
        //on the other hand, it works! Lot to be said for that...
        //counterpoint: it can only really be said to work if you supply it with a fully accurate input. Practically no error checking

        #region Decompiler

        private struct OptFlag
        {
            public byte flagbyte;
            public byte optbyte;
        }

        private static OptFlag getOptFlag(byte b)
        {
            var temp = new OptFlag
            {
                flagbyte = (byte)((b & 0x0F) >> 0),
                optbyte = (byte)((b & 0xF0) >> 4)
            };
            return temp;
        }

        private static string CondFloatExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 0:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " + ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondFloat(sub);
                        }

                        break;
                    }

                case 2:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " * ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondFloat(sub);
                        }

                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        private static string CondFloat(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 1: //int
                    {
                        s = CondInt(buffer);
                        break;
                    }

                case 2: //float
                    {
                        switch (Flags.optbyte)
                        {
                            case 2: //float
                                {
                                    s = "f" + BitConverter.ToSingle(buffer, 1);
                                    break;
                                }

                            case 5: //expression
                                {
                                    return "(" + CondFloatExp(buffer) + ")";
                                }

                            case 6: //table
                                {
                                    s = "plot.floats[" + BitConverter.ToInt32(buffer, 1) + "]";
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }

                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        private static string CondIntExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 0:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " + ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondInt(sub);
                        }

                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        private static string CondInt(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 1: //int
                    {
                        switch (Flags.optbyte)
                        {
                            case 1: //int
                                {
                                    s = "i" + BitConverter.ToInt32(buffer, 1);
                                    break;
                                }
                            case 3: //argument
                                {
                                    var value = BitConverter.ToInt32(buffer, 1);
                                    s = "a" + value;
                                    break;
                                }

                            case 5: //expression
                                {
                                    return "(" + CondIntExp(buffer) + ")";
                                }

                            case 6: //table
                                {
                                    s = "plot.ints[" + BitConverter.ToInt32(buffer, 1) + "]";
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }

                        break;
                    }

                case 2: //float
                    {
                        s = CondFloat(buffer);
                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        private static string CondGen(byte[] buffer)
        {
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 0: //bool
                    {
                        return CondBool(buffer);
                    }

                case 1: //int
                    {
                        return CondInt(buffer);
                    }

                default:
                    {
                        return CondFloat(buffer);
                    }
            }
        }

        private static string CondBoolExp(byte[] buffer)
        {
            string s = "";
            var op = buffer[1];
            switch (op)
            {
                case 4:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " && ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondBool(sub);
                        }

                        break;
                    }

                case 5:
                    {
                        var count = BitConverter.ToUInt16(buffer, 2);
                        for (int i = 0, j = 0; i < count; i++, j += 2)
                        {
                            if (i > 0)
                                s += " || ";
                            int n = BitConverter.ToUInt16(buffer, j + 4) + 4;
                            byte[] sub = new byte[buffer.Length - n];
                            for (int k = n; k < buffer.Length; k++)
                                sub[k - n] = buffer[k];
                            s += CondBool(sub);
                        }

                        break;
                    }

                case 6:
                    {
                        int n = BitConverter.ToUInt16(buffer, 4) + 4;
                        byte[] sub = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub[k - n] = buffer[k];
                        s = CondBool(sub) + " == false";
                        break;
                    }

                case 7:
                case 8:
                case 9:
                case 10:
                case 11:
                case 12:
                    {
                        int n = BitConverter.ToUInt16(buffer, 4) + 4;
                        byte[] sub = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub[k - n] = buffer[k];
                        n = BitConverter.ToUInt16(buffer, 6) + 4;
                        byte[] sub2 = new byte[buffer.Length - n];
                        for (int k = n; k < buffer.Length; k++)
                            sub2[k - n] = buffer[k];

                        var left = CondGen(sub);
                        var right = CondGen(sub2);

                        var comparisonType = buffer[1];
                        switch (comparisonType)
                        {
                            case 7:
                                {
                                    s = left + " == " + right;
                                    break;
                                }

                            case 8:
                                {
                                    s = left + " != " + right;
                                    break;
                                }

                            case 9:
                                {
                                    s = left + " < " + right;
                                    break;
                                }

                            case 10:
                                {
                                    s = left + " <= " + right;
                                    break;
                                }

                            case 11:
                                {
                                    s = left + " > " + right;
                                    break;
                                }

                            case 12:
                                {
                                    s = left + " >= " + right;
                                    break;
                                }

                            default:
                                {
                                    break;
                                }
                        }

                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        private static string CondBool(byte[] buffer)
        {
            string s = "";
            OptFlag Flags = getOptFlag(buffer[0]);
            switch (Flags.flagbyte)
            {
                case 0: //bool
                    {
                        switch (Flags.optbyte)
                        {
                            case 0: //bool
                                {
                                    s = (buffer[1] == 1 ? "Bool true" : "Bool false");
                                    break;
                                }
                            case 3: //Argument
                                {
                                    var value = BitConverter.ToInt32(buffer, 1);
                                    if (value == -1)
                                    {
                                        s = "(arg == -1)";
                                        break;
                                    }

                                    var functionLength = BitConverter.ToInt16(buffer, 5);
                                    var tagLength = BitConverter.ToInt16(buffer, 7);
                                    string function = "";
                                    for (int i = 0; i < functionLength; i++)
                                        function += (char)buffer[9 + i];
                                    s = "Function :" + function + " Value:" + value;
                                    if (tagLength > 0)
                                    {
                                        string tag = "";
                                        for (int i = 0; i < tagLength; i++)
                                            function += (char)buffer[9 + functionLength + i];
                                        s += " Tag:" + tag;
                                    }

                                    break;
                                }
                            case 5: //expression
                                {
                                    return "(" + CondBoolExp(buffer) + ")";
                                }

                            case 6: //table
                                {
                                    return "plot.bools[" + BitConverter.ToInt32(buffer, 1) + "]";
                                }

                            default:
                                {
                                    break;
                                }
                        }

                        break;
                    }
                case 1: //int
                    {
                        s = CondInt(buffer) + " != 0";
                        break;
                    }

                case 2: //float
                    {
                        s = CondFloat(buffer) + " != 0";
                        break;
                    }

                default:
                    {
                        break;
                    }
            }

            return s;
        }

        #endregion

        #region Compiler

        private class Compiler
        {
            public struct Token
            {
                public int type;
                public string Value;
            }

            private int TokenPos;
            private readonly List<Token> tokenlist = new();
            private readonly string Text;
            private TreeNode AST;

            public Compiler(string text)
            {
                Text = text;
                CheckBracketCount(text);
            }
            private static void CheckBracketCount(string code)
            {
                int crbo, crbc, ccbo, ccbc;
                crbo = crbc = ccbo = ccbc = 0;
                code = code.Replace("\n", "");
                code = code.Replace("\r", "");
                for (int i = 0; i < code.Length; i++)
                {
                    switch (code[i])
                    {
                        case '(':
                            crbo++;
                            break;
                        case ')':
                            crbc++;
                            break;
                        case '[':
                            ccbo++;
                            int end = i + 1;
                            if (!CheckArrNumb(code, i + 1, out end))
                            {
                                string s;
                                if (i > 10)
                                    s = code.Substring(i - 10, 10 + end - i);
                                else
                                    s = code.Substring(0, end);
                                throw new Exception("Invalid Array Indexing @ : " + i + "!\n" + s);
                            }
                            break;
                        case ']':
                            ccbc++;
                            break;
                        case '&':
                            if (code[i + 1] == '&')
                                i++;
                            else
                            {
                                string s;
                                if (i > 20)
                                    s = code.Substring(i - 20, 20);
                                else
                                    s = code.Substring(0, i + 1);
                                throw new Exception("Missing '&' sign @ :\n" + s);
                            }
                            break;
                        default:
                            break;
                    }
                }
                if (crbo != crbc || ccbo != ccbc)
                {
                    if (crbo > crbc)
                        throw new Exception("Missing bracket(s) : " + (crbo - crbc) + " ')' bracket(s) missing!");
                    if (crbo < crbc)
                        throw new Exception("Missing bracket(s) : " + (crbc - crbo) + " '(' bracket(s) missing!");
                    if (ccbo > ccbc)
                        throw new Exception("Missing bracket(s) : " + (ccbo - ccbc) + " ']' bracket(s) missing!");
                    if (ccbo < ccbc)
                        throw new Exception("Missing bracket(s) : " + (ccbc - ccbo) + " '[' bracket(s) missing!");
                }
            }
            private static bool CheckArrNumb(string code, int pos, out int end)
            {
                const string pat = "0123456789 ";
                bool f;
                char c;
                while (true)
                {
                    end = pos;
                    c = code[pos++];
                    f = false;
                    for (int i = 0; i < pat.Length; i++)
                        if (pat[i] == c)
                        {
                            f = true;
                            break;
                        }
                    if (!f && c != ']')
                        return false;
                    if (!f && c == ']')
                        return true;
                    if (pos >= code.Length)
                        return false;
                }
            }

            #region Tokenizing

            public List<Token> Tokenize()
            {
                while (TokenPos < Text.Length)
                {
                    char c = Text[TokenPos];
                    if (isWhiteSpace(c))
                    {
                        TokenPos++;
                        continue;
                    }
                    if (isLetter(c))
                    {
                        ReadWord();
                        continue;
                    }
                    if (isDigit(c))
                    {
                        ReadValue();
                        continue;
                    }
                    if (isQuote(c))
                    {
                        ReadString();
                        continue;
                    }
                    ReadSymbol();
                }
                string s = "";
                for (int i = 0; i < tokenlist.Count; i++)
                {
                    s += i + " Type:" + tokenlist[i].type + " Value:" + tokenlist[i].Value + "\n";
                    if (i >= 7 && (tokenlist[i].Value is "true" or "false"))
                    {
                        if (!string.Equals(tokenlist[i - 1].Value, "bool", StringComparison.OrdinalIgnoreCase))
                        {
                            string s2 = "";
                            for (int j = i - 7; j <= i; j++)
                                s2 += tokenlist[j].Value + " ";
                            throw new Exception("Missing 'bool' before 'true'/'false' @:\n" + s2);
                        }
                    }
                }
                return tokenlist;
            }

            private static bool isWhiteSpace(char c) => c is > '\0' and <= ' ';

            private static bool isQuote(char c) => c == '\"';

            private static bool isLetter(char c) => char.IsLetter(c);

            private static bool isDigit(char c) => char.IsDigit(c);

            private void ReadString()
            {
                int len = Text.Length;
                var temp = new Token
                {
                    type = 3,
                    Value = ""
                };
                for (int i = 1; TokenPos + i < len; i++)
                {
                    char c = Text[TokenPos + i];
                    if (isLetter(c))
                    {
                        temp.Value += c;
                        continue;
                    }
                    if (c == '"')
                    {
                        TokenPos += i + 1;
                        tokenlist.Add(temp);
                        return;
                    }
                }
                TokenPos += temp.Value.Length;
                tokenlist.Add(temp);
            }

            private void ReadWord()
            {
                int len = Text.Length;
                var temp = new Token
                {
                    type = 1,
                    Value = ""
                };
                for (int i = 0; TokenPos + i < len; i++)
                {
                    char c = Text[TokenPos + i];
                    if (isLetter(c) || isDigit(c) || c == '-')
                    {
                        temp.Value += c;
                    }
                    else
                    {
                        TokenPos += i;
                        tokenlist.Add(temp);
                        return;
                    }
                }
                TokenPos += temp.Value.Length;
                tokenlist.Add(temp);
            }

            private void ReadValue()
            {
                var temp = new Token();
                int len = Text.Length;
                temp.type = 2;
                temp.Value = "";
                for (int i = 0; TokenPos + i < len; i++)
                {
                    char c = Text[TokenPos + i];
                    if (isDigit(c))
                    {
                        temp.Value += c;
                    }
                    else
                    {
                        TokenPos += i;
                        tokenlist.Add(temp);
                        return;
                    }
                }
                TokenPos += temp.Value.Length;
                tokenlist.Add(temp);
            }

            private void ReadSymbol()
            {
                char c = Text[TokenPos];
                char c2 = ' ';
                if (TokenPos < Text.Length - 1)
                    c2 = Text[TokenPos + 1];
                var temp = new Token
                {
                    type = 4,
                    Value = ""
                };
                switch (c)
                {
                    case '+':
                    case '-':
                    case '*':
                    case '/':
                    case '(':
                    case ')':
                    case '[':
                    case ']':
                    case ',':
                    case '.':
                    case ':':
                        temp.Value += c;
                        tokenlist.Add(temp);
                        TokenPos++;
                        return;
                    case '=':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 == '=')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        else
                        {
                            throw new Exception($"Unexpected operator '=' at character {TokenPos}. Did you mean '=='?");
                        }
                    case '!':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 == '=')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        tokenlist.Add(temp);
                        return;
                    case '&':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 == '&')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        else
                        {
                            throw new Exception($"Unexpected operator '&' at character {TokenPos}. Did you mean '&&'?");
                        }
                    case '|':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 == '|')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        else
                        {
                            throw new Exception($"Unexpected operator '|' at character {TokenPos}. Did you mean '||'?");
                        }
                    case '<':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 is '>' or '=')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        tokenlist.Add(temp);
                        return;
                    case '>':
                        temp.Value += c;
                        TokenPos++;
                        if (c2 == '=')
                        {
                            TokenPos++;
                            temp.Value += c2;
                            tokenlist.Add(temp);
                            return;
                        }
                        tokenlist.Add(temp);
                        return;
                    default:
                        temp.type = 0;
                        temp.Value = "";
                        tokenlist.Add(temp);
                        TokenPos++;
                        return;

                }
            }

            #endregion

            #region Parsing

            public void Parser()
            {
                AST = new TreeNode("Root");
                Parse(0, AST);
            }

            private record TreeNode(string Text)
            {
                public readonly List<TreeNode> Nodes = new();

                public static implicit operator TreeNode(string text) => new(text);
            }

            private int ReadPlot(int pos, TreeNode node)
            {
                if (pos > tokenlist.Count - 6) return 0;
                Token temp1 = tokenlist[pos];
                Token temp2 = tokenlist[pos + 1];
                Token temp3 = tokenlist[pos + 2];
                Token temp4 = tokenlist[pos + 3];
                Token temp5 = tokenlist[pos + 4];
                Token temp6 = tokenlist[pos + 5];
                if (string.Equals(temp1.Value, "plot", StringComparison.OrdinalIgnoreCase) &&
                    temp2.Value == "." &&
                    temp3.Value.CaseInsensitiveEquals("bools") &&
                    temp4.Value == "[" &&
                    temp5.type == 2 &&
                    temp6.Value == "]")
                {
                    TreeNode t1 = new TreeNode("plot bool");
                    TreeNode t2 = new TreeNode(temp5.Value);
                    t1.Nodes.Add(t2);
                    node.Nodes.Add(t1);
                }
                if (temp1.Value.CaseInsensitiveEquals("plot") &&
                    temp2.Value == "." &&
                    temp3.Value.CaseInsensitiveEquals("ints") &&
                    temp4.Value == "[" &&
                    temp5.type == 2 &&
                    temp6.Value == "]")
                {
                    TreeNode t1 = new TreeNode("plot int");
                    TreeNode t2 = new TreeNode(temp5.Value);
                    t1.Nodes.Add(t2);
                    node.Nodes.Add(t1);
                }
                if (temp1.Value.CaseInsensitiveEquals("plot") &&
                    temp2.Value == "." &&
                    temp3.Value.CaseInsensitiveEquals("floats") &&
                    temp4.Value == "[" &&
                    temp5.type == 2 &&
                    temp6.Value == "]")
                {
                    TreeNode t1 = new TreeNode("plot float");
                    TreeNode t2 = new TreeNode(temp5.Value);
                    t1.Nodes.Add(t2);
                    node.Nodes.Add(t1);
                }
                return 6;
            }

            private int ReadBool(int pos, TreeNode node)
            {
                int len = tokenlist.Count;
                if (pos >= len - 1) return 0;
                Token temp = tokenlist[pos];
                Token temp2 = tokenlist[pos + 1];
                if (temp.Value.CaseInsensitiveEquals("bool"))
                {
                    if (temp2.type == 1)
                    {
                        TreeNode t1 = new TreeNode("bool");
                        TreeNode t2 = new TreeNode(temp2.Value);
                        t1.Nodes.Add(t2);
                        node.Nodes.Add(t1);
                        return 2;
                    }
                    return 1;
                }
                return 0;
            }

            private int ReadFunc(int pos, TreeNode node)
            {
                int len = tokenlist.Count;
                if (pos >= len - 5) return 0;
                Token temp = tokenlist[pos + 2];
                Token temp2 = tokenlist[pos + 5];
                TreeNode t1 = new TreeNode("Function");
                TreeNode t2 = new TreeNode(temp.Value);
                TreeNode t3 = new TreeNode(temp2.Value);
                t1.Nodes.Add(t2);
                t1.Nodes.Add(t3);
                node.Nodes.Add(t1);
                return 6;
            }

            private int ReadExpression(int pos, TreeNode node)
            {
                if (pos >= tokenlist.Count - 1) return 0;
                int currpos = pos + 1;
                Token temp2 = tokenlist[currpos];
                Token tahead = new Token();
                if (pos < tokenlist.Count - 1)
                    tahead = tokenlist[currpos + 1];
                while (temp2.Value != ")")
                {
                    switch (temp2.type)
                    {
                        case 0: return 0;
                        case 1:
                            if (temp2.Value.CaseInsensitiveEquals("plot"))
                            {
                                int n = ReadPlot(currpos, node);
                                currpos += n;
                            }
                            else if (temp2.Value.CaseInsensitiveEquals("bool"))
                            {
                                int n = ReadBool(currpos, node);
                                currpos += n;
                            }
                            else if (temp2.Value.CaseInsensitiveEquals("function"))
                            {
                                int n = ReadFunc(currpos, node);
                                currpos += n;
                            }
                            else if (temp2.Value.CaseInsensitiveEquals("false"))
                            {
                                TreeNode t = new TreeNode("false");
                                node.Nodes.Add(t);
                                currpos++;
                            }
                            else if (temp2.Value.Length > 1)
                            {
                                if (temp2.Value[0] == 'a' && (isDigit(temp2.Value[1]) || temp2.Value[1] == '-'))
                                {
                                    TreeNode t = new TreeNode("value_a");
                                    string v = temp2.Value.Substring(1, temp2.Value.Length - 1);
                                    t.Nodes.Add(Convert.ToInt32(v).ToString());
                                    node.Nodes.Add(t);
                                    currpos++;
                                }
                                else if (temp2.Value[0] == 'i' && (isDigit(temp2.Value[1]) || temp2.Value[1] == '-'))
                                {
                                    TreeNode t = new TreeNode("value_i");
                                    string v = temp2.Value.Substring(1, temp2.Value.Length - 1);
                                    t.Nodes.Add(Convert.ToInt32(v).ToString());
                                    node.Nodes.Add(t);
                                    currpos++;
                                }
                                else if (temp2.Value[0] == 'f' && (isDigit(temp2.Value[1]) || temp2.Value[1] == '-'))
                                {
                                    TreeNode t = new TreeNode("value_f");
                                    string v = temp2.Value.Substring(1, temp2.Value.Length - 1);
                                    t.Nodes.Add(Convert.ToSingle(v).ToString());
                                    node.Nodes.Add(t);
                                    currpos++;
                                }
                            }
                            break;
                        case 2:
                            TreeNode tn = new TreeNode("value");
                            tn.Nodes.Add(temp2.Value);
                            node.Nodes.Add(tn);
                            currpos++;
                            break;
                        case 3:
                        case 4:
                            if (temp2.Value == "(")
                            {
                                TreeNode t = new TreeNode("expr");
                                node.Nodes.Add(t);
                                int n = ReadExpression(currpos, t);
                                currpos += n;
                                break;
                            }
                            else if (temp2.Value == "-")
                            {
                                Token t2 = tokenlist[currpos + 1];
                                if (t2.type == 2)
                                {
                                    TreeNode t = new TreeNode("value");
                                    t.Nodes.Add("-" + t2.Value);
                                    node.Nodes.Add(t);
                                    currpos += 2;
                                }
                                break;
                            }
                            TreeNode tmp = new TreeNode(temp2.Value);
                            node.Nodes.Add(tmp);
                            currpos++;
                            break;
                    }
                    temp2 = tokenlist[currpos];
                }
                return currpos - pos + 1;
            }

            private int Parse(int pos, TreeNode node)
            {
                int len = tokenlist.Count;
                if (pos >= len - 1) return 0;
                Token temp = tokenlist[pos];
                switch (temp.type)
                {
                    case 1://Word
                        if (temp.Value.CaseInsensitiveEquals("bool"))
                        {
                            ReadBool(pos, node);
                            return 2;
                        }
                        if (temp.Value.CaseInsensitiveEquals("plot"))
                        {
                            ReadPlot(pos, node);
                            return 2;
                        }
                        break;
                    case 2://Value
                        break;
                    case 4://Symbol
                        if (temp.Value == "(")
                        {
                            TreeNode tmp = new TreeNode("expr");
                            node.Nodes.Add(tmp);
                            return ReadExpression(pos, tmp);
                        }
                        break;
                    default:
                        return 0;
                }
                return 0;
            }

            #endregion

            #region ByteCodeGen

            private static byte[] CodeBool(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "bool")
                {
                    Cout = new byte[2];
                    Cout[0] = 0;
                    TreeNode t1 = node.Nodes[0];
                    Cout[1] = t1.Text switch
                    {
                        "true" => 1,
                        "false" => 0,
                        _ => throw new Exception($"{t1.Text} is not a valid boolean value! Expected 'true' or 'false'")
                    };
                }
                return Cout;
            }

            private static byte[] CodeIntValue(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "value" || node.Text == "value_a")
                {
                    int n = Convert.ToInt16(node.Nodes[0].Text);
                    Cout = new byte[node.Text == "value_a" ? 9 : 5];
                    Cout[0] = 0x31;
                    byte[] buff = BitConverter.GetBytes(n);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodeIntValue_i(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "value_i")
                {
                    int n = Convert.ToInt32(node.Nodes[0].Text);
                    Cout = new byte[5];
                    Cout[0] = 0x11;
                    byte[] buff = BitConverter.GetBytes(n);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodeIntValue_f(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "value_f")
                {
                    float f = Convert.ToSingle(node.Nodes[0].Text);
                    Cout = new byte[5];
                    Cout[0] = 0x22;
                    byte[] buff = BitConverter.GetBytes(f);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodePlotBool(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "plot bool")
                {
                    int n = Convert.ToInt32(node.Nodes[0].Text);
                    if (Math.Abs(n) > PLOT_MAX)
                    {
                        throw new Exception($"plot bool id cannot be greater than {PLOT_MAX}");
                    }
                    Cout = new byte[5];
                    Cout[0] = 0x60;
                    byte[] buff = BitConverter.GetBytes(n);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodePlotInt(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "plot int")
                {
                    int n = Convert.ToInt32(node.Nodes[0].Text);
                    if (Math.Abs(n) > PLOT_MAX)
                    {
                        throw new Exception($"plot int id cannot be greater than {PLOT_MAX}");
                    }
                    Cout = new byte[5];
                    Cout[0] = 0x61;
                    byte[] buff = BitConverter.GetBytes(n);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodePlotFloat(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "plot float")
                {
                    int n = Convert.ToInt32(node.Nodes[0].Text);
                    if (Math.Abs(n) > PLOT_MAX)
                    {
                        throw new Exception($"plot float id cannot be greater than {PLOT_MAX}");
                    }
                    Cout = new byte[5];
                    Cout[0] = 0x62;
                    byte[] buff = BitConverter.GetBytes(n);
                    Cout[1] = buff[0];
                    Cout[2] = buff[1];
                    Cout[3] = buff[2];
                    Cout[4] = buff[3];
                }
                return Cout;
            }

            private static byte[] CodeFunction(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "Function")
                {
                    TreeNode t1 = node.Nodes[0];
                    TreeNode t2 = node.Nodes[1];
                    string s = t1.Text;
                    short l = (short)s.Length;
                    byte[] buff = BitConverter.GetBytes(l);
                    Cout = new byte[9 + l];
                    Cout[0] = 0x30;
                    BinaryPrimitives.WriteInt32LittleEndian(Cout.AsSpan(1 ,4), int.Parse(t2.Text));
                    Cout[5] = buff[0];
                    Cout[6] = buff[1];
                    for (int i = 0; i < l; i++)
                        Cout[9 + i] = (byte)s[i];
                }
                return Cout;
            }

            private static byte GetExprType(TreeNode node)
            {
                bool isFloat = false;
                TreeNode operation = node.Nodes[1];
                switch (operation.Text)
                {
                    case "+":
                    case "-":
                    case "*":
                    case "/":
                        for (int i = 0; i < (node.Nodes.Count + 1) / 2; i++)
                        {
                            TreeNode t = node.Nodes[i * 2];
                            switch (t.Text)
                            {
                                case "plot bool":
                                    return 0;
                                case "plot float":
                                case "value_f":
                                    isFloat = true;
                                    break;
                                case "expr":
                                    int res = GetExprType(t);
                                    if (res == 0)
                                        return 0;
                                    if (res == 2)
                                        isFloat = true;
                                    break;
                            }
                        }
                        if (isFloat)
                            return 2;
                        else
                            return 1;
                    default:
                        return 0;
                }
            }

            private byte[] CodeExpr(TreeNode node)
            {
                byte[] Cout = new byte[0];
                if (node.Text == "Root")
                {
                    TreeNode t1 = node.Nodes[0];
                    switch (t1.Text)
                    {
                        case "expr":
                            Cout = CodeExpr(t1);
                            break;
                        case "bool":
                            Cout = CodeBool(t1);
                            break;
                        case "plot bool":
                            Cout = CodePlotBool(t1);
                            break;
                        case "plot int":
                            Cout = CodePlotInt(t1);
                            break;
                        case "plot float":
                            Cout = CodePlotFloat(t1);
                            break;
                    }
                }
                if (node.Text == "expr")
                {
                    int n = node.Nodes.Count;
                    if (n < 3) return Cout;
                    string compare = node.Nodes[1].Text;
                    List<byte[]> Ctmp = new List<byte[]>();
                    for (int i = 0; i < (n - 1) / 2; i++)
                    {
                        switch (node.Nodes[i * 2].Text)
                        {
                            case "plot bool":
                                Ctmp.Add(CodePlotBool(node.Nodes[i * 2]));
                                break;
                            case "plot int":
                                Ctmp.Add(CodePlotInt(node.Nodes[i * 2]));
                                break;
                            case "plot float":
                                Ctmp.Add(CodePlotFloat(node.Nodes[i * 2]));
                                break;
                            case "expr":
                                Ctmp.Add(CodeExpr(node.Nodes[i * 2]));
                                break;
                            case "bool":
                                Ctmp.Add(CodeBool(node.Nodes[i * 2]));
                                break;
                            case "value":
                            case "value_a":
                                Ctmp.Add(CodeIntValue(node.Nodes[i * 2]));
                                break;
                            case "value_i":
                                Ctmp.Add(CodeIntValue_i(node.Nodes[i * 2]));
                                break;
                            case "value_f":
                                Ctmp.Add(CodeIntValue_f(node.Nodes[i * 2]));
                                break;
                            case "Function":
                                Ctmp.Add(CodeFunction(node.Nodes[i * 2]));
                                break;
                        }
                        if (node.Nodes[i * 2 + 1].Text != compare)
                            return Cout;
                    }
                    bool negexp = false;
                    switch (node.Nodes[n - 1].Text)
                    {
                        case "plot bool":
                            Ctmp.Add(CodePlotBool(node.Nodes[n - 1]));
                            break;
                        case "plot int":
                            Ctmp.Add(CodePlotInt(node.Nodes[n - 1]));
                            break;
                        case "plot float":
                            Ctmp.Add(CodePlotFloat(node.Nodes[n - 1]));
                            break;
                        case "expr":
                            Ctmp.Add(CodeExpr(node.Nodes[n - 1]));
                            break;
                        case "bool":
                            Ctmp.Add(CodeBool(node.Nodes[n - 1]));
                            break;
                        case "value":
                        case "value_a":
                            Ctmp.Add(CodeIntValue(node.Nodes[n - 1]));
                            break;
                        case "value_i":
                            Ctmp.Add(CodeIntValue_i(node.Nodes[n - 1]));
                            break;
                        case "value_f":
                            Ctmp.Add(CodeIntValue_f(node.Nodes[n - 1]));
                            break;
                        case "false":
                            negexp = true;
                            break;
                    }
                    int size = Ctmp.Count * 2 + 4;
                    for (int i = 0; i < Ctmp.Count; i++)
                        size += Ctmp[i].Length;
                    Cout = new byte[size];
                    Cout[0] = (byte)(0x50 + GetExprType(node));
                    switch (compare)
                    {
                        case "*":
                            Cout[1] = 2;
                            break;
                        case "&&":
                            Cout[1] = 4;
                            break;
                        case "||":
                            Cout[1] = 5;
                            break;
                        case "==":
                            Cout[1] = 7;
                            break;
                        case "!=":
                            Cout[1] = 8;
                            break;
                        case "<":
                            Cout[1] = 9;
                            break;
                        case "<=":
                            Cout[1] = 10;
                            break;
                        case ">":
                            Cout[1] = 11;
                            break;
                        case ">=":
                            Cout[1] = 12;
                            break;
                    }
                    if (negexp)
                        Cout[1] = 6;
                    short hsize = (short)(Ctmp.Count * 2);
                    byte[] count = BitConverter.GetBytes((short)Ctmp.Count);
                    Cout[2] = count[0];
                    Cout[3] = count[1];
                    for (int i = 0; i < Ctmp.Count; i++)
                    {
                        byte[] buff = BitConverter.GetBytes(hsize);
                        Cout[i * 2 + 4] = buff[0];
                        Cout[i * 2 + 5] = buff[1];
                        hsize += (short)Ctmp[i].Length;
                    }
                    hsize = (short)(Ctmp.Count * 2 + 4);
                    for (int i = 0; i < Ctmp.Count; i++)
                    {
                        for (int j = 0; j < Ctmp[i].Length; j++)
                            Cout[hsize + j] = Ctmp[i][j];
                        hsize += (short)Ctmp[i].Length;
                    }
                    return Cout;
                }
                return Cout;
            }

            public byte[] CodeGen()
            {
                return CodeExpr(AST);
            }

            #endregion
        }

        #endregion
    }
}