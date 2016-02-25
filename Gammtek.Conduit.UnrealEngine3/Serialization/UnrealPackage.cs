using System;

namespace Gammtek.Conduit.UnrealEngine3.Serialization
{
	public sealed class UnrealPackage : IDisposable, IBuffered
	{
		#region Implementation of IDisposable

		public void Dispose()
		{
			throw new NotImplementedException();
		}

		#endregion

		#region Implementation of IBuffered

		public IUnrealStream Buffer { get; private set; }

		public int BufferPosition { get; private set; }

		public int BufferSize { get; private set; }

		public byte[] CopyBuffer()
		{
			throw new NotImplementedException();
		}

		public string GetBufferId(bool fullName = false)
		{
			throw new NotImplementedException();
		}

		#endregion
	}
}
