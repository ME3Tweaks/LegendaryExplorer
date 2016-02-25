using Gammtek.Conduit.ComponentModel;

namespace Gammtek.Conduit.UnrealEngine3.Core
{
	public class UStringRef : BindableBase
	{
		private int _index;
		private string _value;

		public UStringRef(int index = -1, string value = "None")
		{
			Index = index;
			Value = value;
		}

		public int Index
		{
			get { return _index; }
			set { SetProperty(ref _index, value); }
		}

		public string Value
		{
			get { return _value; }
			set { SetProperty(ref _value, value); }
		}

		public static implicit operator UStringRef(string value)
		{
			return new UStringRef(value: value);
		}

		public static implicit operator UStringRef(int value)
		{
			return new UStringRef(value);
		}

		public static implicit operator int(UStringRef stringRef)
		{
			return stringRef.Index;
		}

		public static implicit operator string(UStringRef stringRef)
		{
			return stringRef.Value;
		}

		public override string ToString()
		{
			return string.IsNullOrEmpty(Value) ? string.Format("{{{0}}}", Index) : Value;
		}
	}
}
