using System;
using System.Globalization;
using System.Text;
using Gammtek.Conduit.IO;

namespace MassEffect3.ConditionalDump
{
	public static class ByteBufferReaderTest
	{
		public static string DumpConditionalGeneric(ByteBufferReader reader)
		{
			var flags = reader[0].ReadByte();
			var flagType = (FlagType) ((flags & 0x0F) >> 0);

			switch (flagType)
			{
				case FlagType.Bool:
				{
					return DumpConditionalBool(reader);
				}
				case FlagType.Int:
				{
					return DumpConditionalInt(reader);
				}
				default:
				{
					return DumpConditionalFloat(reader);
				}
			}
		}

		public static string DumpConditionalBool(ByteBufferReader reader)
		{
			var flags = reader[0].ReadByte();

			var flagType = (FlagType) ((flags & 0x0F) >> 0);
			var opType = (OpType) ((flags & 0xF0) >> 4);

			switch (flagType)
			{
				case FlagType.Bool:
				{
					switch (opType)
					{
						case OpType.StaticBool:
						{
							return reader[1].ReadBoolean() ? "true" : "false";
						}
						case OpType.Argument:
						{
							var value = reader[1].ReadInt32();
							if (value == -1)
							{
								return "(arg != 0)";
							}

							var functionLength = reader[5].ReadUInt16();
							var tagLength = reader[7].ReadUInt16();

							var function = reader[9].ReadString(functionLength, true, Encoding.ASCII);

							var sb = new StringBuilder();
							sb.Append("GetLocalVariable");
							sb.Append("(");
							sb.AppendFormat("\"{0}\"", function);
							sb.Append(", ");
							sb.AppendFormat("{0}", value);

							if (tagLength > 0)
							{
								var tag = reader[9 + functionLength].ReadString(tagLength, true, Encoding.ASCII);
								sb.Append(", ");
								sb.AppendFormat("\"{0}\"", tag);
							}

							sb.Append(")");

							return sb.ToString();
						}
						case OpType.Expression:
						{
							return "(" + DumpConditionalBoolExpression(reader) + ")";
						}
						case OpType.Table:
						{
							return "plot.bools[" + reader[1].ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				case FlagType.Int:
				{
					return DumpConditionalInt(reader) + " != 0";
				}
				case FlagType.Float:
				{
					return DumpConditionalFloat(reader) + " != 0";
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalBoolExpression(ByteBufferReader reader)
		{
			var op = reader[1].ReadByte();
			switch (op)
			{
				case 4:
				{
					var count = reader[2].ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" && ");
						}

						var r = reader[j + 4];
						var seek = r.ReadUInt16();
						seek += 4;
						var subReader = reader[seek];
						sb.Append(DumpConditionalBool(subReader));
					}

					return sb.ToString();
				}
				case 5:
				{
					var count = reader[2].ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" || ");
						}

						var r = reader[j + 4];
						var seek = r.ReadUInt16();
						seek += 4;
						var subReader = reader[seek];
						sb.Append(DumpConditionalBool(subReader));
					}

					return sb.ToString();
				}
				case 6:
				{
					return DumpConditionalBool(reader[reader[4].ReadUInt16() + 4]) + " == false";
				}
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				{
					var seek = reader[4].ReadUInt16();
					seek += 4;
					var seekr = reader[seek];

					var left = DumpConditionalGeneric(seekr);

					seek = reader[6].ReadUInt16();
					seek += 4;
					seekr = reader[seek];
					var right = DumpConditionalGeneric(seekr);

					var comparisonType = reader[1].ReadByte();
					switch (comparisonType)
					{
						case 7:
						{
							return left + " == " + right;
						}
						case 8:
						{
							return left + " != " + right;
						}
						case 9:
						{
							return left + " < " + right;
						}
						case 10:
						{
							return left + " <= " + right;
						}
						case 11:
						{
							return left + " > " + right;
						}
						case 12:
						{
							return left + " >= " + right;
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalInt(ByteBufferReader reader)
		{
			var flags = reader[0].ReadByte();
			var flagType = (FlagType) ((flags & 0x0F) >> 0);
			var opType = (OpType) ((flags & 0xF0) >> 4);

			switch (flagType)
			{
				case FlagType.Int:
				{
					switch (opType)
					{
						case OpType.StaticInt:
						{
							return reader[1].ReadInt32().ToString(CultureInfo.InvariantCulture);
						}
						case OpType.Argument:
						{
							var value = reader[1].ReadInt32();
							if (value == -1)
							{
								return "arg";
							}

							throw new NotImplementedException();
						}
						case OpType.Expression:
						{
							return "(" + DumpConditionalIntExpression(reader) + ")";
						}
						case OpType.Table:
						{
							return "plot.ints[" + reader[1].ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				case FlagType.Float:
				{
					return DumpConditionalFloat(reader);
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalIntExpression(ByteBufferReader reader)
		{
			var op = reader[1].ReadByte();

			switch (op)
			{
				case 0:
				{
					var count = reader[2].ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" + ");
						}

						var subReader = reader[reader[j + 4].ReadUInt16() + 4];
						sb.Append(DumpConditionalInt(subReader));
					}

					return sb.ToString();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalFloat(ByteBufferReader reader)
		{
			var flags = reader[0].ReadByte();
			var flagType = (FlagType) ((flags & 0x0F) >> 0);
			var opType = (OpType) ((flags & 0xF0) >> 4);

			switch (flagType)
			{
				case FlagType.Int:
				{
					return DumpConditionalInt(reader);
				}
				case FlagType.Float:
				{
					switch (opType)
					{
						case OpType.StaticFloat:
						{
							return reader[1].ReadSingle().ToString(CultureInfo.InvariantCulture);
						}
						case OpType.Expression:
						{
							return "(" + DumpConditionalFloatExpression(reader) + ")";
						}
						case OpType.Table:
						{
							return "plot.floats[" + reader[1].ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalFloatExpression(ByteBufferReader reader)
		{
			var op = reader[1].ReadByte();
			switch (op)
			{
				case 0:
				{
					var count = reader[2].ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" + ");
						}

						var subReader = reader[reader[j + 4].ReadUInt16() + 4];
						sb.Append(DumpConditionalFloat(subReader));
					}

					return sb.ToString();
				}
				case 2:
				{
					var count = reader[2].ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" * ");
						}

						var subReader = reader[reader[j + 4].ReadUInt16() + 4];
						sb.Append(DumpConditionalFloat(subReader));
					}

					return sb.ToString();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}
	}
}
