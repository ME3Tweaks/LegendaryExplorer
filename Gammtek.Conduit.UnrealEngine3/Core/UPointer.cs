namespace Gammtek.Conduit.UnrealEngine3.Core
{
	public struct UPointer
	{
		public UPointer(int dummy = 0)
			: this()
		{
			Dummy = dummy;
		}

		public int Dummy { get; set; }

		public static implicit operator UPointer(int i)
		{
			return new UPointer(i);
		}

		public static implicit operator int(UPointer pointer)
		{
			return pointer.Dummy;
		}
	}
}
