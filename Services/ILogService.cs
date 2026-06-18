using System;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 日志服务接口
    /// </summary>
    public interface ILogService
    {
        /// <summary>写日志</summary>
        void Log(LogCategory category, LogLevel level, string userName, string message);

        /// <summary>日志新增事件（供UI实时刷新）</summary>
        event Action<LogEntry> OnLogAdded;

        /// <summary>获取所有日志（用于UI展示）</summary>
        System.Collections.Generic.List<LogEntry> GetLogs();

        /// <summary>清空内存中的日志缓存</summary>
        void ClearMemoryLogs();
    }
}
