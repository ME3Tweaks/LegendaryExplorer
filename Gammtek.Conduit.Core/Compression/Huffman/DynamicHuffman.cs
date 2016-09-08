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
using System.Collections.Generic;
using System.Text;

namespace Gammtek.Conduit.Compression.Huffman
{
	/// <summary>
	///     Adaptive Huffman implementation using modifications to Vitter's algorithm to
	///     support a non-determistic range of symbols, allow weight modification of the
	///     NotYetTransmitted sequence, and allow decrementing the weight of leaves.
	/// </summary>
	public class DynamicHuffman<TSymbolType>
		where TSymbolType : struct, IComparable<TSymbolType>
	{
		/// <summary>
		///     Mapping for symbols to nodes
		/// </summary>
		private readonly IDictionary<SymbolSpace, uint> _map;

		private readonly NotYetTransmittedWeightTweakDelegate<TSymbolType> _tweaker;
		private Entry[] _entries;

		/// <summary>
		///     Creates a new instance of the dynamic Huffman class with the provided delegate for adjusting
		///     the weight of the NotYetTransmitted symbol.
		/// </summary>
		/// <param name="tweaker">Delegate for adjusting the weight of the NotYetTransmitted symbol.</param>
		public DynamicHuffman(NotYetTransmittedWeightTweakDelegate<TSymbolType> tweaker)
		{
			_entries = new Entry[1];
			_entries[0].Height = 1;
			_map = new Dictionary<SymbolSpace, uint>();
			_map[SymbolSpace.NotYetTransmitted] = 0;

			_tweaker = tweaker ?? DefaultNotYetTransmittedWeightTweak;
		}

		/// <summary>
		///     Creates a new instance of the dynamic Huffman class with the default delegate for adjusting
		///     the weight of the NotYetTransmitted symbol.
		/// </summary>
		public DynamicHuffman()
			: this(null) {}

		/// <summary>
		///     Delegate to write tree transformation updates to.
		/// </summary>
		public WriteDotStringDelegate DotWriter { get; set; }

		/// <summary>
		///     The height of the tree.
		///     This function runs in O(1).
		/// </summary>
		public uint Height
		{
			get { return _entries[Root].Height; }
		}

		/// <summary>
		///     The weight of the tree.
		///     This function runs in O(1).
		/// </summary>
		public uint Weight
		{
			get { return _entries[Root].Weight; }
		}

		/// <summary>
		///     The entry of minimum weight (where new nodes are added to).
		/// </summary>
		private uint MinWeightEntry
		{
			get { return (uint) (_entries.Length - 1); }
		}

		/// <summary>
		///     The Root node of the tree.
		/// </summary>
		private uint Root
		{
			get { return 0; }
		}

		/// <summary>
		///     Returns the level in the tree <paramref name="symbol" /> occurs at.
		///     This function runs in O(map + level).
		/// </summary>
		public uint GetLevel(TSymbolType symbol)
		{
			return GetLevelInternal(symbol);
		}

		/// <summary>
		///     Retrieves a symbol.  If the bit sequence from <paramref name="bitReader" />
		///     identifies a symbol literal, <paramref name="symbolReader" /> is called.
		/// </summary>
		public TSymbolType GetSymbol(ReadBitDelegate bitReader, ReadSymbolDelegate<TSymbolType> symbolReader)
		{
			var result = GetSymbolInternal(bitReader);
			if (result.IsValid)
			{
				UpdateSymbolInternal(result, 1);
				UpdateSymbolInternal(SymbolSpace.NotYetTransmitted, GetTweak(result.Symbol, false));
				return result.Symbol;
			}

			result = symbolReader();
			UpdateSymbolInternal(SymbolSpace.NotYetTransmitted, GetTweak(result.Symbol, true));
			UpdateSymbolInternal(result, 1);
			return result.Symbol;
		}

		/// <summary>
		///     Returns the weight of <paramref name="symbol" /> in the tree.
		///     This function runs in O(map).
		/// </summary>
		/// <returns>The weight of the symbol or zero if not found.</returns>
		public uint GetWeight(TSymbolType symbol)
		{
			return GetWeightInternal(symbol);
		}

		/// <summary>
		///     Returns whether the tree has the given <paramref name="symbol" />.
		///     This function runs in O(map).
		/// </summary>
		public bool HasSymbol(TSymbolType symbol)
		{
			return _map.ContainsKey(symbol);
		}

		/// <summary>
		///     Updates the weight of <paramref name="symbol" /> by <paramref name="weightModifier" />.
		/// </summary>
		public void UpdateSymbol(TSymbolType symbol, int weightModifier)
		{
			UpdateSymbolInternal(symbol, weightModifier);
		}

		/// <summary>
		///     Writes the code for <paramref name="symbol" />.  If the symbol was not previously encountered,
		///     the bit sequence to mark a symbol literal is written to <paramref name="bitWriter" />
		///     and the symbol through <paramref name="symbolWriter" />.
		/// </summary>
		public void WriteCode(TSymbolType symbol, WriteBitDelegate bitWriter,
			WriteSymbolDelegate<TSymbolType> symbolWriter)
		{
			var nytSeen = false;
			if (HasSymbol(symbol))
			{
				WriteCodeInternal(symbol, bitWriter);
			}
			else
			{
				WriteCodeInternal(SymbolSpace.NotYetTransmitted, bitWriter);
				UpdateSymbolInternal(SymbolSpace.NotYetTransmitted, GetTweak(symbol, true));
				symbolWriter(symbol);
				nytSeen = true;
			}
			UpdateSymbolInternal(symbol, 1);
			if (!nytSeen)
			{
				UpdateSymbolInternal(SymbolSpace.NotYetTransmitted, GetTweak(symbol, false));
			}
		}

		/// <summary>
		///     Writes the table out to the provided delegates.  This is primarily for validation purposes of the algorithm.
		///     Functions are called based on the order nodes are encountered in the tree.
		///     The first value written is the number of unique symbols.
		///     Internal nodes write two values, the left child index and the right child index.
		/// </summary>
		public void WriteTable(WriteSymbolDelegate<TSymbolType> symbolWriter, WriteUInt32Delegate valueWriter,
			WriteNotYetTransmittedDelegate notYetTransmittedWriter)
		{
			valueWriter((uint) _map.Count);

			for (uint i = 0; i < _entries.Length; ++i)
			{
				if (_entries[i].IsLeaf)
				{
					if (_entries[i].Symbol.IsValid)
					{
						symbolWriter(_entries[i].Symbol.Symbol);
					}
					else
					{
						notYetTransmittedWriter();
					}
				}
				else
				{
					valueWriter(_entries[i].LeftChild);
					valueWriter(_entries[i].RightChild);

					// Technically unneeded, but consistent with static implementation.
				}
			}
		}

		/// <summary>
		///     Default NotYetImplemented tweaker.  Increments the weight by one when the NotYetTransmitted
		///     symbol is encountered, decrements it otherwise (to a minimum of zero).
		/// </summary>
		private static int DefaultNotYetTransmittedWeightTweak(uint treeHeight, uint nytLevel, uint treeWeight,
			uint nytWeight, uint symbolCount, TSymbolType symbol,
			bool nytOccurred)
		{
			return nytOccurred
				? 1
				: (nytWeight > 0 ? -1 : 0);
		}

		/// <summary>
		///     Adds the new symbol to the list with a weight of zero as the last entry.
		///     This function runs in O(map).
		/// </summary>
		/// <returns>
		///     The index to the new symbol.
		/// </returns>
		private uint AddNewSymbol(SymbolSpace symbol)
		{
			var oldMinWeightEntry = MinWeightEntry;
			var movedSymbol = _entries[oldMinWeightEntry].Symbol;

			{
				var newEntries = new Entry[_entries.Length + 2];
				Array.Copy(_entries, newEntries, _entries.Length);
				_entries = newEntries;
			}

			// Copy the previous entry
			_entries[oldMinWeightEntry + 1] = _entries[oldMinWeightEntry];

			// Make the old entry an internal node
			_entries[oldMinWeightEntry].FirstChildIndex = oldMinWeightEntry + 1;
			_entries[oldMinWeightEntry].Symbol.Clear();

			// Update the new entry
			_entries[oldMinWeightEntry + 2].Symbol = symbol;
			_entries[oldMinWeightEntry + 2].Height = 1;

			// Set parents
			_entries[oldMinWeightEntry + 1].ParentIndex = oldMinWeightEntry;
			_entries[oldMinWeightEntry + 2].ParentIndex = oldMinWeightEntry;

			RecomputeHeightsFrom(oldMinWeightEntry);

			_map[movedSymbol] = oldMinWeightEntry + 1;
			_map[symbol] = oldMinWeightEntry + 2;

			return oldMinWeightEntry + 2;
		}

		/// <summary>
		///     Decreases the weight of the node at <paramref name="index" />
		///     by <paramref name="weightModifier" />.
		///     This function runs in O(weightModifier * height).  Expected case is E(height).
		/// </summary>
		private void DecreaseIndex(uint index, int weightModifier)
		{
			// If we are decreasing the weight substantially, we may have to do the same 
			// block swapping operation multiple times to move this subtree down.
			var remainingWeightModifier = weightModifier;
			while (remainingWeightModifier > 0)
			{
				uint nextBlockWeight;
				var highestIndexOfSameWeight = GetHighestIndexOfSameWeight(index, out nextBlockWeight);
				SwapEntries(index, highestIndexOfSameWeight);
				index = highestIndexOfSameWeight;
				var weightToRemove = (uint) Math.Min(weightModifier, _entries[index].Weight - nextBlockWeight);
				_entries[index].Weight -= weightToRemove;
				remainingWeightModifier -= (int) weightToRemove;
				ToDot(DotOp.ChangeWeight, index, 0, -((int) weightToRemove), default(TSymbolType));
				if (index != Root)
				{
					var parentIndex = _entries[index].ParentIndex;
					DecreaseIndex(parentIndex, (int) weightToRemove);
				}
			}
		}

		/// <summary>
		///     Handles fixing parent indices, parent heights, and map indices from entry swaps
		///     starting from <paramref name="index" />.
		/// </summary>
		private void FixupEntry(uint index)
		{
			// Update child references to parent index
			if (_entries[index].FirstChildIndex != 0)
			{
				_entries[_entries[index].LeftChild].ParentIndex = index;
				_entries[_entries[index].RightChild].ParentIndex = index;
			}

			// Update height information
			RecomputeHeightsFrom(_entries[index].ParentIndex);

			if (_entries[index].FirstChildIndex == 0)
			{
				_map[_entries[index].Symbol] = index;
			}
		}

		/// <summary>
		///     This is a corresponding function to GetLowestIndexOfSameWeight handling
		///     finding the highest index of the same weight for decrementing the weight
		///     of the node at <paramref name="index" />.  It must skip child nodes
		///     resulting in a breadth first search through the subtree formed from
		///     <paramref name="index" /> for node indices to skip.  This results in
		///     poorer performance in comparison to its counterpart as a Queue is
		///     required for the traversal hitting memory more frequently.
		///     This function runs in O(SymbolCount).
		/// </summary>
		/// <returns>The highest index of the same weight</returns>
		private uint GetHighestIndexOfSameWeight(uint index, out uint nextBlockWeight)
		{
			nextBlockWeight = 0;
			var childIndicesToSkip = new Queue<uint>(16);
			var weight = _entries[index].Weight;
			var indexOfBestMatch = index;
			childIndicesToSkip.Enqueue(_entries[index].LeftChild);
			childIndicesToSkip.Enqueue(_entries[index].RightChild);
			var nextHighest = index + 1;
			while (nextHighest < _entries.Length)
			{
				if (nextHighest == childIndicesToSkip.Peek())
				{
					childIndicesToSkip.Dequeue();
					childIndicesToSkip.Enqueue(_entries[nextHighest].LeftChild);
					childIndicesToSkip.Enqueue(_entries[nextHighest].RightChild);
				}
				else
				{
					if (_entries[nextHighest].Weight == weight)
					{
						indexOfBestMatch = nextHighest;
					}
					else
					{
						nextBlockWeight = _entries[nextHighest].Weight;
						break;
					}
				}
				++nextHighest;
			}

			return indexOfBestMatch;
		}

		/// <summary>
		///     Returns the level in the tree <paramref name="symbol" /> occurs at.
		///     This function runs in O(map + level).
		/// </summary>
		private uint GetLevelInternal(SymbolSpace symbol)
		{
			uint level = 0;
			for (var index = _map[symbol]; index != Root; index = _entries[index].ParentIndex)
			{
				++level;
			}
			return level;
		}

		/// <summary>
		///     Gets the lowest index of same weight.
		///     Because nodes can have zero weight, it is possible for a parent node to have the same weight as a
		///     child node.  We can't select any of these nodes however as it will break the tree.
		///     So we do our search up to each ParentIndex effectively up the tree to ensure we don't select any
		///     node on the path to the root.
		///     This function runs in O(SymbolCount).
		/// </summary>
		/// <returns>
		///     The lowest index of same weight.
		/// </returns>
		private uint GetLowestIndexOfSameWeight(uint index, out uint nextBlockWeight)
		{
			nextBlockWeight = uint.MaxValue;
			var weight = _entries[index].Weight;
			var parentIndex = index;
			var indexOfBestMatch = index;
			var nextHighest = index;
			while (nextHighest > Root)
			{
				if (nextHighest != parentIndex)
				{
					if (_entries[nextHighest].Weight == weight)
					{
						indexOfBestMatch = nextHighest;
					}
					else
					{
						nextBlockWeight = _entries[nextHighest].Weight;
						break;
					}
				}
				else
				{
					parentIndex = _entries[parentIndex].ParentIndex;
				}
				--nextHighest;
			}
			return indexOfBestMatch;
		}

		/// <summary>
		///     Handles reading the symbol from the symbol space (including the NotYetTransmitted sequence).
		/// </summary>
		private SymbolSpace GetSymbolInternal(ReadBitDelegate bitReader)
		{
			if (_entries.Length > 1)
			{
				var index = Root;
				while (!_entries[index].IsLeaf)
				{
					index = _entries[index].GetIndex(bitReader());
				}
				return _entries[index].Symbol;
			}
			return SymbolSpace.NotYetTransmitted;
		}

		/// <summary>
		///     Determines weight modifier to apply to the NotYetTransmitted sequence based on
		///     the delegate provided (or the default).
		///     This function runs in at least O(map + level), not including the time for the delegate itself.
		/// </summary>
		/// <param name="symbol">The symbol encountered</param>
		/// <param name="occurred">Whether the NotYetTransmitted symbol occurred (instead of another symbol).</param>
		/// <returns>Amount to modify the weight of the NotYetTransmitted symbol.</returns>
		private int GetTweak(TSymbolType symbol, bool occurred)
		{
			return _tweaker(
				Height,
				GetLevelInternal(SymbolSpace.NotYetTransmitted),
				Weight,
				GetWeightInternal(SymbolSpace.NotYetTransmitted),
				(uint) _map.Count,
				symbol,
				occurred);
		}

		/// <summary>
		///     Returns the weight of <paramref name="symbol" /> in the tree.
		///     This function runs in O(map).
		/// </summary>
		/// <returns>The weight of the symbol or zero if not found.</returns>
		private uint GetWeightInternal(SymbolSpace symbol)
		{
			uint index;
			if (_map.TryGetValue(symbol, out index))
			{
				return _entries[index].Weight;
			}
			return 0;
		}

		/// <summary>
		///     Increases the weight of the node at <paramref name="index" />
		///     by <paramref name="weightModifier" />.
		///     This function runs in O(weightModifier * height).  Expected case is E(height).
		/// </summary>
		private void IncreaseIndex(uint index, int weightModifier)
		{
			if (uint.MaxValue - _entries[index].Weight < weightModifier)
			{
				throw new ArgumentOutOfRangeException(nameof(weightModifier));
			}

			// If we are increasing the weight substantially, we may have to do the same 
			// block swapping operation multiple times to move this subtree up.
			var remainingWeightModifier = weightModifier;
			while (remainingWeightModifier > 0)
			{
				uint nextBlockWeight;
				var lowestIndexOfSameWeight = GetLowestIndexOfSameWeight(index, out nextBlockWeight);
				SwapEntries(index, lowestIndexOfSameWeight);
				index = lowestIndexOfSameWeight;
				var weightToAdd = (uint) Math.Min(weightModifier, nextBlockWeight - _entries[index].Weight);
				_entries[index].Weight += weightToAdd;
				remainingWeightModifier -= (int) weightToAdd;
				ToDot(DotOp.ChangeWeight, index, 0, (int) weightToAdd, default(TSymbolType));
				if (index != Root)
				{
					var parentIndex = _entries[index].ParentIndex;
					IncreaseIndex(parentIndex, (int) weightToAdd);
				}
			}
		}

		/// <summary>
		///     Recomputes the heights of parent nodes from <paramref name="index" /> up the tree
		///     from adding or swapping children.
		///     This function runs in O(height) of the tree.
		/// </summary>
		private void RecomputeHeightsFrom(uint index)
		{
			for (; index != Root; index = _entries[index].ParentIndex)
			{
				_entries[index].Height = Math.Max(_entries[_entries[index].LeftChild].Height,
					_entries[_entries[index].RightChild].Height) + 1;
			}
			_entries[Root].Height = Math.Max(_entries[_entries[Root].LeftChild].Height,
				_entries[_entries[Root].RightChild].Height) + 1;
		}

		/// <summary>
		///     Simple generic function for swapping two structs.
		/// </summary>
		private void Swap<T>(ref T a, ref T b) where T : struct
		{
			var temp = a;
			a = b;
			b = temp;
		}

		/// <summary>
		///     Swaps the contents of the entries at indices <paramref name="a" /> and
		///     <paramref name="b" />.
		/// </summary>
		private void SwapEntries(uint a, uint b)
		{
			if (a != b)
			{
				// Swap entries
				Swap(ref _entries[a], ref _entries[b]);

				// Swap parent indices back
				Swap(ref _entries[a].ParentIndex, ref _entries[b].ParentIndex);

				// Fixup child references to parent and hight information.
				FixupEntry(a);
				FixupEntry(b);

				ToDot(DotOp.SwapNodes, a, b, 0, default(TSymbolType));
			}
		}

		/// <summary>
		///     Dumps the tree in linear order to the debugger.  By default compiled out.
		///     Recommend using a monospaced font for viewing.
		/// </summary>
		private void ToDot(DotOp op, uint nodeAffected, uint swapNode, int weightChange, TSymbolType addedSymbol)
		{
			if (DotWriter != null)
			{
				var builder = new StringBuilder();
				builder.AppendFormat("digraph {0} {{", op).AppendLine();
				builder.Append("\tgraph [ranksep=0];").AppendLine();
				builder.Append("\tnode [shape=record];").AppendLine();

				for (uint i = 0; i < _entries.Length; ++i)
				{
					builder.Append(_entries[i].ToDot(i, this)).AppendLine();
				}

				builder.AppendFormat("\tNode{0} [color=lawngreen];", nodeAffected).AppendLine();
				switch (op)
				{
					case DotOp.SwapNodes:
						builder.AppendFormat("\tNode{0} [color=lawngreen];", swapNode).AppendLine();
						builder.AppendFormat("\tlabel=\"Swapped nodes {0} and {1}.\"", nodeAffected, swapNode).
							AppendLine();
						break;
					case DotOp.AddSymbol:
						builder.AppendFormat("\tlabel=\"Added symbol {0} at index {1}.\"", addedSymbol, nodeAffected).
							AppendLine();
						break;
					case DotOp.ChangeWeight:
						builder.AppendFormat("\tlabel=\"Changed node weight for {0} by {1}.\"", nodeAffected,
							weightChange).
							AppendLine();
						break;
				}
				builder.Append("}}").AppendLine();
				DotWriter(builder.ToString());
			}
#if false
			int maxIndexWidth = (int) Math.Ceiling(Math.Log10(entries.Length - 1));
			int maxWeightWidth = (int) Math.Ceiling(Math.Log10(entries.Max(entry => entry.weight)));
			int maxSymbolWidth = entries.Max(entry => entry.FirstChildIndex != 0 ? 1 : entry.symbol.ToString().Length);

			int maxWidth = Math.Max(maxIndexWidth, Math.Max(maxWeightWidth, maxSymbolWidth));
			StringBuilder indices = new StringBuilder ();
			StringBuilder symbols = new StringBuilder ();
			StringBuilder weights = new StringBuilder ();
			StringBuilder left = new StringBuilder();
			StringBuilder right = new StringBuilder();
			StringBuilder parent = new StringBuilder();

			for( int i = 0; i < entries.Length; ++i) {
				indices.Append(" ").Append(i.ToString().PadRight(maxWidth, ' ')).Append(" |");
				symbols.Append(" ").Append((entries[i].FirstChildIndex != 0 ? "*" : entries[i].symbol.ToString()).PadRight(maxWidth, ' ')).Append(" |");
				weights.Append(" ").Append(entries[i].weight.ToString().PadRight(maxWidth, ' ')).Append(" |");
				parent.Append(" ").Append(entries[i].ParentIndex.ToString().PadRight(maxWidth, ' ')).Append(" |");
				if( entries[i].LeftChild != 0 )
				{
					left.Append(" ").Append(entries[i].LeftChild.ToString().PadRight(maxWidth, ' ')).Append(" |");
					right.Append(" ").Append(entries[i].RightChild.ToString().PadRight(maxWidth, ' ')).Append(" |");
				}
				else
				{
					left.Append(" ").Append("".PadRight(maxWidth, ' ')).Append(" |");
					right.Append(" ").Append("".PadRight(maxWidth, ' ')).Append(" |");
				}
			}

			StringBuilder mapData = new StringBuilder();
			foreach (KeyValuePair<SymbolSpace, uint> pair in map) {
				mapData.Append(pair.Key.ToString()).Append(": ");
				mapData.Append(pair.Value).Append(" ");
			}

			WriteLine("Dumping tree {0}...", detail);
			WriteLine(" Index:" + indices);
			WriteLine("Symbol:" + symbols);
			WriteLine("Weight:" + weights);
			WriteLine("Parent:" + parent);
			WriteLine("  Left:" + left);
			WriteLine(" Right:" + right);
			WriteLine(string.Empty);
			WriteLine("Map:   " + mapData);
#endif
		}

		/// <summary>
		///     Updates the weight of <paramref name="symbol" /> based on the provided <paramref name="weightModifier" />.
		///     The modification can be either positive or negative.
		///     This function runs in O(weightModifier * height).  Expected is E(height).
		/// </summary>
		private void UpdateSymbolInternal(SymbolSpace symbol, int weightModifier)
		{
			// Find out if the symbol is in the tree
			uint index;
			if (!_map.TryGetValue(symbol, out index))
			{
				index = AddNewSymbol(symbol);
				ToDot(DotOp.AddSymbol, index, 0, 0, symbol.Symbol);
			}

			if (weightModifier < 0
				&& _entries[index].Weight < -weightModifier)
			{
				throw new ArgumentOutOfRangeException(nameof(weightModifier));
			}

			if (weightModifier < 0)
			{
				DecreaseIndex(index, Math.Abs(weightModifier));
			}
			else
			{
				IncreaseIndex(index, weightModifier);
			}
		}

		/// <summary>
		///     Writes the code for the entry at <paramref name="index" /> to <paramref name="bitWriter" />.
		/// </summary>
		private void WriteCode(uint index, WriteBitDelegate bitWriter)
		{
			if (_entries.Length > 1)
			{
				var parentIndex = _entries[index].ParentIndex;
				if (parentIndex != Root)
				{
					WriteCode(parentIndex, bitWriter);
				}
				bitWriter(_entries[parentIndex].GetBit(index));
			}
		}

		/// <summary>
		///     Converts writing <paramref name="symbol" /> to writing based on its corresponding index in the tree.
		/// </summary>
		private void WriteCodeInternal(SymbolSpace symbol, WriteBitDelegate bitWriter)
		{
			WriteCode(_map[symbol], bitWriter);
		}

		private enum DotOp
		{
			AddSymbol,
			ChangeWeight,
			SwapNodes
		};

		private struct Entry
		{
			public uint FirstChildIndex;
			public uint Height;
			public uint ParentIndex;
			public SymbolSpace Symbol;
			public uint Weight;

			public bool IsLeaf
			{
				get { return FirstChildIndex == 0; }
			}

			public uint LeftChild
			{
				get { return FirstChildIndex; }
			}

			public uint RightChild
			{
				get { return FirstChildIndex + 1; }
			}

			public bool GetBit(uint index)
			{
				if (index != LeftChild && index != RightChild)
				{
					throw new ArgumentException("index");
				}
				return index == RightChild;
			}

			public uint GetIndex(bool bitValue)
			{
				return bitValue ? RightChild : LeftChild;
			}

			public string ToDot(uint myOrder, DynamicHuffman<TSymbolType> parent)
			{
				var builder = new StringBuilder();
				if (IsLeaf)
				{
					builder.AppendFormat("\tNode{0} [label=\"{{{{{0}|{1}|{2}}}|", myOrder, Symbol,
						Weight);
					parent.WriteCode(myOrder, value => builder.Append(value ? '1' : '0'));
					builder.Append("}}\"];");
				}
				else
				{
					builder.AppendFormat("\tNode{0} [label=\"{{{{{0}|{1}}}}}\"];", myOrder, Weight);
					builder.AppendFormat("\tNode{0} -> Node{1}", myOrder, LeftChild);
					builder.AppendFormat("\tNode{0} -> Node{1}", myOrder, RightChild);
				}
				return builder.ToString().Replace("<NYT>", "*NYT*");
			}
		};

		private struct SymbolSpace : IEquatable<SymbolSpace>, IEquatable<TSymbolType>, IComparable<SymbolSpace>
		{
			// ReSharper disable StaticFieldInGenericType
			public static readonly SymbolSpace NotYetTransmitted = new SymbolSpace

				// ReSharper restore StaticFieldInGenericType
			{
				_symbol = default(TSymbolType),
				IsValid = false
			};

			private TSymbolType _symbol;

			public bool IsValid { get; private set; }

			public TSymbolType Symbol
			{
				get { return _symbol; }
			}

			public void Clear()
			{
				_symbol = default(TSymbolType);
				IsValid = false;
			}

			public int CompareTo(SymbolSpace other)
			{
				if (IsValid && other.IsValid)
				{
					return _symbol.CompareTo(other._symbol);
				}
				return IsValid.CompareTo(other.IsValid);
			}

			public bool Equals(SymbolSpace other)
			{
				return IsValid
					? _symbol.CompareTo(other._symbol) == 0
					: IsValid == other.IsValid;
			}

			public bool Equals(TSymbolType other)
			{
				return IsValid && _symbol.CompareTo(other) == 0;
			}

			public override string ToString()
			{
				return IsValid ? _symbol.ToString() : "<NYT>";
			}

			public static implicit operator SymbolSpace(TSymbolType symbol)
			{
				var space = new SymbolSpace
				{
					_symbol = symbol,
					IsValid = true
				};

				return space;
			}
		};
	};
}
