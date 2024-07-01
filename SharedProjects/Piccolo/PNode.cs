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
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using Piccolo.Activities;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo
{
    #region Delegates
    /// <summary>
    /// A delegate that recieves low level paint invalidated events.
    /// </summary>
    /// <remarks>
    /// Used to recieve low level node events.  This delegate together with the
    /// <see cref="FullBoundsInvalidatedDelegate">FullBoundsInvalidatedDelegate</see> gives
    /// Piccolo users an efficient way to learn about low level changes in Piccolo's scene
    /// graph. Most users will not need to use this.
    /// <seealso cref="FullBoundsInvalidatedDelegate"/>
    /// </remarks>
    public delegate void PaintInvalidatedDelegate(PNode node);

    /// <summary>
    /// A delegate that recieves low level full bounds invalidated events.
    /// </summary>
    /// <remarks>
    /// Used to recieve low level node events.  This delegate together with the
    /// <see cref="PaintInvalidatedDelegate">PaintInvalidatedDelegate</see> gives Piccolo users
    /// an efficient way to learn about low level changes in Piccolo's scene graph.  Most users
    /// will not need to use this.
    /// <seealso cref="PaintInvalidatedDelegate"/>
    /// </remarks>
    public delegate void FullBoundsInvalidatedDelegate(PNode node);

    /// <summary>
    /// A delegate that is used to register for mouse and keyboard events on a PNode.
    /// </summary>
    /// <Remarks>
    /// Note the events that you get depend on the node that you have registered with. For
    /// example you will only get mouse moved events when the mouse is over the node
    /// that you have registered with, not when the mouse is over some other node.
    /// For more control over events, a listener class can be registered rather than an event
    /// handler method.  <see cref="PInputEventListener"/>
    /// <seealso cref="PDragEventHandler"/>
    /// <seealso cref="PDragSequenceEventHandler"/>
    /// <seealso cref="PInputEventArgs"/>
    /// <seealso cref="PPanEventHandler"/>
    /// <seealso cref="PZoomEventHandler"/>
    /// </Remarks>
    public delegate void PInputEventHandler(object sender, PInputEventArgs e);

    /// <summary>
    /// A delegate that is used to register for property change events on a PNode.
    /// </summary>
    /// <remarks>
    /// Note the events that you get depend on the node that you have registered with. For
    /// example you will only get a bounds PropertyEvent when the bounds of the node
    /// that you have registered with changes, not when some other node's bounds change.
    /// <seealso cref="PPropertyEventArgs"/>
    /// </remarks>
    public delegate void PPropertyEventHandler(object sender, PPropertyEventArgs e);
    #endregion

    /// <summary>
    /// <b>PNode</b> is the central abstraction in Piccolo. All objects that are
    /// visible on the screen are instances of the node class. All nodes may have 
    /// other "child" nodes added to them. 
    /// </summary>
    /// <remarks>
    /// See UMD.HCIL.PiccoloExample.NodeExample for demonstrations of how nodes
    /// can be used and how new types of nodes can be created.
    /// </remarks>
    public class PNode : IEnumerable<PNode>
    {
        #region Fields
        // Scene graph delegates

        /// <summary>
        /// Used to recieve low level paint invalidated events.
        /// </summary>
        public PaintInvalidatedDelegate PaintInvalidated;

        /// <summary>
        /// Used to recieve low level full bounds invalidated events.
        /// </summary>
        public FullBoundsInvalidatedDelegate FullBoundsInvalidated;

        // Defines a key for storing the delegate for each event in the Events list.

        /// <summary>
        /// The key that identifies <see cref="KeyDown">KeyDown</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_KEYDOWN = new();

        /// <summary>
        /// The key that identifies <see cref="KeyPress">KeyPress</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_KEYPRESS = new();

        /// <summary>
        /// The key that identifies <see cref="KeyUp">KeyUp</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_KEYUP = new();

        /// <summary>
        /// The key that identifies <see cref="Click">Click</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_CLICK = new();

        /// <summary>
        /// The key that identifies <see cref="DoubleClick">DoubleClick</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_DOUBLECLICK = new();

        /// <summary>
        /// The key that identifies <see cref="MouseDown">MouseDown</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEDOWN = new();

        /// <summary>
        /// The key that identifies <see cref="MouseUp">MouseUp</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEUP = new();

        /// <summary>
        /// The key that identifies <see cref="MouseDrag">MouseDrag</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEDRAG = new();

        /// <summary>
        /// The key that identifies <see cref="MouseMove">MouseMove</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEMOVE = new();

        /// <summary>
        /// The key that identifies <see cref="MouseEnter">MouseEnter</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEENTER = new();

        /// <summary>
        /// The key that identifies <see cref="MouseLeave">MouseLeave</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSELEAVE = new();

        /// <summary>
        /// The key that identifies <see cref="MouseWheel">MouseWheel</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_MOUSEWHEEL = new();

        /// <summary>
        /// The key that identifies <see cref="DragEnter">DragEnter</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_DRAGENTER = new();

        /// <summary>
        /// The key that identifies <see cref="DragLeave">DragLeave</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_DRAGLEAVE = new();

        /// <summary>
        /// The key that identifies <see cref="DragOver">DragOver</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_DRAGOVER = new();

        /// <summary>
        /// The key that identifies <see cref="DragDrop">DragDrop</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_DRAGDROP = new();

        /// <summary>
        /// The key that identifies <see cref="GotFocus">GotFocus</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_GOTFOCUS = new();

        /// <summary>
        /// The key that identifies <see cref="LostFocus">LostFocus</see> events in a node's event
        /// handler list.
        /// </summary>
        protected static readonly object EVENT_KEY_LOSTFOCUS = new();

        // Property change events.

        /// <summary>
        /// The key that identifies <see cref="TagChanged"/> events in a node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_TAG = new();

        /// <summary>
        /// A bit field that identifies a <see cref="TagChanged">TagChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether TagChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_TAG = 1 << 0;

        /// <summary>
        /// The key that identifies <see cref="BoundsChanged">BoundsChanged</see> events in a node's
        /// event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_BOUNDS = new();

        /// <summary>
        /// A bit field that identifies <see cref="BoundsChanged">BoundsChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether BoundsChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_BOUNDS = 1 << 1;

        /// <summary>
        /// The key that identifies <see cref="FullBoundsChanged">FullBoundsChanged</see> events in
        /// a node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_FULLBOUNDS = new();

        /// <summary>
        /// A bit field that identifies a <see cref="FullBoundsChanged">FullBoundsChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether FullBoundsChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_FULLBOUNDS = 1 << 2;

        /// <summary>
        /// The key that identifies <see cref="TransformChanged">TransformChanged</see> events in
        /// a node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_TRANSFORM = new();

        /// <summary>
        /// A bit field that identifies a <see cref="TransformChanged">TransformChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether TransformChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_TRANSFORM = 1 << 3;

        /// <summary>
        /// The key that identifies <see cref="VisibleChanged">VisibleChanged</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_VISIBLE = new();

        /// <summary>
        /// A bit field that identifies a <see cref="VisibleChanged">VisibleChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether VisibleChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_VISIBLE = 1 << 4;

        /// <summary>
        /// The key that identifies <see cref="BrushChanged">BrushChanged</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_BRUSH = new();

        /// <summary>
        /// A bit field that identifies a <see cref="BrushChanged">BrushChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether BrushChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_BRUSH = 1 << 5;

        /// <summary>
        /// The key that identifies <see cref="PickableChanged">PickableChanged</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_PICKABLE = new();

        /// <summary>
        /// A bit field that identifies a <see cref="PickableChanged">PickableChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether PickableChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>	
        public const int PROPERTY_CODE_PICKABLE = 1 << 6;

        /// <summary>
        /// The key that identifies <see cref="ChildrenPickableChanged">ChildrenPickableChanged</see>
        /// events in a node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_CHILDRENPICKABLE = new();

        /// <summary>
        /// A bit field that identifies a
        /// <see cref="ChildrenPickableChanged">ChildrenPickableChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether ChildrenPickableChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_CHILDRENPICKABLE = 1 << 7;

        /// <summary>
        /// The key that identifies <see cref="ChildrenChanged">ChildrenChanged</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_CHILDREN = new();

        /// <summary>
        /// A bit field that identifies a <see cref="ChildrenChanged">ChildrenChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether ChildrenChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>	
        public const int PROPERTY_CODE_CHILDREN = 1 << 8;

        /// <summary>
        /// The key that identifies <see cref="ParentChanged">ParentChanged</see> events in a
        /// node's event handler list.
        /// </summary>
        protected static readonly object PROPERTY_KEY_PARENT = new();

        /// <summary>
        /// A bit field that identifies a <see cref="ParentChanged">ParentChanged</see> event.
        /// </summary>
        /// <remarks>
        /// This field is used to indicate whether ParentChanged events should be forwarded to
        /// a node's parent.
        /// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
        /// <seealso cref="PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
        /// </remarks>
        public const int PROPERTY_CODE_PARENT = 1 << 9;

		private PNode parent;
		private List<PNode> children;

        /// <summary>
        /// The bounds of this node, stored in local coordinates.
        /// </summary>
        protected RectangleF bounds;

        private readonly PMatrix matrix;
        private Brush brush;
        private object tag;
        private RectangleF fullBoundsCache;

        private readonly EventHandlerList handlers;

        private bool pickable;
        private bool childrenPickable;
        private bool visible;
        private bool childBoundsVolatile;
        private bool fullBoundsInvalid;

        #endregion

        #region Constructors
        /// <summary>
        /// Constructs a new PNode.
        /// </summary>
        /// <Remarks>
        /// By default a node's brush is null, and bounds are empty. These values
        /// must be set for the node to show up on the screen once it's added to
        /// a scene graph.
        /// </Remarks>
        public PNode()
        {
            bounds = RectangleF.Empty;
            fullBoundsCache = RectangleF.Empty;
            pickable = true;
            childrenPickable = true;
            matrix = new PMatrix();
            handlers = new EventHandlerList();
            Visible = true;
        }
        #endregion

        #region Tag
        //****************************************************************
        // Tag Property - The Tag Property provides a way for programmers
        // to attach extra information to a node without having to
        // subclass it and add new instance variables.
        //****************************************************************

        /// <summary>
        /// Gets or sets the object that contains data about the control.
        /// </summary>
        /// <value>
        /// An Object that contains data about the control. The default is a null reference.
        /// </value>
        /// <remarks>
        /// Any type derived from the Object class can be assigned to this property.
        /// <para>
        /// A common use for the Tag property is to store data that is closely associated with
        /// the node. For example, if you have a node with a tooltip, you might store the text
        /// in that node's tag property.
        /// </para>
        /// </remarks>
        public object Tag
        {
            get => tag;
            set
            {
                tag = value;
                FirePropertyChangedEvent(PROPERTY_KEY_TAG, PROPERTY_CODE_TAG, null, tag);
            }
        }
        #endregion

        #region Animation Methods
        //****************************************************************
        // Animation - Methods to animate this node.
        // 
        // Note that animation is implemented by activities (PActivity), 
        // so if you need more control over your animation look at the 
        // activities package. Each animate method creates an animation that
        // will animate the node from its current state to the new state
        // specified over the given duration. These methods will try to 
        // automatically schedule the new activity, but if the node does not
        // descend from the root node when the method is called then the 
        // activity will not be scheduled and you must schedule it manually.
        //****************************************************************

        /// <summary>
        /// Animate this node's bounds from their current location when the activity
        /// starts to the specified bounds.
        /// </summary>
        /// <param name="x">The x coordinate of the target bounds.</param>
        /// <param name="y">The y coordinate of the target bounds.</param>
        /// <param name="width">The width of the target bounds.</param>
        /// <param name="height">The height of the target bounds.</param>
        /// <param name="duration">The amount of time that the animation should take.</param>
        /// <returns>The newly scheduled activity.</returns>
        /// <remarks>
        /// If this node descends from the root then the activity will be scheduled,
        /// else the returned activity should be scheduled manually. If two different
        /// transform activities are scheduled for the same node at the same time,
        /// they will both be applied to the node, but the last one scheduled will be
        /// applied last on each frame, so it will appear to have replaced the original.
        /// Generally you will not want to do that. Note this method animates the node's
        /// bounds, but does not change the node's matrix. Use <see cref="AnimateMatrixToBounds">
        /// AnimateMatrixToBounds</see> to animate the node's matrix instead.
        /// </remarks>
        public virtual PInterpolatingActivity AnimateToBounds(float x, float y, float width, float height, long duration)
        {
            var dst = new RectangleF(x, y, width, height);

            if (duration == 0)
            {
                Bounds = dst;
                return null;
            }

            PInterpolatingActivity ta = new PNodeBoundsActivity(this, dst, duration);
            AddActivity(ta);
            return ta;
        }

        /// <summary>
        /// An activity that animates the target node's bounds from the source rectangle to
        /// the destination rectangle.
        /// </summary>
        public class PNodeBoundsActivity : PInterpolatingActivity
        {
            private RectangleF src;
            private readonly RectangleF dst;

            /// <summary>
            /// Constructs a new PNodeBoundsActivity
            /// </summary>
            /// <param name="target">The target node.</param>
            /// <param name="dst">The destination bounds.</param>
            /// <param name="duration">The duration of the activity.</param>
            public PNodeBoundsActivity(PNode target, RectangleF dst, long duration)
                : base(duration, PUtil.DEFAULT_ACTIVITY_STEP_RATE)
            {
                this.Target = target;
                this.dst = dst;
            }
            /// <summary>
            /// Gets the target node.
            /// </summary>
            public PNode Target { get; }

            /// <summary>
            /// Overridden.  See
            /// <see cref="PInterpolatingActivity.OnActivityStarted">base.OnActivityStarted</see>.
            /// </summary>
            protected override void OnActivityStarted()
            {
                src = Target.Bounds;
                Target.StartResizeBounds();
                base.OnActivityStarted();
            }
            /// <summary>
            /// Overridden.  See
            /// <see cref="PInterpolatingActivity.SetRelativeTargetValue">base.SetRelativeTargetValue</see>.
            /// </summary>
            public override void SetRelativeTargetValue(float zeroToOne)
            {
                Target.SetBounds(src.X + (zeroToOne * (dst.X - src.X)),
                    src.Y + (zeroToOne * (dst.Y - src.Y)),
                    src.Width + (zeroToOne * (dst.Width - src.Width)),
                    src.Height + (zeroToOne * (dst.Height - src.Height)));
            }

            /// <summary>
            /// Overridden.  See
            /// <see cref="PInterpolatingActivity.OnActivityFinished">base.OnActivityFinished</see>.
            /// </summary>
            protected override void OnActivityFinished()
            {
                base.OnActivityFinished();
                Target.EndResizeBounds();
            }
        }

        /// <summary>
        /// Animate this node from it's current matrix when the activity starts a new matrix that will fit
        /// the node into the given bounds.
        /// </summary>
        /// <param name="x">The x coordinate of the target bounds.</param>
        /// <param name="y">The y coordinate of the target bounds.</param>
        /// <param name="width">The width of the target bounds.</param>
        /// <param name="height">The height of the target bounds.</param>
        /// <param name="duration">The amount of time that the animation should take.</param>
        /// <returns>The newly scheduled activity.</returns>
        /// <remarks>
        /// If this node descends from the root then the activity will be scheduled,
        /// else the returned activity should be scheduled manually. If two different
        /// transform activities are scheduled for the same node at the same time,
        /// they will both be applied to the node, but the last one scheduled will be
        /// applied last on each frame, so it will appear to have replaced the original.
        /// Generally you will not want to do that. Note this method animates the node's
        /// matrix, but does not directly change the node's bounds rectangle. Use
        /// <see cref="AnimateToBounds">AnimateToBounds</see> to animate the node's bounds
        /// rectangle instead.
        /// </remarks>
        public PTransformActivity AnimateMatrixToBounds(float x, float y, float width, float height, long duration)
        {
            PMatrix m = new PMatrix();
            m.ScaleBy(width / Width, height / Height);

            float scale = m.Scale;
            m.OffsetX = x - (X * scale);
            m.OffsetY = y - (Y * scale);

            return AnimateToMatrix(m, duration);
        }

        /// <summary>
        /// Animate this node's matrix from its current location when the
        /// activity starts to the specified location, scale, and rotation.
        /// </summary>
        /// <param name="x">The x coordinate of the target location.</param>
        /// <param name="y">The y coordinate of the target location</param>
        /// <param name="scale">The scale of the target matrix</param>
        /// <param name="theta">The rotation of the target matrix</param>
        /// <param name="duration">The amount of time that the animation should take.</param>
        /// <returns>The newly scheduled activity.</returns>
        /// <remarks>
        /// If this node descends from the root then the activity will be scheduled,
        /// else the returned activity should be scheduled manually. If two different
        /// transform activities are scheduled for the same node at the same time,
        /// they will both be applied to the node, but the last one scheduled will be
        /// applied last on each frame, so it will appear to have replaced the
        /// original. Generally you will not want to do that.
        /// </remarks>
        public virtual PTransformActivity AnimateToPositionScaleRotation(float x, float y, float scale, float theta, long duration)
        {
            PMatrix m = Matrix;
            m.OffsetX = x;
            m.OffsetY = y;
            m.Scale = scale;
            m.Rotation = theta;
            return AnimateToMatrix(m, duration);
        }

        /// <summary>
        /// Animate this node's matrix from its current values when the activity
        /// starts to the new values specified in the given matrix.
        /// </summary>
        /// <param name="destMatrix">The final matrix value.</param>
        /// <param name="duration">The amount of time that the animation should take.</param>
        /// <returns>The newly scheduled activity</returns>
        /// <remarks>
        /// If this node descends from the root then the activity will be scheduled,
        /// else the returned activity should be scheduled manually. If two different
        /// transform activities are scheduled for the same node at the same time,
        /// they will both be applied to the node, but the last one scheduled will be
        /// applied last on each frame, so it will appear to have replaced the
        /// original. Generally you will not want to do that.
        /// </remarks>
        public virtual PTransformActivity AnimateToMatrix(PMatrix destMatrix, long duration)
        {
            if (duration == 0)
            {
                Matrix = destMatrix;
                return null;
            }

            PTransformActivity.Target t = new PNodeTransformTarget(this);
            PTransformActivity ta = new PTransformActivity(duration, PUtil.DEFAULT_ACTIVITY_STEP_RATE, t, destMatrix);
            AddActivity(ta);
            return ta;
        }

        /// <summary>
        /// A target for a transform activity that gets and sets the matrix of the specified PNode.
        /// </summary>
        public class PNodeTransformTarget : PTransformActivity.Target
        {
            /// <summary>
            /// Constructs a new PNodeTransformTarget.
            /// </summary>
            /// <param name="target">The target node.</param>
            public PNodeTransformTarget(PNode target)
            {
                this.Target = target;
            }
            /// <summary>
            /// Gets the target node.
            /// </summary>
            public PNode Target { get; }

            /// <summary>
            /// Implements the PTransformActivity.Target interface.
            /// </summary>
            public PMatrix Matrix
            {
                get => Target.MatrixReference;
                set => Target.Matrix = value;
            }
        }

        /// <summary>
        /// Animate this node's color from its current value to the new value
        /// specified.
        /// </summary>
        /// <param name="destColor">The final color value.</param>
        /// <param name="duration">The amount of time that the animation should take.</param>
        /// <returns>The newly scheduled activity.</returns>
        /// <remarks>
        /// This method assumes that this nodes Brush property is of type SolidBrush.
        /// If this node descends from the root then the activity will be scheduled,
        /// else the returned activity should be scheduled manually. If two different
        /// color activities are scheduled for the same node at the same time, they will
        /// both be applied to the node, but the last one scheduled will be applied last
        /// on each frame, so it will appear to have replaced the original. Generally
        /// you will not want to do that.
        /// </remarks>
        public virtual PColorActivity AnimateToColor(Color destColor, long duration)
        {
            PColorActivity.Target t = new PNodeColorTarget(this);

            if (duration == 0)
            {
                t.Color = destColor;
                return null;
            }

            PColorActivity ca = new PColorActivity(duration, PUtil.DEFAULT_ACTIVITY_STEP_RATE, t, destColor);
            AddActivity(ca);
            return ca;
        }

        /// <summary>
        /// A target for a color activity that gets and sets the color of the specified PNode.
        /// </summary>
        public class PNodeColorTarget : PColorActivity.Target
        {
            /// <summary>
            /// Constructs a new PNodeColorTarget
            /// </summary>
            /// <param name="target">The target node.</param>
            public PNodeColorTarget(PNode target)
            {
                this.Target = target;
            }
            /// <summary>
            /// Gets the target node.
            /// </summary>
            public PNode Target { get; }

            /// <summary>
            /// Implements the PColorActivity.Target interface.
            /// </summary>
            public Color Color
            {
                get => ((SolidBrush)Target.Brush).Color;
                set => Target.Brush = new SolidBrush(value);
            }
        }

        /// <summary>
        /// Schedule the given activity with the root.
        /// </summary>
        /// <param name="activity">The new activity to schedule.</param>
        /// <returns>True if the activity is successfully scheduled.</returns>
        /// <remarks>
        /// Note that only scheduled activities will be stepped. If the activity is
        /// successfully added true is returned, else false. 
        /// </remarks>
        public virtual bool AddActivity(PActivity activity)
        {
            PRoot r = Root;
            if (r != null)
            {
                return r.AddActivity(activity);
            }
            return false;
        }
        #endregion

        #region Coordinate System Conversions
        //****************************************************************
        // Coordinate System Conversions - Methods for converting 
        // geometry between this nodes local coordinates and the other 
        // major coordinate systems.
        // 
        // Each nodes has a matrix that it uses to define its own coordinate
        // system. For example if you create a new node and add it to the
        // canvas it will appear in the upper right corner. Its coordinate
        // system matches the coordinate system of its parent (the root node)
        // at this point. But if you move this node by calling
        // node.TranslateBy() the nodes matrix will be modified and the node
        // will appear at a different location on the screen. The node
        // coordinate system no longer matches the coordinate system of its
        // parent. 
        //
        // This is useful because it means that the node's methods for 
        // rendering and picking don't need to worry about the fact that 
        // the node has been moved to another position on the screen, they
        // keep working just like they did when it was in the upper right 
        // hand corner of the screen.
        // 
        // The problem is now that each node defines its own coordinate
        // system it is difficult to compare the positions of two node with
        // each other. These methods are all meant to help solve that problem.
        // 
        // The terms used in the methods are as follows:
        // 
        // local - The local or base coordinate system of a node.
        // parent - The coordinate system of a node's parent
        // global - The topmost coordinate system, above the root node.
        // 
        // Normally when comparing the positions of two nodes you will 
        // convert the local position of each node to the global coordinate
        // system, and then compare the positions in that common coordinate
        // system.
        //***************************************************************

        /// <summary>
        /// Transform the given point from this node's local coordinate system to
        /// its parent's local coordinate system.
        /// </summary>
        /// <param name="point">The point in local coordinate system to be transformed.</param>
        /// <returns>The point in parent's local coordinate system.</returns>
        public virtual PointF LocalToParent(PointF point)
        {
            return matrix.Transform(point);
        }

        /// <summary>
        /// Transform the given size from this node's local coordinate system to
        /// its parent's local coordinate system.
        /// </summary>
        /// <param name="size">The size in local coordinate system to be transformed.</param>
        /// <returns>The point in parent's local coordinate system.</returns>
        public virtual SizeF LocalToParent(SizeF size)
        {
            return matrix.Transform(size);
        }

        /// <summary>
        /// Transform the given rectangle from this node's local coordinate system to
        /// its parent's local coordinate system.
        /// </summary>
        /// <param name="rectangle">The rectangle in local coordinate system to be transformed.</param>
        /// <returns>The rectangle in parent's local coordinate system.</returns>
        public virtual RectangleF LocalToParent(RectangleF rectangle)
        {
            return matrix.Transform(rectangle);
        }

        /// <summary>
        /// Transform the given point from this node's parent's local coordinate system to
        /// the local coordinate system of this node.
        /// </summary>
        /// <param name="point">The point in parent's coordinate system to be transformed.</param>
        /// <returns>The point in this node's local coordinate system.</returns>
        public virtual PointF ParentToLocal(PointF point)
        {
            return matrix.InverseTransform(point);
        }

        /// <summary>
        /// Transform the given size from this node's parent's local coordinate system to
        /// the local coordinate system of this node.
        /// </summary>
        /// <param name="size">The size in parent's coordinate system to be transformed.</param>
        /// <returns>The size in this node's local coordinate system.</returns>
        public virtual SizeF ParentToLocal(SizeF size)
        {
            return matrix.InverseTransform(size);
        }

        /// <summary>
        /// Transform the given rectangle from this node's parent's local coordinate system to
        /// the local coordinate system of this node.
        /// </summary>
        /// <param name="rectangle">The rectangle in parent's coordinate system to be transformed.</param>
        /// <returns>The rectangle in this node's local coordinate system.</returns>
        public virtual RectangleF ParentToLocal(RectangleF rectangle)
        {
            return matrix.InverseTransform(rectangle);
        }

        /// <summary>
        /// Transform the given point from this node's local coordinate system to
        /// the global coordinate system.
        /// </summary>
        /// <param name="point">The point in local coordinate system to be transformed.</param>
        /// <returns>The point in global coordinates.</returns>
        public virtual PointF LocalToGlobal(PointF point)
        {
            PNode n = this;
            while (n != null)
            {
                point = n.LocalToParent(point);
                n = n.Parent;
            }
            return point;
        }

        /// <summary>
        /// Transform the given size from this node's local coordinate system to
        /// the global coordinate system.
        /// </summary>
        /// <param name="size">The size in local coordinate system to be transformed.</param>
        /// <returns>The size in global coordinates.</returns>
        public virtual SizeF LocalToGlobal(SizeF size)
        {
            PNode n = this;
            while (n != null)
            {
                size = n.LocalToParent(size);
                n = n.Parent;
            }
            return size;
        }

        /// <summary>
        /// Transform the given rectangle from this node's local coordinate system to
        /// the global coordinate system.
        /// </summary>
        /// <param name="rectangle">The rectangle in local coordinate system to be transformed.</param>
        /// <returns>The rectangle in global coordinates.</returns>
        public virtual RectangleF LocalToGlobal(RectangleF rectangle)
        {
            PNode n = this;
            while (n != null)
            {
                rectangle = n.LocalToParent(rectangle);
                n = n.Parent;
            }
            return rectangle;
        }

        /// <summary>
        /// Transform the given point from global coordinates to this node's 
        /// local coordinate system.
        /// </summary>
        /// <param name="point">The point in global coordinates to be transformed.</param>
        /// <returns>The point in this node's local coordinate system.</returns>
        public virtual PointF GlobalToLocal(PointF point)
        {
            if (Parent != null)
            {
                point = Parent.GlobalToLocal(point);
            }
            return ParentToLocal(point);
        }

        /// <summary>
        /// Transform the given size from global coordinates to this node's 
        /// local coordinate system.
        /// </summary>
        /// <param name="size">The size in global coordinates to be transformed.</param>
        /// <returns>The size in this node's local coordinate system.</returns>
        public virtual SizeF GlobalToLocal(SizeF size)
        {
            if (Parent != null)
            {
                size = Parent.GlobalToLocal(size);
            }
            return ParentToLocal(size);
        }

        /// <summary>
        /// Transform the given rectangle from global coordinates to this node's 
        /// local coordinate system.
        /// </summary>
        /// <param name="rectangle">The rectangle in global coordinates to be transformed.</param>
        /// <returns>The rectangle in this node's local coordinate system.</returns>
        public virtual RectangleF GlobalToLocal(RectangleF rectangle)
        {
            if (Parent != null)
            {
                rectangle = Parent.GlobalToLocal(rectangle);
            }
            return ParentToLocal(rectangle);
        }

        /// <summary>
        /// Return the matrix that converts local coordinates at this node 
        /// to the global coordinate system.
        /// </summary>
        /// <value>The concatenation of matrices from the top node down to this
        /// node.</value>
        public virtual PMatrix LocalToGlobalMatrix
        {
            get
            {
                if (Parent != null)
                {
                    PMatrix result = Parent.LocalToGlobalMatrix;
                    result.Multiply(MatrixReference);
                    return result;
                }
                else
                {
                    return Matrix;
                }
            }
        }

        /// <summary>
        /// Return the matrix that converts global coordinates  to local coordinates
        /// of this node.
        /// </summary>
        /// <value>The inverse of the concatenation of matrices from the root down to
        /// this node.</value>
        public virtual PMatrix GlobalToLocalMatrix
        {
            get
            {
                PMatrix r = LocalToGlobalMatrix;
                r.Invert();
                return r;
            }
        }
        #endregion

        #region Events
        //****************************************************************
        // Event Handlers - Methods for adding and removing event handler
        // delegates and event listener classes to and from a node.
        //
        // Here methods are provided to add property change handlers and input
        // event handlers. The property change handlers are notified when
        // certain properties of this node change, and the input event handlers
        // are notified when the node receives new key and mouse events.
        //
        // There are two ways to listen to input events.  One can use the
        // standard .NET approach of adding a handler method (delegate) to an
        // event for a given node.  For example, to listen for MouseDown events
        // on aNode, you could do the following.
        //
        // aNode.MouseDown += new PInputEventHandler(PNode_MouseDown);
        //
        // However, if you would like to add state to your event listeners and
        // the ability to filter events, then you will need to add an event
        // listener class.  You can use an existing one, or implement your own.
        // For example, to add a PZoomEventHandler to aNode, you could do the
        // following.
        //
        // aNode.AddInputEventListener(new PZoomEventHandler());
        //
        // See Piccolo.Events for more details.
        //****************************************************************

        /// <summary>
        /// Return the list of event handlers associated with this node.
        /// </summary>
        /// <value>The list of event handlers for this node.</value>
        public virtual EventHandlerList HandlerList => handlers;

        // Input Events

        /// <summary>
        /// Occurs when a key is pressed while the node has focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the KeyDown Event as in
        /// KeyDown += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by keyDownEventKey in the Events list).
        /// When a user removes an event handler from the KeyDown event as in 
        /// KeyDown -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by keyDownEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler KeyDown
        {
            add => handlers.AddHandler(EVENT_KEY_KEYDOWN, value);
            remove => handlers.RemoveHandler(EVENT_KEY_KEYDOWN, value);
        }

        /// <summary>
        /// Occurs when a key is pressed while the node has focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the KeyPress Event as in
        /// KeyPress += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by keyPressEventKey in the Events list).
        /// When a user removes an event handler from the KeyPress event as in 
        /// KeyPress -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by keyPressEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler KeyPress
        {
            add => handlers.AddHandler(EVENT_KEY_KEYPRESS, value);
            remove => handlers.RemoveHandler(EVENT_KEY_KEYPRESS, value);
        }

        /// <summary>
        /// Occurs when a key is released while the node has focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the KeyUp Event as in
        /// KeyUp += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by keyUpEventKey in the Events list).
        /// When a user removes an event handler from the KeyUp event as in 
        /// KeyUp -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by keyUpEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler KeyUp
        {
            add => handlers.AddHandler(EVENT_KEY_KEYUP, value);
            remove => handlers.RemoveHandler(EVENT_KEY_KEYUP, value);
        }

        /// <summary>
        /// Occurs when the node is clicked.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the Click Event as in
        /// Click += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by clickEventKey in the Events list).
        /// When a user removes an event handler from the Click event as in 
        /// Click -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by clickEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler Click
        {
            add => handlers.AddHandler(EVENT_KEY_CLICK, value);
            remove => handlers.RemoveHandler(EVENT_KEY_CLICK, value);
        }

        /// <summary>
        /// Occurs when the node is double clicked.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the DoubleClick Event as in
        /// DoubleClick += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by doubleClickEventKey in the Events list).
        /// When a user removes an event handler from the DoubleClick event as in 
        /// DoubleClick -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by doubleClickEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler DoubleClick
        {
            add => handlers.AddHandler(EVENT_KEY_DOUBLECLICK, value);
            remove => handlers.RemoveHandler(EVENT_KEY_DOUBLECLICK, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer is over the node and a mouse button is pressed.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseDown Event as in
        /// MouseDown += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseDownEventKey in the Events list).
        /// When a user removes an event handler from the MouseDown event as in 
        /// MouseDown -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseDownEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseDown
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSEDOWN, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEDOWN, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer is over the node and a mouse button is released.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseUp Event as in
        /// MouseUp += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseUpEventKey in the Events list).
        /// When a user removes an event handler from the MouseUp event as in 
        /// MouseUp -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseUpEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseUp
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSEUP, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEUP, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer is moved over the node.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseMove Event as in
        /// MouseMove += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseMoveEventKey in the Events list).
        /// When a user removes an event handler from the MouseMove event as in 
        /// MouseMove -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseMoveEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseMove
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSEMOVE, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEMOVE, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer is dragged over the node.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseDrag Event as in
        /// MouseDrag += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseDragEventKey in the Events list).
        /// When a user removes an event handler from the MouseDrag event as in 
        /// MouseDrag -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseDragEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseDrag
        {
            add
            {
                //By removing first we ensure a duplicate is not added
                handlers.RemoveHandler(EVENT_KEY_MOUSEDRAG, value);
                handlers.AddHandler(EVENT_KEY_MOUSEDRAG, value);
            }
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEDRAG, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer enters the node.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseEnter Event as in
        /// MouseEnter += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseEnterEventKey in the Events list).
        /// When a user removes an event handler from the MouseEnter event as in 
        /// MouseEnter -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseEnterEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseEnter
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSEENTER, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEENTER, value);
        }

        /// <summary>
        /// Occurs when the mouse pointer leaves the node.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseLeave Event as in
        /// MouseLeave += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseLeaveEventKey in the Events list).
        /// When a user removes an event handler from the MouseLeave event as in 
        /// MouseLeave -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseLeaveEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseLeave
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSELEAVE, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSELEAVE, value);
        }

        /// <summary>
        /// Occurs when the mouse wheel moves while the node has focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the MouseWheel Event as in
        /// MouseWheel += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by mouseWheelEventKey in the Events list).
        /// When a user removes an event handler from the MouseWheel event as in 
        /// MouseWheel -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by mouseWheelEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler MouseWheel
        {
            add => handlers.AddHandler(EVENT_KEY_MOUSEWHEEL, value);
            remove => handlers.RemoveHandler(EVENT_KEY_MOUSEWHEEL, value);
        }

        /// <summary>
        /// Occurs when an object is dragged into this node's bounds.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the DragEnter Event as in
        /// DragEnter += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by DragEnterEventKey in the Events list).
        /// When a user removes an event handler from the DragEnter event as in 
        /// DragEnter -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by DragEnterEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler DragEnter
        {
            add => handlers.AddHandler(EVENT_KEY_DRAGENTER, value);
            remove => handlers.RemoveHandler(EVENT_KEY_DRAGENTER, value);
        }

        /// <summary>
        /// Occurs when an object is dragged out of this node's bounds.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the DragLeave Event as in
        /// DragLeave += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by DragLeaveEventKey in the Events list).
        /// When a user removes an event handler from the DragLeave event as in 
        /// DragLeave -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by DragLeaveEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler DragLeave
        {
            add => handlers.AddHandler(EVENT_KEY_DRAGLEAVE, value);
            remove => handlers.RemoveHandler(EVENT_KEY_DRAGLEAVE, value);
        }

        /// <summary>
        /// Occurs when an object is dragged over this node's bounds.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the DragOver Event as in
        /// DragOver += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by dragOverEventKey in the Events list).
        /// When a user removes an event handler from the DragOver event as in 
        /// DragOver -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by dragOverEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler DragOver
        {
            add => handlers.AddHandler(EVENT_KEY_DRAGOVER, value);
            remove => handlers.RemoveHandler(EVENT_KEY_DRAGOVER, value);
        }

        /// <summary>
        /// Occurs when a drag-and-drop operation is completed.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the DragDrop Event as in
        /// DragDrop += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by dragDropEventKey in the Events list).
        /// When a user removes an event handler from the DragDrop event as in 
        /// DragDrop -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by dragDropEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler DragDrop
        {
            add => handlers.AddHandler(EVENT_KEY_DRAGDROP, value);
            remove => handlers.RemoveHandler(EVENT_KEY_DRAGDROP, value);
        }

        /// <summary>
        /// Occurs when the node receives focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the GotFocus Event as in
        /// GotFocus += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by gotFocusEventKey in the Events list).
        /// When a user removes an event handler from the GotFocus event as in 
        /// GotFocus -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by gotFocusEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler GotFocus
        {
            add => handlers.AddHandler(EVENT_KEY_GOTFOCUS, value);
            remove => handlers.RemoveHandler(EVENT_KEY_GOTFOCUS, value);
        }

        /// <summary>
        /// Occurs when the node loses focus.
        /// </summary>
        /// <remarks>
        /// When a user attaches an event handler to the LostFocus Event as in
        /// LostFocus += new PInputEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by lostFocusEventKey in the Events list).
        /// When a user removes an event handler from the LostFocus event as in 
        /// LostFocus -= new PInputEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by lostFocusEventKey in the Events list).
        /// </remarks>
        public virtual event PInputEventHandler LostFocus
        {
            add => handlers.AddHandler(EVENT_KEY_LOSTFOCUS, value);
            remove => handlers.RemoveHandler(EVENT_KEY_LOSTFOCUS, value);
        }

        // Property change events

        /// <summary>
        /// Occurs when the value of the <see cref="Tag">Tag</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the new
        /// value will be a reference to this node's tag, but old value will always be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the TagChanged Event as in
        /// TagChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_TAG in the Events list).
        /// When a user removes an event handler from the tagChanged event as in 
        /// TagChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_TAG in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler TagChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_TAG, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_TAG, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Bounds">Bounds</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the new value will
        /// be a this node's bounds, but old value will always be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the BoundsChanged Event as in
        /// BoundsChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_BOUNDS in the Events list).
        /// When a user removes an event handler from the BoundsChanged event as in 
        /// BoundsChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_BOUNDS in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler BoundsChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_BOUNDS, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_BOUNDS, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="FullBounds">FullBounds</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the new value will be
        /// a this node's full bounds cache, but old value will always be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the FullBoundsChanged Event as in
        /// FullBoundsChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_FULLBOUNDS in the Events list).
        /// When a user removes an event handler from the FullBoundsChanged event as in 
        /// FullBoundsChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_FULLBOUNDS in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler FullBoundsChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_FULLBOUNDS, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_FULLBOUNDS, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Matrix">Matrix</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the new value
        /// will be a reference to this node's matrix, but old value will always be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the TransformChanged Event as in
        /// TransformChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_TRANSFORM in the Events list).
        /// When a user removes an event handler from the TransformChanged event as in 
        /// TransformChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_TRANSFORM in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler TransformChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_TRANSFORM, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_TRANSFORM, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Visible">Visible</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, both the old and
        /// new value will be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the VisibleChanged Event as in
        /// VisibleChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_VISIBLE in the Events list).
        /// When a user removes an event handler from the VisibleChanged event as in 
        /// VisibleChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_VISIBLE in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler VisibleChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_VISIBLE, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_VISIBLE, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Brush">Brush</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, both the old
        /// and new value will be a set correctly.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the BrushChanged Event as in
        /// BrushChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_BRUSH in the Events list).
        /// When a user removes an event handler from the BrushChanged event as in 
        /// BrushChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_BRUSH in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler BrushChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_BRUSH, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_BRUSH, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Pickable">Pickable</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, both the old and
        /// new value will be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the PickableChanged Event as in
        /// PickableChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_PICKABLE in the Events list).
        /// When a user removes an event handler from the PickableChanged event as in 
        /// PickableChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_PICKABLE in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler PickableChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_PICKABLE, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_PICKABLE, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="ChildrenPickable">ChildrenPickable</see>
        /// property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, both the old
        /// and new value will be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the ChildrenPickableChanged Event as in
        /// ChildrenPickableChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_CHILDRENPICKABLE in the Events list).
        /// When a user removes an event handler from the ChildrenPickableChanged event as in 
        /// ChildrenPickableChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_CHILDRENPICKABLE in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler ChildrenPickableChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_CHILDRENPICKABLE, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_CHILDRENPICKABLE, value);
        }

        /// <summary>
        /// Occurs when there is a change in the set of this node's direct children.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the new value will
        /// be a reference to this node's children, but the old value will always be null.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the ChildrenChanged Event as in
        /// ChildrenChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_CHILDREN in the Events list).
        /// When a user removes an event handler from the ChildrenChanged event as in 
        /// ChildrenChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_CHILDREN in the Events list).
        /// </para>
        /// <seealso cref="PNode.ChildrenReference">PNode.ChildrenReference</seealso>
        /// </remarks>
        public virtual event PPropertyEventHandler ChildrenChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_CHILDREN, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_CHILDREN, value);
        }

        /// <summary>
        /// Occurs when the value of the <see cref="Parent">Parent</see> property changes.
        /// </summary>
        /// <remarks>
        /// <para>
        /// In the <see cref="PPropertyEventArgs">PPropertyEventArgs</see>, the old and new
        /// value will be a set correctly.
        /// </para>
        /// <para>
        /// When a user attaches an event handler to the ParentChanged Event as in
        /// ParentChanged += new PPropertyEventHandler(aHandler),
        /// the add method adds the handler to the delegate for the event
        /// (keyed by PROPERTY_KEY_PARENT in the Events list).
        /// When a user removes an event handler from the ParentChanged event as in 
        /// ParentChanged -= new PPropertyEventHandler(aHandler),
        /// the remove method removes the handler from the delegate for the event
        /// (keyed by PROPERTY_KEY_PARENT in the Events list).
        /// </para>
        /// </remarks>
        public virtual event PPropertyEventHandler ParentChanged
        {
            add => handlers.AddHandler(PROPERTY_KEY_PARENT, value);
            remove => handlers.RemoveHandler(PROPERTY_KEY_PARENT, value);
        }

        /// <summary>
        /// Adds the specified input event listener to receive input events 
        /// from this node.
        /// </summary>
        /// <param name="listener">The new input listener</param>
        public virtual void AddInputEventListener(PInputEventListener listener)
        {
            KeyDown += listener.OnKeyDown;
            KeyPress += listener.OnKeyPress;
            KeyUp += listener.OnKeyUp;
            Click += listener.OnClick;
            DoubleClick += listener.OnDoubleClick;
            MouseDown += listener.OnMouseDown;
            MouseUp += listener.OnMouseUp;
            MouseMove += listener.OnMouseMove;
            MouseDrag += listener.OnMouseDrag;
            MouseEnter += listener.OnMouseEnter;
            MouseLeave += listener.OnMouseLeave;
            MouseWheel += listener.OnMouseWheel;
            DragEnter += listener.OnDragEnter;
            DragLeave += listener.OnDragLeave;
            DragOver += listener.OnDragOver;
            DragDrop += listener.OnDragDrop;
            GotFocus += listener.OnGotFocus;
            LostFocus += listener.OnLostFocus;
        }

        /// <summary>
        /// Removes the specified input event listener so that it no longer 
        /// receives input events from this node.
        /// </summary>
        /// <param name="listener">The input listener to remove.</param>
        public virtual void RemoveInputEventListener(PInputEventListener listener)
        {
            KeyDown -= listener.OnKeyDown;
            KeyPress -= listener.OnKeyPress;
            KeyUp -= listener.OnKeyUp;
            Click -= listener.OnClick;
            DoubleClick -= listener.OnDoubleClick;
            MouseDown -= listener.OnMouseDown;
            MouseUp -= listener.OnMouseUp;
            MouseMove -= listener.OnMouseMove;
            MouseDrag -= listener.OnMouseDrag;
            MouseEnter -= listener.OnMouseEnter;
            MouseLeave -= listener.OnMouseLeave;
            MouseWheel -= listener.OnMouseWheel;
            DragEnter -= listener.OnDragEnter;
            DragLeave -= listener.OnDragLeave;
            DragOver -= listener.OnDragOver;
            DragDrop -= listener.OnDragDrop;
            GotFocus -= listener.OnGotFocus;
            LostFocus -= listener.OnLostFocus;
        }

        /// <summary>
        /// Gets or sets a value that determines which property change events are
        /// forwarded to this node's parent so that it's property change listeners
        /// will also be notified.
        /// </summary>
        /// <value>
        /// A bitwise combination of the PROPERTY_CODE_XXX flags that determines
        /// which properties change events are forwarded to this node's parent.
        /// </value>
        public int PropertyChangeParentMask { get; set; } = 0;

        /// <summary>
        /// Raises the KeyDown event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnKeyDown method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnKeyDown in a derived class,
        /// be sure to call the base class's OnKeyDown method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnKeyDown(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_KEYDOWN];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the KeyPress event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnKeyPress method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnKeyPress in a derived class,
        /// be sure to call the base class's OnKeyPress method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnKeyPress(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_KEYPRESS];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the KeyUp event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnKeyUp method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnKeyUp in a derived class,
        /// be sure to call the base class's OnKeyUp method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnKeyUp(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_KEYUP];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the Click event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnClick method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnClick in a derived class,
        /// be sure to call the base class's OnClick method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnClick(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_CLICK];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the DoubleClick event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnDoubleClick method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnDoubleClick in a derived class,
        /// be sure to call the base class's OnDOubleClick method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnDoubleClick(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_DOUBLECLICK];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseDown event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseDown method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseDown in a derived class,
        /// be sure to call the base class's OnMouseDown method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseDown(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEDOWN];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseUp event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseUp method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseUp in a derived class,
        /// be sure to call the base class's OnMouseUp method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseUp(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEUP];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseMove event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseMove method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseMove in a derived class,
        /// be sure to call the base class's OnMouseMove method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseMove(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEMOVE];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseDrag event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseDrag method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseDrag in a derived class,
        /// be sure to call the base class's OnMouseDrag method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseDrag(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEDRAG];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseEnter event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseEnter method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseEnter in a derived class,
        /// be sure to call the base class's OnMouseEnter method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseEnter(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEENTER];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseLeave event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseLeave method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseLeave in a derived class,
        /// be sure to call the base class's OnMouseLeave method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseLeave(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSELEAVE];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the MouseWheel event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnMouseWheel method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnMouseWheel in a derived class,
        /// be sure to call the base class's OnMouseWheel method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnMouseWheel(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_MOUSEWHEEL];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the DragEnter event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnDragEnter method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnDragEnter in a derived class,
        /// be sure to call the base class's OnDragEnter method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnDragEnter(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_DRAGENTER];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the DragLeave event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnDragLeave method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnDragLeave in a derived class,
        /// be sure to call the base class's OnDragLeave method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnDragLeave(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_DRAGLEAVE];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the DragOver event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnDragOver method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnDragOver in a derived class,
        /// be sure to call the base class's OnDragOver method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnDragOver(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_DRAGOVER];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the DragDrop event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnDragDrop method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnDragDrop in a derived class,
        /// be sure to call the base class's OnDragDrop method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnDragDrop(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_DRAGDROP];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the GotFocus event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnGotFocus method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnGotFocus in a derived class,
        /// be sure to call the base class's OnGotFocus method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnGotFocus(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_GOTFOCUS];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raises the LostFocus event.
        /// </summary>
        /// <param name="e">A PInputEventArgs that contains the event data</param>
        /// <remarks>
        /// Raising an event invokes the event handler through a delegate.
        /// <para>
        /// The OnLostFocus method also allows derived classes to handle the event
        /// without attaching a delegate. This is the preferred technique for handling
        /// the event in a derived class.
        /// </para>
        /// <para>
        /// <b>Notes to Inheritors:</b>  When overriding OnLostFocus in a derived class,
        /// be sure to call the base class's OnLostFocus method so that registered
        /// delegates receive the event.
        /// </para>
        /// </remarks>
        public virtual void OnLostFocus(PInputEventArgs e)
        {
            PInputEventHandler handler = (PInputEventHandler)handlers[EVENT_KEY_LOSTFOCUS];
            HandleEvent(e, handler);
        }

        /// <summary>
        /// Raise the given input event.
        /// </summary>
        /// <param name="e">The arguments for this input event.</param>
        /// <param name="handler">The delegate to dispatch this event to.</param>
        /// <remarks>
        /// If an event has been set as handled, and the delegate is not a member of a listener
        /// class, then the event is consumed.  If the delegate is a member of a listener class
        /// the decision of consumption is left up to the filter associated with that class.
        /// </remarks>
        protected virtual void HandleEvent(PInputEventArgs e, PInputEventHandler handler)
        {
            if (handler != null)
            {
                Delegate[] list = handler.GetInvocationList();

                for (int i = list.Length - 1; i >= 0; i--)
                {
                    Delegate each = list[i];

                    if (each.Target is PInputEventListener listener)
                    {
                        if (listener.DoesAcceptEvent(e))
                        {
                            // The source is the node from which the event originated, not the
                            // picked node.
                            ((PInputEventHandler)each)(this, e);
                        }
                    }
                    else if (!e.Handled)
                    {
                        // The source is the node from which the event originated, not the
                        // picked node.
                        ((PInputEventHandler)each)(this, e);
                    }
                }
            }
        }

        /// <summary>
        /// Raise the given property change event.
        /// </summary>
        /// <param name="propertyKey">The key associated with the property that changed.</param>
        /// <param name="propertyCode">The code that identifies the property that changed.</param>
        /// <param name="oldValue">The old value of the property.</param>
        /// <param name="newValue">The new value of the property.</param>
        protected virtual void FirePropertyChangedEvent(object propertyKey, int propertyCode, object oldValue, object newValue)
        {
            PPropertyEventArgs e = null;

            // Fire the event to handlers registered for this particular property change event.
            PPropertyEventHandler h = GetPropertyHandlers(propertyKey);
            if (h != null)
            {
                e = new PPropertyEventArgs(oldValue, newValue);
                h(this, e);
            }

            if (parent != null && (propertyCode & PropertyChangeParentMask) != 0)
            {
                if (e == null) e = new PPropertyEventArgs(oldValue, newValue);
                parent.FireChildPropertyChangedEvent(e, propertyKey, propertyCode);
            }
        }

        /// <summary>
        /// Called by child node to forward property change events up the node tree so that
        /// property change listeners registered with this node will be notified of property
        /// changes of its children nodes.
        /// </summary>
        /// <remarks>
        /// For performance reason only propertyCodes listed in the propertyChangeParentMask
        /// are forwarded.
        /// </remarks>
        /// <param name="e">The property change event to forward.</param>
        /// <param name="propertyKey">The key associated with the property that changed.</param>
        /// <param name="propertyCode">The code that identifies the property that changed.</param>
        protected virtual void FireChildPropertyChangedEvent(PPropertyEventArgs e, object propertyKey, int propertyCode)
        {
            PPropertyEventHandler h = GetPropertyHandlers(propertyKey);
            h?.Invoke(this, e);

            if (parent != null && (propertyCode & PropertyChangeParentMask) != 0)
            {
                parent.FireChildPropertyChangedEvent(e, propertyKey, propertyCode);
            }
        }

        /// <summary>
        /// Gets all property event handlers for this node, that are associated with the
        /// specified property key.
        /// </summary>
        /// <param name="propertyKey">The key for which to find associated handlers.</param>
        /// <returns>
        /// All property event handlers associated with <code>propertyKey</code>
        /// </returns>
        protected virtual PPropertyEventHandler GetPropertyHandlers(object propertyKey)
        {
            PPropertyEventHandler h = null;

            if (handlers != null)
            {
                h = (PPropertyEventHandler)handlers[propertyKey];
            }
            return h;
        }
        #endregion

        #region Bounds Geometry
        //****************************************************************
        // Bounds Geometry - Methods for setting and querying the bounds 
        // of this node.
        // 
        // The bounds of a node store the node's position and size in 
        // the nodes local coordinate system. Many node subclasses will need 
        // to override the SetBounds method so that they can update their
        // internal state appropriately. See PPath for an example.
        // 
        // Since the bounds are stored in the local coordinate system
        // they WILL NOT change if the node is scaled, translated, or rotated.
        // 
        // The bounds may be accessed with the Bounds property.  If a node is
        // marked as volatile then it may modify its bounds before returning
        // from the get accessor of the Bounds property, otherwise it may not.
        //****************************************************************

        /// <summary>
        /// Gets or sets this node's bounds in local coordinates.
        /// </summary>
        /// <value>The bounds of this node.</value>
        /// <remarks>
        /// These bounds are stored in the local coordinate system of this node
        /// and do not include the bounds of any of this node's children.
        /// </remarks>
        public virtual RectangleF Bounds
        {
            get => bounds;
            set => SetBounds(value.X, value.Y, value.Width, value.Height);
        }

        /// <summary>
        /// Set the bounds of this node to the given values.
        /// </summary>
        /// <param name="x">The new x coordinate of the bounds.</param>
        /// <param name="y">The new y coordinate of the bounds.</param>
        /// <param name="width">The new width of the bounds.</param>
        /// <param name="height">The new height of the bounds.</param>
        /// <returns>True if the bounds have changed; otherwise, false.</returns>
        /// <remarks>
        /// These bounds are stored in the local coordinate system of this node.
        /// </remarks>
        public virtual bool SetBounds(float x, float y, float width, float height)
        {
            if (bounds.X != x || bounds.Y != y || bounds.Width != width || bounds.Height != height)
            {
                bounds.X = x;
                bounds.Y = y;
                bounds.Width = width;
                bounds.Height = height;

                InternalUpdateBounds(x, y, width, height);
                InvalidatePaint();
                SignalBoundsChanged();

				// Don't put any invalidating code here or else nodes with volatile bounds will
				// create a soft infinite loop (calling Control.BeginInvoke()) when they validate
				// their bounds.
				return true;
			}
			return false;
        }

        /// <summary>
        /// Gives nodes a chance to update their internal structure before bounds changed
        /// notifications are sent. When this message is received, the node's bounds field
        /// will contain the new value.
        /// </summary>
        /// <param name="x">The new x coordinate of the bounds.</param>
        /// <param name="y">The new y coordinate of the bounds.</param>
        /// <param name="width">The new width of the bounds.</param>
        /// <param name="height">The new height of the bounds.</param>
        protected virtual void InternalUpdateBounds(float x, float y, float width, float height)
        {
        }

        /// <summary>
        /// Notify this node that you will begin to repeatedly call <c>SetBounds</c>.
        /// </summary>
        /// <remarks>
        /// After a call to <c>StartResizeBounds</c>, <c>EndResizeBounds</c> should eventually
        /// be called to notify the node of the end of the sequence of <c>SetBounds</c> calls.
        /// </remarks>
        public virtual void StartResizeBounds()
        {
        }

        /// <summary>
        /// Notify this node that you have finished a resize bounds sequence.
        /// </summary>
        /// <remarks>
        /// A call to <c>StartResizeBounds</c> should precede a call to <c>EndResizeBounds</c>.
        /// </remarks>
        public virtual void EndResizeBounds()
        {
        }

        /// <summary>
        /// Set the bounds of this node back to an empty rectangle.
        /// </summary>
        public virtual void ResetBounds()
        {
            SetBounds(0, 0, 0, 0);
        }

        /// <summary>
        /// Gets the x position (in local coordinates) of this node's bounds.
        /// </summary>
        /// <value>The x position of this node's bounds.</value>
        public virtual float X
        {
            get => Bounds.X;
            set => SetBounds(value, Y, Width, Height);
        }

        /// <summary>
        /// Gets the y position (in local coordinates) of this node's bounds.
        /// </summary>
        /// <value>The y position of this node's bounds.</value>
        public virtual float Y
        {
            get => Bounds.Y;
            set => SetBounds(X, value, Width, Height);
        }

        /// <summary>
        /// Gets the width (in local coordinates) of this node's bounds.
        /// </summary>
        /// <value>The width of this node's bounds.</value>
        public virtual float Width
        {
            get => Bounds.Width;
            set => SetBounds(X, Y, value, Height);
        }

		/// <summary>
		/// Gets the height (in local coordinates) of this node's bounds.
		/// </summary>
		/// <value>The height of this node's bounds.</value>
		public virtual float Height {
			get => Bounds.Height;
            set => SetBounds(X, Y, Width, value);
        }

        /// <summary>
        /// Gets the width (in global coordinates) of the union of this node and all of its descendants' bounds.
        /// </summary>
        /// <value>The width of this node's bounds.</value>
        public virtual float GlobalFullWidth => GlobalFullBounds.Width;

        /// <summary>
        /// Gets the height (in global coordinates) of the union of this node and all of its descendants' bounds.
        /// </summary>
        /// <value>The height of this node's bounds.</value>
        public virtual float GlobalFullHeight => GlobalFullBounds.Height;

        /// <summary>
        /// Gets a copy of the bounds of this node in the global coordinate system.
        /// </summary>
        /// <value>The bounds in the global coordinate system.</value>
        public RectangleF GlobalBounds => LocalToGlobal(Bounds);

        /// <summary>
        /// Adjust the bounds of this node so that they are centered on the given point
        /// specified in the local coordinate system of this node.
        /// </summary>
        /// <param name="x">
        /// The x coordinate of the point on which to center the bounds, in local coordinates.
        /// </param>
        /// <param name="y">
        /// The y coordinate of the point on which to center the bounds, in local coordinates.
        /// </param>
        /// <returns>True if the bounds changed.</returns>
        /// <remarks>Note that this method will modify the node's bounds, while CenterFullBoundsOnPoint
        /// will modify the node's matrix.
        /// </remarks>
        public virtual bool CenterBoundsOnPoint(float x, float y)
        {
            PointF center = PUtil.CenterOfRectangle(bounds);
            float dx = x - center.X;
            float dy = y - center.Y;
            return SetBounds(bounds.X + dx, bounds.Y + dy, bounds.Width, bounds.Height);
        }

        /// <summary>
        /// Adjust the full bounds of this node so that they are centered on the given point
        /// specified in the local coordinates of this node's parent.
        /// </summary>
        /// <param name="x">
        /// The x coordinate of the point on which to center the bounds, in parent coordinates.
        /// </param>
        /// <param name="y">
        /// The y coordinate of the point on which to center the bounds, in parent coordinates.
        /// </param>
        /// <remarks>
        /// Note that this meathod will modify the node's matrix, while CenterBoundsOnPoint
        ///	will modify the node's bounds.
        /// </remarks>
        public virtual void CenterFullBoundsOnPoint(float x, float y)
        {
            PointF center = PUtil.CenterOfRectangle(FullBounds);
            float dx = x - center.X;
            float dy = y - center.Y;
            OffsetBy(dx, dy);
        }

        /// <summary>
        /// Return true if this node intersects the given rectangle specified in local bounds.
        /// </summary>
        /// <param name="bounds">The bounds to test for intersection.</param>
        /// <returns>true if the given rectangle intersects this node's geometry.</returns>
        /// <remarks>
        /// If the geometry of this node is complex, this method can become expensive.  It
        /// is therefore recommended that <c>FullIntersects</c> is used for quick rejects
        /// before calling this method.
        /// </remarks>
        public virtual bool Intersects(RectangleF bounds)
        {
            return Bounds.IntersectsWith(bounds);
        }
        #endregion

        #region Full Bounds Geometry
        //****************************************************************
        // Full Bounds - Methods for computing and querying the 
        // full bounds of this node.
        // 
        // The full bounds of a node store the nodes bounds 
        // together with the union of the bounds of all the 
        // node's descendents. The full bounds are stored in the parent
        // coordinate system of this node, the full bounds DOES change 
        // when you translate, scale, or rotate this node.
        //****************************************************************

        /// <summary>
        /// Gets this node's full bounds in the parent coordinate system of this node.
        /// </summary>
        /// <value>This full bounds of this node.</value>
        /// <remarks>
        /// These bounds are stored in the parent coordinate system of this node and they
        /// include the union of this node's bounds and all the bounds of it's descendents.
        /// </remarks>
        public virtual RectangleF FullBounds
        {
            get
            {
                ValidateFullBounds();
                return fullBoundsCache;
            }
        }

        /// <summary>
        /// Compute and return the full bounds of this node.
        /// </summary>
        /// <returns>The full bounds in the parent coordinate system of this node.</returns>
        public virtual RectangleF ComputeFullBounds()
        {
            RectangleF result = UnionOfChildrenBounds;
            if (result != RectangleF.Empty)
            {
                if (Bounds != Rectangle.Empty)
                {
                    result = RectangleF.Union(result, Bounds);
                }
            }
            else
            {
                result = Bounds;
            }

            if (result != RectangleF.Empty)
            {
                result = LocalToParent(result);
            }

            return result;
        }

        /// <summary>
        /// Gets the union of the full bounds of all the children of this node.
        /// </summary>
        /// <value>The union of the full bounds of the children of this node.</value>
        public virtual RectangleF UnionOfChildrenBounds
        {
            get
            {
                RectangleF result = RectangleF.Empty;
                foreach (PNode each in this)
                {
                    if (each != this)
                    {
                        RectangleF eachBounds = each.FullBounds;
                        if (result != RectangleF.Empty)
                        {
                            if (eachBounds != RectangleF.Empty)
                            {
                                result = RectangleF.Union(result, each.FullBounds);
                            }
                        }
                        else
                        {
                            result = eachBounds;
                        }
                    }
                }
                return result;
            }
        }

        /// <summary>
        /// Gets the full bounds of this node in the global coordinate system.
        /// </summary>
        /// <value>The full bounds in the global coordinate system.</value>
        public virtual RectangleF GlobalFullBounds
        {
            get
            {
                if (Parent != null)
                {
                    return Parent.LocalToGlobal(FullBounds);
                }
                return FullBounds;
            }
        }

        /// <summary>
        /// Return true if the full bounds of this node intersect with the specified bounds.
        /// </summary>
        /// <param name="bounds">
        /// The bounds to test for intersection (specified in the parent's coordinate system).
        /// </param>
        /// <returns>True if this node's full bounds intersect the given bounds.</returns>
        public virtual bool FullIntersects(RectangleF bounds)
        {
            return FullBounds.IntersectsWith(bounds);
        }
        #endregion

        #region Bounds Damage Managment
        //****************************************************************
        // Bounds Damage Management - Methods used to invalidate and validate
        // the bounds of nodes.
        //****************************************************************

        /// <summary>
        /// Gets a value indicating whether this nodes bounds may change at any time.
        /// </summary>
        /// <value>The property is true if this node has volatile bounds; otherwise, false.</value>
        /// <remarks>
        /// <b>Notes to Inheritors:</b>  The default behavior is to return false.  Subclasses that
        /// override this method to return true, should also override the get accessor of the Bounds
        /// property and compute the volatile bounds there before returning.
        /// </remarks>
        protected virtual bool BoundsVolatile => false;

        /// <summary>
        /// Gets or sets a value indicating whether this has a child with volatile bounds.
        /// </summary>
        /// <value>The property is true if this node has a child with volatile bounds; otherwise, false.</value>
        protected virtual bool ChildBoundsVolatile
        {
            get => childBoundsVolatile;
            set => childBoundsVolatile = value;
        }

        /// <summary>
        /// Gets or sets a value indicating whether this node's bounds have recently changed.
        /// </summary>
        /// <value>The property is true if this node has a child with volatile bounds; otherwise, false.</value>
        /// <remarks>This flag will be reset on the next call of <c>ValidateFullBounds</c>.</remarks>
        protected virtual bool BoundsModified { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the full bounds of this node are valid.
        /// </summary>
        /// <value>The property is true if the full bounds of this node are invalid; otherwise, false.</value>
        /// <remarks>
        /// If this property is true, the full bounds of this node have changed and need to be
        /// recomputed.
        /// </remarks>
        protected virtual bool FullBoundsInvalid
        {
            get => fullBoundsInvalid;
            set => fullBoundsInvalid = value;
        }

        /// <summary>
        /// Gets or sets a value indicating if one of this node's descendents has invalid bounds.
        /// </summary>
        /// <value>The property is true if the bounds of of this node's descendents are invalid; otherwise, false.</value>
        protected virtual bool ChildBoundsInvalid { get; set; }

        /// <summary>
        /// This method should be called when the bounds of this node are changed.
        /// </summary>
        /// <remarks>
        /// This method invalidates the full bounds of this node, and also notifies each of 
        /// this node's children that their parent's bounds have changed. As a result 
        /// of this method getting called this node's <c>LayoutChildren</c> will be called.
        /// </remarks>
        public virtual void SignalBoundsChanged()
        {
            InvalidateFullBounds();
            BoundsModified = true;
            FirePropertyChangedEvent(PROPERTY_KEY_BOUNDS, PROPERTY_CODE_BOUNDS, null, bounds);

            int count = ChildrenCount;
            for (int i = 0; i < count; i++)
            {
                children[i].ParentBoundsChanged();
            }
        }

        /// <summary>
        /// Invalidate this node's layout, so that later <c>LayoutChildren</c> will get
        /// called.
        /// </summary>
        public virtual void InvalidateLayout()
        {
            InvalidateFullBounds();
        }

        /// <summary>
        /// A notification that the bounds of this node's parent have changed.
        /// </summary>
        public virtual void ParentBoundsChanged()
        {
        }

        /// <summary>
        /// Invalidates the full bounds of this node, and sets the child bounds invalid flag
        /// on each of this node's ancestors.
        /// </summary>
        public virtual void InvalidateFullBounds()
        {
            FullBoundsInvalid = true;

            PNode n = parent;
            while (n is { ChildBoundsInvalid: false })
            {
                n.ChildBoundsInvalid = true;
                n = n.Parent;
            }

            OnFullBoundsInvalidated();
        }

        /// <summary>
        /// Raises the <see cref="FullBoundsInvalidated">FullBoundsInvalidated</see> event.
        /// </summary>
        protected virtual void OnFullBoundsInvalidated()
        {
            FullBoundsInvalidated?.Invoke(this);
        }

        /// <summary>
        /// This method is called to validate the bounds of this node and all of its 
        /// descendents.
        /// </summary>
        /// <returns>True if this node or any of its descendents have volatile bounds.</returns>
        /// <remarks>
        /// This method returns true if this node's bounds or the bounds of any of its
        /// descendents are marked as volatile.
        /// </remarks>
        protected virtual bool ValidateFullBounds()
        {
            // 1. Only compute new bounds if invalid flags are set.
            if (FullBoundsInvalid || ChildBoundsInvalid || BoundsVolatile || ChildBoundsVolatile)
            {
                // 2. If my bounds are volatile and they have not been changed then signal a change. 
                // For most cases this will do nothing, but if a node's bounds depend on its model,
                // then validate bounds has the responsibility of making the bounds match the model's
                // value. For example PPaths ValidateBounds method makes sure that the bounds are
                // equal to the bounds of the GraphicsPath model.
                if (BoundsVolatile && !BoundsModified)
                {
                    SignalBoundsChanged();
                }

                // 3. If the bounds of one of my decendents are invalid then validate the bounds of all
                // of my children.
                if (ChildBoundsInvalid || ChildBoundsVolatile)
                {
                    ChildBoundsVolatile = false;
                    int count = ChildrenCount;
                    for (int i = 0; i < count; i++)
                    {
                        ChildBoundsVolatile |= children[i].ValidateFullBounds();
                    }
                }

                // 4. Now that my children’s bounds are valid and my own bounds are valid run any
                // layout algorithm here. Note that if you try to layout volatile children piccolo
                // will most likely start a "soft" infinite loop. It won't freeze your program, but
                // it will make an infinite number of calls to BeginInvoke later. You don't
                // want to do that.
                LayoutChildren();

                // 5. If the full bounds cache is invalid then recompute the full bounds cache
                // here after our own bounds and the children’s bounds have been computed above.
                if (FullBoundsInvalid)
                {
                    RectangleF oldRect = fullBoundsCache;
                    bool oldEmpty = fullBoundsCache.IsEmpty;

                    // 6. This will call FullBounds on all of the children. So if the above
                    // layoutChildren method changed the bounds of any of the children they will be
                    // validated again here.
                    fullBoundsCache = ComputeFullBounds();

                    bool fullBoundsChanged =
                        !Equals(oldRect, fullBoundsCache) ||
                        oldEmpty != fullBoundsCache.IsEmpty;

                    // 7. If the new full bounds cache differs from the previous cache then
                    // tell our parent to invalidate their full bounds. This is how bounds changes
                    // deep in the tree percolate up.
                    if (fullBoundsChanged)
                    {
                        parent?.InvalidateFullBounds();
                        FirePropertyChangedEvent(PROPERTY_KEY_FULLBOUNDS, PROPERTY_CODE_FULLBOUNDS, null, fullBoundsCache);

                        // 8. If our paint was invalid make sure to repaint our old full bounds. The
                        // new bounds will be computed later in the ValidatePaint pass.
                        if (PaintInvalid && !oldEmpty)
                        {
                            RepaintFrom(oldRect, this);
                        }
                    }
                }

                // 9. Clear the invalid bounds flags.		
                BoundsModified = false;
                FullBoundsInvalid = false;
                ChildBoundsInvalid = false;
            }

            return BoundsVolatile || childBoundsVolatile;
        }

        /// <summary>
        /// Nodes that apply layout constraints to their children should override this method
        /// and do the layout there.
        /// </summary>
        /// <remarks>
        /// This method gets called whenever Piccolo determines that this node's children may
        /// need to be re-layed out.  This occurs when the bounds of this node or a descendent
        /// node change.  This method may also be invoked indirectly when LayoutChildren is
        /// called on an ancestor node, since typically you will modify the bounds of the
        /// children there.
        /// </remarks>
        public virtual void LayoutChildren()
        {
        }
        #endregion

        #region Node Matrix
        //****************************************************************
        // Node Matrix - Methods to manipulate the node's matrix.
        // 
        // Each node has a matrix that is used to define the nodes
        // local coordinate system. IE it is applied before picking and 
        // rendering the node.
        // 
        // The usual way to move nodes about on the canvas is to manipulate
        // this matrix, as opposed to changing the bounds of the 
        // node.
        // 
        // Since this matrix defines the local coordinate system of this
        // node the following methods with affect the global position both 
        // this node and all of its descendents.
        //****************************************************************

        /// <summary>
        /// Gets or sets the rotation applied by this node's transform in degrees.
        /// </summary>
        /// <value>The rotation in degrees</value>
        /// <remarks>
        /// This rotation affects this node and all it's descendents.  The value
        /// returned will be between 0 and 360,
        /// </remarks>
        public virtual float Rotation
        {
            get => matrix.Rotation;
            set => RotateBy(value - Rotation);
        }

        /// <summary>
        /// Rotates this node by theta (in degrees) about the 0,0 point.
        /// </summary>
        /// <param name="theta">The rotation in degrees.</param>
        /// <remarks>This will affect this node and all its descendents.</remarks>
        public virtual void RotateBy(float theta)
        {
            RotateBy(theta, 0, 0);
        }

        /// <summary>
        /// Rotates this node by theta (in degrees), and then translates the node so
        /// that the x, y position of its fullBounds stays constant.
        /// </summary>
        /// <param name="theta">The amount to rotate by in degrees.</param>
        public virtual void RotateInPlace(float theta)
        {
            RectangleF b = FullBounds;
            float px = b.X;
            float py = b.Y;
            RotateBy(theta, 0, 0);
            b = FullBounds;
            OffsetBy(px - b.X, py - b.Y);
        }

        /// <summary>
        /// Rotates this node by theta (in degrees) about the given 
        /// point.
        /// </summary>
        /// <param name="theta">The amount to rotate by in degrees.</param>
        /// <param name="point">The point to rotate about.</param>
        /// <remarks>This will affect this node and all its descendents.</remarks>
        public virtual void RotateBy(float theta, PointF point)
        {
            RotateBy(theta, point.X, point.Y);
        }

        /// <summary>
        /// Rotates this node by theta (in degrees) about the given 
        /// point.
        /// </summary>
        /// <param name="theta">The amount to rotate by in degrees</param>
        /// <param name="x">The x-coordinate of the point to rotate about.</param>
        /// <param name="y">The y-coordinate of the point to rotate about.</param>
        public virtual void RotateBy(float theta, float x, float y)
        {
            MatrixReference.RotateBy(theta, x, y);
            InvalidatePaint();
            InvalidateFullBounds();
            FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
        }

        /// <summary>
        /// Gets or sets a the total amount of rotation applied to this node by its own
        /// matrix together with the matrices of all its ancestors.
        /// </summary>
        /// <value>
        /// A value between 0 and 360 degrees, that represents the total
        /// rotation of this node.
        /// </value>
        public virtual float GlobalRotation
        {
            get => LocalToGlobalMatrix.Rotation;
            set
            {
                if (parent != null)
                {
                    Rotation = value - parent.GlobalRotation;
                }
                else
                {
                    Rotation = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the scale applied by this node's matrix.
        /// </summary>
        /// <value>
        /// A float value that represents the scale applied by this node's matrix.
        /// </value>
        /// <remarks>
        /// The scale affects this node and all its descendents.
        /// </remarks>
        public virtual float Scale
        {
            get => matrix.Scale;
            set
            {
                if (Scale != 0)
                {
                    ScaleBy(value / Scale);
                }
                else
                {
                    throw new InvalidOperationException("Can't set Scale when Scale is equal to 0");
                }
            }
        }

        /// <summary>
        /// Scale this node's matrix by the given amount.
        /// </summary>
        /// <param name="scale">The amount to scale by.</param>
        /// <remarks>This will affect this node and all of its descendents.</remarks>
        public virtual void ScaleBy(float scale)
        {
            ScaleBy(scale, 0, 0);
        }

        /// <summary>
        /// Scale this node's matrix by the given amount about the specified
        ///	point.
        /// </summary>
        /// <param name="scale">The amount to scale by.</param>
        /// <param name="point">The point to scale about.</param>
        /// <remarks>This will affect this node and all of its descendents.</remarks>
        public virtual void ScaleBy(float scale, PointF point)
        {
            ScaleBy(scale, point.X, point.Y);
        }

        /// <summary>
        /// Scale this node's matrix by the given amount about the specified
        ///	point.
        /// </summary>
        /// <param name="scale">The amount to scale by.</param>
        /// <param name="x">The x-coordinate of the point to scale about.</param>
        /// <param name="y">The y-coordinate of the point to scale about.</param>
        public virtual void ScaleBy(float scale, float x, float y)
        {
            matrix.ScaleBy(scale, x, y);
            InvalidatePaint();
            InvalidateFullBounds();
            FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
        }

        /// <summary>
        /// Gets or sets the global scale that is being applied to this node by its matrix
        ///	together with the matrices of all its ancestors.
        /// </summary>
        /// <value>The total scale of this node.</value>
        public virtual float GlobalScale
        {
            get => LocalToGlobalMatrix.Scale;
            set
            {
                if (parent != null)
                {
                    Scale = value / parent.GlobalScale;
                }
                else
                {
                    Scale = value;
                }
            }
        }

        /// <summary>
        /// Gets or sets the offset applied to this node by its matrix.
        /// </summary>
        /// <value>
        /// The offset applied to this node, specified in the parent
        /// coordinate system.
        /// </value>
        /// <remarks>
        /// The offset affects this node and all of its descendents and is specified
        /// in the parent coordinate system.  This property returns the values that
        /// are in the dx and dy positions of the matrix.  Setting this property
        /// directly sets those values.  Unlike <c>PNode.TranslateBy()</c>, this is not
        /// affected by the scale value of the matrix.
        /// </remarks>
        public virtual PointF Offset
        {
            get => new(matrix.OffsetX, matrix.OffsetY);
            set => SetOffset(value.X, value.Y);
        }

        /// <summary>
        /// Gets or sets the x offset applied to this node by its matrix.
        /// </summary>
        /// <value>
        /// The x offset applied to this node, specified in the parent
        /// coordinate system.
        /// </value>
        /// <remarks>
        /// The offset affects this node and all of its descendents and is
        /// specified in the parent coordinate system.  This returns the value
        /// that is in the dx position of the matrix.  Setting this property
        /// directly sets that value.  Unlike <c>PNode.TranslateBy()</c>, this is not
        /// affected by the scale value of the matrix.
        /// </remarks>
        public virtual float OffsetX
        {
            get => matrix.OffsetX;
            set => SetOffset(value, matrix.OffsetY);
        }

        /// <summary>
        /// Gets or sets the y offset applied to this node by its matrix.
        /// </summary>
        /// <value>
        /// The y offset applied to this node, specified in the parent
        /// coordinate system.
        /// </value>
        /// <remarks>
        /// The offset affects this node and all of its descendents and is
        /// specified in the parent coordinate system.  This returns the value
        /// that is in the dx position of the matrix.  Setting this property
        /// directly sets that value.  Unlike <c>PNode.TranslateBy()</c>, this is not
        /// affected by the scale value of the matrix.
        /// </remarks>
        public virtual float OffsetY
        {
            get => matrix.OffsetY;
            set => SetOffset(matrix.OffsetX, value);
        }

        /// <summary>
        /// Offset this node relative to the parent's coordinate system.  This is NOT
        /// affected by this node's current scale or rotation.
        /// </summary>
        /// <param name="dx">The amount to add to the x-offset for this node.</param>
        /// <param name="dy">The amount to add to the y-offset for this node.</param>
        /// <remarks>
        /// This is implemented by directly adding dx to the dx position and dy to
        /// the dy position of the matrix.
        /// </remarks>
        public virtual void OffsetBy(float dx, float dy)
        {
            SetOffset(matrix.OffsetX + dx, matrix.OffsetY + dy);
        }

        /// <summary>
        /// Sets the offset applied to this node by it's matrix.
        /// </summary>
        /// <param name="x">
        /// The x amount of the offset, specified in the parent coordinate system.
        /// </param>
        /// <param name="y">
        /// The y amount of the offset, specified in the parent coordinate system.
        /// </param>
        /// <remarks>
        /// The offset affects this node and all of its descendents and is specified
        /// in the parent coordinate system.  This directly sets the values that are in
        /// the dx and dy positions of the matrix.  Unlike <c>PNode.TranslateBy()</c>,
        /// this is not affected by the scale value of the matrix.
        /// </remarks>
        public virtual void SetOffset(float x, float y)
        {
            matrix.OffsetX = x;
            matrix.OffsetY = y;
            InvalidatePaint();
            InvalidateFullBounds();
            FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
        }

        /// <summary>
        /// Sets the offset applied to this node by it's matrix.
        /// </summary>
        /// <param name="p">
        /// The offset, specified in the parent coordinate system.
        /// </param>
        /// <remarks>
        /// The offset affects this node and all of its descendents and is specified
        /// in the parent coordinate system.  This directly sets the values that are in
        /// the dx and dy positions of the matrix.  Unlike <c>PNode.TranslateBy()</c>,
        /// this is not affected by the scale value of the matrix.
        /// </remarks>
        public virtual void SetOffset(PointF p)
        {
            SetOffset(p.X, p.Y);
        }

        /// <summary>
        /// Translate this node's matrix by the given amount, using the standard matrix
        /// <c>Translate</c> method.
        /// </summary>
        /// <param name="dx">The amount to translate in the x direction.</param>
        /// <param name="dy">The amount to translate in the y direction.</param>
        /// <remarks>This translation affects this node and all of its descendents.</remarks>
        public virtual void TranslateBy(float dx, float dy)
        {
            matrix.TranslateBy(dx, dy);
            InvalidatePaint();
            InvalidateFullBounds();
            FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
        }

        /// <summary>
        /// Gets or sets the global translation that is being applied to this node by its
        /// matrix together with the matrices of all its ancestors.
        /// </summary>
        /// <value>The desired global translation.</value>
        /// <remarks>
        /// Setting this property translates this node's matrix the required amount so that
        /// the node's global translation is as requested.
        /// </remarks>
        public virtual PointF GlobalTranslation
        {
            get
            {
                PointF p = Offset;
                if (parent != null)
                {
                    p = parent.LocalToGlobal(p);
                }
                return p;
            }
            set
            {
                PointF p = value;
                if (parent != null)
                {
                    p = parent.GlobalToLocalMatrix.Transform(p);
                }
                Offset = p;
            }
        }

        /// <summary>
        /// Transform this node's matrix by the given matrix.
        /// </summary>
        /// <param name="matrix">The transform to apply.</param>
        public virtual void TransformBy(PMatrix matrix)
        {
            this.matrix.Multiply(matrix);
            InvalidatePaint();
            InvalidateFullBounds();
            FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
        }

        /// <summary>
        /// Linearly interpolates between a and b, based on t.
        /// Specifically, it computes Lerp(a, b, t) = a + t*(b - a).
        /// This produces a result that changes from a (when t = 0) to b (when t = 1).
        /// </summary>
        /// <param name="t">The variable 'time' parameter.</param>
        /// <param name="a">The starting value.</param>
        /// <param name="b">The ending value.</param>
        /// <returns>A value between a and b determined by t.</returns>
        static public float Lerp(float t, float a, float b)
        {
            return (a + t * (b - a));
        }

        /// <summary>
        /// Animate this node's matrix to one that will make this node appear at the specified
        /// position relative to the specified bounding box.
        /// </summary>
        /// <param name="srcPt"></param>
        /// <param name="destPt"></param>
        /// <param name="destBounds"></param>
        /// <param name="millis"></param>
        /// <remarks>
        /// The source point specifies a point in the unit square (0, 0) - (1, 1) that
        /// represents an anchor point on the corresponding node to this matrox.  The
        /// destination point specifies	an anchor point on the reference node.  The
        /// position method then computes the matrix that results in transforming this
        /// node so that the source anchor point coincides with the reference anchor
        /// point. This can be useful for layout algorithms as it is straightforward to
        /// position one object relative to another.
        /// <para>
        /// For example, If you have two nodes, A and B, and you call
        /// <code>
        ///     PointF srcPt = new PointF(1.0f, 0.0f);
        ///     PointF destPt = new PointF(0.0f, 0.0f);
        ///     A.Position(srcPt, destPt, B.GlobalBounds, 750);
        /// </code>
        /// The result is that A will move so that its upper-right corner is at
        /// the same place as the upper-left corner of B, and the transition will
        /// be smoothly animated over a period of 750 milliseconds.
        /// </para>
        /// </remarks>
        public virtual void Position(PointF srcPt, PointF destPt, RectangleF destBounds, int millis)
        {
            if (parent != null)
            {
                // First compute translation amount in global coordinates
                RectangleF srcBounds = GlobalFullBounds;
                float srcx = Lerp(srcPt.X, srcBounds.X, srcBounds.X + srcBounds.Width);
                float srcy = Lerp(srcPt.Y, srcBounds.Y, srcBounds.Y + srcBounds.Height);
                float destx = Lerp(destPt.X, destBounds.X, destBounds.X + destBounds.Width);
                float desty = Lerp(destPt.Y, destBounds.Y, destBounds.Y + destBounds.Height);

                // Convert vector to local coordinates
                var pt1 = new PointF(srcx, srcy);
                pt1 = GlobalToLocal(pt1);
                var pt2 = new PointF(destx, desty);
                pt2 = GlobalToLocal(pt2);
                float dx = (pt2.X - pt1.X);
                float dy = (pt2.Y - pt1.Y);

                // Finally, animate change
                var matrix = new PMatrix(MatrixReference.Matrix);
                matrix.TranslateBy(dx, dy);
                AnimateToMatrix(matrix, millis);
            }
        }

        /// <summary>
        /// Gets or sets the matrix associated with this node.
        /// </summary>
        /// <value>The matrix associated with this node.</value>
        /// <remarks>This property returns a copy of the node's matrix.</remarks>
        public virtual PMatrix Matrix
        {
            get => (PMatrix)matrix.Clone();
            set
            {
                matrix.Reset();
                matrix.Multiply(value);
                InvalidatePaint();
                InvalidateFullBounds();
                FirePropertyChangedEvent(PROPERTY_KEY_TRANSFORM, PROPERTY_CODE_TRANSFORM, null, matrix);
            }
        }

        /// <summary>
        /// Gets a reference to the matrix associated with this node.
        /// </summary>
        /// <value>A reference to this node's matrix.</value>
        /// <remarks>
        /// The returned matrix should not be modified.
        /// </remarks>
        public virtual PMatrix MatrixReference => matrix;

        /// <summary>
        /// Gets an inverted copy of the matrix associated with this node.
        /// </summary>
        /// <value>An inverted copy of this node's matrix.</value>
        public virtual PMatrix InverseMatrix
        {
            get
            {
                PMatrix result = (PMatrix)matrix.Clone();
                result.Invert();
                return result;
            }
        }
        #endregion

        #region Paint Damage Managment
        //****************************************************************
        // Paint Damage Management - Methods used to invalidate the areas of 
        // the screen that this node appears in so that they will later get 
        // painted.
        // 
        // Generally you will not need to call these invalidate methods 
        // when starting out with Piccolo because methods such as setPaint
        // already automatically call them for you. You will need to call
        // them when you start creating your own nodes.
        // 
        // When you do create you own nodes the only method that you will
        // normally need to call is InvalidatePaint. This method marks the 
        // nodes as having invalid paint, the root node's UI cycle will then 
        // later discover this damage and report it to the Windows repaint manager.
        // 
        // Repainting is normally done with PNode.InvalidatePaint() instead of 
        // directly calling PNode.Repaint() because PNode.Repaint() requires 
        // the nodes bounds to be computed right away. But with invalidatePaint 
        // the bounds computation can be delayed until the end of the root's UI
        // cycle, and this can add up to a bit savings when modifying a
        // large number of nodes all at once.
        // 
        // The other methods here will rarely be called except internally
        // from the framework.
        //****************************************************************

        /// <summary>
        /// Gets or sets a value indicating whether this node's paint is invalid,
        /// in which case the node needs to be repainted.
        /// </summary>
        /// <value>True if this node needs to be repainted; else false;</value>
        /// <remarks>
        /// If this property is set to true, the node will later be repainted.
        /// Note, this property is most often set internally.
        /// </remarks>
        public virtual bool PaintInvalid { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this node has a child with
        /// invalid paint.
        /// </summary>
        /// <value>True if this node has a child with invalid paint, else false.</value>
        public virtual bool ChildPaintInvalid { get; set; }

        /// <summary>
        /// Invalidate this node's paint, and mark all of its ancestors as having
        /// a node with invalid paint.
        /// </summary>
        public virtual void InvalidatePaint()
        {
            PaintInvalid = true;

            PNode n = parent;
            while (n is { ChildPaintInvalid: false })
            {
                n.ChildPaintInvalid = true;
                n = n.Parent;
            }

            OnPaintInvalidated();
        }

        /// <summary>
        /// Raises the <see cref="PaintInvalidated">PaintInvalidated</see> event.
        /// </summary>
        protected virtual void OnPaintInvalidated()
        {
            PaintInvalidated?.Invoke(this);
        }

        /// <summary>
        /// Repaint this node and any of its descendents if they have invalid paint.
        /// </summary>
        public virtual void ValidateFullPaint()
        {
            if (PaintInvalid)
            {
                Repaint();
                PaintInvalid = false;
            }

            if (ChildPaintInvalid)
            {
                int count = ChildrenCount;
                for (int i = 0; i < count; i++)
                {
                    children[i].ValidateFullPaint();
                }
                ChildPaintInvalid = false;
            }
        }

        /// <summary>
        /// Mark the area on the screen represented by this node's full bounds 
        /// as needing a repaint.
        /// </summary>
        public virtual void Repaint()
        {
            RepaintFrom(FullBounds, this);
        }

        /// <summary>
        /// Pass the given repaint request up the tree, so that any cameras
        ///	can invalidate that region on their associated canvas.
        /// </summary>
        /// <param name="bounds">
        /// The bounds to repaint, specified in the local coordinate system.
        /// </param>
        /// <param name="childOrThis">
        /// If childOrThis does not equal this then this node's matrix will
        /// be applied to the bounds paramater.
        /// </param>
        public virtual void RepaintFrom(RectangleF bounds, PNode childOrThis)
        {
            if (parent != null)
            {
                if (childOrThis != this)
                {
                    bounds = LocalToParent(bounds);
                }
                else if (!Visible)
                {
                    return;
                }
                parent.RepaintFrom(bounds, this);
            }
        }
        #endregion

        #region Occlusion
        //****************************************************************
        // Occluding - Methods to support occluding optimization. Not yet
        // complete.
        //***************************************************************

        /// <summary>
        /// 
        /// </summary>
        /// <param name="boundary"></param>
        /// <returns></returns>
        public virtual bool IsOpaque(RectangleF boundary)
        {
            return false;
        }

        /// <summary>
        /// Returns true if this node is occluded by another node, in which case it
        /// should not be drawn.
        /// </summary>
        /// <value></value>
        /// <remarks>
        /// If Occluded returns true, then <c>FullPaint</c> won't call <c>Paint</c> 
        /// and so this node will not be drawn.  Note, it's children may be drawn
        /// though.
        /// </remarks>
        public bool Occluded { get; set; }

        #endregion

        #region Painting
        //****************************************************************
        // Painting - Methods for painting this node and its children
        // 
        // Painting is how a node defines its visual representation on the
        // screen, and is done in the local coordinate system of the node.
        // 
        // The default painting behavior is to first paint the node, and 
        // then paint the node's children on top of the node. If a node
        // needs wants specialized painting behavior it can override:
        // 
        // Paint() - Painting here will happen before the children
        // are painted, so the children will be painted on top of painting done
        // here.
        //
        // PaintAfterChildren() - Painting here will happen after the children
        // are painted, so it will paint on top of them.
        // 
        // Note that you should not normally need to override FullPaint().
        // 
        // The visible flag can be used to make a node invisible so that 
        // it will never get painted.
        //****************************************************************

        /// <summary>
        /// Gets or sets a value indicating whether this node is visible, that is
        /// if it will paint itself and its descendents.
        /// </summary>
        /// <value>True if this node and it's descendents are visible; else false.</value>
        /// <remarks>
        /// Setting <code>Visible</code> to false will not affect whether a node is pickable.
        /// That is, invisible nodes will still receive events.  To disable a node from receiving
        /// events, set the <see cref="Pickable">Pickable</see> property explicitly.
        /// </remarks>
        public virtual bool Visible
        {
            get => visible;
            set
            {
                if (Visible != value)
                {
                    if (!value)
                    {
                        Repaint();
                    }
                    visible = value;
                    FirePropertyChangedEvent(PROPERTY_KEY_VISIBLE, PROPERTY_CODE_VISIBLE, null, null);
                    InvalidatePaint();
                }
            }
        }

        /// <summary>
        /// Gets or sets the Brush used to paint this node.
        /// </summary>
        /// <value>The Brush used to paint this node.</value>
        /// <remarks>
        /// This property may be set to null, in which case paint will still be called,
        /// but the node will not be filled.
        /// </remarks>
        public virtual Brush Brush
        {
            get => brush;
            set
            {
                if (Brush != value)
                {
                    Brush oldBrush = brush;
                    brush = value;
                    InvalidatePaint();
                    FirePropertyChangedEvent(PROPERTY_KEY_BRUSH, PROPERTY_CODE_BRUSH, oldBrush, brush);
                }
            }
        }

        /// <summary>
        /// Paint this node behind any of its children nodes.
        /// </summary>
        /// <param name="paintContext">The paint context to use for painting this node.</param>
        /// <remarks>
        /// <b>Notes to Inheritors:</b>  Subclasses that define a different appearance should
        /// override this method and paint themselves there.
        /// </remarks>
        protected virtual void Paint(PPaintContext paintContext)
        {
            if (Brush != null)
            {
                Graphics g = paintContext.Graphics;
                g.FillRectangle(Brush, Bounds);
            }
        }

        /// <summary>
        /// Paint this node and all of its descendents.
        /// </summary>
        /// <param name="paintContext">The paint context to use for painting this node.</param>
        /// <remarks>
        /// <b>Notes to Inheritors:</b>  Most subclasses do not need to override this method,
        /// they should override <c>paint</c> or <c>paintAfterChildren</c> instead.
        /// </remarks>
        public virtual void FullPaint(PPaintContext paintContext)
        {
            if (Visible && FullIntersects(paintContext.LocalClip))
            {
                paintContext.PushMatrix(matrix);
                //paintContext.PushTransparency(transparency);

                if (!Occluded)
                {
                    Paint(paintContext);
                }

                int count = ChildrenCount;
                for (int i = 0; i < count; i++)
                {
                    children[i].FullPaint(paintContext);
                }

                PaintAfterChildren(paintContext);

                //paintContext.popTransparency(transparency);
                paintContext.PopMatrix();
            }
        }

        /// <summary>
        /// Subclasses that wish to do additional painting after their children
        /// are painted should override this method and do that painting here.
        /// </summary>
        /// <param name="paintContext">
        /// The paint context to use for painting after the children are painted.
        /// </param>
        protected virtual void PaintAfterChildren(PPaintContext paintContext)
        {
        }

        /// <summary>
        /// Return a new Image representing this node and all of its children.
        /// </summary>
        /// <returns>A new Image representing this node and its descendents.</returns>
        /// <remarks>
        /// The image size will be equal to the size of this node's full bounds.
        /// </remarks>
        public virtual Image ToImage()
        {
            RectangleF b = FullBounds;
            return ToImage((int)Math.Ceiling(b.Width), (int)Math.Ceiling(b.Height), null);
        }

        /// <summary>
        /// Return a new Image of the requested size representing this node and all of
        /// its children.
        /// </summary>
        /// <param name="width">The desired width of the image, in pixels.</param>
        /// <param name="height">The desired height of the image, in pixels.</param>
        /// <param name="backgroundBrush">
        /// The brush used to render the background of the image.
        /// </param>
        /// <returns>A new Image representing this node and its descendents.</returns>
        /// <remarks>
        /// If backgroundBrush is null, then the image will not be filled with a color
        /// prior to rendering.
        /// </remarks>
        public virtual Image ToImage(int width, int height, Brush backgroundBrush)
        {
            RectangleF bnds = FullBounds;

            if (width / bnds.Width < height / bnds.Height)
            {
                float scale = width / bnds.Width;
                height = (int)(bnds.Height * scale);
            }
            else
            {
                float scale = height / bnds.Height;
                width = (int)(bnds.Width * scale);
            }

            Image image = new Bitmap(width, height);
            return ToImage(image, backgroundBrush);
        }

        /// <summary>
        /// Paint a representation of this node into the specified image.  If backgroundBrush
        /// is null, then the image will not be filled with a color prior to rendering.
        /// </summary>
        /// <param name="image">The image to render this node into.</param>
        /// <param name="backgroundBrush">
        /// The brush used to render the background of the image.
        /// </param>
        /// <returns>
        /// A rendering of this node and its descendents into the specified image.
        /// </returns>
        public virtual Image ToImage(Image image, Brush backgroundBrush)
        {
            int width = image.Width;
            int height = image.Height;
            Graphics g = Graphics.FromImage(image);

            if (backgroundBrush != null)
            {
                g.FillRectangle(backgroundBrush, 0, 0, width, height);
            }
            ScaleAndDraw(g, new RectangleF(0, 0, width, height));

            g.Dispose();
            return image;
        }

        /// <summary>
        /// Scale the Graphics so that this node's full bounds fit in displayRect and then
        /// render into the given Graphics context.
        /// </summary>
        /// <param name="g">The Graphics context to use when rendering the node.</param>
        /// <param name="displayRect">The imageable area.</param>
        protected virtual void ScaleAndDraw(Graphics g, RectangleF displayRect)
        {
            RectangleF bounds = FullBounds;
            g.TranslateTransform(displayRect.X, displayRect.Y);

            // scale the graphics so node's full bounds fit in the imageable bounds.
            float scale = displayRect.Width / bounds.Width;
            if (displayRect.Height / bounds.Height < scale)
            {
                scale = displayRect.Height / bounds.Height;
            }

            g.ScaleTransform(scale, scale);
            g.TranslateTransform(-bounds.X, -bounds.Y);

            var pc = new PPaintContext(g, null)
            {
                RenderQuality = RenderQuality.HighQuality
            };
            FullPaint(pc);
        }
        #endregion

        #region Picking
        //****************************************************************
        // Picking - Methods for picking this node and its children.
        // 
        // Picking is used to determine the node that intersects a point or 
        // rectangle on the screen. It is most frequently used by the 
        // PInputManager to determine the node that the cursor is over.
        // 
        // The Intersects() method is used to determine if a node has
        // been picked or not. The default implementation just tests to see
        // if the pick bounds intersects the bounds of the node. Subclasses
        // whose geometry (a circle for example) does not match up exactly with
        // the bounds should override the Intersects() method.
        // 
        // The default picking behavior is to first try to pick the node's 
        // children, and then try to pick the nodes own bounds. If a node
        // wants specialized picking behavior it can override:
        // 
        // Pick() - Pick nodes here that should be picked before the nodes
        // children are picked.
        //
        // PickAfterChildren() - Pick nodes here that should be picked after the
        // node’s children are picked.
        // 
        // Note that FullPick should not normally be overridden.
        // 
        // The pickable and childrenPickable flags can be used to make a
        // node or it children not pickable even if their geometry does 
        // intersect the pick bounds.
        //****************************************************************

        /// <summary>
        /// Gets or sets a value indicating whether this node is pickable
        /// </summary>
        /// <value>True if this node is pickable; else false.</value>
        /// <remarks>
        /// Only pickable nodes can receive input events. Nodes are pickable by default.
        /// </remarks>
        public virtual bool Pickable
        {
            get => pickable;
            set
            {
                if (Pickable != value)
                {
                    pickable = value;
                    FirePropertyChangedEvent(PROPERTY_KEY_PICKABLE, PROPERTY_CODE_PICKABLE, null, null);
                }
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether the children of his node should be picked.
        /// </summary>
        /// <value>True if this node tries to pick it's children; else false.</value>
        /// <remarks>
        /// If this flag is false then this node will not try to pick its children. Children
        ///	are pickable by default.
        /// </remarks>
        public virtual bool ChildrenPickable
        {
            get => childrenPickable;
            set
            {
                if (ChildrenPickable != value)
                {
                    childrenPickable = value;
                    FirePropertyChangedEvent(PROPERTY_KEY_CHILDRENPICKABLE, PROPERTY_CODE_CHILDRENPICKABLE, null, null);
                }
            }
        }

        /// <summary>
        /// Try to pick this node before its children have had a chance to be picked.
        /// </summary>
        /// <param name="pickPath">The pick path used for the pick operation.</param>
        /// <returns>True if this node was picked; else false.</returns>
        /// <remarks>
        /// Nodes that paint on top of their children may want to override this method to see
        /// if the pick path intersects that paint.
        /// </remarks>
        protected virtual bool Pick(PPickPath pickPath)
        {
            return false;
        }

        /// <summary>
        /// Try to pick this node and all of its descendents.
        /// </summary>
        /// <param name="pickPath">The pick path to add the node to if its picked.</param>
        /// <returns>True if this node or one of its descendents was picked; else false.</returns>
        /// <remarks>
        /// <b>Notes to Inheritors:</b>  Most subclasses should not need to override this
        /// method. Instead they should override <c>Pick</c> or <c>PickAfterChildren</c>.
        ///	</remarks>
        public virtual bool FullPick(PPickPath pickPath)
        {
            if ((Pickable || ChildrenPickable) && FullIntersects(pickPath.PickBounds))
            {
                pickPath.PushNode(this);
                pickPath.PushMatrix(matrix);

                bool thisPickable = Pickable && pickPath.AcceptsNode(this);

                if (thisPickable && Pick(pickPath))
                {
                    return true;
                }

                if (ChildrenPickable)
                {
                    int count = ChildrenCount;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        if (children[i].FullPick(pickPath))
                        {
                            return true;
                        }
                    }
                }

                if (thisPickable && PickAfterChildren(pickPath))
                {
                    return true;
                }

                pickPath.PopMatrix(matrix);
                pickPath.PopNode();
            }

            return false;
        }

        /// <summary>
        /// Checks this node and it's descendents for intersection with the given bounds
        /// and adds any intersecting nodes to the given list.
        /// </summary>
        /// <param name="fullBounds">
        /// The bounds to check for intersection, in parent coordinates.
        /// </param>
        /// <returns>
        /// The resulting list of nodes.
        /// </returns>
        public List<PNode> FindIntersectingNodes(RectangleF fullBounds)
        {
            var results = new List<PNode>();
			if (FullIntersects(fullBounds)) {
				RectangleF localBounds = ParentToLocal(fullBounds);

                if (Intersects(localBounds))
                {
                    results.Add(this);
                }

				int count = ChildrenCount;
				for (int i = count - 1; i >= 0; i--) {
					PNode each = children[i];
					results.AddRange(each.FindIntersectingNodes(localBounds));
				}
			}

            return results;
        }

        /// <summary>
        /// Try to pick this node after its children have had a chance to be picked.
        /// </summary>
        /// <param name="pickPath">The pick path used for the pick operation.</param>
        /// <returns>True if this node was picked; else false.</returns>
        /// <remarks>
        /// <b>Notes to Inheritors:</b>  Most subclasses that define a different geometry will
        /// need to override this method.
        /// </remarks>
        protected virtual bool PickAfterChildren(PPickPath pickPath)
        {
            if (Intersects(pickPath.PickBounds))
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Creates a pick path with a null camera and empty pickbounds and adds this node.
        /// </summary>
        /// <returns>
        /// A pick path with a null camera and empty pickbounds that contains this node
        /// </returns>
        /// <remarks>
        /// This method is useful if you want to dispatch events directly to a single node.
        /// For an example, see PSelectionExample, where the KeyboardFocus is set using
        /// this approach.
        /// </remarks>
        public virtual PPickPath ToPickPath()
        {
            return ToPickPath(null, RectangleF.Empty);
        }

        /// <summary>
        /// Creates a pick path with the given Camera and pickbounds and adds this node.
        /// </summary>
        /// <param name="aCamera">The camera to use when creating the pick path.</param>
        /// <param name="pickBounds">The pick bounds to use when creating the pick path.</param>
        /// <returns>
        /// A pick path with the given camera and pickbounds that contains this node
        /// </returns>
        public virtual PPickPath ToPickPath(PCamera aCamera, RectangleF pickBounds)
        {
            PPickPath pickPath = new PPickPath(aCamera, pickBounds);
            pickPath.PushNode(this);
            return pickPath;
        }
        #endregion

        #region Structure
        //****************************************************************
        // Structure - Methods for manipulating and traversing the 
        // parent child relationship
        // 
        // Most of these methods won't need to be overridden by subclasses
        // but you will use them frequently to build up your node structures.
        //****************************************************************

        /// <summary>Add a node to be a new child of this node.</summary>
        /// <param name="child">The new child to add to this node.</param>
        /// <remarks>
        /// The new node is added to the end of the list of this node's children.
        /// If child was previously a child of another node, it is removed from that
        /// node first.
        /// </remarks>
        public virtual void AddChild(PNode child)
        {
            int insertIndex = ChildrenCount;
            if (child.Parent == this)
            {
                insertIndex--;
            }
            AddChild(insertIndex, child);
        }

        /// <summary>
        /// Add a node to be a new child of this node at the specified index.
        /// </summary>
        /// <param name="index">The index at which to add the new child.</param>
        /// <param name="child">The new child to add to this node.</param>
        /// <remarks>
        /// If child was previously a child of another node, it is removed 
        /// from that node first.
        /// </remarks>
        public virtual void AddChild(int index, PNode child)
        {
            PNode oldParent = child.Parent;

            oldParent?.RemoveChild(child);

            child.Parent = this;
            ChildrenReference.Insert(index, child);
            child.InvalidatePaint();
            InvalidateFullBounds();

            FirePropertyChangedEvent(PROPERTY_KEY_CHILDREN, PROPERTY_CODE_CHILDREN, null, children);
        }

        /// <summary>
        /// Add a collection of nodes to be children of this node.
        /// </summary>
        /// <param name="nodes">
        /// A collection of nodes to be added to this node.
        /// </param>
        /// <remarks>
        /// <para>
        /// If these nodes already have parents they will first be removed from
        /// those parents.
        /// </para>
        /// </remarks>
        protected virtual void AddChildren(IEnumerable<PNode> nodes) {
			foreach (PNode each in nodes) {
				AddChild(each);
			}
		}

        /// <summary>
        /// Return true if this node is an ancestor of the parameter node.
        /// </summary>
        /// <param name="node">A possible descendent node.</param>
        /// <returns>True if this node is an ancestor of the given node; else false.</returns>
        public virtual bool IsAncestorOf(PNode node)
        {
            PNode p = node.Parent;
            while (p != null)
            {
                if (p == this)
                {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        /// <summary>
        /// Return true if this node is a descendent of the parameter node.
        /// </summary>
        /// <param name="node">A possible descendent node.</param>
        /// <returns>True if this node is a descendent of the given node; else false.</returns>
        public virtual bool IsDescendentOf(PNode node)
        {
            PNode p = parent;
            while (p != null)
            {
                if (p == node)
                {
                    return true;
                }
                p = p.Parent;
            }
            return false;
        }

        /// <summary>
        /// Return true if this node is a descendent of the Root.
        /// </summary>
        /// <returns>True if this node is a descendent of the root, else false;</returns>
        public virtual bool IsDescendentOfRoot()
        {
            return Root != null;
        }

        /// <summary>
        /// Change the order of this node in its parent's children list so that
        /// it will draw in back of all of its other sibling nodes.
        /// </summary>
        public virtual void MoveToBack()
        {
            PNode p = parent;
            if (p != null)
            {
                p.RemoveChild(this);
                p.AddChild(0, this);
            }
        }

        /// <summary>
        /// Change the order of this node in its parent's children list so that
        /// it will draw before the given sibling node.
        /// </summary>
        /// <param name="sibling">The sibling to move behind.</param>
        public void MoveInBackOf(PNode sibling)
        {
            PNode p = parent;
            if (p != null && p == sibling.Parent)
            {
                p.RemoveChild(this);
                int index = p.IndexOfChild(sibling);
                p.AddChild(index, this);
            }
        }

        /// <summary>
        /// Change the order of this node in its parent's children list so that
        /// it will draw in front of all of its other sibling nodes.
        /// </summary>
        public virtual void MoveToFront()
        {
            PNode p = parent;
            if (p != null)
            {
                p.RemoveChild(this);
                p.AddChild(this);
            }
        }

        /// <summary>
        /// Change the order of this node in its parent's children list so that
        /// it will draw after the given sibling node.
        /// </summary>
        /// <param name="sibling">The sibling to move in front of.</param>
        public void MoveInFrontOf(PNode sibling)
        {
            PNode p = parent;
            if (p != null && p == sibling.Parent)
            {
                p.RemoveChild(this);
                int index = p.IndexOfChild(sibling);
                p.AddChild(index + 1, this);
            }
        }

        /// <summary>
        /// Gets or sets the parent of this node.
        /// </summary>
        /// <value>The node that this node descends directly from.</value>
        /// <remarks>
        /// This property will be null if the node has not been added to a parent yet.
        /// </remarks>
        public virtual PNode Parent
        {
            get => parent;
            set
            {
                PNode old = parent;
                parent = value;
                FirePropertyChangedEvent(PROPERTY_KEY_PARENT, PROPERTY_CODE_PARENT, old, parent);
            }
        }

        /// <summary>
        /// Return the index where the given child is stored.
        /// </summary>
        /// <param name="child">The child whose index is desired.</param>
        /// <returns>The index of the given child.</returns>
        public virtual int IndexOfChild(PNode child)
        {
            if (children == null) return -1;
            return children.IndexOf(child);
        }

        /// <summary>
        /// Remove the given child from this node's children list.
        /// </summary>
        /// <param name="child">The child to remove.</param>
        /// <remarks>
        /// Any subsequent children are shifted to the left (one is subtracted 
        /// from their indices). The removed child’s parent is set to null.
        /// </remarks>
        /// <returns>The removed child.</returns>
        public virtual PNode RemoveChild(PNode child)
        {
            return RemoveChild(IndexOfChild(child));
        }

        /// <summary>
        /// Remove the child at the specified position from this node's children.
        /// </summary>
        /// <param name="index">The index of the child to remove.</param>
        /// <remarks >
        /// Any subsequent children are shifted to the left (one is subtracted 
        /// from their indices). The removed child’s parent is set to null.
        /// </remarks>
        /// <returns>The removed child.</returns>
        public virtual PNode RemoveChild(int index)
        {
            if (index == -1) return null;
            PNode child = children[index];
            children.RemoveAt(index);

            if (children.Count == 0)
            {
                children = null;
            }

            child.Repaint();
            child.Parent = null;
            InvalidateFullBounds();

            FirePropertyChangedEvent(PROPERTY_KEY_CHILDREN, PROPERTY_CODE_CHILDREN, null, children);

            return child;
        }

        /// <summary>
        /// Remove all the children in the given enumerable from this node’s
        /// list of children.
        /// </summary>
        /// <param name="childrenNodes">
        /// The collection of children to remove.
        /// </param>
        /// <remarks>
        /// <para>
        /// All removed nodes will have their parent set to null.
        /// </para>
        /// </remarks>
        public virtual void RemoveChildren(IEnumerable<PNode> childrenNodes) {
			foreach (PNode each in childrenNodes) {
				RemoveChild(each);
			}
		}

        /// <summary>
        /// Remove all the children from this node.
        /// </summary>
        /// <remarks>
        /// Note this method is more efficient then removing each child individually.
        /// </remarks>
        public virtual void RemoveAllChildren()
        {
            if (children != null)
            {
                int count = ChildrenCount;
                for (int i = 0; i < count; i++)
                {
                    children[i].Parent = null;
                }

                children = null;
                InvalidatePaint();
                InvalidateFullBounds();

                FirePropertyChangedEvent(PROPERTY_KEY_CHILDREN, PROPERTY_CODE_CHILDREN, null, children);
            }
        }

        /// <summary>
        /// Delete this node by removing it from its parent’s list of children.
        /// </summary>
        public virtual void RemoveFromParent()
        {
            parent?.RemoveChild(this);
        }

        /// <summary>
        /// Set the parent of this node, and transform the node in such a way that it
        /// doesn't move in global coordinates.
        /// </summary>
        /// <param name="newParent">The new parent of this node.</param>
        public virtual void Reparent(PNode newParent)
        {
            PMatrix originalTransform = LocalToGlobalMatrix;
            PMatrix newTransform = newParent.GlobalToLocalMatrix;
            newTransform.Multiply(originalTransform);

            RemoveFromParent();
            Matrix = newTransform;
            newParent.AddChild(this);
            fullBoundsCache = ComputeFullBounds();
        }

        /// <summary>
        /// Swaps this node out of the scene graph tree, and replaces it with the specified
        /// replacement node.
        /// </summary>
        /// <param name="replacementNode">
        /// The new node that replaces the current node in the scene graph tree.
        /// </param>
        /// <remarks>
        /// This node is left dangling, and it is up to the caller to manage it.  The
        /// replacement node will be added to this node's parent in the same position as this node
        /// was located.  That is, if this was the 3rd child of its parent, then after calling
        /// <c>ReplaceWith</c>, the replacement node will also be the 3rd child of its parent.
        ///	If this node has no parent when <c>ReplaceWith</c> is called, then nothing will be
        ///	done at all.
        /// </remarks>
        public virtual void ReplaceWith(PNode replacementNode)
        {
            if (parent != null)
            {
                PNode myParent = parent;
                int index = myParent.ChildrenReference.IndexOf(this);
                myParent.RemoveChild(this);
                myParent.AddChild(index, replacementNode);
            }
        }

        /// <summary>
        /// Gets the number of children that this node has.
        /// </summary>
        /// <value>The number of children this node has.</value>
        public virtual int ChildrenCount
        {
            get
            {
                if (children == null)
                {
                    return 0;
                }
                return children.Count;
            }
        }

        /// <summary>
        /// Return the child node at the specified index.
        /// </summary>
        /// <param name="index">The index of the desired child.</param>
        /// <returns>The child node at the specified index.</returns>
        public virtual PNode GetChild(int index)
        {
            return children[index];
        }

        /// <summary>
        /// Allows a PNode to be indexed directly to access it's children.
        /// </summary>
        /// <remarks>
        /// This provides a shortcut to indexing a node's children.  For example,
        /// <c>aNode.GetChild(i)</c> is equivalent to <c>aNode[i]</c>.  Note that using the
        /// indexor to set a child will remove the child currently at that index.
        /// </remarks>
        public PNode this[int index]
        {
            get => children[index];
            set
            {
                RemoveChild(index);
                AddChild(index, value);
            }
        }

		/// <summary>
		/// Gets a reference to the list used to manage this node’s children.
		/// </summary>
		/// <value>A reference to the list of children.</value>
		/// <remarks>This list should not be modified.</remarks>
		public virtual List<PNode> ChildrenReference => children ??= new List<PNode>();

        /// <summary>
        /// Return an enumerator for this node’s direct descendent children.
        /// </summary>
        /// <returns>An enumerator for this node's children.</returns>
        /// <remarks>
        /// This method allows you to use the foreach loop to iterate over a node's children.
        /// For example, you could do the following:
        /// 
        /// <code>
        ///		foreach(PNode node in aNode) {
        ///			node.DoSomething();
        ///		}
        /// </code>
        /// 
        /// Typically, you will not need to call this method directly.  Instead use the
        /// ChildrenEnumerator property.
        /// </remarks>
        public IEnumerator<PNode> GetEnumerator() => ChildrenEnumerator;

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Return an enumerator for this node’s direct descendent children.
        /// </summary>
        /// <value>An enumerator for this node's children.</value>
        public IEnumerator<PNode> ChildrenEnumerator
        {
            get
            {
                if (children == null)
                {
                    return Enumerable.Empty<PNode>().GetEnumerator();
                }
                return children.GetEnumerator();
            }
        }

        /// <summary>
        /// Gets the root node (instance of PRoot).
        /// </summary>
        /// <value>The root node that this node descends from.</value>
        /// <remarks>
        /// If this node does not descend from a PRoot then null will be returned.
        /// </remarks>
        public virtual PRoot Root => parent?.Root;

        /// <summary>
		/// Gets a list containing this node and all of its descendent nodes.
		/// </summary>
		/// <value>A list containing this node and all its descendents.</value>
		public virtual List<PNode> AllNodes => GetAllNodes(null, null);

        /// <summary>
		/// Gets a list containing the subset of this node and all of its descendent nodes
		/// that are accepted by the given node filter. 
		/// </summary>
		/// <param name="filter">The filter used to determine the subset.</param>
		/// <param name="results">The list used to collect the subset; can be null.</param>
		/// <returns>
		/// A list containing the subset of this node and all its descendents accepted by
		/// the filter.
		/// </returns>
		/// <remarks>
		/// If the filter is null then all nodes will be accepted. If the results parameter is not
		/// null then it will be used to store this subset instead of creating a new list.
		/// </remarks>
		public virtual List<PNode> GetAllNodes(PNodeFilter filter, List<PNode> results) {
			results ??= new List<PNode>();
			if (filter == null || filter.Accept(this)) {
				results.Add(this);
			}

            if (filter == null || filter.AcceptChildrenOf(this))
            {
                int count = ChildrenCount;
                for (int i = 0; i < count; i++)
                {
                    children[i].GetAllNodes(filter, results);
                }
            }

            return results;
        }
        #endregion

        #region Debugging
        //****************************************************************
        // Debugging - methods for debugging
        //****************************************************************

        /// <summary>
        /// Returns a string representation of this object for debugging purposes.
        /// </summary>
        /// <returns>A string representation of this object.</returns>
        public override string ToString()
        {
            return base.ToString() + "[" + ParamString + "]";
        }

        /// <summary>
        /// Gets a string representing the state of this node.
        /// </summary>
        /// <value>A string representation of this node's state.</value>
        /// <remarks>
        /// This property is intended to be used only for debugging purposes, and the content
        /// and format of the returned string may vary between implementations. The returned
        /// string may be empty but may not be <c>null</c>.
        /// </remarks>
        protected virtual string ParamString
        {
            get
            {
                var result = new StringBuilder();

                result.Append("bounds=" + bounds);
                result.Append(",fullBounds=" + fullBoundsCache);
                result.Append(",matrix=" + (matrix == null ? "null" : matrix.ToString()));
                result.Append(",brush=" + (brush == null ? "null" : brush.ToString()));
                if (brush is SolidBrush solidBrush)
                {
                    result.Append("[" + solidBrush.Color + "]");
                }
                //transparency not yet implemented
                //    result.Append(",transparency=" + transparency);
                result.Append(",childrenCount=" + ChildrenCount);

                if (fullBoundsInvalid)
                {
                    result.Append(",fullBoundsInvalid");
                }

                if (pickable)
                {
                    result.Append(",pickable");
                }

                if (childrenPickable)
                {
                    result.Append(",childrenPickable");
                }

                if (visible)
                {
                    result.Append(",visible");
                }

                return result.ToString();
            }
        }
        #endregion
    }
}
