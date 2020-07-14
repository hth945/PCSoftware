using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PCSoftware
{
    public struct QueueData
    {
        public string cmd;
        public object o;
    }

    public class MyIAP
    {
        public int startA = 0;  //起始地址
        public int restMode = 0; // 复位方式 0 调用复位委托(若不存在则不复位)  1 "rest\n"
        public int restTime = 2000;
        byte[] hexB;

        byte[] outBTem;
        public int threadIsRun = 0;
        Thread mThread;

        public MyCommunication mc;  //外部实现 通信接口

        public delegate int MCURestDelegate();
        public MCURestDelegate MCURestD; //mcu复位委托 相当于函数指针

        public delegate int startDelegate();
        public exitDelegate startD; //start 开始下载委托 相当于函数指针

        public delegate int exitDelegate();
        public exitDelegate exitD; //exit 下载完成委托 相当于函数指针

        public ConcurrentQueue<QueueData> uiRecQueue = new ConcurrentQueue<QueueData>();//portrec
        public ConcurrentQueue<QueueData> uiQueue = new ConcurrentQueue<QueueData>();  //sendtoui

        //进入iap模式
        private int MCURest(int mode, int oTime)
        {
            if (mode == 0)
            {
                if (MCURestD!= null)
                {
                    MCURestD();
                }
            }else if (mode == 1)
            {
                byte[] byteArray = System.Text.Encoding.ASCII.GetBytes("Rest\n");
                mc.Write(byteArray, byteArray.Length);
            }

            long lastTime = DateTime.Now.Ticks / 10000;
            int i = 0;
            while (true)
            {
                if (mc.runCmdPact(0x01, null, 0, 55, out outBTem) >= 0)
                {
                    setText("debug", "进入iap成功\r\n");
                    break;
                }
                i++;

                if (DateTime.Now.Ticks / 10000 - lastTime > oTime)
                {
                    setText("debug", "进入iap失败\r\n");
                    setText("debug", "i: "+i+"\r\n");
                    return -1;
                }

                Thread.Sleep(1);
            }

            // 回读状态 判断是否进入iap
            return 0;
        }

        private int downlandIAP()
        {
            int i;
            byte[] b2;
            int start = 0;

            byte[] b = new byte[1024 * 1024];
            string name = "";
            int l;

            if (startD != null)
                startD();

            if (MCURest(restMode, restTime) < 0)
                return -1;

            setProgressBar1("Value", 0);
            if ((l = HEX2BIN.readHex(hexB, ref b, b.Length ,ref start,ref name)) < 0)
            {
                setText("debug", "读取文件失败\r\n");
                return -1;
            }
            setText("debug", "hex name :"+ name +"\r\n");

            if (start != startA)
            {
                setText("debug", "首地址错误\r\n");
                return -1;
            }
            

            setProgressBar1("Maximum",l);
            //Thread.Sleep(500);

            if (mc.runCmdPact(0x47, null, 0, 3000,out outBTem) >= 0)
            {
                string str = Encoding.ASCII.GetString(outBTem);
                setText("debug", "回读旧程序型号:"+str.TrimEnd('\0') + "\r\n");
            }
            else
            {
                setText("debug", "未读取到型号\r\n");
            }
            
            /***********擦除数据**************/
            int[] intArray = new int[2] { startA, l };
            b2 = new byte[8];
            Buffer.BlockCopy(intArray, 0, b2, 0, b2.Length);
            if (mc.runCmdPact(0x41, b2, 8, 3000, out outBTem) >= 0)
            {
                setText("debug", "擦除成功\r\n");
            }
            else
            {
                setText("debug", "擦除错误\r\n");
                setText("debug", "threadStop\r\n");
                return -1;
            }

            /***********下载**************/
            b2 = new byte[512 + 8];
            i = 0;

            while(l > 0)
            {
                intArray[0] = startA + i;
                //setText("debug", "write"+i+"\r\n");
                if (l > 512)
                {
                    intArray[1] = 512;
                    Buffer.BlockCopy(intArray, 0, b2, 0, 8);
                    Buffer.BlockCopy(b, i, b2, 8, 512);
                    if (mc.runCmdPact(0x42, b2, 512 + 8, 2000, out outBTem) < 0)
                    {
                        setText("debug", "数据错误\r\n");
                        return -2;
                    }
                    l -= 512;
                    i += 512;
                    setProgressBar1("Value", i);
                }
                else
                {
                    intArray[1] = l;
                    Buffer.BlockCopy(intArray, 0, b2, 0, 8);
                    Buffer.BlockCopy(b, i, b2, 8, l);
                    if (mc.runCmdPact(0x42, b2, l + 8, 2000, out outBTem) < 0)
                    {
                        setText("debug", "数据错误\r\n");
                        return -2;
                    }
                    i += l;
                    setProgressBar1("Value", i);
                    l -= l;
                }
            }

            Thread.Sleep(50);
            /***********重启**************/
            if (mc.runCmdPact(0x43, null, 0, 1000, out outBTem) < 0)
            {
                setText("debug", "重启失败，请重新下载\r\n");
                return -3;
            }else
            {
                setText("debug", "下载成功\r\n");
            }

            return 0;
        }


        public MyIAP()
        {

            
        }

        public int startDownland(byte[] hex, int startArr)
        {
            hexB = hex;
            startA = startArr;

            if (threadIsRun != 0)
            {
                setText("debug", "线程正在运行");
                return -1;
            }
            
            mThread = new Thread(() => // Lambda 表达式
            {
                threadIsRun = 1;
                
                setText("debug", "进入线程");
                downlandIAP();
                setText("debug", "退出线程");
                if (exitD != null)
                    exitD();
                threadIsRun = 0;
            });
            mThread.Start();  // 开始

            return 0;
        }


        private void setText(string i, string s)
        {
            QueueData mpd = new QueueData();
            mpd.cmd = "text" + i;
            mpd.o = s;
            uiQueue.Enqueue(mpd);  //串口接收的数据发送给ui  ui发送过来的命令舍弃
        }

        private void setEnd(int i)
        {
            QueueData mpd = new QueueData();
            mpd.cmd = "end";
            mpd.o = i;
            uiQueue.Enqueue(mpd);  //串口接收的数据发送给ui  ui发送过来的命令舍弃
        }

        private void setProgressBar1(string i, int s)
        {
            QueueData mpd = new QueueData();
            mpd.cmd = "progressBar1." + i;
            mpd.o = s;
            uiQueue.Enqueue(mpd);  //串口接收的数据发送给ui  ui发送过来的命令舍弃
        }


    }
}
