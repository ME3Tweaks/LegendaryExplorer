using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace MassEffect3.PackageUnpack
{
	public class BlockStream : Stream
	{
		private readonly Stream _BaseStream;
		private readonly List<Block> _Blocks;
		private Block _CurrentBlock;
		private long _CurrentOffset;

		public BlockStream(Stream baseStream)
		{
			if (baseStream == null)
			{
				throw new ArgumentNullException("baseStream");
			}

			_BaseStream = baseStream;
			_Blocks = new List<Block>();
			_CurrentOffset = 0;
		}

		public void AddBlock(
			uint uncompressedOffset,
			uint uncompressedSize,
			uint compressedOffset,
			uint compressedSize)
		{
			_Blocks.Add(
				new Block(
					uncompressedOffset,
					uncompressedSize,
					compressedOffset,
					compressedSize));
		}

		private bool LoadBlock(long offset)
		{
			if (_CurrentBlock == null ||
				_CurrentBlock.IsValidOffset(offset) == false)
			{
				Block block = _Blocks.SingleOrDefault(
					candidate => candidate.IsValidOffset(offset));

				if (block == null)
				{
					_CurrentBlock = null;
					return false;
				}

				_CurrentBlock = block;
			}

			return _CurrentBlock.Load(_BaseStream);
		}

		public void SaveUncompressed(Stream output)
		{
			var data = new byte[1024];

			uint totalSize = _Blocks.Max(
				candidate =>
					candidate.UncompressedOffset +
					candidate.UncompressedSize);

			output.SetLength(totalSize);

			foreach (var block in _Blocks)
			{
				output.Seek(block.UncompressedOffset, SeekOrigin.Begin);
				Seek(block.UncompressedOffset, SeekOrigin.Begin);

				var total = (int) block.UncompressedSize;
				while (total > 0)
				{
					int read = Read(data, 0, Math.Min(total, data.Length));
					output.Write(data, 0, read);
					total -= read;
				}
			}

			output.Flush();
		}

		#region Stream

		public override bool CanRead
		{
			get { return _BaseStream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return _BaseStream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return false; }
		}

		public override long Length
		{
			get { throw new NotImplementedException(); }
		}

		public override long Position
		{
			get { return _CurrentOffset; }

			set { Seek(value, SeekOrigin.Begin); }
		}

		public override void Flush()
		{
			_BaseStream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			int totalRead = 0;

			while (totalRead < count)
			{
				if (LoadBlock(_CurrentOffset) == false)
				{
					throw new InvalidOperationException();
				}

				int read = _CurrentBlock.Read(
					_BaseStream,
					_CurrentOffset,
					buffer,
					offset,
					count);

				totalRead += read;
				_CurrentOffset += read;
				offset += read;
				count -= read;
			}

			return totalRead;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			if (origin == SeekOrigin.End)
			{
				throw new NotSupportedException();
			}

			if (origin == SeekOrigin.Current)
			{
				if (offset == 0)
				{
					return _CurrentOffset;
				}

				offset = _CurrentOffset + offset;
			}

			if (LoadBlock(offset) == false)
			{
				throw new InvalidOperationException();
			}

			_CurrentOffset = offset;
			return _CurrentOffset;
		}

		public override void SetLength(long value)
		{
			throw new NotImplementedException();
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			throw new NotImplementedException();
		}

		#endregion

		private class Block
		{
			private readonly List<Segment> _Segments;
			private byte[] _CurrentSegmentData;
			private int _CurrentSegmentIndex;
			private bool _IsLoaded;
			private uint _SegmentSize;

			public Block(
				uint uncompressedOffset,
				uint uncompressedSize,
				uint compressedOffset,
				uint compressedSize)
			{
				UncompressedOffset = uncompressedOffset;
				UncompressedSize = uncompressedSize;
				CompressedOffset = compressedOffset;
				CompressedSize = compressedSize;

				_IsLoaded = false;
				_Segments = new List<Segment>();
				_CurrentSegmentIndex = -1;
			}

			public uint UncompressedOffset { get; private set; }
			public uint UncompressedSize { get; private set; }
			private uint CompressedOffset { get; set; }
			private uint CompressedSize { get; set; }

			public bool IsValidOffset(long offset)
			{
				return
					offset >= UncompressedOffset &&
					offset < UncompressedOffset + UncompressedSize;
			}

			public bool Load(Stream input)
			{
				if (_IsLoaded)
				{
					return true;
				}

				input.Seek(CompressedOffset, SeekOrigin.Begin);

				if (input.ReadValueU32() != 0x9E2A83C1)
				{
					throw new FormatException("bad block magic");
				}

				_SegmentSize = input.ReadValueU32();

				/*uint compressedSize = */
				input.ReadValueU32();
				uint uncompressedSize = input.ReadValueU32();

				if (uncompressedSize != UncompressedSize)
				{
					throw new InvalidOperationException();
				}

				uint count = ((uncompressedSize + _SegmentSize) - 1) / _SegmentSize;
				uint segmentOffset = (4 * 4) + (count * 8);

				for (uint i = 0; i < count; i++)
				{
					// ReSharper disable UseObjectOrCollectionInitializer
					var segment = new Segment();
					// ReSharper restore UseObjectOrCollectionInitializer
					segment.CompressedSize = input.ReadValueU32();
					segment.UncompressedSize = input.ReadValueU32();
					segment.Offset = segmentOffset;
					_Segments.Add(segment);
					segmentOffset += segment.CompressedSize;
				}

				_IsLoaded = true;
				return true;
			}

			public int Read(Stream input, long baseOffset, byte[] buffer, int offset, int count)
			{
				var relativeOffset = (int) (baseOffset - UncompressedOffset);
				int segmentIndex = relativeOffset / (int) _SegmentSize;

				int totalRead = 0;

				while (relativeOffset < UncompressedSize)
				{
					if (segmentIndex != _CurrentSegmentIndex)
					{
						_CurrentSegmentIndex = segmentIndex;
						Segment segment = _Segments[segmentIndex];

						var compressedData = new byte[segment.CompressedSize];
						_CurrentSegmentData = new byte[segment.UncompressedSize];

						input.Seek(CompressedOffset + segment.Offset, SeekOrigin.Begin);
						input.Read(compressedData, 0, compressedData.Length);

						using (var temp = new MemoryStream(compressedData))
						{
							var zlib = new InflaterInputStream(temp);
							if (zlib.Read(_CurrentSegmentData, 0, _CurrentSegmentData.Length) !=
								_CurrentSegmentData.Length)
							{
								throw new InvalidOperationException("decompression error");
							}
						}
					}

					int segmentOffset = relativeOffset % (int) _SegmentSize;
					int left = Math.Min(
						count - totalRead,
						_CurrentSegmentData.Length - segmentOffset);

					Array.ConstrainedCopy(
						_CurrentSegmentData,
						segmentOffset,
						buffer,
						offset,
						left);

					totalRead += left;

					if (totalRead >= count)
					{
						break;
					}

					offset += left;
					relativeOffset += left;
					segmentIndex++;
				}

				return totalRead;
			}

			private struct Segment
			{
				public uint CompressedSize;
				public uint Offset;
				public uint UncompressedSize;
			}
		}
	}
}