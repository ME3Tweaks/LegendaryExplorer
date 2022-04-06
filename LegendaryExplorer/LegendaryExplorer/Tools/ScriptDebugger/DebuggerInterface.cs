using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using LegendaryExplorerCore.Helpers;
using LegendaryExplorerCore.Packages;
using LegendaryExplorerCore.Unreal;

namespace LegendaryExplorer.Tools.ScriptDebugger
{
    public partial class DebuggerInterface : IDisposable
    {
        public readonly MEGame Game;
        private readonly uint windowsMessageFilter;
        private readonly IntPtr MEHandle;
        private IntPtr NamePool;

        public bool InBreakState;

        public event Action OnDetach;
        public event Action OnAttach;
        public event Action OnBreak;
        
        public readonly List<DebuggerFrame> CallStack = new();

        public DebuggerInterface(MEGame game, Process meProcess)
        {
            Game = game;
            if (!game.IsLEGame())
            {
                throw new ArgumentException($@"{nameof(game)} must be either LE1. LE2, or LE3", nameof(game));
            }
            if (PresentationSource.FromVisual(App.Instance.MainWindow) is HwndSource hwndSource)
            {
                hwndSource.AddHook(WndProc);
            }
            windowsMessageFilter = game switch
            {
                MEGame.LE1 => 0x02AC00D7,
                MEGame.LE2 => 0x02AC00D8,
                _ => 0x02AC00D9,
            };

            const uint VirtualMemoryOperation = 0x8;
            const uint VirtualMemoryRead = 0x10;
            const uint VirtualMemoryWrite = 0x20;

            MEHandle = OpenProcess(VirtualMemoryOperation | VirtualMemoryRead | VirtualMemoryWrite, false, (uint)meProcess.Id);

        }

        public void Attach()
        {
            SendMessage(new[] { (byte)PipeCommands.AttachDebugger });
        }

        public void Detach()
        {
            SendMessage(new[] { (byte)PipeCommands.DetachDebugger });
        }

        public void BreakASAP()
        {
            SendMessage(new[] { (byte)PipeCommands.BreakImmediate });
        }

        public void SetBreakPoint(string functionPath, ushort location) => BreakPoint(true, functionPath, location);
        public void RemoveBreakPoint(string functionPath, ushort location) => BreakPoint(false, functionPath, location);

        private void BreakPoint(bool add, string functionPath, ushort location)
        {
            var ms = new MemoryStream();
            ms.WriteByte((byte)PipeCommands.Breakpoint);
            ms.WriteByte((byte)(add ? PipeCommands.Add : PipeCommands.Remove));
            ms.WriteUInt16(location);
            ms.WriteStringASCIINull(functionPath);
            SendMessage(ms.ToArray());
        }

        public void StepInto()
        {
            if (!InBreakState)
            {
                throw new InvalidOperationException("Can't StepInto when not in break state.");
            }
            SendMessage(new[] { (byte)PipeCommands.StepInto });
            InBreakState = false;
        }

        public void StepOver()
        {
            if (!InBreakState)
            {
                throw new InvalidOperationException("Can't StepOver when not in break state.");
            }
            SendMessage(new[] { (byte)PipeCommands.StepOver });
            InBreakState = false;
        }

        public void StepOut()
        {
            if (!InBreakState)
            {
                throw new InvalidOperationException("Can't StepOut when not in break state.");
            }
            SendMessage(new[] { (byte)PipeCommands.StepOut });
            InBreakState = false;
        }

        public void Resume()
        {
            if (!InBreakState)
            {
                throw new InvalidOperationException("Can't Resume when not in break state.");
            }
            SendMessage(new[] { (byte)PipeCommands.Resume });
            InBreakState = false;
        }

        private enum PipeCommands : byte
        {
            AttachDebugger = 1,
            DetachDebugger = 2,
            BreakImmediate = 3,
            Breakpoint = 4,
                Add = 5,
                Remove = 6,
            StepInto = 7,
            StepOver = 8,
            StepOut = 9,
            Resume = 10,
        };

        private void SendMessage(byte[] bytes)
        {
            Task.Run(() =>
            {
                using var client = new NamedPipeClientStream($"LEX_{Game}_SCRIPTDEBUG_PIPE");
                client.Connect();
                client.Write(bytes.AsSpan());
                client.Flush();
            });
        }

        private void ProcessMessage(string msg, IntPtr debuggerFrame)
        {
            switch (msg)
            {
                case "Attached":
                    if (OnAttach is null)
                    {
                        //Debugger has been disconnected, allow program to resume 
                        Detach();
                        return;
                    }
                    OnAttach();
                    break;
                case "Detached":
                    OnDetach?.Invoke();
                    break;
                case "Break":
                    EnterBreakState(debuggerFrame);
                    break;
            }
        }

        private void EnterBreakState(IntPtr framePtr)
        {
            InBreakState = true;
            ClassCache.Clear();
            ObjectCache.Clear();
            NameStringCache.Clear();
            CallStack.Clear();
            while (framePtr != IntPtr.Zero)
            {
                var frame = ReadValue<DebuggerFrame>(framePtr);
                CallStack.Add(frame);
                framePtr = frame.PreviousFrame;
            }

            OnBreak?.Invoke();
        }

        public List<PropertyValue> LoadLocals(DebuggerFrame frame)
        {
            var locals = new List<PropertyValue>();
            if (frame.NativeFunction == IntPtr.Zero && ReadObject(frame.Node) is NStruct func)
            {
                for (NField child = func.FirstChild; child is not null; child = child.Next)
                {
                    if (child is not NProperty prop || prop.PropertyFlags.Has(UnrealFlags.EPropertyFlags.ReturnParm))
                    {
                        continue;
                    }
                    IntPtr propAddr = IntPtr.Zero;
                    if (prop.PropertyFlags.Has(UnrealFlags.EPropertyFlags.OutParm))
                    {
                        OutParmInfo outParmInfo;
                        for (IntPtr outParmInfoPtr = frame.OutParms; outParmInfoPtr != IntPtr.Zero; outParmInfoPtr = outParmInfo.Next)
                        {
                            outParmInfo = ReadValue<OutParmInfo>(outParmInfoPtr);
                            var outParmProp = ReadObject(outParmInfo.Prop);
                            if (outParmProp == prop)
                            {
                                propAddr = outParmInfo.PropAddr;
                                break;
                            }
                        }
                    }
                    else
                    {
                        propAddr = frame.Locals + prop.Offset;
                    }

                    if (propAddr == IntPtr.Zero)
                    {
                        continue;
                    }
                    prop.ReadProperty(propAddr, locals);
                }
            }
            locals.Sort((val1, val2) => string.CompareOrdinal(val1.PropName, val2.PropName));
            if (frame.Object != IntPtr.Zero)
            {
                locals.Add(new ObjectPropertyValue(this, IntPtr.Zero, "Self", ReadObject(frame.Object)));
            }
            return locals;
        }

        [StructLayout(LayoutKind.Sequential)]
        struct COPYDATASTRUCT
        {
            public ulong dwData;
            public uint cbData;
            public IntPtr lpData;
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1, Size = 12)]
        struct FNameEntry
        {
            [FieldOffset(0x0)]
            private uint Index;
            [FieldOffset(0x4)]
            private IntPtr HashNext; //FNameEntry*

            public uint Length => (Index >> 20) & 0b_111111111;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FName
        {
            public readonly uint OffsetAndChunkBitField;
            public readonly int Number;
            public uint Offset => OffsetAndChunkBitField & 0b_11111_11111111_11111111_11111111;
            public uint Chunk => (OffsetAndChunkBitField >> 29) & 0b_111;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FScriptDelegate
        {
            public IntPtr Object; //UObject*
            public FName FunctionName;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct FScriptInterface
        {
            public IntPtr Object; //UObject*
            public IntPtr Interface; //void*
        }

        [StructLayout(LayoutKind.Sequential)]
        struct OutParmInfo
        {
            public IntPtr Prop; //UProperty*
            public IntPtr PropAddr; //byte*
            public IntPtr Next; //OutParmInfo*
        }

        [StructLayout(LayoutKind.Sequential)]
        unsafe struct LexMsg
        {
            public IntPtr currentFrame; //DebuggerFrame*
            public IntPtr NamePool; //FNameEntry**
            public ulong msgLength; 
            public fixed ushort msg[1024];
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct DebuggerFrame
        {
            public IntPtr Node; //UStruct*
            public IntPtr Object; //UObject*
            public IntPtr CodeBaseAddr; //byte*
            public IntPtr Locals; //byte*
            public IntPtr OutParms; //OutParmInfo*
            public IntPtr PreviousFrame; //DebuggerFrame*
            public IntPtr NativeFunction; //UFunction*
            public IntPtr NodePath; //char*
            public IntPtr FileName; //wchar_t*
            public ushort FileNameLength;
            public ushort NodePathLength;
            public ushort CurrentPosition;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct TArray
        {
            public IntPtr Data; //T*
            public int Count;
            public readonly int Max;
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_COPYDATA = 0x004a;
            if (msg == WM_COPYDATA)
            {
                var cds = Marshal.PtrToStructure<COPYDATASTRUCT>(lParam);
                if (cds.dwData == windowsMessageFilter)
                {
                    var msgStruct = Marshal.PtrToStructure<LexMsg>(cds.lpData);
                    NamePool = msgStruct.NamePool;
                    char[] msgChars = new char[msgStruct.msgLength];
                    unsafe
                    {
                        Marshal.Copy((IntPtr)msgStruct.msg, msgChars, 0, msgChars.Length);
                    }
                    ProcessMessage(new string(msgChars), msgStruct.currentFrame);

                    handled = true;
                    return (IntPtr)1;
                }
            }
            return IntPtr.Zero;
        }

        public unsafe T ReadValue<T>(IntPtr address) where T : unmanaged
        {
            Unsafe.SkipInit(out T value);
            var byteSpan = new Span<byte>(&value, sizeof(T));
            ReadProcessMemory(address, byteSpan);
            return value;
        }

        public unsafe void WriteValue<T>(IntPtr address, T value) where T : unmanaged
        {
            var byteSpan = new Span<byte>(&value, sizeof(T));
            fixed (byte* tPtr = byteSpan)
            {
                if (!WriteProcessMemory(MEHandle, address, tPtr, sizeof(T), out _))
                {
                    throw new AccessViolationException();
                }
            }
        }

        public unsafe string ReadASCIIString(IntPtr address, int length)
        {
            if (length <= 0)
            {
                return "";
            }
            Span<byte> bytes = stackalloc byte[length];
            ReadProcessMemory(address, bytes);
            return Encoding.ASCII.GetString(bytes);
        }
        
        public unsafe string ReadUnicodeString(IntPtr address, int length)
        {
            if (length <= 0)
            {
                return "";
            }
            Span<byte> bytes = stackalloc byte[length * 2];
            ReadProcessMemory(address, bytes);
            return Encoding.Unicode.GetString(bytes);
        }

        //returns false if there was not enough space to write the string. Since we can't allocate new memory, we are bound by the existing allocation length
        public unsafe bool WriteUnicodeString(IntPtr address, string str, int maxCharsAvailable)
        {
            var charSpan = str.AsSpan();
            var bytesLength = Encoding.Unicode.GetByteCount(charSpan);
            if ((bytesLength + 2) > maxCharsAvailable * 2)
            {
                return false;
            }
            Span<byte> bytes = stackalloc byte[maxCharsAvailable * 2];
            bytes.Fill(0);
            Encoding.Unicode.GetBytes(charSpan, bytes);
            fixed (byte* bytePtr = bytes)
            {
                if (!WriteProcessMemory(MEHandle, address, bytePtr, bytes.Length, out _))
                {
                    throw new AccessViolationException();
                }
            }
            return true;
        }

        //FName.Chunk is a three bit number, so there is a max of 8 chunk pointers
        private readonly IntPtr[] chunkPtrCache = new IntPtr[8];
        private readonly Dictionary<uint, string> NameStringCache = new();

        public unsafe NameReference GetNameReference(FName fName)
        {
            if (NameStringCache.TryGetValue(fName.OffsetAndChunkBitField, out string cachedString))
            {
                return new NameReference(cachedString, fName.Number);
            }
            ref IntPtr chunkPtr = ref chunkPtrCache[fName.Chunk];
            if (chunkPtr == IntPtr.Zero)
            {
                chunkPtr = ReadValue<IntPtr>(NamePool + (int)(fName.Chunk * sizeof(IntPtr)));
            }
            IntPtr fNameAddress = chunkPtr + (int)fName.Offset;
            var entry = ReadValue<FNameEntry>(fNameAddress);
            string str = ReadASCIIString(fNameAddress + sizeof(FNameEntry), (int)entry.Length);
            NameStringCache[fName.OffsetAndChunkBitField] = str;
            return new NameReference(str, fName.Number);
        }

        private unsafe void ReadProcessMemory(IntPtr address, Span<byte> bytes)
        {
            fixed (byte* bytePtr = bytes)
            {
                if (!ReadProcessMemory(MEHandle, address, bytePtr, bytes.Length, out _))
                {
                    throw new AccessViolationException();
                }
            }
        }

        private void ReleaseResources()
        {
            if (PresentationSource.FromVisual(App.Instance.MainWindow) is HwndSource hwndSource)
            {
                hwndSource.RemoveHook(WndProc);
            }
            CloseHandle(MEHandle);
        }

        public void Dispose()
        {
            ReleaseResources();
            GC.SuppressFinalize(this);
        }

        ~DebuggerInterface()
        {
            ReleaseResources();
        }

        [DllImport("kernel32.dll")]
        static extern unsafe bool ReadProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte* lpBuffer,
            int dwSize,
            out IntPtr lpNumberOfBytesRead); 

        [DllImport("kernel32.dll")]
        static extern unsafe bool WriteProcessMemory(
            IntPtr hProcess,
            IntPtr lpBaseAddress,
            byte* lpBuffer,
            int nSize,
            out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(
            uint processAccess,
            bool bInheritHandle,
            uint processId
        );

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);
    }
}
