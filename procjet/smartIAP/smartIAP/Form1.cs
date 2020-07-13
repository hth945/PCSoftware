using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace smartIAP
{
    public partial class Form1 : Form
    {
        SerialPort serialPort1;

        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {

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
    }
}


