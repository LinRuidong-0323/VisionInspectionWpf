using System;

namespace VisionInspection.Models
{
    /// <summary>
    /// 日志条目
    /// </summary>
    public class LogEntry
    {
        /// <summary>时间戳</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>分类：[CAMERA][JOB][TCP][COM][SETTING][RECIPE][SYSTEM]</summary>
        public LogCategory Category { get; set; }

        /// <summary>级别：DEBUG/INFO/WARN/ERROR</summary>
        public LogLevel Level { get; set; }

        /// <summary>操作用户</summary>
        public string UserName { get; set; }

        /// <summary>日志消息内容</summary>
        public string Message { get; set; }

        public LogEntry()
        {
            Timestamp = DateTime.Now;
            UserName = "";
            Message = "";
        }

        public LogEntry(LogCategory category, LogLevel level, string userName, string message)
        {
            Timestamp = DateTime.Now;
            Category = category;
            Level = level;
            UserName = userName;
            Message = message;
        }

        /// <summary>
        /// 格式化为文档规定的格式：
        /// [2026-06-17 14:22:01] [CAMERA] [User:Admin] Cam-01 Exposure: 5000→8000 us
        /// </summary>
        public override string ToString()
        {
            string levelStr = Level == LogLevel.INFO ? "" : $" [{Level}]";
            return $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Category}]{levelStr} [User:{UserName}] {Message}";
        }
    }
}
