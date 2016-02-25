using System;
using System.Collections.Generic;

namespace MassEffect3.TlkEditor
{
	public class TlkFile
	{
		public TlkFile(List<TlkString> strings = null, List<string> includes = null)
		{
			MaleStrings = strings ?? new List<TlkString>();
			Includes = includes ?? new List<string>();
		}

		public int Count
		{
			get { return MaleStrings.Count; }
		}

		public string Id { get; set; }

		public List<string> Includes { get; set; } 

		public string Name { get; set; }

		public int Position { get; set; }

		public string Source { get; set; }

		public List<TlkString> MaleStrings { get; set; }

		public List<TlkString> FemaleStrings { get; set; }

		public TlkString this[int index]
		{
			get { return MaleStrings[index]; }
			set { MaleStrings[index] = value; }
		}

		public void Add(TlkString tlkString)
		{
			MaleStrings.Add(tlkString);
		}

		public void Add(int id, string value = TlkString.EmptyText, int position = 0)
		{
			MaleStrings.Add(new TlkString(id, value, position));
		}

		public void Clear()
		{
			MaleStrings.Clear();
		}

		public bool Remove(TlkString tlkString)
		{
			return MaleStrings.Remove(tlkString);
		}

		public int RemoveAll(Predicate<TlkString> match)
		{
			return MaleStrings.RemoveAll(match);
		}

		public void RemoveAt(int index)
		{
			MaleStrings.RemoveAt(index);
		}
	}
}