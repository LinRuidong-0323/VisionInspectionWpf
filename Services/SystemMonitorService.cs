using System;
using System.Diagnostics;
using System.IO;
using System.Timers;

namespace VisionInspection.Services
{
    /// <summary>
    /// 系统资源监控服务
    /// 监控 CPU 使用率、内存占用、硬盘剩余空间
    /// </summary>
    public class SystemMonitorService : IDisposable
    {
        private System.Timers.Timer _refreshTimer;
        private PerformanceCounter _cpuCounter;
        private PerformanceCounter _memoryCounter;
        private float _lastCpuUsage;
        private float _lastMemoryUsage;

        /// <summary>当前 CPU 使用率 (%)</summary>
        public float CpuUsage
        {
            get
            {
                try
                {
                    if (_cpuCounter != null)
                        _lastCpuUsage = _cpuCounter.NextValue();
                }
                catch { }
                return _lastCpuUsage;
            }
        }

        /// <summary>当前内存使用率 (%)</summary>
        public float MemoryUsage
        {
            get
            {
                try
                {
                    if (_memoryCounter != null)
                        _lastMemoryUsage = _memoryCounter.NextValue();
                }
                catch { }
                return _lastMemoryUsage;
            }
        }

        /// <summary>总物理内存 (GB)</summary>
        public float TotalMemoryGB
        {
            get
            {
                try
                {
                    return (float)Math.Round(
                        new Microsoft.VisualBasic.Devices.ComputerInfo().TotalPhysicalMemory / 1024.0 / 1024.0 / 1024.0,
                        1);
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>可用内存 (GB)</summary>
        public float AvailableMemoryGB
        {
            get
            {
                try
                {
                    return (float)Math.Round(
                        new Microsoft.VisualBasic.Devices.ComputerInfo().AvailablePhysicalMemory / 1024.0 / 1024.0 / 1024.0,
                        1);
                }
                catch
                {
                    return 0;
                }
            }
        }

        /// <summary>数据刷新事件</summary>
        public event Action OnDataRefreshed;

        public SystemMonitorService(int refreshIntervalMs = 1000)
        {
            try
            {
                _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _memoryCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
                // 首次调用返回0，需要调用两次
                _cpuCounter.NextValue();
                _memoryCounter.NextValue();
            }
            catch
            {
                // 如果 PerformanceCounter 初始化失败（如权限不足），优雅降级
                _cpuCounter = null;
                _memoryCounter = null;
            }

            _refreshTimer = new System.Timers.Timer(refreshIntervalMs);
            _refreshTimer.Elapsed += OnRefreshTick;
            _refreshTimer.AutoReset = true;
            _refreshTimer.Start();
        }

        private void OnRefreshTick(object sender, ElapsedEventArgs e)
        {
            // 触发刷新（实际值在属性 getter 中按需获取）
            OnDataRefreshed?.Invoke();
        }

        /// <summary>
        /// 获取指定驱动器的信息
        /// </summary>
        public DriveInfo GetDriveInfo(string driveLetter)
        {
            try
            {
                string drivePath = driveLetter;
                if (!drivePath.EndsWith(":\\") && !drivePath.EndsWith(":"))
                    drivePath = drivePath + ":\\";
                else if (!drivePath.EndsWith("\\"))
                    drivePath = drivePath + "\\";

                return new DriveInfo(drivePath.Substring(0, 1));
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 获取驱动器剩余空间格式化字符串
        /// </summary>
        public string GetDriveFreeSpace(string driveLetter)
        {
            try
            {
                var drive = GetDriveInfo(driveLetter);
                if (drive != null && drive.IsReady)
                {
                    long freeGB = drive.AvailableFreeSpace / (1024 * 1024 * 1024);
                    long totalGB = drive.TotalSize / (1024 * 1024 * 1024);
                    return $"{drive.Name} {freeGB}/{totalGB}GB 可用";
                }
            }
            catch { }
            return "--";
        }

        /// <summary>
        /// 获取总内存格式化字符串
        /// </summary>
        public string GetMemoryInfo()
        {
            return $"RAM {AvailableMemoryGB:F1}/{TotalMemoryGB:F1}GB";
        }

        public void Dispose()
        {
            _refreshTimer?.Stop();
            _refreshTimer?.Dispose();
            _cpuCounter?.Dispose();
            _memoryCounter?.Dispose();
        }
    }
}
