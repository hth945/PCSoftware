using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ca310CCFL
{
    public class MyportData
    {
        public int num;
        public byte[] b;

        public byte start;
        public byte cmd;
        public int len;
        public byte[] data;
        public byte check;
        public byte end;


        public int saveChar(byte c)
        {
            if (num == 0)
            {
                if (c == 0xbb)
                {
                    check = 0;
                    start = c;
                    num++;
                }
                else
                {
                    num = 0;
                    return -1;
                }
            }
            else if (num == 1)
            {
                check ^= c;
                cmd = c;
                num++;
                data = new byte[4];
            }
            else if (num < 6)
            {
                check ^= c;
                data[num - 2] = c;
                num++;
                if (num == 6)
                {
                    len = 0;
                    for (int i = 0; i < 4; i++)
                    {
                        len = len * 256 + data[3 - i];
                    }
                    data = new byte[len];
                }
            }
            else if (num < 6 + len)
            {
                check ^= c;
                data[num - 6] = c;
                num++;
            }
            else if (num < 7 + len)
            {
                if (check == c)
                    num++;
                else
                {
                    num = 0;
                    return -2;
                }
            }
            else if (num < 8 + len)
            {
                end = c;
                if (c == 0xee)
                {
                    return 2;
                }
                else
                {
                    num = 0;
                    return -3;
                }
            }
            return 1;
        }

        public byte[] getByte()
        {
            b = new byte[len + 8];
            int l = (int)len;
            b[0] = start;
            b[1] = cmd;
            b[2] = (byte)(len % 256);
            len = len / 256;
            b[3] = (byte)(len % 256);
            len = len / 256;
            b[4] = (byte)(len % 256);
            len = len / 256;
            b[5] = (byte)(len % 256);

            if (l != 0)
                Buffer.BlockCopy(data, 0, b, 6, l);

            b[6 + l] = 0;
            b[7 + l] = end;
            for (int i = 1; i < l + 6; i++)
                b[6 + l] ^= b[i];

            return b;
        }
    }
}
