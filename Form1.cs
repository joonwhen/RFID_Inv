using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections;
using System.Resources;
using System.Reflection;
using System.Net;
using System.Threading;
using System.Diagnostics;
using UHF;
using System.IO;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using Npgsql;

namespace UHFReader288MPDemo
{
    public partial class Form1 : Form
    {
        [DllImport("User32.dll", EntryPoint = "PostMessage")]
        private static extern int PostMessage(
        IntPtr hWnd, // handle to destination window 
        uint Msg, // message 
        uint wParam, // first message parameter 
        uint lParam // second message parameter 
        );

        [DllImport("User32.dll", EntryPoint = "SendMessage")]
        private static extern int SendMessage(IntPtr hwnd, int wMsg, IntPtr wParam, string lParam);

        [DllImport("User32.dll", EntryPoint = "FindWindow")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public const int USER = 0x0400;
        public const int WM_SENDTAG = USER + 101;
        public const int WM_SENDTAGSTAT = USER + 102;
        public const int WM_SENDSTATU = USER + 103;
        public const int WM_SENDBUFF = USER + 104;
        public const int WM_MIXTAG = USER + 105;
        public const int WM_SHOWNUM = USER + 106;
        public const int WM_FASTID = USER + 107;

        private byte fComAdr = 0xff; //当前操作的ComAdr
        private byte fBaud;
        private double fdminfre;
        private double fdmaxfre;
        private int fCmdRet = 30; //所有执行指令的返回值
        private byte[] fOperEPC = new byte[100];
        private byte[] fPassWord = new byte[4];
        private byte[] fOperID_6B = new byte[10];
        ArrayList list = new ArrayList();
        private List<string> epclist = new List<string>();
        private List<string> tidlist = new List<string>();
        private string fInventory_EPC_List; //存贮询查列表（如果读取的数据没有变化，则不进行刷新）
        private int frmcomportindex;
        private byte Target = 0;
        private byte InAnt = 0;
        private byte Scantime = 0;
        private byte FastFlag = 0;
        private byte Qvalue = 0;
        private byte Session = 0;
        private int total_tagnum = 0;//标签数量
        private int CardNum = 0;
        private int total_time = 0;//总时间
        private int targettimes = 0;
        private byte TIDFlag = 0;
        private byte tidLen = 0;
        private byte tidAddr = 0;
        public static byte antinfo = 0;
        private int AA_times = 0;
        private int CommunicationTime = 0;
        public DeviceClass SelectedDevice;
        private static List<DeviceClass> DevList;
        private static SearchCallBack searchCallBack = new SearchCallBack(searchCB);
        private string ReadTypes = "";

        //JW - Global list to keep track of the EPCs 
        public static List<string> Full_list = new List<string>();
        public static List<string> inventory_list = new List<string>();
        public static List<string> remove_list = new List<string>();
        //JW - Condition to run epc checker
        public static bool run_epc_checker = true;
        public static bool hw_connected = false;
        public static bool connected = false;
        public static bool scan_active = false;
        public static bool startup = true;

        /// <summary>
        /// Device Search的回调函数;
        /// </summary>
        private static void searchCB(IntPtr dev, IntPtr data)
        {
            uint ipAddr = 0;
            StringBuilder devname = new StringBuilder(100);
            StringBuilder macAdd = new StringBuilder(100);
            //获取搜索到的设备信息；
            DevControl.tagErrorCode eCode = DevControl.DM_GetDeviceInfo(dev, ref ipAddr, macAdd, devname);
            if (eCode == DevControl.tagErrorCode.DM_ERR_OK)
            {
                //将搜索到的设备加入设备列表；
                DeviceClass device = new DeviceClass(dev, ipAddr, macAdd.ToString(), devname.ToString());
                DevList.Add(device);
            }
            else
            {
                //异常处理；
                string errMsg = ErrorHandling.GetErrorMsg(eCode);
                Log.WriteError(errMsg);
            }

        }
        private static IPAddress getIPAddress(uint interIP)
        {
            return new IPAddress((uint)IPAddress.HostToNetworkOrder((int)interIP));
        }

        RFIDCallBack elegateRFIDCallBack;
        public Form1()
        {
            InitializeComponent();
            //初始化设备列表；
            DevList = new List<DeviceClass>();

            //JW - Initiate a new thread
            Thread Checker_thr = new Thread(Periodic_Checker);
            Checker_thr.Start();

            //初始化设备控制模块；
            DevControl.tagErrorCode eCode = DevControl.DM_Init(searchCallBack, IntPtr.Zero);
            if (eCode != DevControl.tagErrorCode.DM_ERR_OK)
            {
                //如果初始化失败则关闭程序，并进行异常处理；
                string errMsg = ErrorHandling.HandleError(eCode);
                throw new Exception(errMsg);
            }
            elegateRFIDCallBack = new RFIDCallBack(GetUid);
        }

        public void GetUid(IntPtr p, Int32 nEvt)
        {

            RFIDTag ce = (RFIDTag)Marshal.PtrToStructure(p, typeof(RFIDTag));
            this.Invoke((EventHandler)delegate
            {
                IntPtr ptrWnd = IntPtr.Zero;
                ptrWnd = FindWindow(null, "UHFReader288MP Demo V2.2");
                if (ptrWnd != IntPtr.Zero)         // 检查当前统计窗口是否打开
                {
                    int Antnum = ce.ANT;
                    string str_ant = Convert.ToString(Antnum, 2).PadLeft(4, '0');
                    string epclen = Convert.ToString(ce.LEN, 16);
                    if (epclen.Length == 1) epclen = "0" + epclen;
                    string para = str_ant + "," + epclen + ce.UID + "," + ce.RSSI.ToString() + " ";
                    SendMessage(ptrWnd, WM_SENDTAG, IntPtr.Zero, para);
                    
                }
                total_tagnum++;
                CardNum++;
            });

        }
        protected override void DefWndProc(ref Message m)
        {
            if (m.Msg == WM_SENDTAG)
            {

                string tagInfo = Marshal.PtrToStringAnsi(m.LParam);
                string sEPC;
                string str_ant = tagInfo.Substring(0, 4);
                tagInfo = tagInfo.Substring(5);
                int index = tagInfo.IndexOf(',');
                sEPC = tagInfo.Substring(0, index);
                string str_epclen = sEPC.Substring(0, 2);
                byte epclen = Convert.ToByte(str_epclen, 16);
                sEPC = sEPC.Substring(2);
                index++;
                string RSSI = tagInfo.Substring(index);

                //JW - Insert Raw Data Here and call the function to check if the EPC exists on the list.
                List<string> Raw_EPC_List = new List<string>();
                string time_now = DateTime.Now.ToString();
                Raw_EPC_List.Add(sEPC);
                Raw_EPC_List.Add(RSSI);
                Raw_EPC_List.Add(time_now);
                Raw_EPC_List.Add(str_ant);
                EPC_Checker(Raw_EPC_List);
                Raw_EPC_List.Clear();

            }
            else if (m.Msg == WM_MIXTAG)
            {
                string tagInfo = Marshal.PtrToStringAnsi(m.LParam);
                string sEPC, sData;
                string str_ant = tagInfo.Substring(0, 4);
                tagInfo = tagInfo.Substring(5);

                int index = tagInfo.IndexOf(',');
                sEPC = tagInfo.Substring(0, index);
                int n = sEPC.IndexOf("-");
                sData = sEPC.Substring(n + 1);
                sEPC = sEPC.Substring(0, n);
                index++;
                string RSSI = tagInfo.Substring(index);
                
            }
            else if (m.Msg == WM_FASTID)
            {
                string tagInfo = Marshal.PtrToStringAnsi(m.LParam);
                string sEPC = "";
                string sTID = "";
                string str_ant = tagInfo.Substring(0, 4);
                tagInfo = tagInfo.Substring(5);
                int index = tagInfo.IndexOf(',');
                sEPC = tagInfo.Substring(0, index);
                string str_epclen = sEPC.Substring(0, 2);
                byte nlen = Convert.ToByte(str_epclen, 16);
                if ((nlen & 0x80) == 0)
                {
                    sEPC = sEPC.Substring(2);//只有EPC
                    if (epclist.IndexOf(sEPC) == -1)
                    {
                        epclist.Add(sEPC);
                    }
                }
                else
                {
                    int epclen = (nlen & 0x7F) - 12;
                    sTID = sEPC.Substring(2 + epclen * 2, 24);
                    sEPC = sEPC.Substring(2, epclen * 2);
                    if (epclist.IndexOf(sEPC) == -1)
                    {
                        epclist.Add(sEPC);
                    }
                    if (tidlist.IndexOf(sTID) == -1)
                    {
                        tidlist.Add(sTID);
                    }

                }
                index++;
                string RSSI = tagInfo.Substring(index);
                
            }
            else if (m.Msg == WM_SENDTAGSTAT)
            {
                string tagInfo = Marshal.PtrToStringAnsi(m.LParam);
                int index = tagInfo.IndexOf(',');
                string tagRate = tagInfo.Substring(0, index);
                index++;
                string str = tagInfo.Substring(index);
                index = str.IndexOf(',');
                string tagNum = str.Substring(0, index);
                index++;
                string cmdTime = str.Substring(index);

            }
            else if (m.Msg == WM_SENDSTATU)
            {
                string Info = Marshal.PtrToStringAnsi(m.LParam);
                fCmdRet = Convert.ToInt32(Info);
                string strLog = "Inventory: " + GetReturnCodeDesc(fCmdRet);
            }
            else if (m.Msg == WM_SENDBUFF)
            {
                string tagInfo = Marshal.PtrToStringAnsi(m.LParam);
                int index = tagInfo.IndexOf(',');
                string tagNum = tagInfo.Substring(0, index);
                index++;

                string str = tagInfo.Substring(index);
                index = str.IndexOf(',');
                string cmdTime = str.Substring(0, index);
                index++;

                str = str.Substring(index);
                index = str.IndexOf(',');
                string tagRate = str.Substring(0, index);
                index++;

                str = str.Substring(index);
                string total_tagnum = str;

            }
            else
                base.DefWndProc(ref m);
        }

        private delegate void WriteLogUnSafe(CustomControl.LogRichTextBox logRichTxt, string strLog, int nType);
        private void WriteLog(CustomControl.LogRichTextBox logRichTxt, string strLog, int nType)
        {
            if (this.InvokeRequired)
            {
                WriteLogUnSafe InvokeWriteLog = new WriteLogUnSafe(WriteLog);
                this.Invoke(InvokeWriteLog, new object[] { logRichTxt, strLog, nType });
            }
            else
            {
                if ((nType == 0) || (nType == 0x26) || (nType == 0x01) || (nType == 0x02) || (nType == 0xFB))
                {
                    logRichTxt.AppendTextEx(strLog, Color.Indigo);
                }
                else
                {
                    logRichTxt.AppendTextEx(strLog, Color.Red);
                }

                logRichTxt.Select(logRichTxt.TextLength, 0);
                logRichTxt.ScrollToCaret();
            }
        }
        /// <summary>
        /// 16进制数组字符串转换
        /// </summary>
        /// <param name="s"></param>
        /// <returns></returns>
        #region 
        public static byte[] HexStringToByteArray(string s)
        {
            s = s.Replace(" ", "");
            byte[] buffer = new byte[s.Length / 2];
            for (int i = 0; i < s.Length; i += 2)
                buffer[i / 2] = (byte)Convert.ToByte(s.Substring(i, 2), 16);
            return buffer;
        }

        public static string ByteArrayToHexString(byte[] data)
        {
            StringBuilder sb = new StringBuilder(data.Length * 3);
            foreach (byte b in data)
                sb.Append(Convert.ToString(b, 16).PadLeft(2, '0'));
            return sb.ToString().ToUpper();

        }
        #endregion
        /// <summary>
        /// 错误代码
        /// </summary>
        /// <param name="cmdRet"></param>
        /// <returns></returns>
        #region 
        private string GetReturnCodeDesc(int cmdRet)
        {
            switch (cmdRet)
            {
                case 0x00:
                case 0x26:
                    return "success";
                case 0x01:
                    return "Return before Inventory finished";
                case 0x02:
                    return "the Inventory-scan-time overflow";
                case 0x03:
                    return "More Data";
                case 0x04:
                    return "Reader module MCU is Full";
                case 0x05:
                    return "Access Password Error";
                case 0x09:
                    return "Destroy Password Error";
                case 0x0a:
                    return "Destroy Password Error Cannot be Zero";
                case 0x0b:
                    return "Tag Not Support the command";
                case 0x0c:
                    return "Use the commmand,Access Password Cannot be Zero";
                case 0x0d:
                    return "Tag is protected,cannot set it again";
                case 0x0e:
                    return "Tag is unprotected,no need to reset it";
                case 0x10:
                    return "There is some locked bytes,write fail";
                case 0x11:
                    return "can not lock it";
                case 0x12:
                    return "is locked,cannot lock it again";
                case 0x13:
                    return "Parameter Save Fail,Can Use Before Power";
                case 0x14:
                    return "Cannot adjust";
                case 0x15:
                    return "Return before Inventory finished";
                case 0x16:
                    return "Inventory-Scan-Time overflow";
                case 0x17:
                    return "More Data";
                case 0x18:
                    return "Reader module MCU is full";
                case 0x19:
                    return "'Not Support Command Or AccessPassword Cannot be Zero";
                case 0x1A:
                    return "Tag custom function error";
                case 0xF8:
                    return "Check antenna error";
                case 0xF9:
                    return "Command execute error";
                case 0xFA:
                    return "Get Tag,Poor Communication,Inoperable";
                case 0xFB:
                    return "No Tag Operable";
                case 0xFC:
                    return "Tag Return ErrorCode";
                case 0xFD:
                    return "Command length wrong";
                case 0xFE:
                    return "Illegal command";
                case 0xFF:
                    return "Parameter Error";
                case 0x30:
                    return "Communication error";
                case 0x31:
                    return "CRC checksummat error";
                case 0x32:
                    return "Return data length error";
                case 0x33:
                    return "Communication busy";
                case 0x34:
                    return "Busy,command is being executed";
                case 0x35:
                    return "ComPort Opened";
                case 0x36:
                    return "ComPort Closed";
                case 0x37:
                    return "Invalid Handle";
                case 0x38:
                    return "Invalid Port";
                case 0xEE:
                    return "Return Command Error";
                default:
                    return "";
            }
        }
        private string GetErrorCodeDesc(int cmdRet)
        {
            switch (cmdRet)
            {
                case 0x00:
                    return "Other error";
                case 0x03:
                    return "Memory out or pc not support";
                case 0x04:
                    return "Memory Locked and unwritable";
                case 0x0b:
                    return "No Power,memory write operation cannot be executed";
                case 0x0f:
                    return "Not Special Error,tag not support special errorcode";
                default:
                    return "";
            }
        }
        #endregion
        private void DisabledForm()
        {
            ////应答模式下
            text_RDVersion.Text = "";
            text_Serial.Text = "";
            timer_answer.Enabled = false;
            btIventoryG2.Text = "Start";
            panel9.Enabled = false;
            gpb_address.Enabled = false;
            gpb_antconfig.Enabled = false;
            gpb_baud.Enabled = false;
            gpb_GPIO.Enabled = false;
            gpb_beep.Enabled = false;
            gpb_RDVersion.Enabled = false;
            gpb_checkant.Enabled = false;
            gpb_DBM.Enabled = false;
            gpb_Serial.Enabled = false;
            gpb_Relay.Enabled = false;
            gpb_OutputRep.Enabled = false;
            gpb_Freq.Enabled = false;
            gbp_buff.Enabled = false;
            btDefault.Enabled = false;
            btGetInformation.Enabled = false;
            group_maxtime.Enabled = false;
            gbp_wpower.Enabled = false;
            gbp_Retry.Enabled = false;
            gbp_DRM.Enabled = false;
            gbCmdTemperature.Enabled = false;
            gbReturnLoss.Enabled = false;
            btDefault.Enabled = false;
        }
        private void EnabledForm()
        {
            panel9.Enabled = true;
            gpb_address.Enabled = true;
            gpb_antconfig.Enabled = true;
            gpb_baud.Enabled = true;
            gpb_GPIO.Enabled = true;
            gpb_beep.Enabled = true;
            gpb_RDVersion.Enabled = true;
            gpb_checkant.Enabled = true;
            gpb_DBM.Enabled = true;
            gpb_Serial.Enabled = true;
            gpb_Relay.Enabled = true;
            gpb_OutputRep.Enabled = true;
            gpb_Freq.Enabled = true;
            gbp_buff.Enabled = true;
            btGetInformation.Enabled = true;
            group_maxtime.Enabled = true;
            gbp_wpower.Enabled = true;
            gbp_Retry.Enabled = true;
            gbp_DRM.Enabled = true;
            gbCmdTemperature.Enabled = true;
            gbReturnLoss.Enabled = true;
            btDefault.Enabled = true;
        }
        private void rb_rs232_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_rs232.Checked)
            {
                if ((frmcomportindex > 0) && (frmcomportindex < 256))
                {
                    if (frmcomportindex > 0)
                        fCmdRet = RWDev.CloseNetPort(frmcomportindex);
                    if (fCmdRet == 0)
                    {
                        frmcomportindex = -1;
                        btConnectTcp.Enabled = true;
                        btDisConnectTcp.Enabled = false;
                        DisabledForm();
                        btConnectTcp.ForeColor = Color.Indigo;
                        btDisConnectTcp.ForeColor = Color.Black;
                        SetButtonBold(btConnectTcp);
                        SetButtonBold(btDisConnectTcp);
                    }
                    if (fCmdRet != 0)
                    {
                        string strLog = "TCP close failed: " + GetReturnCodeDesc(fCmdRet);

                        return;
                    }
                    else
                    {
                        string strLog = "TCP close success";
                    }


                }

                gpb_rs232.Enabled = true;
                btDisConnect232.Enabled = false;
                //设置按钮字体颜色
                btConnect232.ForeColor = Color.Indigo;
                SetButtonBold(btConnect232);
                if (btConnectTcp.Font.Bold)
                {
                    SetButtonBold(btConnectTcp);
                }
                gpb_tcp.Enabled = false;
            }
        }

        private void rb_tcp_CheckedChanged(object sender, EventArgs e)
        {
            if (rb_tcp.Checked)
            {
                if ((frmcomportindex > 0) && (frmcomportindex < 256))
                {
                    if (frmcomportindex > 0)
                        fCmdRet = RWDev.CloseSpecComPort(frmcomportindex);
                    if (fCmdRet == 0)
                    {
                        frmcomportindex = -1;
                        DisabledForm();
                        btConnect232.Enabled = true;
                        btDisConnect232.Enabled = false;

                        btConnect232.ForeColor = Color.Indigo;
                        btDisConnect232.ForeColor = Color.Black;
                        SetButtonBold(btConnect232);
                        SetButtonBold(btDisConnect232);
                    }
                    if (fCmdRet != 0)
                    {
                        string strLog = "COM close failed: " + GetReturnCodeDesc(fCmdRet);

                        return;
                    }
                    else
                    {
                        string strLog = "COM close success";
                    }
                }
                gpb_tcp.Enabled = true;
                btDisConnectTcp.Enabled = false;

                //设置按钮字体颜色
                btConnectTcp.ForeColor = Color.Indigo;
                if (btConnect232.Font.Bold)
                {
                    SetButtonBold(btConnect232);
                }
                SetButtonBold(btConnectTcp);
                gpb_rs232.Enabled = false;
            }
        }
        private void SetButtonBold(Button btnBold)
        {
            Font oldFont = btnBold.Font;
            Font newFont = new Font(oldFont, oldFont.Style ^ FontStyle.Bold);
            btnBold.Font = newFont;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            gpb_rs232.Enabled = false;
            gpb_tcp.Enabled = false;
            rb_rs232.Checked = true;
            ComboBox_COM.SelectedIndex = 0;
            ComboBox_baud2.SelectedIndex = 3;
            int i = 0;
            for (i = 0x00; i <= 0xff; i++)
            {
                comboBox_maxtime.Items.Add(Convert.ToString(i) + "*100ms");
            }
            comboBox_maxtime.SelectedIndex = 0;
            DisabledForm();
            radioButton_band2.Checked = true;
            ComboBox_baud.SelectedIndex = 3;
            ComboBox_PowerDbm.SelectedIndex = 30;

            for (i = 0; i < 256; i++)
            {
                com_MFliterTime.Items.Add(i.ToString() + "*1s");
            }
            for (i = 1; i < 256; i++)
            {
                ComboBox_RelayTime.Items.Add(Convert.ToString(i));
            }
            ComboBox_RelayTime.SelectedIndex = 0;
            ComboBox_RelayTime.SelectedIndex = 0;

            com_MFliterTime.SelectedIndex = 0;
            COM_MRPTime.SelectedIndex = 0;
            com_MQ.SelectedIndex = 4;
            com_MS.SelectedIndex = 4;
            com_Mmode.SelectedIndex = 0;
            com_wpower.SelectedIndex = 30;
            com_retrytimes.SelectedIndex = 3;
            cbbAnt.SelectedIndex = 0;
        }

        private void btConnect232_Click(object sender, EventArgs e)
        {
            int portNum = ComboBox_COM.SelectedIndex + 1;
            int FrmPortIndex = 0;
            string strException = string.Empty;
            fBaud = Convert.ToByte(ComboBox_baud2.SelectedIndex);
            if (fBaud > 2)
                fBaud = Convert.ToByte(fBaud + 2);
            fComAdr = 255;//广播地址打开设备
            fCmdRet = RWDev.OpenComPort(portNum, ref fComAdr, fBaud, ref FrmPortIndex);
            if (fCmdRet != 0)
            {
                string strLog = "Connect reader failed: " + GetReturnCodeDesc(fCmdRet);
                return;
            }
            else
            {
                frmcomportindex = FrmPortIndex;
                string strLog = "Connect: " + ComboBox_COM.Text + "@" + ComboBox_baud2.Text;
                hw_connected = true;
            }

            //处理界面元素是否有效
            EnabledForm();
            btConnect232.Enabled = false;
            btDisConnect232.Enabled = true;
            //设置按钮字体颜色
            btConnect232.ForeColor = Color.Black;
            btDisConnect232.ForeColor = Color.Indigo;
            SetButtonBold(btConnect232);
            SetButtonBold(btDisConnect232);
            btGetInformation_Click(null, null);//获取读写器信息
            if (FrmPortIndex > 0)
                RWDev.InitRFIDCallBack(elegateRFIDCallBack, true, FrmPortIndex);
        }

        private void btDisConnect232_Click(object sender, EventArgs e)
        {
            if (frmcomportindex > 0)
                fCmdRet = RWDev.CloseSpecComPort(frmcomportindex);
            if (fCmdRet == 0)
            {
                frmcomportindex = -1;
                DisabledForm();
                btConnect232.Enabled = true;
                btDisConnect232.Enabled = false;

                btConnect232.ForeColor = Color.Indigo;
                btDisConnect232.ForeColor = Color.Black;
                SetButtonBold(btConnect232);
                SetButtonBold(btDisConnect232);
            }
            if (fCmdRet != 0)
            {
                string strLog = "COM close failed: " + GetReturnCodeDesc(fCmdRet);

                return;
            }
            else
            {
                string strLog = "COM close success";
                hw_connected = false;
            }
        }

        private void btDisConnectTcp_Click(object sender, EventArgs e)
        {
            if (frmcomportindex > 0)
                fCmdRet = RWDev.CloseNetPort(frmcomportindex);
            if (fCmdRet == 0)
            {
                frmcomportindex = -1;
                btConnectTcp.Enabled = true;
                btDisConnectTcp.Enabled = false;
                DisabledForm();
                btConnectTcp.ForeColor = Color.Indigo;
                btDisConnectTcp.ForeColor = Color.Black;
                SetButtonBold(btConnectTcp);
                SetButtonBold(btDisConnectTcp);
            }
            if (fCmdRet != 0)
            {
                string strLog = "TCP close failed " + GetReturnCodeDesc(fCmdRet);

                return;
            }
            else
            {
                string strLog = "TCP close success";
                hw_connected = false;
            }
        }

        private void btConnectTcp_Click(object sender, EventArgs e)
        {
            try
            {
                string strException = string.Empty;
                string ipAddress = ipServerIP.IpAddressStr;
                int nPort = Convert.ToInt32(tb_Port.Text);
                fComAdr = 255;
                int FrmPortIndex = 0;
                fCmdRet = RWDev.OpenNetPort(nPort, ipAddress, ref fComAdr, ref FrmPortIndex);
                if (fCmdRet != 0)
                {
                    string strLog = "Connect reader failed: " + GetReturnCodeDesc(fCmdRet);
                    return;
                }
                else
                {
                    frmcomportindex = FrmPortIndex;
                    string strLog = "Connect: " + ipAddress + "@" + nPort.ToString();
                    hw_connected = true;
                }


                EnabledForm();
                btConnectTcp.Enabled = false;
                btDisConnectTcp.Enabled = true;

                //设置按钮字体颜色
                btConnectTcp.ForeColor = Color.Black;
                btDisConnectTcp.ForeColor = Color.Indigo;
                SetButtonBold(btConnectTcp);
                SetButtonBold(btDisConnectTcp);
                btGetInformation_Click(null, null);//获取读写器信息
                if (FrmPortIndex > 0)
                    RWDev.InitRFIDCallBack(elegateRFIDCallBack, true, FrmPortIndex);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        byte[] antlist = new byte[4];
        private volatile bool fIsInventoryScan = false;
        private volatile bool toStopThread = false;
        private Thread mythread = null;

        byte[] ReadAdr = new byte[2];
        byte[] Psd = new byte[4];
        byte ReadLen = 0;
        byte ReadMem = 0;

        private void btIventoryG2_Click(object sender, EventArgs e)
        {

            if (btIventoryG2.Text == "Start")
            {
                epclist.Clear();
                tidlist.Clear();
                AA_times = 0;

                startup = false;

                total_tagnum = 0;
                total_time = System.Environment.TickCount;
                fIsInventoryScan = false;
                btIventoryG2.BackColor = Color.Indigo;
                btIventoryG2.Text = "Stop";
                Array.Clear(antlist, 0, 4);
                if (check_ant1.Checked)
                {
                    antlist[0] = 1;
                    InAnt = 0x80;
                }
                if (check_ant2.Checked)
                {
                    antlist[1] = 1;
                    InAnt = 0x81;
                }
                if (check_ant3.Checked)
                {
                    antlist[2] = 1;
                    InAnt = 0x82;
                }
                if (check_ant4.Checked)
                {
                    antlist[3] = 1;
                    InAnt = 0x83;
                }
                toStopThread = false;
                if (fIsInventoryScan == false)
                {
                    mythread = new Thread(new ThreadStart(inventory));
                    mythread.IsBackground = true;
                    mythread.Start();
                    timer_answer.Enabled = true;
                }
            }
            else
            {
                toStopThread = true;
                btIventoryG2.Enabled = false;
                btIventoryG2.BackColor = Color.Transparent;
                btIventoryG2.Text = "Stop";
            }
        }
        #region ///EPC或TID查询
        private void flash_G2()
        {
            byte Ant = 0;
            int TagNum = 0;
            int Totallen = 0;
            int EPClen, m;
            byte[] EPC = new byte[50000];
            int CardIndex;
            string temps, temp;
            temp = "";
            string sEPC;
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[100];
            byte MaskFlag = 0;
            MaskFlag = 0;
            int cbtime = System.Environment.TickCount;
            CardNum = 0;
            fCmdRet = RWDev.Inventory_G2(ref fComAdr, Qvalue, Session, MaskMem, MaskAdr, MaskLen, MaskData, MaskFlag, tidAddr, tidLen, TIDFlag, Target, InAnt, Scantime, FastFlag, EPC, ref Ant, ref Totallen, ref TagNum, frmcomportindex);
            int cmdTime = System.Environment.TickCount - cbtime;//命令时间
            if ((fCmdRet == 0x30) || (fCmdRet == 0x37))
            {
                if (rb_tcp.Checked)
                {
                    if ((frmcomportindex > 0) && (frmcomportindex < 256))
                    {
                        fCmdRet = RWDev.CloseNetPort(frmcomportindex);
                        if (fCmdRet == 0) frmcomportindex = -1;
                        Thread.Sleep(1000);
                    }
                    fComAdr = 255;
                    string ipAddress = ipServerIP.IpAddressStr;
                    int nPort = Convert.ToInt32(tb_Port.Text);
                    fCmdRet = RWDev.OpenNetPort(nPort, ipAddress, ref fComAdr, ref frmcomportindex);
                }
            }
            if (fCmdRet == 0x30)
            {
                CardNum = 0;
            }
            if (CardNum == 0)
            {
                if (Session > 1)
                    AA_times = AA_times + 1;//没有得到标签只更新状态栏
            }
            else
                AA_times = 0;
            if ((fCmdRet == 1) || (fCmdRet == 2) || (fCmdRet == 0xFB) || (fCmdRet == 0x26))
            {
                if (cmdTime > CommunicationTime)
                    cmdTime = cmdTime - CommunicationTime;//减去通讯时间等于标签的实际时间
                if (cmdTime > 0)
                {
                    int tagrate = (CardNum * 1000) / cmdTime;//速度等于张数/时间
                    IntPtr ptrWnd = IntPtr.Zero;
                    ptrWnd = FindWindow(null, "UHFReader288MP Demo V2.2");
                    if (ptrWnd != IntPtr.Zero)         // 检查当前统计窗口是否打开
                    {
                        string para = tagrate.ToString() + "," + total_tagnum.ToString() + "," + cmdTime.ToString();
                        SendMessage(ptrWnd, WM_SENDTAGSTAT, IntPtr.Zero, para);
                    }
                }

            }
            IntPtr ptrWnd1 = IntPtr.Zero;
            ptrWnd1 = FindWindow(null, "UHFReader288MP Demo V2.2");
            if (ptrWnd1 != IntPtr.Zero)         // 检查当前统计窗口是否打开
            {
                string para = fCmdRet.ToString();
                SendMessage(ptrWnd1, WM_SENDSTATU, IntPtr.Zero, para);
            }
            ptrWnd1 = IntPtr.Zero;
        }
        #endregion

        #region ///混合查询
        private void flashmix_G2()
        {
            byte Ant = 0;
            int TagNum = 0;
            int Totallen = 0;
            int EPClen, m;
            byte[] EPC = new byte[50000];
            int CardIndex;
            string temps, temp;
            temp = "";
            string sEPC;
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[100];
            byte MaskFlag = 0;
            MaskFlag = 0;
            int cbtime = System.Environment.TickCount;
            CardNum = 0;
            fCmdRet = RWDev.InventoryMix_G2(ref fComAdr, Qvalue, Session, MaskMem, MaskAdr, MaskLen, MaskData, MaskFlag, ReadMem, ReadAdr, ReadLen, Psd, Target, InAnt, Scantime, FastFlag, EPC, ref Ant, ref Totallen, ref TagNum, frmcomportindex);
            int cmdTime = System.Environment.TickCount - cbtime;//命令时间
            if ((fCmdRet == 0x30) || (fCmdRet == 0x37))
            {
                if (rb_tcp.Checked)
                {
                    if ((frmcomportindex > 0) && (frmcomportindex < 256))
                    {
                        fCmdRet = RWDev.CloseNetPort(frmcomportindex);
                        if (fCmdRet == 0) frmcomportindex = -1;
                        Thread.Sleep(1000);
                    }
                    fComAdr = 255;
                    string ipAddress = ipServerIP.IpAddressStr;
                    int nPort = Convert.ToInt32(tb_Port.Text);
                    fCmdRet = RWDev.OpenNetPort(nPort, ipAddress, ref fComAdr, ref frmcomportindex);
                }
            }
            if (CardNum == 0)
            {
                if (Session > 1)
                    AA_times = AA_times + 1;//没有得到标签只更新状态栏
            }
            else
                AA_times = 0;
            if ((fCmdRet == 1) || (fCmdRet == 2) || (fCmdRet == 0xFB) || (fCmdRet == 0x26))
            {
                if (cmdTime > CommunicationTime)
                    cmdTime = cmdTime - CommunicationTime;//减去通讯时间等于标签的实际时间
                if (cmdTime > 0)
                {
                    int tagrate = (CardNum * 1000) / cmdTime;//速度等于张数/时间
                    IntPtr ptrWnd = IntPtr.Zero;
                    ptrWnd = FindWindow(null, "UHFReader288MP Demo V2.2");
                    if (ptrWnd != IntPtr.Zero)         // 检查当前统计窗口是否打开
                    {
                        string para = tagrate.ToString() + "," + total_tagnum.ToString() + "," + cmdTime.ToString();
                        SendMessage(ptrWnd, WM_SENDTAGSTAT, IntPtr.Zero, para);
                    }
                }

            }
            IntPtr ptrWnd1 = IntPtr.Zero;
            ptrWnd1 = FindWindow(null, "UHFReader288MP Demo V2.2");
            if (ptrWnd1 != IntPtr.Zero)         // 检查当前统计窗口是否打开
            {
                string para = fCmdRet.ToString();
                SendMessage(ptrWnd1, WM_SENDSTATU, IntPtr.Zero, para);
            }
            ptrWnd1 = IntPtr.Zero;
        }
        #endregion

        private void inventory()
        {
            fIsInventoryScan = true;
            while (!toStopThread)
            {
                if (Session == 255)
                {
                    FastFlag = 0;
                    flash_G2();

                }
                else
                {
                    for (int m = 0; m < 4; m++)
                    {
                        switch (m)
                        {
                            case 0:
                                InAnt = 0x80;
                                break;
                            case 1:
                                InAnt = 0x81;
                                break;
                            case 2:
                                InAnt = 0x82;
                                break;
                            case 3:
                                InAnt = 0x83;
                                break;
                        }
                        FastFlag = 1;
                        if (antlist[m] == 1)
                        {
                            if (Session > 1)//s2,s3
                            {
                                if ((AA_times + 1 > targettimes))
                                {
                                    Target = Convert.ToByte(1 - Target);  //如果连续2次未读到卡片，A/B状态切换。
                                    AA_times = 0;
                                }
                            }
                            else
                            {
                                flash_G2();
                            }
                        }
                    }
                }
                Thread.Sleep(5);
            }
            this.Invoke((EventHandler)delegate
            {

                if (fIsInventoryScan)
                {
                    toStopThread = true;//标志，接收数据线程判断stop为true，正常情况下会自动退出线程           

                    btIventoryG2.Text = "Start";
                    mythread.Abort();//若线程无法退出，强制结束
                    timer_answer.Enabled = false;
                    fIsInventoryScan = false;
                }
                timer_answer.Enabled = false;
                fIsInventoryScan = false;
                btIventoryG2.Enabled = true;
            });

        }
        private void timer_answer_Tick(object sender, EventArgs e)
        {
            IntPtr ptrWnd = IntPtr.Zero;
            ptrWnd = FindWindow(null, "UHFReader288MP Demo V2.2");
            if (ptrWnd != IntPtr.Zero)         // 检查当前统计窗口是否打开
            {
                string para = fCmdRet.ToString();
                SendMessage(ptrWnd, WM_SHOWNUM, IntPtr.Zero, para);
            }
            ptrWnd = IntPtr.Zero;
        }

        private void radioButton_band1_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dmaxfre.Items.Clear();
            ComboBox_dminfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 20; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(920.125 + i * 0.25) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(920.125 + i * 0.25) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(920.125 + i * 0.25));
            }
            ComboBox_dmaxfre.SelectedIndex = 19;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 10;
        }

        private void radioButton_band2_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dmaxfre.Items.Clear();
            ComboBox_dminfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 50; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(902.75 + i * 0.5) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(902.75 + i * 0.5) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(902.75 + i * 0.5));
            }
            ComboBox_dmaxfre.SelectedIndex = 49;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 25;
        }

        private void radioButton_band3_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dmaxfre.Items.Clear();
            ComboBox_dminfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 32; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(917.1 + i * 0.2) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(917.1 + i * 0.2) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(917.1 + i * 0.2));
            }
            ComboBox_dmaxfre.SelectedIndex = 31;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 16;
        }

        private void radioButton_band4_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dminfre.Items.Clear();
            ComboBox_dmaxfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 15; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(865.1 + i * 0.2) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(865.1 + i * 0.2) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(865.1 + i * 0.2));
            }
            ComboBox_dmaxfre.SelectedIndex = 14;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 7;
        }

        private void CheckBox_SameFre_CheckedChanged(object sender, EventArgs e)
        {
            if (CheckBox_SameFre.Checked)
                ComboBox_dmaxfre.SelectedIndex = ComboBox_dminfre.SelectedIndex;
        }

        private bool CheckCRC(string s)
        {
            int i, j;
            int current_crc_value;
            byte crcL, crcH;
            byte[] data = HexStringToByteArray(s);
            current_crc_value = 0xFFFF;
            for (i = 0; i <= (data.Length - 1); i++)
            {
                current_crc_value = current_crc_value ^ (data[i]);
                for (j = 0; j < 8; j++)
                {
                    if ((current_crc_value & 0x01) != 0)
                        current_crc_value = (current_crc_value >> 1) ^ 0x8408;
                    else
                        current_crc_value = (current_crc_value >> 1);
                }
            }
            crcL = Convert.ToByte(current_crc_value & 0xFF);
            crcH = Convert.ToByte((current_crc_value >> 8) & 0xFF);
            if (crcH == 0 && crcL == 0)
            {
                return true;
            }
            else
            {
                return false;
            }

        }

        private void timer_runmode_Tick(object sender, EventArgs e)
        {

        }


        private void Maintab_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (fIsInventoryScan)
            {
                toStopThread = true;
                btIventoryG2.Enabled = false;
                btIventoryG2.BackColor = Color.Transparent;
                btIventoryG2.Text = "Start";
            }
            if (fIsBuffScan)
            {
                toStopThread = true;//标志，接收数据线程判断stop为true，正常情况下会自动退出线程                                
                ReadThread.Abort();//若线程无法退出，强制结束
                fIsInventoryScan = false;
            }
            timer_runmode.Enabled = false;
            timer_answer.Enabled = false;
            timer_RealTime.Enabled = false;
            btIventoryG2.Text = "Start";
            btIventoryG2.BackColor = Color.Transparent;

            if ((ReadTypes == "16") || (ReadTypes == "21"))//单口
            {
                check_ant1.Checked = true;
                check_ant2.Checked = false;
                check_ant3.Checked = false;
                check_ant4.Checked = false;
            }

        }

        private void tb_Port_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = ("0123456789".IndexOf(Char.ToUpper(e.KeyChar)) < 0);
        }

        private void text_address_KeyPress(object sender, KeyPressEventArgs e)
        {
            e.Handled = ("0123456789ABCDEF".IndexOf(Char.ToUpper(e.KeyChar)) < 0);
        }

        private void btGetInformation_Click(object sender, EventArgs e)
        {
            byte TrType = 0;
            byte[] VersionInfo = new byte[2];
            byte ReaderType = 0;
            byte ScanTime = 0;
            byte dmaxfre = 0;
            byte dminfre = 0;
            byte powerdBm = 0;
            byte FreBand = 0;
            byte Ant = 0;
            byte BeepEn = 0;
            byte OutputRep = 0;
            byte CheckAnt = 0;
            text_RDVersion.Text = "";
            int ctime = System.Environment.TickCount;
            fCmdRet = RWDev.GetReaderInformation(ref fComAdr, VersionInfo, ref ReaderType, ref TrType, ref dmaxfre, ref dminfre, ref powerdBm, ref ScanTime, ref Ant, ref BeepEn, ref OutputRep, ref CheckAnt, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get Reader Information failed:" + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                CommunicationTime = System.Environment.TickCount - ctime;
                comboBox_maxtime.SelectedIndex = ScanTime;
                //ReaderType = 0x20;
                switch (ReaderType)
                {
                    case 0x20://2882
                        {
                            ReadTypes = "20";
                            //GPIO显示
                            check_int2.Visible = true;
                            //天线显示
                            gpb_checkant.Enabled = true;
                            gpb_antconfig.Enabled = true;
                            //主动模式显示
                            if (tabControl1.TabPages.IndexOf(tabPage_Module) == -1)
                                tabControl1.TabPages.Add(tabPage_Module);

                            //缓存显示
                            gbp_buff.Enabled = true;
                            text_RDVersion.Text = "UHFReader288MP--" + Convert.ToString(VersionInfo[0], 10).PadLeft(2, '0') + "." + Convert.ToString(VersionInfo[1], 10).PadLeft(2, '0');
                        }
                        break;

                    case 0x21://9882M
                        {
                            ReadTypes = "21";
                            //GPIO显示
                            check_int2.Visible = true;
                            //天线显示
                            gpb_checkant.Enabled = false;
                            gpb_antconfig.Enabled = false;
                            //主动模式显示
                            tabControl1.TabPages.Remove(tabPage_Module);
                            //缓存显示
                            gbp_buff.Enabled = false;
                            text_RDVersion.Text = "UHFReader82--" + Convert.ToString(VersionInfo[0], 10).PadLeft(2, '0') + "." + Convert.ToString(VersionInfo[1], 10).PadLeft(2, '0');
                        }
                        break;
                    default:
                        text_RDVersion.Text = "0x" + Convert.ToString(ReaderType, 16).PadLeft(2, '0').ToUpper() + " -- " + Convert.ToString(VersionInfo[0], 10).PadLeft(2, '0') + "." + Convert.ToString(VersionInfo[1], 10).PadLeft(2, '0');
                        break;
                }
                ComboBox_PowerDbm.SelectedIndex = Convert.ToInt32(powerdBm);
                text_address.Text = Convert.ToString(fComAdr, 16).PadLeft(2, '0');
                FreBand = Convert.ToByte(((dmaxfre & 0xc0) >> 4) | (dminfre >> 6));
                switch (FreBand)
                {
                    case 1:
                        {
                            radioButton_band1.Checked = true;
                            fdminfre = 920.125 + (dminfre & 0x3F) * 0.25;
                            fdmaxfre = 920.125 + (dmaxfre & 0x3F) * 0.25;
                        }
                        break;
                    case 2:
                        {
                            radioButton_band2.Checked = true;
                            fdminfre = 902.75 + (dminfre & 0x3F) * 0.5;
                            fdmaxfre = 902.75 + (dmaxfre & 0x3F) * 0.5;
                        }
                        break;
                    case 3:
                        {
                            radioButton_band3.Checked = true;
                            fdminfre = 917.1 + (dminfre & 0x3F) * 0.2;
                            fdmaxfre = 917.1 + (dmaxfre & 0x3F) * 0.2;
                        }
                        break;
                    case 4:
                        {
                            radioButton_band4.Checked = true;
                            fdminfre = 865.1 + (dminfre & 0x3F) * 0.2;
                            fdmaxfre = 865.1 + (dmaxfre & 0x3F) * 0.2;
                        }
                        break;
                    case 8:
                        {
                            radioButton_band8.Checked = true;
                            fdminfre = 840.125 + (dminfre & 0x3F) * 0.25;
                            fdmaxfre = 840.125 + (dmaxfre & 0x3F) * 0.25;
                        }
                        break;
                    case 12:
                        {
                            radioButton_band12.Checked = true;
                            fdminfre = 902 + (dminfre & 0x3F) * 0.5;
                            fdmaxfre = 902 + (dmaxfre & 0x3F) * 0.5;
                        }
                        break;
                }
                if (fdmaxfre != fdminfre)
                    CheckBox_SameFre.Checked = false;
                ComboBox_dminfre.SelectedIndex = dminfre & 0x3F;
                ComboBox_dmaxfre.SelectedIndex = dmaxfre & 0x3F;
                switch (BeepEn)
                {
                    case 1:
                        Radio_beepEn.Checked = true;
                        break;
                    case 0:
                        Radio_beepDis.Checked = true;
                        break;
                }

                if ((Ant & 0x01) == 1)
                {
                    check_ant1.Checked = true;
                    checkant1.Checked = true;
                }
                else
                {
                    check_ant1.Checked = false;
                    checkant1.Checked = false;
                }

                if ((Ant & 0x02) == 2)
                {
                    check_ant2.Checked = true;
                    checkant2.Checked = true;
                }
                else
                {
                    check_ant2.Checked = false;
                    checkant2.Checked = false;
                }

                if ((Ant & 0x04) == 4)
                {
                    check_ant3.Checked = true;
                    checkant3.Checked = true;
                }
                else
                {
                    check_ant3.Checked = false;
                    checkant3.Checked = false;
                }

                if ((Ant & 0x08) == 8)
                {
                    check_ant4.Checked = true;
                    checkant4.Checked = true;
                }
                else
                {
                    check_ant4.Checked = false;
                    checkant4.Checked = false;
                }

                if ((OutputRep & 0x01) == 1)
                    check_OutputRep1.Checked = true;
                else
                    check_OutputRep1.Checked = false;

                if ((OutputRep & 0x02) == 2)
                    check_OutputRep2.Checked = true;
                else
                    check_OutputRep2.Checked = false;

                if ((OutputRep & 0x04) == 4)
                    check_OutputRep3.Checked = true;
                else
                    check_OutputRep3.Checked = false;

                if ((OutputRep & 0x08) == 8)
                    check_OutputRep4.Checked = true;
                else
                    check_OutputRep4.Checked = false;

                if (CheckAnt == 0)
                {
                    rb_Closecheckant.Checked = true;
                }
                else
                {
                    rb_Opencheckant.Checked = true;
                }
                string strLog = "Get Reader Information success ";
            }
        }

        private void btDefault_Click(object sender, EventArgs e)
        {
            byte aNewComAdr, powerDbm, dminfre, dmaxfre, scantime;
            dminfre = 128;
            dmaxfre = 49;
            aNewComAdr = 0x00;
            powerDbm = 30;
            fBaud = 5;
            scantime = 0;
            ComboBox_baud.SelectedIndex = 3;
            fCmdRet = RWDev.SetAddress(ref fComAdr, aNewComAdr, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set Reader address failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set Reader address success ";
            }

            fCmdRet = RWDev.SetRfPower(ref fComAdr, powerDbm, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set Power failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set power success ";
            }

            fCmdRet = RWDev.SetRegion(ref fComAdr, dmaxfre, dminfre, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set Region failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set Region success";
            }

            fCmdRet = RWDev.SetBaudRate(ref fComAdr, fBaud, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set baud rate failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set baud rate success ";
            }

            fCmdRet = RWDev.SetInventoryScanTime(ref fComAdr, scantime, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set inventory scan time failed:： " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set inventory scan time success ";
            }
            btGetInformation_Click(null, null);
        }

        private void btaddress_Click(object sender, EventArgs e)
        {
            byte aNewComAdr = Convert.ToByte(text_address.Text, 16);
            fCmdRet = RWDev.SetAddress(ref fComAdr, aNewComAdr, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set reader address failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set reader address success ";
            }
        }

        private void btFreq_Click(object sender, EventArgs e)
        {
            byte dminfre, dmaxfre;
            int band = 2;
            if (radioButton_band1.Checked)
                band = 1;
            if (radioButton_band2.Checked)
                band = 2;
            if (radioButton_band3.Checked)
                band = 3;
            if (radioButton_band4.Checked)
                band = 4;
            if (radioButton_band8.Checked)
                band = 8;
            if (radioButton_band12.Checked)
                band = 12;
            dminfre = Convert.ToByte(((band & 3) << 6) | (ComboBox_dminfre.SelectedIndex & 0x3F));
            dmaxfre = Convert.ToByte(((band & 0x0c) << 4) | (ComboBox_dmaxfre.SelectedIndex & 0x3F));
            fCmdRet = RWDev.SetRegion(ref fComAdr, dmaxfre, dminfre, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set region failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set region success ";
            }
        }

        private void BT_DBM_Click(object sender, EventArgs e)
        {
            byte powerDbm = (byte)ComboBox_PowerDbm.SelectedIndex;
            fCmdRet = RWDev.SetRfPower(ref fComAdr, powerDbm, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set power failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set power success ";
            }
        }

        private void btBaudRate_Click(object sender, EventArgs e)
        {
            byte fBaud = (byte)ComboBox_baud.SelectedIndex;
            if (fBaud > 2)
                fBaud = (byte)(fBaud + 2);
            fCmdRet = RWDev.SetBaudRate(ref fComAdr, fBaud, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set baud rate failed " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set baud rate success ";
            }
        }

        private void btSerial_Click(object sender, EventArgs e)
        {
            byte[] SeriaNo = new byte[4];
            text_Serial.Text = "";
            fCmdRet = RWDev.GetSeriaNo(ref fComAdr, SeriaNo, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get serial number failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                text_Serial.Text = ByteArrayToHexString(SeriaNo);
                string strLog = "Get serial number success ";
            }
        }

        private void Button_Beep_Click(object sender, EventArgs e)
        {
            byte BeepEn = 0;
            if (Radio_beepEn.Checked)
                BeepEn = 1;
            else
                BeepEn = 0;
            fCmdRet = RWDev.SetBeepNotification(ref fComAdr, BeepEn, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set beep failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set beep success ";
            }
        }

        private void btRelay_Click(object sender, EventArgs e)
        {
            byte RelayTime = 0;
            RelayTime = Convert.ToByte(ComboBox_RelayTime.SelectedIndex + 1);
            fCmdRet = RWDev.SetRelay(ref fComAdr, RelayTime, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set relay failed:" + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set relay success ";
            }
        }

        private void btOutputRep_Click(object sender, EventArgs e)
        {
            byte OutputRep = 0;
            if (check_OutputRep1.Checked)
                OutputRep = Convert.ToByte(OutputRep | 0x01);
            if (check_OutputRep2.Checked)
                OutputRep = Convert.ToByte(OutputRep | 0x02);
            if (check_OutputRep3.Checked)
                OutputRep = Convert.ToByte(OutputRep | 0x04);
            if (check_OutputRep3.Checked)
                OutputRep = Convert.ToByte(OutputRep | 0x08);
            fCmdRet = RWDev.SetNotificationPulseOutput(ref fComAdr, OutputRep, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set notification pulse output failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set notification pulse output success ";
            }
        }

        private void Button_SetGPIO_Click(object sender, EventArgs e)
        {
            byte OutputPin = 0;
            if (check_out1.Checked)
                OutputPin = Convert.ToByte(OutputPin | 0x01);
            if (check_out2.Checked)
                OutputPin = Convert.ToByte(OutputPin | 0x02);

            fCmdRet = RWDev.SetGPIO(ref fComAdr, OutputPin, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set GPIO failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set GPIO success";
            }
        }

        private void Button_GetGPIO_Click(object sender, EventArgs e)
        {
            byte OutputPin = 0;
            fCmdRet = RWDev.GetGPIOStatus(ref fComAdr, ref OutputPin, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get GPIO failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                if ((OutputPin & 0x10) == 0x10)
                    check_out1.Checked = true;
                else
                    check_out1.Checked = false;

                if ((OutputPin & 0x20) == 0x20)
                    check_out2.Checked = true;
                else
                    check_out2.Checked = false;



                if ((OutputPin & 0x01) == 1)
                    check_int1.Checked = true;
                else
                    check_int1.Checked = false;

                if ((OutputPin & 0x02) == 2)
                    check_int2.Checked = true;
                else
                    check_int2.Checked = false;


                string strLog = "Get GPIO success ";
            }
        }

        private void btSetcheckant_Click(object sender, EventArgs e)
        {
            byte CheckAnt = 0;
            if (rb_Opencheckant.Checked)
                CheckAnt = 1;
            else
                CheckAnt = 0;
            fCmdRet = RWDev.SetCheckAnt(ref fComAdr, CheckAnt, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set antenna check failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set antenna check success ";
            }
        }

        private void Button_Ant_Click(object sender, EventArgs e)
        {
            byte ANT = 0;
            byte ANT1 = 0;
            if (checkant1.Checked) ANT = Convert.ToByte(ANT | 1);
            if (checkant2.Checked) ANT = Convert.ToByte(ANT | 2);
            if (checkant3.Checked) ANT = Convert.ToByte(ANT | 4);
            if (checkant4.Checked) ANT = Convert.ToByte(ANT | 8);
            ANT1 = ANT;
            fCmdRet = RWDev.SetAntennaMultiplexing(ref fComAdr, ANT, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Antenna config failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                if ((ANT1 & 0x01) == 1)
                {
                    check_ant1.Checked = true;
                }
                else
                {
                    check_ant1.Checked = false;
                }

                if ((ANT1 & 0x02) == 2)
                {
                    check_ant2.Checked = true;
                }
                else
                {
                    check_ant2.Checked = false;
                }

                if ((ANT1 & 0x04) == 4)
                {
                    check_ant3.Checked = true;
                }
                else
                {
                    check_ant3.Checked = false;
                }

                if ((ANT1 & 0x08) == 8)
                {
                    check_ant4.Checked = true;
                }
                else
                {
                    check_ant4.Checked = false;
                }
                string strLog = "Antenna config success ";
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            DevControl.tagErrorCode eCode = DevControl.DM_DeInit();
            if (eCode != DevControl.tagErrorCode.DM_ERR_OK)
            {
                ErrorHandling.HandleError(eCode);
            }
        }

        private void btSetEPCandTIDLen_Click(object sender, EventArgs e)
        {
            byte SaveLen = 0;
            if (rb128.Checked)
            {
                SaveLen = 0;
            }
            else
            {
                SaveLen = 1;
            }
            fCmdRet = RWDev.SetSaveLen(ref fComAdr, SaveLen, frmcomportindex);
            if (fCmdRet == 0)
            {
                string strLog = "Set save length success ";
            }
            else
            {
                string strLog = "Set save length failed: " + GetReturnCodeDesc(fCmdRet);
            }
        }

        private void btGetEPCandTIDLen_Click(object sender, EventArgs e)
        {
            byte SaveLen = 0;
            fCmdRet = RWDev.GetSaveLen(ref fComAdr, ref SaveLen, frmcomportindex);
            if (fCmdRet == 0)
            {
                if (SaveLen == 0)
                    rb128.Checked = true;
                else
                    rb496.Checked = true;
                string strLog = "Get save length success ";
            }
            else
            {
                string strLog = "Get save length failed: " + GetReturnCodeDesc(fCmdRet);
            }
        }

        private Thread ReadThread = null;
        private volatile bool fIsBuffScan = false;
        /***
        private void GetBuffData()
        {
            int TagNum = 0;
            int BufferCount = 0;
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[100];
            byte MaskFlag = 0;
            byte AdrTID = 0;
            byte LenTID = 0;
            AdrTID = 0;
            LenTID = 6;
            MaskFlag = 0;
            int cbtime = System.Environment.TickCount;
            TagNum = 0;
            BufferCount = 0;
            Target = 0;
            Scantime = 0x14;
            Qvalue = 6;
            if (TIDFlag == 0)
                Session = 255;
            else
                Session = 1;
            FastFlag = 0;
            fCmdRet = RWDev.InventoryBuffer_G2(ref fComAdr, Qvalue, Session, MaskMem, MaskAdr, MaskLen, MaskData, MaskFlag, AdrTID, LenTID, TIDFlag, Target, InAnt, Scantime, FastFlag, ref BufferCount, ref TagNum, frmcomportindex);
            int x_time = System.Environment.TickCount - cbtime;//命令时间
            //string strLog = "带缓存查询： " + GetReturnCodeDesc(fCmdRet);
            //WriteLog(lrtxtLog, strLog, 0);
            ///////////设置网络断线重连
            if (fCmdRet == 0)//代表已查找结束，
            {
                IntPtr ptrWnd = IntPtr.Zero;
                ptrWnd = FindWindow(null, "UHFReader288MP Demo V2.2");
                if (ptrWnd != IntPtr.Zero)         // 检查当前统计窗口是否打开
                {
                    total_tagnum = total_tagnum + TagNum;
                    int tagrate = (TagNum * 1000) / x_time;//速度等于张数/时间
                    string para = BufferCount.ToString() + "," + x_time.ToString() + "," + tagrate.ToString() + "," + total_tagnum.ToString();
                    SendMessage(ptrWnd, WM_SENDBUFF, IntPtr.Zero, para);
                }
                ptrWnd = IntPtr.Zero;
            }
        }
        private void ReadProcess()
        {
            fIsBuffScan = true;
            while (!toStopThread)
            {
                GetBuffData();
            }
            fIsBuffScan = false;
        }
        
         ***/

        private void btSetMaxtime_Click(object sender, EventArgs e)
        {
            byte Scantime = 0;
            Scantime = Convert.ToByte(comboBox_maxtime.SelectedIndex);
            fCmdRet = RWDev.SetInventoryScanTime(ref fComAdr, Scantime, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set inventory scan time failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set inventory scan time success ";
            }
        }

        private void panel9_Paint(object sender, PaintEventArgs e)
        {

        }

        private void btSetMmode_Click(object sender, EventArgs e)
        {
            byte ReadMode = 0;

            string temp = text_RDVersion.Text;
            ReadMode = (byte)com_Mmode.SelectedIndex;
            fCmdRet = RWDev.SetReadMode(ref fComAdr, ReadMode, frmcomportindex);

            if (fCmdRet != 0)
            {
                string strLog = "Set read mode failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set read mode success ";
            }
        }

        private void btMSetParameter_Click(object sender, EventArgs e)
        {
            byte[] Parameter = new byte[200];
            byte MaskMem = 0;
            byte[] MaskAdr = new byte[2];
            byte MaskLen = 0;
            byte[] MaskData = new byte[200];
            byte MaskFlag = 0;
            byte AdrTID = 0;
            byte LenTID = 0;
            byte TIDFlag = 0;
            if (MRB_G2.Checked)
            {
                Parameter[0] = 0;
            }
            else
            {
                Parameter[0] = 1;
            }

            Parameter[1] = (byte)COM_MRPTime.SelectedIndex;
            Parameter[2] = (byte)com_MFliterTime.SelectedIndex;
            Parameter[3] = (byte)com_MQ.SelectedIndex;
            Parameter[4] = (byte)com_MS.SelectedIndex;
            if (Parameter[4] > 3) Parameter[4] = 255;
            if (checkBox_mask.Checked)
            {
                if (RBM_EPC.Checked)
                {
                    MaskMem = 1;
                }
                else if (RBM_TID.Checked)
                {
                    MaskMem = 2;
                }
                else if (RBM_USER.Checked)
                {
                    MaskMem = 3;
                }
                if ((txt_Maddr.Text.Length != 4) || (txt_Mlen.Text.Length != 2) || (txt_Mdata.Text.Length % 2 != 0))
                {
                    MessageBox.Show("Mask error!", "information");
                    return;
                }
                MaskAdr = HexStringToByteArray(txt_Maddr.Text);
                int len = Convert.ToInt32(txt_Mlen.Text, 16);
                MaskLen = (byte)len;
                MaskData = HexStringToByteArray(txt_Mdata.Text);
                MaskFlag = 1;
            }
            if (checkBox_tid.Checked)
            {
                AdrTID = Convert.ToByte(txt_mtidaddr.Text, 16);
                LenTID = Convert.ToByte(txt_Mtidlen.Text, 16);
                TIDFlag = 1;
            }
            fCmdRet = RWDev.SetReadParameter(ref fComAdr, Parameter, MaskMem, MaskAdr, MaskLen, MaskData, MaskFlag, AdrTID, LenTID, TIDFlag, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set read parameter failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set read parameter success ";
            }

        }

        private void btMGetParameter_Click(object sender, EventArgs e)
        {
            byte[] Parameter = new byte[200];
            fCmdRet = RWDev.GetReadParameter(ref fComAdr, Parameter, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get read parameter failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                com_Mmode.SelectedIndex = Parameter[0];
                if (Parameter[1] == 0)
                {
                    MRB_G2.Checked = true;
                }
                else
                {
                    MRB_6B.Checked = true;
                }
                COM_MRPTime.SelectedIndex = Parameter[2];
                com_MFliterTime.SelectedIndex = Parameter[3];
                com_MQ.SelectedIndex = Parameter[4];
                if (Parameter[5] == 255)
                    com_MS.SelectedIndex = 4;
                else
                    com_MS.SelectedIndex = Parameter[5];
                if (Parameter[6] == 1)
                {
                    RBM_EPC.Checked = true;
                }
                else if (Parameter[6] == 2)
                {
                    RBM_TID.Checked = true;
                }
                else if (Parameter[6] == 3)
                {
                    RBM_USER.Checked = true;
                }
                byte[] maskaddr = new byte[2];
                Array.Copy(Parameter, 7, maskaddr, 0, 2);
                txt_Maddr.Text = ByteArrayToHexString(maskaddr);
                txt_Mlen.Text = Convert.ToString(Parameter[9], 16).PadLeft(2, '0');
                byte[] data = new byte[32];
                Array.Copy(Parameter, 10, data, 0, 32);
                string temp = ByteArrayToHexString(data);
                int len = Parameter[9];
                if ((len % 8) == 0)
                {
                    len = len / 8;
                }
                else
                {
                    len = len / 8 + 1;
                }
                if (len <= 32)
                {
                    temp = temp.Substring(0, len * 2);
                }
                txt_Mdata.Text = temp;
                txt_mtidaddr.Text = Convert.ToString(Parameter[42], 16).PadLeft(2, '0');
                txt_Mtidlen.Text = Convert.ToString(Parameter[43], 16).PadLeft(2, '0');
                string strLog = "Get read parameter success ";
            }
        }

        private void GetRealtiemeData()
        {
            byte[] ScanModeData = new byte[40960];
            int nLen, NumLen;
            string temp1 = "";
            string binarystr1 = "";
            string binarystr2 = "";
            string RSSI = "";
            string AntStr = "";
            string lenstr = "";
            string EPCStr = "";
            int ValidDatalength;
            string temp;
            ValidDatalength = 0;
            DataGridViewRow rows = new DataGridViewRow();
            int xtime = System.Environment.TickCount;
            fCmdRet = RWDev.ReadActiveModeData(ScanModeData, ref ValidDatalength, frmcomportindex);
            if (fCmdRet == 0)
            {
                try
                {
                    byte[] daw = new byte[ValidDatalength];
                    Array.Copy(ScanModeData, 0, daw, 0, ValidDatalength);
                    temp = ByteArrayToHexString(daw);
                    fInventory_EPC_List = fInventory_EPC_List + temp;//把字符串存进列表
                    nLen = fInventory_EPC_List.Length;
                    while (fInventory_EPC_List.Length > 18)
                    {
                        string FlagStr = Convert.ToString(fComAdr, 16).PadLeft(2, '0') + "EE00";//查找头位置标志字符串
                        int nindex = fInventory_EPC_List.IndexOf(FlagStr);
                        if (nindex > 1)
                            fInventory_EPC_List = fInventory_EPC_List.Substring(nindex - 2);
                        else
                        {
                            fInventory_EPC_List = fInventory_EPC_List.Substring(2);
                            continue;
                        }
                        NumLen = Convert.ToInt32(fInventory_EPC_List.Substring(0, 2), 16) * 2 + 2;//取第一个帧的长度
                        if (fInventory_EPC_List.Length < NumLen)
                        {
                            break;
                        }
                        temp1 = fInventory_EPC_List.Substring(0, NumLen);
                        fInventory_EPC_List = fInventory_EPC_List.Substring(NumLen);
                        if (!CheckCRC(temp1)) continue;
                        AntStr = Convert.ToString(Convert.ToInt32(temp1.Substring(8, 2), 16), 2).PadLeft(4, '0');
                        lenstr = Convert.ToString(Convert.ToInt32(temp1.Substring(10, 2), 16), 10);
                        EPCStr = temp1.Substring(12, temp1.Length - 18);
                        RSSI = temp1.Substring(temp1.Length - 6, 2);
                        
                    }
                }
                catch (System.Exception ex)
                {
                    ex.ToString();
                }
            }
            
        }
        private void timer_RealTime_Tick(object sender, EventArgs e)
        {
            if (fIsInventoryScan) return;
            fIsInventoryScan = true;
            GetRealtiemeData();
            fIsInventoryScan = false;
        }

        private void radioButton_band8_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dmaxfre.Items.Clear();
            ComboBox_dminfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 20; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(840.125 + i * 0.25) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(840.125 + i * 0.25) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(840.125 + i * 0.25));
            }
            ComboBox_dmaxfre.SelectedIndex = 19;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 10;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            byte WritePower = 0;
            WritePower = (byte)(com_wpower.SelectedIndex);
            if (rb_wp1.Checked)
            {
                WritePower = WritePower;
            }
            else
            {
                WritePower = Convert.ToByte(WritePower | 0x80);
            }
            fCmdRet = RWDev.WriteRfPower(ref fComAdr, WritePower, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set failed： " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set success ";
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            byte WritePower = 0;
            fCmdRet = RWDev.ReadRfPower(ref fComAdr, ref WritePower, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                if ((WritePower & 0x80) == 0)
                {
                    rb_wp1.Checked = true;
                    com_wpower.SelectedIndex = Convert.ToInt32(WritePower);
                }
                else
                {
                    com_wpower.SelectedIndex = Convert.ToInt32(WritePower & 0x3F);
                    rb_wp2.Checked = true;
                }
                string strLog = "Get success ";
            }
        }

        private void bt_Setretry_Click(object sender, EventArgs e)
        {
            byte RetryTime = 0;
            RetryTime = (byte)(com_retrytimes.SelectedIndex | 0x80);
            fCmdRet = RWDev.RetryTimes(ref fComAdr, ref RetryTime, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set failed： " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set success ";
            }
        }

        private void bt_Getretry_Click(object sender, EventArgs e)
        {
            byte Times = 0;
            fCmdRet = RWDev.RetryTimes(ref fComAdr, ref Times, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                com_retrytimes.SelectedIndex = Convert.ToInt32(Times);
                string strLog = "Get success ";
            }
        }

        private void bt_setDRM_Click(object sender, EventArgs e)
        {
            byte DRM = 0;
            if (DRM_CLOSE.Checked) DRM = 0;
            if (DRM_OPEN.Checked) DRM = 1;
            fCmdRet = RWDev.SetDRM(ref fComAdr, DRM, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Set DRM failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                string strLog = "Set DRM success ";
            }
        }

        private void bt_GetDRM_Click(object sender, EventArgs e)
        {
            byte DRM = 0;
            fCmdRet = RWDev.GetDRM(ref fComAdr, ref DRM, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get DRM failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                if (DRM == 0) DRM_CLOSE.Checked = true;
                if (DRM == 1) DRM_OPEN.Checked = true;
                string strLog = "Get DRM success ";
            }
        }

        private void check_int2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void btnGetReaderTemperature_Click(object sender, EventArgs e)
        {

            byte PlusMinus = 0;
            byte Temperature = 0;
            string temp = "";
            txtReaderTemperature.Text = "";
            fCmdRet = RWDev.GetReaderTemperature(ref fComAdr, ref PlusMinus, ref Temperature, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get Temperature failed: " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                if (PlusMinus == 0)
                    temp = "-";
                temp += (Temperature.ToString() + "°C");
                txtReaderTemperature.Text = temp;
                string strLog = "Get Temperature success ";
            }
        }

        private void btReturnLoss_Click(object sender, EventArgs e)
        {
            byte[] TestFreq = new byte[4];
            byte Ant = (byte)cbbAnt.SelectedIndex;
            byte ReturnLoss = 0;
            string temp = cmbReturnLossFreq.Text;

            float freq = Convert.ToSingle(Convert.ToSingle(temp) * 1000);
            int freq0 = Convert.ToInt32(freq);
            temp = Convert.ToString(freq0, 16).PadLeft(8, '0');
            TestFreq = HexStringToByteArray(temp);
            textReturnLoss.Text = "";
            fCmdRet = RWDev.MeasureReturnLoss(ref fComAdr, TestFreq, Ant, ref ReturnLoss, frmcomportindex);
            if (fCmdRet != 0)
            {
                string strLog = "Get failed:  " + GetReturnCodeDesc(fCmdRet);
            }
            else
            {
                textReturnLoss.Text = ReturnLoss.ToString() + "dB";
                string strLog = "Get success ";
            }
        }

        private void radioButton_band12_CheckedChanged(object sender, EventArgs e)
        {
            int i;
            ComboBox_dmaxfre.Items.Clear();
            ComboBox_dminfre.Items.Clear();
            cmbReturnLossFreq.Items.Clear();
            for (i = 0; i < 53; i++)
            {
                ComboBox_dminfre.Items.Add(Convert.ToString(902 + i * 0.5) + " MHz");
                ComboBox_dmaxfre.Items.Add(Convert.ToString(902 + i * 0.5) + " MHz");
                cmbReturnLossFreq.Items.Add(Convert.ToString(902 + i * 0.5));
            }
            ComboBox_dmaxfre.SelectedIndex = 52;
            ComboBox_dminfre.SelectedIndex = 0;
            cmbReturnLossFreq.SelectedIndex = 26;
        }

        private void text_RDVersion_TextChanged(object sender, EventArgs e)
        {

        }

        //JW - Function to check if EPC is already on the list

        public static void EPC_Checker(List<string> Raw_Entry_List)
        {
            if(run_epc_checker == true)
            {
                if (inventory_list.Contains(Raw_Entry_List[0]))
                {
                    //wanted signal
                    switch (Raw_Entry_List[3])
                    {
                        case "1000":
                            Raw_Entry_List[3] = "4";
                            break;
                        case "0100":
                            Raw_Entry_List[3] = "3";
                            break;
                        case "0010":
                            Raw_Entry_List[3] = "2";
                            break;
                        case "0001":
                            Raw_Entry_List[3] = "1";
                            break;
                    }
                    if (Full_list.Contains(Raw_Entry_List[0]))
                    {
                        //epc is on the full list
                        int epc_index = Full_list.IndexOf(Raw_Entry_List[0]);
                        Full_list[epc_index + 1] = Raw_Entry_List[1];
                        Full_list[epc_index + 2] = Raw_Entry_List[2];
                        Full_list[epc_index + 3] = Raw_Entry_List[3];
                    }
                    else
                    {
                        //epc is not on the full list
                        Full_list.Add(Raw_Entry_List[0]);
                        Full_list.Add(Raw_Entry_List[1]);
                        Full_list.Add(Raw_Entry_List[2]);
                        Full_list.Add(Raw_Entry_List[3]);
                    }
                }
                else
                {
                    //unwanted signal
                }
            }
        }

        //JW - Periodic Checker on a separate thread
        private void Periodic_Checker()
        {
            while (true)
            {
                //Initiate Connection with Database and obtain all EPC on the database
                Console.WriteLine("Syncing with Database . . . ");
                string connstring = String.Format("Host=localhost;Port=5432;User Id=Admin;Password=password;Database=Inventory;");
                NpgsqlConnection conn = new NpgsqlConnection(connstring);
                NpgsqlDataReader reader;
                try
                {
                    conn.Open();
                    if (conn.State == ConnectionState.Open)
                    {
                        connected = true;
                        var cmd = new NpgsqlCommand("SELECT epc FROM RFID_inventory;", conn);
                        reader = cmd.ExecuteReader();
                        while (reader.Read())
                        {
                            if (inventory_list.Contains(reader.GetString(0).Trim(' ')))
                            {
                            }

                            else
                            {
                                inventory_list.Add(reader.GetString(0).Trim(' '));
                            }
                        }
                        reader.Close();
                        conn.Close();

                        Console.WriteLine("Synced. Thread going to sleep.");

                        //Put thread to sleep, in milliseconds
                        Thread.Sleep(10 * 1000);
                        //disable epc_checker, check if the items have been checked out
                        DateTime time_start = DateTime.Now;
                        run_epc_checker = false;
                        int i = 0, u = 0;
                        int time_diff, minimum_idle_time = 30;
                        bool remove_epc = false;
                        for (i = 0; i < Full_list.Count; i = i + 4)
                        {
                            bool status_error = false;
                            DateTime time_now = DateTime.Now;
                            time_diff = Convert.ToInt32(time_now.Subtract(Convert.ToDateTime(Full_list[i + 2])).TotalSeconds);
                            Console.WriteLine(Full_list[i] + " has a time difference of: " + time_diff);
                            if (time_diff > minimum_idle_time)
                            {
                                //item is considered to be checked out, check the manual entry
                                conn.Open();
                                cmd = new NpgsqlCommand("SELECT * FROM rfid_inventory where epc = '" + Full_list[i] + "';", conn);
                                reader = cmd.ExecuteReader();
                                while (reader.Read())
                                {
                                    if (reader["item_status_manual"].ToString() == "Available")
                                    {
                                        //manual entry indicates that it is available, then there is an error as the antenna is not able to pick up the signal
                                        //it could only mean the item is checked out or hidden in the room and the signal cannot be picked up
                                        status_error = true;
                                        cmd.Cancel();
                                    }
                                }
                                conn.Close();

                                conn.Open();
                                if (status_error)
                                {
                                    cmd = new NpgsqlCommand("UPDATE rfid_inventory SET item_status_automated = 'Error: status do not tally.', tagtime = '" + Full_list[i + 2] + "' WHERE epc = '" + Full_list[i] + "';", conn);
                                    cmd.ExecuteNonQuery();
                                    remove_epc = true;
                                    remove_list.Add(Full_list[i]);
                                }
                                else
                                {
                                    cmd = new NpgsqlCommand("UPDATE rfid_inventory SET item_status_automated = 'Checked Out', tagtime = '" + Full_list[i + 2] + "' WHERE epc = '" + Full_list[i] + "';", conn);
                                    cmd.ExecuteNonQuery();
                                    remove_epc = true;
                                    remove_list.Add(Full_list[i]);
                                }
                                conn.Close();
                            }
                            else
                            {
                                //item is available
                                conn.Open();
                                cmd = new NpgsqlCommand("UPDATE RFID_Inventory SET item_status_automated = 'Available', tagtime = '" + Full_list[i + 2] + "' WHERE epc = '" + Full_list[i] + "';", conn);
                                cmd.ExecuteNonQuery();
                                conn.Close();
                            }
                        }

                        if (remove_epc)
                        {
                            for (i = 0; i < remove_list.Count; i++)
                            {
                                int index_to_remove = Full_list.IndexOf(remove_list[i]);
                                Full_list.RemoveRange(index_to_remove, 4);
                            }
                            remove_list.Clear();
                        }
                        remove_epc = false;
                        DateTime time_end = DateTime.Now;
                        int runtime_ms = Convert.ToInt32(time_end.Subtract(time_start).TotalMilliseconds);
                        Console.WriteLine("Program ran for: " + runtime_ms + " milliseconds");
                        Console.WriteLine("Printing Full List:");
                        Console.WriteLine("Full List has a total number of: " + Full_list.Count);
                        Console.WriteLine("-------------------");
                        for (i = 0; i < Full_list.Count; i += 4)
                        {
                            Console.WriteLine(Full_list[i] + " " + Full_list[i + 1] + " " + Full_list[i + 2] + " " + Full_list[i + 3]);
                        }
                        run_epc_checker = true;
                    }
                    else
                    {
                        connected = false;
                        Console.WriteLine("Disconnected");
                    }
                }
                catch(Exception e)
                {   
                    connected = false;
                }
                
            }
        }

        private DataSet dataset_query = new DataSet();
        private DataTable datatable_query = new DataTable();

        private void BN_Open_FullDB_Click(object sender, EventArgs e)
        {
            
            string connstring = String.Format("Host=localhost;Port=5432;User Id=Admin;Password=password;Database=Inventory;");
            NpgsqlConnection conn = new NpgsqlConnection(connstring);
            conn.Open();
            string command = "SELECT * FROM rfid_inventory;";
            NpgsqlDataAdapter dataadapter_query = new NpgsqlDataAdapter(command, conn);
            dataset_query.Reset();
            dataadapter_query.Fill(dataset_query);
            datatable_query = dataset_query.Tables[0];
            View_InvList.DataSource = datatable_query;
            conn.Close();
            
        }

        private void BN_Search_Click(object sender, EventArgs e)
        {
            string epc_search = textBox1.Text;
            string sn_search = textBox2.Text;
            string connstring = String.Format("Host=localhost;Port=5432;User Id=Admin;Password=password;Database=Inventory;");
            NpgsqlConnection conn = new NpgsqlConnection(connstring);
            conn.Open();

            if (epc_search == null)
            {
                string command = "SELECT * FROM rfid_inventory WHERE item_sn = '" + sn_search + "';";
                NpgsqlDataAdapter dataadapter_query = new NpgsqlDataAdapter(command, conn);
                dataset_query.Reset();
                dataadapter_query.Fill(dataset_query);
                datatable_query = dataset_query.Tables[0];
                View_InvList.DataSource = datatable_query;
                conn.Close();
            }

            else if(sn_search == null)
            {
                string command = "SELECT * FROM rfid_inventory WHERE epc = '" + epc_search + "';";
                NpgsqlDataAdapter dataadapter_query = new NpgsqlDataAdapter(command, conn);
                dataset_query.Reset();
                dataadapter_query.Fill(dataset_query);
                datatable_query = dataset_query.Tables[0];
                View_InvList.DataSource = datatable_query;
                conn.Close();
            }

            else
            {
                string command = "SELECT * FROM rfid_inventory WHERE item_sn = '" + sn_search + "' AND epc = '" + epc_search + "';";
                NpgsqlDataAdapter dataadapter_query = new NpgsqlDataAdapter(command, conn);
                dataset_query.Reset();
                dataadapter_query.Fill(dataset_query);
                datatable_query = dataset_query.Tables[0];
                View_InvList.DataSource = datatable_query;
                conn.Close();
            }
        }


        private void status_checker_Tick(object sender, EventArgs e)
        {
            if (connected)
            {
                tb_db_connection.BackColor = Color.Green;
                tb_db_connection.Text = "Connected";
            }
            if (!connected)
            {
                tb_db_connection.BackColor = Color.Red;
                tb_db_connection.Text = "Disconnected";
            }
            if (hw_connected)
            {
                tb_hw_connected.BackColor = Color.Green;
                tb_hw_connected.Text = "Connected";
            }
            if (!hw_connected)
            {
                tb_hw_connected.BackColor = Color.Red;
                tb_hw_connected.Text = "Disconnected";
            }

            if(startup)
            {
                tb_scanning.BackColor = Color.Red;
                tb_scanning.Text = "Disconnected";
            }

            else if (!startup)
            {
                if (toStopThread)
                {
                    tb_scanning.BackColor = Color.Red;
                    tb_scanning.Text = "Disconnected";
                }

                if (!toStopThread)
                {
                    tb_scanning.BackColor = Color.Green;
                    tb_scanning.Text = "Operational";
                }
            }
        }

        private void alarm_checker_Tick(object sender, EventArgs e)
        {
            Console.WriteLine("Syncing with Database . . . ");
            string connstring = String.Format("Host=localhost;Port=5432;User Id=Admin;Password=password;Database=Inventory;");
            NpgsqlConnection conn = new NpgsqlConnection(connstring);
            conn.Open();
            var cmd = new NpgsqlCommand("SELECT COUNT(*) from rfid_inventory WHERE item_status_automated = 'Error: status do not tally.';", conn);
            int alarm_no = Convert.ToInt32(cmd.ExecuteScalar());
            conn.Close();
            lbl_alarm.Text = alarm_no.ToString();
            if (alarm_no > 0)
            {
                lbl_alarm.BackColor = Color.Red;
            }
            else
            {
                lbl_alarm.BackColor = Color.LightGreen;
            }
            
        }
        
        private void lbl_alarm_Click(object sender, EventArgs e)
        {
            Inventory_Monitoring.Form2 f2 = new Inventory_Monitoring.Form2();
            f2.ShowDialog();
        }
    }
}
