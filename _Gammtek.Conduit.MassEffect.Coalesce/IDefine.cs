namespace Gammtek.Conduit.MassEffect.Coalesce
{
	public interface IDefine<out T>
	{
		string Name { get; }

		T Value { get; }
	}
}
