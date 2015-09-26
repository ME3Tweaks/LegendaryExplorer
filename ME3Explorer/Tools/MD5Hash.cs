using System;
using System.IO;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ME3Explorer
{
    public static class MD5Hash
    {
        public static string FromFile(string path)
        {
            if (File.Exists(path))
            {
                FileStream file = new FileStream(path, FileMode.Open);
                string res = FromStream(file);
                file.Close();
                return res;
            }
            else
                return "";
        }

        public static string FromString(string s)
        {
            MemoryStream m = new MemoryStream(ASCIIEncoding.Default.GetBytes(s));
            string res = FromStream(m);
            return res;
        }

        public static string FromStream(Stream s)
        {
            MD5 md5 = new MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(s);
            string res = "";
            for (int i = 0; i < retVal.Length; i++)
                res += retVal[i].ToString("x2");
            return res;
        }
    }
}
