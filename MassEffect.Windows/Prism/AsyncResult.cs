using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace MassEffect.Windows.Prism
{
	[SuppressMessage("Microsoft.Design", "CA1001",
		Justification = "Calling the End method, which is part of the contract of using an IAsyncResult, releases the IDisposable.")]
	public class AsyncResult<T> : IAsyncResult
	{
		private readonly AsyncCallback _asyncCallback;
		private readonly object _asyncState;
		private readonly object _lockObject;
		private bool _endCalled;
		private Exception _exception;
		private ManualResetEvent _waitHandle;

		public AsyncResult(AsyncCallback asyncCallback, object asyncState)
		{
			_lockObject = new object();
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		public T Result { get; private set; }

		public object AsyncState
		{
			get { return _asyncState; }
		}

		public WaitHandle AsyncWaitHandle
		{
			get
			{
				lock (_lockObject)
				{
					if (_waitHandle == null)
					{
						_waitHandle = new ManualResetEvent(IsCompleted);
					}
				}

				return _waitHandle;
			}
		}

		public bool CompletedSynchronously { get; private set; }

		public bool IsCompleted { get; private set; }

		[SuppressMessage("Microsoft.Design", "CA1000:DoNotDeclareStaticMembersOnGenericTypes",
			Justification = "Entry point to be used to implement End* methods.")]
		public static AsyncResult<T> End(IAsyncResult asyncResult)
		{
			var localResult = asyncResult as AsyncResult<T>;
			if (localResult == null)
			{
				throw new ArgumentNullException("asyncResult");
			}

			lock (localResult._lockObject)
			{
				if (localResult._endCalled)
				{
					//throw new InvalidOperationException(Resources.EndMethodAlreadyCalled);
					throw new InvalidOperationException("End method already called.");
				}

				localResult._endCalled = true;
			}

			if (!localResult.IsCompleted)
			{
				localResult.AsyncWaitHandle.WaitOne();
			}

			if (localResult._waitHandle != null)
			{
				localResult._waitHandle.Close();
			}

			if (localResult._exception != null)
			{
				throw localResult._exception;
			}

			return localResult;
		}

		public void SetComplete(T result, bool completedSynchronously)
		{
			Result = result;

			DoSetComplete(completedSynchronously);
		}

		public void SetComplete(Exception e, bool completedSynchronously)
		{
			_exception = e;

			DoSetComplete(completedSynchronously);
		}

		private void DoSetComplete(bool completedSynchronously)
		{
			if (completedSynchronously)
			{
				CompletedSynchronously = true;
				IsCompleted = true;
			}
			else
			{
				lock (_lockObject)
				{
					IsCompleted = true;
					if (_waitHandle != null)
					{
						_waitHandle.Set();
					}
				}
			}

			if (_asyncCallback != null)
			{
				_asyncCallback(this);
			}
		}
	}
}
