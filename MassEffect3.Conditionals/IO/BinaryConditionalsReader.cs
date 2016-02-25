using System;
using System.ComponentModel;
using System.IO;
using Gammtek.Conduit.IO;

namespace MassEffect3.Conditionals.IO
{
	public class BinaryConditionalsReader : DataReader
	{
		public BinaryConditionalsReader(Stream input, ByteOrder byteOrder = ByteOrder.LittleEndian)
			: base(input, byteOrder) {}

		protected BinaryTokenFlags ReadTokenFlags()
		{
			var flags = ReadByte();

			return new BinaryTokenFlags
			{
				OpType = (TokenOpType) ((flags & 0xF0) >> 4),
				ValueType = (TokenValueType) ((flags & 0x0F) >> 0)
			};
		}

		public ConditionalToken ReadBoolConditional()
		{
			var tokenFlags = ReadTokenFlags();

			switch (tokenFlags.ValueType)
			{
				case TokenValueType.Bool:
				{
					switch (tokenFlags.OpType)
					{
						case TokenOpType.Argument:
						{
							var value = ReadInt32();

							if (value == -1)
							{
								//return "(arg == -1)"
								return new ConditionalExpressionToken(value);
							}

							var functionLength = ReadInt16();
							var tagLength = ReadInt16();

							var function = new string(ReadChars(functionLength));

							break;
						}
						case TokenOpType.Expression:
						{
							break;
						}
						case TokenOpType.StaticBool:
						{
							//return ReadBoolean() ? "true" : "false"
							return new ConditionalBoolToken(ReadBoolean());
						}
						case TokenOpType.Table:
						{
							break;
						}
						default:
						{
							throw new ArgumentOutOfRangeException();
						}
					}

					break;
				}
				case TokenValueType.Int:
				{
					break;
				}
				case TokenValueType.Float:
				{
					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException();
				}
			}

			return null;
		}

		public void ReadBoolConditionalExpression() {}

		public void ReadFloatConditional()
		{
			var tokenFlags = ReadTokenFlags();

			switch (tokenFlags.ValueType)
			{
				case TokenValueType.Bool:
				{
					break;
				}
				case TokenValueType.Float:
				{
					switch (tokenFlags.OpType)
					{
						case TokenOpType.Argument:
						{
							break;
						}
						case TokenOpType.Expression:
						{
							break;
						}
						case TokenOpType.StaticFloat:
						{
							break;
						}
						case TokenOpType.Table:
						{
							break;
						}
						default:
						{
							throw new ArgumentOutOfRangeException();
						}
					}

					break;
				}
				case TokenValueType.Int:
				{
					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException();
				}
			}
		}

		public void ReadFloatConditionalExpression() {}

		public void ReadIntConditional()
		{
			var tokenFlags = ReadTokenFlags();

			switch (tokenFlags.ValueType)
			{
				case TokenValueType.Bool:
				{
					break;
				}
				case TokenValueType.Float:
				{
					break;
				}
				case TokenValueType.Int:
				{
					switch (tokenFlags.OpType)
					{
						case TokenOpType.Argument:
						{
							break;
						}
						case TokenOpType.Expression:
						{
							break;
						}
						case TokenOpType.StaticInt:
						{
							break;
						}
						case TokenOpType.Table:
						{
							break;
						}
						default:
						{
							throw new ArgumentOutOfRangeException();
						}
					}

					break;
				}
				default:
				{
					throw new ArgumentOutOfRangeException();
				}
			}
		}

		public void ReadIntConditionalExpression() {}

		protected struct BinaryTokenFlags
		{
			public BinaryTokenFlags(TokenValueType valueType = TokenValueType.Unknown, TokenOpType opType = TokenOpType.Unknown)
				: this()
			{
				ValueType = valueType;
				OpType = opType;
			}

			public TokenOpType OpType { get; set; }

			public TokenValueType ValueType { get; set; }
		}
	}
}
