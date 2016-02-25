using System;

namespace Gammtek.Conduit.Compression.Huffman
{
	public class HuffmanNode<T> : IComparable
	{
		public HuffmanNode(double probability, T value)
		{
			Probability = probability;
			LeftSon = RightSon = Parent = null;
			Value = value;
			IsLeaf = true;
		}

		public HuffmanNode(HuffmanNode<T> leftSon, HuffmanNode<T> rightSon)
		{
			LeftSon = leftSon;
			RightSon = rightSon;
			Probability = leftSon.Probability + rightSon.Probability;
			leftSon.IsZero = true;
			rightSon.IsZero = false;
			leftSon.Parent = rightSon.Parent = this;
			IsLeaf = false;
		}

		public int Bit => IsZero ? 0 : 1;

		public bool IsLeaf { get; set; }

		public bool IsRoot => Parent == null;

		public bool IsZero { get; set; }

		public HuffmanNode<T> LeftSon { get; set; }

		public HuffmanNode<T> Parent { get; set; }

		public double Probability { get; set; }

		public HuffmanNode<T> RightSon { get; set; }

		public T Value { get; set; }

		public int CompareTo(object obj)
		{
			return -Probability.CompareTo(((HuffmanNode<T>) obj).Probability);
		}
	}
}
