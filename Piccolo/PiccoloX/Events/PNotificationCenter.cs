/* 
 * Copyright (c) 2003-2006, University of Maryland
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without modification, are permitted provided
 * that the following conditions are met:
 * 
 *		Redistributions of source code must retain the above copyright notice, this list of conditions
 *		and the following disclaimer.
 * 
 *		Redistributions in binary form must reproduce the above copyright notice, this list of conditions
 *		and the following disclaimer in the documentation and/or other materials provided with the
 *		distribution.
 * 
 *		Neither the name of the University of Maryland nor the names of its contributors may be used to
 *		endorse or promote products derived from this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND ANY EXPRESS OR IMPLIED
 * WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A
 * PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
 * ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS
 * INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR
 * TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
 * ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 * 
 * Piccolo was written at the Human-Computer Interaction Laboratory www.cs.umd.edu/hcil by Jesse Grosjean
 * and ported to C# by Aaron Clamage under the supervision of Ben Bederson.  The Piccolo website is
 * www.cs.umd.edu/hcil/piccolo.
 * 
 * This class PNotification center is derived from the class NSNotificationCenter from:
 * 
 * Wotonomy: OpenStep design patterns for pure Java applications.
 * Copyright (C) 2000 Blacksmith, Inc.
 */

using System;
using System.Collections;
using System.Reflection;
 
namespace UMD.HCIL.PiccoloX.Events {
	/// <summary>
	/// <b>PNotificationCenter</b> provides a way for objects that don’t know about
	/// each other to communicate.
	/// </summary>
	/// <remarks>
	/// This class receives PNotification objects and broadcasts them to all interested
	/// listeners.  Unlike standard C# events, the event handlers don't need to know about
	/// the event source, and the event source doesn't need to maintain event delegates.
	/// <para>
	/// Listeners of the notfications center are held by weak references.  So the
	/// notfication center will not create garbage collection problems as standard event
	/// listeners do.
	/// </para>
	/// </remarks>
	public class PNotificationCenter {
		#region Fields
		private static ArrayList TEMP_LIST = new ArrayList();

		/// <summary>
		/// Used to represent a null name or object in the listenersMap.
		/// </summary>
		public static readonly Object NULL_MARKER = new Object();

		/// <summary>
		/// The default notification center for registering listeners.
		/// </summary>
		protected static PNotificationCenter DEFAULT_CENTER;

		/// <summary>
		/// A hash table of all listeners registered with this notification center.
		/// </summary>
		protected Hashtable listenersMap;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PNotificationCenter.
		/// </summary>
		private PNotificationCenter() {
			listenersMap = new Hashtable();
		}

		/// <summary>
		/// Gets the default notification center for registering listeners.
		/// </summary>
		/// <value>The default notification center for registering listeners.</value>
		/// <remarks>
		/// This property returns a static PNotificationCenter that is lazily created.
		/// </remarks>
		public static PNotificationCenter DefaultCenter {
			get {
				if (DEFAULT_CENTER == null) {
					DEFAULT_CENTER = new PNotificationCenter();
				}
				return DEFAULT_CENTER;
			}
		}
		#endregion

		#region Add Listener Methods
		//****************************************************************
		// Add Listener - Methods for registering listeners with this
		// notification center.
		//****************************************************************

		/// <summary>
		/// Registers the listener to receive notifications with the specified
		/// notification name and/or containing the given object.
		/// </summary>
		/// <param name="listener">The listener to register.</param>
		/// <param name="callbackMethodName">
		/// The name of the method to invoke on the listener.
		/// </param>
		/// <param name="notificationName">
		/// The name of notifications the listener would like to receive.
		/// </param>
		/// <param name="obj">
		/// The object for which the listener would like to recieve associated notifications.
		/// </param>
		/// <remarks>
		/// When a matching notification is posted, the 'callBackMethodName' message will be
		/// sent to the listener with a single PNotification argument.  If the notification
		/// name is null, the listener will receive all notifications with an object matching
		/// the given object. If the object is null, the listener will receive all
		/// notifications with the notification name.
		/// </remarks>
		public virtual void AddListener(Object listener, String callbackMethodName, String notificationName, Object obj) {
			ProcessDeadKeys();

			Object name = notificationName;
			MethodInfo methodInfo = null;

			try {
				methodInfo = listener.GetType().GetMethod(callbackMethodName, new Type[] { typeof(PNotification) });
			} catch (ArgumentNullException e) {
				System.Console.WriteLine(e.StackTrace);
				return;
			}
		
			if (name == null) name = NULL_MARKER;
			if (obj == null) obj = NULL_MARKER;

			Object key = new CompoundKey(name, obj);
			Object val = new CompoundValue(listener, methodInfo);

			IList list = (IList) listenersMap[key];
			if (list == null) {
				list = new ArrayList();
				listenersMap.Add(new CompoundKey(name, obj), list);
			}

			if (!list.Contains(val)) {
				list.Add(val);
			}
		}
		#endregion

		#region Remove Listener Methods
		//****************************************************************
		// Remove Listener - Methods for unregistering listeners from
		// this notification center.
		//****************************************************************

		/// <summary>
		/// Unregisters the listener from recieving notfications from this notfication
		/// center.
		/// </summary>
		/// <param name="listener">The listener to unregister.</param>
		public virtual void RemoveListener(Object listener) {
			ProcessDeadKeys();

			IEnumerator i = (new ArrayList(listenersMap.Keys)).GetEnumerator();
			while (i.MoveNext()) {
				RemoveListener(listener, i.Current);
			}
		}

		/// <summary>
		/// Unregisters the listener from recieving notifications matching notificationName
		/// and object.
		/// </summary>
		/// <param name="listener">The listener to unregister, or <c>null</c>.</param>
		/// <param name="notificationName">
		/// The name of notifications the listener would like to be unregistered from recieving.
		/// </param>
		/// <param name="obj">
		/// The object for which the listener would like to be unregistered from recieving
		/// associated notifications.
		/// </param>
		/// <remarks>
		/// If the listener is null, all listeners matching notificationName and object
		/// are unregistered.  If notificationName is null, the listener will be unregistered
		/// from all notifications containing the object.  If the object is null, the listener
		/// will be unregistered from all notifications matching notficationName.
		/// </remarks>
		public virtual void RemoveListener(Object listener, String notificationName, Object obj) {
			ProcessDeadKeys();

			IList keys = MatchingKeys(notificationName, obj);
			IEnumerator it = keys.GetEnumerator();
			while (it.MoveNext()) {
				RemoveListener(listener, it.Current);
			}
		}
		#endregion

		#region Post PNotification Methods
		//****************************************************************
		// Post PNotification - Methods for posting notifications to
		// registered listeners.
		//****************************************************************

		/// <summary>
		/// Creates a notification with the given name and object, and posts
		/// it to this notification center.
		/// </summary>
		/// <param name="notificationName">The name of the notification to post.</param>
		/// <param name="obj">The object associated with the notification.</param>
		/// <remarks>
		/// The object is typically the object posting the notification.  It may also be
		/// null.
		/// </remarks>
		public virtual void PostNotification(String notificationName, Object obj) {
			PostNotification(notificationName, obj, null);
		}

		/// <summary>
		/// Creates a notification with the given name, object and properties, and posts
		/// it to this notification center.
		/// </summary>
		/// <param name="notificationName">The name of the notification to post.</param>
		/// <param name="obj">The object to associate with the notification.</param>
		/// <param name="properties">The properties to associate with the notification.</param>
		/// <remarks>
		/// The object is typically the object posting the notification.  It may also be
		/// <c>null</c>.
		/// </remarks>
		public virtual void PostNotification(String notificationName, Object obj, IDictionary properties) {
			PostNotification(new PNotification(notificationName, obj, properties));
		}

		/// <summary>
		/// Post the notification to this notification center.
		/// </summary>
		/// <remarks>
		/// Instead of calling this method directly, it is usually easier to use one of the
		/// convenience <c>PostNotifcation</c> methods, which create the notification
		/// internally.
		/// </remarks>
		/// <param name="aNotification">The notification to post.</param>
		public virtual void PostNotification(PNotification aNotification) {
			ArrayList mergedListeners = new ArrayList();
			IList listenersList;
		
			Object name = aNotification.Name;
			Object obj = aNotification.Object;

			if (name != null) {
				if (obj != null) { // both are specified
					listenersList = (IList) listenersMap[new CompoundKey(name, obj)];
					if (listenersList != null) {
						mergedListeners.AddRange(listenersList);
					}
					listenersList = (IList) listenersMap[new CompoundKey(name, NULL_MARKER)];
					if (listenersList != null) {
						mergedListeners.AddRange(listenersList);
					}
					listenersList = (IList) listenersMap[new CompoundKey(NULL_MARKER, obj)];
					if (listenersList != null) {
						mergedListeners.AddRange(listenersList);
					}
				} else { // object is null
					listenersList = (IList) listenersMap[new CompoundKey(name, NULL_MARKER)];
					if (listenersList != null) {
						mergedListeners.AddRange(listenersList);
					}
				}
			} else if (obj != null) { // name is null
				listenersList = (IList) listenersMap[new CompoundKey(NULL_MARKER, obj)];
				if (listenersList != null) {
					mergedListeners.AddRange(listenersList);
				}
			}

			Object key = new CompoundKey(NULL_MARKER, NULL_MARKER);
			listenersList = (IList) listenersMap[key];
			if (listenersList != null) {
				mergedListeners.AddRange(listenersList);
			}

			CompoundValue val;
			TEMP_LIST.Clear();
			TEMP_LIST.AddRange(mergedListeners);
			IEnumerator i = TEMP_LIST.GetEnumerator();

			while (i.MoveNext()) {
				val = (CompoundValue) i.Current;
				if (val.Target == null) {
					mergedListeners.Remove(val);
				} else {
					try {
						val.MethodInfo.Invoke(val.Target, new Object[] { aNotification });
					} catch (MethodAccessException e) {
						System.Console.WriteLine(e.StackTrace);
					} catch (TargetInvocationException e) {
						System.Console.WriteLine(e.StackTrace);
					}
				}
			}
		}
		#endregion

		#region Implementation Classes and Methods
		//****************************************************************
		// Implementation - Classes and methods used internally by the
		// notification center.
		//****************************************************************

		/// <summary>
		/// Gets all keys that match the name/object pair.
		/// </summary>
		/// <remarks>
		/// Either or both the name and the object can be null.
		/// <para>
		/// If both the name and the object are specified, only keys that match both of these
		/// values will be returned.  If the name is <c>null</c>, all keys that match the
		/// object will be returned.  If the object is null, all keys that match the name will
		/// be returned.  And, If both the name and the object are <c>null</c>, all of the keys
		/// in the listeners map will be returned.
		/// </para>
		/// </remarks>
		/// <param name="name">The name for which to find matching keys.</param>
		/// <param name="obj">The object for which to find matching keys.</param>
		/// <returns>All keys that match the name/object pair.</returns>
		protected virtual IList MatchingKeys(String name, Object obj) {
			IList result = new ArrayList();

			IEnumerator it = listenersMap.Keys.GetEnumerator();
			while (it.MoveNext()) {
				CompoundKey key = (CompoundKey) it.Current;
				if ((name == null) || (name.Equals(key.Name))) {
					if ((obj == null) || (obj == key.Target)) {
						result.Add(key);
					}
				}
			}

			return result;
		}

		/// <summary>
		/// Removes the listener from the list mapped to the given key in the listeners
		/// map.
		/// </summary>
		/// <param name="listener">The listener to remove, or <c>null</c>.</param>
		/// <param name="key">
		/// The key that maps the list from which the listener will be removed.
		/// </param>
		/// <remarks>
		/// If listener is null, the entire list of listeners mapped to the given key will be
		/// removed.
		/// </remarks>
		protected virtual void RemoveListener(Object listener, Object key) {
			if (listener == null) {
				listenersMap.Remove(key);
				return;
			}

			IList list = (IList) listenersMap[key];
			if (list == null) {
				return;
			}

			IEnumerator it = (new ArrayList(list)).GetEnumerator();
			while (it.MoveNext()) {
				CompoundValue val = (CompoundValue)it.Current;
				Object observer = val.Target;
				if ((observer == null) || (listener == observer)) {
					list.Remove(val);
				}
			}
		
			if (list.Count == 0) {
				listenersMap.Remove(key);
			}
		}

		/// <summary>
		/// Removes listeners that are mapped to keys which have been garbage collected.
		/// </summary>
		protected virtual void ProcessDeadKeys() {
			IEnumerator i = listenersMap.Keys.GetEnumerator();
			while (i.MoveNext()) {
				CompoundKey key = (CompoundKey)i.Current;
				if (!key.IsAlive) {
					listenersMap.Remove(key);
				}
			}
		}

		/// <summary>
		/// <b>CompoundKey</b> stores a name/object pair used to map listener lists in the
		/// listeners map.
		/// </summary>
		/// <remarks>
		/// This class extends WeakReference to avoid the garbage collection problems
		/// associated with standard event listeners.
		/// </remarks>
		protected class CompoundKey : WeakReference {
			private Object name;
			private int hashCode;

			/// <summary>
			/// Constructs a new CompoundKey, creating a weak reference to the object.
			/// </summary>
			/// <param name="aName">The name attribute of this key.</param>
			/// <param name="anObject">The object attribute of this key.</param>
			public CompoundKey(Object aName, Object anObject)
				: base(anObject) {
				name = aName;
				hashCode = aName.GetHashCode() + anObject.GetHashCode();
			}

			/// <summary>
			/// Gets the name attribute of this key.
			/// </summary>
			/// <value>The name attribute of this key.</value>
			public Object Name {
				get { return name; }
			}

			/// <summary>
			/// Overridden.  See <see cref="Object.GetHashCode">Object.GetHashCode</see>.
			/// </summary>
			public override int GetHashCode() {
				return hashCode;
			}

			/// <summary>
			/// Overridden.  See <see cref="Object.Equals(object)">Object.Equals</see>.
			/// </summary>
			public override bool Equals(object obj) {
				if (this == obj) return true;
				CompoundKey key = (CompoundKey) obj;
				if (name == key.name || (name != null && name.Equals(key.name))) {
					Object thisObj = Target;
					if (thisObj != null) {
						if ( thisObj == (key.Target)) {
							return true;
						}
					}
				}
				return false;
			}
		}

		/// <summary>
		/// <b>CompoundValue</b> stores an object (the listener) and a callback to invoke
		/// on that object.
		/// </summary>
		/// <remarks>
		/// This class extends WeakReference to avoid the garbage collection problems
		/// associated with standard event listeners.
		/// </remarks>
		public class CompoundValue : WeakReference {
			/// <summary>
			/// A hash value, suitable for use in hashing algorithms and data structures like
			/// a hash table.
			/// </summary>
			protected int hashCode;

			/// <summary>
			/// The callback attribute of this compound value.
			/// </summary>
			protected MethodInfo methodInfo;

			/// <summary>
			/// Constructs a new CompoundValue, creating a weak reference to the object.
			/// </summary>
			/// <param name="obj">The object attribute of this compound value.</param>
			/// <param name="methodInfo">The callback attribute of this compound value.</param>
			public CompoundValue(Object obj, MethodInfo methodInfo)
				: base(obj) {
				hashCode = obj.GetHashCode();
				this.methodInfo = methodInfo;
			}

			/// <summary>
			/// Gets the callback attribute of this compound value.
			/// </summary>
			/// <value>The callback attribute of this compound value.</value>
			public MethodInfo MethodInfo {
				get { return methodInfo; }
			}

			/// <summary>
			/// Overridden.  See <see cref="Object.GetHashCode">Object.GetHashCode</see>.
			/// </summary>
			public override int GetHashCode() {
				return hashCode;
			}

			/// <summary>
			/// Overridden.  See <see cref="Object.Equals(object)">Object.Equals</see>.
			/// </summary>
			public override bool Equals(object obj) {
				if (this == obj) return true;
				CompoundValue val = (CompoundValue) obj;
				if (methodInfo == val.methodInfo || (methodInfo != null && methodInfo.Equals(val.methodInfo))) {
					Object o = Target;
					if (o != null) {
						if (o == val.Target) {
							return true;
						}
					}
				}
				return false;
			}
		}
		#endregion
	}
}
