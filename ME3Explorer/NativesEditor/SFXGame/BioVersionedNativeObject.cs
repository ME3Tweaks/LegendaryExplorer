using Gammtek.Conduit.ComponentModel;

namespace Gammtek.Conduit.MassEffect3.SFXGame
{
	/// <summary>
	/// </summary>
	public abstract class BioVersionedNativeObject : BindableBase, IBioVersionedNativeObject
	{
		/// <summary>
		/// </summary>
		public const int DefaultInstanceVersion = 0;

		private int _instanceVersion;

		/// <summary>
		/// </summary>
		/// <param name="instanceVersion"></param>
		protected BioVersionedNativeObject(int instanceVersion = DefaultInstanceVersion)
		{
			InstanceVersion = instanceVersion;
		}

		protected BioVersionedNativeObject(BioVersionedNativeObject other)
		{
			if (other == null)
			{
				ThrowHelper.ThrowArgumentNullException(nameof(other));
			}

			InstanceVersion = other.InstanceVersion;
		}

		/// <summary>
		/// </summary>
		public int InstanceVersion
		{
			get { return _instanceVersion; }
			set { SetProperty(ref _instanceVersion, value); }
		}
	}
}
