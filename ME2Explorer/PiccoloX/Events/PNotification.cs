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
 */

using System;
using System.Collections;

namespace UMD.HCIL.PiccoloX.Events {
	/// <summary>
	/// <b>PNotification</b> objects encapsulate information so that it can be
	/// broadcast to other objects by a <see cref="PNotificationCenter"/>.
	/// </summary>
	/// <remarks>
	/// A PNotification contains a name, an object, and an optional properties map.
	/// The name is a tag identifying the notification.  The object is any object
	/// that the poster of the notification wants to send to observers of that
	/// notification (typically, it is the object that posted the notification). The
	/// properties map stores other related objects, if any.
	/// <para>
	/// You don’t usually create your own notifications directly. The
	/// <see cref="PNotificationCenter.PostNotification(String, Object)">
	/// PNotificationCenter.PostNotification</see> method allows you to conveniently
	/// post a notification without creating it first.
	/// </para>
	/// </remarks>
	public class PNotification {
		#region Fields
		/// <summary>
		/// The name of this notification.
		/// </summary>
		protected String name;

		/// <summary>
		/// The object associated with this notification.
		/// </summary>
		protected Object source;

		/// <summary>
		/// Properties associated with this notification.
		/// </summary>
		protected IDictionary properties;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PNotification.
		/// </summary>
		/// <param name="name">The name of this notification.</param>
		/// <param name="source">The object associated with this notification.</param>
		/// <param name="properties">Properties associated with this notification.</param>
		public PNotification(String name, Object source, IDictionary properties) {
			this.name = name;
			this.source = source;
			this.properties = properties;
		}
		#endregion

		#region Notification Data
		/// <summary>
		/// Gets the name of this notification.
		/// </summary>
		/// <value>The name of this notification.</value>
		/// <remarks>
		/// The value returned is the same name used to register with the notification center.
		/// </remarks>
		public virtual String Name {
			get { return name; }
		}

		/// <summary>
		/// Gets the object associated with this notification.
		/// </summary>
		/// <value>The object associated with this notification.</value>
		/// <remarks>
		/// The value returned is most often the same object that posted the notfication.  It
		/// may be null.
		/// </remarks>
		public virtual Object Object {
			get { return source; }
		}

		/// <summary>
		/// Gets a property associated with this notfication.
		/// </summary>
		/// <param name="key">The key that identifies the desired property.</param>
		/// <returns>A property associated with this notification.</returns>
		public virtual Object GetProperty(Object key) {
			if (properties != null) {
				return properties[key];
			}
			return null;
		}
		#endregion
	}
}