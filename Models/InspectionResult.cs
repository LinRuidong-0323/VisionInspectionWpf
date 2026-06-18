using System;
using System.Collections.Generic;

namespace VisionInspection.Models
{
    /// <summary>
    /// 单次检测结果
    /// </summary>
    public class InspectionResult
    {
        /// <summary>记录ID</summary>
        public long Id { get; set; }

        /// <summary>检测时间</summary>
        public DateTime Timestamp { get; set; }

        /// <summary>当前产品/配方名称</summary>
        public string RecipeName { get; set; }

        /// <summary>当前作业名称</summary>
        public string JobName { get; set; }

        /// <summary>判定结果</summary>
        public InspectionVerdict Verdict { get; set; }

        /// <summary>节拍时间（毫秒）</summary>
        public double CycleTimeMs { get; set; }

        /// <summary>图像保存路径（如有）</summary>
        public string ImagePath { get; set; }

        /// <summary>变量键值对（工具输出的变量）</summary>
        public Dictionary<string, object> Variables { get; set; }

        /// <summary>工具报错信息列表</summary>
        public List<string> ErrorMessages { get; set; }

        public InspectionResult()
        {
            Timestamp = DateTime.Now;
            RecipeName = "Default";
            JobName = "";
            Verdict = InspectionVerdict.Unknown;
            Variables = new Dictionary<string, object>();
        }

        /// <summary>
        /// 获取判定结果的中文显示
        /// </summary>
        public string VerdictDisplay
        {
            get
            {
                switch (Verdict)
                {
                    case InspectionVerdict.OK: return "OK";
                    case InspectionVerdict.NG: return "NG";
                    default: return "--";
                }
            }
        }
    }
}
