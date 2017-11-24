using Microsoft.AspNet.SignalR;
using Microsoft.Owin.Cors;
using Microsoft.Owin.Hosting;
using Owin;
using System;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
//using OrganizationalManager;
//using OrganizationalManager.Model;
using Newtonsoft.Json;
using System.Linq;
using System.Collections.Generic;
using CommModel;
using Dos.Common;

namespace CommServer {
    /// <summary>
    /// WinForms host for a SignalR server. The host can stop and start the SignalR
    /// server, report errors when trying to start the server on a URI where a
    /// server is already being hosted, and monitor when clients connect and disconnect. 
    /// The hub used in this server is a simple echo service, and has the same 
    /// functionality as the other hubs in the SignalR Getting Started tutorials.
    /// </summary>
    public partial class WinFormsServer : Form {
        private IDisposable SignalR { get; set; }
        const string ServerURI = "http://localhost:8063";
        public List<IMCommModel> Clients { get; set; }
        public List<IMCommModel> Services { get; set; }
        internal WinFormsServer() {
            InitializeComponent();
            Clients = new List<IMCommModel>();
            Services = new List<IMCommModel>();
            ButtonStart_Click(this, EventArgs.Empty);
        }

        /// <summary>
        /// Calls the StartServer method with Task.Run to not
        /// block the UI thread. 
        /// </summary>
        private void ButtonStart_Click(object sender, EventArgs e) {
            WriteToConsole("Starting server...");
            ButtonStart.Enabled = false;
            Task.Run(() => StartServer());
        }

        /// <summary>
        /// Stops the server and closes the form. Restart functionality omitted
        /// for clarity.
        /// </summary>
        private void ButtonStop_Click(object sender, EventArgs e) {
            //SignalR will be disposed in the FormClosing event
            Close();
        }

        /// <summary>
        /// Starts the server and checks for error thrown when another server is already 
        /// running. This method is called asynchronously from Button_Start.
        /// </summary>
        private void StartServer() {
            try {
                SignalR = WebApp.Start(ServerURI);
            } catch (TargetInvocationException) {
                WriteToConsole("Server failed to start. A server is already running on " + ServerURI);
                //Re-enable button to let user try to start server again
                this.Invoke((Action)(() => ButtonStart.Enabled = true));
                return;
            }
            this.Invoke((Action)(() => ButtonStop.Enabled = true));
            WriteToConsole("Server started at " + ServerURI);
        }
        /// <summary>
        /// This method adds a line to the RichTextBoxConsole control, using Invoke if used
        /// from a SignalR hub thread rather than the UI thread.
        /// </summary>
        /// <param name="message"></param>
        internal void WriteToConsole(String message) {
            if (RichTextBoxConsole.InvokeRequired) {
                this.Invoke((Action)(() =>
                    WriteToConsole(message)
                ));
                return;
            }
            RichTextBoxConsole.AppendText(message + Environment.NewLine);
        }

        private void WinFormsServer_FormClosing(object sender, FormClosingEventArgs e) {

            if (SignalR != null) {
                SignalR.Dispose();
            }
            Clients.Clear();
            Services.Clear();
        }
    }
    /// <summary>
    /// Used by OWIN's startup process. 
    /// </summary>
    class Startup {
        public void Configuration(IAppBuilder app) {
            app.UseCors(CorsOptions.AllowAll);
            app.MapSignalR();
        }
    }
    /// <summary>
    /// Echoes messages sent using the Send message by calling the
    /// addMessage method on the client. Also reports to the console
    /// when clients connect and disconnect.
    /// </summary>
    public class MyHub : Hub {
        private IMCommParamLineState imcp;
        private IMCommModel imModel;
        private static string jsonStr = string.Empty;
        public void SendMessage(string msgData) {
            var msg = JSON.ToObject<IMCommParamMessage>(msgData);
            LogHelper.Debug(string.Format("from {0} to {1} \n Contact: {2}", msg.fromConnId, msg.toConnId, msg.message));
            Program.MainForm.WriteToConsole(string.Format("Request Text: from {0} to {1} \n Contact: {2}", msg.fromConnId, string.IsNullOrEmpty(msg.message) ? "All" : msg.toConnId, msg.message));
            if (string.IsNullOrEmpty(msg.toConnId))
                Clients.All.addMessage(msgData);
            else
                Clients.Client(msg.toConnId).addMessage(msgData);
        }
        public override Task OnConnected() {
            imModel = JSON.ToObject<IMCommModel>(Context.QueryString.Get("Param"));
            //此处应该有认证
            imModel.ConnId = Context.ConnectionId;
            Program.MainForm.WriteToConsole(string.Format("{0} connected: {1} ,  IPAddr: {2} \n Count: {3}", imModel.isService ? "Service" : "Client", Context.ConnectionId, imModel.ipAddress.ToString(), imModel.isService ? Program.MainForm.Services.Count : Program.MainForm.Clients.Count));

            if (imModel.isService) {
                if (!Program.MainForm.Services.Exists(e => e.ConnId == imModel.ConnId))
                    Program.MainForm.Services.Add(imModel);
                Groups.Add(imModel.ConnId, "Service");
                imcp = new IMCommParamLineState() { IMModel = imModel, msgType = msgType.LineState, fromConnId = imModel.ConnId, isSuccess = true };
                jsonStr = JsonConvert.SerializeObject(imcp);
                Clients.Group("Client", new string[0]).addMessage(jsonStr);
                Clients.Group("Service", new string[0]).addMessage(jsonStr);
            } else {
                if (!Program.MainForm.Clients.Exists(e => e.ConnId == imModel.ConnId))
                    Program.MainForm.Clients.Add(imModel);
                Groups.Add(imModel.ConnId, "Client");
                imcp = new IMCommParamLineState() { IMModel = imModel, msgType = msgType.LineState, fromConnId = imModel.ConnId, isSuccess = true };
                jsonStr = JsonConvert.SerializeObject(imcp);
                Clients.Group("Service", new string[0]).addMessage(jsonStr);
                Clients.Group("Client", new string[0]).addMessage(jsonStr);
            }
            LogHelper.Debug("Client connected: " + JsonConvert.SerializeObject(imModel));
            return base.OnConnected();
        }
        public override Task OnDisconnected() {
            var model = Program.MainForm.Services.Union(Program.MainForm.Clients).First(f=>f.ConnId == Context.ConnectionId);
            Program.MainForm.WriteToConsole(string.Format("{0} disconnected: {1} , Count {2} ", model.isService ? "Service" : "Client", Context.ConnectionId, model.isService ? Program.MainForm.Services.Count : Program.MainForm.Clients.Count));
             
            if (Program.MainForm.Clients.Exists(e => e.ConnId == Context.ConnectionId)) {
                imModel = Program.MainForm.Clients.Find(f => f.ConnId == Context.ConnectionId);
                Program.MainForm.Clients.Remove(imModel);
                Groups.Remove(imModel.ConnId, "Client");
                imcp = new IMCommParamLineState() { IMModel = imModel , msgType = msgType.LineState, fromConnId = imModel.ConnId, isSuccess = false };
                jsonStr = JsonConvert.SerializeObject(imcp);
                Clients.Group("Service", new string[0]).addMessage(jsonStr);
                Clients.Group("Client", new string[0]).addMessage(jsonStr);
            } else if (Program.MainForm.Services.Exists(e => e.ConnId == Context.ConnectionId)) {
                imModel = Program.MainForm.Services.Find(f => f.ConnId == Context.ConnectionId);
                Program.MainForm.Services.Remove(imModel);
                Groups.Remove(imModel.ConnId, "Service");
                imcp = new IMCommParamLineState() { IMModel = imModel, msgType = msgType.LineState, fromConnId = imModel.ConnId, isSuccess = false };
                jsonStr = JsonConvert.SerializeObject(imcp);
                Clients.Group("Client", new string[0]).addMessage(jsonStr);
                Clients.Group("Service", new string[0]).addMessage(jsonStr);
            }
            LogHelper.Debug("Client disconnected: " + JsonConvert.SerializeObject(imModel));

            return base.OnDisconnected();
        }
        public void GetOnlineClients() {
            imcp = new IMCommParamLineState() { msgType = msgType.LineState, isSuccess = true, IMModels = Program.MainForm.Services };
            jsonStr = JsonConvert.SerializeObject(imcp);
            Clients.Caller.addMessage(jsonStr);
        }
        public void GetOnlineServices() {
            imcp = new IMCommParamLineState() { msgType = msgType.LineState, isSuccess = true, IMModels = Program.MainForm.Clients };
            jsonStr = JsonConvert.SerializeObject(imcp);
            Clients.Caller.addMessage(jsonStr);
        }
    }
}
