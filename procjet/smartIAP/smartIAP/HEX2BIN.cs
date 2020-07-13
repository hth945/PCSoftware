using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PCSoftware
{
    static class HEX2BIN
    {
        static public string readName(byte[] b)
        {
            int i;
            for (i = 0; i < 128; i++)
            {
                if (b[0x0f00 + i] == 0)
                {
                    break;
                }
            }
            byte[] s = new byte[i];
            Array.Copy(b, 0x0f00, s, 0, i);
            string str = Encoding.Default.GetString(s);
            return str;
        }

        static public int readHex(byte[] hexB,ref byte[] b, int len, ref int start, ref string name)
        {
            int a = 0; 
            int ofset = 0, l = 0;
            //StreamReader sr = new StreamReader(fs);
            Stream stream = new MemoryStream(hexB);
            StreamReader sr = new StreamReader(stream);

            string s;
            if ((s = sr.ReadLine()) == null)
                return -1;
            HENLINE hl = new HENLINE(s);
            if (hl.Conversion() < 0)
                return -2;
            if (hl.cmd != 04)
                return -3;
            start = hl.data[0] * 256 * 256 * 256 + hl.data[1] * 256 * 256;
            ofset = start;

            while ((s = sr.ReadLine()) != null)
            {
                hl = new HENLINE(s);
                if (hl.Conversion() < 0)
                    return -4;
                if (hl.cmd == 0x04)
                {
                    ofset = hl.data[0] * 256 * 256 * 256 + hl.data[1] * 256 * 256;
                }
                else if (hl.cmd == 0x00)
                {
                    if (a == 0)
                    {
                        a = 1;
                        start += hl.offset;
                    }
                    l = ofset - start + hl.offset + hl.data.Length;
                    if (len < l)
                        return -5;
                    Array.Copy(hl.data, 0, b, ofset - start + hl.offset, hl.data.Length);
                }
                else if (hl.cmd == 0x01)
                {
                    name = readName(b);
                    return l;
                }
            }
            return 0;
        }
    }
    class HENLINE
    {
        string s;
        int len;
        public int offset;
        public int cmd;
        public byte[] data;
        int crc;

        public HENLINE(string s)
        {
            this.s = s;
        }

        public int Conversion()
        {
            
            string ss = s.Substring(0, 1);
            if (ss != ":")
                return -1;

            ss = s.Substring(1, 2);
            len = Convert.ToByte(ss, 16);
            crc += len;
            byte[] b = new byte[len + 4];
            data = new byte[len];
            
            for (int i = 0; i < len + 4; i++)
            {
                ss = s.Substring(3 + i * 2, 2);
                b[i] = Convert.ToByte(ss, 16);
                crc += b[i];
            }
            if ((crc % 256) != 0)
                return -2;

            for (int i = 0; i < len ; i++)
            {
                data[i] = b[i + 3];
            }
            offset = b[0] * 256 + b[1];
            cmd = b[2];
            crc = b[3 + len];

            return 1;
        }
    }
}
