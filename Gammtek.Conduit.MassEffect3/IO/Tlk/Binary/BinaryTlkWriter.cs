using System;
using System.IO;

namespace Gammtek.Conduit.IO.Tlk.Binary
{
	internal class BinaryTlkWriter : IDisposable
	{
		public BinaryTlkWriter(Stream stream)
		{
			if (stream == null)
			{
				throw new ArgumentNullException(nameof(stream));
			}

			Writer = new DataWriter(stream);
		}

		public virtual Stream BaseStream
		{
			get { return (Writer != null) ? Writer.BaseStream : null; }
		}

		public DataWriter Writer { get; protected set; }

		protected virtual void Dispose(bool disposing)
		{
			if (disposing && Writer != null)
			{
				Writer.Close();
			}

			Writer = null;
		}

		#region Implementation of IDisposable

		/// <summary>
		///     Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
		/// </summary>
		public void Dispose()
		{
			Dispose(true);
		}

		#endregion
	}
}
