using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// ILogService 扩展方法
    /// 提供 Info/Error/Warn/Debug 便捷调用
    /// </summary>
    public static class LogServiceExtensions
    {
        public static void Info(this ILogService logService, LogCategory category, string userName, string message)
        {
            if (logService != null)
                logService.Log(category, LogLevel.INFO, userName, message);
        }

        public static void Error(this ILogService logService, LogCategory category, string userName, string message)
        {
            if (logService != null)
                logService.Log(category, LogLevel.ERROR, userName, message);
        }

        public static void Warn(this ILogService logService, LogCategory category, string userName, string message)
        {
            if (logService != null)
                logService.Log(category, LogLevel.WARN, userName, message);
        }

        public static void Debug(this ILogService logService, LogCategory category, string userName, string message)
        {
            if (logService != null)
                logService.Log(category, LogLevel.DEBUG, userName, message);
        }
    }
}
