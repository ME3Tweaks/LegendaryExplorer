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


namespace Gammtek.Conduit.Compression.Huffman
{
	/// <summary>
	///     Delegate for writing a single bit <paramref name="value" />.
	/// </summary>
	public delegate void WriteBitDelegate(bool value);

	/// <summary>
	///     Delegate for reading a single bit.
	/// </summary>
	public delegate bool ReadBitDelegate();

	/// <summary>
	///     Delegate for manipulating the weight of the NotYetTransmitted marker.
	/// </summary>
	/// <param name="treeHeight">Height of the Huffman tree</param>
	/// <param name="nytLevel">Level of the NotYetTransmitted marker</param>
	/// <param name="treeWeight">Weight of the entire tree</param>
	/// <param name="nytWeight">Weight of the NotYetTransmitted marker</param>
	/// <param name="symbolCount">Number of symbols in the tree</param>
	/// <param name="symbol">The symbol that was encountered</param>
	/// <param name="nytOccurred">Whether the NotYetTransmitted marker just occurred or whether another symbol was processed</param>
	/// <returns>Amount to adjust the weight of the NotYetTransmitted marker</returns>
	public delegate int NotYetTransmittedWeightTweakDelegate<TSymbolType>(
		uint treeHeight, uint nytLevel, uint treeWeight, uint nytWeight, uint symbolCount, TSymbolType symbol, bool nytOccurred);

	/// <summary>
	///     Delegate for writing a <paramref name="symbol" />.
	/// </summary>
	public delegate void WriteSymbolDelegate<TSymbolType>(TSymbolType symbol) where TSymbolType : struct;

	/// <summary>
	///     Delegate for writing the NotYetTransmitted marker from the Huffman tree during WriteTable.
	/// </summary>
	public delegate void WriteNotYetTransmittedDelegate();

	/// <summary>
	///     Delegate for reading a symbol.
	/// </summary>
	public delegate TSymbolType ReadSymbolDelegate<TSymbolType>();

	/// <summary>
	///     Delegate for reading an unsigned integer (for Huffman table processing).
	/// </summary>
	public delegate uint ReadUInt32Delegate();

	/// <summary>
	///     Delegate for writing an unsigned integer <paramref name="value" /> (for Huffman table processing).
	/// </summary>
	public delegate void WriteUInt32Delegate(uint value);

	/// <summary>
	///     Writes a DOT formatted graph of the current Huffman tree potentially with the update operation.
	/// </summary>
	public delegate void WriteDotStringDelegate(string dotGraph);
}
