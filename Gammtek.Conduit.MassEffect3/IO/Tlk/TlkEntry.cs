using System;
using Gammtek.Conduit.ComponentModel;

namespace Gammtek.Conduit.IO.Tlk
{
	public class TlkEntry : BindableBase, IComparable<TlkEntry>, IComparable
	{
		public const string EmptyText = "-1";

		private TlkEntryGender _gender;
		private int _id;
		private int _position;
		private object _tag;
		private string _value;

		protected TlkEntry(TlkEntry entry)
		{
			if (entry == null)
			{
				throw new ArgumentNullException(nameof(entry));
			}

			Gender = entry.Gender;
			Id = entry.Id;
			Position = entry.Position;
			Tag = entry.Tag;
			Value = entry.Value;
		}

		public TlkEntry(int id = -1, string value = null, int position = 0, TlkEntryGender gender = TlkEntryGender.Male)
		{
			Gender = gender;
			Id = id;
			Position = position;
			Tag = null;
			Value = value ?? EmptyText;
		}

		public TlkEntryGender Gender
		{
			get { return _gender; }
			set { SetProperty(ref _gender, value); }
		}

		public int Id
		{
			get { return _id; }
			set { SetProperty(ref _id, value); }
		}

		public bool IsLocalizable
		{
			get { return Id >= 0 && ((Id & 134217728) != 0x8000000); }
		}

		public int Position
		{
			get { return _position; }
			set { SetProperty(ref _position, value); }
		}

		public object Tag
		{
			get { return _tag; }
			set { SetProperty(ref _tag, value); }
		}

		public string Value
		{
			get { return _value; }
			set { SetProperty(ref _value, value); }
		}

		#region Overrides of Object

		/// <summary>
		///     Returns a string that represents the current object.
		/// </summary>
		/// <returns>
		///     A string that represents the current object.
		/// </returns>
		public override string ToString()
		{
			return Value;
		}

		#endregion

		#region Implementation of IComparable

		/// <summary>
		///     Compares the current instance with another object of the same type and returns an integer that indicates whether the current instance
		///     precedes, follows, or occurs in the same position in the sort order as the other object.
		/// </summary>
		/// <returns>
		///     A value that indicates the relative order of the objects being compared. The return value has these meanings: Value Meaning Less than
		///     zero This instance precedes <paramref name="obj" /> in the sort order. Zero This instance occurs in the same position in the sort order
		///     as <paramref name="obj" />. Greater than zero This instance follows <paramref name="obj" /> in the sort order.
		/// </returns>
		/// <param name="obj">An object to compare with this instance. </param>
		/// <exception cref="T:System.ArgumentException"><paramref name="obj" /> is not the same type as this instance. </exception>
		public int CompareTo(object obj)
		{
			if (obj == null)
			{
				return 1;
			}

			if (!(obj is TlkEntry))
			{
				throw new ArgumentException("Argument must be a TlkEntry.");
			}

			var other = obj as TlkEntry;

			return Position.CompareTo(other.Position);
		}

		#endregion

		#region Implementation of IComparable<in TlkEntry>

		/// <summary>
		///     Compares the current object with another object of the same type.
		/// </summary>
		/// <returns>
		///     A value that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning
		///     Less than zero This object is less than the <paramref name="other" /> parameter.Zero This object is equal to <paramref name="other" />.
		///     Greater than zero This object is greater than <paramref name="other" />.
		/// </returns>
		/// <param name="other">An object to compare with this object.</param>
		public int CompareTo(TlkEntry other)
		{
			return Position.CompareTo(other.Position);
		}

		#endregion

		/*public char[] ToCharArray(bool ignoreNegativeIds = true)
		{
			var list = new List<char>();

			foreach (var str in this)
			{
				if (ignoreNegativeIds && str.Id < 0)
				{
					continue;
				}

				list.AddRange(str.Value.ToCharArray());
			}

			return list.ToArray();
		}*/
	}
}
