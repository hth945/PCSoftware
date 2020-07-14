using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCSoftware
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

 

    public class MyCommunication 
    {
        public delegate int WriteDelegate(byte[] data, int len);

        MyportData myRecData;
        MyportData mySendData;
        int recFlag = 0;
        long lastTime;
        long outTime = 50; //超时时间设置 默认50ms
        AutoResetEvent rectEvent = new AutoResetEvent(false);

        public WriteDelegate Write; //委托 相当于函数指针

        public MyCommunication()
        {
            myRecData = new MyportData();
            mySendData = new MyportData();
            lastTime = DateTime.Now.Ticks / 10000; //ms数
        }

        //virtual public int Write(byte[] data, int len)
        //{
        //    return 0;
        //}

        public int recive(byte[] data, int len)
        {
            if (recFlag == 2) //已经有完整的一帧数据缓存
            {
                return 2;
            }

            long T = DateTime.Now.Ticks / 10000 - lastTime;
            lastTime = DateTime.Now.Ticks / 10000;
            if (T > outTime)   //超时 并且没有接收完毕 那么从新开始
            {
                myRecData.num = 0;
                recFlag = 0;
            }
                

            for (int i = 0; i < len; i++)
            {
                recFlag = myRecData.saveChar(data[i]);
                if (recFlag == 2)
                {
                    rectEvent.Set();
                    return 2;
                }
            }
            return 0;
        }

        private int restRec()
        {
            myRecData.num = 0;
            recFlag = 0;
            rectEvent.Reset();
            return 0;
        }

        public int waitData(byte cmd, int time, out byte[] outByte)
        {
            if (rectEvent.WaitOne(time))  //ms\
            {
                if (recFlag == 2)
                {
                    if (myRecData.cmd == (byte)(cmd | 0x10))
                    {
                        outByte = myRecData.data;
                        return 0;
                    }
                }
            }
            outByte = null;
            return -1;
        }
        public int sendData( byte cmd, byte[] data, int len)
        {
            mySendData.start = 0xbb; // aa 01 00 00 00 00 ee
            mySendData.cmd = cmd;
            mySendData.len = len;
            mySendData.data = data;
            mySendData.end = 0xee;
            byte[] b = mySendData.getByte();
            return Write(b, b.Length);            //发送命令
        }
        public int runCmdPact(byte cmd, byte[] data, int len,int oTime, out byte[] outByte)
        {
            outByte = null;
            restRec();
            if (sendData(cmd, data, len) < 0)
                return -1;
            return waitData(cmd, oTime,out outByte);
        }
    }
}
