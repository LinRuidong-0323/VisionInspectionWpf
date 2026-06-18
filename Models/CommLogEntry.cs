using System;

namespace VisionInspection.Forms
{
    /// <summary>
    /// 通讯日志条目
    /// </summary>
    internal class CommLogEntry
    {
        public DateTime Time { get; set; }
        public string Direction { get; set; }
        public byte[] Data { get; set; }
        public string Source { get; set; }
    }
}
