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

namespace ca310CCFL
{
    public struct QueueData
    {
        public string cmd;
        public object o;
    }

    public class BData
    {
        public int downMode;   //0正常启动  1 其他冷启动 2 镜头冷启动
        public string COM;     //串口端口
        public byte type;      
        public byte id;
        public string path;    //下载文件路径
        public int startA;     //app基地址
    }

    public class MyThreadPort
    {
        public byte downMode = 0x42; //0x42 不加密 0x44通用加密 0x45唯一ID加密
        public byte type;
        public byte id;
        public string Path = "";
        public int startA = 0;
        public int oldUp = 0;  // 1 旧s10 ，0 新s10与老化 2，cfl串口

        public int portFlag = 0;//-1 正在查找 0不查找 1已找到正确串口
        public int threadFlag = 0;
        public int threadIsRun = 0;
        Thread mThread;

        MyportData g_tem;
        public SerialPort serialPort1;

        public ConcurrentQueue<QueueData> uiRecQueue = new ConcurrentQueue<QueueData>();//portrec
        public ConcurrentQueue<QueueData> portRecQueue = new ConcurrentQueue<QueueData>();//portrec
        public ConcurrentQueue<QueueData> uiQueue = new ConcurrentQueue<QueueData>();  //sendtoui

        //进入iap模式
        private void MCURest()
        {
            serialPort1.RtsEnable = true;
            Thread.Sleep(1);
            serialPort1.DtrEnable = false;
            Thread.Sleep(1);
            serialPort1.DtrEnable = true;
        }

        //进入正常运行模式
        private void MCURest2()
        {
            serialPort1.RtsEnable = true;
            Thread.Sleep(1);
            serialPort1.DtrEnable = false;
            Thread.Sleep(1);
            serialPort1.RtsEnable = false;
        }

        public MyThreadPort()
        {
            serialPort1 = new SerialPort();
            serialPort1.DataReceived += new SerialDataReceivedEventHandler(DataReceive);  //实例化委托对象
            serialPort1.BaudRate = 460800;
            serialPort1.ReadBufferSize = 81920;
            serialPort1.ReadTimeout = 50;
            
            serialPort1.DataBits = 8;
            //serialPort1.DtrEnable = true;
            //serialPort1.Handshake = Handshake.RequestToSend;
            serialPort1.StopBits = StopBits.One;
            serialPort1.Parity = Parity.None;

            mThread = new Thread(() => // Lambda 表达式
            {
                threadRun();
            });
            mThread.Start();  // 开始
        }
        
        private int IAPUpdateCOMport()//在新线程中调用
        {
            MyportData sendData = new MyportData();
            //portFlag = 1;
            for (int i = 1; i < 255; i++)
            {
                serialPort1.Close();
                try
                {
                    serialPort1.PortName = "COM" + i;
                    serialPort1.Open();
                    QueueData c;
                    int time = 0;
                    
                    /*********************特殊板子 镜头*************************************/
                    if ((type == 15) || (type == 12))  //cfl镜头 可能使用转板 字符串形式
                    {
                        serialPort1.BaudRate = 115200;
                        while (true)
                        {
                            time++;
                            byte[] b = Encoding.ASCII.GetBytes("Rest\r");
                            //b = sendData.getByte();
                            serialPort1.Write(b, 0, b.Length);            //发送命令

                            if (time > 10)
                                break;
                            Thread.Sleep(20);

                            while (portRecQueue.TryDequeue(out c))
                            {
                                if (c.cmd == "port2Thread")
                                {
                                    MyportData tem = (MyportData)c.o;
                                    if (tem.cmd == 0x11)
                                    {
                                        oldUp = 2;
                                        serialPort1.BaudRate = 460800;
                                        return 1; //找到串口了
                                    }
                                }
                            }
                        }

                        time = 0;
                        serialPort1.BaudRate = 460800;
                        while (true)
                        {
                            time++;
                            byte[] b;
                            sendData.start = 0xbb;
                            sendData.cmd = 0x01;
                            sendData.len = 0;
                            sendData.end = 0xee;
                            b = sendData.getByte();
                            serialPort1.Write(b, 0, b.Length);            //发送命令

                            if (time > 5)
                                break;
                            Thread.Sleep(20);

                            while (portRecQueue.TryDequeue(out c))
                            {
                                if (c.cmd == "port2Thread")
                                {
                                    MyportData tem = (MyportData)c.o;
                                    if (tem.cmd == 0x11)
                                    {
                                        oldUp = 2;
                                        serialPort1.BaudRate = 460800;
                                        return 1; //找到串口了
                                    }
                                }
                            }
                        }
                    }


                    /************S10*************************/
                    time = 0;
                    serialPort1.BaudRate = 14400;
                    while (true)
                    {
                        time++;
                        byte[] b;
                        sendData.start = 0xbb;
                        sendData.cmd = 0x01;
                        sendData.len = 0;
                        sendData.end = 0xee;
                        b = sendData.getByte();
                        serialPort1.Write(b, 0, b.Length);            //发送命令

                        if (time > 5)
                            break;
                        Thread.Sleep(20);

                        while (portRecQueue.TryDequeue(out c))
                        {
                            if (c.cmd == "port2Thread")
                            {
                                MyportData tem = (MyportData)c.o;
                                if (tem.cmd == 0x11)
                                {
                                    oldUp = 0;
                                    serialPort1.BaudRate = 460800;
                                    return 1; //找到串口了
                                }
                            }
                        }
                    }

                    /****************旧老化主机***********************/
                    time = 0;
                    if (type == 1)
                    {
                        MCURest();
                        serialPort1.BaudRate = 460800;
                        while (true)
                        {
                            time++;
                            byte[] b;
                            sendData.start = 0xaa;
                            sendData.cmd = 0x01;
                            sendData.len = 0;
                            sendData.end = 0xee;
                            b = sendData.getByte();
                            serialPort1.Write(b, 0, b.Length);            //发送命令

                            if (time > 75)
                                break;
                            Thread.Sleep(20);

                            while (portRecQueue.TryDequeue(out c))
                            {
                                if (c.cmd == "port2Thread")
                                {
                                    MyportData tem = (MyportData)c.o;
                                    if (tem.cmd == 0x01)
                                    {
                                        oldUp = 1;
                                        return 1; //找到串口了
                                    }
                                }
                            }
                        }
                    }
                    serialPort1.Close();
                }
                catch (Exception)
                {
                    continue;
                }
            }
            return -1;
        }
        
        void threadRun()
        {
            QueueData b;
            while (true)
            {
                if ((threadFlag < 0) || (threadFlag > 200))//命令控制或超时关闭
                {
                    setText("debug", "thread out");
                    return;
                }
                else if (threadFlag > 0)
                    threadFlag++;

                
                while (uiRecQueue.TryDequeue(out b)) //清空队列
                {
                    threadIsRun = 1;
                    setText("debug", "进入线程");
                    //if (processUIcmd(b) < 0) //命令执行不成功 重新连接
                    //{
                    //    setText("end", "下载失败\r\n");
                    //}else
                    //{
                    //    setText("end", "下载成功\r\n");
                    //}
                    setEnd(processUIcmd(b));
                    setText("debug", "退出线程");
                    serialPort1.Close();
                    threadIsRun = 0;
                }
                
                Thread.Sleep(5);
            }
        }

        private void setText(string i,string s)
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

        private void DataReceive(object sender, SerialDataReceivedEventArgs e)
        {
            int i = 0;
            byte j;
            MyportData myRecData = new MyportData();
            while (true)
            {
                try
                {
                    j = (byte)serialPort1.ReadByte();
                    i = myRecData.saveChar(j);//返回值 1 正在接收 2接收完成 负数有错误自动重新开始
                    if (i == 2)
                    {
                        QueueData qd = new QueueData();
                        qd.cmd = "port2Thread";
                        qd.o = myRecData;
                        portRecQueue.Enqueue(qd);
                        myRecData = new MyportData();
                        myRecData.num = 0;
                    }
                }
                catch (Exception)
                {
                    myRecData.num = 0;
                    return;
                }
            }
        }

        private int blockRead(byte cmd, byte[] data, int len,ref MyportData myD)
        {
            QueueData c;
            int i = 0,time = 0;
            byte j;
            serialPort1.ReadTimeout = 2000;
            myD.start = 0xaa; // aa 01 00 00 00 00 ee
            myD.cmd = cmd;
            myD.len = len;
            myD.data = data;
            myD.end = 0xee;
            byte[] b = myD.getByte();
            serialPort1.Write(b, 0, b.Length);            //发送命令

            while (true)
            {
                while (portRecQueue.TryDequeue(out c))
                {
                    if (c.cmd == "port2Thread")
                    {
                        myD = (MyportData)c.o;
                        return 1; //找到串口了
                    }
                }

                if (time > 75)
                    break;
                Thread.Sleep(20);
            }
            return 0;
        }

        public int readFile(ref byte[] b, ref int start)
        {
            
            FileStream strmReaderObj;
            try
            {
                strmReaderObj = new FileStream(Path, FileMode.Open, FileAccess.Read);
            }
            catch (Exception)
            {
                setText("debug", "文件路径错误\r\n");
                return -1;
            }

            string strExtension = "";

            int nIndex = Path.LastIndexOf('.');
            if (nIndex >= 0)
            {
                strExtension = Path.Substring(nIndex);
            }
            int l = 0;
            if (strExtension.ToLower() == ".hex")
            {
                HEX2BIN h2b = new HEX2BIN();
                l = h2b.Conversion(strmReaderObj, b, b.Length,ref start);
                downMode = 0x42;
            }
            else
            {
                l = strmReaderObj.Read(b, 0, b.Length);
                if (strExtension.ToLower() == ".bin")
                {
                    start = 0x8010000;
                    downMode = 0x42;
                }else if (strExtension.ToLower() == ".skyimg")
                {
                    start = startA;
                    downMode = 0x44;
                }
                else if (strExtension.ToLower() == ".skyimg2")
                {
                    start = startA;
                    downMode = 0x45;
                }
            }
                
            strmReaderObj.Close();

            if (l < 0)
            {
                setText("debug", "读取错误\r\n");
                setText("debug", "threadStop\r\n");
                return -1;
            }
            else
            {
                setText("debug", "读取成功数据大小为 " + l + " byte\r\n");
            }
            
            return l;
        }

        public string readName(int mode,ref byte[] b,ref int ll)
        {
            int i;
            if (mode == 0x42)
            {
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
            }else
            {
                for (i = 0; i < 128; i++)
                {
                    if (b[i] == 0)
                    {
                        break;
                    }
                }
                byte[] s = new byte[i];
                Array.Copy(b, 0, s, 0, i);
                string str = Encoding.Default.GetString(s);
                ll = ll - i - 1;
                s = new byte[ll];
                Array.Copy(b, i + 1, s, 0, s.Length);
                b = s;
                return str;
            }
            
        }

        private int downlandIAPOld()
        {
            int start = 0;
            MyportData myD = new MyportData();

            byte[] b = new byte[1024 * 1024];
            int l;
            if ((l = readFile(ref b,ref start)) < 0)
            {
                return -1;
            }
            
            byte[] b2 = BitConverter.GetBytes(l);
            int j = blockRead(0x02, b2, 4, ref myD);

            if ((j == 1) && (myD.cmd == 0x02) && (myD.data[0] == 0x01))
            {
                setText("debug", "擦除成功\r\n");
            }
            else
            {
                setText("debug", "擦除错误\r\n");
                setText("debug", "threadStop\r\n");
                return -1;
            }

            setProgressBar1("Maximum", l);

            byte[] temB = new byte[4 * 1024];
            int temBLen = 4 * 1024;
            int i = 0, maxI = 0;
            maxI = l / 4 * 1024;
            if ((l % 4 * 1024) != 0)
                maxI++;
            int ca = l;
            
            while (true)
            {
                if (ca >= 1024 * 4)
                {
                    temBLen = 4 * 1024;
                    Array.Copy(b, i * 1024 * 4, temB, 0, 4 * 1024);
                }
                else if (ca > 0)
                {
                    temBLen = ca;
                    Array.Copy(b, i * 1024 * 4, temB, 0, ca);
                }
                else
                {
                    setText("debug", "发送完成\r\n");
                    break;
                }

                j = blockRead(0x03, temB, temBLen, ref myD);
                if ((j == 1) && (myD.cmd == 0x03))
                {
                    //setText("debug", "write" + i + "\r\n");
                    int temI = myD.data[1] + myD.data[2] * 256 + myD.data[3] * 256 * 256 + myD.data[4] * 256 * 256 * 256;
                    if (temI != i)
                    {
                        setText("debug", "包编号" + i + "顺序出错" + "temI:" + temI + "\r\n");
                        break;
                    }

                    if (myD.data[0] == 0x01)
                    {
                        ca = ca - temBLen;
                        setProgressBar1("Value", l - ca);
                        i++;
                    }
                    else
                    {
                        setText("debug", "编号:" + i + " 错误\r\n");
                    }
                }
                else
                {
                    if (j == 1)
                    {
                            setText("debug", "命令错误\r\n");
                    }
                    else
                    {
                            setText("debug", "超时\r\n");
                    }
                    break;
                }
            }
            
            MCURest2();
            serialPort1.Close();
            Thread.Sleep(100);
            setText("debug", "串口关闭\r\nthreadStop\r\n");
            //progressBar1.Value = 0;

            return 0;
        }

        private int downlandIAP()
        {
            int i;
            byte[] b2;
            int start = 0;

            /***********读取文件**************/
            byte[] b = new byte[1024 * 1024];
            int l;
            if ((l = readFile(ref b,ref start)) < 0)
            {
                setText("debug", "读取文件失败\r\n");
                return -1;
            }

            if (start != startA)
            {
                setText("debug", "首地址错误\r\n");
                return -1;
            }
            

            setProgressBar1("Maximum",l);
            Thread.Sleep(500);

            //string str = readName(downMode, ref b);
            //string[] sArray = str.Split('+');
            //if (sArray.Length != 5)
            //{
            //    return -2;
            //}

            //string ss = sArray[0] + "+" + sArray[1];
            //byte[] b2 = Encoding.Default.GetBytes(ss);
            //byte[] b3 = new byte[b2.Length + 1];
            //Buffer.BlockCopy(b2, 0, b3, 0, b2.Length);
            //if (runCmd(0x46, b3, b3.Length, 3000) >= 0)
            //{
            //    setText("debug", "校验成功\r\n");
            //}
            //else
            //{
            //    setText("debug", "校验错误,请选择正确的文件\r\n");
            //    setText("debug", "threadStop\r\n");
            //    return -1;
            //}

            if (runCmd(0x47, null, 0, 3000) >= 0)
            {
                string str = Encoding.ASCII.GetString(g_tem.data);
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
            if (runCmd(0x41, b2, 8, 3000) >= 0)
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
                    if (runCmd(downMode, b2, 512 + 8, 2000) < 0)
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
                    if (runCmd(downMode, b2, l + 8, 2000) < 0)
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
            if (runCmd(0x43, null, 0, 1000) < 0)
            {
                setText("debug", "重启失败，请重新下载\r\n");
                return -3;
            }else
            {
                setText("debug", "下载成功\r\n");
            }

            return 0;
        }

        private int downlandOther(int ColdStart)
        {
            int i;
            byte[] b2;
            int start = 0;

            if (ColdStart <= 0)  //正常启动
            {
                Thread.Sleep(1000);
                if (runForwarding(0x43, null, 0, 1000) < 0)
                {
                    setText("debug", "重启失败，请重新下载\r\n");
                    return -3;
                }
                else
                {
                    setText("debug", "重启成功\r\n");
                }

                i = 0;
                Thread.Sleep(5);
                while (true)
                {
                    if (runForwarding(0x40, null, 0, 20) >= 0)
                    {
                        setText("debug", "进入iap成功\r\n");
                        break;
                    }
                    i++;
                    if (i > 10)
                    {
                        setText("debug", "进入iap失败\r\n");
                        return -1;
                    }
                    Thread.Sleep(1);
                }
            }
            else if (ColdStart == 1)  //s10 冷启动下载其他
            {
                setText("debug", "冷启动下载\r\n");

                i = 0;
                while (true)
                {
                    if (runForwarding(0x40, null, 0, 40) >= 0)
                    {
                        setText("debug", "进入iap成功\r\n");
                        break;
                    }
                    i++;
                    if (i > 50)
                    {
                        setText("debug", "进入iap失败\r\n");
                        return -1;
                    }
                    Thread.Sleep(2);
                }
            }
            else if (ColdStart == 2)  //镜头 冷启动
            {
                setText("debug", "冷启动下载\r\n");

                i = 0;
                while (true)
                {
                    if (runForwarding(0x40, null, 0, 40) >= 0)
                    {
                        setText("debug", "CFL进入iap成功\r\n");
                        break;
                    }
                    i++;
                    if (i > 100)
                    {
                        setText("debug", "CFL进入iap失败\r\n");
                        return -1;
                    }
                    Thread.Sleep(30);
                }
            }

            byte[] b = new byte[1024 * 1024];
            int l;
            if ((l = readFile(ref b,ref start)) < 0)
            {
                return -1;
            }

            setProgressBar1("Maximum", l);

            string str = readName(downMode, ref b,ref l);
            string[] sArray = str.Split('+');
            if (sArray.Length != 5)
            {
                setText("debug", "文件解析错误\r\n");
                return -2;
            }
            setText("debug", "当前程序型号:" + str + "\r\n");

            /***********回读数据**************/
            if (runForwarding(0x47, null, 0, 3000) >= 0)
            {
                string str1 = Encoding.ASCII.GetString(g_tem.data);
                setText("debug", "回读旧程序型号:" + str1.TrimEnd('\0') + "\r\n");
            }
            else
            {
                setText("debug", "未读取到型号\r\n");
                //return -1;
            }
           

            /***********校验**************/
            string ss = sArray[0] + "+" + sArray[1].Split('.')[0];
            b2 = Encoding.Default.GetBytes(ss);
            byte[] b3 = new byte[b2.Length + 1];
            Buffer.BlockCopy(b2, 0, b3, 0, b2.Length);
            if (runForwarding(0x46, b3, b3.Length, 3000) >= 0)
            {
                setText("debug", "校验成功\r\n");
            }
            else
            {
                setText("debug", "校验错误,请选择正确的文件\r\n");
                setText("debug", "threadStop\r\n");
                return -1;
            }
            
            /***********擦除数据**************/
            int[] intArray = new int[2] { startA, l };
            b2 = new byte[8];
            Buffer.BlockCopy(intArray, 0, b2, 0, b2.Length);
            if (runForwarding(0x41, b2, 8, 2000) >= 0)
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

            while (l > 0)
            {
                intArray[0] = startA + i;
                //setText("debug", "write" + i + "\r\n");
                if (l > 512)
                {
                    intArray[1] = 512;
                    Buffer.BlockCopy(intArray, 0, b2, 0, 8);
                    Buffer.BlockCopy(b, i, b2, 8, 512);
                    if (runForwarding(downMode, b2, 512 + 8, 2000) < 0)
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
                    if (runForwarding(downMode, b2, l + 8, 2000) < 0)
                    {
                        setText("debug", "数据错误\r\n");
                        return -2;
                    }
                    i += l;
                    l -= l;
                    setProgressBar1("Value", i);
                }
            }

            Thread.Sleep(50);
            /***********重启**************/
            if (runForwarding( 0x43, null, 0, 1000) < 0)
            {
                setText("debug", "重启失败，请重新下载\r\n");
                return -3;
            }
            else
            {
                setText("debug", "下载成功\r\n");
            }

            if (runCmd(0x43, null, 0, 1000) < 0)
            {
                setText("debug", "重启失败，请重新下载\r\n");
                return -3;
            }
            else
            {
                setText("debug", "重启成功\r\n");
            }

            return 0;
        }

        private int downlandCFL()
        {
            int i;
            byte[] b2;
            int start = 0;


            //i = 0;
            //Thread.Sleep(5);
            //while (true)
            //{
            //    if (runCmd(0x40, null, 0, 20) >= 0)
            //    {
            //        setText("debug", "进入iap成功\r\n");
            //        break;
            //    }
            //    i++;
            //    if (i > 10)
            //    {
            //        setText("debug", "进入iap失败\r\n");
            //        return -1;
            //    }
            //    Thread.Sleep(1);
            //}


            /***********读取文件**************/
            byte[] b = new byte[1024 * 1024];
            int l;
            if ((l = readFile(ref b, ref start)) < 0)
            {
                return -1;
            }

            if (start != startA)
            {
                setText("debug", "首地址错误\r\n");
                return -1;
            }


            setProgressBar1("Maximum", l);
            //Thread.Sleep(500);

            string str = readName(downMode, ref b, ref l);
            string[] sArray = str.Split('+');
            if (sArray.Length != 5)
            {
                return -2;
            }
            setText("debug", "当前程序型号:" + str + "\r\n");
            /***********回读数据**************/
            //if (runCmd(0x47, null, 0, 3000) >= 0)
            //{
            //    string str1 = Encoding.ASCII.GetString(g_tem.data);
            //    setText("debug", "型号:" + str1.TrimEnd('\0') + "\r\n");
            //}
            //else
            //{
            //    setText("debug", "读取型号错误\r\n");
            //    //return -1;
            //}

            /***********校验**************/
            string ss = sArray[0] + "+" + sArray[1].Split('.')[0];
            b2 = Encoding.Default.GetBytes(ss);
            byte[] b3 = new byte[b2.Length + 1];
            Buffer.BlockCopy(b2, 0, b3, 0, b2.Length);
            if (runCmd(0x46, b3, b3.Length, 3000) >= 0)
            {
                setText("debug", "校验成功\r\n");
            }
            else
            {
                setText("debug", "校验错误,请选择正确的文件\r\n");
                setText("debug", "threadStop\r\n");
                return -1;
            }

            /***********擦除数据**************/
            int[] intArray = new int[2] { startA, l };
            b2 = new byte[8];
            Buffer.BlockCopy(intArray, 0, b2, 0, b2.Length);
            if (runCmd(0x41, b2, 8, 3000) >= 0)
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

            while (l > 0)
            {
                intArray[0] = startA + i;
                //setText("debug", "write"+i+"\r\n");
                if (l > 512)
                {
                    intArray[1] = 512;
                    Buffer.BlockCopy(intArray, 0, b2, 0, 8);
                    Buffer.BlockCopy(b, i, b2, 8, 512);
                    if (runCmd(downMode, b2, 512 + 8, 2000) < 0)
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
                    if (runCmd(downMode, b2, l + 8, 2000) < 0)
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
            if (runCmd(0x43, null, 0, 1000) < 0)
            {
                setText("debug", "重启失败，请重新下载\r\n");
                return -3;
            }
            else
            {
                setText("debug", "下载成功\r\n");
            }

            return 0;
        }

        private int runCmd(byte cmd,byte[] b,int len,int time)
        {
            QueueData c;
            MyportData sendData = new MyportData();

            byte[] b2;
            sendData.start = 0xbb;
            sendData.cmd = cmd;
            sendData.len = len;
            sendData.data = b;
            sendData.end = 0xee;
            b2 = sendData.getByte();
            while (portRecQueue.TryDequeue(out c));
            serialPort1.Write(b2, 0, b2.Length);            //发送命令
            while (true)
            {
                time--;
                while (portRecQueue.TryDequeue(out c))
                {
                    if (c.cmd == "port2Thread")
                    {
                        g_tem = (MyportData)c.o;
                        if (g_tem.cmd == (cmd|0x10))
                        {
                            return 1; 
                        }else
                        {
                            return -1;
                        }
                    }
                }

                if (time < 0)
                    break;
                Thread.Sleep(1);
            }
            return -1;
        }

        private int runForwarding(byte cmd, byte[] b, int len, int time)
        {
            
            QueueData c;
            MyportData sendData = new MyportData();

            byte[] b2;
            sendData.start = 0xbb;
            sendData.cmd = cmd;
            sendData.len = len;
            sendData.data = b;
            sendData.end = 0xee;
            b2 = sendData.getByte();

            byte[] b3 = new byte[12 + b2.Length];
            int[] i3 = new int[] { type,id, time };
            Buffer.BlockCopy(i3, 0, b3, 0, 12);
            Buffer.BlockCopy(b2, 0, b3, 12, b2.Length);

            sendData.start = 0xbb;
            sendData.cmd = 0x61;
            sendData.len = b3.Length;
            sendData.data = b3;
            sendData.end = 0xee;
            b2 = sendData.getByte();

            while (portRecQueue.TryDequeue(out c));
            serialPort1.Write(b2, 0, b2.Length);            //发送命令
            time += 10;
            while (true)
            {
                time--;

                while (portRecQueue.TryDequeue(out c))
                {
                    if (c.cmd == "port2Thread")
                    {
                        MyportData tem = (MyportData)c.o;
                        if (tem.cmd == (0x61 | 0x10))
                        {
                            MyportData myPData = new MyportData();
                            for (int i = 0; i < tem.len; i++)
                            {
                                myPData.saveChar(tem.data[i]);
                            }
                            g_tem = myPData;
                            if (myPData.cmd == (cmd | 0x10))
                                return 1;
                            else
                                return -1;
                        }else
                        {
                            return -1;
                        }
                    }
                }

                if (time < 0)
                    break;
                Thread.Sleep(1);
            }
            return -1;
        }

        private int processUIcmd(QueueData cm)
        {
            int restFlag = 0;
            int time = 0;
            int i = 0;
            QueueData c = new QueueData();
            MyportData sendData = new MyportData();

            BData hs = (BData)cm.o;
            

            type = hs.type;
            id = hs.id;
            Path = hs.path;
            startA = hs.startA;

            serialPort1.Close();
            serialPort1.BaudRate = 460800;
            serialPort1.PortName = hs.COM;
            serialPort1.Open();
            setProgressBar1("Value", 0);

            i = 0;
            while (true)
            {
                if (runCmd(0x01, null, 0, 50) >= 0)
                {
                    setText("debug", "转板进入iap成功\r\n");
                    break;
                }
                i++;
                if (i > 20)
                {
                    setText("debug", "转板进入iap失败\r\n");
                    return -1;
                }
                Thread.Sleep(1);
            }

            downlandCFL();

            //if ((type == 12) || (type == 15))
            //{
            //    try
            //    {
            //        serialPort1.Close();
            //        serialPort1.BaudRate = 115200;
            //        serialPort1.PortName = hs.COM;
            //        serialPort1.Open();
            //    }
            //    catch
            //    {
            //        setText("debug", "未能打开串口，请先通电用普通方式找到串口，或手动修改XML文件填写端口重启软件\r\n");
            //        return -1;
            //    }
            //    setText("debug", "已打开串口\r\n");

            //    i = 0;
            //    while (true)
            //    {
            //        i++;
            //        byte[] b = Encoding.ASCII.GetBytes("Rest\r");
            //        //b = sendData.getByte();
            //        serialPort1.Write(b, 0, b.Length);            //发送命令

            //        if (i > 10)
            //        {
            //            setText("debug", "主板复位失败\r\n");
            //            break; //超时 不是转板
            //        }

            //        Thread.Sleep(20);
            //        while (portRecQueue.TryDequeue(out c))
            //        {
            //            if (c.cmd == "port2Thread")
            //            {
            //                MyportData tem = (MyportData)c.o;
            //                if (tem.cmd == 0x11)
            //                {
            //                    setText("debug", "主板进入iap成功\r\n");
            //                    serialPort1.BaudRate = 460800;
            //                    i = 0;
            //                    break;
            //                }
            //            }
            //        }
            //        if (i == 0)
            //            break;
            //    }

            //    serialPort1.BaudRate = 460800;
            //    i = 0;
            //    Thread.Sleep(5);
            //while (true)
            //{
            //    if (runCmd(0x40, null, 0, 20) >= 0)
            //    {
            //        setText("debug", "转板进入iap成功\r\n");
            //        break;
            //    }
            //    i++;
            //    if (i > 10)
            //    {
            //        setText("debug", "转板进入iap失败\r\n");
            //        return -1;
            //    }
            //    Thread.Sleep(1);
            //}

            //    if (type == 12)
            //    {
            //        i = downlandOther(hs.downMode);
            //        return i;
            //    }
            //    else
            //    {
            //        i = downlandCFL();
            //        return i;
            //    }

            //}
            serialPort1.Close();
            
            return 0;
        }
    }
}
