using System;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Security.Permissions;
using System.Windows.Forms.VisualStyles;
using System.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Globalization;
using System.ComponentModel.DataAnnotations;

namespace Be.Windows.Forms
{
    /// <summary>
    /// Represents a hex box control.
    /// </summary>
    [ToolboxBitmap(typeof(HexBox), "HexBox.bmp")]
    public partial class HexBox : Control, INotifyPropertyChanged
    {
        #region IKeyInterpreter interface
        /// <summary>
        /// Defines a user input handler such as for mouse and keyboard input
        /// </summary>
        interface IKeyInterpreter
        {
            /// <summary>
            /// Activates mouse events
            /// </summary>
            void Activate();
            /// <summary>
            /// Deactivate mouse events
            /// </summary>
            void Deactivate();
            /// <summary>
            /// Preprocesses WM_KEYUP window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmKeyUp(ref Message m);
            /// <summary>
            /// Preprocesses WM_CHAR window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmChar(ref Message m);
            /// <summary>
            /// Preprocesses WM_KEYDOWN window message.
            /// </summary>
            /// <param name="m">the Message object to process.</param>
            /// <returns>True, if the message was processed.</returns>
            bool PreProcessWmKeyDown(ref Message m);
            /// <summary>
            /// Gives some information about where to place the caret.
            /// </summary>
            /// <param name="byteIndex">the index of the byte</param>
            /// <returns>the position where the caret is to place.</returns>
            PointF GetCaretPointF(long byteIndex);
        }
        #endregion

        #region EmptyKeyInterpreter class
        /// <summary>
        /// Represents an empty input handler without any functionality. 
        /// If is set ByteProvider to null, then this interpreter is used.
        /// </summary>
        class EmptyKeyInterpreter : IKeyInterpreter
        {
            HexBox _hexBox;

            public EmptyKeyInterpreter(HexBox hexBox)
            {
                _hexBox = hexBox;
            }

            #region IKeyInterpreter Members
            public void Activate() { }
            public void Deactivate() { }

            public bool PreProcessWmKeyUp(ref Message m)
            { return _hexBox.BasePreProcessMessage(ref m); }

            public bool PreProcessWmChar(ref Message m)
            { return _hexBox.BasePreProcessMessage(ref m); }

            public bool PreProcessWmKeyDown(ref Message m)
            { return _hexBox.BasePreProcessMessage(ref m); }

            public PointF GetCaretPointF(long byteIndex)
            { return new PointF(); }

            #endregion
        }
        #endregion

        #region KeyInterpreter class
        /// <summary>
        /// Handles user input such as mouse and keyboard input during hex view edit
        /// </summary>
        class KeyInterpreter : IKeyInterpreter
        {
            /// <summary>
            /// Delegate for key-down processing.
            /// </summary>
            /// <param name="m">the message object contains key data information</param>
            /// <returns>True, if the message was processed</returns>
            delegate bool MessageDelegate(ref Message m);

            #region Fields
            /// <summary>
            /// Contains the parent HexBox control
            /// </summary>
            protected HexBox _hexBox;

            /// <summary>
            /// Contains True, if shift key is down
            /// </summary>
            protected bool _shiftDown;
            /// <summary>
            /// Contains True, if mouse is down
            /// </summary>
            bool _mouseDown;
            /// <summary>
            /// Contains the selection start position info
            /// </summary>
            BytePositionInfo _bpiStart;
            /// <summary>
            /// Contains the current mouse selection position info
            /// </summary>
            BytePositionInfo _bpi;
            /// <summary>
            /// Contains all message handlers of key interpreter key down message
            /// </summary>
            Dictionary<Keys, MessageDelegate> _messageHandlers;
            #endregion

            #region Ctors
            public KeyInterpreter(HexBox hexBox)
            {
                _hexBox = hexBox;
            }
            #endregion

            #region Activate, Deactive methods
            public virtual void Activate()
            {
                _hexBox.MouseDown += BeginMouseSelection;
                _hexBox.MouseMove += UpdateMouseSelection;
                _hexBox.MouseUp += EndMouseSelection;
            }

            public virtual void Deactivate()
            {
                _hexBox.MouseDown -= BeginMouseSelection;
                _hexBox.MouseMove -= UpdateMouseSelection;
                _hexBox.MouseUp -= EndMouseSelection;
            }
            #endregion

            #region Mouse selection methods
            void BeginMouseSelection(object sender, MouseEventArgs e)
            {
                //System.Diagnostics.Debug.WriteLine("BeginMouseSelection()", "KeyInterpreter");

                if (e.Button != MouseButtons.Left)
                    return;

                _mouseDown = true;

                if (!_shiftDown)
                {
                    _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    UpdateMouseSelection(this, e);
                }
            }

            void UpdateMouseSelection(object sender, MouseEventArgs e)
            {
                if (!_mouseDown)
                    return;

                _bpi = GetBytePositionInfo(new Point(e.X, e.Y));
                long selEnd = _bpi.Index;
                long realselStart;
                long realselLength;

                if (selEnd < _bpiStart.Index)
                {
                    realselStart = selEnd;
                    realselLength = _bpiStart.Index - selEnd;
                }
                else if (selEnd > _bpiStart.Index)
                {
                    realselStart = _bpiStart.Index;
                    realselLength = selEnd - realselStart;
                }
                else
                {
                    realselStart = _hexBox._bytePos;
                    realselLength = 0;
                }

                if (realselStart != _hexBox._bytePos || realselLength != _hexBox._selectionLength)
                {
                    _hexBox.InternalSelect(realselStart, realselLength);
                    _hexBox.ScrollByteIntoView(_bpi.Index);
                }
            }

            void EndMouseSelection(object sender, MouseEventArgs e)
            {
                _mouseDown = false;
            }
            #endregion

            #region PrePrcessWmKeyDown methods
            public virtual bool PreProcessWmKeyDown(ref Message m)
            {
                //System.Diagnostics.Debug.WriteLine("PreProcessWmKeyDown(ref Message m)", "KeyInterpreter");

                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                // detect whether key down event should be raised
                var hasMessageHandler = this.MessageHandlers.ContainsKey(keyData);
                if (hasMessageHandler && RaiseKeyDown(keyData))
                    return true;

                MessageDelegate messageHandler = hasMessageHandler
                    ? this.MessageHandlers[keyData]
                    : PreProcessWmKeyDown_Default;

                return messageHandler(ref m);
            }

            protected bool PreProcessWmKeyDown_Default(ref Message m)
            {
                _hexBox.ScrollByteIntoView();
                return _hexBox.BasePreProcessMessage(ref m);
            }

            protected bool RaiseKeyDown(Keys keyData)
            {
                KeyEventArgs e = new KeyEventArgs(keyData);
                _hexBox.OnKeyDown(e);
                return e.Handled;
            }

            protected virtual bool PreProcessWmKeyDown_Left(ref Message m)
            {
                return PerformPosMoveLeft();
            }

            protected virtual bool PreProcessWmKeyDown_Up(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (!(pos == 0 && cp == 0))
                {
                    pos = Math.Max(-1, pos - _hexBox._iHexMaxHBytes);
                    if (pos == -1)
                        return true;

                    _hexBox.SetPosition(pos);

                    if (pos < _hexBox._startByte)
                    {
                        _hexBox.PerformScrollLineUp();
                    }

                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                }

                _hexBox.ScrollByteIntoView();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Right(ref Message m)
            {
                return PerformPosMoveRight();
            }

            protected virtual bool PreProcessWmKeyDown_Down(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length && cp == 0)
                    return true;

                pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxHBytes);

                if (pos == _hexBox._byteProvider.Length)
                    cp = 0;

                _hexBox.SetPosition(pos, cp);

                if (pos > _hexBox._endByte - 1)
                {
                    _hexBox.PerformScrollLineDown();
                }

                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_PageUp(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos == 0 && cp == 0)
                    return true;

                pos = Math.Max(0, pos - _hexBox._iHexMaxBytes);
                if (pos == 0)
                    return true;

                _hexBox.SetPosition(pos);

                if (pos < _hexBox._startByte)
                {
                    _hexBox.PerformScrollPageUp();
                }

                _hexBox.ReleaseSelection();
                _hexBox.UpdateCaret();
                _hexBox.Invalidate();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_PageDown(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length && cp == 0)
                    return true;

                pos = Math.Min(_hexBox._byteProvider.Length, pos + _hexBox._iHexMaxBytes);

                if (pos == _hexBox._byteProvider.Length)
                    cp = 0;

                _hexBox.SetPosition(pos, cp);

                if (pos > _hexBox._endByte - 1)
                {
                    _hexBox.PerformScrollPageDown();
                }

                _hexBox.ReleaseSelection();
                _hexBox.UpdateCaret();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftLeft(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos + sel < 1)
                    return true;

                if (pos + sel <= _bpiStart.Index)
                {
                    if (pos == 0)
                        return true;

                    pos--;
                    sel++;
                }
                else
                {
                    sel = Math.Max(0, sel - 1);
                }

                _hexBox.ScrollByteIntoView();
                _hexBox.InternalSelect(pos, sel);

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftUp(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos < _hexBox._iHexMaxHBytes && pos <= _bpiStart.Index)
                    return true;

                if (_bpiStart.Index >= pos + sel)
                {
                    pos -= _hexBox._iHexMaxHBytes;
                    sel += _hexBox._iHexMaxHBytes;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }
                else
                {
                    sel -= _hexBox._iHexMaxHBytes;
                    if (sel < 0)
                    {
                        pos = _bpiStart.Index + sel;
                        sel = -sel;
                        _hexBox.InternalSelect(pos, sel);
                        _hexBox.ScrollByteIntoView();
                    }
                    else
                    {
                        sel -= _hexBox._iHexMaxHBytes;
                        _hexBox.InternalSelect(pos, sel);
                        _hexBox.ScrollByteIntoView(pos + sel);
                    }
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftRight(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos + sel >= _hexBox._byteProvider.Length)
                    return true;

                if (_bpiStart.Index <= pos)
                {
                    sel++;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView(pos + sel);
                }
                else
                {
                    pos++;
                    sel = Math.Max(0, sel - 1);
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftDown(ref Message m)
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                long max = _hexBox._byteProvider.Length;

                if (pos + sel + _hexBox._iHexMaxHBytes > max)
                    return true;

                if (_bpiStart.Index <= pos)
                {
                    sel += _hexBox._iHexMaxHBytes;
                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView(pos + sel);
                }
                else
                {
                    sel -= _hexBox._iHexMaxHBytes;
                    if (sel < 0)
                    {
                        pos = _bpiStart.Index;
                        sel = -sel;
                    }
                    else
                    {
                        pos += _hexBox._iHexMaxHBytes;
                        //sel -= _hexBox._iHexMaxHBytes;
                    }

                    _hexBox.InternalSelect(pos, sel);
                    _hexBox.ScrollByteIntoView();
                }

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Tab(ref Message m)
            {
                if (_hexBox._stringViewVisible && _hexBox._keyInterpreter.GetType() == typeof(KeyInterpreter))
                {
                    _hexBox.ActivateStringKeyInterpreter();
                    _hexBox.ScrollByteIntoView();
                    _hexBox.ReleaseSelection();
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                    return true;
                }

                if (_hexBox.Parent == null) return true;
                _hexBox.Parent.SelectNextControl(_hexBox, true, true, true, true);
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftTab(ref Message m)
            {
                if (_hexBox._keyInterpreter is StringKeyInterpreter)
                {
                    _shiftDown = false;
                    _hexBox.ActivateKeyInterpreter();
                    _hexBox.ScrollByteIntoView();
                    _hexBox.ReleaseSelection();
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                    return true;
                }

                if (_hexBox.Parent == null) return true;
                _hexBox.Parent.SelectNextControl(_hexBox, false, true, true, true);
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Back(ref Message m)
            {
                if (!_hexBox._byteProvider.SupportsDeleteBytes())
                    return true;

                if (_hexBox.ReadOnly)
                    return true;

                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;
                int cp = _hexBox._byteCharacterPos;

                long startDelete = (cp == 0 && sel == 0) ? pos - 1 : pos;
                if (startDelete < 0 && sel < 1)
                    return true;

                long bytesToDelete = (sel > 0) ? sel : 1;
                _hexBox._byteProvider.DeleteBytes(Math.Max(0, startDelete), bytesToDelete);
                _hexBox.UpdateScrollSize();

                if (sel == 0)
                    PerformPosMoveLeftByte();

                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Delete(ref Message m)
            {
                if (!_hexBox._byteProvider.SupportsDeleteBytes())
                    return true;

                if (_hexBox.ReadOnly)
                    return true;

                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;

                if (pos >= _hexBox._byteProvider.Length)
                    return true;

                long bytesToDelete = (sel > 0) ? sel : 1;
                _hexBox._byteProvider.DeleteBytes(pos, bytesToDelete);

                _hexBox.UpdateScrollSize();
                _hexBox.ReleaseSelection();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_Home(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos < 1)
                    return true;

                pos = 0;
                cp = 0;
                _hexBox.SetPosition(pos, cp);

                _hexBox.ScrollByteIntoView();
                _hexBox.UpdateCaret();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_End(ref Message m)
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos >= _hexBox._byteProvider.Length - 1)
                    return true;

                pos = _hexBox._byteProvider.Length;
                cp = 0;
                _hexBox.SetPosition(pos, cp);

                _hexBox.ScrollByteIntoView();
                _hexBox.UpdateCaret();
                _hexBox.ReleaseSelection();

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ShiftShiftKey(ref Message m)
            {
                if (_mouseDown)
                    return true;
                if (_shiftDown)
                    return true;

                _shiftDown = true;

                if (_hexBox._selectionLength > 0)
                    return true;

                _bpiStart = new BytePositionInfo(_hexBox._bytePos, _hexBox._byteCharacterPos);

                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlC(ref Message m)
            {
                _hexBox.Copy();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlX(ref Message m)
            {
                _hexBox.Cut();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlV(ref Message m)
            {
                _hexBox.Paste();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlShiftC(ref Message m)
            {
                _hexBox.CopyHex();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlShiftV(ref Message m)
            {
                _hexBox.PasteHex();
                return true;
            }

            protected virtual bool PreProcessWmKeyDown_ControlA(ref Message m)
            {
                _hexBox.SelectAll();
                return true;
            }

            #endregion

            #region PreProcessWmChar methods
            public virtual bool PreProcessWmChar(ref Message m)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    return _hexBox.BasePreProcessMessage(ref m);
                }

                bool sw = _hexBox._byteProvider.SupportsWriteByte();
                bool si = _hexBox._byteProvider.SupportsInsertBytes();
                bool sd = _hexBox._byteProvider.SupportsDeleteBytes();

                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;
                int cp = _hexBox._byteCharacterPos;

                if (
                    (!sw && pos != _hexBox._byteProvider.Length) ||
                    (!si && pos == _hexBox._byteProvider.Length))
                {
                    return _hexBox.BasePreProcessMessage(ref m);
                }

                char c = (char)m.WParam.ToInt32();

                if (Uri.IsHexDigit(c))
                {
                    if (RaiseKeyPress(c))
                        return true;

                    if (_hexBox.ReadOnly)
                        return true;

                    bool isInsertMode = (pos == _hexBox._byteProvider.Length);

                    // do insert when insertActive = true
                    if (!isInsertMode && si && _hexBox.InsertActive && cp == 0)
                        isInsertMode = true;

                    if (sd && si && sel > 0)
                    {
                        _hexBox._byteProvider.DeleteBytes(pos, sel);
                        isInsertMode = true;
                        cp = 0;
                        _hexBox.SetPosition(pos, cp);
                    }

                    _hexBox.ReleaseSelection();

                    byte currentByte;
                    if (isInsertMode)
                        currentByte = 0;
                    else
                        currentByte = _hexBox._byteProvider.ReadByte(pos);

                    string sCb = currentByte.ToString("X", System.Threading.Thread.CurrentThread.CurrentCulture);
                    if (sCb.Length == 1)
                        sCb = "0" + sCb;

                    string sNewCb = c.ToString();
                    if (cp == 0)
                        sNewCb += sCb.Substring(1, 1);
                    else
                        sNewCb = sCb.Substring(0, 1) + sNewCb;
                    byte newcb = byte.Parse(sNewCb, System.Globalization.NumberStyles.AllowHexSpecifier, System.Threading.Thread.CurrentThread.CurrentCulture);

                    if (isInsertMode)
                        _hexBox._byteProvider.InsertBytes(pos, [newcb]);
                    else
                        _hexBox._byteProvider.WriteByte(pos, newcb);

                    PerformPosMoveRight();

                    _hexBox.Invalidate();
                    return true;
                }
                else
                {
                    return _hexBox.BasePreProcessMessage(ref m);
                }
            }

            protected bool RaiseKeyPress(char keyChar)
            {
                KeyPressEventArgs e = new KeyPressEventArgs(keyChar);
                _hexBox.OnKeyPress(e);
                return e.Handled;
            }
            #endregion

            #region PreProcessWmKeyUp methods
            public virtual bool PreProcessWmKeyUp(ref Message m)
            {
                //System.Diagnostics.Debug.WriteLine("PreProcessWmKeyUp(ref Message m)", "KeyInterpreter");

                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                switch (keyData)
                {
                    case Keys.ShiftKey:
                    case Keys.Insert:
                        if (RaiseKeyUp(keyData))
                            return true;
                        break;
                }

                switch (keyData)
                {
                    case Keys.ShiftKey:
                        _shiftDown = false;
                        return true;
                    case Keys.Insert:
                        return PreProcessWmKeyUp_Insert(ref m);
                    default:
                        return _hexBox.BasePreProcessMessage(ref m);
                }
            }

            protected virtual bool PreProcessWmKeyUp_Insert(ref Message m)
            {
                _hexBox.InsertActive = !_hexBox.InsertActive;
                return true;
            }

            protected bool RaiseKeyUp(Keys keyData)
            {
                KeyEventArgs e = new KeyEventArgs(keyData);
                _hexBox.OnKeyUp(e);
                return e.Handled;
            }
            #endregion

            #region Misc
            Dictionary<Keys, MessageDelegate> MessageHandlers =>
                _messageHandlers ??= new Dictionary<Keys, MessageDelegate>
                {
                    { Keys.Left, PreProcessWmKeyDown_Left }, // move left
                    { Keys.Up, PreProcessWmKeyDown_Up }, // move up
                    { Keys.Right, PreProcessWmKeyDown_Right }, // move right
                    { Keys.Down, PreProcessWmKeyDown_Down }, // move down
                    { Keys.PageUp, PreProcessWmKeyDown_PageUp }, // move pageup
                    { Keys.PageDown, PreProcessWmKeyDown_PageDown }, // move page down
                    { Keys.Left | Keys.Shift, PreProcessWmKeyDown_ShiftLeft }, // move left with selection
                    { Keys.Up | Keys.Shift, PreProcessWmKeyDown_ShiftUp }, // move up with selection
                    { Keys.Right | Keys.Shift, PreProcessWmKeyDown_ShiftRight }, // move right with selection
                    { Keys.Down | Keys.Shift, PreProcessWmKeyDown_ShiftDown }, // move down with selection
                    { Keys.Tab, PreProcessWmKeyDown_Tab }, // switch to string view
                    { Keys.Back, PreProcessWmKeyDown_Back }, // back
                    { Keys.Delete, PreProcessWmKeyDown_Delete }, // delete
                    { Keys.Home, PreProcessWmKeyDown_Home }, // move to home
                    { Keys.End, PreProcessWmKeyDown_End }, // move to end
                    { Keys.ShiftKey | Keys.Shift, PreProcessWmKeyDown_ShiftShiftKey }, // begin selection process
                    { Keys.C | Keys.Control, PreProcessWmKeyDown_ControlC }, // copy 
                    { Keys.X | Keys.Control, PreProcessWmKeyDown_ControlX }, // cut
                    { Keys.V | Keys.Control, PreProcessWmKeyDown_ControlV }, // paste
                    { Keys.C | Keys.Control | Keys.Shift, PreProcessWmKeyDown_ControlShiftC }, // copy hex
                    { Keys.V | Keys.Control | Keys.Shift, PreProcessWmKeyDown_ControlShiftV }, // paste hex
                    { Keys.A | Keys.Control, PreProcessWmKeyDown_ControlA } // select all 
                };

            protected virtual bool PerformPosMoveLeft()
            {
                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;
                int cp = _hexBox._byteCharacterPos;

                if (sel != 0)
                {
                    cp = 0;
                    _hexBox.SetPosition(pos, cp);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    if (pos == 0 && cp == 0)
                        return true;

                    if (cp > 0)
                    {
                        cp--;
                    }
                    else
                    {
                        pos = Math.Max(0, pos - 1);
                        cp++;
                    }

                    _hexBox.SetPosition(pos, cp);

                    if (pos < _hexBox._startByte)
                    {
                        _hexBox.PerformScrollLineUp();
                    }
                    _hexBox.UpdateCaret();
                    _hexBox.Invalidate();
                }

                _hexBox.ScrollByteIntoView();
                return true;
            }
            protected virtual bool PerformPosMoveRight()
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;
                long sel = _hexBox._selectionLength;

                if (sel != 0)
                {
                    pos += sel;
                    cp = 0;
                    _hexBox.SetPosition(pos, cp);
                    _hexBox.ReleaseSelection();
                }
                else
                {
                    if (!(pos == _hexBox._byteProvider.Length && cp == 0))
                    {
                        if (cp > 0)
                        {
                            pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
                            cp = 0;
                        }
                        else
                        {
                            cp++;
                        }

                        _hexBox.SetPosition(pos, cp);

                        if (pos > _hexBox._endByte - 1)
                        {
                            _hexBox.PerformScrollLineDown();
                        }
                        _hexBox.UpdateCaret();
                        _hexBox.Invalidate();
                    }
                }

                _hexBox.ScrollByteIntoView();
                return true;
            }
            protected virtual bool PerformPosMoveLeftByte()
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos == 0)
                    return true;

                pos = Math.Max(0, pos - 1);
                cp = 0;

                _hexBox.SetPosition(pos, cp);

                if (pos < _hexBox._startByte)
                {
                    _hexBox.PerformScrollLineUp();
                }
                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.Invalidate();

                return true;
            }

            protected virtual bool PerformPosMoveRightByte()
            {
                long pos = _hexBox._bytePos;
                int cp = _hexBox._byteCharacterPos;

                if (pos == _hexBox._byteProvider.Length)
                    return true;

                pos = Math.Min(_hexBox._byteProvider.Length, pos + 1);
                cp = 0;

                _hexBox.SetPosition(pos, cp);

                if (pos > _hexBox._endByte - 1)
                {
                    _hexBox.PerformScrollLineDown();
                }
                _hexBox.UpdateCaret();
                _hexBox.ScrollByteIntoView();
                _hexBox.Invalidate();

                return true;
            }

            public virtual PointF GetCaretPointF(long byteIndex)
            {
                //System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "KeyInterpreter");

                return _hexBox.GetBytePointF(byteIndex);
            }

            protected virtual BytePositionInfo GetBytePositionInfo(Point p)
            {
                return _hexBox.GetHexBytePositionInfo(p);
            }
            #endregion
        }
        #endregion

        #region StringKeyInterpreter class
        /// <summary>
        /// Handles user input such as mouse and keyboard input during string view edit
        /// </summary>
        class StringKeyInterpreter : KeyInterpreter
        {
            #region Ctors
            public StringKeyInterpreter(HexBox hexBox)
                : base(hexBox)
            {
                _hexBox._byteCharacterPos = 0;
            }
            #endregion

            #region PreProcessWmKeyDown methods
            public override bool PreProcessWmKeyDown(ref Message m)
            {
                Keys vc = (Keys)m.WParam.ToInt32();

                Keys keyData = vc | Control.ModifierKeys;

                switch (keyData)
                {
                    case Keys.Tab | Keys.Shift:
                    case Keys.Tab:
                        if (RaiseKeyDown(keyData))
                            return true;
                        break;
                }

                return keyData switch
                {
                    Keys.Tab | Keys.Shift => PreProcessWmKeyDown_ShiftTab(ref m),
                    Keys.Tab => PreProcessWmKeyDown_Tab(ref m),
                    _ => base.PreProcessWmKeyDown(ref m),
                };
            }

            protected override bool PreProcessWmKeyDown_Left(ref Message m)
            {
                return PerformPosMoveLeftByte();
            }

            protected override bool PreProcessWmKeyDown_Right(ref Message m)
            {
                return PerformPosMoveRightByte();
            }

            #endregion

            #region PreProcessWmChar methods
            public override bool PreProcessWmChar(ref Message m)
            {
                if (Control.ModifierKeys == Keys.Control)
                {
                    return _hexBox.BasePreProcessMessage(ref m);
                }

                bool sw = _hexBox._byteProvider.SupportsWriteByte();
                bool si = _hexBox._byteProvider.SupportsInsertBytes();
                bool sd = _hexBox._byteProvider.SupportsDeleteBytes();

                long pos = _hexBox._bytePos;
                long sel = _hexBox._selectionLength;
                int cp = _hexBox._byteCharacterPos;

                if (
                    (!sw && pos != _hexBox._byteProvider.Length) ||
                    (!si && pos == _hexBox._byteProvider.Length))
                {
                    return _hexBox.BasePreProcessMessage(ref m);
                }

                char c = (char)m.WParam.ToInt32();

                if (RaiseKeyPress(c))
                    return true;

                if (_hexBox.ReadOnly)
                    return true;

                bool isInsertMode = (pos == _hexBox._byteProvider.Length);

                // do insert when insertActive = true
                if (!isInsertMode && si && _hexBox.InsertActive)
                    isInsertMode = true;

                if (sd && si && sel > 0)
                {
                    _hexBox._byteProvider.DeleteBytes(pos, sel);
                    isInsertMode = true;
                    cp = 0;
                    _hexBox.SetPosition(pos, cp);
                }

                _hexBox.ReleaseSelection();

                byte b = _hexBox.ByteCharConverter.ToByte(c);
                if (isInsertMode)
                    _hexBox._byteProvider.InsertBytes(pos, [b]);
                else
                    _hexBox._byteProvider.WriteByte(pos, b);

                PerformPosMoveRightByte();
                _hexBox.Invalidate();

                return true;
            }
            #endregion

            #region Misc
            public override PointF GetCaretPointF(long byteIndex)
            {
                //System.Diagnostics.Debug.WriteLine("GetCaretPointF()", "StringKeyInterpreter");

                Point gp = _hexBox.GetGridBytePoint(byteIndex);
                return _hexBox.GetByteStringPointF(gp);
            }

            protected override BytePositionInfo GetBytePositionInfo(Point p)
            {
                return _hexBox.GetStringBytePositionInfo(p);
            }
            #endregion
        }
        #endregion

        #region Fields
        /// <summary>
        /// Contains the hole content bounds of all text
        /// </summary>
        Rectangle _recContent;
        /// <summary>
        /// Contains the line info bounds
        /// </summary>
        Rectangle _recLineInfo;
        /// <summary>
        /// Contains the column info header rectangle bounds
        /// </summary>
        Rectangle _recColumnInfo;
        /// <summary>
        /// Contains the hex data bounds
        /// </summary>
        Rectangle _recHex;
        /// <summary>
        /// Contains the string view bounds
        /// </summary>
        Rectangle _recStringView;

        /// <summary>
        /// Contains string format information for text drawing
        /// </summary>
        StringFormat _stringFormat;
        /// <summary>
        /// Contains the maximum of visible horizontal bytes
        /// </summary>
        int _iHexMaxHBytes;
        /// <summary>
        /// Contains the maximum of visible vertical bytes
        /// </summary>
        int _iHexMaxVBytes;
        /// <summary>
        /// Contains the maximum of visible bytes.
        /// </summary>
        int _iHexMaxBytes;

        /// <summary>
        /// Contains the number of digits shown in the line info
        /// </summary>
        int _lineInfoNumDigits = 6;
        const int _lineInfoMinDigits = 2;
        /// <summary>
        /// Contains the scroll bars minimum value
        /// </summary>
        long _scrollVmin;
        /// <summary>
        /// Contains the scroll bars maximum value
        /// </summary>
        long _scrollVmax;
        /// <summary>
        /// Contains the scroll bars current position
        /// </summary>
        long _scrollVpos;
        /// <summary>
        /// Contains a vertical scroll
        /// </summary>
        VScrollBar _vScrollBar;
        /// <summary>
        /// Contains a timer for thumbtrack scrolling
        /// </summary>
        Timer _thumbTrackTimer = new Timer();
        /// <summary>
        /// Contains the thumbtrack scrolling position
        /// </summary>
        long _thumbTrackPosition;
        /// <summary>
        /// Contains the thumptrack delay for scrolling in milliseconds.
        /// </summary>
        const int THUMPTRACKDELAY = 50;
        /// <summary>
        /// Contains the Enviroment.TickCount of the last refresh
        /// </summary>
        int _lastThumbtrack;
        /// <summary>
        /// Contains the border�s left shift
        /// </summary>
        int _recBorderLeft = SystemInformation.Border3DSize.Width;
        /// <summary>
        /// Contains the border�s right shift
        /// </summary>
        int _recBorderRight = SystemInformation.Border3DSize.Width;
        /// <summary>
        /// Contains the border�s top shift
        /// </summary>
        int _recBorderTop = SystemInformation.Border3DSize.Height;
        /// <summary>
        /// Contains the border bottom shift
        /// </summary>
        int _recBorderBottom = SystemInformation.Border3DSize.Height;

        /// <summary>
        /// Contains the index of the first visible byte
        /// </summary>
        long _startByte;
        /// <summary>
        /// Contains the index of the last visible byte
        /// </summary>
        long _endByte;

        /// <summary>
        /// Contains the current byte position
        /// </summary>
        long _bytePos = -1;
        /// <summary>
        /// Contains the current char position in one byte
        /// </summary>
        /// <example>
        /// "1A"
        /// "1" = char position of 0
        /// "A" = char position of 1
        /// </example>
        int _byteCharacterPos;

        /// <summary>
        /// Contains string format information for hex values
        /// </summary>
        string _hexStringFormat = "X";

        /// <summary>
        /// Contains the current key interpreter
        /// </summary>
        IKeyInterpreter _keyInterpreter;
        /// <summary>
        /// Contains an empty key interpreter without functionality
        /// </summary>
        EmptyKeyInterpreter _eki;
        /// <summary>
        /// Contains the default key interpreter
        /// </summary>
        KeyInterpreter _ki;
        /// <summary>
        /// Contains the string key interpreter
        /// </summary>
        StringKeyInterpreter _ski;

        /// <summary>
        /// Contains True if caret is visible
        /// </summary>
        bool _caretVisible;

        /// <summary>
        /// Contains true, if the find (Find method) should be aborted.
        /// </summary>
        bool _abortFind;
        /// <summary>
        /// Contains a value of the current finding position.
        /// </summary>
        long _findingPos;

        /// <summary>
        /// Contains a state value about Insert or Write mode. When this value is true and the ByteProvider SupportsInsert is true bytes are inserted instead of overridden.
        /// </summary>
        bool _insertActive;

        /// <summary>
        /// Hightlight region entry
        /// </summary>
        public class HighlightRegion : IEquatable<HighlightRegion>
        {
            /// <summary>
            /// Hightlight region start index
            /// </summary>
            public long Start { get; set; }

            /// <summary>
            /// Hightlight region end index
            /// </summary>
            public long End { get; set; }

            /// <summary>
            /// Hightlight region foreground color
            /// </summary>
            public Color ForeColor { get; set; }

            /// <summary>
            /// Hightlight region background color
            /// </summary>
            public Color BackColor { get; set; }

            /// <summary>
            /// Hightlight region label
            /// </summary>
            public String Label { get; set; }

            /// <summary>
            /// Checks hightlight region within position index
            /// </summary>
            public bool IsWithin(long t)
            {
                return t >= Start && t <= End;
            }

            /// <summary>
            /// Merge two hightlight regions
            /// </summary>
            public void Merge(HighlightRegion other)
            {
                Start = Math.Min(Start, other.Start);
                End = Math.Max(End, other.End);
            }

            /// <summary>
            /// Checks if hightlight regions overlap
            /// </summary>
            public bool Overlaps(HighlightRegion other)
            {
                return IsWithin(other.Start) || IsWithin(other.End) || other.IsWithin(Start);
            }

            /// <summary>
            /// Checks two hightlight regions have same coordinates
            /// </summary>
            public bool Equals(HighlightRegion other)
            {
                return other.Start == Start && other.End == End;
            }
        }

        /// <summary>
        /// Hightlighted regions
        /// </summary>
        List<HighlightRegion> _highlightRegions = [];

        /// <summary>
        /// Seleced region
        /// </summary>
        HighlightRegion _selectedRegion;

        #endregion

        #region Events
        /// <summary>
        /// Occurs, when the value of InsertActive property has changed.
        /// </summary>
        [Description("Occurs, when the value of InsertActive property has changed.")]
        public event EventHandler InsertActiveChanged;
        /// <summary>
        /// Occurs, when the value of ReadOnly property has changed.
        /// </summary>
        [Description("Occurs, when the value of ReadOnly property has changed.")]
        public event EventHandler ReadOnlyChanged;
        /// <summary>
        /// Occurs, when the value of ByteProvider property has changed.
        /// </summary>
        [Description("Occurs, when the value of ByteProvider property has changed.")]
        public event EventHandler ByteProviderChanged;
        /// <summary>
        /// Occurs, when the value of SelectionStart property has changed.
        /// </summary>
        [Description("Occurs, when the value of SelectionStart property has changed.")]
        public event EventHandler SelectionStartChanged;
        /// <summary>
        /// Occurs, when the value of SelectionLength property has changed.
        /// </summary>
        [Description("Occurs, when the value of SelectionLength property has changed.")]
        public event EventHandler SelectionLengthChanged;
        /// <summary>
        /// Occurs, when the value of LineInfoVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of LineInfoVisible property has changed.")]
        public event EventHandler LineInfoVisibleChanged;
        /// <summary>
        /// Occurs, when the value of ColumnInfoVisibleChanged property has changed.
        /// </summary>
        [Description("Occurs, when the value of ColumnInfoVisibleChanged property has changed.")]
        public event EventHandler ColumnInfoVisibleChanged;
        /// <summary>
        /// Occurs, when the value of GroupSeparatorVisibleChanged property has changed.
        /// </summary>
        [Description("Occurs, when the value of GroupSeparatorVisibleChanged property has changed.")]
        public event EventHandler GroupSeparatorVisibleChanged;
        /// <summary>
        /// Occurs, when the value of StringViewVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of StringViewVisible property has changed.")]
        public event EventHandler StringViewVisibleChanged;
        /// <summary>
        /// Occurs, when the value of BorderStyle property has changed.
        /// </summary>
        [Description("Occurs, when the value of BorderStyle property has changed.")]
        public event EventHandler BorderStyleChanged;
        /// <summary>
        /// Occurs, when the value of ColumnWidth property has changed.
        /// </summary>
        [Description("Occurs, when the value of GroupSize property has changed.")]
        public event EventHandler GroupSizeChanged;
        /// <summary>
        /// Occurs, when the value of BytesPerLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of BytesPerLine property has changed.")]
        public event EventHandler BytesPerLineChanged;
        /// <summary>
        /// Occurs, when the value of UseFixedBytesPerLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of UseFixedBytesPerLine property has changed.")]
        public event EventHandler UseFixedBytesPerLineChanged;
        /// <summary>
        /// Occurs, when the value of VScrollBarVisible property has changed.
        /// </summary>
        [Description("Occurs, when the value of VScrollBarVisible property has changed.")]
        public event EventHandler VScrollBarVisibleChanged;
        /// <summary>
        /// Occurs, when the value of HexCasing property has changed.
        /// </summary>
        [Description("Occurs, when the value of HexCasing property has changed.")]
        public event EventHandler HexCasingChanged;
        /// <summary>
        /// Occurs, when the value of HorizontalByteCount property has changed.
        /// </summary>
        [Description("Occurs, when the value of HorizontalByteCount property has changed.")]
        public event EventHandler HorizontalByteCountChanged;
        /// <summary>
        /// Occurs, when the value of VerticalByteCount property has changed.
        /// </summary>
        [Description("Occurs, when the value of VerticalByteCount property has changed.")]
        public event EventHandler VerticalByteCountChanged;
        /// <summary>
        /// Occurs, when the value of CurrentLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of CurrentLine property has changed.")]
        public event EventHandler CurrentLineChanged;
        /// <summary>
        /// Occurs, when the value of CurrentPositionInLine property has changed.
        /// </summary>
        [Description("Occurs, when the value of CurrentPositionInLine property has changed.")]
        public event EventHandler CurrentPositionInLineChanged;
        /// <summary>
        /// Occurs, when Copy method was invoked and ClipBoardData changed.
        /// </summary>
        [Description("Occurs, when Copy method was invoked and ClipBoardData changed.")]
        public event EventHandler Copied;
        /// <summary>
        /// Occurs, when CopyHex method was invoked and ClipBoardData changed.
        /// </summary>
        [Description("Occurs, when CopyHex method was invoked and ClipBoardData changed.")]
        public event EventHandler CopiedHex;
        /// <summary>
        /// Occurs, when the CharSize property has changed
        /// </summary>
        [Description("Occurs, when the CharSize property has changed")]
        public event EventHandler CharSizeChanged;
        /// <summary>
        /// Occurs, when the RequiredWidth property changes
        /// </summary>
        [Description("Occurs, when the RequiredWidth property changes")]
        public event EventHandler RequiredWidthChanged;
        /// <summary>
        /// Occurs, when the highlight region is added
        /// </summary>
        [Description("Occurs, when the highlight region is added")]
        public event EventHandler HighlightRegionAdded;
        /// <summary>
        /// Occurs, when the highlight region is selected
        /// </summary>
        [Description("Occurs, when the highlight region is selected")]
        public event EventHandler HighlightRegionSelected;
        #endregion

        #region Ctors

        /// <summary>
        /// Initializes a new instance of a HexBox class.
        /// </summary>
        public HexBox()
        {
            this.MaxBytesPerLine = 0x64; //default absolute max.
            this._vScrollBar = new VScrollBar();
            this._vScrollBar.Scroll += _vScrollBar_Scroll;

            this._builtInContextMenu = new BuiltInContextMenu(this);

            Font = SystemFonts.MessageBoxFont;
            _stringFormat = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.MeasureTrailingSpaces
            };

            ActivateEmptyKeyInterpreter();

            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.ResizeRedraw, true);

            _thumbTrackTimer.Interval = 50;
            _thumbTrackTimer.Tick += new EventHandler(PerformScrollThumbTrack);
        }

        #endregion

        #region Scroll methods
        void _vScrollBar_Scroll(object sender, ScrollEventArgs e)
        {
            switch (e.Type)
            {
                case ScrollEventType.Last:
                    break;
                case ScrollEventType.EndScroll:
                    break;
                case ScrollEventType.SmallIncrement:
                    PerformScrollLineDown();
                    break;
                case ScrollEventType.SmallDecrement:
                    PerformScrollLineUp();
                    break;
                case ScrollEventType.LargeIncrement:
                    PerformScrollPageDown();
                    break;
                case ScrollEventType.LargeDecrement:
                    PerformScrollPageUp();
                    break;
                case ScrollEventType.ThumbPosition:
                    long lPos = FromScrollPos(e.NewValue);
                    PerformScrollThumbPosition(lPos);
                    break;
                case ScrollEventType.ThumbTrack:
                    // to avoid performance problems use a refresh delay implemented with a timer
                    if (_thumbTrackTimer.Enabled) // stop old timer
                        _thumbTrackTimer.Enabled = false;

                    // perform scroll immediately only if last refresh is very old
                    int currentThumbTrack = System.Environment.TickCount;
                    if (currentThumbTrack - _lastThumbtrack > THUMPTRACKDELAY)
                    {
                        PerformScrollThumbTrack(null, null);
                        _lastThumbtrack = currentThumbTrack;
                        break;
                    }

                    // start thumbtrack timer 
                    _thumbTrackPosition = FromScrollPos(e.NewValue);
                    _thumbTrackTimer.Enabled = true;
                    break;
                case ScrollEventType.First:
                    break;
                default:
                    break;
            }

            e.NewValue = ToScrollPos(_scrollVpos);
        }

        /// <summary>
        /// Performs the thumbtrack scrolling after an delay.
        /// </summary>
        void PerformScrollThumbTrack(object sender, EventArgs e)
        {
            _thumbTrackTimer.Enabled = false;
            PerformScrollThumbPosition(_thumbTrackPosition);
            _lastThumbtrack = Environment.TickCount;
        }

        void UpdateScrollSize()
        {
            //System.Diagnostics.Debug.WriteLine("UpdateScrollSize()", "HexBox");

            // calc scroll bar info
            if (VScrollBarVisible && _byteProvider != null && _byteProvider.Length > 0 && _iHexMaxHBytes != 0)
            {
                long scrollmax = (long)Math.Ceiling((double)(_byteProvider.Length + 1) / (double)_iHexMaxHBytes - (double)_iHexMaxVBytes);
                scrollmax = Math.Max(0, scrollmax);

                if (scrollmax != 0)
                {
                    scrollmax += _vScrollBar.LargeChange - 1;
                }

                long scrollpos = _startByte / _iHexMaxHBytes;

                if (scrollmax < _scrollVmax)
                {
                    /* Data size has been decreased. */
                    if (_scrollVpos == _scrollVmax)
                        /* Scroll one line up if we at bottom. */
                        PerformScrollLineUp();
                }

                if (scrollmax == _scrollVmax && scrollpos == _scrollVpos)
                    return;

                _scrollVmin = 0;
                _scrollVmax = scrollmax;
                _scrollVpos = Math.Min(scrollpos, scrollmax);
                UpdateVScroll();
            }
            else if (VScrollBarVisible)
            {
                // disable scroll bar
                _scrollVmin = 0;
                _scrollVmax = 0;
                _scrollVpos = 0;
                UpdateVScroll();
            }
        }

        void UpdateVScroll()
        {
            //System.Diagnostics.Debug.WriteLine("UpdateVScroll()", "HexBox");

            int max = ToScrollMax(_scrollVmax);

            if (max > 0)
            {
                _vScrollBar.Minimum = 0;
                _vScrollBar.Maximum = max;
                _vScrollBar.Value = ToScrollPos(_scrollVpos);
                _vScrollBar.Visible = true;
            }
            else
            {
                _vScrollBar.Visible = false;
            }
        }

        int ToScrollPos(long value)
        {
            int max = 65535;

            if (_scrollVmax < max)
                return (int)value;
            else
            {
                double valperc = (double)value / (double)_scrollVmax * (double)100;
                int res = (int)Math.Floor((double)max / (double)100 * valperc);
                res = (int)Math.Max(_scrollVmin, res);
                res = (int)Math.Min(_scrollVmax, res);
                return res;
            }
        }

        long FromScrollPos(int value)
        {
            int max = 65535;
            if (_scrollVmax < max)
            {
                return (long)value;
            }
            else
            {
                double valperc = (double)value / (double)max * (double)100;
                long res = (int)Math.Floor((double)_scrollVmax / (double)100 * valperc);
                return res;
            }
        }

        int ToScrollMax(long value)
        {
            long max = 65535;
            if (value > max)
                return (int)max;
            else
                return (int)value;
        }

        void PerformScrollToLine(long pos)
        {
            if (pos < _scrollVmin || pos > _scrollVmax || pos == _scrollVpos)
                return;

            _scrollVpos = pos;

            UpdateVScroll();
            UpdateVisibilityBytes();
            UpdateCaret();
            Invalidate();
        }

        
        /// <summary>
        /// Scrolls the view up or down the indicates amount of lines.
        /// </summary>
        /// <param name="lines">Number of lines to scroll. Negative numbers scroll up, positive scrolls down.</param>
        void PerformScrollLines(int lines)
        {
            long pos;
            if (lines > 0)
            {
                pos = Math.Min(_scrollVmax - (_vScrollBar.LargeChange - 1), _scrollVpos + lines);
            }
            else if (lines < 0)
            {
                pos = Math.Max(_scrollVmin, _scrollVpos + lines);
            }
            else
            {
                return;
            }

            PerformScrollToLine(pos);
        }

        void PerformScrollLineDown()
        {
            this.PerformScrollLines(1);
        }

        void PerformScrollLineUp()
        {
            this.PerformScrollLines(-1);
        }

        void PerformScrollPageDown()
        {
            this.PerformScrollLines(_iHexMaxVBytes);
        }

        void PerformScrollPageUp()
        {
            this.PerformScrollLines(-_iHexMaxVBytes);
        }

        void PerformScrollThumbPosition(long pos)
        {
            // Bug fix: Scroll to end, do not scroll to end
            int difference = (_scrollVmax > 65535) ? 10 : 9;

            if (ToScrollPos(pos) == ToScrollMax(_scrollVmax) - difference)
                pos = _scrollVmax;
            // End Bug fix

            pos = Math.Min(pos, _scrollVmax - (_vScrollBar.LargeChange - 1));
            PerformScrollToLine(pos);
        }

        /// <summary>
        /// Scrolls the selection start byte into view
        /// </summary>
        public void ScrollByteIntoView()
        {
            //System.Diagnostics.Debug.WriteLine("ScrollByteIntoView()", "HexBox");

            ScrollByteIntoView(_bytePos);
        }

        /// <summary>
        /// Scrolls the specific byte into view
        /// </summary>
        /// <param name="index">the index of the byte</param>
        public void ScrollByteIntoView(long index)
        {
            //System.Diagnostics.Debug.WriteLine("ScrollByteIntoView(long index)", "HexBox");

            if (_byteProvider == null || _keyInterpreter == null)
                return;

            if (index < _startByte)
            {
                long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
                PerformScrollThumbPosition(line);
            }
            else if (index > _endByte)
            {
                long line = (long)Math.Floor((double)index / (double)_iHexMaxHBytes);
                line -= _iHexMaxVBytes - 1;
                PerformScrollThumbPosition(line);
            }
        }
        #endregion

        #region Selection methods
        void ReleaseSelection()
        {
            //System.Diagnostics.Debug.WriteLine("ReleaseSelection()", "HexBox");

            if (_selectionLength == 0)
                return;
            _selectionLength = 0;
            OnSelectionLengthChanged(EventArgs.Empty);

            if (!_caretVisible)
                CreateCaret();
            else
                UpdateCaret();

            Invalidate();
        }

        /// <summary>
        /// Returns true if Select method could be invoked.
        /// </summary>
        public bool CanSelectAll()
        {
            if (!this.Enabled)
                return false;
            if (_byteProvider == null)
                return false;

            return true;
        }

        /// <summary>
        /// Selects all bytes.
        /// </summary>
        public void SelectAll()
        {
            if (this.ByteProvider == null)
                return;
            this.Select(0, this.ByteProvider.Length);
        }

        /// <summary>
        /// Selects the hex box.
        /// </summary>
        /// <param name="start">the start index of the selection</param>
        /// <param name="length">the length of the selection</param>
        public void Select(long start, long length)
        {
            if (this.ByteProvider == null)
                return;
            if (!this.Enabled)
                return;

            InternalSelect(start, length);
            ScrollByteIntoView();
        }

        void InternalSelect(long start, long length)
        {
            long pos = start;
            long sel = length;
            int cp = 0;

            if (sel > 0 && _caretVisible)
                DestroyCaret();
            else if (sel == 0 && !_caretVisible)
                CreateCaret();

            SetPosition(pos, cp);
            SetSelectionLength(sel);

            UpdateCaret();
            Invalidate();
        }
        #endregion

        #region Key interpreter methods
        void ActivateEmptyKeyInterpreter()
        {
            if (_eki == null)
                _eki = new EmptyKeyInterpreter(this);

            if (_eki == _keyInterpreter)
                return;

            _keyInterpreter?.Deactivate();

            _keyInterpreter = _eki;
            _keyInterpreter.Activate();
        }

        void ActivateKeyInterpreter()
        {
            if (_ki == null)
                _ki = new KeyInterpreter(this);

            if (_ki == _keyInterpreter)
                return;

            _keyInterpreter?.Deactivate();

            _keyInterpreter = _ki;
            _keyInterpreter.Activate();
        }

        void ActivateStringKeyInterpreter()
        {
            if (_ski == null)
                _ski = new StringKeyInterpreter(this);

            if (_ski == _keyInterpreter)
                return;

            _keyInterpreter?.Deactivate();

            _keyInterpreter = _ski;
            _keyInterpreter.Activate();
        }
        #endregion

        #region Caret methods
        void CreateCaret()
        {
            if (_byteProvider == null || _keyInterpreter == null || _caretVisible || !this.Focused)
                return;

            //System.Diagnostics.Debug.WriteLine("CreateCaret()", "HexBox");

            // define the caret width depending on InsertActive mode
            int caretWidth = (this.InsertActive) ? 1 : (int)_charSize.Width;
            int caretHeight = (int)_charSize.Height;
            NativeMethods.CreateCaret(Handle, IntPtr.Zero, caretWidth, caretHeight);

            UpdateCaret();
            NativeMethods.ShowCaret(Handle);
            _caretVisible = true;
        }

        void UpdateCaret()
        {
            if (_byteProvider == null || _keyInterpreter == null)
                return;

            //System.Diagnostics.Debug.WriteLine("UpdateCaret()", "HexBox");

            long byteIndex = _bytePos - _startByte;
            PointF p = _keyInterpreter.GetCaretPointF(byteIndex);
            p.X += _byteCharacterPos * _charSize.Width;
            NativeMethods.SetCaretPos((int)p.X, (int)p.Y);
        }

        void DestroyCaret()
        {
            if (!_caretVisible)
                return;

            //System.Diagnostics.Debug.WriteLine("DestroyCaret()", "HexBox");

            NativeMethods.DestroyCaret();
            _caretVisible = false;
        }

        void SetCaretPosition(Point p)
        {
            //System.Diagnostics.Debug.WriteLine("SetCaretPosition()", "HexBox");

            if (_byteProvider == null || _keyInterpreter == null)
                return;

            long pos = _bytePos;
            int cp = _byteCharacterPos;

            if (_recHex.Contains(p))
            {
                BytePositionInfo bpi = GetHexBytePositionInfo(p);
                pos = bpi.Index;
                cp = bpi.CharacterPosition;

                SetPosition(pos, cp);

                ActivateKeyInterpreter();
                UpdateCaret();
                Invalidate();
            }
            else if (_recStringView.Contains(p))
            {
                BytePositionInfo bpi = GetStringBytePositionInfo(p);
                pos = bpi.Index;
                cp = bpi.CharacterPosition;

                SetPosition(pos, cp);

                ActivateStringKeyInterpreter();
                UpdateCaret();
                Invalidate();
            }
        }

        BytePositionInfo GetHexBytePositionInfo(Point p)
        {
            //System.Diagnostics.Debug.WriteLine("GetHexBytePositionInfo()", "HexBox");

            float x = ((float)(p.X - _recHex.X) / _charSize.Width);
            float y = ((float)(p.Y - _recHex.Y) / _charSize.Height);
            int iX = (int)x;
            int iY = (int)y;

            int hPos = (iX / 3 + 1);

            long bytePos = Math.Min(_byteProvider.Length,
                _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos - 1);
            int byteCharaterPos = (iX % 3);
            if (byteCharaterPos > 1)
                byteCharaterPos = 1;

            if (bytePos == _byteProvider.Length)
                byteCharaterPos = 0;

            if (bytePos < 0)
                return new BytePositionInfo(0, 0);
            return new BytePositionInfo(bytePos, byteCharaterPos);
        }

        BytePositionInfo GetStringBytePositionInfo(Point p)
        {
            //System.Diagnostics.Debug.WriteLine("GetStringBytePositionInfo()", "HexBox");

            float x = ((float)(p.X - _recStringView.X) / _charSize.Width);
            float y = ((float)(p.Y - _recStringView.Y) / _charSize.Height);
            int iX = (int)x;
            int iY = (int)y;

            int hPos = iX + 1;

            long bytePos = Math.Min(_byteProvider.Length,
                _startByte + (_iHexMaxHBytes * (iY + 1) - _iHexMaxHBytes) + hPos - 1);
            int byteCharacterPos = 0;

            if (bytePos < 0)
                return new BytePositionInfo(0, 0);
            return new BytePositionInfo(bytePos, byteCharacterPos);
        }
        #endregion

        #region PreProcessMessage methods
        /// <summary>
        /// Preprocesses windows messages.
        /// </summary>
        /// <param name="m">the message to process.</param>
        /// <returns>true, if the message was processed</returns>
        public override bool PreProcessMessage(ref Message m)
        {
            return m.Msg switch
            {
                NativeMethods.WM_KEYDOWN => _keyInterpreter.PreProcessWmKeyDown(ref m),
                NativeMethods.WM_CHAR => _keyInterpreter.PreProcessWmChar(ref m),
                NativeMethods.WM_KEYUP => _keyInterpreter.PreProcessWmKeyUp(ref m),
                _ => base.PreProcessMessage(ref m),
            };
        }

        bool BasePreProcessMessage(ref Message m)
        {
            return base.PreProcessMessage(ref m);
        }
        #endregion

        #region Find methods
        /// <summary>
        /// Searches the current ByteProvider
        /// </summary>
        /// <param name="options">contains all find options</param>
        /// <returns>the SelectionStart property value if find was successfull or
        /// -1 if there is no match
        /// -2 if Find was aborted.</returns>
        public long Find(FindOptions options)
        {
            var startIndex = this.SelectionStart + this.SelectionLength;
            int match = 0;

            byte[] buffer1 = null;
            byte[] buffer2 = null;
            if (options.Type == FindType.Text && options.MatchCase)
            {
                if (options.FindBuffer == null || options.FindBuffer.Length == 0)
                    throw new ArgumentException("FindBuffer can not be null when Type: Text and MatchCase: false");
                buffer1 = options.FindBuffer;
            }
            else if (options.Type == FindType.Text && !options.MatchCase)
            {
                if (options.FindBufferLowerCase == null || options.FindBufferLowerCase.Length == 0)
                    throw new ArgumentException("FindBufferLowerCase can not be null when Type is Text and MatchCase is true");
                if (options.FindBufferUpperCase == null || options.FindBufferUpperCase.Length == 0)
                    throw new ArgumentException("FindBufferUpperCase can not be null when Type is Text and MatchCase is true");
                if (options.FindBufferLowerCase.Length != options.FindBufferUpperCase.Length)
                    throw new ArgumentException("FindBufferUpperCase and FindBufferUpperCase must have the same size when Type is Text and MatchCase is true");
                buffer1 = options.FindBufferLowerCase;
                buffer2 = options.FindBufferUpperCase;
            }
            else if (options.Type == FindType.Hex)
            {
                if (options.Hex == null || options.Hex.Length == 0)
                    throw new ArgumentException("Hex can not be null when Type is Hex");
                buffer1 = options.Hex;
            }

            int buffer1Length = buffer1.Length;

            _abortFind = false;

            for (long pos = startIndex; pos < _byteProvider.Length; pos++)
            {
                if (_abortFind)
                    return -2;

                if (pos % 1000 == 0) // for performance reasons: DoEvents only 1 times per 1000 loops
                    Application.DoEvents();

                byte compareByte = _byteProvider.ReadByte(pos);
                bool buffer1Match = compareByte == buffer1[match];
                bool hasBuffer2 = buffer2 != null;
                bool buffer2Match = hasBuffer2 && compareByte == buffer2[match];
                bool isMatch = buffer1Match || buffer2Match;
                if (!isMatch)
                {
                    pos -= match;
                    match = 0;
                    _findingPos = pos;
                    continue;
                }

                match++;

                if (match == buffer1Length)
                {
                    long bytePos = pos - buffer1Length + 1;
                    Select(bytePos, buffer1Length);
                    ScrollByteIntoView(_bytePos + _selectionLength);
                    ScrollByteIntoView(_bytePos);

                    return bytePos;
                }
            }

            return -1;
        }

        /// <summary>
        /// Aborts a working Find method.
        /// </summary>
        public void AbortFind()
        {
            _abortFind = true;
        }

        /// <summary>
        /// Gets a value that indicates the current position during Find method execution.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentFindingPosition
        {
            get
            {
                return _findingPos;
            }
        }
        #endregion

        #region Copy, Cut and Paste methods
        byte[] GetCopyData()
        {
            if (!CanCopy()) return [];

            // put bytes into buffer
            byte[] buffer = new byte[_selectionLength];
            int id = -1;
            for (long i = _bytePos; i < _bytePos + _selectionLength; i++)
            {
                id++;

                buffer[id] = _byteProvider.ReadByte(i);
            }
            return buffer;
        }

        const string binarydataCopyFormatString = "HexboxBinData";

        /// <summary>
        /// Copies the current selection in the hex box to the Clipboard.
        /// </summary>
        public void Copy()
        {
            if (!CanCopy()) return;

            // put bytes into buffer
            byte[] buffer = GetCopyData();

            // set string buffer clipboard data
            string sBuffer;
            if (_keyInterpreter == _ki)
            {
                //selection is in hex section
                sBuffer = string.Create(buffer.Length * 2, buffer, static (span, bytes) =>
                {
                    //contrary to appearances, this array is static, not allocated every time (true of all arrays of primitives assigned to ReadOnlySpans)
                    ReadOnlySpan<char> hexChars = ['0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'];
                    ref readonly char hexCharsRef = ref hexChars.GetPinnableReference();
                    //Unsafe.Add instead of indexer is to avoid bounds checking 
                    for (int i = 0, j = 0; i < bytes.Length; i++)
                    {
                        byte value = bytes[i];
                        int digit = value >> 4;
                        span[j++] = Unsafe.Add(ref Unsafe.AsRef(in hexCharsRef), digit);
                        digit = value & 0xF;
                        span[j++] = Unsafe.Add(ref Unsafe.AsRef(in hexCharsRef), digit);
                    }
                });
            }
            else
            {
                //selection is in ascii section
                sBuffer = Encoding.ASCII.GetString(buffer, 0, buffer.Length);
            }

            var da = new DataObject();

            //set memorystream (BinaryData) clipboard data
            var ms = new System.IO.MemoryStream(buffer, 0, buffer.Length, false, true);
            da.SetData(binarydataCopyFormatString, ms);

            da.SetData(typeof(string), sBuffer);

            Clipboard.SetDataObject(da, true);
            UpdateCaret();
            ScrollByteIntoView();
            Invalidate();

            OnCopied(EventArgs.Empty);
        }

        /// <summary>
        /// Return true if Copy method could be invoked.
        /// </summary>
        public bool CanCopy()
        {
            if (_selectionLength < 1 || _byteProvider == null)
                return false;

            return true;
        }

        /// <summary>
        /// Moves the current selection in the hex box to the Clipboard.
        /// </summary>
        public void Cut()
        {
            if (!CanCut()) return;

            Copy();

            _byteProvider.DeleteBytes(_bytePos, _selectionLength);
            _byteCharacterPos = 0;
            UpdateCaret();
            ScrollByteIntoView();
            ReleaseSelection();
            Invalidate();
            Refresh();
        }

        /// <summary>
        /// Return true if Cut method could be invoked.
        /// </summary>
        public bool CanCut()
        {
            if (ReadOnly || !this.Enabled)
                return false;
            if (_byteProvider == null)
                return false;
            if (_selectionLength < 1 || !_byteProvider.SupportsDeleteBytes())
                return false;

            return true;
        }

        [GeneratedRegex("[^0-9A-Fa-f]")]
        private static partial Regex ValidHexRegex();

        /// <summary>
        /// Replaces the current selection in the hex box with the contents of the Clipboard.
        /// </summary>
        public void Paste()
        {
            if (!CanPaste()) return;

            if (_selectionLength > 0)
                _byteProvider.DeleteBytes(_bytePos, _selectionLength);

            byte[] buffer = null;
            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent(binarydataCopyFormatString))
            {
                System.IO.MemoryStream ms = (System.IO.MemoryStream)da.GetData(binarydataCopyFormatString);
                buffer = new byte[ms.Length];
                ms.Read(buffer, 0, buffer.Length);
            }
            else if (da.GetDataPresent(typeof(string)))
            {
                string sBuffer = (string)da.GetData(typeof(string));
                Regex invalidHex = ValidHexRegex();
                bool hexString = !invalidHex.IsMatch(sBuffer) && sBuffer.Length % 2 == 0;
                if (!hexString)
                {
                    string spaceLess = sBuffer.Replace(" ", "");
                    if (!invalidHex.IsMatch(spaceLess) && spaceLess.Length % 2 == 0)
                    {
                        hexString = true;
                        sBuffer = spaceLess;
                    }
                }
                if (hexString && _keyInterpreter == _ki)
                {
                    buffer = new byte[sBuffer.Length / 2];
                    for (int i = 0; i < sBuffer.Length; i += 2)
                    {
                        if (!byte.TryParse(sBuffer.AsSpan(i, 2), NumberStyles.HexNumber, NumberFormatInfo.CurrentInfo, out byte tmp))
                        {
                            buffer = null;
                            break;
                        }
                        buffer[i / 2] = tmp;
                    }
                }
                buffer ??= Encoding.ASCII.GetBytes(sBuffer);
            }
            else
            {
                return;
            }

            _byteProvider.InsertBytes(_bytePos, buffer);

            SetPosition(_bytePos + buffer.Length, 0);

            ReleaseSelection();
            ScrollByteIntoView();
            UpdateCaret();
            Invalidate();
        }

        /// <summary>
        /// Return true if Paste method could be invoked.
        /// </summary>
        public bool CanPaste()
        {
            if (ReadOnly || !this.Enabled) return false;

            if (_byteProvider == null || !_byteProvider.SupportsInsertBytes())
                return false;

            if (!_byteProvider.SupportsDeleteBytes() && _selectionLength > 0)
                return false;

            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent(binarydataCopyFormatString))
                return true;
            else if (da.GetDataPresent(typeof(string)))
                return true;
            else
                return false;
        }
        /// <summary>
        /// Return true if PasteHex method could be invoked.
        /// </summary>
        public bool CanPasteHex()
        {
            if (!CanPaste()) return false;

            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent(typeof(string)))
            {
                string hexString = (string)da.GetData(typeof(string));
                byte[] buffer = ConvertHexToBytes(hexString);
                return (buffer != null);
            }
            return false;
        }

        /// <summary>
        /// Replaces the current selection in the hex box with the hex string data of the Clipboard.
        /// </summary>
        public void PasteHex()
        {
            if (!CanPaste()) return;

            byte[] buffer = null;
            IDataObject da = Clipboard.GetDataObject();
            if (da.GetDataPresent(typeof(string)))
            {
                string hexString = (string)da.GetData(typeof(string));
                buffer = ConvertHexToBytes(hexString);
                if (buffer == null)
                    return;
            }
            else
            {
                return;
            }

            if (_selectionLength > 0)
                _byteProvider.DeleteBytes(_bytePos, _selectionLength);

            _byteProvider.InsertBytes(_bytePos, buffer);

            SetPosition(_bytePos + buffer.Length, 0);

            ReleaseSelection();
            ScrollByteIntoView();
            UpdateCaret();
            Invalidate();
        }

        /// <summary>
        /// Copies the current selection in the hex box to the Clipboard in hex format.
        /// </summary>
        public void CopyHex()
        {
            if (!CanCopy()) return;

            // put bytes into buffer
            byte[] buffer = GetCopyData();

            DataObject da = new DataObject();

            // set string buffer clipbard data
            string hexString = ConvertBytesToHex(buffer);
            da.SetData(typeof(string), hexString);

            //set memorystream (BinaryData) clipboard data
            System.IO.MemoryStream ms = new System.IO.MemoryStream(buffer, 0, buffer.Length, false, true);
            da.SetData(binarydataCopyFormatString, ms);

            Clipboard.SetDataObject(da, true);
            UpdateCaret();
            ScrollByteIntoView();
            Invalidate();

            OnCopiedHex(EventArgs.Empty);
        }

        #endregion

        #region Highlight methods
        /// <summary>
        /// Highlights bytes
        /// </summary>
        /// <param name="start">the start index of the selection</param>
        /// <param name="size">the length of the selection</param>
        /// <param name="fColor">Fore color</param>
        /// <param name="bColor">Back color</param>
        /// <param name="label">Region label</param>
        public void Highlight(long start, long size, Color fColor, Color bColor, String label = null)
        {
            HighlightRegion region = new HighlightRegion()
            {
                Start = start,
                End = start + size - 1,
                ForeColor = fColor,
                BackColor = bColor,
                Label = label
            };

            if (!_highlightRegions.Contains(region))
            {
                HighlightRegion found = null;
                while ((found = _highlightRegions.Find(item => item.Overlaps(region))) != null)
                {
                    region.Merge(found);
                    _highlightRegions.Remove(found);
                }

                _highlightRegions.Add(region);

                OnHighlightRegionAdded(EventArgs.Empty);

                Invalidate();
            }
        }

        /// <summary>
        /// Highlights bytes
        /// </summary>
        /// <param name="start">the start index of the selection</param>
        /// <param name="size">the length of the selection</param>
        public void Highlight(long start, long size)
        {
            Highlight(start, size, HighlightForeColor, HighlightBackColor);
        }

        /// <summary>
        /// Remove highlighted region by position
        /// </summary>
        /// /// <param name="pos">highlight position</param>
        public void Unhighlight(long pos)
        {
            var found = _highlightRegions.Find(item => item.IsWithin(pos));
            if (found != null)
            {
                _highlightRegions.Remove(found);
                Invalidate();
            }
        }

        /// <summary>
        /// Remove highlighted region by the caret position
        /// </summary>
        public void Unhighlight()
        {
            Unhighlight(_bytePos);
        }

        /// <summary>
        /// Removes all highlighted regions
        /// </summary>
        public void UnhighlightAll()
        {
            _highlightRegions.Clear();
            Invalidate();
        }

        /// <summary>
        /// Highlights selected bytes
        /// </summary>
        public void HighlightSelected()
        {
            if (SelectionLength > 0)
            {
                Highlight(SelectionStart, SelectionLength);
            }
        }
        #endregion

        #region Paint methods
        /// <summary>
        /// Paints the background.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected override void OnPaintBackground(PaintEventArgs e)
        {
            switch (_borderStyle)
            {
                case BorderStyle.Fixed3D:
                    {
                        if (TextBoxRenderer.IsSupported)
                        {
                            VisualStyleElement state = VisualStyleElement.TextBox.TextEdit.Normal;
                            Color backColor = this.BackColor;

                            if (this.Enabled)
                            {
                                if (this.ReadOnly)
                                    state = VisualStyleElement.TextBox.TextEdit.ReadOnly;
                                else if (this.Focused)
                                    state = VisualStyleElement.TextBox.TextEdit.Focused;
                            }
                            else
                            {
                                state = VisualStyleElement.TextBox.TextEdit.Disabled;
                                backColor = this.BackColorDisabled;
                            }

                            VisualStyleRenderer vsr = new VisualStyleRenderer(state);
                            vsr.DrawBackground(e.Graphics, this.ClientRectangle);

                            Rectangle rectContent = vsr.GetBackgroundContentRectangle(e.Graphics, this.ClientRectangle);
                            e.Graphics.FillRectangle(new SolidBrush(backColor), rectContent);
                        }
                        else
                        {
                            // draw background
                            e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

                            // draw default border
                            ControlPaint.DrawBorder3D(e.Graphics, ClientRectangle, Border3DStyle.Sunken);
                        }

                        break;
                    }
                case BorderStyle.FixedSingle:
                    {
                        // draw background
                        e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);

                        // draw fixed single border
                        ControlPaint.DrawBorder(e.Graphics, ClientRectangle, Color.Black, ButtonBorderStyle.Solid);
                        break;
                    }
                default:
                    {
                        // draw background
                        e.Graphics.FillRectangle(new SolidBrush(BackColor), ClientRectangle);
                        break;
                    }
            }
        }

        /// <summary>
        /// Paints the hex box.
        /// </summary>
        /// <param name="e">A PaintEventArgs that contains the event data.</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            if (_byteProvider == null)
                return;

            //System.Diagnostics.Debug.WriteLine("OnPaint " + DateTime.Now.ToString(), "HexBox");

            // draw only in the content rectangle, so exclude the border and the scrollbar.
            Region r = new Region(ClientRectangle);
            r.Exclude(_recContent);
            e.Graphics.ExcludeClip(r);

            UpdateVisibilityBytes();

            if (_lineInfoVisible)
                PaintLineInfo(e.Graphics, _startByte, _endByte);

            if (!_stringViewVisible)
            {
                PaintHex(e.Graphics, _startByte, _endByte);
            }
            else
            {
                PaintHexAndStringView(e.Graphics, _startByte, _endByte);
                if (_shadowSelectionVisible)
                    PaintCurrentBytesSign(e.Graphics);
            }
            if (_columnInfoVisible)
                PaintHeaderRow(e.Graphics);
            if (_groupSeparatorVisible)
                PaintColumnSeparator(e.Graphics);
        }

        void PaintLineInfo(Graphics g, long startByte, long endByte)
        {
            int numDigits = _lineInfoNumDigits;
            Span<char> formattedInfo = stackalloc char[8];
            formattedInfo = numDigits > formattedInfo.Length ? new char[numDigits] : formattedInfo[..numDigits];
            string format = _hexStringFormat + numDigits;

            // Ensure endByte isn't > length of array.
            endByte = Math.Min(_byteProvider.Length - 1, endByte);

            Color lineInfoColor = (this.InfoForeColor != Color.Empty) ? this.InfoForeColor : this.ForeColor;
            Brush brush = new SolidBrush(lineInfoColor);

            int maxLine = GetGridBytePoint(endByte - startByte).Y + 1;

            for (int i = 0; i < maxLine; i++)
            {
                long firstLineByte = startByte + (_iHexMaxHBytes * i) + _lineInfoOffset;

                PointF bytePointF = GetBytePointF(new Point(0, 0 + i));
                if (!firstLineByte.TryFormat(formattedInfo, out _, format, System.Threading.Thread.CurrentThread.CurrentCulture))
                {
                    formattedInfo.Fill('~');
                }

                g.DrawString(formattedInfo, Font, brush, new PointF(_recLineInfo.X, bytePointF.Y), _stringFormat);
            }
        }

        void PaintHeaderRow(Graphics g)
        {
            Brush brush = new SolidBrush(this.InfoForeColor);
            for (int col = 0; col < _iHexMaxHBytes; col++)
            {
                PaintColumnInfo(g, (byte)col, brush, col);
            }
        }

        void PaintColumnSeparator(Graphics g)
        {
            for (int col = GroupSize; col < _iHexMaxHBytes; col += GroupSize)
            {
                var pen = new Pen(new SolidBrush(this.InfoForeColor), 1);
                PointF headerPointF = GetColumnInfoPointF(col);
                headerPointF.X -= _charSize.Width / 2;
                g.DrawLine(pen, headerPointF, new PointF(headerPointF.X, headerPointF.Y + _recColumnInfo.Height + _recHex.Height));
                if (StringViewVisible)
                {
                    PointF byteStringPointF = GetByteStringPointF(new Point(col, 0));
                    headerPointF.X -= 2;
                    g.DrawLine(pen, new PointF(byteStringPointF.X, byteStringPointF.Y), new PointF(byteStringPointF.X, byteStringPointF.Y + _recHex.Height));
                }
            }
        }

        void PaintHex(Graphics g, long startByte, long endByte)
        {
            Brush brush = new SolidBrush(GetDefaultForeColor());
            Brush selBrush = new SolidBrush(_selectionForeColor);
            Brush selBrushBack = new SolidBrush(_selectionBackColor);

            int counter = -1;
            long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

            bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);

            for (long i = startByte; i < intern_endByte + 1; i++)
            {
                counter++;
                Point gridPoint = GetGridBytePoint(counter);
                byte b = _byteProvider.ReadByte(i);

                bool isSelectedByte = i >= _bytePos && i <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

                HighlightRegion hl = _highlightRegions.Find(item => item.IsWithin(i));

                if (isSelectedByte && isKeyInterpreterActive)
                {
                    PaintHexStringSelected(g, b, selBrush, selBrushBack, gridPoint);
                }
                else if (null != hl)
                {
                    PaintHexStringSelected(g, b, new SolidBrush(hl.ForeColor), new SolidBrush(hl.BackColor), gridPoint);
                }
                else
                {
                    PaintHexString(g, b, brush, gridPoint);
                }
            }
        }

        void PaintHexString(Graphics g, byte b, Brush brush, Point gridPoint)
        {
            Span<char> hexChars = stackalloc char[2];
            PointF bytePointF = GetBytePointF(gridPoint);

            ConvertByteToHex(b, hexChars);

            g.DrawString(hexChars.Slice(0, 1), Font, brush, bytePointF, _stringFormat);
            bytePointF.X += _charSize.Width;
            g.DrawString(hexChars.Slice(1, 1), Font, brush, bytePointF, _stringFormat);
        }

        void PaintColumnInfo(Graphics g, byte b, Brush brush, int col)
        {
            Span<char> hexChars = stackalloc char[2];
            PointF headerPointF = GetColumnInfoPointF(col);

            ConvertByteToHex(b, hexChars);

            g.DrawString(hexChars.Slice(0, 1), Font, brush, headerPointF, _stringFormat);
            headerPointF.X += _charSize.Width;
            g.DrawString(hexChars.Slice(1, 1), Font, brush, headerPointF, _stringFormat);
        }

        void PaintHexStringSelected(Graphics g, byte b, Brush brush, Brush brushBack, Point gridPoint)
        {
            Span<char> hexChars = stackalloc char[2];
            ConvertByteToHex(b, hexChars);

            PointF bytePointF = GetBytePointF(gridPoint);

            bool isLastLineChar = (gridPoint.X + 1 == _iHexMaxHBytes);
            float bcWidth = (isLastLineChar) ? _charSize.Width * 2 : _charSize.Width * 3;

            g.FillRectangle(brushBack, bytePointF.X, bytePointF.Y, bcWidth, _charSize.Height);
            g.DrawString(hexChars.Slice(0, 1), Font, brush, bytePointF, _stringFormat);
            bytePointF.X += _charSize.Width;
            g.DrawString(hexChars.Slice(1, 1), Font, brush, bytePointF, _stringFormat);
        }

        void PaintHexAndStringView(Graphics g, long startByte, long endByte)
        {
            Span<char> byteChar = stackalloc char[1];

            Brush brush = new SolidBrush(GetDefaultForeColor());
            Brush selBrush = new SolidBrush(_selectionForeColor);
            Brush selBrushBack = new SolidBrush(_selectionBackColor);

            int counter = -1;
            long intern_endByte = Math.Min(_byteProvider.Length - 1, endByte + _iHexMaxHBytes);

            bool isKeyInterpreterActive = _keyInterpreter == null || _keyInterpreter.GetType() == typeof(KeyInterpreter);
            bool isStringKeyInterpreterActive = _keyInterpreter != null && _keyInterpreter.GetType() == typeof(StringKeyInterpreter);

            for (long i = startByte; i < intern_endByte + 1; i++)
            {
                counter++;
                Point gridPoint = GetGridBytePoint(counter);
                PointF byteStringPointF = GetByteStringPointF(gridPoint);
                byte b = _byteProvider.ReadByte(i);

                bool isSelectedByte = i >= _bytePos && i <= (_bytePos + _selectionLength - 1) && _selectionLength != 0;

                HighlightRegion hl = _highlightRegions.Find(item => item.IsWithin(i));

                if (isSelectedByte && isKeyInterpreterActive)
                {
                    PaintHexStringSelected(g, b, selBrush, selBrushBack, gridPoint);
                }
                else if (hl != null)
                {
                    PaintHexStringSelected(g, b, new SolidBrush(hl.ForeColor), new SolidBrush(hl.BackColor), gridPoint);
                }
                else
                {
                    PaintHexString(g, b, brush, gridPoint);
                }

                byteChar[0] = ByteCharConverter.ToChar(b);

                if (isSelectedByte && isStringKeyInterpreterActive)
                {
                    g.FillRectangle(selBrushBack, byteStringPointF.X, byteStringPointF.Y, _charSize.Width, _charSize.Height);
                    g.DrawString(byteChar, Font, selBrush, byteStringPointF, _stringFormat);
                }
                else if (hl != null)
                {
                    g.FillRectangle(new SolidBrush(hl.BackColor), byteStringPointF.X, byteStringPointF.Y, _charSize.Width, _charSize.Height);
                    g.DrawString(byteChar, Font, new SolidBrush(hl.ForeColor), byteStringPointF, _stringFormat);
                }
                else
                {
                    g.DrawString(byteChar, Font, brush, byteStringPointF, _stringFormat);
                }
            }
        }

        void PaintCurrentBytesSign(Graphics g)
        {
            if (_keyInterpreter != null && _bytePos != -1 && Enabled)
            {
                if (_keyInterpreter.GetType() == typeof(KeyInterpreter))
                {
                    if (_selectionLength == 0)
                    {
                        Point gp = GetGridBytePoint(_bytePos - _startByte);
                        PointF pf = GetByteStringPointF(gp);
                        Size s = new Size((int)_charSize.Width, (int)_charSize.Height);
                        Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
                        if (r.IntersectsWith(_recStringView))
                        {
                            r.Intersect(_recStringView);
                            PaintCurrentByteSign(g, r);
                        }
                    }
                    else
                    {
                        int lineWidth = (int)(_recStringView.Width - _charSize.Width);

                        Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
                        PointF startSelPointF = GetByteStringPointF(startSelGridPoint);

                        Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
                        PointF endSelPointF = GetByteStringPointF(endSelGridPoint);

                        int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
                        if (multiLine == 0)
                        {
                            Rectangle singleLine = new Rectangle(
                                (int)startSelPointF.X,
                                (int)startSelPointF.Y,
                                (int)(endSelPointF.X - startSelPointF.X + _charSize.Width),
                                (int)_charSize.Height);
                            if (singleLine.IntersectsWith(_recStringView))
                            {
                                singleLine.Intersect(_recStringView);
                                PaintCurrentByteSign(g, singleLine);
                            }
                        }
                        else
                        {
                            Rectangle firstLine = new Rectangle(
                                (int)startSelPointF.X,
                                (int)startSelPointF.Y,
                                (int)(_recStringView.X + lineWidth - startSelPointF.X + _charSize.Width),
                                (int)_charSize.Height);
                            if (firstLine.IntersectsWith(_recStringView))
                            {
                                firstLine.Intersect(_recStringView);
                                PaintCurrentByteSign(g, firstLine);
                            }

                            if (multiLine > 1)
                            {
                                Rectangle betweenLines = new Rectangle(
                                    _recStringView.X,
                                    (int)(startSelPointF.Y + _charSize.Height),
                                    (int)(_recStringView.Width),
                                    (int)(_charSize.Height * (multiLine - 1)));
                                if (betweenLines.IntersectsWith(_recStringView))
                                {
                                    betweenLines.Intersect(_recStringView);
                                    PaintCurrentByteSign(g, betweenLines);
                                }
                            }

                            Rectangle lastLine = new Rectangle(
                                _recStringView.X,
                                (int)endSelPointF.Y,
                                (int)(endSelPointF.X - _recStringView.X + _charSize.Width),
                                (int)_charSize.Height);
                            if (lastLine.IntersectsWith(_recStringView))
                            {
                                lastLine.Intersect(_recStringView);
                                PaintCurrentByteSign(g, lastLine);
                            }
                        }
                    }
                }
                else
                {
                    if (_selectionLength == 0)
                    {
                        Point gp = GetGridBytePoint(_bytePos - _startByte);
                        PointF pf = GetBytePointF(gp);
                        Size s = new Size((int)_charSize.Width * 2, (int)_charSize.Height);
                        Rectangle r = new Rectangle((int)pf.X, (int)pf.Y, s.Width, s.Height);
                        PaintCurrentByteSign(g, r);
                    }
                    else
                    {
                        int lineWidth = (int)(_recHex.Width - _charSize.Width * 5);

                        Point startSelGridPoint = GetGridBytePoint(_bytePos - _startByte);
                        PointF startSelPointF = GetBytePointF(startSelGridPoint);

                        Point endSelGridPoint = GetGridBytePoint(_bytePos - _startByte + _selectionLength - 1);
                        PointF endSelPointF = GetBytePointF(endSelGridPoint);

                        int multiLine = endSelGridPoint.Y - startSelGridPoint.Y;
                        if (multiLine == 0)
                        {
                            Rectangle singleLine = new Rectangle(
                                (int)startSelPointF.X,
                                (int)startSelPointF.Y,
                                (int)(endSelPointF.X - startSelPointF.X + _charSize.Width * 2),
                                (int)_charSize.Height);
                            if (singleLine.IntersectsWith(_recHex))
                            {
                                singleLine.Intersect(_recHex);
                                PaintCurrentByteSign(g, singleLine);
                            }
                        }
                        else
                        {
                            Rectangle firstLine = new Rectangle(
                                (int)startSelPointF.X,
                                (int)startSelPointF.Y,
                                (int)(_recHex.X + lineWidth - startSelPointF.X + _charSize.Width * 2),
                                (int)_charSize.Height);
                            if (firstLine.IntersectsWith(_recHex))
                            {
                                firstLine.Intersect(_recHex);
                                PaintCurrentByteSign(g, firstLine);
                            }

                            if (multiLine > 1)
                            {
                                Rectangle betweenLines = new Rectangle(
                                    _recHex.X,
                                    (int)(startSelPointF.Y + _charSize.Height),
                                    (int)(lineWidth + _charSize.Width * 2),
                                    (int)(_charSize.Height * (multiLine - 1)));
                                if (betweenLines.IntersectsWith(_recHex))
                                {
                                    betweenLines.Intersect(_recHex);
                                    PaintCurrentByteSign(g, betweenLines);
                                }
                            }

                            Rectangle lastLine = new Rectangle(
                                _recHex.X,
                                (int)endSelPointF.Y,
                                (int)(endSelPointF.X - _recHex.X + _charSize.Width * 2),
                                (int)_charSize.Height);
                            if (lastLine.IntersectsWith(_recHex))
                            {
                                lastLine.Intersect(_recHex);
                                PaintCurrentByteSign(g, lastLine);
                            }
                        }
                    }
                }
            }
        }

        void PaintCurrentByteSign(Graphics g, Rectangle rec)
        {
            // stack overflowexception on big files - workaround
            if (rec.Top < 0 || rec.Left < 0 || rec.Width <= 0 || rec.Height <= 0)
                return;

            Bitmap myBitmap = new Bitmap(rec.Width, rec.Height);
            Graphics bitmapGraphics = Graphics.FromImage(myBitmap);

            SolidBrush greenBrush = new SolidBrush(_shadowSelectionColor);

            bitmapGraphics.FillRectangle(greenBrush, 0,
                0, rec.Width, rec.Height);

            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.GammaCorrected;

            g.DrawImage(myBitmap, rec.Left, rec.Top);
        }

        Color GetDefaultForeColor()
        {
            if (Enabled)
                return ForeColor;
            else
                return Color.Gray;
        }
        void UpdateVisibilityBytes()
        {
            if (_byteProvider == null || _byteProvider.Length == 0)
                return;

            _startByte = (_scrollVpos + 1) * _iHexMaxHBytes - _iHexMaxHBytes;
            _endByte = (long)Math.Min(_byteProvider.Length - 1, _startByte + _iHexMaxBytes);
        }
        #endregion

        #region Positioning methods
        void UpdateRectanglePositioning()
        {
            // calc char size
            SizeF charSize;
            using (var graphics = this.CreateGraphics())
            {
                charSize = graphics.MeasureString("A", Font, 100, _stringFormat);
            }
            CharSize = new SizeF(MathF.Ceiling(charSize.Width), MathF.Ceiling(charSize.Height));

            int requiredWidth = 0;

            // calc content bounds
            _recContent = ClientRectangle;
            _recContent.X += _recBorderLeft;
            _recContent.Y += _recBorderTop;
            _recContent.Width -= _recBorderRight + _recBorderLeft;
            _recContent.Height -= _recBorderBottom + _recBorderTop;

            if (_vScrollBarVisible)
            {
                _recContent.Width -= _vScrollBar.Width;
                _vScrollBar.Left = _recContent.X + _recContent.Width;
                _vScrollBar.Top = _recContent.Y;
                _vScrollBar.Height = _recContent.Height;
                requiredWidth += _vScrollBar.Width;
            }

            int marginLeft = 4;

            // calc line info bounds
            if (_lineInfoVisible)
            {
                var numBytes = _byteProvider?.Length ?? 0;
                int maxDigitsNeccesary = $"{numBytes:X}".Length;
                _lineInfoNumDigits = Math.Max(maxDigitsNeccesary, _lineInfoMinDigits);

                _recLineInfo = new Rectangle(_recContent.X + marginLeft,
                    _recContent.Y,
                    (int)(_charSize.Width * (_lineInfoNumDigits + 2)),
                    _recContent.Height);
                requiredWidth += _recLineInfo.Width;
            }
            else
            {
                _recLineInfo = Rectangle.Empty;
                _recLineInfo.X = marginLeft;
                requiredWidth += marginLeft;
            }

            // calc line info bounds
            _recColumnInfo = new Rectangle(_recLineInfo.X + _recLineInfo.Width, _recContent.Y, _recContent.Width - _recLineInfo.Width, (int)charSize.Height + 4);
            if (_columnInfoVisible)
            {
                _recLineInfo.Y += (int)charSize.Height + 4;
                _recLineInfo.Height -= (int)charSize.Height + 4;
            }
            else
            {
                _recColumnInfo.Height = 0;
            }

            // calc hex bounds and grid
            _recHex = new Rectangle(_recLineInfo.X + _recLineInfo.Width,
                _recLineInfo.Y,
                _recContent.Width - _recLineInfo.Width,
                _recContent.Height - _recColumnInfo.Height);

            if (UseFixedBytesPerLine)
            {
                SetHorizontalByteCount(_bytesPerLine);
                _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * _charSize.Width * 3 + (2 * _charSize.Width));
                requiredWidth += _recHex.Width;
            }
            else
            {
                int hmax = (int)Math.Floor((double)_recHex.Width / (double)_charSize.Width);
                if (_stringViewVisible)
                {
                    hmax -= 2;
                    if (hmax > 1)
                        SetHorizontalByteCount((int)Math.Floor((double)hmax / 4));
                    else
                        SetHorizontalByteCount(1);
                }
                else
                {
                    if (hmax > 1)
                        SetHorizontalByteCount((int)Math.Floor((double)hmax / 3));
                    else
                        SetHorizontalByteCount(1);
                }
                _recHex.Width = (int)Math.Floor(((double)_iHexMaxHBytes) * _charSize.Width * 3 + (2 * _charSize.Width));
                requiredWidth += _recHex.Width;
            }

            MinWidth = 5 + _recHex.X + (int)Math.Floor(MinBytesPerLine * _charSize.Width * 3 + 2 * _charSize.Width);
            MaxWidth = 5 + _recHex.X + (int)Math.Floor((MaxBytesPerLine + 1) * _charSize.Width * 3 + 2 * _charSize.Width) + (int)(_charSize.Width * (MaxBytesPerLine + 1));

            if (_stringViewVisible)
            {
                _recStringView = new Rectangle(_recHex.X + _recHex.Width,
                    _recHex.Y,
                    (int)(_charSize.Width * _iHexMaxHBytes),
                    _recHex.Height);
                requiredWidth += _recStringView.Width;
            }
            else
            {
                _recStringView = Rectangle.Empty;
            }

            RequiredWidth = requiredWidth;

            int vmax = (int)Math.Floor((double)_recHex.Height / (double)_charSize.Height);
            //Debug.WriteLine("Vmax: " + vmax);
            SetVerticalByteCount(vmax);

            _iHexMaxBytes = _iHexMaxHBytes * _iHexMaxVBytes;

            UpdateScrollSize();
        }

        PointF GetBytePointF(long byteIndex)
        {
            Point gp = GetGridBytePoint(byteIndex);

            return GetBytePointF(gp);
        }

        PointF GetBytePointF(Point gp)
        {
            float x = (3 * _charSize.Width) * gp.X + _recHex.X;
            float y = (gp.Y + 1) * _charSize.Height - _charSize.Height + _recHex.Y;

            return new PointF(x, y);
        }
        PointF GetColumnInfoPointF(int col)
        {
            Point gp = GetGridBytePoint(col);
            float x = (3 * _charSize.Width) * gp.X + _recColumnInfo.X;
            float y = _recColumnInfo.Y;

            return new PointF(x, y);
        }

        PointF GetByteStringPointF(Point gp)
        {
            float x = (_charSize.Width) * gp.X + _recStringView.X;
            float y = (gp.Y + 1) * _charSize.Height - _charSize.Height + _recStringView.Y;

            return new PointF(x, y);
        }

        Point GetGridBytePoint(long byteIndex)
        {
            int row = (int)MathF.Floor(byteIndex / _iHexMaxHBytes);
            int column = (int)(byteIndex + _iHexMaxHBytes - _iHexMaxHBytes * (row + 1));

            return new Point(column, row);
        }

        private static float ScalingFactor
        {
            get
            {
                using Graphics g = Graphics.FromHwnd(IntPtr.Zero);
                return g.DpiX / 96;
            }
        }

        #endregion

        #region Overridden properties

        /// <summary>
        /// The font used to display text in the hexbox.
        /// </summary>
        public override Font Font
        {
            get
            {
                return base.Font;
            }
            set
            {
                if (value == null)
                    return;

                base.Font = value;
                this.UpdateRectanglePositioning();
                this.Invalidate();
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
        public override string Text
        {
            get
            {
                return base.Text;
            }
            set
            {
                base.Text = value;
            }
        }

        /// <summary>
        /// Not used.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden), EditorBrowsable(EditorBrowsableState.Never), Bindable(false)]
        public override RightToLeft RightToLeft
        {
            get
            {
                return base.RightToLeft;
            }
            set
            {
                base.RightToLeft = value;
            }
        }
        #endregion

        #region Properties

        private int _minWidth;
        public int MinWidth
        {
            get => (int)Math.Ceiling(_minWidth / ScalingFactor);
            set => SetProperty(ref _minWidth, value);
        }

        private int _maxWidth;
        public int MaxWidth
        {
            get => (int)Math.Ceiling(_maxWidth / ScalingFactor);
            set => SetProperty(ref _maxWidth, value);
        }
        /// <summary>
        /// Gets or sets the background color for the disabled control.
        /// </summary>
        [Category("Appearance"), DefaultValue(typeof(Color), "WhiteSmoke")]
        public Color BackColorDisabled { get; set; } = Color.FromName("WhiteSmoke");

        /// <summary>
        /// Gets or sets if the count of bytes in one line is fix.
        /// </summary>
        /// <remarks>
        /// When set to True, BytesPerLine property determine the maximum count of bytes in one line.
        /// </remarks>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets if the count of bytes in one line is fix.")]
        public bool ReadOnly
        {
            get { return _readOnly; }
            set
            {
                if (_readOnly == value)
                    return;

                _readOnly = value;
                OnReadOnlyChanged(EventArgs.Empty);
                Invalidate();
            }
        }
        bool _readOnly;

        /// <summary>
        /// Gets or sets the maximum count of bytes in one line.
        /// </summary>
        /// <remarks>
        /// UseFixedBytesPerLine property no longer has to be set to true for this to work
        /// </remarks>
        [DefaultValue(16), Category("Hex"), Description("Gets or sets the maximum count of bytes in one line.")]
        public int BytesPerLine
        {
            get { return _bytesPerLine; }
            set
            {
                if (_bytesPerLine == value)
                    return;

                _bytesPerLine = value;
                OnBytesPerLineChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        int _bytesPerLine = 16;

        /// <summary>
        /// Gets or sets the number of bytes in a group. Used to show the group separator line (if GroupSeparatorVisible is true)
        /// </summary>
        /// <remarks>
        /// GroupSeparatorVisible property must set to true
        /// </remarks>
        [DefaultValue(4), Category("Hex"), Description("Gets or sets the byte-count between group separators (if visible).")]
        public int GroupSize
        {
            get { return _groupSize; }
            set
            {
                if (_groupSize == value)
                    return;

                _groupSize = value;
                OnGroupSizeChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        int _groupSize = 4;
        /// <summary>
        /// Gets or sets if the count of bytes in one line is fix.
        /// </summary>
        /// <remarks>
        /// When set to True, BytesPerLine property determine the maximum count of bytes in one line.
        /// </remarks>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets if the count of bytes in one line is fix.")]
        public bool UseFixedBytesPerLine
        {
            get { return _useFixedBytesPerLine; }
            set
            {
                if (_useFixedBytesPerLine == value)
                    return;

                _useFixedBytesPerLine = value;
                OnUseFixedBytesPerLineChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _useFixedBytesPerLine;

        /// <summary>
        /// Gets or sets the visibility of a vertical scroll bar.
        /// </summary>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a vertical scroll bar.")]
        public bool VScrollBarVisible
        {
            get { return this._vScrollBarVisible; }
            set
            {
                if (_vScrollBarVisible == value)
                    return;

                _vScrollBarVisible = value;

                if (_vScrollBarVisible)
                    Controls.Add(_vScrollBar);
                else
                    Controls.Remove(_vScrollBar);

                UpdateRectanglePositioning();
                UpdateScrollSize();

                OnVScrollBarVisibleChanged(EventArgs.Empty);
            }
        }
        bool _vScrollBarVisible;

        /// <summary>
        /// Gets or sets the ByteProvider.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IByteProvider ByteProvider
        {
            get { return _byteProvider; }
            set
            {
                if (_byteProvider == value)
                    return;

                if (value == null)
                    ActivateEmptyKeyInterpreter();
                else
                    ActivateKeyInterpreter();

                if (_byteProvider != null)
                    _byteProvider.LengthChanged -= new EventHandler(_byteProvider_LengthChanged);

                _byteProvider = value;
                if (_byteProvider != null)
                    _byteProvider.LengthChanged += new EventHandler(_byteProvider_LengthChanged);

                OnByteProviderChanged(EventArgs.Empty);

                if (value == null) // do not raise events if value is null
                {
                    _bytePos = -1;
                    _byteCharacterPos = 0;
                    _selectionLength = 0;

                    DestroyCaret();
                }
                else
                {
                    SetPosition(0, 0);
                    SetSelectionLength(0);

                    if (_caretVisible && Focused)
                        UpdateCaret();
                    else
                        CreateCaret();
                }

                _highlightRegions.Clear();

                CheckCurrentLineChanged();
                CheckCurrentPositionInLineChanged();

                _scrollVpos = 0;

                UpdateVisibilityBytes();
                UpdateRectanglePositioning();

                Invalidate();
            }
        }

        IByteProvider _byteProvider;
        /// <summary>
        /// Gets or sets the visibility of the group separator.
        /// </summary>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a separator vertical line.")]
        public bool GroupSeparatorVisible
        {
            get { return _groupSeparatorVisible; }
            set
            {
                if (_groupSeparatorVisible == value)
                    return;

                _groupSeparatorVisible = value;
                OnGroupSeparatorVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _groupSeparatorVisible = false;

        /// <summary>
        /// Gets or sets the visibility of the column info
        /// </summary>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of header row.")]
        public bool ColumnInfoVisible
        {
            get { return _columnInfoVisible; }
            set
            {
                if (_columnInfoVisible == value)
                    return;

                _columnInfoVisible = value;
                OnColumnInfoVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _columnInfoVisible = false;

        /// <summary>
        /// Gets or sets the visibility of a line info.
        /// </summary>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of a line info.")]
        public bool LineInfoVisible
        {
            get { return _lineInfoVisible; }
            set
            {
                if (_lineInfoVisible == value)
                    return;

                _lineInfoVisible = value;
                OnLineInfoVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _lineInfoVisible = false;

        /// <summary>
        /// Gets or sets the offset of a line info.
        /// </summary>
        [DefaultValue((long)0), Category("Hex"), Description("Gets or sets the offset of the line info.")]
        public long LineInfoOffset
        {
            get { return _lineInfoOffset; }
            set
            {
                if (_lineInfoOffset == value)
                    return;

                _lineInfoOffset = value;

                Invalidate();
            }
        }
        long _lineInfoOffset = 0;

        /// <summary>
        /// Gets or sets the hex box�s border style.
        /// </summary>
        [DefaultValue(typeof(BorderStyle), "Fixed3D"), Category("Hex"), Description("Gets or sets the hex box�s border style.")]
        public BorderStyle BorderStyle
        {
            get { return _borderStyle; }
            set
            {
                if (_borderStyle == value)
                    return;

                _borderStyle = value;
                switch (_borderStyle)
                {
                    case BorderStyle.None:
                        _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 0;
                        break;
                    case BorderStyle.Fixed3D:
                        _recBorderLeft = _recBorderRight = SystemInformation.Border3DSize.Width;
                        _recBorderTop = _recBorderBottom = SystemInformation.Border3DSize.Height;
                        break;
                    case BorderStyle.FixedSingle:
                        _recBorderLeft = _recBorderTop = _recBorderRight = _recBorderBottom = 1;
                        break;
                }

                UpdateRectanglePositioning();

                OnBorderStyleChanged(EventArgs.Empty);
            }
        }
        BorderStyle _borderStyle = BorderStyle.Fixed3D;

        /// <summary>
        /// Gets or sets the visibility of the string view.
        /// </summary>
        [DefaultValue(false), Category("Hex"), Description("Gets or sets the visibility of the string view.")]
        public bool StringViewVisible
        {
            get { return _stringViewVisible; }
            set
            {
                if (_stringViewVisible == value)
                    return;

                _stringViewVisible = value;
                OnStringViewVisibleChanged(EventArgs.Empty);

                UpdateRectanglePositioning();
                Invalidate();
            }
        }
        bool _stringViewVisible;

        /// <summary>
        /// Gets or sets whether the HexBox control displays the hex characters in upper or lower case.
        /// </summary>
        [DefaultValue(typeof(HexCasing), "Upper"), Category("Hex"), Description("Gets or sets whether the HexBox control displays the hex characters in upper or lower case.")]
        public HexCasing HexCasing
        {
            get
            {
                if (_hexStringFormat == "X")
                    return HexCasing.Upper;
                else
                    return HexCasing.Lower;
            }
            set
            {
                string format;
                if (value == HexCasing.Upper)
                    format = "X";
                else
                    format = "x";

                if (_hexStringFormat == format)
                    return;

                _hexStringFormat = format;
                OnHexCasingChanged(EventArgs.Empty);

                Invalidate();
            }
        }

        /// <summary>
        /// Gets and sets the starting point of the bytes selected in the hex box.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long SelectionStart
        {
            get { return _bytePos; }
            set
            {
                SetPosition(value, 0);
                ScrollByteIntoView();
                Invalidate();
            }
        }

        /// <summary>
        /// Gets and sets the number of bytes selected in the hex box.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long SelectionLength
        {
            get { return _selectionLength; }
            set
            {
                SetSelectionLength(value);
                ScrollByteIntoView();
                Invalidate();
            }
        }
        long _selectionLength;

        /// <summary>
        /// Gets or sets the info color used for column info and line info. When this property is null, then ForeColor property is used.
        /// </summary>
        [DefaultValue(typeof(Color), "Gray"), Category("Hex"), Description("Gets or sets the line info color. When this property is null, then ForeColor property is used.")]
        public Color InfoForeColor
        {
            get { return _infoForeColor; }
            set { _infoForeColor = value; Invalidate(); }
        }
        Color _infoForeColor = Color.Gray;

        /// <summary>
        /// Gets or sets the background color for the selected bytes.
        /// </summary>
        [DefaultValue(typeof(Color), "Blue"), Category("Hex"), Description("Gets or sets the background color for the selected bytes.")]
        public Color SelectionBackColor
        {
            get { return _selectionBackColor; }
            set { _selectionBackColor = value; Invalidate(); }
        }
        Color _selectionBackColor = Color.Blue;

        /// <summary>
        /// Gets or sets the foreground color for the selected bytes.
        /// </summary>
        [DefaultValue(typeof(Color), "White"), Category("Hex"), Description("Gets or sets the foreground color for the selected bytes.")]
        public Color SelectionForeColor
        {
            get { return _selectionForeColor; }
            set { _selectionForeColor = value; Invalidate(); }
        }
        Color _selectionForeColor = Color.White;

        /// <summary>
        /// Gets or sets the foreground color for the highlighted bytes.
        /// </summary>
        [DefaultValue(typeof(Color), "Black"), Category("Hex"), Description("Highlight foreground color.")]
        public Color HighlightForeColor
        {
            get { return _hlForeColor; }
            set
            {
                _hlForeColor = value; Invalidate();
            }
        }
        Color _hlForeColor = Color.Black;

        /// <summary>
        /// Gets or sets the background color for the highlighted bytes.
        /// </summary>
        [DefaultValue(typeof(Color), "Yellow"), Category("Hex"), Description("Highlight background color.")]
        public Color HighlightBackColor
        {
            get { return _hlBackColor; }
            set { _hlBackColor = value; Invalidate(); }
        }
        Color _hlBackColor = Color.Yellow;

        /// <summary>
        /// Gets or sets the visibility of a shadow selection.
        /// </summary>
        [DefaultValue(true), Category("Hex"), Description("Gets or sets the visibility of a shadow selection.")]
        public bool ShadowSelectionVisible
        {
            get { return _shadowSelectionVisible; }
            set
            {
                if (_shadowSelectionVisible == value)
                    return;
                _shadowSelectionVisible = value;
                Invalidate();
            }
        }
        bool _shadowSelectionVisible = true;

        /// <summary>
        /// Gets or sets the color of the shadow selection. 
        /// </summary>
        /// <remarks>
        /// A alpha component must be given! 
        /// Default alpha = 100
        /// </remarks>
        [Category("Hex"), Description("Gets or sets the color of the shadow selection.")]
        public Color ShadowSelectionColor
        {
            get { return _shadowSelectionColor; }
            set { _shadowSelectionColor = value; Invalidate(); }
        }
        Color _shadowSelectionColor = Color.FromArgb(100, 60, 188, 255);

        /// <summary>
        /// Contains the size of a single character in pixel
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public SizeF CharSize
        {
            get { return _charSize; }
            private set
            {
                if (_charSize == value)
                    return;
                _charSize = value;
                CharSizeChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        SizeF _charSize;

        /// <summary>
        /// Gets the width required for the content
        /// </summary>
        [DefaultValue(0), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int RequiredWidth
        {
            get { return _requiredWidth; }
            private set
            {
                if (_requiredWidth == value)
                    return;
                _requiredWidth = value;
                RequiredWidthChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        int _requiredWidth;

        /// <summary>
        /// Gets the number bytes drawn horizontally.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int HorizontalByteCount
        {
            get { return _iHexMaxHBytes; }
        }

        /// <summary>
        /// Gets the number bytes drawn vertically.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public int VerticalByteCount
        {
            get { return _iHexMaxVBytes; }
        }

        /// <summary>
        /// Gets the current line
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentLine
        {
            get { return _currentLine; }
        }
        long _currentLine;

        /// <summary>
        /// Gets the current position in the current line
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public long CurrentPositionInLine
        {
            get { return _currentPositionInLine; }
        }
        int _currentPositionInLine;

        /// <summary>
        /// Gets the a value if insertion mode is active or not.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public bool InsertActive
        {
            get { return _insertActive; }
            set
            {
                if (_insertActive == value)
                    return;

                _insertActive = value;

                // recreate caret
                DestroyCaret();
                CreateCaret();

                // raise change event
                OnInsertActiveChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Gets or sets the built-in context menu.
        /// </summary>
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Content)]
        public BuiltInContextMenu BuiltInContextMenu
        {
            get { return _builtInContextMenu; }
        }
        BuiltInContextMenu _builtInContextMenu;

        /// <summary>
        /// Gets or sets the converter that will translate between byte and character values.
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public IByteCharConverter ByteCharConverter
        {
            get
            {
                _byteCharConverter ??= new DefaultByteCharConverter();
                return _byteCharConverter;
            }
            set
            {
                if (value != null && value != _byteCharConverter)
                {
                    _byteCharConverter = value;
                    Invalidate();
                }
            }
        }
        IByteCharConverter _byteCharConverter;

        /// <summary>
        /// Gets currently selected region
        /// </summary>
        [Browsable(false), DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public HighlightRegion SelectedRegion
        {
            get
            {
                return _selectedRegion;
            }
        }
        #endregion

        #region Misc
        /// <summary>
        /// Converts a byte array to a hex string. For example: {10,11} = "0A 0B"
        /// </summary>
        /// <param name="data">the byte array</param>
        /// <returns>the hex string</returns>
        string ConvertBytesToHex(byte[] data)
        {
            Span<char> hexChars = stackalloc char[2];
            var sb = new StringBuilder();
            foreach (byte b in data)
            {
                ConvertByteToHex(b, hexChars);
                sb.Append(hexChars);
                sb.Append(' ');
            }
            if (sb.Length > 0)
                sb.Remove(sb.Length - 1, 1);
            return sb.ToString();
        }
        /// <summary>
        /// Converts the byte to a hex string. For example: "10" = "0A";
        /// </summary>
        /// <param name="b">the byte to format</param>
        /// <returns>the hex string</returns>
        void ConvertByteToHex(byte b, [Length(2, 2)] Span<char> hexChars)
        {
            if (_hexStringFormat == "x")
            {
                b.TryFormat(hexChars, out _, "x2", System.Threading.Thread.CurrentThread.CurrentCulture);
            }
            else
            {
                b.TryFormat(hexChars, out _, "X2", System.Threading.Thread.CurrentThread.CurrentCulture);
            }
        }
        /// <summary>
        /// Converts the hex string to an byte array. The hex string must be separated by a space char ' '. If there is any invalid hex information in the string the result will be null.
        /// </summary>
        /// <param name="hex">the hex string separated by ' '. For example: "0A 0B 0C"</param>
        /// <returns>the byte array. null if hex is invalid or empty</returns>
        byte[] ConvertHexToBytes(string hex)
        {
            if (string.IsNullOrEmpty(hex))
                return null;
            hex = hex.Trim();
            var hexArray = hex.Split(' ');
            var byteArray = new byte[hexArray.Length];

            for (int i = 0; i < hexArray.Length; i++)
            {
                var hexValue = hexArray[i];

                byte b;
                var isByte = ConvertHexToByte(hexValue, out b);
                if (!isByte)
                    return null;
                byteArray[i] = b;
            }

            return byteArray;
        }

        bool ConvertHexToByte(string hex, out byte b)
        {
            bool isByte = byte.TryParse(hex, System.Globalization.NumberStyles.HexNumber, System.Threading.Thread.CurrentThread.CurrentCulture, out b);
            return isByte;
        }

        void SetPosition(long bytePos)
        {
            SetPosition(bytePos, _byteCharacterPos);
        }

        void SetPosition(long bytePos, int byteCharacterPos)
        {
            if (_byteCharacterPos != byteCharacterPos)
            {
                _byteCharacterPos = byteCharacterPos;
            }

            if (bytePos != _bytePos)
            {
                _bytePos = bytePos;
                CheckCurrentLineChanged();
                CheckCurrentPositionInLineChanged();

                OnSelectionStartChanged(EventArgs.Empty);

                _selectedRegion = _highlightRegions.Find(item => item.IsWithin(_bytePos));
                if (null != _selectedRegion)
                {
                    OnHighlightRegionSelected(EventArgs.Empty);
                }
            }
        }

        void SetSelectionLength(long selectionLength)
        {
            if (selectionLength != _selectionLength)
            {
                _selectionLength = selectionLength;
                OnSelectionLengthChanged(EventArgs.Empty);
            }
        }

        void SetHorizontalByteCount(int value)
        {
            if (_iHexMaxHBytes == value)
                return;

            if (value > MinBytesPerLine && value <= MaxBytesPerLine)
            {
                _iHexMaxHBytes = value;
            }
            else
            {
                _iHexMaxHBytes = value > MaxBytesPerLine ? MaxBytesPerLine : MinBytesPerLine;
            }
            OnHorizontalByteCountChanged(EventArgs.Empty);
        }

        void SetVerticalByteCount(int value)
        {
            if (_iHexMaxVBytes == value)
                return;

            _iHexMaxVBytes = value;
            OnVerticalByteCountChanged(EventArgs.Empty);
        }

        void CheckCurrentLineChanged()
        {
            long currentLine = (long)Math.Floor((double)_bytePos / (double)_iHexMaxHBytes) + 1;

            if (_byteProvider == null && _currentLine != 0)
            {
                _currentLine = 0;
                OnCurrentLineChanged(EventArgs.Empty);
            }
            else if (currentLine != _currentLine)
            {
                _currentLine = currentLine;
                OnCurrentLineChanged(EventArgs.Empty);
            }
        }

        void CheckCurrentPositionInLineChanged()
        {
            Point gb = GetGridBytePoint(_bytePos);
            int currentPositionInLine = gb.X + 1;

            if (_byteProvider == null && _currentPositionInLine != 0)
            {
                _currentPositionInLine = 0;
                OnCurrentPositionInLineChanged(EventArgs.Empty);
            }
            else if (currentPositionInLine != _currentPositionInLine)
            {
                _currentPositionInLine = currentPositionInLine;
                OnCurrentPositionInLineChanged(EventArgs.Empty);
            }
        }

        /// <summary>
        /// Raises the InsertActiveChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnInsertActiveChanged(EventArgs e)
        {
            InsertActiveChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ReadOnlyChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnReadOnlyChanged(EventArgs e)
        {
            ReadOnlyChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ByteProviderChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnByteProviderChanged(EventArgs e)
        {
            ByteProviderChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the SelectionStartChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnSelectionStartChanged(EventArgs e)
        {
            SelectionStartChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the SelectionLengthChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnSelectionLengthChanged(EventArgs e)
        {
            SelectionLengthChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the LineInfoVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnLineInfoVisibleChanged(EventArgs e)
        {
            LineInfoVisibleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the OnColumnInfoVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnColumnInfoVisibleChanged(EventArgs e)
        {
            ColumnInfoVisibleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the ColumnSeparatorVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnGroupSeparatorVisibleChanged(EventArgs e)
        {
            GroupSeparatorVisibleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the StringViewVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnStringViewVisibleChanged(EventArgs e)
        {
            StringViewVisibleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the BorderStyleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnBorderStyleChanged(EventArgs e)
        {
            BorderStyleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the UseFixedBytesPerLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnUseFixedBytesPerLineChanged(EventArgs e)
        {
            UseFixedBytesPerLineChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the GroupSizeChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnGroupSizeChanged(EventArgs e)
        {
            GroupSizeChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the BytesPerLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnBytesPerLineChanged(EventArgs e)
        {
            BytesPerLineChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the VScrollBarVisibleChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnVScrollBarVisibleChanged(EventArgs e)
        {
            VScrollBarVisibleChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the HexCasingChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHexCasingChanged(EventArgs e)
        {
            HexCasingChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the HorizontalByteCountChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHorizontalByteCountChanged(EventArgs e)
        {
            HorizontalByteCountChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the VerticalByteCountChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnVerticalByteCountChanged(EventArgs e)
        {
            VerticalByteCountChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the CurrentLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCurrentLineChanged(EventArgs e)
        {
            CurrentLineChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the CurrentPositionInLineChanged event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCurrentPositionInLineChanged(EventArgs e)
        {
            CurrentPositionInLineChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the Copied event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCopied(EventArgs e)
        {
            Copied?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the CopiedHex event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnCopiedHex(EventArgs e)
        {
            CopiedHex?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the MouseDown event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("OnMouseDown()", "HexBox");

            if (!Focused)
                Focus();

            if (e.Button == MouseButtons.Left)
                SetCaretPosition(new Point(e.X, e.Y));

            base.OnMouseDown(e);
        }

        /// <summary>
        /// Raises the MouseWhell event
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            int linesToScroll = -(e.Delta * SystemInformation.MouseWheelScrollLines / 120);
            this.PerformScrollLines(linesToScroll);

            base.OnMouseWheel(e);
        }

        /// <summary>
        /// Raises the Resize event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateRectanglePositioning();
        }

        /// <summary>
        /// Raises the GotFocus event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnGotFocus(EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("OnGotFocus()", "HexBox");

            base.OnGotFocus(e);

            CreateCaret();
        }

        /// <summary>
        /// Raises the LostFocus event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected override void OnLostFocus(EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("OnLostFocus()", "HexBox");

            base.OnLostFocus(e);

            DestroyCaret();
        }

        void _byteProvider_LengthChanged(object sender, EventArgs e)
        {
            UpdateScrollSize();
            UpdateRectanglePositioning();
        }

        /// <summary>
        /// Raises the HighlightRegionAdded event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHighlightRegionAdded(EventArgs e)
        {
            HighlightRegionAdded?.Invoke(this, e);
        }

        /// <summary>
        /// Raises the HighlightRegionSelected event.
        /// </summary>
        /// <param name="e">An EventArgs that contains the event data.</param>
        protected virtual void OnHighlightRegionSelected(EventArgs e)
        {
            HighlightRegionSelected?.Invoke(this, e);
        }
        #endregion

        #region Scaling Support for High DPI resolution screens
        /// <summary>
        /// For high resolution screen support
        /// </summary>
        /// <param name="factor">the factor</param>
        /// <param name="specified">bounds</param>
        protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
        {
            base.ScaleControl(factor, specified);

            this.BeginInvoke(new MethodInvoker(() =>
                {
                    this.UpdateRectanglePositioning();
                    if (_caretVisible)
                    {
                        DestroyCaret();
                        CreateCaret();
                    }
                    this.Invalidate();
                }));
        }
        #endregion

        public System.Drawing.Font BoldFont { get; set; }
        /// <summary>
        /// Sets the minimum amount of bytes per row that can be displayed.
        /// </summary>
        public int MinBytesPerLine { get; set; }

        /// <summary>
        /// Sets the maximum amount of bytes per row that can be displayed.
        /// </summary>
        public int MaxBytesPerLine { get; set; }

        public Color LineInfoForeColor { get; set; }

        #region DarkModeSupport

        /// <summary>
        /// Gets the background color for the control.
        /// </summary>
        //[DefaultValue(typeof(Color), Control.D)]
        public override Color BackColor => backColor;

        /// <summary>
        /// Gets the background color for the control.
        /// </summary>
        [DefaultValue(typeof(Color), "Black")]
        public override Color ForeColor => foreColor;

        public static void SetColors(Color background, Color text)
        {
            backColor = background;
            foreColor = text;
        }

        private static Color backColor = SystemColors.Window;
        private static Color foreColor = SystemColors.ControlText;

        #endregion

        #region NotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Notifies listeners when given property is updated.
        /// </summary>
        /// <param name="propertyname">Name of property to give notification for. If called in property, argument can be ignored as it will be default.</param>
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyname = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyname));
        }

        /// <summary>
        /// Sets given property and notifies listeners of its change. IGNORES setting the property to same value.
        /// Should be called in property setters.
        /// </summary>
        /// <typeparam name="T">Type of given property.</typeparam>
        /// <param name="field">Backing field to update.</param>
        /// <param name="value">New value of property.</param>
        /// <param name="propertyName">Name of property.</param>
        /// <returns>True if success, false if backing field and new value aren't compatible.</returns>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}
