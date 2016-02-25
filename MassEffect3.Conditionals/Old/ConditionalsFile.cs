using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using MassEffect3.Core.Extensions;
using MassEffect3.Core.Extensions.IO;
using MassEffect3.Core.IO;

namespace MassEffect3.Conditionals
{
	public class ConditionalsFile
	{
		public const int ValidHeaderId = 0x434F4E44;

		private readonly Dictionary<uint, byte[]> _buffers
			= new Dictionary<uint, byte[]>();

		private readonly Dictionary<int, uint> _conditionals
			= new Dictionary<int, uint>();

		private readonly StringComparison _stringComparison;
		public ByteOrder Endian;
		public uint Version;
		private byte[] _buffer;
		private int _bufferSize;
		private ByteOrder _byteOrder;
		private List<int> _curRefBool;
		private List<int> _curRefInt;
		private TokenNode _tokenNode;
		private int _tokenPos;
		private List<Token> _tokens;
		private int _unknownInt16;

		public ConditionalsFile(string path = "")
		{
			Entries = new ConditionalEntries();
			_stringComparison = StringComparison.OrdinalIgnoreCase;
			ByteOrder = ByteOrder.LittleEndian;

			if (!path.IsNullOrEmpty())
			{
				Open(path);
			}
		}

		public byte[] Code { get; set; }

		public ConditionalEntries Entries { get; set; }

		public ByteOrderConverter Converter { get; protected set; }

		public ByteOrder ByteOrder
		{
			get { return _byteOrder; }
			set
			{
				Converter = (value == ByteOrder.BigEndian) ? ByteOrderConverter.BigEndian : ByteOrderConverter.LittleEndian;
				_byteOrder = value;
			}
		}

		public string DetectCondition(byte[] buffer)
		{
			_curRefBool = new List<int>();
			_curRefInt = new List<int>();

			return GetBoolConditional(buffer);
		}

		public string GenerateCode()
		{
			if (_tokenNode == null)
			{
				_tokenNode = new TokenNode("Root");
			}

			Code = CreateExpression(_tokenNode);

			var x = 0;
			var ascii = "";
			var outs = "";

			for (var j = 0; j < Code.Length; j++)
			{
				x++;

				if (x == 17)
				{
					x = 1;
					outs += string.Format("{0}\n{1}\t", ascii, j.ToString("X"));
					ascii = "";
				}

				var n = Code[j];

				if (n > 15)
				{
					outs += n.ToString("X") + " ";
				}
				else
				{
					outs += "0" + n.ToString("X") + " ";
				}

				if (n > 31)
				{
					ascii += (char) n;
				}
				else
				{
					ascii += ".";
				}
			}

			if (x == 16)
			{
				outs += ascii;
			}
			else
			{
				for (var j = 0; j < 16 - x; j++)
				{
					outs += "   ";
				}

				outs += ascii;
			}

			return outs;
		}

		public byte[] CreateBool(TokenNode node)
		{
			var result = new byte[0];

			if (node.Text != "bool")
			{
				return result;
			}

			result = new byte[2];
			result[0] = 0;

			var n1 = node.Nodes[0];

			if (n1.Text == "true")
			{
				result[1] = 1;
			}

			return result;
		}

		public byte[] CreateExpression(TokenNode node = null)
		{
			if (node == null)
			{
				node = _tokenNode;
			}

			var result = new byte[0];

			if (node.Text == "Root")
			{
				var n1 = node.Nodes[0];

				switch (n1.Text)
				{
					case "expr":
					{
						result = CreateExpression(n1);

						break;
					}
					case "bool":
					{
						result = CreateBool(n1);

						break;
					}
					case "plot bool":
					{
						result = CreatePlotBool(n1);

						break;
					}
					case "plot int":
					{
						result = CreatePlotInt(n1);

						break;
					}
				}
			}

			if (node.Text != "expr")
			{
				return result;
			}

			var n = node.Nodes.Count;

			if (n < 3)
			{
				return result;
			}

			var compare = node.Nodes[1].Text;
			var bytes = new List<byte[]>();

			for (var i = 0; i < (n - 1) / 2; i++)
			{
				switch (node.Nodes[i * 2].Text)
				{
					case "bool":
					{
						bytes.Add(CreateBool(node.Nodes[i * 2]));

						break;
					}
					case "expr":
					{
						bytes.Add(CreateExpression(node.Nodes[i * 2]));

						break;
					}
					case "Function":
					{
						bytes.Add(CreateFunction(node.Nodes[i * 2]));

						break;
					}
					case "plot bool":
					{
						bytes.Add(CreatePlotBool(node.Nodes[i * 2]));

						break;
					}
					case "plot int":
					{
						bytes.Add(CreatePlotInt(node.Nodes[i * 2]));

						break;
					}
					case "value":
					{
						bytes.Add(CreateValue(node.Nodes[i * 2]));

						break;
					}
				}

				if (node.Nodes[i * 2 + 1].Text != compare)
				{
					return result;
				}
			}

			var negExp = false;

			switch (node.Nodes[n - 1].Text)
			{
				case "bool":
				{
					bytes.Add(CreateBool(node.Nodes[n - 1]));

					break;
				}
				case "expr":
				{
					bytes.Add(CreateExpression(node.Nodes[n - 1]));

					break;
				}
				case "true":
				{
					bytes.Add(new byte[] { 0, 1 });

					break;
				}
				case "false":
				{
					bytes.Add(new byte[] { 0, 0 });
					negExp = true;

					break;
				}
				case "plot bool":
				{
					bytes.Add(CreatePlotBool(node.Nodes[n - 1]));

					break;
				}
				case "plot int":
				{
					bytes.Add(CreatePlotInt(node.Nodes[n - 1]));

					break;
				}
				case "value":
				{
					bytes.Add(CreateValue(node.Nodes[n - 1]));

					break;
				}
			}

			var size = bytes.Count * 2 + 4 + bytes.Sum(t => t.Length);

			result = new byte[size];
			result[0] = 0x50;

			switch (compare)
			{
				case "&&":
				{
					result[1] = 4;

					break;
				}
				case "||":
				{
					result[1] = 5;

					break;
				}
				case "==":
				{
					result[1] = 7;

					break;
				}
				case "!=":
				{
					result[1] = 8;

					break;
				}
				case "<":
				{
					result[1] = 9;

					break;
				}
				case "<=":
				{
					result[1] = 10;

					break;
				}
				case ">":
				{
					result[1] = 11;

					break;
				}
				case ">=":
				{
					result[1] = 12;

					break;
				}
			}

			if (negExp)
			{
				result[1] = 6;
			}

			var hsize = (short) (bytes.Count * 2);
			var count = BitConverter.GetBytes((short) bytes.Count);

			result[2] = count[0];
			result[3] = count[1];

			for (var i = 0; i < bytes.Count; i++)
			{
				var buff = BitConverter.GetBytes(hsize);

				result[i * 2 + 4] = buff[0];
				result[i * 2 + 5] = buff[1];
				hsize += (short) bytes[i].Length;
			}

			hsize = (short) (bytes.Count * 2 + 4);

			foreach (var t in bytes)
			{
				for (var j = 0; j < t.Length; j++)
				{
					result[hsize + j] = t[j];
				}

				hsize += (short) t.Length;
			}

			return result;
		}

		public byte[] CreateFunction(TokenNode node)
		{
			var result = new byte[0];

			//if (node.Text != "Function")
			if (!node.Text.Equals("Function", _stringComparison))
			{
				return result;
			}

			var n1 = node.Nodes[0];
			var n2 = node.Nodes[1];
			var s = n1.Text;
			var l = (short) s.Length;
			var buffer = BitConverter.GetBytes(l);

			result = new byte[9 + l];
			result[0] = 0x30;
			result[5] = buffer[0];
			result[6] = buffer[1];

			for (var i = 0; i < l; i++)
			{
				result[9 + i] = (byte) s[i];
			}

			return result;
		}

		public byte[] CreatePlotBool(TokenNode node)
		{
			var result = new byte[0];

			if (node.Text != "plot bool")
			{
				return result;
			}

			int n = Convert.ToInt16(node.Nodes[0].Text);
			var buffer = BitConverter.GetBytes(n);

			result = new byte[5];
			result[0] = 0x60;
			result[1] = buffer[0];
			result[2] = buffer[1];
			result[3] = buffer[2];
			result[4] = buffer[3];

			return result;
		}

		public byte[] CreatePlotInt(TokenNode node)
		{
			var result = new byte[0];

			if (node.Text != "plot int")
			{
				return result;
			}

			int n = Convert.ToInt16(node.Nodes[0].Text);
			var buffer = BitConverter.GetBytes(n);

			result = new byte[5];
			result[0] = 0x61;
			result[1] = buffer[0];
			result[2] = buffer[1];
			result[3] = buffer[2];
			result[4] = buffer[3];

			return result;
		}

		public byte[] CreateValue(TokenNode node)
		{
			var result = new byte[0];

			if (node.Text != "value")
			{
				return result;
			}

			int n = Convert.ToInt16(node.Nodes[0].Text);
			var buffer = BitConverter.GetBytes(n);

			result = new byte[5];
			result[0] = 0x31;
			result[1] = buffer[0];
			result[2] = buffer[1];
			result[3] = buffer[2];
			result[4] = buffer[3];

			return result;
		}

		public string GetBoolConditional(byte[] buffer)
		{
			var s = "";
			var flags = buffer[0];
			var flagType = GetFlagType(flags);
			var opType = GetOpType(flags);

			switch (flagType)
			{
				case FlagType.Bool:
				{
					switch (opType)
					{
						case OpType.Argument:
						{
							var value = BitConverter.ToInt32(buffer, 1);

							if (value == -1)
							{
								s = "(arg == -1)";
								break;
							}

							var functionLength = BitConverter.ToInt16(buffer, 5);
							var tagLength = BitConverter.ToInt16(buffer, 7);
							var function = "";

							for (var i = 0; i < functionLength; i++)
							{
								function += (char) buffer[9 + i];
							}

							s = "Function :" + function + " Value:" + value;

							if (tagLength > 0)
							{
								var tag = "";

								for (var i = 0; i < tagLength; i++)
								{
									function += (char) buffer[9 + functionLength + i];
									tag += (char) buffer[9 + functionLength + i];
								}

								s += " Tag:" + tag;
							}

							break;
						}
						case OpType.Expression:
						{
							return "(" + GetBoolConditionalExpression(buffer) + ")";
						}
						case OpType.StaticBool:
						{
							//s = (buffer[1] == 1 ? "Bool true" : "Bool false");
							s = (buffer[1] == 1 ? "true" : "false");

							break;
						}
						case OpType.Table:
						{
							_curRefBool.Add(BitConverter.ToInt32(buffer, 1));

							return "plot.bools[" + BitConverter.ToInt32(buffer, 1) + "]";
						}
					}

					break;
				}
				case FlagType.Float:
				{
					s = GetFloatConditional(buffer) + " != 0";

					break;
				}
				case FlagType.Int:
				{
					s = GetIntConditional(buffer) + " != 0";

					break;
				}
			}

			return s;
		}

		public string GetBoolConditionalExpression(byte[] buffer)
		{
			var s = "";
			var op = buffer[1];

			switch (op)
			{
				case 4:
				{
					var count = BitConverter.ToUInt16(buffer, 2);

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							s += " && ";
						}

						var n = BitConverter.ToUInt16(buffer, j + 4) + 4;
						var sub = new byte[buffer.Length - n];

						for (var k = n; k < buffer.Length; k++)
						{
							sub[k - n] = buffer[k];
						}

						s += GetBoolConditional(sub);
					}

					break;
				}
				case 5:
				{
					var count = BitConverter.ToUInt16(buffer, 2);

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							s += " || ";
						}

						var n = BitConverter.ToUInt16(buffer, j + 4) + 4;
						var sub = new byte[buffer.Length - n];

						for (var k = n; k < buffer.Length; k++)
						{
							sub[k - n] = buffer[k];
						}

						s += GetBoolConditional(sub);
					}

					break;
				}
				case 6:
				{
					var n = BitConverter.ToUInt16(buffer, 4) + 4;
					var sub = new byte[buffer.Length - n];

					for (var k = n; k < buffer.Length; k++)
					{
						sub[k - n] = buffer[k];
					}

					s = GetBoolConditional(sub) + " == false";

					break;
				}
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				{
					var n = BitConverter.ToUInt16(buffer, 4) + 4;
					var sub = new byte[buffer.Length - n];

					for (var k = n; k < buffer.Length; k++)
					{
						sub[k - n] = buffer[k];
					}

					n = BitConverter.ToUInt16(buffer, 6) + 4;
					var sub2 = new byte[buffer.Length - n];

					for (var k = n; k < buffer.Length; k++)
					{
						sub2[k - n] = buffer[k];
					}

					var left = GetGenericConditional(sub);
					var right = GetGenericConditional(sub2);

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
					}

					break;
				}
			}

			return s;
		}

		public string GetFloatConditional(byte[] buffer)
		{
			var s = "";
			var flags = buffer[0];
			var flagType = GetFlagType(flags);
			var opType = GetOpType(flags);

			switch (flagType)
			{
				case FlagType.Float:
				{
					switch (opType)
					{
						case OpType.Expression:
						{
							return "(" + GetFloatConditionalExpression(buffer) + ")";
						}
						case OpType.StaticFloat:
						{
							s = BitConverter.ToSingle(buffer, 1).ToString(CultureInfo.CurrentCulture);

							break;
						}
						case OpType.Table:
						{
							s = "plot.floats[" + BitConverter.ToInt32(buffer, 1) + "]";

							break;
						}
					}

					break;
				}
				case FlagType.Int:
				{
					s = GetIntConditional(buffer);

					break;
				}
			}

			return s;
		}

		public string GetFloatConditionalExpression(byte[] buffer)
		{
			var s = "";
			var op = buffer[1];

			switch (op)
			{
				case 0:
				{
					var count = BitConverter.ToUInt16(buffer, 2);

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							s += " + ";
						}

						var n = BitConverter.ToUInt16(buffer, j + 4) + 4;
						var sub = new byte[buffer.Length - n];

						for (var k = n; k < buffer.Length; k++)
						{
							sub[k - n] = buffer[k];
						}

						s += GetFloatConditional(sub);
					}

					break;
				}
				case 2:
				{
					var count = BitConverter.ToUInt16(buffer, 2);

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							s += " * ";
						}

						var n = BitConverter.ToUInt16(buffer, j + 4) + 4;
						var sub = new byte[buffer.Length - n];

						for (var k = n; k < buffer.Length; k++)
						{
							sub[k - n] = buffer[k];
						}

						s += GetFloatConditional(sub);
					}

					break;
				}
			}

			return s;
		}

		public string GetGenericConditional(byte[] buffer)
		{
			var flags = buffer[0];
			var flagType = GetFlagType(flags);

			switch (flagType)
			{
				case FlagType.Bool:
				{
					return GetBoolConditional(buffer);
				}
				case FlagType.Float:
				{
					return GetFloatConditional(buffer);
				}
				default:
				{
					return GetIntConditional(buffer);
				}
			}
		}

		public string GetIntConditional(byte[] buffer)
		{
			var s = "";
			var flags = buffer[0];
			var flagType = GetFlagType(flags);
			var opType = GetOpType(flags);

			switch (flagType)
			{
				case FlagType.Float:
				{
					s = GetFloatConditional(buffer);

					break;
				}
				case FlagType.Int:
				{
					switch (opType)
					{
						case OpType.Argument:
						{
							var value = BitConverter.ToInt32(buffer, 1);
							s = value.ToString(CultureInfo.InvariantCulture);

							break;
						}
						case OpType.Expression:
						{
							return "(" + GetIntConditionalExpression(buffer) + ")";
						}
						case OpType.StaticInt:
						{
							s = BitConverter.ToInt32(buffer, 1).ToString(CultureInfo.CurrentCulture);

							break;
						}
						case OpType.Table:
						{
							_curRefInt.Add(BitConverter.ToInt32(buffer, 1));
							s = "plot.ints[" + BitConverter.ToInt32(buffer, 1) + "]";

							break;
						}
					}

					break;
				}
			}

			return s;
		}

		public string GetIntConditionalExpression(byte[] buffer)
		{
			var s = "";
			var op = buffer[1];

			switch (op)
			{
				case 0:
				{
					var count = BitConverter.ToUInt16(buffer, 2);

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							s += " + ";
						}

						var n = BitConverter.ToUInt16(buffer, j + 4) + 4;
						var sub = new byte[buffer.Length - n];

						for (var k = n; k < buffer.Length; k++)
						{
							sub[k - n] = buffer[k];
						}

						s += GetIntConditional(sub);
					}

					break;
				}
			}

			return s;
		}

		public bool IsPlotType(IList<Token> tokens, string plotType)
		{
			if (tokens == null)
			{
				return false;
			}

			return tokens[0].Value.Equals("plot", _stringComparison)
				   && tokens[1].Value == "."
				   && tokens[2].Value.Equals(plotType, _stringComparison)
				   && tokens[3].Value == "["
				   && tokens[4].Type == TokenType.Value
				   && tokens[5].Value == "]";
		}

		public void InitParser()
		{
			_tokenNode = new TokenNode("Root");

			Parse(0, _tokenNode);
		}

		public int Parse(int pos = 0, TokenNode node = null)
		{
			var count = _tokens.Count;

			//if (pos >= (count - 1))
			if (pos >= (count))
			{
				return 0;
			}

			var token = _tokens[pos];

			switch (token.Type)
			{
				case TokenType.Symbol:
				{
					if (token.Value == "(")
					{
						var node2 = new TokenNode("expr");

						if (node != null)
						{
							node.Nodes.Add(node2);

							return ReadExpression(pos, node2);
						}
					}

					break;
				}
				case TokenType.Word:
				{
					if (token.Value.Equals("bool", _stringComparison))
					{
						ReadBool(pos, node);

						return 2;
					}

					if (token.Value.Equals("plot", _stringComparison))
					{
						ReadPlot(pos, node);

						return 2;
					}

					if ((token.Value.Equals("true", _stringComparison))
						|| (token.Value.Equals("false", _stringComparison)))
					{
						ReadBool(pos, node);

						return 2;
					}

					break;
				}
			}

			return 0;
		}

		public int ReadBool(int pos, TokenNode node)
		{
			var count = _tokens.Count;

			//if (pos >= (count - 1))
			if (pos >= (count))
			{
				return 0;
			}

			var token = _tokens[pos];
			//var token2 = _tokens[pos + 1];

			//if (!token.Value.Equals("bool", _stringComparison))
			if (!(token.Value.Equals("true", _stringComparison))
				&& !(token.Value.Equals("false", _stringComparison)))
			{
				return 0;
			}

			if (token.Type != TokenType.Word)
			{
				return 1;
			}

			var n1 = new TokenNode("bool");
			var n2 = new TokenNode(token.Value.ToLower());

			n1.Nodes.Add(n2);

			node.Nodes.Add(n1);

			return 2;
		}

		public int ReadExpression(int pos, TokenNode node)
		{
			var count = _tokens.Count;

			if (pos >= (count - 1))
			{
				return 0;
			}

			var currentPos = pos + 1;
			var token = _tokens[currentPos];

			while (token.Value != ")")
			{
				switch (token.Type)
				{
					case TokenType.Symbol:
					{
						if (token.Value == "(")
						{
							var n1 = new TokenNode("expr");

							node.Nodes.Add(n1);

							var n = ReadExpression(currentPos, n1);

							currentPos += n;
						}
						else if (token.Value == "-")
						{
							var token2 = _tokens[currentPos + 1];

							if (token2.Type == TokenType.Value)
							{
								var n1 = new TokenNode("value");

								n1.Nodes.Add("-" + token2.Value);
								node.Nodes.Add(n1);

								currentPos += 2;
							}
						}
						else
						{
							var n1 = new TokenNode(token.Value);

							node.Nodes.Add(n1);

							currentPos++;
						}

						break;
					}
					case TokenType.Unknown:
					{
						return 0;
					}
					case TokenType.Value:
					{
						var n1 = new TokenNode("value");

						n1.Nodes.Add(token.Value);
						node.Nodes.Add(n1);
						currentPos++;

						break;
					}
					case TokenType.Word:
					{
						if (token.Value.Equals("plot", _stringComparison))
						{
							var n = ReadPlot(currentPos, node);
							currentPos += n;
						}

						if (token.Value.Equals("bool", _stringComparison))
						{
							var n = ReadBool(currentPos, node);
							currentPos += n;
						}

						if (token.Value.Equals("function", _stringComparison))
						{
							var n = ReadFunction(currentPos, node);
							currentPos += n;
						}

						if (token.Value.Equals("true", _stringComparison))
						{
							var n1 = new TokenNode("bool");
							var n2 = new TokenNode("true");

							n1.Nodes.Add(n2);
							node.Nodes.Add(n1);
							currentPos++;
						}

						if (token.Value.Equals("false", _stringComparison))
						{
							var n1 = new TokenNode("bool");
							var n2 = new TokenNode("false");

							n1.Nodes.Add(n2);
							node.Nodes.Add(n1);

							node.Nodes.Add(n1);
							currentPos++;
						}

						break;
					}
					default:
					{
						break;
					}
				}

				token = _tokens[currentPos];
			}

			return currentPos - pos + 1;
		}

		public int ReadFunction(int pos, TokenNode node)
		{
			var count = _tokens.Count;

			if (pos >= (count - 5))
			{
				return 0;
			}

			var token = _tokens[pos + 2];
			var token2 = _tokens[pos + 5];

			var n1 = new TokenNode("Function");
			var n2 = new TokenNode(token.Value);
			var n3 = new TokenNode(token2.Value);

			n1.Nodes.Add(n2);
			n1.Nodes.Add(n3);

			node.Nodes.Add(n1);

			return 6;
		}

		public int ReadPlot(int pos, TokenNode node)
		{
			if (pos > (_tokens.Count - 6))
			{
				return 0;
			}

			var tokens = new[]
			{
				_tokens[pos],
				_tokens[pos + 1],
				_tokens[pos + 2],
				_tokens[pos + 3],
				_tokens[pos + 4],
				_tokens[pos + 5]
			};

			TokenNode n1;
			TokenNode n2;

			if (IsPlotType(tokens, "bools"))
			{
				n1 = new TokenNode("plot bool");
				n2 = new TokenNode(tokens[4].Value);

				n1.Nodes.Add(n2);
				node.Nodes.Add(n1);
			}

			if (!IsPlotType(tokens, "ints"))
			{
				return 6;
			}

			n1 = new TokenNode("plot int");
			n2 = new TokenNode(tokens[4].Value);

			n1.Nodes.Add(n2);
			node.Nodes.Add(n1);

			return 6;
		}

		public void ReadString(string input)
		{
			var length = input.Length;
			var token = new Token(TokenType.String);

			for (var i = 1; (_tokenPos + i) < length; i++)
			{
				var c = input[_tokenPos + i];

				if (char.IsLetter(c))
				{
					token.Value += c;

					continue;
				}

				if (!c.IsQuote())
				{
					continue;
				}

				_tokenPos += i + 1;
				_tokens.Add(token);

				return;
			}

			_tokenPos += token.Length;
			_tokens.Add(token);
		}

		public void ReadSymbol(string input)
		{
			var c = input[_tokenPos];
			var c2 = ' ';
			var token = new Token(TokenType.Symbol);

			if (_tokenPos < (input.Length - 1))
			{
				c2 = input[_tokenPos + 1];
			}

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
				{
					token.Value += c;
					_tokenPos++;

					break;
				}
				case '=':
				case '!':
				case '&':
				case '|':
				case '<':
				case '>':
				{
					token.Value += c;
					_tokenPos++;

					if (c2 == '=' || c2 == '&' || c2 == '|' || c2 == '<')
					{
						token.Value += c2;
						_tokenPos++;
					}

					break;
				}
				default:
				{
					token.Type = 0;
					_tokenPos++;

					break;
				}
			}

			_tokens.Add(token);
		}

		public void ReadValue(string input)
		{
			var length = input.Length;
			var token = new Token(TokenType.Value);

			for (var i = 0; (_tokenPos + i) < length; i++)
			{
				var c = input[_tokenPos + i];

				if (c.IsDigit())
				{
					token.Value += c;
				}
				else
				{
					_tokenPos += i;
					_tokens.Add(token);

					return;
				}
			}

			_tokenPos += token.Length;
			_tokens.Add(token);
		}

		public void ReadWord(string input)
		{
			var length = input.Length;
			var token = new Token(TokenType.Word);

			for (var i = 0; (_tokenPos + 1) < length && (_tokenPos + i) < length; i++)
			{
				var c = input[_tokenPos + i];

				if (c.IsLetter() || c.IsDigit())
				{
					token.Value += c;
				}
				else
				{
					_tokenPos += i;
					_tokens.Add(token);

					return;
				}
			}

			_tokenPos += token.Length;
			_tokens.Add(token);
		}

		public string Tokenize(string input)
		{
			var result = "";

			_tokenPos = 0;
			_tokens = new List<Token>();

			while (_tokenPos < input.Length)
			{
				var c = input[_tokenPos];

				if (c.IsWhiteSpace())
				{
					_tokenPos++;
				}
				else if (c.IsLetter())
				{
					ReadWord(input);
				}
				else if (c.IsDigit())
				{
					ReadValue(input);
				}
				else if (c.IsQuote())
				{
					ReadString(input);
				}
				else
				{
					ReadSymbol(input);
				}
			}

			for (var i = 0; i < _tokens.Count; i++)
			{
				result += string.Format("{0} Type: {1} Value: {2}\n", i, _tokens[i].Type, _tokens[i].Value);
			}

			return result;
		}

		public int GetHighestId()
		{
			return Entries.Select(entry => entry.Id).Concat(new[]
			{
				0
			}).Max();
		}

		public static FlagType GetFlagType(byte flags)
		{
			return (FlagType) ((flags & 0x0F) >> 0);
		}

		public static OpType GetOpType(byte flags)
		{
			return (OpType) ((flags & 0xF0) >> 4);
		}

		public void GetSize(long fileSize, List<ConditionalEntry> entries = null)
		{
			if (entries == null && Entries == null)
			{
				throw new NullReferenceException();
			}

			if (entries == null && Entries != null)
			{
				entries = Entries;
			}

			if (entries == null)
			{
				throw new ArgumentNullException("entries");
			}

			for (var i = 0; i < entries.Count(); i++)
			{
				var size1 = 0;
				var size2 = entries[i].Offset;

				if (i == entries.Count - 1)
				{
					size1 = (int) fileSize;
				}
				else
				{
					for (var j = i + 1; j < entries.Count(); j++)
					{
						if (entries[j].Offset <= size2)
						{
							continue;
						}

						size1 = entries[j].Offset;

						break;
					}
				}

				var temp = entries[i];
				temp.Size = size1 - size2;
				entries[i] = temp;
			}
		}

		public void ReplaceData(int index, byte[] buffer)
		{
			var entry = Entries[index];

			entry.Data = buffer;
			entry.Size = buffer.Length;
			Entries[index] = entry;
		}

		public static ConditionalsFile Load(string path)
		{
			var result = new ConditionalsFile();

			return result;
		}

		public void Open(string path)
		{
			using (var reader = new DataReader(new FileStream(path, FileMode.Open)))
			{
				_buffer = reader.ReadBytes((int) reader.Length);

				reader.Seek(0);

				var headerId = reader.ReadInt32();

				if (headerId != ValidHeaderId)
				{
					reader.Close();

					return;
				}

				var version = reader.ReadInt32();

				if (version != 1)
				{
					reader.Close();

					return;
				}

				_unknownInt16 = reader.ReadInt16();
				var count = reader.ReadInt16();
				Entries = new ConditionalEntries();

				for (var i = 0; i < count; i++)
				{
					var temp = new ConditionalEntry
					{
						Id = reader.ReadInt32(), 
						Offset = reader.ReadInt32(), 
						ListOffset = i * 8 + 12,
						Size = -1
					};

					Entries.Add(temp);
				}

				// Sort by Offset
				Entries = new ConditionalEntries(Entries.OrderBy(entry => entry.Offset));

				GetSize(reader.BaseStream.Length);

				foreach (var entry in Entries)
				{
					entry.Data = new byte[entry.Size];

					reader.Seek(entry.Offset);
					reader.Read(entry.Data, 0, entry.Size);
				}

				// Sort by Id
				Entries = new ConditionalEntries(Entries.OrderBy(entry => entry.Id));
			}
		}

		public void Deserialize(Stream input)
		{
			var magic = input.ReadUInt32();

			if (magic != 0x434F4E44 && magic.Swap() != 0x434F4E44)
			{
				throw new FormatException();
			}

			var endian = magic == 0x434F4E44 ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

			var version = input.ReadUInt32(endian);

			if (version != 1)
			{
				throw new FormatException();
			}

			Version = version;

			var unknown08 = input.ReadUInt16(endian);
			var count = input.ReadUInt16(endian);

			var ids = new int[count];
			var offsets = new uint[count];
			for (ushort i = 0; i < count; i++)
			{
				ids[i] = input.ReadInt32(endian);
				offsets[i] = input.ReadUInt32(endian);
			}

			var sortedOffsets = offsets
				.OrderBy(o => o)
				.Distinct()
				.ToArray();

			_buffers.Clear();

			for (var i = 0; i < sortedOffsets.Length; i++)
			{
				var offset = sortedOffsets[i];

				if (offset == 0)
				{
					continue;
				}

				var nextOffset = i + 1 < sortedOffsets.Length
					? sortedOffsets[i + 1]
					: input.Length;

				input.Seek(offset, SeekOrigin.Begin);

				var length = (int) (nextOffset - offset);

				var bytes = input.ReadBytes(length);
				_buffers.Add(offset, bytes);
			}

			_conditionals.Clear();

			for (var i = 0; i < count; i++)
			{
				_conditionals.Add(ids[i], offsets[i]);
			}

			Endian = endian;
		}

		public void Serialize(Stream input)
		{
			var magic = input.ReadUInt32();

			if (magic != 0x434F4E44 && magic.Swap() != 0x434F4E44)
			{
				throw new FormatException();
			}

			var endian = magic == 0x434F4E44 ? ByteOrder.LittleEndian : ByteOrder.BigEndian;

			var version = input.ReadUInt32(endian);

			if (version != 1)
			{
				throw new FormatException();
			}

			Version = version;

			var unknown08 = input.ReadUInt16(endian);
			var count = input.ReadUInt16(endian);

			var ids = new int[count];
			var offsets = new uint[count];
			for (ushort i = 0; i < count; i++)
			{
				ids[i] = input.ReadInt32(endian);
				offsets[i] = input.ReadUInt32(endian);
			}

			var sortedOffsets = offsets
				.OrderBy(o => o)
				.Distinct()
				.ToArray();

			_buffers.Clear();

			for (var i = 0; i < sortedOffsets.Length; i++)
			{
				var offset = sortedOffsets[i];

				if (offset == 0)
				{
					continue;
				}

				var nextOffset = i + 1 < sortedOffsets.Length
					? sortedOffsets[i + 1]
					: input.Length;

				input.Seek(offset, SeekOrigin.Begin);

				var length = (int)(nextOffset - offset);

				var bytes = input.ReadBytes(length);
				_buffers.Add(offset, bytes);
			}

			_conditionals.Clear();

			for (var i = 0; i < count; i++)
			{
				_conditionals.Add(ids[i], offsets[i]);
			}

			Endian = endian;
		}

		public byte[] GetConditional(int id)
		{
			if (_conditionals.ContainsKey(id) == false)
			{
				throw new ArgumentOutOfRangeException("id");
			}

			var offset = _conditionals[id];

			if (_buffers.ContainsKey(offset) == false)
			{
				throw new InvalidOperationException();
			}

			return (byte[]) _buffers[offset].Clone();
		}
	}
}
