﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using HPSocketCS;
using System.Runtime.InteropServices;
using System.IO;
using System.Threading;

namespace SSLPackClientNS
{
    public enum AppState
    {
        Starting, Started, Stoping, Stoped, Error
    }

    public enum StudentType
    {
        None, Array, List, Single,
    }

    public partial class frmClient : Form
    {
        private AppState appState = AppState.Stoped;

        private delegate void ConnectUpdateUiDelegate();
        private delegate void SetAppStateDelegate(AppState state);
        private delegate void ShowMsg(string msg);
        private ShowMsg AddMsgDelegate;

        // 两种构造方式,第一种
        //HPSocketCS.SSLPackClient client = null;
        // 两种构造方式,第二种
        HPSocketCS.SSLPackClient client = new HPSocketCS.SSLPackClient(SSLVerifyMode.Peer | SSLVerifyMode.FailIfNoPeerCert, "ssl-cert\\server.cer", "ssl-cert\\server.key", "123456", "ssl-cert\\ca.crt");

        public frmClient()
        {
            InitializeComponent();
        }

        private void frmClient_Load(object sender, EventArgs e)
        {
            try
            {
                // 初始化ssl环境
                // 初始化ssl环境
                if (!client.Initialize())
                {
                    SetAppState(AppState.Error);
                    AddMsg("初始化ssl环境失败：" + Sdk.SYS_GetLastError());
                    return;
                }

                // 加个委托显示msg,因为on系列都是在工作线程中调用的,ui不允许直接操作
                AddMsgDelegate = new ShowMsg(AddMsg);

                // 设置client事件
                client.OnPrepareConnect += new ClientEvent.OnPrepareConnectEventHandler(OnPrepareConnect);
                client.OnConnect += new ClientEvent.OnConnectEventHandler(OnConnect);
                client.OnSend += new ClientEvent.OnSendEventHandler(OnSend);
                client.OnReceive += new ClientEvent.OnReceiveEventHandler(OnReceive);
                client.OnClose += new ClientEvent.OnCloseEventHandler(OnClose);

                client.OnHandShake += new ClientEvent.OnHandShakeEventHandler(OnHandShake);

                // 设置包头标识,与对端设置保证一致性
                client.PackHeaderFlag = 0xff;
                // 设置最大封包大小
                client.MaxPackSize = 0x1000;

                SetAppState(AppState.Stoped);
            }
            catch (Exception ex)
            {
                SetAppState(AppState.Error);
                AddMsg(ex.Message);
            }
        }

        private void btnStart_Click(object sender, EventArgs e)
        {
            try
            {
                String ip = this.txtIpAddress.Text.Trim();
                ushort port = ushort.Parse(this.txtPort.Text.Trim());

                // 写在这个位置是上面可能会异常
                SetAppState(AppState.Starting);

                AddMsg(string.Format("$Client Starting ... -> ({0}:{1})", ip, port));

                if (client.Connect(ip, port, this.cbxAsyncConn.Checked))
                {
                    if (cbxAsyncConn.Checked == false)
                    {
                        SetAppState(AppState.Started);
                    }

                    AddMsg(string.Format("$Client Start OK -> ({0}:{1})", ip, port));
                }
                else
                {
                    SetAppState(AppState.Stoped);
                    throw new Exception(string.Format("$Client Start Error -> {0}({1})", client.ErrorMessage, client.ErrorCode));
                }
            }
            catch (Exception ex)
            {
                AddMsg(ex.Message);
            }
        }

        private void btnStop_Click(object sender, EventArgs e)
        {

            // 停止服务
            AddMsg("$Server Stop");
            if (client.Stop())
            {
                SetAppState(AppState.Stoped);
            }
            else
            {
                AddMsg(string.Format("$Stop Error -> {0}({1})", client.ErrorMessage, client.ErrorCode));
            }
        }

        private void btnSend_Click(object sender, EventArgs e)
        {
            try
            {
                string send = this.txtSend.Text;
                if (send.Length == 0)
                {
                    return;
                }

                byte[] bytes = Encoding.Default.GetBytes(send);
                IntPtr connId = client.ConnectionId;

                // 发送
                if (client.Send(bytes, bytes.Length))
                {
                    AddMsg(string.Format("$ ({0}) Send OK --> {1}", connId, send));
                }
                else
                {
                    AddMsg(string.Format("$ ({0}) Send Fail --> {1} ({2})", connId, send, bytes.Length));
                }

            }
            catch (Exception ex)
            {
                AddMsg(string.Format("$ Send Fail -->  msg ({0})", ex.Message));
            }

        }

        private void lbxMsg_KeyPress(object sender, KeyPressEventArgs e)
        {

            // 清理listbox
            if (e.KeyChar == 'c' || e.KeyChar == 'C')
            {
                this.lbxMsg.Items.Clear();
            }
        }

        void ConnectUpdateUi()
        {
            if (this.cbxAsyncConn.Checked == true)
            {
                SetAppState(AppState.Started);
            }
        }

        HandleResult OnPrepareConnect(IClient sender, IntPtr socket)
        {
            return HandleResult.Ok;
        }

        HandleResult OnConnect(IClient sender)
        {
            // 已连接 到达一次

            // 如果是异步联接,更新界面状态
            this.Invoke(new ConnectUpdateUiDelegate(ConnectUpdateUi));

            AddMsg(string.Format(" > [{0},OnConnect]", client.ConnectionId));

            return HandleResult.Ok;
        }

        HandleResult OnSend(IClient sender, byte[] bytes)
        {
            // 客户端发数据了
            AddMsg(string.Format(" > [{0},OnSend] -> ({1} bytes)", client.ConnectionId, bytes.Length));

            return HandleResult.Ok;
        }

        HandleResult OnReceive(IClient sender, byte[] bytes)
        {
            // 数据到达了

            AddMsg(string.Format(" > [{0},OnReceive] -> ({1} bytes)", client.ConnectionId, bytes.Length));

            return HandleResult.Ok;
        }

        HandleResult OnClose(IClient sender, SocketOperation enOperation, int errorCode)
        {
            if (errorCode == 0)
                // 连接关闭了
                AddMsg(string.Format(" > [{0},OnClose]", client.ConnectionId));
            else
                // 出错了
                AddMsg(string.Format(" > [{0},OnError] -> OP:{1},CODE:{2}", client.ConnectionId, enOperation, errorCode));

            // 通知界面,只处理了连接错误,也没进行是不是连接错误的判断,所以有错误就会设置界面
            // 生产环境请自己控制
            this.Invoke(new SetAppStateDelegate(SetAppState), AppState.Stoped);

            return HandleResult.Ok;
        }


        HandleResult OnHandShake(IClient sender)
        {
            // 握手了
            AddMsg(string.Format(" > [{0},OnHandShake])", client.ConnectionId));

            return HandleResult.Ok;
        }
        /// <summary>
        /// 设置程序状态
        /// </summary>
        /// <param name="state"></param>
        void SetAppState(AppState state)
        {
            appState = state;
            this.btnStart.Enabled = (appState == AppState.Stoped);
            this.btnStop.Enabled = (appState == AppState.Started);
            this.txtIpAddress.Enabled = (appState == AppState.Stoped);
            this.txtPort.Enabled = (appState == AppState.Stoped);
            this.cbxAsyncConn.Enabled = (appState == AppState.Stoped);
            this.btnSend.Enabled = (appState == AppState.Started);
        }

        /// <summary>
        /// 往listbox加一条项目
        /// </summary>
        /// <param name="msg"></param>
        void AddMsg(string msg)
        {
            if (this.lbxMsg.InvokeRequired)
            {
                // 很帅的调自己
                this.lbxMsg.Invoke(AddMsgDelegate, msg);
            }
            else
            {
                if (this.lbxMsg.Items.Count > 100)
                {
                    this.lbxMsg.Items.RemoveAt(0);
                }
                this.lbxMsg.Items.Add(msg);
                this.lbxMsg.TopIndex = this.lbxMsg.Items.Count - (int)(this.lbxMsg.Height / this.lbxMsg.ItemHeight);
            }
        }

        private void frmClient_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (client != null)
            {
                // 反初始化ssl环境
                client.UnInitialize();

                client.Destroy();

            }
        }

    }
}
