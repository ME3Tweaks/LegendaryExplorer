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

using UMD.HCIL.Piccolo;
using UMD.HCIL.Piccolo.Activities;
using UMD.HCIL.Piccolo.Util;

namespace UMD.HCIL.PiccoloX.Components {

	#region Enums
	/// <summary>
	/// This enumeration is used by the <see cref="PScrollableControl"/> class.  It represents
	/// the various policies that guide when a scrollbar should be displayed.
	/// </summary>
	public enum ScrollBarPolicy {
		/// <summary>
		/// The scrollbar should always be displayed.
		/// </summary>
		Always,

		/// <summary>
		/// The scrollbar should never be displayed.
		/// </summary>
		Never,

		/// <summary>
		/// The scrollbar should only be displayed when needed.
		/// </summary>
		AsNeeded
	}
	#endregion

	/// <summary>
	/// <b>PScrollableControl</b> is a simple control that correctly handles scrolling a
	/// <see cref="PCanvas"/>.
	/// </summary>
	/// <remarks>
	/// This class does not extend <see cref="System.Windows.Forms.ScrollableControl">
	/// System.Windows.Forms.ScrollableControl</see> since there is no simple mechanism
	/// to override the scrolling behavior in that class.
	/// </remarks>
	public class PScrollableControl : Control {
		#region Fields
		private static Rectangle DEFAULT_CLIENT_BOUNDS = new Rectangle(0, 0, 200, 200);
		private static int DEFAULT_SCROLL_WIDTH = 16;
		private static float DEFAULT_SMALL_CHANGE_FACTOR = .1f; // The value multiplied by the
																// ViewSize to calculate the SmallChange
		private static float DEFAULT_LARGE_CHANGE_FACTOR = .9f; // The value multiplied by the
																// ViewSize to calculate the LargeChange
		private static int DEFAULT_VSMALL_CHANGE = 50;
		private static int DEFAULT_HSMALL_CHANGE = 50;
		private static int DEFAULT_VLARGE_CHANGE = 100;
		private static int DEFAULT_HLARGE_CHANGE = 100;

		private bool startOfScrollSequence = true;

		/// <summary>
		/// The scrollbar that scrolls the canvas in the vertical direction.
		/// </summary>
		protected ScrollBar vScrollBar;

		/// <summary>
		/// The scrollbar that scrolls the canvas in the horizontal direction.
		/// </summary>
		protected ScrollBar hScrollBar;

		/// <summary>
		/// The canvas scrolled by the vertical and horizontal scrollbars.
		/// </summary>
		protected PCanvas view;

		/// <summary>
		/// The director that this scrollable control signals.
		/// </summary>
		protected PScrollDirector scrollDirector;

		/// <summary>
		/// A value that indicates whether scrolling is turned on for this scrollable
		/// control.
		/// </summary>
		protected bool scrollable = true;

		/// <summary>
		/// The policy for the vertical scrollbar.
		/// </summary>
		protected ScrollBarPolicy vsbPolicy = ScrollBarPolicy.AsNeeded;

		/// <summary>
		/// The policy for the horizontal scrollbar.
		/// </summary>
		protected ScrollBarPolicy hsbPolicy = ScrollBarPolicy.AsNeeded;

		/// <summary>
		/// The value multiplied by the viewsize to calculate the SmallChange.
		/// </summary>
		protected float smallChangeFactor = DEFAULT_SMALL_CHANGE_FACTOR;

		/// <summary>
		/// The value multiplied by the viewsize to calculate the LargeChange.
		/// </summary>
		protected float largeChangeFactor = DEFAULT_LARGE_CHANGE_FACTOR;

		/// <summary>
		/// The vertical small change in pixels.
		/// </summary>
		protected int vSmallChange = DEFAULT_VSMALL_CHANGE;

		/// <summary>
		/// The horizontal small change in pixels.
		/// </summary>
		protected int hSmallChange = DEFAULT_HSMALL_CHANGE;

		/// <summary>
		/// The vertical large change in pixels.
		/// </summary>
		protected int vLargeChange = DEFAULT_VLARGE_CHANGE;

		/// <summary>
		/// The horizontal large change in pixels.
		/// </summary>
		protected int hLargeChange = DEFAULT_HLARGE_CHANGE;

		/// <summary>
		/// The value indicating whether or not the large change should be
		/// calculated automatically as the extent changes.
		/// </summary>
		protected bool autoLargeChange = true;

		/// <summary>
		/// The activity used to scroll the view when AnimateScrolls is true.
		/// </summary>
		protected PActivity scroll;

		/// <summary>
		/// A value that indicates whether or not scrolls are animated.
		/// </summary>
		protected bool animateScrolls = true;

		/// <summary>
		/// The duration of a scroll when animateScrolls is true.
		/// </summary>
		protected long animateScrollDuration = 300;

		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PScrollableControl.
		/// </summary>
		public PScrollableControl() {
		}

		/// <summary>
		/// Constructs a new PScrollableControl that scrolls the given canvas.
		/// </summary>
		/// <param name="view"></param>
		public PScrollableControl(PCanvas view) {
			Canvas = view;
		}
		#endregion

		#region Basics
		/// <summary>
		/// Gets or sets the canvas scrolled by this scrollable control.
		/// </summary>
		/// <value>The canvas scrolled by this scrollable control.</value>
		/// <remarks>
		/// When this property is set, the canvas is anchored to the scrollable control, the
		/// appropriate event handlers are set up, and the scroll director is instantiated.
		/// </remarks>
		public PCanvas Canvas {
			get { return view; }
			set {
				this.view = value;

				this.SuspendLayout();
				Bounds = DEFAULT_CLIENT_BOUNDS;
				view.Bounds = ClientRectangle;
				vScrollBar = new VScrollBar();
				vScrollBar.Bounds = new Rectangle(ClientRectangle.Width - DEFAULT_SCROLL_WIDTH, ClientRectangle.Y, DEFAULT_SCROLL_WIDTH, ClientRectangle.Height - DEFAULT_SCROLL_WIDTH);

				hScrollBar = new HScrollBar();
				hScrollBar.Bounds = new Rectangle(ClientRectangle.X, ClientRectangle.Height - DEFAULT_SCROLL_WIDTH, ClientRectangle.Width - DEFAULT_SCROLL_WIDTH, DEFAULT_SCROLL_WIDTH);
			
				this.Controls.Add(view);
				this.Controls.Add(vScrollBar);
				this.Controls.Add(hScrollBar);

				view.Anchor = 
					AnchorStyles.Bottom |
					AnchorStyles.Top |
					AnchorStyles.Left |
					AnchorStyles.Right;

				hScrollBar.Anchor =
					AnchorStyles.Left |
					AnchorStyles.Bottom |
					AnchorStyles.Right;

				vScrollBar.Anchor =
					AnchorStyles.Top |
					AnchorStyles.Bottom |
					AnchorStyles.Right;

				vScrollBar.Scroll += new ScrollEventHandler(scrollBar_Scroll);
				hScrollBar.Scroll += new ScrollEventHandler(scrollBar_Scroll);

				ScrollDirector = CreateScrollDirector();
				this.ResumeLayout(false);
			}
		}

		/// <summary>
		/// Overridden.  This method is overridden to direct the focus to the view.
		/// </summary>
		/// <param name="e">An EventArgs that contains the event data.</param>
		protected override void OnGotFocus(EventArgs e) {
			base.OnGotFocus (e);
			this.view.Focus();
		}

		/// <summary>
		/// Gets a value indicating whether the vertical scrollbar is currently being
		/// displayed.
		/// </summary>
		/// <value>
		/// A value indicating whether the vertical scrollbar is currently being displayed.
		/// </value>
		/// <remarks>
		/// Use the <see cref="VsbPolicy"/> property to control when the vertical scrollbar
		/// is displayed.
		/// </remarks>
		public virtual bool VScroll {
			get {
				return this.vScrollBar.Visible;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the horizontal scrollbar is currently being
		/// displayed.
		/// </summary>
		/// <value>
		/// A value indicating whether the horizontal scrollbar is currently being displayed.
		/// </value>
		/// <remarks>
		/// Use the <see cref="HsbPolicy"/> property to control when the horizontal scrollbar
		/// is displayed.
		/// </remarks>
		public virtual bool HScroll {
			get {
				return this.hScrollBar.Visible;
			}
		}

		/// <summary>
		/// Gets or sets a value that indicates whether scrolling is turned on for this scrollable
		/// control.
		/// </summary>
		public virtual bool Scrollable {
			get {
				return scrollable;
			}
			set {
				scrollable = value;
				PerformLayout();
			}
		}


		/// <summary>
		/// Gets or sets the policy for the vertical scrollbar.
		/// </summary>
		/// <value>The policy for the vertical scrollbar.</value>
		public virtual ScrollBarPolicy VsbPolicy {
			get { return vsbPolicy; }
			set {
				vsbPolicy = value;
			}
		}

		/// <summary>
		/// Gets or sets the policy for the horizontal scrollbar.
		/// </summary>
		/// <value>The policy for the horizontal scrollbar.</value>
		public virtual ScrollBarPolicy HsbPolicy {
			get { return hsbPolicy; }
			set {
				hsbPolicy = value;
			}
		}

		/// <summary>
		/// Gets or sets a value that indicates whether or not scrolls are animated.
		/// </summary>
		/// <value>True if scrolls should be animated; false, otherwise.</value>
		public virtual bool AnimateScrolls {
			get { return animateScrolls; }
			set {
				this.animateScrolls = value;
			}
		}

		/// <summary>
		/// Gets or sets the duration of a scroll when
		/// <see cref="AnimateScrolls">AnimateScrolls</see> is true.
		/// </summary>
		/// <remarks>
		/// Note, if the scroll duration is longer than the interval of the scrollbar's
		/// internal timer, the animation will be cut off when the mouse is held down
		/// over the arrow buttons or the scrollbar's trough.  To avoid this behavior,
		/// use a relatively short animation time.
		/// </remarks>
		/// <value>The duration of a scroll.</value>
		public virtual long AnimateScrollDuration {
			get { return animateScrollDuration; }
			set {
				animateScrollDuration = value;
			}
		}
		#endregion

		#region Scroll Event Handlers
		/// <summary>
		/// Sets the view position to the appropriate value when either scrollbar changes.
		/// </summary>
		/// <param name="sender">The source of the ScrollEvent.</param>
		/// <param name="e">A ScrollEventArgs containing the event data.</param>
		protected virtual void scrollBar_Scroll(object sender, ScrollEventArgs e) {
			Scroll((ScrollBar)sender, e, animateScrolls);
		}

		/// <summary>
		/// Scrolls the view according to the given ScrollEventArgs.
		/// </summary>
		/// <param name="scrollBar">The source of the ScrollEvent.</param>
		/// <param name="e">A ScrollEventArgs containing the event data.</param>
		/// <param name="animateFirst">
		/// A boolean value indicating whether or not to animate the first scroll.
		/// </param>
		protected virtual void Scroll(ScrollBar scrollBar, ScrollEventArgs e, bool animateFirst) {
			// If we are animating, terminate any previous scroll activities.
			if (animateScrolls) {
				TerminatePreviousScroll(scrollBar, e);
			}

			if (e.Type != ScrollEventType.EndScroll) {
				Point viewPosition;
				if (scrollBar is VScrollBar) {
					viewPosition = new Point(ViewPosition.X, e.NewValue);
				} else {
					viewPosition = new Point(e.NewValue, ViewPosition.Y);
				}

				// At the start of a scroll sequence, we will animate the scroll if
				// animateScrolls is set to true.
				if (startOfScrollSequence && animateFirst) {
					if (e.Type != ScrollEventType.ThumbTrack) {
						SetViewPosition(viewPosition, true);
						e.NewValue = scrollBar.Value;
					}
					startOfScrollSequence = false;
				}
				else {
					if (!Canvas.Interacting) Canvas.Interacting = true;
					// Otherwise, just set the position directly.
					SetViewPosition(viewPosition, false);
				}

			} else {
				// Reset some flags at the end of a scroll sequence.
				startOfScrollSequence = true;
				if (Canvas.Interacting) Canvas.Interacting = false;
			}
		}

		/// <summary>
		/// Terminates the previous scroll activity if a new scroll event occurs.
		/// </summary>
		/// <param name="scrollBar">The source of the new Scroll event.</param>
		/// <param name="e">The event data associated with the Scroll event.</param>
		protected virtual void TerminatePreviousScroll(ScrollBar scrollBar, ScrollEventArgs e) {
			if (e.Type != ScrollEventType.EndScroll && scroll != null && scroll.IsStepping) {
				ScrollActivity scrollActivity = (ScrollActivity)scroll;
				scroll.Terminate();

				int newPos;
				if (scrollBar is VScrollBar) {
					newPos = scrollActivity.newPosition.Y;
				} else {
					newPos = scrollActivity.newPosition.X;
				}

				int max = scrollBar.Maximum - scrollBar.LargeChange+1;
				switch (e.Type) {
					case ScrollEventType.SmallDecrement:
						e.NewValue = Math.Max(vScrollBar.Minimum, (newPos - scrollBar.SmallChange));
						break;
					case ScrollEventType.SmallIncrement:
						e.NewValue = Math.Min(max, (newPos + scrollBar.SmallChange));
						break;
					case ScrollEventType.LargeDecrement:
						e.NewValue = Math.Max(vScrollBar.Minimum, (newPos - scrollBar.LargeChange));
						break;
					case ScrollEventType.LargeIncrement:
						e.NewValue = Math.Min(max, (newPos + scrollBar.LargeChange));
						break;
				}
			}
		}

		/// <summary>
		/// Overridden.  See <see cref="Control.OnMouseWheel"/>.
		/// </summary>
		protected override void OnMouseWheel(MouseEventArgs e) {
			base.OnMouseWheel (e);

			if (Scrollable) {
				// Get the new value.
				int val = vScrollBar.Value - e.Delta;
				int max = vScrollBar.Maximum - vScrollBar.LargeChange+1;
				val = Math.Max(val, vScrollBar.Minimum);
				val = Math.Min(val, max);
				Point viewPosition = new Point(hScrollBar.Value, val);

				// Scroll the scrollbar.
				Scroll(vScrollBar, new ScrollEventArgs(ScrollEventType.ThumbPosition, val), false);
				Scroll(vScrollBar, new ScrollEventArgs(ScrollEventType.EndScroll, val), false);
			}
		}
		#endregion

		#region Layout
		/// <summary>
		/// Lays out the scrollbars and the canvas.
		/// </summary>
		/// <param name="levent">A LayoutEventArgs containing the event data.</param>
		protected override void OnLayout(LayoutEventArgs levent) {
			base.OnLayout (levent);

			Rectangle availRect = ClientRectangle;

			bool vsbNeeded = false;
			bool hsbNeeded = false;

			if (scrollable) {
				if (vsbPolicy == ScrollBarPolicy.Always) {
					vsbNeeded = true;
				}
				else if (vsbPolicy == ScrollBarPolicy.AsNeeded) {
					vsbNeeded = GetViewSize(new RectangleF(0, 0, availRect.Width, availRect.Height)).Height > availRect.Height;
				}

				if (vsbNeeded) {
					availRect.Width -= vScrollBar.Width;
				}

				if (hsbPolicy == ScrollBarPolicy.Always) {
					hsbNeeded = true;
				}
				else if (hsbPolicy == ScrollBarPolicy.AsNeeded) {
					hsbNeeded = GetViewSize(new Rectangle(0, 0, availRect.Width, availRect.Height)).Width > availRect.Width;
				}

				if (hsbNeeded) {
					availRect.Height -= hScrollBar.Height;

					if (!vsbNeeded && (vsbPolicy != ScrollBarPolicy.Never)) {

						vsbNeeded = GetViewSize(new RectangleF(0, 0, availRect.Width, availRect.Height)).Height > availRect.Height;

						if (vsbNeeded) {
							availRect.Width -= vScrollBar.Width;
						}
					}
				}
			}

			if (vsbNeeded) {
				vScrollBar.Height = availRect.Height;
				vScrollBar.Visible = true;
			}
			else {
				vScrollBar.Visible = false;
			}

			if (hsbNeeded) {
				hScrollBar.Width = availRect.Width;
				hScrollBar.Visible = true;
			}
			else {
				hScrollBar.Visible = false;
			}

			view.Size = availRect.Size;
		}
		#endregion

		#region Update Scrollbars
		/// <summary>
		/// Updates the values of the scrollbars to the current state of the view.
		/// </summary>
		public virtual void UpdateScrollbars() {
			if (scrollable) {
				// update values
				Size s = ViewSize;
				Point p = ViewPosition;

				vScrollBar.Maximum = s.Height-(Extent.Height-VLargeChangePixels)-1;
				vScrollBar.Value = p.Y;

				hScrollBar.Maximum = s.Width-(Extent.Width-HLargeChangePixels)-1;
				hScrollBar.Value = p.X;
			
				// layout controls
				PerformLayout();

				vScrollBar.SmallChange = VSmallChangePixels;
				vScrollBar.LargeChange = VLargeChangePixels;
				hScrollBar.SmallChange = HSmallChangePixels;
				hScrollBar.LargeChange = HLargeChangePixels;
			}
		}
		#endregion

		#region View Port
		/// <summary>
		/// Subclassers can override this method to install a different scroll director
		/// in the constructor.  Returns a new <see cref="PScrollDirector"/> object.
		/// </summary>
		/// <returns>A new <see cref="PScrollDirector"/> object.</returns>
		protected virtual PScrollDirector CreateScrollDirector() {
			return new PDefaultScrollDirector();
		}

		/// <summary>
		/// Gets or sets the scroll director for this scrollable control.
		/// </summary>
		public virtual PScrollDirector ScrollDirector {
			get {
				return scrollDirector;
			}
			set {
				if (this.scrollDirector != null) {
					this.scrollDirector.UnInstall();
				}
				this.scrollDirector = value;
				if (scrollDirector != null) {
					this.scrollDirector.Install(this, view);
				}
			}
		}

		/// <summary>
		/// Gets the extent size.
		/// </summary>
		/// <value>The extent size.</value>
		public virtual Size Extent {
			get {
				int offset = 0;
				if (vScrollBar.Visible) {
					offset = DEFAULT_SCROLL_WIDTH;
				}
				int width = Math.Max(0, ClientRectangle.Width - offset);

				offset = 0;
				if (hScrollBar.Visible) {
					offset = DEFAULT_SCROLL_WIDTH;
				}
				int height = Math.Max(0, ClientRectangle.Height - offset);

				return new Size(width, height);

			}
		}

		/// <summary>
		/// Gets the view size from the scroll director based on the current extent size.
		/// </summary>
		/// <value>The view size.</value>
		public virtual Size ViewSize {
			get {
				return scrollDirector.GetViewSize(new RectangleF(0, 0, Extent.Width, Extent.Height));
			}
		}

		/// <summary>
		/// Gets the view size from the scroll director based on the specified extent size
		/// </summary>
		/// <param name="bounds">The extent size from which the view size is computed.</param>
		/// <returns>The view size.</returns>
		public virtual Size GetViewSize(RectangleF bounds) {
			return scrollDirector.GetViewSize(bounds);
		}

		/// <summary>
		/// Gets or sets the view position from the scroll director.
		/// </summary>
		/// <value>The view position.</value>
		public virtual Point ViewPosition {
			get {
				return scrollDirector.GetViewPosition(new RectangleF(0, 0, Extent.Width, Extent.Height));
			}
			set {
				SetViewPosition(value, false);
			}
		}

		/// <summary>
		/// Sets the view position of the scrollDirector.
		/// </summary>
		/// <param name="point">The new position.</param>
		/// <param name="animate">Indicates whether or not to animate the transition.</param>
		protected virtual void SetViewPosition(Point point, bool animate) {
			if (view == null) {
				return;
			}

			float oldX = 0, oldY = 0, x = point.X, y = point.Y;

			PointF vp = this.ViewPosition;
			oldX = vp.X;
			oldY = vp.Y;

			// Send the scroll director the exact view position and let it
			// interpret it as needed
			float newX = x;
			float newY = y;

			if ((oldX != newX) || (oldY != newY)) {
				if (animate) {
					scroll = new ScrollActivity(ViewPosition, point, scrollDirector, animateScrollDuration);
					Canvas.Root.AddActivity(scroll);
				} else {
					scrollDirector.SetViewPosition(newX, newY);
				}
			}
		}

		/// <summary>
		/// An activity that animates the view to a new position.
		/// </summary>
		class ScrollActivity : PInterpolatingActivity {
			private int oldX, oldY, newX, newY;
			PScrollDirector scrollDirector;
			public ScrollActivity(Point oldPosition, Point newPosition, PScrollDirector scrollDirector, long duration)
				: base(duration, PUtil.DEFAULT_ACTIVITY_STEP_RATE) {

				this.SlowInSlowOut = false;
				this.scrollDirector = scrollDirector;
				oldX = oldPosition.X;
				oldY = oldPosition.Y;
				newX = newPosition.X;
				newY = newPosition.Y;
			}
			public Point newPosition {
				get { return new Point(newX, newY); }
			}
			public override void SetRelativeTargetValue(float zeroToOne) {
				scrollDirector.SetViewPosition(oldX + zeroToOne * (newX - oldX),
					oldY + zeroToOne * (newY - oldY));
			}
			protected override void OnActivityFinished() {
				base.OnActivityFinished ();
			}
			public override bool IsAnimation {
				get { return true; }
			}
		}

		/// <summary>
		/// Gets or sets the value multiplied by the extent to calculate the LargeChange
		/// when <see cref="AutoLargeChange">AutoLargeChange</see> is true.
		/// </summary>
		/// <value>The value multiplied by the viewsize to calculate the LargeChange.</value>
		public virtual float AutoLargeChangeFactor {
			get {
				return largeChangeFactor;
			}
			set {
				largeChangeFactor = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether or not the large change should be
		/// calculatied automatically as the extent changes.
		/// </summary>
		/// <remarks>
		/// When this property is true, the large change will always be equal to the
		/// <see cref="AutoLargeChangeFactor">AutoLargeChangeFactor</see> *
		/// <see cref="Extent">Extent</see>.Height and any value set via the VLargeChangeXXX
		/// and HLargeChangeXXX properties will be ignored.  When this property is false,
		/// the large change will be equal to the values set via the VLargeChangeXXX and
		/// the HLargeChangeXXX properties.
		/// </remarks>
		/// <value>
		/// A value indicating whether or not the large change should be calculated
		/// automatically as the extent changes.
		/// </value>
		public virtual bool AutoLargeChange {
			get {
				return autoLargeChange;
			}
			set {
				autoLargeChange = value;
				UpdateScrollbars();
			}
		}

		/// <summary>
		/// Gets or sets the vertical large change in pixels.
		/// </summary>
		/// <remarks>
		/// This value is ignored if <see cref="AutoLargeChange">AutoLargeChange</see> is true.
		/// </remarks>
		/// <value>The vertical large change in pixels</value>
		public virtual int VLargeChangePixels {
			get {
				if (autoLargeChange) {
					return (int)Math.Max(1, (Extent.Height * AutoLargeChangeFactor));
				} else {
					return vLargeChange;
				}
			}
			set {
				if (!autoLargeChange) {
					vLargeChange = value;
					UpdateScrollbars();
				}
			}
		}

		/// <summary>
		/// Gets or sets the vertical large change in view coordinates.
		/// </summary>
		/// <remarks>
		/// This value is ignored if <see cref="AutoLargeChange">AutoLargeChange</see> is true.
		/// </remarks>
		/// <value>The vertical large change in view coordinates</value>
		public virtual float VLargeChangeView {
			get {
				SizeF largeChangeSize = new SizeF(VLargeChangePixels, VLargeChangePixels);
				return view.Camera.LocalToView(largeChangeSize).Width;
			}
			set {
				SizeF largeChangeSize = new SizeF(value, value);
				int vLargeChange = (int)Math.Round(view.Camera.ViewToLocal(largeChangeSize).Width);
				VLargeChangePixels = Math.Max(1, vLargeChange);
			}
		}

		/// <summary>
		/// Gets or sets the horizontal large change in pixels.
		/// </summary>
		/// <remarks>
		/// This value is ignored if <see cref="AutoLargeChange">AutoLargeChange</see> is true.
		/// </remarks>
		/// <value>The horizontal large change in pixels</value>
		public virtual int HLargeChangePixels {
			get {
				if (autoLargeChange) {
					return (int)Math.Max(1, (Extent.Width * AutoLargeChangeFactor));
				} else {
					return hLargeChange;
				}
			}
			set {
				if (!autoLargeChange) {
					hLargeChange = value;
					UpdateScrollbars();
				}
			}
		}

		/// <summary>
		/// Gets or sets the horizontal large change in view coordinates.
		/// </summary>
		/// <remarks>
		/// This value is ignored if <see cref="AutoLargeChange">AutoLargeChange</see> is true.
		/// </remarks>
		/// <value>The horizontal large change in view coordinates</value>
		public virtual float HLargeChangeView {
			get {
				SizeF largeChangeSize = new SizeF(HLargeChangePixels, HLargeChangePixels);
				return view.Camera.LocalToView(largeChangeSize).Width;
			}
			set{
				SizeF largeChangeSize = new SizeF(value, value);
				int hLargeChange = (int)Math.Round(view.Camera.ViewToLocal(largeChangeSize).Width);
				HLargeChangePixels = Math.Max(1, hLargeChange);
			}
		}

		/// <summary>
		/// Gets or sets the vertical small change in pixels.
		/// </summary>
		/// <value>The vertical small change in pixels</value>
		public virtual int VSmallChangePixels {
			get {
				return vSmallChange;
			}
			set {
				vSmallChange = Math.Min(value, VLargeChangePixels);
				UpdateScrollbars();
			}
		}

		/// <summary>
		/// Gets or sets the vertical small change in view coordinates.
		/// </summary>
		/// <value>The vertical small change in pixels</value>
		public virtual float VSmallChangeView {
			get {
				return view.Camera.LocalToView(new SizeF(vSmallChange, vSmallChange)).Width;
			}
			set {
				SizeF smallChangeSize = new SizeF(value, value);
				int vSmallChange = (int)Math.Round(view.Camera.ViewToLocal(smallChangeSize).Width);
				VSmallChangePixels = Math.Max(1, vSmallChange);
			}
		}

		/// <summary>
		/// Gets or sets the horizontal small change in pixels.
		/// </summary>
		/// <value>The horizontal small change in pixels</value>
		public virtual int HSmallChangePixels {
			get {
				return hSmallChange;
			}
			set {
				hSmallChange = Math.Min(value, HLargeChangePixels);
				UpdateScrollbars();
			}
		}

		/// <summary>
		/// Gets or sets the horizontal small change in view coordinates.
		/// </summary>
		/// <value>The horizontal small change in view coordinates</value>
		public virtual float HSmallChangeView {
			get {
				return view.Camera.LocalToView(new SizeF(hSmallChange, hSmallChange)).Width;
			}
			set {
				SizeF smallChangeSize = new SizeF(value, value);
				int hSmallChange = (int)Math.Round(view.Camera.ViewToLocal(smallChangeSize).Width);
				HSmallChangePixels = Math.Max(1, hSmallChange);
			}
		}
		#endregion

		#region Deprecated
		/// <summary>
		/// Deprecated.  Use VSmallChange instead.
		/// <para>
		/// Subclasses should override the get accessor to change the value set when
		/// <see cref="UpdateScrollbars"/> is called.
		/// </para>
		/// </summary>
		public virtual int GetVSmallChange(Size viewSize) {
			return VSmallChangePixels;
		}

		/// <summary>
		/// Deprecated.  Use VLargeChange instead.
		/// <para>
		/// Subclasses should override the get accessor to change the value set when
		/// <see cref="UpdateScrollbars"/> is called.
		/// </para>
		/// </summary>
		public virtual int GetVLargeChange(Size viewSize) {
			return VLargeChangePixels;
		}

		/// <summary>
		/// Deprecated.  Use HSmallChange instead.
		/// <para>
		/// Subclasses should override the get accessor to change the value set when
		/// <see cref="UpdateScrollbars"/> is called.
		/// </para>
		/// </summary>
		public virtual int GetHSmallChange(Size viewSize) {
			return HSmallChangePixels;
		}

		/// <summary>
		/// Deprecated;  Use HLargeChange instead.
		/// <para>
		/// Subclasses should override the get accessor to change the value set when
		/// <see cref="UpdateScrollbars"/> is called.
		/// </para>
		/// </summary>
		public virtual int GetHLargeChange(Size viewSize) {
			return HLargeChangePixels;
		}
		#endregion
	}
}