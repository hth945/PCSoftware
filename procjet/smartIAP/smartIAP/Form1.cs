using PCSoftware;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;


namespace smartIAP
{
    public partial class Form1 : Form
    {
        SerialPort serialPort1 = new SerialPort();
        string COMName="";
        SerialPort MCSerialPort = new SerialPort();
        MyCommunication seroalPortMc = new MyCommunication();
        MyIAP mIAP = new MyIAP();

        public Form1()
        {
            InitializeComponent();

            mIAP.mc = seroalPortMc;
            mIAP.MCURestD += comMCURest;
            mIAP.startD += mIAPstartD;
            mIAP.exitD += mIAPexitD;
            seroalPortMc.Write += seroalPortWrite;
            MCSerialPort.DataReceived += new SerialDataReceivedEventHandler(seroalPortDataReceive);  //实例化委托对象


            textBox2.Text = @"D:\sysDef\Documents\GitHub\hthStm32SoftwareFrame\bsp\f103_C8t6RTThread\Output\STM32F103.hex";
            Application.Idle += Application_Idle;
        }

        private void Application_Idle(object sender, EventArgs e)
        {
            QueueData mpd;
            while (mIAP.uiQueue.TryDequeue(out mpd))   //清空接收消息队列
            {
                if (mpd.cmd == "end")
                {
                    int i = (int)mpd.o; // i为负数失败
                }
                else if (mpd.cmd == "textdebug")
                {
                    textBox1.AppendText((string)mpd.o);//追加文本
                    textBox1.ScrollToCaret();
                }
                else if (mpd.cmd == "progressBar1.Maximum")
                {
                    progressBar1.Minimum = 0;
                    progressBar1.Maximum = (int)mpd.o;
                    progressBar1.Value = 0;
                }
                else if (mpd.cmd == "progressBar1.Value")
                {
                    progressBar1.Value = (int)mpd.o;
                }
            }
        }


        private void seroalPortDataReceive(object sender, SerialDataReceivedEventArgs e)
        {
            SerialPort sp = ((SerialPort)sender);
            byte[] bTem = new byte[sp.BytesToRead];
            sp.Read(bTem, 0, bTem.Length);
            seroalPortMc.recive(bTem, bTem.Length);
        }

        private int seroalPortWrite(byte[] data, int len)
        {
            MCSerialPort.Write(data, 0, len);
            return len;
        }

        private int comMCURest()
        {
            MCSerialPort.BaudRate = 115200;
            MCSerialPort.Close();
            MCSerialPort.Open();
            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes("\nRest\n");
            MCSerialPort.Write(byteArray, 0, byteArray.Length);
            Thread.Sleep(30);
            MCSerialPort.BaudRate = 460800;
            MCSerialPort.Close();
            MCSerialPort.Open();
            
            return 0;
        }

        private int mIAPstartD()
        {
            //MCSerialPort.BaudRate = 115200;
            MCSerialPort.PortName = COMName;
            //MCSerialPort.Open();
            return 0;
        }

        private int mIAPexitD()
        {
            MCSerialPort.Close();
            return 0;
        }


        private void button1_Click(object sender, EventArgs e)
        {
            FileStream fs = new FileStream(textBox2.Text, FileMode.Open);
            byte[] array = new byte[fs.Length];
            fs.Read(array, 0, array.Length);
            fs.Close();
            COMName = comboBox1.Text.Trim();

            mIAP.startDownland(array, 0x8008000);
        }


        private int UpdateCOMport()
        {
            bool comExistence = false;//有可用串口标志位
            comboBox1.Items.Clear(); //清除当前串口号中的所有串口名称
            for (int i = 2; i < 25; i++)
            {
                try
                {
                    serialPort1.PortName = "COM" + i;
                    serialPort1.Open();
                    serialPort1.Close();
                    comboBox1.Items.Add("COM" + i.ToString());
                    comExistence = true;
                }
                catch (Exception)
                {
                    continue;
                }
            }
            if (!comExistence)
                comboBox1.Items.Add("无");
            comboBox1.SelectedIndex = 0;//使 ListBox 显示第 1 个添加的索引
            if (!comExistence)
                return -1;
            return 1;
        }

        private void comboBox1_MouseDown(object sender, MouseEventArgs e)
        {
            UpdateCOMport();
        }

        private void textBox2_DragDrop(object sender, DragEventArgs e)
        {
            string path = ((System.Array)e.Data.GetData(DataFormats.FileDrop)).GetValue(0).ToString();
            ((TextBox)sender).Text = path;
            ((TextBox)sender).Cursor = System.Windows.Forms.Cursors.IBeam; //还原鼠标形状 
        }

        private void textBox2_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effect = DragDropEffects.Link;
                ((TextBox)sender).Cursor = System.Windows.Forms.Cursors.Arrow;//指定鼠标形状   
            }
            else
            {
                e.Effect = DragDropEffects.None;
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {

            MCSerialPort.BaudRate = 115200;
            MCSerialPort.PortName = comboBox1.Text.Trim();
            MCSerialPort.Close();
            MCSerialPort.Open();

            
            
            byte[] byteArray = System.Text.Encoding.ASCII.GetBytes("\nRest\n");
            MCSerialPort.Write(byteArray, 0, byteArray.Length);

            MCSerialPort.Close();
            MCSerialPort.Open();
            MCSerialPort.BaudRate = 460800;
            
            MyportData mySendData = new MyportData();
            mySendData.start = 0xbb; // aa 01 00 00 00 00 ee
            mySendData.cmd = 0x01;
            mySendData.len = 0;
            mySendData.data = null;
            mySendData.end = 0xee;
            byte[] b = mySendData.getByte();
            MCSerialPort.Write(byteArray, 0, byteArray.Length);

            MCSerialPort.Close();


        }
    }
}


