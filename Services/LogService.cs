using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 日志服务实现
    /// - 分类标识：[CAMERA][JOB][TCP][COM][SETTING][RECIPE][SYSTEM]
    /// - 级别：DEBUG/INFO/WARN/ERROR
    /// - 存储：按天保存到 Log\ 目录，单文件超 50MB 自动切割，保留 30 天
    /// - 实时推送到 UI
    /// </summary>
    public class LogService : ILogService, IDisposable
    {
        private readonly string _logDirectory;
        private int _maxFileSizeMB;
        private int _retentionDays;
        private readonly ConcurrentQueue<LogEntry> _recentLogs;
        private readonly object _fileLock = new object();
        // 按级别分文件：每个级别独立追踪当前日志文件
        private string _currentInfoFile;
        private string _currentWarnFile;
        private string _currentErrorFile;
        private string _currentDebugFile;
        private DateTime _currentLogDate;
        private int _infoSeq, _warnSeq, _errorSeq, _debugSeq;

        public event Action<LogEntry> OnLogAdded;

        public LogService(string basePath, int maxFileSizeMB = 50, int retentionDays = 30)
        {
            _logDirectory = Path.Combine(basePath, "Log");
            _maxFileSizeMB = maxFileSizeMB;
            _retentionDays = retentionDays;
            _recentLogs = new ConcurrentQueue<LogEntry>();
            _infoSeq = _warnSeq = _errorSeq = _debugSeq = 0;

            // 确保日志目录存在
            Directory.CreateDirectory(_logDirectory);

            // 清理过期日志
            CleanOldLogs();
        }

        public void Log(LogCategory category, LogLevel level, string userName, string message)
        {
            var entry = new LogEntry(category, level, userName, message);

            // 加入内存缓存
            _recentLogs.Enqueue(entry);
            // 限制内存中最多保留 10000 条
            while (_recentLogs.Count > 10000)
            {
                _recentLogs.TryDequeue(out _);
            }

            // 写入文件
            WriteToFile(entry);

            // 推送到UI
            OnLogAdded?.Invoke(entry);
        }

        public List<LogEntry> GetLogs()
        {
            return _recentLogs.ToList();
        }

        public void ClearMemoryLogs()
        {
            while (_recentLogs.TryDequeue(out _)) { }
        }

        /// <summary>
        /// 便捷方法：INFO 级别日志
        /// </summary>
        public void Info(LogCategory category, string userName, string message)
        {
            Log(category, LogLevel.INFO, userName, message);
        }

        /// <summary>
        /// 便捷方法：WARN 级别日志
        /// </summary>
        public void Warn(LogCategory category, string userName, string message)
        {
            Log(category, LogLevel.WARN, userName, message);
        }

        /// <summary>
        /// 便捷方法：ERROR 级别日志
        /// </summary>
        public void Error(LogCategory category, string userName, string message)
        {
            Log(category, LogLevel.ERROR, userName, message);
        }

        /// <summary>
        /// 便捷方法：DEBUG 级别日志
        /// </summary>
        public void Debug(LogCategory category, string userName, string message)
        {
            Log(category, LogLevel.DEBUG, userName, message);
        }

        private void WriteToFile(LogEntry entry)
        {
            lock (_fileLock)
            {
                DateTime today = DateTime.Today;

                if (_currentLogDate != today)
                {
                    _currentLogDate = today;
                    _infoSeq = _warnSeq = _errorSeq = _debugSeq = 0;
                    _currentInfoFile = null;
                    _currentWarnFile = null;
                    _currentErrorFile = null;
                    _currentDebugFile = null;
                }

                string filePath;
                switch (entry.Level)
                {
                    case LogLevel.ERROR:
                        if (_currentErrorFile == null || FileSizeExceeded(_currentErrorFile))
                        {
                            if (FileSizeExceeded(_currentErrorFile)) _errorSeq++;
                            _currentErrorFile = GetLogFilePath("错误", _errorSeq);
                        }
                        filePath = _currentErrorFile;
                        break;
                    case LogLevel.WARN:
                        if (_currentWarnFile == null || FileSizeExceeded(_currentWarnFile))
                        {
                            if (FileSizeExceeded(_currentWarnFile)) _warnSeq++;
                            _currentWarnFile = GetLogFilePath("警告", _warnSeq);
                        }
                        filePath = _currentWarnFile;
                        break;
                    case LogLevel.DEBUG:
                        if (_currentDebugFile == null || FileSizeExceeded(_currentDebugFile))
                        {
                            if (FileSizeExceeded(_currentDebugFile)) _debugSeq++;
                            _currentDebugFile = GetLogFilePath("调试", _debugSeq);
                        }
                        filePath = _currentDebugFile;
                        break;
                    default:
                        if (_currentInfoFile == null || FileSizeExceeded(_currentInfoFile))
                        {
                            if (FileSizeExceeded(_currentInfoFile)) _infoSeq++;
                            _currentInfoFile = GetLogFilePath("信息", _infoSeq);
                        }
                        filePath = _currentInfoFile;
                        break;
                }

                try
                {
                    string dir = Path.GetDirectoryName(filePath);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.AppendAllText(filePath, entry.ToString() + Environment.NewLine);
                }
                catch { }
            }
        }

        private bool FileSizeExceeded(string filePath)
        {
            try
            {
                if (string.IsNullOrEmpty(filePath)) return true;
                var fi = new FileInfo(filePath);
                return fi.Exists && fi.Length >= _maxFileSizeMB * 1024L * 1024L;
            }
            catch { return false; }
        }

        private string GetLogFilePath(string level, int seq)
        {
            string dateFolder = _currentLogDate.ToString("yyyy-MM-dd");
            string fileName = seq > 0
                ? string.Format("{0}_{1}.txt", level, seq)
                : string.Format("{0}.txt", level);
            return Path.Combine(_logDirectory, dateFolder, fileName);
        }

        private void CleanOldLogs()
        {
            try
            {
                var cutoff = DateTime.Today.AddDays(-_retentionDays);
                // 遍历日期文件夹
                foreach (var dateDir in Directory.GetDirectories(_logDirectory))
                {
                    string dirName = Path.GetFileName(dateDir);
                    if (DateTime.TryParse(dirName, out DateTime dirDate) && dirDate < cutoff)
                    {
                        try { Directory.Delete(dateDir, true); } catch { }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// 运行时更新日志保留配置（由设置窗口调用）
        /// </summary>
        public void UpdateRetention(int retentionDays, int maxFileSizeMB)
        {
            _retentionDays = retentionDays;
            _maxFileSizeMB = maxFileSizeMB;
            CleanOldLogs();
        }

        public void Dispose()
        {
            // 确保最后日志写入完成
            Thread.Sleep(100);
        }
    }
}
