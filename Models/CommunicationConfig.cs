using System;

namespace VisionInspection.Models
{
    /// <summary>
    /// TCP 通讯配置
    /// </summary>
    public class TcpConfig
    {
        /// <summary>角色：Server 或 Client</summary>
        public TcpRole Role { get; set; }

        /// <summary>IP 地址（Server=监听IP，Client=远程IP）</summary>
        public string IPAddress { get; set; }

        /// <summary>端口号</summary>
        public int Port { get; set; }

        /// <summary>连接超时（毫秒）</summary>
        public int TimeoutMs { get; set; } = 3000;

        /// <summary>字节序</summary>
        public ByteOrder ByteOrder { get; set; } = ByteOrder.BigEndian;

        /// <summary>字符编码：ASCII / UTF8 / HEX</summary>
        public string Encoding { get; set; } = "ASCII";

        /// <summary>帧起始符（十六进制字符串，为空表示无）</summary>
        public string StartDelimiter { get; set; } = "";

        /// <summary>帧结束符（十六进制字符串，为空表示无）</summary>
        public string EndDelimiter { get; set; } = "";

        /// <summary>分隔符（字符，为空表示无）</summary>
        public string Separator { get; set; } = "";

        /// <summary>是否启用自动回复</summary>
        public bool AutoReplyEnabled { get; set; } = false;

        /// <summary>自动回复内容（支持表达式变量）</summary>
        public string AutoReplyMessage { get; set; } = "";
    }

    /// <summary>
    /// 串口通讯配置
    /// </summary>
    public class SerialConfig
    {
        /// <summary>串口名称（如 COM3）</summary>
        public string PortName { get; set; } = "COM1";

        /// <summary>波特率</summary>
        public int BaudRate { get; set; } = 9600;

        /// <summary>数据位</summary>
        public int DataBits { get; set; } = 8;

        /// <summary>停止位</summary>
        public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;

        /// <summary>校验位</summary>
        public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;

        /// <summary>流控制</summary>
        public System.IO.Ports.Handshake Handshake { get; set; } = System.IO.Ports.Handshake.None;

        /// <summary>帧起始符</summary>
        public string StartDelimiter { get; set; } = "";

        /// <summary>帧结束符</summary>
        public string EndDelimiter { get; set; } = "";

        /// <summary>分隔符</summary>
        public string Separator { get; set; } = "";
    }

    /// <summary>
    /// 统一的通讯配置
    /// </summary>
    public class CommunicationConfig
    {
        public TcpConfig TcpConfig { get; set; } = new TcpConfig();
        public SerialConfig SerialConfig { get; set; } = new SerialConfig();
        public bool TcpEnabled { get; set; } = false;
        public bool SerialEnabled { get; set; } = false;
    }
}
