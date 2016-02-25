using System;
using System.Diagnostics;

namespace MassEffect.Windows.Extensions
{
	public static class EventHelperExtensions
	{
		[DebuggerHidden]
		public static void Raise(this EventHandler handler, object sender)
		{
			EventHelper.Raise(handler, sender);
		}

		[DebuggerHidden]
		public static void Raise<T>(this EventHandler<T> handler, object sender, T e)
			where T : EventArgs
		{
			EventHelper.Raise(handler, sender, e);
		}

		[DebuggerHidden]
		public static void Raise<T>(this EventHandler<T> handler, object sender, Func<T> createEventArguments)
			where T : EventArgs
		{
			EventHelper.Raise(handler, sender, createEventArguments);
		}

		[DebuggerHidden]
		public static void Raise(this Delegate handler, object sender, EventArgs e)
		{
			EventHelper.Raise(handler, sender, e);
		}

		[DebuggerHidden]
		public static void BeginRaise(this EventHandler handler, object sender, AsyncCallback callback, object asyncState)
		{
			EventHelper.BeginRaise(handler, sender, callback, asyncState);
		}

		[DebuggerHidden]
		public static void BeginRaise<T>(this EventHandler<T> handler, object sender, T e, AsyncCallback callback, object asyncState)
			where T : EventArgs
		{
			EventHelper.BeginRaise(handler, sender, e, callback, asyncState);
		}

		[DebuggerHidden]
		public static void BeginRaise<T>(this EventHandler<T> handler, object sender, Func<T> createEventArguments, AsyncCallback callback,
			object asyncState)
			where T : EventArgs
		{
			EventHelper.BeginRaise(handler, sender, createEventArguments, callback, asyncState);
		}

		[DebuggerHidden]
		public static void BeginRaise(this Delegate handler, object sender, EventArgs e, AsyncCallback callback, object asyncState)
		{
			EventHelper.BeginRaise(handler, sender, e, callback, asyncState);
		}
	}
}
