using System;

namespace VisionInspection.Models
{
    /// <summary>
    /// 应用程序设置
    /// </summary>
    public class AppSettings
    {
        /// <summary>当前运行模式</summary>
        public RunMode RunMode { get; set; } = RunMode.Offline;

        /// <summary>当前配方名称</summary>
        public string CurrentRecipe { get; set; } = "Default";

        /// <summary>是否启用图像保存</summary>
        public bool ImageSaveEnabled { get; set; } = false;

        /// <summary>图像保存条件：All / OK / NG</summary>
        public string ImageSaveCondition { get; set; } = "All";

        /// <summary>图像保存路径模板</summary>
        public string ImageSavePath { get; set; } = @"Images\{Recipe}\{Date}\{Verdict}\";

        /// <summary>是否启用数据记录</summary>
        public bool DataLogEnabled { get; set; } = true;

        /// <summary>日志保留天数</summary>
        public int LogRetentionDays { get; set; } = 30;

        /// <summary>单个日志文件最大大小（MB）</summary>
        public int LogMaxFileSizeMB { get; set; } = 50;

        /// <summary>自动锁定超时（秒），0表示不超时</summary>
        public int AutoLockTimeoutSec { get; set; } = 300;

        /// <summary>仿真模式连续运行间隔（毫秒）</summary>
        public int SimulationIntervalMs { get; set; } = 500;

        /// <summary>统计计数器自动清零时间（HH:mm），空字符串表示不定时</summary>
        public string AutoResetCounterTime { get; set; } = "";

        /// <summary>相机自动重连间隔（秒）</summary>
        public int CameraReconnectIntervalSec { get; set; } = 5;

        /// <summary>相机自动重连最大次数</summary>
        public int CameraReconnectMaxRetries { get; set; } = 3;
    }
}
