namespace Gammtek.Conduit.UnrealEngine3.Serialization
{
	public interface IBuffered
	{
		IUnrealStream Buffer { get; }

		int BufferPosition { get; }

		int BufferSize { get; }

		byte[] CopyBuffer();

		string GetBufferId(bool fullName = false);
	}
}
