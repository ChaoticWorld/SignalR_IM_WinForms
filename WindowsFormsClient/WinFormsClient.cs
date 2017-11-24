using Microsoft.AspNet.SignalR.Client;
using System;
using System.Net.Http;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json;
using CommModel;
using System.Configuration;
using System.Linq;

namespace CommClient {
    /// <summary>
    /// SignalR client hosted in a WinForms application. The client
    /// lets the user pick a user name, connect to the server asynchronously
    /// to not block the UI thread, and send chat messages to all connected 
    /// clients whether they are hosted in WinForms, WPF, or a web application.
    /// </summary>
    public partial class WinFormsClient : Form {
        /// <summary>
        /// This name is simply added to sent messages to identify the user; this 
        /// sample does not include authentication.
        /// </summary>
        private static IMCommModel imModel;
        private IHubProxy HubProxy { get; set; }
        const string ServerURI = "http://localhost:8063/signalr";
        private static List<IMCommModel> Services;
        private static List<IMCommModel> Clients;
        private IMCommParamBase imcp;
        private HubConnection Connection { get; set; }

        internal WinFormsClient() {
            InitializeComponent();
            var mode = ConfigurationManager.AppSettings["Mode"];
            imModel = new IMCommModel() {
                ComputerName = GetLocalSysInfo.GetComputerName(),
                Name = GetLocalSysInfo.GetLoginUserName(),
                ipAddress = GetLocalSysInfo.GetIPAddress(),
                macAddress = GetLocalSysInfo.GetMacAddress(),
                isService = (mode == "Service")
            };
            Services = new List<IMCommModel>();
            Clients = new List<IMCommModel>();
            Text = string.Format("WinForms Comm {0}", imModel.isService ? "Service" : "Client");
        }

        private void ButtonSend_Click(object sender, EventArgs e) {
            var toId = (string)lblModel.Tag;
            toId = (toId == "ALL") ? string.Empty : toId;
            imcp = new IMCommParamMessage() { msgType = msgType.Text, fromConnId = imModel.ConnId, toConnId=toId, message = TextBoxMessage.Text, isSuccess = true };
            HubProxy.Invoke("SendMessage", JsonConvert.SerializeObject(imcp));
            TextBoxMessage.Text = String.Empty;
            TextBoxMessage.Focus();
        }

        /// <summary>
        /// Creates and connects the hub connection and hub proxy. This method
        /// is called asynchronously from SignInButton_Click.
        /// </summary>
        private async void ConnectAsync() {
            Dictionary<string, string> hubConnParams = new Dictionary<string, string> {
                { "Param", JsonConvert.SerializeObject(imModel) }
            };
            Connection = new HubConnection(ServerURI, hubConnParams);
            Connection.Closed += Connection_Closed;
            Connection.Reconnected += Connection_Succeed;
            
            HubProxy = Connection.CreateHubProxy("MyHub");
            //Handle incoming event from server: use Invoke to write to console from SignalR's thread
            //HubProxy.On<string, string>("AddMessage", (ServiceName, Data) =>
            //    this.Invoke((Action)(() => {
            //        RichTextBoxConsole.AppendText(String.Format("Service - {0}:\n {1}" + Environment.NewLine, ServiceName, Data));
            //    }))
            //);
            HubProxy.On<string>("AddMessage", doMessage);
            try {
                await Connection.Start();
                imModel.ConnId = Connection.ConnectionId;
                ConnectionStatus.Text = "服务器连接成功！" + imModel.ConnId;
                btnConnSrv.Text = "重新连接";
                Connection_Succeed();
            } catch (HttpRequestException e) {
                ConnectionStatus.Text = "服务器连接失败：" + e.Message;
                //No connection: Don't enable Send button or show chat UI
                btnConnSrv.Enabled = true;
                return;
            }

            //Activate UI
            ButtonSend.Enabled = true;
            TextBoxMessage.Focus();
            RichTextBoxConsole.AppendText("Connected to server at " + ServerURI + Environment.NewLine);
        }

        private void doMessage(string data) {
            dynamic imcpData;
            imcp = JsonConvert.DeserializeObject<IMCommParamBase>(data);
            switch (imcp.msgType) {
                case msgType.Text:
                    imcpData = JsonConvert.DeserializeObject<IMCommParamMessage>(data);
                    var immodel = Services.Union(Clients).First(f => f.ConnId == imcp.fromConnId);
                    this.Invoke((Action)(() => {
                        RichTextBoxConsole.AppendText(String.Format("{0} - {1}:\n {2}" + Environment.NewLine, immodel.isService ? "Service" : "Client", immodel.Name, imcpData.message));
                    }));
                    break;
                case msgType.LineState:
                    imcpData = JsonConvert.DeserializeObject<IMCommParamLineState>(data);
                    if (imcp.isSuccess) {
                        if (imcpData.Count > 0) {//中断确定类型是否为List<T>
                            foreach (var d in (List<IMCommModel>)imcpData.IMModels)
                                if (d.isService) {
                                    if (!Services.Exists(e => e.ConnId == d.ConnId)) {
                                        Services.Add(d);
                                        treeViewFlush(treeView1, imcp.isSuccess, d);
                                    }
                                } else {
                                    if (!Clients.Exists(e => e.ConnId == d.ConnId)) {
                                        Clients.Add(d);
                                        treeViewFlush(treeView2, imcp.isSuccess, d);
                                    }
                                }
                        } else {
                            var cm = (IMCommModel)imcpData.IMModel;
                            if (cm == null) return;
                            if (cm.isService) {
                                if (!Services.Exists(e => e.ConnId == cm.ConnId)) {
                                    Services.Add(cm);
                                    treeViewFlush(treeView1, imcp.isSuccess, cm);
                                }
                            } else {
                                if (!Clients.Exists(e => e.ConnId == cm.ConnId)) {
                                    Clients.Add(cm);
                                    treeViewFlush(treeView2, imcp.isSuccess, cm);
                                }
                            }
                        }
                    } else {
                        var cm = (IMCommModel)imcpData.IMModel;
                        if (cm == null) return;
                        if (cm.isService) {
                            Services.Remove(Services.First(f => f.ConnId == imcp.fromConnId));
                            treeViewFlush(treeView1, imcp.isSuccess, cm);
                        } else {
                            Clients.Remove(Clients.First(f => f.ConnId == imcp.fromConnId));
                            treeViewFlush(treeView2, imcp.isSuccess, cm);
                        }
                    }
                    break;
                case msgType.Command:
                    //反射
                    break;
            }
        }

        private void treeViewFlush(TreeView tv, bool isOnline, IMCommModel imcm) {
            if (isOnline)
                this.Invoke((Action)(() => {
                    TreeNode tn = new TreeNode() { Name = imcm.ConnId, Text = imcm.Name, ToolTipText = imcm.ConnId };
                    tv.Nodes.Add(tn);
                }));
            else
                this.Invoke((Action)(() => { tv.Nodes.RemoveByKey(imcm.ConnId); }));
        }

        //#region 获得组织结构 Modefing...
        //private void setGroups(string jsonString) {
        //    //MessageBox.Show(jsonString);
        //    List<GroupModel> Groups = JsonConvert.DeserializeObject<List<GroupModel>>(jsonString);
        //    this.Invoke((Action)(() => {
        //        RichTextBoxConsole.AppendText(string.Format("{0}: {1}" + Environment.NewLine, "Server-Groups", Groups.Count));
        //        //生成MD5,与jsonString 保存到配置文件

        //        treeView1.Nodes.Clear();
        //        TreeNode tn = json2TreeNode(jsonString);
        //        foreach (TreeNode n in tn.Nodes)
        //            treeView1.Nodes.Add(n);

        //    }));
        //}
        //private TreeNode json2TreeNode(string jsonstring) {
        //    OrganizationalNode tn = JsonConvert.DeserializeObject<OrganizationalNode>(jsonstring);
        //    var user = tn.Tag.GetType().Equals(typeof(UserModel)) ? tn.Tag as UserModel : null;
        //    TreeNode returnNode = new TreeNode() {
        //        Name = tn.Id.ToString() + tn.Name,
        //        Text = tn.Name,
        //        ToolTipText = tn.Title,
        //        Tag = tn.Tag,
        //        StateImageKey = (user == null ? string.Empty : user.conntionId),
        //        ForeColor = (user == null ? System.Drawing.Color.Black : System.Drawing.Color.Green)
        //    };

        //    if (!(tn.Nodes is null) && tn.Nodes.Count > 0) {
        //        foreach(OrganizationalNode on in tn.Nodes)
        //        returnNode.Nodes.Add(getNode(on));
        //    }

        //    return returnNode;
        //}
        //private TreeNode getNode(OrganizationalNode on) {
        //    var user = on.Tag.GetType().Equals(typeof(UserModel)) ? on.Tag as UserModel : null;
        //    TreeNode returnNode = new TreeNode() {
        //        Name = on.Id.ToString() + on.Name,
        //        Text = on.Name,
        //        ToolTipText = on.Title,
        //        Tag = on.Tag,
        //        StateImageKey = (user == null ? string.Empty : user.conntionId),
        //        ForeColor = (user == null ? System.Drawing.Color.Black : System.Drawing.Color.Green)
        //    };
        //    if (!(on.Nodes is null) && on.Nodes.Count > 0) {
        //        foreach (OrganizationalNode on1 in on.Nodes)
        //            returnNode.Nodes.Add(getNode(on1));
        //    }
        //    return returnNode;
        //}
        //#endregion

        //private void setUserState(string jsonstring) {
        //    UserModel user = JsonConvert.DeserializeObject<UserModel>(jsonstring);
        //    var userNodes = treeView1.Nodes.Find(user.userId.ToString() + user.userName, true);
        //    if (userNodes != null && userNodes.Length > 0) {
        //        var userNode = userNodes[0];
        //        userNode.ForeColor = System.Drawing.Color.Green;
        //        userNode.StateImageKey = user.conntionId;
        //    }
        //}

        private void Connection_Succeed() {
            imModel.ConnId = Connection.ConnectionId;
            this.Invoke((Action)(() => {
                treeView1.Nodes.Clear();
                treeView2.Nodes.Clear();
                treeView1.Nodes.Add("ALL", "ALL");
                treeView2.Nodes.Add("ALL", "ALL");
            }));
            Services.Clear();
            Clients.Clear();
            HubProxy.Invoke("GetOnlineServices");
            HubProxy.Invoke("GetOnlineClients");
        }

        /// <summary>
        /// If the server is stopped, the connection will time out after 30 seconds (default), and the 
        /// Closed event will fire.
        /// </summary>
        private void Connection_Closed() {
            imModel.ConnId = null;
            //Deactivate chat UI; show login UI. 
            this.Invoke((Action)(() => {
                RichTextBoxConsole.AppendText("Disconnected to server at " + DateTime.Now.ToLocalTime() + Environment.NewLine);
                ConnectionStatus.Text = "通信连接关闭！";
                ButtonSend.Enabled = false;
            }));
        }

        private void WinFormsClient_FormClosing(object sender, FormClosingEventArgs e) {
            if (Connection != null) {
                Connection.Stop();
                Connection.Dispose();
            }
        }

        private void WinFormsClient_Load(object sender, EventArgs e) {
            //UserName = Environment.UserName;
            ConnectAsync();
        }

        private void btnConnSrv_Click(object sender, EventArgs e) {
            switch (Connection.State) {
                case ConnectionState.Connected:
                    Connection.Stop();
                    btnConnSrv_Click(null, EventArgs.Empty);
                    break;
                case ConnectionState.Connecting:
                case ConnectionState.Reconnecting:
                    btnConnSrv_Click(null, EventArgs.Empty);
                    break;
                case ConnectionState.Disconnected:
                    ConnectAsync();
                    break;
            }
        }

        private void RichTextBoxConsole_LinkClicked(object sender, LinkClickedEventArgs e) {
            System.Diagnostics.Process.Start(e.LinkText);
        }

        private void treeView1_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            RichTextBoxConsole.AppendText("Service " + e.Node.Name + " Clicked by " + DateTime.Now.ToLocalTime() + Environment.NewLine);
            lblModel.Tag = e.Node.Name;
            lblModel.Text = e.Node.Text;
        }

        private void treeView2_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e) {
            RichTextBoxConsole.AppendText("Client " + e.Node.Name + " Clicked by " + DateTime.Now.ToLocalTime() + Environment.NewLine);
            lblModel.Tag = e.Node.Name;
            lblModel.Text = e.Node.Text;
        }
    }
}
