namespace Gammtek.Conduit.MassEffect.ModBuilder
{
	public interface IDefine<out T>
	{
		string Name { get; }

		T Value { get; }
	}
}
