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
using System.Drawing;
using System.Windows.Forms;

namespace UMD.HCIL.Piccolo.Util {
	/// <summary>
	/// <b>PDebug</b> is used to set framework wide debugging flags.
	/// </summary>
	public class PDebug {
 		#region Fields
		private static bool debugRegionManagement = false;	
		private static bool debugPaintCalls = false;	
		private static bool debugPrintFrameRate = false;	
		private static bool debugPrintUsedMemory = false; 
		private static bool debugBounds = false;
		private static bool debugFullBounds = false;
		private static bool debugThreading = false;
		private static int printResultsFrameRate = 10;
	
		private static int debugPaintColor;
		private static long framesProcessed;
		private static long startProcessingOutputTime;
		private static long startProcessingInputTime;
		private static long processOutputTime;
		private static long processInputTime;
		private static bool processingOutput;
		#endregion

		#region Debug Flags
		/// <summary>
		/// Gets or sets a flag that indicates whether the current clip should be specially painted.
		/// </summary>
		/// <value>A flag that indicates whether the current clip should be specially painted.</value>
		public static bool DebugRegionManagement {
			get { return debugRegionManagement; }
			set { debugRegionManagement = value; }
		}

		/// <summary>
		/// Gets or sets a flag that indicates when the paint method causes an invalidate (which will
		/// create a soft infinite loop).
		/// </summary>
		/// <value>
		/// A flag that indicates when the paint method causes an invalidate (which will create
		/// a soft infinite loop).
		/// </value>
		public static bool DebugPaintCalls {
			get { return debugPaintCalls; }
			set { debugPaintCalls = value; }			
		}

		/// <summary>
		/// Gets or sets a flag that indicates whether the frame rate should be printed to the
		/// console.
		/// </summary>
		/// <value>
		/// a flag that indicates whether the frame rate should be printed to the console.
		/// </value>
		public static bool DebugPrintFrameRate {
			get { return debugPrintFrameRate; }
			set { debugPrintFrameRate = value; }
		}	

		/// <summary>
		/// Gets or sets a flag that indicates whether total memory usage should be printed to the
		/// console.
		/// </summary>
		/// <value>
		/// A flag that indicates whether total memory usage should be printed to the console.
		/// </value>
		public static bool DebugPrintUsedMemory {
			get { return debugPrintUsedMemory; }
			set { debugPrintUsedMemory = value; }
		}

		/// <summary>
		/// Gets or sets a flag that indicates whether the bounds of nodes should be specially
		/// painted.
		/// </summary>
		/// <value>
		/// A flag that indicates whether the bounds of nodes should be specially painted.
		/// </value>
		public static bool DebugBounds {
			get { return debugBounds; }
			set { debugBounds = value; }
		}

		/// <summary>
		/// Gets or sets a flag that indicates whether the full bounds of nodes should be specially
		/// painted.
		/// </summary>
		/// <value>
		/// A flag that indicates whether the full bounds of nodes should be specially painted.
		/// </value>
		public static bool DebugFullBounds {
			get { return debugFullBounds; }
			set { debugFullBounds = value; }
		}

		/// <summary>
		/// Gets or sets a flag that specifies the interval at which to print debug information to
		/// the console.
		/// </summary>
		/// <value>
		/// A flag that specifies the interval at which to print debug information to the console.
		/// </value>
		public static int PrintResultsFrameRate {
			get { return printResultsFrameRate; }
			set { printResultsFrameRate = value; }
		}

		/// <summary>
		/// Gets or sets a flag that checks for whether the scene graph is modified outside the event thread.
		/// Piccolo is not thread safe, and so no part of the scene graph can be modified outside the event thread.
		/// It takes time to check, so only turn this flag on for debugging.  If the scene graph is modified
		/// outside the event thread, a message will be printed out.
		/// </summary>
		/// <value>
		/// A flag that indicates whether the scene graph is checked to make sure it is modified only within the
		/// event thread.
		/// </value>
		public static bool DebugThreading {
			get { return debugThreading; }
			set { debugThreading = value; }
		}
		#endregion

		#region Processing Input/Output
		/// <summary>
		/// This method is called when the scene graph needs an update.
		/// </summary>
		public static void ScheduleProcessInputs() {
			// MessageLoop exists only if this is the event dispatch thread.
			if (debugThreading && !Application.MessageLoop) {
				System.Console.WriteLine("Scene graph manipulated on wrong thread.");
			}
		}

		/// <summary>
		/// This method is called just before the piccolo scene is invalidated.
		/// </summary>
		public static void ProcessInvalidate() {
			if (processingOutput && debugPaintCalls) {
				System.Console.Error.WriteLine("Got repaint while painting scene. This can result in a recursive process that degrades performance.");
			}

			// MessageLoop exists only if this is the event dispatch thread.
			if (debugThreading && !Application.MessageLoop) {
				System.Console.Error.WriteLine("Invalidate called on wrong thread.");
			}
		}
	
		/// <summary>
		/// Gets or sets a flag that indicates whether Piccolo is in the process of painting.
		/// </summary>
		/// <value>True if Piccolo is currently painting; otherwise, false.</value>
		public static bool ProcessingOutput {
			get {
				return processingOutput;
			}
		}

		/// <summary>
		/// This method is called just before the Piccolo scene graph is painted.
		/// </summary>
		public static void StartProcessingOutput() {
			processingOutput = true;
			startProcessingOutputTime = PUtil.CurrentTimeMillis;
		}

		/// <summary>
		/// This method is called just after the Piccolo scene graph is painted.
		/// </summary>
		/// <param name="paintContext">The paint context used to paint the scene graph.</param>
		public static void EndProcessingOutput(PPaintContext paintContext) {
			processOutputTime += (PUtil.CurrentTimeMillis - startProcessingOutputTime);
			framesProcessed++;
			
			if (PDebug.debugPrintFrameRate) {
				if (framesProcessed % printResultsFrameRate == 0) {
					System.Console.WriteLine("Process output frame rate: " + OutputFPS + " fps");
					System.Console.WriteLine("Process input frame rate: " + InputFPS + " fps");
					System.Console.WriteLine("Total frame rate: " + TotalFPS + " fps");
					System.Console.WriteLine();
					ResetFPSTiming();				
				}
			}
		
			if (PDebug.debugPrintUsedMemory) {
				if (framesProcessed % printResultsFrameRate == 0) { 		
					System.Console.WriteLine("Approximate used memory: " + ApproximateUsedMemory / 1024 + " k");
				}
			}
		
			if (PDebug.debugRegionManagement) {
				paintContext.PaintClipRegion(new SolidBrush(DebugPaintColor));
			}

			processingOutput = false;
		}

		/// <summary>
		/// This method is called just before input events are processed.
		/// </summary>
		public static void StartProcessingInput() {
			startProcessingInputTime = PUtil.CurrentTimeMillis;
		}
	
		/// <summary>
		/// This method is called just after input events are processed.
		/// </summary>
		public static void EndProcessingInput() {
			processInputTime += (PUtil.CurrentTimeMillis - startProcessingInputTime);
		}
		#endregion

		#region Basic
		/// <summary>
		/// Gets the color used for painting debugging information.
		/// </summary>
		public static Color DebugPaintColor {
			get {
				int color = 100 + (debugPaintColor++ % 10) * 10;
				return Color.FromArgb(150, color, color, color);
			}
		}

		/// <summary>
		/// Gets the number of frames currently being processed and painted per second.
		/// </summary>
		/// <value>The number of frames processed and painted per second.</value>
		/// <remarks>
		/// Note that since piccolo doesn’t paint continuously this rate will be slow
		/// unless you are interacting with the system or have activities scheduled.
		/// </remarks>
		public static float TotalFPS {
			get {
				if ((framesProcessed > 0)) {
					return 1000.0f / ((processInputTime + processOutputTime) / (float) framesProcessed);
				} else {
					return 0;
				}
			}
		}

		/// <summary>
		/// Gets the number of frames per second used to process input events and
		/// activities.
		/// </summary>
		/// <value>
		/// The number of frames per second used to process input events and activities.
		/// </value>
		public static float InputFPS {
			get {
				if ((processInputTime > 0) && (framesProcessed > 0)) {
					return 1000.0f / (processInputTime / (float) framesProcessed);
				} else {
					return 0;
				}
			}
		}

		/// <summary>
		/// Gets the number of frames per second used to paint graphics to the screen.
		/// </summary>
		/// <value>
		/// The number of frames per second used to paint graphics to the screen.
		/// </value>
		public static float OutputFPS {
			get {
				if ((processOutputTime > 0) && (framesProcessed > 0)) {
					return 1000.0f / (processOutputTime / (float) framesProcessed);
				} else {
					return 0;
				}
			}
		}

		/// <summary>
		/// Gets the number of frames that have been processed since the last time
		/// ResetFPSTiming was called.
		/// </summary>
		public static long FramesProcessed {
			get { return framesProcessed; }
		}

		/// <summary>
		/// Reset the variables used to track FPS.
		/// </summary>
		/// <remarks>
		/// If you reset seldom they you will get good average FPS values, if you reset more
		/// often only the frames recorded after the last reset will be taken into consideration.
		/// </remarks>
		public static void ResetFPSTiming() {
			framesProcessed = 0;
			processInputTime = 0;
			processOutputTime = 0;
		}

		/// <summary>
		/// Gets the number of bytes currently thought to be allocated.
		/// </summary>
		/// <value>The number of bytes currently thought to be allocated.</value>
		public static long ApproximateUsedMemory {
			get {
				GC.Collect();
				GC.WaitForPendingFinalizers();
				return System.GC.GetTotalMemory(true);
			}
		}
		#endregion
	}
}