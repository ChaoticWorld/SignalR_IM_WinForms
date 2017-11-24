using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.NetworkInformation;

namespace CommModel {
    public class IMCommModel {
        public string ConnId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string ComputerName { get; set; } = string.Empty;
        public string ipAddress { get; set; } = string.Empty;
        public string macAddress { get; set; } = string.Empty;
        public bool isService { get; set; } = false;
    }

    /// <summary>
    /// 通讯参数类
    /// dataType类型的数据
    /// msgType=Text -- dataType=>string,data=消息文本
    /// msgType=LineState -- dataType=>bool,data=bool?上线:离线
    /// msgType=Command -- dataType=>data.GetType(),data={命令，参数，数据}
    /// </summary>
    public class IMCommParam<T> {
        /// <summary>
        /// dataType类型的数据
        /// msgType=Text -- dataType=>string,data=消息文本
        /// msgType=LineState -- dataType=>IMModel,isSuccess=bool?上线:离线,data=IMModel
        /// msgType=Command -- dataType=>data.GetType(),data={命令，参数，数据}
        /// </summary>
        public T data { get; set; } 
        public List<IMCommModel> imModels { get; set; }
        public string fromConnId { get; set; }
        public string toConnId { get; set; } = string.Empty;
        public DateTime transTime { get; set; } = DateTime.Now;
        public bool isSuccess { get; set; }
        public msgType msgType { get; set; }
    }

    public class IMCommParamBase {
        public string fromConnId { get; set; }
        public string toConnId { get; set; } = string.Empty;
        public DateTime transTime { get; set; } = DateTime.Now;
        public bool isSuccess { get; set; }
        public msgType msgType { get; set; }
    }
    public class IMCommParamLineState : IMCommParamBase {
        public List<IMCommModel> IMModels { get; set; } = new List<IMCommModel>();
        public int Count { get { return IMModels.Count; } }
        public IMCommModel IMModel { get; set; }
    }
    public class IMCommParamMessage : IMCommParamBase{
        public string message { get; set; }
    }
    public class IMCommParamCommand<T> : IMCommParamBase  {
        public T data { get; set; }
    }

    public enum msgType {
        Text = 0,
        Command = 1,
        LineState = 2
    }
    
}
