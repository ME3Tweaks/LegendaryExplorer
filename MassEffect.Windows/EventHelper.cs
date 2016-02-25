using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace MassEffect.Windows
{
	/// <summary>
    /// Provides helper methods for raising events.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The <c>EventHelper</c> class provides methods that can be used to raise events. It avoids the need for explicitly checking event sinks for <see langword="null"/> before raising the event.
    /// </para>
    /// <para>
    /// The <see cref="Raise"/> overloads raise an event synchronously. All handlers will be invoked one after the other on the calling thread.
    /// </para>
    /// <para>
    /// The <see cref="BeginRaise"/> overloads raise an event asynchronously. All handlers will be invoked via a thread pool thread. No guarantees are made about the order in which handlers will be invoked,
    /// and multiple handlers may be invoked simultaneously. When calling a <see cref="BeginRaise"/> overload, two additional parameters may be provided: a callback and async state. If provided, the callback will
    /// be invoked once all handlers have been executed, and any async state will be passed into that callback (via an <see cref="IAsyncResult"/>).
    /// </para>
    /// </remarks>
    /// <example>
    /// The following example shows how a non-generic event can be raised:
    /// <code>
    /// public event EventHandler Changed;
    /// 
    /// protected void OnChanged()
    /// {
    ///     EventHelper.Raise(Changed, this);
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// The following example shows how a non-generic event can be raised where the event type requires a specific
    /// <c>EventArgs</c> subclass:
    /// <code>
    /// public event PropertyChangedEventHandler PropertyChanged;
    /// 
    /// protected void OnPropertyChanged(PropertyChangedEventArgs e)
    /// {
    ///     EventHelper.Raise(PropertyChanged, this, e);
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// The following example shows how a generic event can be raised:
    /// <code>
    /// public event EventHandler&lt;EventArgs&gt; Changed;
    /// 
    /// protected void OnChanged()
    /// {
    ///     EventHelper.Raise(Changed, this, EventArgs.Empty);
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// The following example shows how a generic event with custom event arguments can be raised:
    /// <code>
    /// public event EventHandler&lt;MyEventArgs&gt; MyEvent;
    /// 
    /// protected void OnMyEvent(MyEventArgs e)
    /// {
    ///     EventHelper.Raise(MyEventArgs, this, e);
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// The following example raises a generic event, but does not create the event arguments unless there is at least one
    /// handler for the event:
    /// <code>
    /// public event EventHandler&lt;MyEventArgs&gt; MyEvent;
    /// 
    /// protected void OnMyEvent(int someData)
    /// {
    ///     EventHelper.Raise(MyEvent, this, delegate
    ///     {
    ///        return new MyEventArgs(someData);
    ///     });
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// The following example raises an event asynchronously:
    /// <code>
    /// public event EventHandler&lt;MyEventArgs&gt; MyEvent;
    /// 
    /// protected void OnMyEvent(int someData)
    /// {
    ///     EventHelper.BeginRaise(MyEvent, this, new MyEventArgs(someData), null, null);
    /// }
    /// </code>
    /// </example>
    public static class EventHelper
    {
        private static readonly ExceptionHelper exceptionHelper = new ExceptionHelper(typeof(EventHelper));

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="Raise(EventHandler,object)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void Raise(EventHandler handler, object sender)
        {
            if (handler != null)
            {
                handler(sender, EventArgs.Empty);
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="Raise{T}(EventHandler{T},object,T)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void Raise<T>(EventHandler<T> handler, object sender, T e)
            where T : EventArgs
        {
            if (handler != null)
            {
                handler(sender, e);
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="Raise{T}(EventHandler{T},object,Func{T})"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void Raise<T>(EventHandler<T> handler, object sender, Func<T> createEventArguments)
            where T : EventArgs
        {
            ArgumentHelper.AssertNotNull(createEventArguments, "createEventArguments");

            if (handler != null)
            {
                handler(sender, createEventArguments());
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="Raise(Delegate,object,EventArgs)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void Raise(Delegate handler, object sender, EventArgs e)
        {
            if (handler != null)
            {
                handler.DynamicInvoke(sender, e);
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="BeginRaise(EventHandler,object,AsyncCallback,object)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void BeginRaise(EventHandler handler, object sender, AsyncCallback callback, object asyncState)
        {
            if (handler != null)
            {
                foreach (EventHandler invocation in handler.GetInvocationList())
                {
                    invocation.BeginInvoke(sender, EventArgs.Empty, callback, asyncState);
                }
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="BeginRaise{T}(EventHandler{T},object,T,AsyncCallback,object)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void BeginRaise<T>(EventHandler<T> handler, object sender, T e, AsyncCallback callback, object asyncState)
            where T : EventArgs
        {
            if (handler != null)
            {
                foreach (EventHandler<T> invocation in handler.GetInvocationList())
                {
                    invocation.BeginInvoke(sender, e, callback, asyncState);
                }
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="BeginRaise{T}(EventHandler{T},object,Func{T},AsyncCallback,object)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void BeginRaise<T>(EventHandler<T> handler, object sender, Func<T> createEventArguments, AsyncCallback callback, object asyncState)
            where T : EventArgs
        {
            ArgumentHelper.AssertNotNull(createEventArguments, "createEventArguments");

            if (handler != null)
            {
                foreach (EventHandler<T> invocation in handler.GetInvocationList())
                {
                    invocation.BeginInvoke(sender, createEventArguments(), callback, asyncState);
                }
            }
        }

        /// <include file='EventHelper.doc.xml' path='doc/member[@name="BeginRaise(Delegate,object,EventArgs,AsyncCallback,object)"]/*' />
        [SuppressMessage("Microsoft.Design", "CA1030", Justification = "False positive - the Raise method overloads are supposed to raise an event on behalf of a client, not on behalf of its declaring class.")]
        [DebuggerHidden]
        public static void BeginRaise(Delegate handler, object sender, EventArgs e, AsyncCallback callback, object asyncState)
        {
            if (handler != null)
            {
                var parameters = handler.Method.GetParameters();
                exceptionHelper.ResolveAndThrowIf(parameters.Length != 2 || parameters[0].ParameterType != typeof(object) || !typeof(EventArgs).IsAssignableFrom(parameters[1].ParameterType), "invalidDelegate");

                // since all we know is Delegate, we need to queue invocations on the thread pool and manage the callback ourselves
                var invocationList = handler.GetInvocationList();
                var remainingInvocations = invocationList.Length;

                foreach (var invocation in invocationList)
                {
                    var localInvocation = invocation;

                    ThreadPool.QueueUserWorkItem(
                        _ =>
                        {
                            localInvocation.DynamicInvoke(sender, e);

                            if (callback != null && Interlocked.Decrement(ref remainingInvocations) == 0)
                            {
                                callback(new BeginRaiseAsyncResult(asyncState));
                            }
                        });
                }
            }
        }

        // only used when BeginInvoke is called against a Delegate, since we need to manage the callback ourselves
        private sealed class BeginRaiseAsyncResult : IAsyncResult
        {
            private readonly object state;
            private WaitHandle waitHandle;

            public BeginRaiseAsyncResult(object state)
            {
                this.state = state;
            }

            public object AsyncState
            {
                get { return this.state; }
            }

            public WaitHandle AsyncWaitHandle
            {
                get
                {
                    if (this.waitHandle == null)
                    {
                        // note that event is already signalled because we represent an asynchronous operation that has already completed
                        this.waitHandle = new ManualResetEvent(true);
                    }

                    return this.waitHandle;
                }
            }

            public bool CompletedSynchronously
            {
                get { return false; }
            }

            public bool IsCompleted
            {
                get { return true; }
            }
        }
    }
}
