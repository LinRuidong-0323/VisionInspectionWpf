using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using VisionInspection.Data;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 检测结果存储服务
    /// 将检测结果写入 SQLite，并提供统计查询
    /// </summary>
    public class InspectionResultService
    {
        private readonly DatabaseHelper _db;
        private readonly ILogService _logService;

        // 当班统计
        private int _okCount;
        private int _ngCount;
        private double _lastCycleTimeMs;
        private double _totalCycleTimeMs;
        private double _minCycleTimeMs = double.MaxValue;
        private double _maxCycleTimeMs;
        private DateTime _shiftStart;

        /// <summary>OK 计数</summary>
        public int OkCount => _okCount;

        /// <summary>NG 计数</summary>
        public int NgCount => _ngCount;

        /// <summary>总数</summary>
        public int TotalCount => _okCount + _ngCount;

        /// <summary>良率 (%)</summary>
        public double YieldRate => TotalCount > 0 ? Math.Round((double)_okCount / TotalCount * 100, 2) : 100.0;

        /// <summary>最近节拍 (ms)</summary>
        public double LastCycleTimeMs => _lastCycleTimeMs;

        /// <summary>平均节拍 (ms)</summary>
        public double AvgCycleTimeMs => TotalCount > 0 ? Math.Round(_totalCycleTimeMs / TotalCount, 1) : 0;

        /// <summary>最小节拍 (ms)</summary>
        public double MinCycleTimeMs => _minCycleTimeMs == double.MaxValue ? 0 : _minCycleTimeMs;

        /// <summary>最大节拍 (ms)</summary>
        public double MaxCycleTimeMs => _maxCycleTimeMs;

        /// <summary>统计数据变更事件</summary>
        public event Action OnStatisticsChanged;

        public InspectionResultService(DatabaseHelper db, ILogService logService)
        {
            _db = db;
            _logService = logService;
            _shiftStart = DateTime.Now;
        }

        /// <summary>
        /// 记录检测结果
        /// </summary>
        public void RecordResult(InspectionResult result)
        {
            // 更新内存统计
            if (result.Verdict == InspectionVerdict.OK)
                _okCount++;
            else if (result.Verdict == InspectionVerdict.NG)
                _ngCount++;

            _lastCycleTimeMs = result.CycleTimeMs;
            _totalCycleTimeMs += result.CycleTimeMs;

            if (result.CycleTimeMs > 0)
            {
                if (result.CycleTimeMs < _minCycleTimeMs)
                    _minCycleTimeMs = result.CycleTimeMs;
                if (result.CycleTimeMs > _maxCycleTimeMs)
                    _maxCycleTimeMs = result.CycleTimeMs;
            }

            // 写入数据库
            try
            {
                _db.InsertInspectionResult(result);
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.SYSTEM, "System", $"记录检测结果失败: {ex.Message}");
            }

            OnStatisticsChanged?.Invoke();
        }

        /// <summary>
        /// 手动清零计数器
        /// </summary>
        public void ResetCounters()
        {
            _okCount = 0;
            _ngCount = 0;
            _lastCycleTimeMs = 0;
            _totalCycleTimeMs = 0;
            _minCycleTimeMs = double.MaxValue;
            _maxCycleTimeMs = 0;
            _shiftStart = DateTime.Now;
            _logService?.Info(LogCategory.SYSTEM, "System", "统计计数器已清零");
            OnStatisticsChanged?.Invoke();
        }

        /// <summary>
        /// 获取历史统计
        /// </summary>
        public (int totalOk, int totalNg, double avgCycleTime) GetHistoryStats(DateTime from, DateTime to)
        {
            try
            {
                int ok = _db.GetOkCount(from, to);
                int ng = _db.GetNgCount(from, to);
                double avg = _db.GetAvgCycleTime(from, to);
                return (ok, ng, avg);
            }
            catch
            {
                return (0, 0, 0);
            }
        }
    }
}
