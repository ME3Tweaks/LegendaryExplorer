// LzBinTree.cs

using System;
using System.IO;
using MassEffect3.FileFormats.SevenZip.Common;

namespace MassEffect3.FileFormats.SevenZip.Compress.LZ
{
	public class BinTree : InWindow, IMatchFinder
	{
		UInt32 _cyclicBufferPos;
		UInt32 _cyclicBufferSize = 0;
		UInt32 _matchMaxLen;

		UInt32[] _son;
		UInt32[] _hash;

		UInt32 _cutValue = 0xFF;
		UInt32 _hashMask;
		UInt32 _hashSizeSum = 0;

		bool HASH_ARRAY = true;

		const UInt32 kHash2Size = 1 << 10;
		const UInt32 kHash3Size = 1 << 16;
		const UInt32 kBT2HashSize = 1 << 16;
		const UInt32 kStartMaxLen = 1;
		const UInt32 kHash3Offset = kHash2Size;
		const UInt32 kEmptyHashValue = 0;
		const UInt32 kMaxValForNormalize = ((UInt32)1 << 31) - 1;
	
		UInt32 kNumHashDirectBytes = 0;
		UInt32 kMinMatchCheck = 4;
		UInt32 kFixHashSize = kHash2Size + kHash3Size;
		
		public void SetType(int numHashBytes)
		{
			HASH_ARRAY = (numHashBytes > 2);
			if (HASH_ARRAY)
			{
				kNumHashDirectBytes = 0;
				kMinMatchCheck = 4;
				kFixHashSize = kHash2Size + kHash3Size;
			}
			else
			{
				kNumHashDirectBytes = 2;
				kMinMatchCheck = 2 + 1;
				kFixHashSize = 0;
			}
		}

		public new void SetStream(Stream stream) { base.SetStream(stream); }
		public new void ReleaseStream() { base.ReleaseStream(); }
		
		public new void Init()
		{
			base.Init();
			for (UInt32 i = 0; i < _hashSizeSum; i++)
				_hash[i] = kEmptyHashValue;
			_cyclicBufferPos = 0;
			ReduceOffsets(-1);
		}

		public new void MovePos()
		{
			if (++_cyclicBufferPos >= _cyclicBufferSize)
				_cyclicBufferPos = 0;
			base.MovePos();
			if (_pos == kMaxValForNormalize)
				Normalize();
		}

		public new Byte GetIndexByte(Int32 index) { return base.GetIndexByte(index); }

		public new UInt32 GetMatchLen(Int32 index, UInt32 distance, UInt32 limit)
		{ return base.GetMatchLen(index, distance, limit); }

		public new UInt32 GetNumAvailableBytes() { return base.GetNumAvailableBytes(); }

		public void Create(UInt32 historySize, UInt32 keepAddBufferBefore,
				UInt32 matchMaxLen, UInt32 keepAddBufferAfter)
		{
			if (historySize > kMaxValForNormalize - 256)
				throw new Exception();
			_cutValue = 16 + (matchMaxLen >> 1);
				
			var windowReservSize = (historySize + keepAddBufferBefore +
					matchMaxLen + keepAddBufferAfter) / 2 + 256;

			base.Create(historySize + keepAddBufferBefore, matchMaxLen + keepAddBufferAfter, windowReservSize);

			_matchMaxLen = matchMaxLen;

			var cyclicBufferSize = historySize + 1;
			if (_cyclicBufferSize != cyclicBufferSize)
				_son = new UInt32[(_cyclicBufferSize = cyclicBufferSize) * 2];

			var hs = kBT2HashSize;

			if (HASH_ARRAY)
			{
				hs = historySize - 1;
				hs |= (hs >> 1);
				hs |= (hs >> 2);
				hs |= (hs >> 4);
				hs |= (hs >> 8);
				hs >>= 1;
				hs |= 0xFFFF;
				if (hs > (1 << 24))
					hs >>= 1;
				_hashMask = hs;
				hs++;
				hs += kFixHashSize;
			}
			if (hs != _hashSizeSum)
				_hash = new UInt32[_hashSizeSum = hs];
		}

		public UInt32 GetMatches(UInt32[] distances)
		{
			UInt32 lenLimit;
			if (_pos + _matchMaxLen <= _streamPos)
				lenLimit = _matchMaxLen;
			else
			{
				lenLimit = _streamPos - _pos;
				if (lenLimit < kMinMatchCheck)
				{
					MovePos();
					return 0;
				}
			}

			UInt32 offset = 0;
			var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
			var cur = _bufferOffset + _pos;
			var maxLen = kStartMaxLen; // to avoid items for len < hashSize;
			UInt32 hashValue, hash2Value = 0, hash3Value = 0;

			if (HASH_ARRAY)
			{
				var temp = CRC.Table[_bufferBase[cur]] ^ _bufferBase[cur + 1];
				hash2Value = temp & (kHash2Size - 1);
				temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
				hash3Value = temp & (kHash3Size - 1);
				hashValue = (temp ^ (CRC.Table[_bufferBase[cur + 3]] << 5)) & _hashMask;
			}
			else
				hashValue = _bufferBase[cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);

			var curMatch = _hash[kFixHashSize + hashValue];
			if (HASH_ARRAY)
			{
				var curMatch2 = _hash[hash2Value];
				var curMatch3 = _hash[kHash3Offset + hash3Value];
				_hash[hash2Value] = _pos;
				_hash[kHash3Offset + hash3Value] = _pos;
				if (curMatch2 > matchMinPos)
					if (_bufferBase[_bufferOffset + curMatch2] == _bufferBase[cur])
					{
						distances[offset++] = maxLen = 2;
						distances[offset++] = _pos - curMatch2 - 1;
					}
				if (curMatch3 > matchMinPos)
					if (_bufferBase[_bufferOffset + curMatch3] == _bufferBase[cur])
					{
						if (curMatch3 == curMatch2)
							offset -= 2;
						distances[offset++] = maxLen = 3;
						distances[offset++] = _pos - curMatch3 - 1;
						curMatch2 = curMatch3;
					}
				if (offset != 0 && curMatch2 == curMatch)
				{
					offset -= 2;
					maxLen = kStartMaxLen;
				}
			}

			_hash[kFixHashSize + hashValue] = _pos;

			var ptr0 = (_cyclicBufferPos << 1) + 1;
			var ptr1 = (_cyclicBufferPos << 1);

			UInt32 len0, len1;
			len0 = len1 = kNumHashDirectBytes;
			
			if (kNumHashDirectBytes != 0)
			{
				if (curMatch > matchMinPos)
				{
					if (_bufferBase[_bufferOffset + curMatch + kNumHashDirectBytes] !=
							_bufferBase[cur + kNumHashDirectBytes])
					{
						distances[offset++] = maxLen = kNumHashDirectBytes;
						distances[offset++] = _pos - curMatch - 1;
					}
				}
			}
			
			var count = _cutValue;
			
			while(true)
			{
				if(curMatch <= matchMinPos || count-- == 0)
				{
					_son[ptr0] = _son[ptr1] = kEmptyHashValue;
					break;
				}
				var delta = _pos - curMatch;
				var cyclicPos = ((delta <= _cyclicBufferPos) ?
							(_cyclicBufferPos - delta) :
							(_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

				var pby1 = _bufferOffset + curMatch;
				var len = Math.Min(len0, len1);
				if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
				{
					while(++len != lenLimit)
						if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
							break;
					if (maxLen < len)
					{
						distances[offset++] = maxLen = len;
						distances[offset++] = delta - 1;
						if (len == lenLimit)
						{
							_son[ptr1] = _son[cyclicPos];
							_son[ptr0] = _son[cyclicPos + 1];
							break;
						}
					}
				}
				if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
				{
					_son[ptr1] = curMatch;
					ptr1 = cyclicPos + 1;
					curMatch = _son[ptr1];
					len1 = len;
				}
				else
				{
					_son[ptr0] = curMatch;
					ptr0 = cyclicPos;
					curMatch = _son[ptr0];
					len0 = len;
				}
			}
			MovePos();
			return offset;
		}

		public void Skip(UInt32 num)
		{
			do
			{
				UInt32 lenLimit;
				if (_pos + _matchMaxLen <= _streamPos)
					lenLimit = _matchMaxLen;
				else
				{
					lenLimit = _streamPos - _pos;
					if (lenLimit < kMinMatchCheck)
					{
						MovePos();
						continue;
					}
				}

				var matchMinPos = (_pos > _cyclicBufferSize) ? (_pos - _cyclicBufferSize) : 0;
				var cur = _bufferOffset + _pos;

				UInt32 hashValue;

				if (HASH_ARRAY)
				{
					var temp = CRC.Table[_bufferBase[cur]] ^ _bufferBase[cur + 1];
					var hash2Value = temp & (kHash2Size - 1);
					_hash[hash2Value] = _pos;
					temp ^= ((UInt32)(_bufferBase[cur + 2]) << 8);
					var hash3Value = temp & (kHash3Size - 1);
					_hash[kHash3Offset + hash3Value] = _pos;
					hashValue = (temp ^ (CRC.Table[_bufferBase[cur + 3]] << 5)) & _hashMask;
				}
				else
					hashValue = _bufferBase[cur] ^ ((UInt32)(_bufferBase[cur + 1]) << 8);

				var curMatch = _hash[kFixHashSize + hashValue];
				_hash[kFixHashSize + hashValue] = _pos;

				var ptr0 = (_cyclicBufferPos << 1) + 1;
				var ptr1 = (_cyclicBufferPos << 1);

				UInt32 len0, len1;
				len0 = len1 = kNumHashDirectBytes;

				var count = _cutValue;
				while (true)
				{
					if (curMatch <= matchMinPos || count-- == 0)
					{
						_son[ptr0] = _son[ptr1] = kEmptyHashValue;
						break;
					}

					var delta = _pos - curMatch;
					var cyclicPos = ((delta <= _cyclicBufferPos) ?
								(_cyclicBufferPos - delta) :
								(_cyclicBufferPos - delta + _cyclicBufferSize)) << 1;

					var pby1 = _bufferOffset + curMatch;
					var len = Math.Min(len0, len1);
					if (_bufferBase[pby1 + len] == _bufferBase[cur + len])
					{
						while (++len != lenLimit)
							if (_bufferBase[pby1 + len] != _bufferBase[cur + len])
								break;
						if (len == lenLimit)
						{
							_son[ptr1] = _son[cyclicPos];
							_son[ptr0] = _son[cyclicPos + 1];
							break;
						}
					}
					if (_bufferBase[pby1 + len] < _bufferBase[cur + len])
					{
						_son[ptr1] = curMatch;
						ptr1 = cyclicPos + 1;
						curMatch = _son[ptr1];
						len1 = len;
					}
					else
					{
						_son[ptr0] = curMatch;
						ptr0 = cyclicPos;
						curMatch = _son[ptr0];
						len0 = len;
					}
				}
				MovePos();
			}
			while (--num != 0);
		}

		void NormalizeLinks(UInt32[] items, UInt32 numItems, UInt32 subValue)
		{
			for (UInt32 i = 0; i < numItems; i++)
			{
				var value = items[i];
				if (value <= subValue)
					value = kEmptyHashValue;
				else
					value -= subValue;
				items[i] = value;
			}
		}

		void Normalize()
		{
			var subValue = _pos - _cyclicBufferSize;
			NormalizeLinks(_son, _cyclicBufferSize * 2, subValue);
			NormalizeLinks(_hash, _hashSizeSum, subValue);
			ReduceOffsets((Int32)subValue);
		}

		public void SetCutValue(UInt32 cutValue) { _cutValue = cutValue; }
	}
}
