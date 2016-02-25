using System;

namespace MassEffect3.Tlk
{
	public class TlkEntry : IComparable, IComparable<TlkEntry>
	{
		public TlkEntry(int id = -1, string text = "", int position = 0, TlkEntryGender gender = TlkEntryGender.Male)
		{
			Gender = gender;
			Id = id;
			Position = position;
			Text = text;
		}

		public TlkEntry(TlkEntry other)
		{
			Gender = other.Gender;
			Id = other.Id;
			Position = other.Position;
			Text = other.Text;
		}

		public TlkEntryGender Gender { get; set; }

		public int Id { get; set; }

		public bool IsReadable
		{
			get
			{
				return Id >= 0 && (Id & 134217728) != 134217728;
			}
		}

		public int Position { get; set; }

		public string Text { get; set; }

		public int CompareTo(object obj)
		{
			return Position.CompareTo(((TlkEntry)obj).Position);
		}

		public int CompareTo(TlkEntry other)
		{
			return Position.CompareTo(other.Position);
		}

		public TlkEntry Clone()
		{
			return new TlkEntry(this);
		}

		public override string ToString()
		{
			return Text;
		}
	}
}