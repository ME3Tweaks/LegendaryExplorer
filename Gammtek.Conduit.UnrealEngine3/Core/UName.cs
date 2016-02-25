using Gammtek.Conduit.ComponentModel;
using Gammtek.Conduit.Extensions;

namespace Gammtek.Conduit.UnrealEngine3.Core
{
	/// <summary>
	/// </summary>
	public class UName : BindableBase
	{
		/// <summary>
		/// </summary>
		public const int DefaultFlags = 0;

		/// <summary>
		/// </summary>
		public const int DefaultIndex = -1;

		/// <summary>
		/// </summary>
		public const string DefaultValue = null;

		private int _flags;
		private int _index;
		private string _value;

		/// <summary>
		/// </summary>
		/// <param name="value"></param>
		/// <param name="index"></param>
		/// <param name="flags"></param>
		public UName(string value = DefaultValue, int index = DefaultIndex, int flags = DefaultFlags)
		{
			Flags = flags;
			Index = index;
			Value = value;
		}

		/// <summary>
		/// </summary>
		/// <param name="other"></param>
		public UName(UName other)
		{
			if (other == null)
			{
				ThrowHelper.ThrowArgumentNullException("other");
			}

			Flags = other.Flags;
			Index = other.Index;
			Value = other.Value;
		}

		/// <summary>
		/// </summary>
		public int Flags
		{
			get { return _flags; }
			set { SetProperty(ref _flags, value); }
		}

		/// <summary>
		/// </summary>
		public int Index
		{
			get { return _index; }
			set { SetProperty(ref _index, value); }
		}

		/// <summary>
		/// </summary>
		public string Value
		{
			get { return _value; }
			set { SetProperty(ref _value, value); }
		}

		/// <summary>
		/// </summary>
		/// <returns></returns>
		public override string ToString()
		{
			return string.IsNullOrEmpty(Value) ? string.Format("{{{0}}}", Index) : Value;
		}

		/// <summary>
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static implicit operator UName(string value)
		{
			return new UName(value);
		}

		/// <summary>
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static implicit operator UName(int value)
		{
			return new UName(index: value);
		}

		/// <summary>
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static implicit operator UName(long value)
		{
			int index, flags;
			value.Split(out index, out flags);

			return new UName(index: index, flags: flags);
		}

		/// <summary>
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static implicit operator int(UName name)
		{
			return name.Index;
		}

		/// <summary>
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static implicit operator long(UName name)
		{
			return name.Index.Join(name.Flags);
		}

		/// <summary>
		/// </summary>
		/// <param name="name"></param>
		/// <returns></returns>
		public static implicit operator string(UName name)
		{
			return name.Value;
		}
	}
}
