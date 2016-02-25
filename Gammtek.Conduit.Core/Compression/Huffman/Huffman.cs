using System;
using System.Collections.Generic;
using Gammtek.Conduit.Collections.Generic;

namespace Gammtek.Conduit.Compression.Huffman
{
	public class Huffman<T>
		where T : IComparable
	{
		private readonly Dictionary<T, HuffmanNode<T>> _leafDictionary = new Dictionary<T, HuffmanNode<T>>();
		private readonly HuffmanNode<T> _root;

		public Huffman(IEnumerable<T> values)
		{
			var counts = new Dictionary<T, int>();
			var priorityQueue = new PriorityQueue<HuffmanNode<T>>();
			var valueCount = 0;

			foreach (var value in values)
			{
				if (!counts.ContainsKey(value))
				{
					counts[value] = 0;
				}
				counts[value]++;
				valueCount++;
			}

			foreach (var value in counts.Keys)
			{
				var node = new HuffmanNode<T>((double) counts[value] / valueCount, value);
				priorityQueue.Add(node);
				_leafDictionary[value] = node;
			}

			while (priorityQueue.Count > 1)
			{
				var leftSon = priorityQueue.Pop();
				var rightSon = priorityQueue.Pop();
				var parent = new HuffmanNode<T>(leftSon, rightSon);
				priorityQueue.Add(parent);
			}

			_root = priorityQueue.Pop();
			_root.IsZero = false;
		}

		public T Decode(List<int> bitString, ref int position)
		{
			var nodeCur = _root;
			while (!nodeCur.IsLeaf)
			{
				if (position > bitString.Count)
				{
					throw new ArgumentException("Invalid bitstring in Decode");
				}
				nodeCur = bitString[position++] == 0 ? nodeCur.LeftSon : nodeCur.RightSon;
			}
			return nodeCur.Value;
		}

		public List<T> Decode(List<int> bitString)
		{
			var position = 0;
			var returnValue = new List<T>();

			while (position != bitString.Count)
			{
				returnValue.Add(Decode(bitString, ref position));
			}
			return returnValue;
		}

		public List<int> Encode(T value)
		{
			var returnValue = new List<int>();
			Encode(value, returnValue);
			return returnValue;
		}

		public void Encode(T value, List<int> encoding)
		{
			if (!_leafDictionary.ContainsKey(value))
			{
				throw new ArgumentException("Invalid value in Encode");
			}
			var nodeCur = _leafDictionary[value];
			var reverseEncoding = new List<int>();
			while (!nodeCur.IsRoot)
			{
				reverseEncoding.Add(nodeCur.Bit);
				nodeCur = nodeCur.Parent;
			}

			reverseEncoding.Reverse();
			encoding.AddRange(reverseEncoding);
		}

		public List<int> Encode(IEnumerable<T> values)
		{
			var returnValue = new List<int>();

			foreach (var value in values)
			{
				Encode(value, returnValue);
			}
			return returnValue;
		}
	}
}
