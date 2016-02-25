using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Gammtek.Conduit.IO.Tlk.Binary
{
	internal class BinaryTlkReader : IDisposable
	{
		public BinaryTlkReader(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			Reader = new DataReader(stream);
		}

		public virtual Stream BaseStream
		{
			get { return (Reader != null) ? Reader.BaseStream : null; }
		}

		public DataReader Reader { get; protected set; }

		public BitArray Bits { get; protected set; }

		public IList<HuffmanNode> CharacterTree { get; protected set; }

		public int Magic { get; set; }

		public int Version { get; set; }

		public int MinVersion { get; set; }

		public int Entry1Count { get; set; }

		public int Entry2Count { get; set; }

		public int TreeNodeCount { get; set; }

		public int DataLength { get; set; }

		public IDictionary<int, string> RawStrings { get; set; }

		public IList<TlkStringRef> ReadStringRefs()
		{
			var rawStrings = ReadRawStrings();

			var stringRefs = new List<TlkStringRef>();

			for (var i = 0; i < (Entry1Count + Entry2Count); i++)
			{
				var stringRef = new TlkStringRef(Reader.ReadInt32(), Reader.ReadInt32(), i);

				if (stringRef.Offset >= 0)
				{
					if (!rawStrings.ContainsKey(stringRef.Offset))
					{
						var stringRefOffset = stringRef.Offset;
						var partString = ReadRawString(ref stringRefOffset);

						// FullString
						var key = rawStrings.Keys.Last(c => c < stringRef.Offset);
						var fullString = rawStrings[key];
						var subStringOffset = fullString.LastIndexOf(partString, StringComparison.Ordinal);
						stringRef.StringStart = subStringOffset;
						stringRef.Data = fullString;

						// PartString
						//stringRef.Data = partString;
					}
					else
					{
						stringRef.Data = rawStrings[stringRef.Offset];
					}
				}

				stringRefs.Add(stringRef);
			}

			return stringRefs;
		}

		public void ReadFileHeader()
		{
			Magic = Reader.ReadInt32();
			Version = Reader.ReadInt32();
			MinVersion = Reader.ReadInt32();
			Entry1Count = Reader.ReadInt32();
			Entry2Count = Reader.ReadInt32();
			TreeNodeCount = Reader.ReadInt32();
			DataLength = Reader.ReadInt32();

			if (Magic != TlkHeader.ValidFileId)
			{
				throw new InvalidDataException("Tlk file is invalid.");
			}

			var pos = Reader.Position;

			Reader.Seek(pos + (Entry1Count + Entry2Count) * 8);

			CharacterTree = new List<HuffmanNode>();

			for (var i = 0; i < TreeNodeCount; i++)
			{
				CharacterTree.Add(new HuffmanNode(Reader.ReadInt32(), Reader.ReadInt32()));
			}

			Bits = new BitArray(Reader.ReadBytes(DataLength));

			Reader.Seek(pos);
		}

		public IDictionary<int, string> ReadRawStrings()
		{
			var rawStrings = new Dictionary<int, string>();
			var offset = 0;

			while (offset < Bits.Length)
			{
				var key = offset;
				var s = ReadRawString(ref offset);

				rawStrings.Add(key, s);
			}

			return rawStrings;
		}

		public string ReadRawString(ref int offset)
		{
			var root = CharacterTree[0];
			var curNode = root;
			var curString = "";
			int i;

			for (i = offset; i < Bits.Length; i++)
			{
				var nextNodeId = Bits[i] ? curNode.RightNodeId : curNode.LeftNodeId;

				if (nextNodeId >= 0)
				{
					curNode = CharacterTree[nextNodeId];
				}
				else
				{
					var c = Reader.Converter.ToChar(Reader.Converter.GetBytes(0xffff - nextNodeId), 0);

					if (c != '\0')
					{
						// It is not a string terminator
						curString += c;
						curNode = root;
					}
					else
					{
						// It is a string terminator
						offset = i + 1;

						return curString;
					}
				}
			}

			offset = i + 1;

			return null;
		}

		public BinaryTlkFile ToBinaryFile()
		{
			if (Reader.Position > 0)
			{
				Reader.Seek(0);
			}

			ReadFileHeader();
			RawStrings = ReadRawStrings();

			return new BinaryTlkFile(stringRefs: ReadStringRefs());
		}

		protected virtual void Dispose(bool disposing)
		{
			if (disposing && Reader != null)
			{
				Reader.Close();
			}

			Reader = null;
		}

		#region Implementation of IDisposable

		/// <summary>
		///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		#endregion
	}
}
