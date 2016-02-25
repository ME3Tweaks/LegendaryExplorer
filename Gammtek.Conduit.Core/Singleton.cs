using System;

namespace Gammtek.Conduit
{
	/// <summary>
	///     A base class for the singleton design pattern.
	/// </summary>
	/// <typeparam name="T">Class type of the singleton</typeparam>
	public abstract class Singleton/*Base*/<T>
		where T : class
	{
		/// <summary>
		///     Static instance. Needs to use lambda expression
		///     to construct an instance (since constructor is private).
		/// </summary>
		private static readonly Lazy<T> SInstance = new Lazy<T>(CreateInstanceOfT);

		/// <summary>
		///     Gets the instance of this singleton.
		/// </summary>
		public static T Instance => SInstance.Value;

		/// <summary>
		///     Creates an instance of T via reflection since T's constructor is expected to be private.
		/// </summary>
		/// <returns></returns>
		private static T CreateInstanceOfT()
		{
			return Activator.CreateInstance(typeof (T), true) as T;
		}
	}

	/*public abstract class Singleton<T>
		where T : class, new()
	{
		private static readonly Lazy<T> Lazy = new Lazy<T>(() => new T());

		public static T Instance => Lazy.Value;
	}*/
}
