/*	Copyright 2012 Brent Scriver

	Licensed under the Apache License, Version 2.0 (the "License");
	you may not use this file except in compliance with the License.
	You may obtain a copy of the License at

		http://www.apache.org/licenses/LICENSE-2.0

	Unless required by applicable law or agreed to in writing, software
	distributed under the License is distributed on an "AS IS" BASIS,
	WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
	See the License for the specific language governing permissions and
	limitations under the License.
*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace Gammtek.Conduit.Compression.Huffman
{
	/// <summary>
	///     Static Huffman table implementation based on the predetermined frequencies of <typeparamref name="TSymbolType" />.
	/// </summary>
	public class StaticHuffman<TSymbolType>
		where TSymbolType : struct, IComparable<TSymbolType>
	{
		private readonly Entry[] _entries;
		private readonly IDictionary<TSymbolType, SymbolInfo> _map;

		/// <summary>
		///     Creates a static Huffman tree for encoding/decoding based on the frequencies for a given set of <typeparamref name="TSymbolType" />.
		/// </summary>
		public StaticHuffman(IDictionary<TSymbolType, uint> symbolWeights)
		{
			_map = new Dictionary<TSymbolType, SymbolInfo>(symbolWeights.Count);

			var symbols = GetSymbolList(symbolWeights);
			SymbolCount = (uint) symbols.Count;
			_entries = new Entry[2 * SymbolCount - 1];

			for (var i = 0; i < symbols.Count; ++i)
			{
				_entries[i] = symbols[i];
			}

			BuildInternalNodes();

			PopulateMap(symbols);

			Height = GetHeight();
		}

		/// <summary>
		///     Creates a static Huffman tree for encoding/decoding based on a tree emitted from WriteTable.
		/// </summary>
		/// <param name="symbolReader">Delegate for reading symbols.</param>
		/// <param name="valueReader">Delegate for reading unsigned integers.</param>
		public StaticHuffman(ReadSymbolDelegate<TSymbolType> symbolReader, ReadUInt32Delegate valueReader)
		{
			SymbolCount = valueReader();
			_entries = new Entry[2 * SymbolCount - 1];
			_map = new Dictionary<TSymbolType, SymbolInfo>((int) SymbolCount);

			for (uint i = 0; i < SymbolCount; ++i)
			{
				var symbol = symbolReader();
				_entries[i].Symbol = symbol;
				SymbolInfo info;
				info.Index = i;
				info.Bits = null;
				_map[symbol] = info;
			}

			for (var i = SymbolCount; i < _entries.Length; ++i)
			{
				_entries[i].ChildIndex = new uint[2];
				_entries[i].ChildIndex[0] = valueReader();
				_entries[i].ChildIndex[1] = valueReader();
			}

			ComputeParents();

			Height = GetHeight();

			Validate();
		}

		/// <summary>
		///     The height of the Huffman tree.
		/// </summary>
		public uint Height { get; }

		/// <summary>
		///     The number of symbols in the Huffman tree.
		/// </summary>
		public uint SymbolCount { get; }

		/// <summary>
		///     Reads a symbol using the bits retrieved with <paramref name="bitReader" />.
		/// </summary>
		public TSymbolType GetSymbol(ReadBitDelegate bitReader)
		{
			var current = (uint) _entries.Length - 1;
			while (!_entries[current].IsSymbol)
			{
				current = _entries[current].GetChild(bitReader());
			}
			return _entries[current].Symbol;
		}

		/// <summary>
		///     Writes the bit representation of <paramref name="symbol" /> with <paramref name="bitWriter" />.
		/// </summary>
		public void WriteCode(TSymbolType symbol, WriteBitDelegate bitWriter)
		{
			var bits = GetCachedBits(symbol);
			for (var i = 0; i < bits.Length; ++i)
			{
				bitWriter(bits[i]);
			}
		}

		/// <summary>
		///     Writes the Huffman tree for future decoding use.
		/// </summary>
		public void WriteTable(WriteSymbolDelegate<TSymbolType> symbolWriter, WriteUInt32Delegate valueWriter)
		{
			valueWriter(SymbolCount);

			for (uint i = 0; i < SymbolCount; ++i)
			{
				symbolWriter(_entries[i].Symbol);
			}

			for (var i = SymbolCount; i < _entries.Length; ++i)
			{
				valueWriter(_entries[i].ChildIndex[0]);
				valueWriter(_entries[i].ChildIndex[1]);
			}
		}

		private void BuildInternalNodes()
		{
			uint symbolIndex = 0;
			var nodeIndex = uint.MaxValue;
			var targetIndex = SymbolCount;

			var selectedWeight = new uint[2];

			while (targetIndex < _entries.Length)
			{
				var selectedIndex = new uint[2];
				for (var selection = 0; selection < 2; ++selection)
				{
					if (symbolIndex >= SymbolCount)
					{
						var index = nodeIndex + SymbolCount;
						selectedIndex[selection] = index;
						selectedWeight[selection] = _entries[index].Weight;
						++nodeIndex;
					}
					else if (nodeIndex > _entries.Length)
					{
						selectedIndex[selection] = symbolIndex;
						selectedWeight[selection] = _entries[symbolIndex].Weight;
						++symbolIndex;
					}
					else if (_entries[symbolIndex].Weight <= _entries[nodeIndex + SymbolCount].Weight)
					{
						selectedIndex[selection] = symbolIndex;
						selectedWeight[selection] = _entries[symbolIndex].Weight;
						++symbolIndex;
					}
					else
					{
						var index = nodeIndex + SymbolCount;
						selectedIndex[selection] = index;
						selectedWeight[selection] = _entries[index].Weight;
						++nodeIndex;
					}
				}

				_entries[targetIndex].ChildIndex = selectedIndex;
				_entries[targetIndex].Weight = selectedWeight[0] + selectedWeight[1];
				_entries[selectedIndex[0]].Parent = targetIndex;
				_entries[selectedIndex[1]].Parent = targetIndex;
				nodeIndex = nodeIndex == uint.MaxValue ? 0 : nodeIndex;
				++targetIndex;
			}
		}

		private void ComputeParents()
		{
			var toProcess = new Stack<uint>();
			toProcess.Push((uint) (_entries.Length - 1));
			while (toProcess.Count > 0)
			{
				var current = toProcess.Pop();
				if (!_entries[current].IsSymbol)
				{
					var children = _entries[current].ChildIndex;
					_entries[children[0]].Parent = current;
					_entries[children[1]].Parent = current;
					toProcess.Push(children[0]);
					toProcess.Push(children[1]);
				}
			}
		}

		private BitArray GetCachedBits(TSymbolType symbol)
		{
			var info = _map[symbol];
			if (info.Bits != null)
			{
				return info.Bits;
			}
			var result = new BitArray((int) Height);
			uint currentIndex = 0;
			UpdateCachedBits(info.Index, result, ref currentIndex);
			result.Length = (int) currentIndex;
			info.Bits = result;
			_map[symbol] = info;
			return result;
		}

		private uint GetHeight()
		{
			var levels = new uint[_entries.Length];
			uint highestLevel = 0;
			var toProcess = new Stack<uint>();
			toProcess.Push((uint) (_entries.Length - 1));
			while (toProcess.Count > 0)
			{
				var current = toProcess.Pop();
				var currentLevel = levels[current];
				highestLevel = Math.Max(highestLevel, currentLevel + 1);
				if (!_entries[current].IsSymbol)
				{
					var children = _entries[current].ChildIndex;
					levels[children[0]] = currentLevel + 1;
					levels[children[1]] = currentLevel + 1;
					toProcess.Push(children[0]);
					toProcess.Push(children[1]);
				}
			}
			return highestLevel + 1;
		}

		/// <summary>
		///     Generates a list of Entry instances for each symbol in the dictionary in sorted order.
		/// </summary>
		/// <param name="symbolWeights"></param>
		/// <returns></returns>
		private List<Entry> GetSymbolList(IDictionary<TSymbolType, uint> symbolWeights)
		{
			var results = new List<Entry>(symbolWeights.Count);

			foreach (var symbolWeight in symbolWeights)
			{
				if (symbolWeight.Value > 0)
				{
					Entry entry;
					entry.Symbol = symbolWeight.Key;
					entry.Weight = symbolWeight.Value;
					entry.ChildIndex = null;
					entry.Parent = 0;
					results.Add(entry);
				}
			}

			results.Sort(results[0]);

			return results;
		}

		private void PopulateMap(List<Entry> sourceEntries)
		{
			for (uint index = 0; index < sourceEntries.Count; ++index)
			{
				SymbolInfo info;
				info.Index = index;
				info.Bits = null;
				_map[sourceEntries[(int) index].Symbol] = info;
			}
		}

		private void UpdateCachedBits(uint index, BitArray bits, ref uint currentIndex)
		{
			var parent = _entries[index].Parent;
			var output = _entries[parent].GetChildIndex(index);
			if (parent != _entries.Length - 1)
			{
				UpdateCachedBits(parent, bits, ref currentIndex);
			}
			bits[(int) currentIndex] = output;
			++currentIndex;
		}

		private void Validate() {}

		private struct Entry : IComparer<Entry>
		{
			public uint[] ChildIndex;
			public uint Parent;
			public TSymbolType Symbol;
			public uint Weight;

			public bool IsSymbol
			{
				get { return ChildIndex == null; }
			}

			public int Compare(Entry x, Entry y)
			{
				var diff = x.Weight.CompareTo(y.Weight);
				if (diff != 0)
				{
					return diff;
				}
				return x.Symbol.CompareTo(y.Symbol);
			}

			public uint GetChild(bool position)
			{
				return ChildIndex[position ? 1 : 0];
			}

			public bool GetChildIndex(uint index)
			{
				return ChildIndex[1] == index;
			}
		}

		private struct SymbolInfo
		{
			public BitArray Bits;
			public uint Index;
		}
	}
}
