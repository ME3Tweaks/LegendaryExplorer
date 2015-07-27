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
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PStream</b> wraps a <see cref="Stream">System.IO.Stream</see> to handle optional
	/// elements.
	/// </summary>
	/// <remarks>
	/// This is similar to the concept of "weak references", but applied to object
	/// serialization rather than garbage collection.  Here, PStream provides a method,
	/// <see cref="WriteConditionalObject"/>, which only serializes the specified object if
	/// there is a strong reference (if it has already been written) to that object elsewhere
	/// in the stream.
	/// <para>
	/// To discover strong references to objects, PStream uses a two-phase writing process.
	/// First, a "discovery" phase is used to find out what objects are about to be serialized.
	/// This works by effectively serializing the object graph to /dev/null, recording which
	/// objects are unconditionally written using the standard <c>GetObjectData</c> method.  Then,
	/// in the second "write" phase, PStream actually serializes the data to the stream.  During
	/// this phase, calls to <see cref="WriteConditionalObject"/> will only write the specified
	/// object if the object was found to be serialized during the discovery stage.  If the
	/// object was not recorded during the discovery stage, a null value is unconditionally
	/// written in place of the object.
	/// </para>
	/// </remarks>
	public class PStream {
		#region Fields
		private Stream stream;
		private static Hashtable unconditionallyWritten;
		private static bool writingRoot;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PStream.
		/// </summary>
		/// <param name="aStream">The stream to to write to.</param>
		public PStream(Stream aStream) {
			unconditionallyWritten = new Hashtable();
			stream = aStream;
		}
		#endregion

		#region Reading/Writing
		/// <summary>
		/// Serializes the object tree to the underlying stream using the given formatter.
		/// </summary>
		/// <param name="formatter">The formatter to use when serializing the object.</param>
		/// <param name="aRoot">The object to serialize.</param>
		/// <remarks>
		/// Applications should call this method to serialize a part of the scene graph.  The nodes
		/// in the scene graph may then choose to conditionally serialize their references by calling
		/// WriteConditionalObject from their <c>GetObjectData</c> method.  For example, all nodes
		/// implemented in the piccolo framework conditionally serialize their parents so that
		/// serializing a node won't pull in the entire object graph.  See
		/// <see cref="PNode.GetObjectData">PNode.GetObjectData</see>.
		/// </remarks>
		public virtual void WriteObjectTree(IFormatter formatter, Object aRoot) {
			unconditionallyWritten.Clear();
			writingRoot = true;
			RecordUnconditionallyWritten(aRoot, formatter.SurrogateSelector); //record pass
			formatter.Serialize(stream, aRoot);  //write pass
			writingRoot = false;
		}

		/// <summary>
		/// Deserializes the object tree from the underlying stream using the given formatter.
		/// </summary>
		/// <param name="formatter">The formatter to use when deserializing the object.</param>
		/// <returns>The deserialized object.</returns>
		public virtual Object ReadObjectTree(IFormatter formatter){
			stream.Seek(0, SeekOrigin.Begin);
			return formatter.Deserialize(stream);
		}

		/// <summary>
		/// Conditionally adds the given object to the given <see cref="SerializationInfo">
		/// System.Runtime.Serialization.SerializationInfo</see>, using the specified name.
		/// </summary>
		/// <param name="info">The <see cref="SerializationInfo"/> to add the object to.</param>
		/// <param name="name">
		/// The name to use when adding the object to the <see cref="SerializationInfo"/>.
		/// </param>
		/// <param name="obj">The object to add to the <see cref="SerializationInfo"/>.</param>
		/// <remarks>
		/// This method only serializes the specified object if there is a strong reference (if
		/// it has already been written) to that object elsewhere in the stream.
		/// </remarks>
		public static void WriteConditionalObject(SerializationInfo info, String name, Object obj) {
			if (!writingRoot) {
				throw new InvalidOperationException("WriteConditionalObject() may only be called when a root object has been written.");
			}

			if (obj != null && unconditionallyWritten.Contains(obj)) {
				info.AddValue(name, obj, obj.GetType());
			}
			else {
				info.AddValue(name, null);
			}
		}
		#endregion

		#region Discovery Phase
		/// <summary>
		/// Performs a discovery phase to find out which objects are about to be serialized.
		/// </summary>
		/// <param name="aRoot">The object to perform the discover phase on.</param>
		/// <param name="surrogateSelector">
		/// The surrogate selector to check when serializing the object graph.
		/// </param>
		/// <remarks>
		/// This works by effectively serializing the object graph to /dev/null, recording which
		/// objects are unconditionally written using the standard
		/// <c>GetObjectData</c> method. 
		/// </remarks>
		protected virtual void RecordUnconditionallyWritten(Object aRoot, ISurrogateSelector surrogateSelector) {
			BinaryFormatter bFormatter = new BinaryFormatter();
			Stream stream = PUtil.NULL_OUTPUT_STREAM;
			bFormatter.SurrogateSelector = new RecordWrittenSurrogateSelector(surrogateSelector); //rss;
			bFormatter.Serialize(stream, aRoot);
		}

		/// <summary>
		/// <b>RecordWrittenSurrogateSelector</b> is a surrogate selector that will always return
		/// a <see cref="RecordWrittenSurrogate"/>, regardless of the type specified.
		/// </summary>
		/// <remarks>
		/// This class is used in the discovery phase.  The binary formatter is given this surrogate
		/// selector so that each time an object is serialized, the
		/// <see cref="RecordWrittenSurrogate.GetObjectData"/> method will be called.  This makes it
		/// possible to avoid registerring every type in the namespace with a standard
		/// <see cref="SurrogateSelector">System.Runtime.Serialization.SurrogateSelector</see>.
		/// </remarks>
		protected class RecordWrittenSurrogateSelector : SurrogateSelector {
			private ISurrogateSelector actualSurrogateSelector;

			/// <summary>
			/// Constructs a new RecordWrittenSurrogateSelector.
			/// </summary>
			/// <param name="actualSurrogateSelector">
			/// The actual surrogate selector used to get the actual surrogate for the specified
			/// type.
			/// </param>
			public RecordWrittenSurrogateSelector(ISurrogateSelector actualSurrogateSelector) {
				this.actualSurrogateSelector = actualSurrogateSelector;
			}

			/// <summary>
			/// Returns a <see cref="RecordWrittenSurrogate"/>, that wraps the surrogate for a
			/// particular type.
			/// </summary>
			/// <param name="type">The <see cref="Type"/> for which the surrogate is requested.</param>
			/// <param name="context">The streaming context.</param>
			/// <param name="selector">The surrogate to use.</param>
			/// <returns>
			/// A <see cref="RecordWrittenSurrogate"/> that wraps the surrogate for the given type.
			/// </returns>
			public override ISerializationSurrogate GetSurrogate(Type type, StreamingContext context, out ISurrogateSelector selector) {
				ISerializationSurrogate actualSurrogate = actualSurrogateSelector.GetSurrogate (type, context, out selector);
				return new RecordWrittenSurrogate(actualSurrogate);
			}
		}

		/// <summary>
		/// <b>RecordWrittenSurrogate</b> is a serialization surrogate that is used during the
		/// discovery phase to record which objects are unconditionally written.
		/// </summary>
		/// <remarks>
		/// This surrogate will always be returned by the <see cref="RecordWrittenSurrogateSelector"/>
		/// regardless of which type is specified.
		/// </remarks>
		protected class RecordWrittenSurrogate : ISerializationSurrogate {
			ISerializationSurrogate actualSurrogate; 

			/// <summary>
			/// Constructs a new RecordWrittenSurrogate.
			/// </summary>
			/// <param name="actualSurrogate">
			/// The actual surrogate designated to handle serialization for the associated type, or
			/// <c>null</c>.
			/// </param>
			public RecordWrittenSurrogate(ISerializationSurrogate actualSurrogate) {
				this.actualSurrogate = actualSurrogate;
			}

			/// <summary>
			/// Populates the provided <see cref="SerializationInfo"/> with the data needed to
			/// serialize the object and records that the object was unconditionally written.
			/// </summary>
			/// <param name="obj">The object to serialize.</param>
			/// <param name="info">The <see cref="SerializationInfo"/> to populate with data.</param>
			/// <param name="context">
			/// The destination (see <see cref="StreamingContext"/>) for this serialization.
			/// </param>
			public void GetObjectData(Object obj, SerializationInfo info, StreamingContext context) {
				if (!unconditionallyWritten.Contains(obj)) {
					// Record that the object was unconditionally written.
					if (!obj.GetType().IsValueType) {
						unconditionallyWritten.Add(obj, true);
					}

					// If there is an actual surrogate for this type of object, use the surrogate
					// to populate the SerializationInfo.  Otherwise, if the object implements
					// ISerializable, use the object's GetObjectData method to populate the
					// SerializationInfo.  Otherwise, get the serializable members of the object
					// and write them directly to the SerializationInfo.

					if (actualSurrogate != null) {
						actualSurrogate.GetObjectData(obj, info, context);
					} else if (obj is ISerializable) {
						((ISerializable)obj).GetObjectData(info, context);
					} else {
						// Serialize serializable members
						Type objType = obj.GetType();
						MemberInfo[] mi = FormatterServices.GetSerializableMembers(objType, context);

						for(int i = 0; i < mi.Length; i++) {
							info.AddValue(mi[i].Name, ((FieldInfo)mi[i]).GetValue(obj));
						}
					}
					
				}
			}

			/// <summary>
			/// Returns <c>null</c> since this surrogate is never be used for deserialization.
			/// </summary>
			/// <param name="obj">The object to populate.</param>
			/// <param name="info">The information to populate the object.</param>
			/// <param name="context">The source from which the object is deserialized.</param>
			/// <param name="selector">
			/// The surrogate selector where the search for a compatible surrogate begins.
			/// </param>
			/// <returns></returns>
			public Object SetObjectData(Object obj, SerializationInfo info, StreamingContext context, ISurrogateSelector selector) {
				return null;
			}
		}
		#endregion
	}
}
