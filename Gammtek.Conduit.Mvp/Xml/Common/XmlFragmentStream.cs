using System;
using System.IO;
using System.Text;
using Gammtek.Conduit.Mvp.Properties;

namespace Gammtek.Conduit.Mvp.Xml.Common
{
	[Obsolete("Use XmlFragmentReader instead.", false)]
	public class XmlFragmentStream : Stream
	{
		// Holds the inner stream with the XML fragments.
		private readonly byte[] _rootend = Encoding.UTF8.GetBytes("</root>");
		private readonly byte[] _rootstart = Encoding.UTF8.GetBytes("<root>");
		private readonly Stream _stream;

		private bool _done;
		private int _endidx = -1;
		private bool _eof;
		private bool _first = true;

		public XmlFragmentStream(Stream innerStream)
		{
			if (innerStream == null)
			{
				throw new ArgumentNullException("innerStream");
			}
			_stream = innerStream;
		}

		public XmlFragmentStream(Stream innerStream, string rootName)
			: this(innerStream)
		{
			_rootstart = Encoding.UTF8.GetBytes("<" + rootName + ">");
			_rootend = Encoding.UTF8.GetBytes("</" + rootName + ">");
		}

		public XmlFragmentStream(Stream innerStream, string rootName, string ns)
			: this(innerStream)
		{
			_rootstart = Encoding.UTF8.GetBytes("<" + rootName + " xmlns=\"" + ns + "\">");
			_rootend = Encoding.UTF8.GetBytes("</" + rootName + ">");
		}

		public override bool CanRead
		{
			get { return _stream.CanRead; }
		}

		public override bool CanSeek
		{
			get { return _stream.CanSeek; }
		}

		public override bool CanWrite
		{
			get { return _stream.CanWrite; }
		}

		public override long Length
		{
			get { return _stream.Length; }
		}

		public override long Position
		{
			get { return _stream.Position; }
			set { _stream.Position = value; }
		}

		public override void Close()
		{
			_stream.Close();
			base.Close();
		}

		public override void Flush()
		{
			_stream.Flush();
		}

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (_done)
			{
				if (_eof)
				{
					throw new EndOfStreamException(Resources.XmlFragmentStream_EOF);
				}

				_eof = true;
				return 0;
			}

			// If this is the first one, return the wrapper root element.
			if (_first)
			{
				_rootstart.CopyTo(buffer, 0);

				_stream.Read(buffer, _rootstart.Length, count - _rootstart.Length);

				_first = false;
				return count;
			}

			// We have a pending closing wrapper root element.
			if (_endidx != -1)
			{
				for (var i = _endidx; i < _rootend.Length; i++)
				{
					buffer[i] = _rootend[i];
				}

				return _rootend.Length - _endidx;
			}

			var ret = _stream.Read(buffer, offset, count);

			// Did we reached the end?
			if (ret >= count)
			{
				return ret;
			}

			_rootend.CopyTo(buffer, ret);

			if (count - ret > _rootend.Length)
			{
				_done = true;
				return ret + _rootend.Length;
			}

			_endidx = count - ret;
			return count;
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			return _stream.Seek(offset, origin);
		}

		public override void SetLength(long value)
		{
			_stream.SetLength(value);
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			_stream.Write(buffer, offset, count);
		}
	}
}
