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
        private readonly int _maxFileSizeMB;
        private readonly int _retentionDays;
        private readonly ConcurrentQueue<LogEntry> _recentLogs;
        private readonly object _fileLock = new object();
        private string _currentLogFile;
        private DateTime _currentLogDate;
        private int _fileSequence;

        public event Action<LogEntry> OnLogAdded;

        public LogService(string basePath, int maxFileSizeMB = 50, int retentionDays = 30)
        {
            _logDirectory = Path.Combine(basePath, "Log");
            _maxFileSizeMB = maxFileSizeMB;
            _retentionDays = retentionDays;
            _recentLogs = new ConcurrentQueue<LogEntry>();
            _fileSequence = 0;

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

                // 日期变更或首次写入时，创建新文件
                if (_currentLogFile == null || _currentLogDate != today)
                {
                    _currentLogDate = today;
                    _fileSequence = 0;
                    _currentLogFile = GetLogFilePath();
                }

                // 检查文件大小
                try
                {
                    var fileInfo = new FileInfo(_currentLogFile);
                    if (fileInfo.Exists && fileInfo.Length >= _maxFileSizeMB * 1024 * 1024)
                    {
                        _fileSequence++;
                        _currentLogFile = GetLogFilePath();
                    }
                }
                catch { }

                // 写入
                try
                {
                    File.AppendAllText(_currentLogFile, entry.ToString() + Environment.NewLine);
                }
                catch { /* 日志写入失败不应影响主程序 */ }
            }
        }

        private string GetLogFilePath()
        {
            string dateStr = _currentLogDate.ToString("yyyyMMdd");
            string fileName;
            if (_fileSequence > 0)
                fileName = string.Format("log_{0}_{1}.txt", dateStr, _fileSequence);
            else
                fileName = string.Format("log_{0}.txt", dateStr);

            return Path.Combine(_logDirectory, fileName);
        }

        private void CleanOldLogs()
        {
            try
            {
                var cutoff = DateTime.Today.AddDays(-_retentionDays);
                foreach (var file in Directory.GetFiles(_logDirectory, "log_*.txt"))
                {
                    var fi = new FileInfo(file);
                    if (fi.LastWriteTime < cutoff)
                    {
                        try { File.Delete(file); } catch { }
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            // 确保最后日志写入完成
            Thread.Sleep(100);
        }
    }
}
