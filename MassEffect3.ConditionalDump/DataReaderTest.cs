using System;
using System.Globalization;
using System.IO;
using System.Text;
using Gammtek.Conduit.IO;

namespace MassEffect3.ConditionalDump
{
	public static class DataReaderTest
	{
		public static string DumpConditionalGeneric(DataReader reader)
		{
			var flags = (byte) reader.PeekChar();
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

		public static string DumpConditionalBool(DataReader reader)
		{
			var flags = reader.ReadByte();

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
							return reader.ReadBoolean() ? "true" : "false";
						}
						case OpType.Argument:
						{
							var value = reader.ReadInt32();
							if (value == -1)
							{
								return "(arg != 0)";
							}

							var functionLength = reader.ReadUInt16();
							var tagLength = reader.ReadUInt16();

							var function = new String(reader.ReadChars(functionLength));

							var sb = new StringBuilder();
							sb.Append("GetLocalVariable");
							sb.Append("(");
							sb.AppendFormat("\"{0}\"", function);
							sb.Append(", ");
							sb.AppendFormat("{0}", value);

							if (tagLength > 0)
							{
								var tag = new String(reader.ReadChars(tagLength));
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
							return "plot.bools[" + reader.ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				case FlagType.Int:
				{
					reader.Seek(-1, SeekOrigin.Current);
					return DumpConditionalInt(reader) + " != 0";
				}
				case FlagType.Float:
				{
					reader.Seek(-1, SeekOrigin.Current);
					return DumpConditionalFloat(reader) + " != 0";
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalBoolExpression(DataReader reader)
		{
			var op = reader.ReadByte();
			
			switch (op)
			{
				case 4:
				{
					var count = reader.ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" && ");
						}

						var pos = reader.Position;

						if (i == 0)
						{
							var seek = reader.ReadUInt16();
							var seek2 = reader.ReadUInt16();

							reader.Seek(seek - 4, SeekOrigin.Current);
						}
						
						var d = DumpConditionalBool(reader);
						sb.Append(d);
					}

					return sb.ToString();
				}
				case 5:
				{
					var count = reader.ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" || ");
						}

						if (i == 0)
						{
							var seek = reader.ReadUInt16();
							var seek2 = reader.ReadUInt16();

							reader.Seek(seek - 4, SeekOrigin.Current);
						}

						var d = DumpConditionalBool(reader);
						sb.Append(d);
					}

					return sb.ToString();
				}
				case 6:
				{
					reader.Seek(2, SeekOrigin.Current);
					var seek = reader.ReadUInt16();

					return DumpConditionalBool(reader) + " == false";
				}
				case 7:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				{
					var comparisonType = op;

					reader.Seek(2, SeekOrigin.Current);
					var seekLeft = reader.ReadUInt16();
					var seekRight = reader.ReadUInt16();
					var pos = reader.Position;

					reader.Seek(seekLeft - 4 + pos);
					var left = DumpConditionalGeneric(reader);

					reader.Seek(seekRight - 4 + pos);
					var right = DumpConditionalGeneric(reader);

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

		public static string DumpConditionalInt(DataReader reader)
		{
			var flags = reader.ReadByte();
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
							return reader.ReadInt32().ToString(CultureInfo.InvariantCulture);
						}
						case OpType.Argument:
						{
							var value = reader.ReadInt32();
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
							return "plot.ints[" + reader.ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
						}
						default:
						{
							throw new NotImplementedException();
						}
					}
				}
				case FlagType.Float:
				{
					reader.Seek(-1, SeekOrigin.Current);
					return DumpConditionalFloat(reader);
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalIntExpression(DataReader reader)
		{
			var op = reader.ReadByte();

			switch (op)
			{
				case 0:
				{
					var count = reader.ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" + ");
						}

						if (i == 0)
						{
							var seek = reader.ReadUInt16();
							var seek2 = reader.ReadUInt16();

							reader.Seek(seek - 4, SeekOrigin.Current);
						}
						
						sb.Append(DumpConditionalInt(reader));
					}

					return sb.ToString();
				}
				default:
				{
					throw new NotImplementedException();
				}
			}
		}

		public static string DumpConditionalFloat(DataReader reader)
		{
			var flags = reader.ReadByte();
			var flagType = (FlagType) ((flags & 0x0F) >> 0);
			var opType = (OpType) ((flags & 0xF0) >> 4);

			switch (flagType)
			{
				case FlagType.Int:
				{
					reader.Seek(-1, SeekOrigin.Current);
					return DumpConditionalInt(reader);
				}
				case FlagType.Float:
				{
					switch (opType)
					{
						case OpType.StaticFloat:
						{
							return reader.ReadSingle().ToString(CultureInfo.InvariantCulture);
						}
						case OpType.Expression:
						{
							return "(" + DumpConditionalFloatExpression(reader) + ")";
						}
						case OpType.Table:
						{
							return "plot.floats[" + reader.ReadInt32().ToString(CultureInfo.InvariantCulture) + "]";
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

		public static string DumpConditionalFloatExpression(DataReader reader)
		{
			var op = reader.ReadByte();
			
			switch (op)
			{
				case 0:
				{
					var count = reader.ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" + ");
						}

						if (i == 0)
						{
							var seek = reader.ReadUInt16();
							var seek2 = reader.ReadUInt16();

							reader.Seek(seek - 4, SeekOrigin.Current);
						}
						sb.Append(DumpConditionalFloat(reader));
					}

					return sb.ToString();
				}
				case 2:
				{
					var count = reader.ReadUInt16();
					var sb = new StringBuilder();

					for (int i = 0, j = 0; i < count; i++, j += 2)
					{
						if (i > 0)
						{
							sb.Append(" * ");
						}

						if (i == 0)
						{
							var seek = reader.ReadUInt16();
							var seek2 = reader.ReadUInt16();

							reader.Seek(seek - 4, SeekOrigin.Current);
						}
						
						sb.Append(DumpConditionalFloat(reader));
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
