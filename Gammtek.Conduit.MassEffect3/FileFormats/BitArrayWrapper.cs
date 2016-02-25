using System;
using System.Collections;

namespace MassEffect3.FileFormats
{
	public class BitArrayWrapper : IList
	{
		public readonly BitArray Target;

		public BitArrayWrapper(BitArray target)
		{
			if (target == null)
			{
				throw new ArgumentNullException(nameof(target), "target cannot be null");
			}

			Target = target;
		}

		#region IEnumerable Members

		IEnumerator IEnumerable.GetEnumerator()
		{
			return Target.GetEnumerator();
		}

		#endregion

		#region IList Members

		int IList.Add(object value)
		{
			if ((value is bool) == false)
			{
				throw new ArgumentException("value");
			}

			var index = Target.Length;
			Target.Length++;
			Target[index] = (bool) value;
			return index;
		}

		void IList.Clear()
		{
			Target.Length = 0;
		}

		bool IList.Contains(object value)
		{
			throw new NotSupportedException();
		}

		int IList.IndexOf(object value)
		{
			throw new NotSupportedException();
		}

		void IList.Insert(int index, object value)
		{
			if ((value is bool) == false)
			{
				throw new ArgumentException("value");
			}

			if (index >= Target.Length)
			{
				Target.Length = index + 1;
				Target[index] = (bool) value;
			}
			else
			{
				Target.Length++;
				for (var i = Target.Length - 1; i > index; i--)
				{
					Target[i] = Target[i - 1];
				}
				Target[index] = (bool) value;
			}
		}

		bool IList.IsFixedSize
		{
			get { return false; }
		}

		bool IList.IsReadOnly
		{
			get { return false; }
		}

		void IList.Remove(object value)
		{
			throw new NotSupportedException();
		}

		void IList.RemoveAt(int index)
		{
			if (index >= Target.Length)
			{
				throw new IndexOutOfRangeException();
			}

			for (var i = Target.Length - 1; i > index; i--)
			{
				Target[i - 1] = Target[i];
			}
			Target.Length--;
		}

		object IList.this[int index]
		{
			get { return Target[index]; }
			set
			{
				if ((value is bool) == false)
				{
					throw new ArgumentException("value");
				}

				Target[index] = (bool) value;
			}
		}

		#endregion

		#region ICollection Members

		void ICollection.CopyTo(Array array, int index)
		{
			for (var i = 0; i < Target.Length; i++)
			{
				array.SetValue(Target[i], index + i);
			}
		}

		int ICollection.Count
		{
			get { return Target.Length; }
		}

		bool ICollection.IsSynchronized
		{
			get { return false; }
		}

		object ICollection.SyncRoot
		{
			get { return this; }
		}

		#endregion

		public bool Item { get; private set; }
	}
}