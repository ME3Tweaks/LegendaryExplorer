namespace MassEffect3.FileFormats.Unreal
{
	public interface ISerializable
	{
		void Serialize(ISerializer stream);
	}
}