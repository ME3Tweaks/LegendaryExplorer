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

using System.Drawing;
using System.Text;
using Piccolo.Event;
using Piccolo.Util;

namespace Piccolo.Nodes {
	/// <summary>
	/// <b>PText</b> is a multi-line text node.  The text will wrap based on the width
	/// of the node's bounds.
	/// </summary>
	public class PText : PNode {
		#region Fields
		/// <summary>
		/// The key that identifies a change in this node's <see cref="Text">Text</see>.
		/// </summary>
		/// <remarks>
		/// In a property change event both the old and new value will be set correctly
		/// to String objects.
		/// </remarks>
		protected static readonly object PROPERTY_KEY_TEXT = new();

		/// <summary>
		/// A bit field that identifies a <see cref="TextChanged">TextChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether TextChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_TEXT = 1 << 17;

		/// <summary>
		/// The key that identifies a change in this node's <see cref="Font">Font</see>.
		/// </summary>
		/// <remarks>
		/// In a property change event both the old and new value will be set correctly
		/// to Font objects.
		/// </remarks>
		protected static readonly object PROPERTY_KEY_FONT = new();

		/// <summary>
		/// A bit field that identifies a <see cref="FontChanged">FontChanged</see> event.
		/// </summary>
		/// <remarks>
		/// This field is used to indicate whether FontChanged events should be forwarded to
		/// a node's parent.
		/// <seealso cref="PPropertyEventArgs">PPropertyEventArgs</seealso>.
		/// <seealso cref="PNode.PropertyChangeParentMask">PropertyChangeParentMask</seealso>.
		/// </remarks>
		public const int PROPERTY_CODE_FONT = 1 << 18;

		/// <summary>
		/// The default font to use when rendering this PText node.
		/// </summary>
		public static Font DEFAULT_FONT = new("Arial", 12);

		private static Graphics GRAPHICS = Graphics.FromImage(new Bitmap(1, 1));
		private string text;
		private Brush textBrush;
		private Font font;
		private StringFormat stringFormat = new();
		private bool constrainHeightToTextHeight = true;
		private bool constrainWidthToTextWidth = true;
		#endregion

		#region Constructors
		/// <summary>
		/// Constructs a new PText with an empty string.
		/// </summary>
		public PText() {
			textBrush = Brushes.Black;
            if (ReferenceEquals(Font, DEFAULT_FONT))
            {
                FontSizeInPoints = Font.SizeInPoints;
            }
		}

		/// <summary>
		/// Constructs a new PText with the given string.
		/// </summary>
		/// <param name="aText">The desired text string for this PText.</param>
		public PText(string aText) : this() {
			Text = aText;
		}

        //specialized constructor to avoid recomputing the bounds when setting textbrush, font, x, and y.
        //also can avoid the very expensize Font.SizeInPoints call
        protected PText(string text, Brush brush, Font textFont, float fontSizeInPoints, float x, float y)
        {
            textBrush = brush;
            font = textFont;
            FontSizeInPoints = fontSizeInPoints;
            bounds = new RectangleF(x, y, 0, 0);
            Text = text;
        }
		#endregion

		#region Basic
		//****************************************************************
		// Basic - Methods for manipulating the underlying text.
		//****************************************************************

		/// <summary>
		/// Occurs when there is a change in this node's <see cref="Text">Text</see>.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the TextChanged Event as in
		/// TextChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_TEXT in the Events list).
		/// When a user removes an event handler from the TextChanged event as in 
		/// TextChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_TEXT in the Events list).
		/// </remarks>
		public virtual event PPropertyEventHandler TextChanged {
			add => HandlerList.AddHandler(PROPERTY_KEY_TEXT, value);
            remove => HandlerList.RemoveHandler(PROPERTY_KEY_TEXT, value);
        }

		/// <summary>
		/// Occurs when there is a change in this node's <see cref="Font">Font</see>.
		/// </summary>
		/// <remarks>
		/// When a user attaches an event handler to the FontChanged Event as in
		/// FontChanged += new PPropertyEventHandler(aHandler),
		/// the add method adds the handler to the delegate for the event
		/// (keyed by PROPERTY_KEY_FONT in the Events list).
		/// When a user removes an event handler from the FontChanged event as in 
		/// FontChanged -= new PPropertyEventHandler(aHandler),
		/// the remove method removes the handler from the delegate for the event
		/// (keyed by PROPERTY_KEY_FONT in the Events list).
		/// </remarks>
		public virtual event PPropertyEventHandler FontChanged {
			add => HandlerList.AddHandler(PROPERTY_KEY_FONT, value);
            remove => HandlerList.RemoveHandler(PROPERTY_KEY_FONT, value);
        }

		/// <summary>
		/// Gets or sets a value indicating whether this node changes its width to fit
		/// the width of its text.
		/// </summary>
		/// <value>
		/// True if this node changes its width to fit its text width; otherwise, false.
		/// </value>
		public virtual bool ConstrainWidthToTextWidth {
			get => constrainWidthToTextWidth;
            set {
				constrainWidthToTextWidth = value;
				InvalidatePaint();
				RecomputeBounds();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether this node changes its height to fit
		/// the height of its text.
		/// </summary>
		/// <value>
		/// True if this node changes its height to fit its text height; otherwise, false.
		/// </value>
		public virtual bool ConstrainHeightToTextHeight {
			get => constrainHeightToTextHeight;
            set {
				constrainHeightToTextHeight = value;
				InvalidatePaint();
				RecomputeBounds();
			}
		}

		/// <summary>
		/// Gets or sets the text for this node.
		/// </summary>
		/// <value>This node's text.</value>
		/// <remarks>
		/// The text will be broken up into multiple lines based on the size of the text
		/// and the bounds width of this node.
		/// </remarks>
		public string Text {
			get => text;
            set {
				string old = text;
				text = value;

				InvalidatePaint();
				RecomputeBounds();
				FirePropertyChangedEvent(PROPERTY_KEY_TEXT, PROPERTY_CODE_TEXT, old, text);
			}
		}

		/// <summary>
		/// Gets or sets a value specifiying the alignment to use when rendering this
		/// node's text.
		/// </summary>
		/// <value>The alignment to use when rendering this node's text.</value>
		public virtual StringAlignment TextAlignment {
			get => stringFormat.Alignment;
            set {
				stringFormat.Alignment = value;
				InvalidatePaint();
				RecomputeBounds();
			}
		}

		/// <summary>
		/// Gets or sets the brush to use when rendering this node's text.
		/// </summary>
		/// <value>The brush to use when rendering this node's text.</value>
		public virtual Brush TextBrush {
			get => textBrush;
            set {
				textBrush = value;
				InvalidatePaint();
			}
		}

		/// <summary>
		/// Use instead of Font.SizeInPoints, which is surprisingly expensive.
		/// </summary>
        public float FontSizeInPoints { get; private set; }

		/// <summary>
		/// Gets or sets the font to use when rendering this node's text.
		/// </summary>
		/// <value>The font to use when rendering this node's text.</value>
		public Font Font {
			get => font ??= DEFAULT_FONT;
            set {
				Font old = font;
				font = value;
                FontSizeInPoints = font.SizeInPoints;
				InvalidatePaint();
				RecomputeBounds();
				FirePropertyChangedEvent(PROPERTY_KEY_FONT, PROPERTY_CODE_FONT, old, font);
			}
		}
		#endregion

		#region Painting
		//****************************************************************
		// Painting - Methods for painting a PText.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PNode.Paint">PNode.Paint</see>.
		/// </summary>
		protected override void Paint(PPaintContext paintContext) {
			base.Paint(paintContext);

			if (text != null && textBrush != null && font != null) {
				Graphics g = paintContext.Graphics;

				float renderedFontSize = FontSizeInPoints * paintContext.Scale;
				if (renderedFontSize < PUtil.GreekThreshold) {
					
					// .NET bug: DrawString throws a generic gdi+ exception when
					// the scaled font size is very small.  So, we will render
					// the text as a simple rectangle for small fonts
					g.FillRectangle(textBrush, Bounds);
				}
				else if (renderedFontSize < PUtil.MaxFontSize) {
					Font renderFont = font;

					// The font needs to be adjusted for printing.

                    //TODO: Remove HighDPI scaling for this as we have zoom controller
					if (g.DpiY != GRAPHICS.DpiY) {
						float fPrintedFontRatio = GRAPHICS.DpiY / 100;
						renderFont = new Font(font.Name, font.Size * fPrintedFontRatio,
							font.Style, font.Unit);
					}

					g.DrawString(text, renderFont, textBrush, Bounds, stringFormat);
				}
			}
		}
		#endregion

		#region Bounds
		//****************************************************************
		// Bounds - Methods for manipulating the bounds of a PText.
		//****************************************************************

		/// <summary>
		/// Overridden.  See <see cref="PNode.InternalUpdateBounds">PNode.InternalUpdateBounds</see>.
		/// </summary>
		protected override void InternalUpdateBounds(float x, float y, float width, float height) {
			RecomputeBounds();
		}

		/// <summary>
		/// Override this method to change the way bounds are computed. For example
		/// this is where you can control how lines are wrapped.
		/// </summary>
		public virtual void RecomputeBounds() {
			if (text != null && (ConstrainWidthToTextWidth || ConstrainHeightToTextHeight)) {
				float textWidth;
				float textHeight;
				if (ConstrainWidthToTextWidth) {
                    SizeF stringSize = GRAPHICS.MeasureString(Text, Font);
                    textWidth = stringSize.Width;
					textHeight = stringSize.Height;
				}
				else {
					textWidth = Width;
					SizeF layoutSize = new SizeF(textWidth, float.PositiveInfinity);
					textHeight = GRAPHICS.MeasureString(Text, Font, layoutSize, stringFormat).Height;
				}

				float newWidth = Width;
				float newHeight = Height;
				if (ConstrainWidthToTextWidth) newWidth = textWidth;
				if (ConstrainHeightToTextHeight) newHeight = textHeight;

				base.SetBounds(X, Y, newWidth, newHeight);
			}
		}
		#endregion
		
		#region Debugging
		//****************************************************************
		// Debugging - Methods for debugging.
		//****************************************************************

		/// <summary>
		/// Overridden.  Gets a string representing the state of this node.
		/// </summary>
		/// <value>A string representation of this node's state.</value>
		/// <remarks>
		/// This property is intended to be used only for debugging purposes, and the content
		/// and format of the returned string may vary between implementations. The returned
		/// string may be empty but may not be <c>null</c>.
		/// </remarks>
		protected override string ParamString {
			get {
				StringBuilder result = new StringBuilder();

				result.Append("text=" + (text == null ? "null" : text));
				result.Append(",font=" + (font == null ? "null" : font.ToString()));
				result.Append(',');
				result.Append(base.ParamString);

				return result.ToString();
			}
		}
		#endregion
	}
}
