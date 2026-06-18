using System;
using System.Collections.Generic;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 视觉服务接口
    /// </summary>
    public interface IVisionService
    {
        /// <summary>当前作业是否已加载</summary>
        bool IsJobLoaded { get; }

        /// <summary>当前作业文件路径</summary>
        string CurrentJobPath { get; }

        /// <summary>当前作业名称</summary>
        string CurrentJobName { get; }

        /// <summary>加载 .vpp 作业文件</summary>
        bool LoadJob(string filePath);

        /// <summary>新建空白作业</summary>
        bool NewJob();

        /// <summary>保存当前作业</summary>
        bool SaveJob();

        /// <summary>另存为</summary>
        bool SaveJobAs(string filePath);

        /// <summary>设置输入图像（仿真模式）</summary>
        void SetInputImage(Cognex.VisionPro.ICogImage image);

        /// <summary>运行一次视觉工具链</summary>
        InspectionResult RunOnce();

        /// <summary>获取当前工具块变量列表</summary>
        Dictionary<string, object> GetToolBlockVariables();

        /// <summary>获取 ToolBlock 编辑器控件</summary>
        Cognex.VisionPro.ToolBlock.CogToolBlockEditV2 GetToolBlockEditor();

        /// <summary>作业变更事件</summary>
        event Action OnJobChanged;
    }
}
