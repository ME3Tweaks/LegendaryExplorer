using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ME3LibWV
{
    public static class DebugLog
    {
        public static RichTextBox rtb;
        public static FileStream fs = new FileStream(Path.GetDirectoryName(Application.ExecutablePath) + "\\DebugLog.txt", FileMode.Create, FileAccess.Write);
        public static int MaxChars = 1000000;
        private static readonly object _sync = new object();
        private static bool DebugToFile = true;
        private static StringBuilder text = new StringBuilder();


        public static void SetBox(RichTextBox box, int MaxChar = 1000000)
        {
            try
            {
                rtb = box;
                MaxChars = MaxChar;
                Update();
            }
            catch { }
        }
        public static void SetDebugToFile(bool set)
        {
            lock (_sync)
            {
                DebugToFile = set;
            }
        }
        public static void PrintLn(string s, bool update = false)
        {
            if (rtb != null)
            {
                lock (_sync)
                {
                    try
                    {
                        string res = DateTime.Now.ToLongTimeString() + " :  " + s + '\n';
                        text.Append(res);
                        res = res.Replace("\n", System.Environment.NewLine);
                        if (DebugToFile)
                            fs.Write(Str2Arr(res), 0, res.Length);
                    }
                    catch { }
                }
                if (update)
                    Update();
            }
        }
        public static void Print(string s, bool update = false)
        {
            if (rtb != null)
            {
                lock (_sync)
                {
                    try
                    {
                        string res = DateTime.Now.ToLongTimeString() + " :  " + s;
                        text.Append(res);
                        if (DebugToFile)
                            fs.Write(Str2Arr(res), 0, res.Length);
                    }
                    catch { }
                }
                if (update)
                    Update();
            }
        }
        public static void Clear(bool update = true)
        {
            if (rtb != null)
            {
                lock (_sync)
                {
                    try
                    {
                        rtb.Invoke(new Action(() => rtb.Clear()));
                        text.Clear();
                        fs = new FileStream(Path.GetDirectoryName(Application.ExecutablePath) + "\\DebugLog.txt", FileMode.Create, FileAccess.Write);
                    }
                    catch { }

                }
                if (update)
                    Update();
            }
        }
        public static void Update()
        {
            if (rtb != null)
            {
                lock (_sync)
                {
                    try
                    {
                        string s = text.ToString();
                        if (s.Length > MaxChars)
                            s = s.Substring(s.Length - MaxChars, MaxChars);
                            rtb.Invoke(new Action(() =>
                                {
                                    rtb.Text = s;
                                    rtb.SelectionStart = s.Length;
                                    rtb.ScrollToCaret();
                                }));
                    }
                    catch { }
                }
            }
        }
        
        public static byte[] Str2Arr(string s)
        {
            byte[] res = new byte[s.Length];
            for (int i = 0; i < s.Length; i++)
                res[i] = (byte)s[i];
            return res;
        }
    }
}
