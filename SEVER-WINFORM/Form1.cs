using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net.Sockets;
using System.Net;
using System.Threading;
using System.Runtime.InteropServices;
using System.Diagnostics;


namespace SEVER_WINFORM
{
    public partial class Form1 : Form
    {
        /// 
        /// memcmp API
        /// 
        /// 字节数组1
        /// 字节数组2
        /// 如果两个数组相同，返回0；如果数组1小于数组2，返回小于0的值；如果数组1大于数组2，返回大于0的值。
        [DllImport("msvcrt.dll")]
        private static extern IntPtr memcmp(byte[] b1, byte[] b2, IntPtr count);


        #region 结构体
        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct QueryData
        {
            public byte header;          //
            public byte addr;          //
            public byte append;        //
            public byte nck;  //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] adi; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] ado; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] aNci; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] crc;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] temp;
        }
        #endregion

        #region 结构体
        [StructLayout(LayoutKind.Sequential), Serializable]
        public struct ReplyData
        {
            public byte header;          //
            public byte addr;          //
            public byte append;        //
            public byte nck;  //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
            public byte[] adi; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
            public byte[] ado; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
            public byte[] aRbtCmuInfo; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 60)]
            public byte[] unRbtState; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] unSnrData; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 40)]
            public byte[] unPipeData; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] aNci; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] aNciTemp; //
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] crc;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
            public byte[] temp;
        }
        #endregion


        public Form1()
        {
            InitializeComponent();
            TextBox.CheckForIllegalCrossThreadCalls = false;
            Control.CheckForIllegalCrossThreadCalls = false;//取消线程间的安全检查
        }

        //启动服务端  
        UdpClient udpServer;
        delegate void SetTextCallBack(string text);
        bool IS_Connected_UDP = true;

        Queue<QueryData> QueList = new Queue<QueryData>();


        private void DATA_RCE_Click(object sender, EventArgs e)
        {
            IS_Connected_UDP = true;
            udpServer = new UdpClient(3000);

            //udpServer.Client.ReceiveTimeout = 30000;
            
            SetText("服务器已启动..");
            DATA_RCE.Enabled = false;
            DATA_RCE_CLOSE.Enabled = true;
            Thread t = new Thread(new ThreadStart(ReceiveMsg));
            t.IsBackground = true;
            t.Start();  
        }


        //存储询问数据结构体
        QueryData queData = new QueryData();
        //备份询问数据包
        byte[] query_buffer_bk = null;
        
        //标记是否发送询问数据改变机器人状态
        bool IS_SendQueDta = false;

        //记录当前镜头变倍倍数
        int CaZo = 0;
        //记录镜头前置还是后置
        int video_fb = 0;
        //记录回复数据中的机器人状态位
        string RoboState = "00000000"; 
        //记录当前由控制中心发出的命令数
        int command_count = 0;
        //记录发送数据包的个数
        int package_count = 0;
        //记录收到的数据包个数
        int package_count_rev= 0;

        public void ReceiveMsg()
        {
            IPEndPoint ipe = new IPEndPoint(IPAddress.Any, 3000);

            //初始化询问结构体
            #region
            queData.header = 0x55;
            queData.addr = 0x00;
            queData.append = 0x00;
            queData.nck = 0x01;
            queData.crc = new byte[] { 0x00, 0x00 };
            queData.temp = new byte[] { 0xaa, 0xaa };
            queData.adi = new byte[] { 0x00, 0x00, 0x00, 0x00 };
            queData.ado = new byte[] { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 };
            queData.aNci = Enumerable.Repeat((byte)0x00, 32).ToArray();
            #endregion

            byte[] buffer = null;

            try
            {
                buffer = udpServer.Receive(ref ipe);
                SetText("客户端连入！");

                udpServer.Client.ReceiveTimeout = 10000;
            }
            catch (SocketException e)
            {
                SetText("异常：超时" + e.Message);
            }

            //接收到第一条数据后，解析机器人参数，对其进行初始化操作
            #region
            if (buffer.Length >= 244)
            {
                //对收到的数据包进行CRC校验
                byte[] arrMsgRec2 = buffer.Skip(0).Take(buffer.Length - 4).ToArray();
                byte[] a2 = GetCRC.crc16Bytes(arrMsgRec2);

                //将回复字节数组转为结构体
                ReplyData repdata = new ReplyData();
                repdata = Byte2Struct(buffer);

                if (a2[0] == repdata.crc[1] && a2[1] == repdata.crc[0])
                {
                    //取当前镜头变倍倍数，并显示在界面上
                    CaZo = Convert.ToInt32(repdata.unRbtState[22]);
                    textBox3.Text = CaZo.ToString();

                    //获取当前镜头前置还是后置
                    RoboState = System.Convert.ToString(repdata.ado[1], 2);
                    RoboState = RoboState.PadLeft(8, '0');
                    char[] charArray = RoboState.ToCharArray();                                    
                    video_fb = int.Parse(charArray[7].ToString());
                    if (video_fb == 0)
                    {
                        textBox5.Text = "前置";
                        CaFocus_far_btn.Enabled = true;
                        CaFocus_near_btn.Enabled = true;
                        CaZoDown_btn.Enabled = true;
                        CaZoUp_btn.Enabled = true;
                        CaZoZero_btn.Enabled = true;
                    }
                    else
                    {
                        textBox5.Text = "后置";
                        CaFocus_far_btn.Enabled = false;
                        CaFocus_near_btn.Enabled = false;
                        CaZoDown_btn.Enabled = false;
                        CaZoUp_btn.Enabled = false;
                        CaZoZero_btn.Enabled = false;
                        textBox3.Text = "";
                    }
                    
                    SetText("接受数据:");
                    string msg1 = BitConverter.ToString(buffer, 0);
                    SetText(msg1);
                }
                else
                {
                    SetText("CRC校验错误");
                }

                buffer = null;
            }
            else
            {
                SetText("接收数据小于244位");
            }
            #endregion

            while (true)
            {
                if (!IS_Connected_UDP) break;

                //发送询问数据包
                #region
                //将询问结构体转字节数组，并发送询问字节数组
                byte[] query_buffer = Struct2Byte(queData);
                query_buffer_bk = query_buffer;//备份

                byte[] arrMsgRec1 = query_buffer.Skip(0).Take(query_buffer.Length - 4).ToArray();
                byte[] a = GetCRC.crc16Bytes(arrMsgRec1);

                queData.crc[0] = a[1];
                queData.crc[1] = a[0];

                query_buffer = Struct2Byte(queData);
                udpServer.Send(query_buffer, query_buffer.Length, ipe);

                package_count++;
                textBox2.Text = package_count.ToString();

                SetText("发送数据:");
                string msg = BitConverter.ToString(query_buffer, 0);
                SetText(msg);
                #endregion


                //发送完，判断当前是否仍点击按钮，若没有则清空询问结构体，否则清空出当前按钮发出命令外的命令
                #region
                int lonconmand_count = 0;

                //询问命令数组重置为0
                if (!IS_SendQueDta)
                {
                    command_count = 0;
                    queData.aNci = Enumerable.Repeat((byte)0x00, 32).ToArray();
                }
                else
                {
                    for (int i = 0; i < 25; i = i + 8)
                    {
                        if (queData.aNci[i] != 0x00)
                        {
                            lonconmand_count++;
                        }
                    }
                    queData.aNci[0] = queData.aNci[8 * (lonconmand_count - 1)];
                    queData.aNci[7] = queData.aNci[8 * lonconmand_count - 1];

                    for (int j = 8; j < 32; j++)
                    {
                        queData.aNci[j] = 0x00;
                    }

                    command_count = 1;
                }
                #endregion


                //接收回复数据包，并解析
                #region
                try
                {
                    buffer = udpServer.Receive(ref ipe);
                    package_count_rev++;
                    textBox4.Text = package_count_rev.ToString();
                }
                catch (SocketException e)
                {
                    SetText("异常：超时" + e.Message);
                    udpServer.Send(query_buffer_bk, query_buffer_bk.Length, ipe);
                }

                if (buffer.Length>=244)
                {

                    //对收到的数据包进行CRC校验
                    byte[] arrMsgRec2 = buffer.Skip(0).Take(buffer.Length - 4).ToArray();
                    byte[] a2 = GetCRC.crc16Bytes(arrMsgRec2);

                    //将回复字节数组转为结构体
                    ReplyData repdata = new ReplyData();
                    repdata = Byte2Struct(buffer);

                    if (a2[0] == repdata.crc[1] && a2[1] == repdata.crc[0])
                    {
                        
                        //取当前镜头变倍倍数，并显示在界面上
                        CaZo = Convert.ToInt32(repdata.unRbtState[22]);
                        textBox3.Text = CaZo.ToString();

                        //获取当前镜头前置还是后置
                        RoboState = System.Convert.ToString(repdata.ado[1], 2);
                        RoboState = RoboState.PadLeft(8, '0');
                        char[] charArray = RoboState.ToCharArray();
                        video_fb = int.Parse(charArray[7].ToString());

                        if (video_fb == 0)
                        {
                            textBox5.Text = "前置";
                            CaFocus_far_btn.Enabled = true;
                            CaFocus_near_btn.Enabled = true;
                            CaZoDown_btn.Enabled = true;
                            CaZoUp_btn.Enabled = true;
                            CaZoZero_btn.Enabled = true;
                        }
                        else
                        {
                            textBox5.Text = "后置";
                            CaFocus_far_btn.Enabled = false;
                            CaFocus_near_btn.Enabled = false;
                            CaZoDown_btn.Enabled = false;
                            CaZoUp_btn.Enabled = false;
                            CaZoZero_btn.Enabled = false;
                            textBox3.Text = "";
                        }

                        SetText("接受数据:");
                        string msg1 = BitConverter.ToString(buffer, 0);
                        SetText(msg1);
                    }
                    else
                    {
                        SetText("CRC校验错误");
                    }

                    buffer = null;
                }
                else
                {
                    SetText("接收数据小于244位");
                }

                #endregion

            }

        }

       

        private void DATA_RCE_CLOSE_Click(object sender, EventArgs e)
        {

            DATA_RCE.Enabled = true;
            DATA_RCE_CLOSE.Enabled = false;

            if (udpServer != null)
            {
                udpServer.Close();
                IS_Connected_UDP = false;
            }
        }


        public void SetText(string text)
        {
            if (text == "")
            {
                if (textBox1.InvokeRequired)
                {
                    SetTextCallBack st = new SetTextCallBack(SetText);
                    this.Invoke(st, new object[] { text });
                }
                else
                {
                    textBox1.Text = "\n";
                }
            }
            else
            {

                if (textBox1.InvokeRequired)
                {
                    SetTextCallBack st = new SetTextCallBack(SetText);
                    this.Invoke(st, new object[] { text });
                }
                else
                {
                    textBox1.Text = text + "\n";
                }

            }
        }

        private ReplyData Byte2Struct(byte[] arr)
        {
            int structSize = Marshal.SizeOf(typeof(ReplyData));
            IntPtr ptemp = Marshal.AllocHGlobal(structSize);
            Marshal.Copy(arr, 0, ptemp, structSize);
            ReplyData rs = (ReplyData)Marshal.PtrToStructure(ptemp, typeof(ReplyData));
            Marshal.FreeHGlobal(ptemp);
            return rs;
        }

        private byte[] Struct2Byte(QueryData s)
        {
            int structSize = Marshal.SizeOf(typeof(QueryData));
            byte[] buffer = new byte[structSize];
            //分配结构体大小的内存空间
            IntPtr structPtr = Marshal.AllocHGlobal(structSize);
            //将结构体拷到分配好的内存空间
            Marshal.StructureToPtr(s, structPtr, false);
            //从内存空间拷到byte数组
            Marshal.Copy(structPtr, buffer, 0, structSize);
            //释放内存空间
            Marshal.FreeHGlobal(structPtr);
            return buffer;
        }

        public bool MemoryCompare(byte[] b1, byte[] b2)
        {
            bool is_equal = true;
            for (int i = 0; i < 32;i++ )
            {
                if (b1[i] != b2[i])
                {
                    is_equal = false;
                    break;
                }
                is_equal = true;
            }
            return is_equal;
        }

        private byte ten2sixteen(int i)
        {
            byte result = 0x01;
            switch (i)
            {
                case 1:
                    result = 0x01;
                    break;
                case 2:
                    result = 0x02;
                    break;
                case 3:
                    result = 0x03;
                    break;
                case 4:
                    result = 0x04;
                    break;
                case 5:
                    result = 0x05;
                    break;
                case 6:
                    result = 0x06;
                    break;
                case 7:
                    result = 0x07;
                    break;
                case 8:
                    result = 0x08;
                    break;
                case 9:
                    result = 0x09;
                    break;
                case 10:
                    result = 0x10;
                    break;
                case 11:
                    result = 0x11;
                    break;
                case 12:
                    result = 0x12;
                    break;
                case -1:
                    result = 0xff;
                    break;
                case -2:
                    result = 0xfe;
                    break;
                case -3:
                    result = 0xfd;
                    break;
                case -4:
                    result = 0xfc;
                    break;
                case -5:
                    result = 0xfb;
                    break;
            }

            return result;
        }

        public byte[] ten2sixteen32(int i)
        {
            byte[] bytes = BitConverter.GetBytes(i);
            i = BitConverter.ToInt32(bytes, 0);

            return bytes;
        }

        

        private void left_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x02;
                byte[] command32=ten2sixteen32(-trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4); 
            }
        }
        
        private void right_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x02;

                byte[] command32 = ten2sixteen32(trackBar1.Value);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);                
            }
        }     

        private void left_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void right_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }
              
        private void CaZoUp_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (CaZo <10)
            {
                if (command_count < 4)
                {
                    IS_SendQueDta = true;
                    command_count++;
                    queData.aNci[(command_count - 1) * 8] = 0x09;
                    //queData.aNci[command_count * 8 - 1] = ten2sixteen(trackBar1.Value);
                    int CaNow = CaZo + 1;

                    byte[] command32 = ten2sixteen32(CaNow);

                    Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4); 
                    
                }               
            }
            else
            {
                MessageBox.Show("超过上限！");
            }
        }

        private void CaZoUp_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void CaZoDown_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (CaZo > 1)
            {
                if (command_count < 4)
                {
                    IS_SendQueDta = true;
                    command_count++;
                    queData.aNci[(command_count - 1) * 8] = 0x09;
                    //queData.aNci[command_count * 8 - 1] = ten2sixteen(trackBar1.Value);
                    int CaNow = CaZo - 1;
                    byte[] command32 = ten2sixteen32(CaNow);

                    Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4); 
                   
                }
            }
            else
            {
                MessageBox.Show("超过下限！");
            }
        }

        private void CaZoDown_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void CaZoZero_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x09;
                //queData.aNci[command_count * 8 - 1] = ten2sixteen(trackBar1.Value);
                queData.aNci[command_count * 8 - 3] = 0xff;
            }
        }

        private void CaZoZero_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void front_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x01;
                

                byte[] command32 = ten2sixteen32(trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4); 
            }
        }

        private void front_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void back_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x01;
                byte[] command32 = ten2sixteen32(-trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4); 
            }
        }

        private void back_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void fb_stop_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x01;

                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);            
            }
        }

        private void fb_stop_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void lr_stop_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x02;

                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);     
            }
        }

        private void lr_stop_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void up_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x03;
                byte[] command32 = ten2sixteen32(1);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);     
            }
        }

        private void up_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void down_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x03;
                byte[] command32 = ten2sixteen32(-1);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);     
            }
        }

        private void down_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void ud_stop_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x03;

                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void ud_stop_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_bend_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x04;
                byte[] command32 = ten2sixteen32(trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);
            }
        }

        private void cradle_bend_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_lean_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x04;
                byte[] command32 = ten2sixteen32(-trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);
            }
        }

        private void cradle_lean_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_blstop_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x04;

                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void cradle_blstop_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_leRota_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x05;
                byte[] command32 = ten2sixteen32(-trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void cradle_leRota_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_riRota_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x05;
                byte[] command32 = ten2sixteen32(trackBar1.Value);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void cradle_riRota_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void cradle_lrstop_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x05;
                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void cradle_lrstop_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void CradleZero_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x06;
                byte[] command32 = ten2sixteen32(0);
                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }


        private void CradleZero_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void CaFocus_near_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x08;
                byte[] command32 = ten2sixteen32(-1);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);    
            }
        }

        private void CaFocus_near_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void CaFocus_far_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x08;
                byte[] command32 = ten2sixteen32(1);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);   
            }
        }

        private void CaFocus_far_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void lightAdj_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x0A;
                byte[] command32 = ten2sixteen32(trackBar1.Value-1);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);  
            }
        }

        private void lightAdj_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }
       

        private void CaSwitch_btn_MouseDown(object sender, MouseEventArgs e)
        {
            if (command_count < 4)
            {
                IS_SendQueDta = true;
                command_count++;
                queData.aNci[(command_count - 1) * 8] = 0x07;
                byte[] command32 = ten2sixteen32(1-video_fb);

                Array.Copy(command32, 0, queData.aNci, (command_count * 8 - 4), 4);  
            }
        }

        private void CaSwitch_btn_MouseUp(object sender, MouseEventArgs e)
        {
            IS_SendQueDta = false;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            //RoboState = System.Convert.ToString(0x00, 2);
            //RoboState = RoboState.PadLeft(8, '0');
            //char[] charArray = RoboState.ToCharArray();
            //video_fb = int.Parse(charArray[7].ToString());
            //SetText(RoboState);

            int i = -1;

            byte[] bytes = BitConverter.GetBytes(i);

            i = BitConverter.ToInt32(bytes, 0);
        }

        private void button3_Click(object sender, EventArgs e)
        {
            byte[] buffer = { 0x55, 00, 00, 00, 0x01, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0xC0, 0xEF, 0xBF, 0xEF, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0x80, 0x3F, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0x57, 0x44, 0x44, 0x4C, 0x32, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 00, 0xEB, 0xCD, 0xAA, 0xAA };
            //byte[] arrMsgRec1 = buffer.Skip(0).Take(buffer.Length - 4).ToArray();
            //byte[] a = GetCRC.crc16Bytes(arrMsgRec1);
            byte[] new1 = { 0xab, 0x23, 0x34, 0x45 };
            Array.Copy(new1, 0, buffer, 2, 4);


            SetText("CRC:");
            string msg1 = BitConverter.ToString(buffer, 0);
            SetText(msg1);

            //SetText(buffer.Length+"");
        }

       
    }
}
