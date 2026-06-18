using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cognex.VisionPro;
using Cognex.VisionPro.ToolBlock;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 视觉服务实现
    /// 管理 CogToolBlock 的加载/保存/运行
    /// </summary>
    public class VisionService : IVisionService, IDisposable
    {
        private CogToolBlock _toolBlock;
        private CogToolBlockEditV2 _toolBlockEditor;
        private ILogService _logService;
        private string _currentJobPath;
        private bool _isDirty;
        private ICogRecord _lastRunRecord;

        public bool IsJobLoaded => _toolBlock != null;
        public string CurrentJobPath => _currentJobPath;
        public string CurrentJobName => IsJobLoaded
            ? Path.GetFileNameWithoutExtension(_currentJobPath)
            : "未加载";

        public event Action OnJobChanged;

        public VisionService(ILogService logService)
        {
            _logService = logService;
        }

        /// <summary>
        /// 从默认模板创建新作业
        /// </summary>
        private void CreateDefaultJob()
        {
            _toolBlock = new CogToolBlock();
            _toolBlock.Name = "DefaultJob";
            _currentJobPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "DefaultJob.vpp");
            _isDirty = false;

            _logService?.Info(LogCategory.JOB, "System", "已从内嵌模板创建默认作业");

            // 初始化编辑器
            InitializeEditor();
            OnJobChanged?.Invoke();
        }

        public bool LoadJob(string filePath)
        {
            if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            {
                _logService?.Error(LogCategory.JOB, "System", $"作业文件不存在: {filePath}");
                return false;
            }

            try
            {
                _toolBlock = CogSerializer.LoadObjectFromFile(filePath) as CogToolBlock;
                if (_toolBlock == null)
                {
                    _logService?.Error(LogCategory.JOB, "System", $"无法解析作业文件: {filePath}");
                    return false;
                }

                _currentJobPath = filePath;
                _isDirty = false;
                _logService?.Info(LogCategory.JOB, "System", $"已加载作业: {filePath}");
                InitializeEditor();
                OnJobChanged?.Invoke();
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.JOB, "System", $"加载作业失败: {ex.Message}");
                return false;
            }
        }

        public bool NewJob()
        {
            try
            {
                if (_isDirty)
                {
                    // TODO: 提示保存
                }

                CreateDefaultJob();
                _currentJobPath = "";
                _isDirty = false;
                _logService?.Info(LogCategory.JOB, "System", "已创建新作业");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.JOB, "System", $"创建作业失败: {ex.Message}");
                return false;
            }
        }

        public bool SaveJob()
        {
            if (string.IsNullOrEmpty(_currentJobPath))
                return false;

            try
            {
                string dir = Path.GetDirectoryName(_currentJobPath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);

                CogSerializer.SaveObjectToFile(_toolBlock, _currentJobPath);
                _isDirty = false;
                _logService?.Info(LogCategory.JOB, "System", $"已保存作业: {_currentJobPath}");
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.JOB, "System", $"保存作业失败: {ex.Message}");
                return false;
            }
        }

        public bool SaveJobAs(string filePath)
        {
            _currentJobPath = filePath;
            return SaveJob();
        }

        public void SetInputImage(ICogImage image)
        {
            if (_toolBlock == null)
            {
                _logService?.Warn(LogCategory.JOB, "System", "SetInputImage: ToolBlock 为 null，无法设置图像");
                return;
            }
            if (image == null)
            {
                _logService?.Warn(LogCategory.JOB, "System", "SetInputImage: 图像为 null，无法设置");
                return;
            }

            try
            {
                var inputs = _toolBlock.Inputs;
                _logService?.Debug(LogCategory.JOB, "System",
                    string.Format("SetInputImage: ToolBlock 有 {0} 个输入端子", inputs != null ? inputs.Count : 0));

                if (inputs == null || inputs.Count == 0)
                {
                    _logService?.Warn(LogCategory.JOB, "System",
                        "SetInputImage: ToolBlock 没有输入端子！请在编辑器中添加一个 ICogImage 类型的输入端子");
                    return;
                }

                bool found = false;
                foreach (CogToolBlockTerminal terminal in inputs)
                {
                    _logService?.Debug(LogCategory.JOB, "System",
                        string.Format("  端子: {0}, 类型={1}", terminal.Name, terminal.ValueType));

                    if (terminal.ValueType == typeof(ICogImage) ||
                        (terminal.ValueType != null && terminal.ValueType.IsAssignableFrom(typeof(ICogImage))))
                    {
                        terminal.Value = image;
                        found = true;
                        _logService?.Info(LogCategory.JOB, "System",
                            string.Format("SetInputImage: 图像已设置到端子 [{0}] ({1}×{2})",
                                terminal.Name, image.Width, image.Height));
                        break;
                    }
                }

                if (!found)
                {
                    _logService?.Warn(LogCategory.JOB, "System",
                        "SetInputImage: 未找到 ICogImage 类型的输入端子，需要先添加。所有端子类型: " +
                        string.Join(", ", inputs.Cast<CogToolBlockTerminal>().Select(t => t.ValueType?.Name ?? "null")));
                }
            }
            catch (Exception ex)
            {
                _logService?.Warn(LogCategory.JOB, "System", "SetInputImage 异常: " + ex.Message);
            }
        }

        public InspectionResult RunOnce()
        {
            var result = new InspectionResult();
            result.JobName = CurrentJobName;

            if (_toolBlock == null)
            {
                result.Verdict = InspectionVerdict.NG;
                _logService?.Warn(LogCategory.JOB, "System", "未加载作业，无法运行");
                return result;
            }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();

                // 运行工具块
                _toolBlock.Run();

                sw.Stop();
                result.CycleTimeMs = sw.Elapsed.TotalMilliseconds;

                // 收集输出变量
                result.Variables = GetToolBlockVariables();

                // 收集所有工具的运行状态（报错/警告/成功）
                try
                {
                    var errorLines = new System.Collections.Generic.List<string>();
                    foreach (ICogTool tool in _toolBlock.Tools)
                    {
                        var status = tool.RunStatus;
                        string msg = status.Message ?? "";
                        if (status.Result == CogToolResultConstants.Error ||
                            status.Result == CogToolResultConstants.Reject)
                        {
                            string toolErr = string.Format("Tool \"{0}\" 报错: {1}", tool.Name, msg);
                            errorLines.Add(toolErr);
                            _logService?.Error(LogCategory.JOB, "System", "VPP工具报错: " + toolErr);
                        }
                        else if (status.Result == CogToolResultConstants.Warning)
                        {
                            _logService?.Warn(LogCategory.JOB, "System",
                                string.Format("Tool \"{0}\" 警告: {1}", tool.Name, msg));
                        }

                    }

                    // 检查 ToolBlock 整体的 RunStatus
                    var blockStatus = _toolBlock.RunStatus;
                    if (blockStatus.Result == CogToolResultConstants.Error ||
                        blockStatus.Result == CogToolResultConstants.Reject)
                    {
                        string blockErr = string.Format("ToolBlock 整体报错: {0}", blockStatus.Message ?? "");
                        if (!errorLines.Contains(blockErr))
                            errorLines.Add(blockErr);
                        _logService?.Error(LogCategory.JOB, "System", blockErr);
                    }

                    if (errorLines.Count > 0)
                    {
                        result.ErrorMessages = errorLines;
                        result.Verdict = InspectionVerdict.NG;
                    }
                }
                catch { }

                // 生成最后一次运行的图形记录（含 overlay 叠加图形）
                try
                {
                    _lastRunRecord = _toolBlock.CreateLastRunRecord();
                }
                catch { _lastRunRecord = null; }

                // 若工具报错已是 NG，直接保留，不再被后续逻辑覆盖
                bool alreadyFailed = result.ErrorMessages != null && result.ErrorMessages.Count > 0;

                // 判断结果：检查是否有工具输出 "Result" 或 "Pass" 变量
                if (!alreadyFailed)
                {
                    if (result.Variables.TryGetValue("Accept", out var acceptObj))
                    {
                        result.Verdict = Convert.ToBoolean(acceptObj) ? InspectionVerdict.OK : InspectionVerdict.NG;
                    }
                    else if (result.Variables.TryGetValue("Result", out var resultObj))
                    {
                        string rs = resultObj?.ToString() ?? "";
                        result.Verdict = (rs == "1" || rs.Equals("OK", StringComparison.OrdinalIgnoreCase) || rs.Equals("True", StringComparison.OrdinalIgnoreCase))
                            ? InspectionVerdict.OK : InspectionVerdict.NG;
                    }
                    else
                    {
                        var runStatus = _toolBlock.RunStatus;
                        if (runStatus.Result == CogToolResultConstants.Accept)
                            result.Verdict = InspectionVerdict.OK;
                        else if (runStatus.Result == CogToolResultConstants.Reject)
                            result.Verdict = InspectionVerdict.NG;
                        else
                            result.Verdict = InspectionVerdict.Unknown;
                    }
                }
            }
            catch (Exception ex)
            {
                result.Verdict = InspectionVerdict.NG;
                _logService?.Error(LogCategory.JOB, "System", $"作业运行失败: {ex.Message}");
            }

            return result;
        }

        public Dictionary<string, object> GetToolBlockVariables()
        {
            var variables = new Dictionary<string, object>();

            if (_toolBlock == null)
                return variables;

            try
            {
                // 收集输入端子
                foreach (CogToolBlockTerminal terminal in _toolBlock.Inputs)
                {
                    if (terminal.Value != null)
                        variables["I." + terminal.Name] = terminal.Value;
                }

                // 收集输出端子
                foreach (CogToolBlockTerminal terminal in _toolBlock.Outputs)
                {
                    if (terminal.Value != null)
                        variables["O." + terminal.Name] = terminal.Value;
                }
            }
            catch { }

            return variables;
        }

        /// <summary>
        /// 确保 ToolBlock 编辑器已创建（即使 VPP 未加载也可显示空编辑器）
        /// </summary>
        public void EnsureToolBlockEditorCreated()
        {
            if (_toolBlockEditor == null || _toolBlockEditor.IsDisposed)
            {
                _toolBlockEditor = new CogToolBlockEditV2();
                _toolBlockEditor.Dock = System.Windows.Forms.DockStyle.Fill;
                if (_toolBlock != null)
                    _toolBlockEditor.Subject = _toolBlock;
            }
        }

        public CogToolBlockEditV2 GetToolBlockEditor()
        {
            EnsureToolBlockEditorCreated();
            return _toolBlockEditor;
        }

        private void InitializeEditor()
        {
            // 如果编辑器已存在且可用，只更新 Subject（不销毁重建，避免宿主引用失效）
            if (_toolBlockEditor != null && !_toolBlockEditor.IsDisposed)
            {
                _toolBlockEditor.Subject = _toolBlock;
                _toolBlockEditor.PerformLayout();
                return;
            }

            // 首次创建
            _toolBlockEditor = new CogToolBlockEditV2();
            _toolBlockEditor.Dock = System.Windows.Forms.DockStyle.Fill;
            if (_toolBlock != null)
                _toolBlockEditor.Subject = _toolBlock;
        }

        /// <summary>
        /// 获取底层 CogToolBlock 对象
        /// </summary>
        public CogToolBlock GetToolBlock()
        {
            return _toolBlock;
        }

        /// <summary>
        /// 获取最后一次运行的图形记录（含 overlay 叠加图形）
        /// </summary>
        public ICogRecord GetLastRunRecord()
        {
            return _lastRunRecord;
        }

        /// <summary>
        /// 确保默认作业可用。若丢失或损坏，自动从代码重建。
        /// </summary>
        public void EnsureDefaultJob(string vppFilePath)
        {
            bool fileExists = File.Exists(vppFilePath);

            if (fileExists)
            {
                try
                {
                    var job = CogSerializer.LoadObjectFromFile(vppFilePath) as CogToolBlock;
                    if (job != null)
                    {
                        _toolBlock = job;
                        _currentJobPath = vppFilePath;
                        InitializeEditor();
                        _logService?.Info(LogCategory.JOB, "System",
                            string.Format("已加载作业: {0}", vppFilePath));
                        OnJobChanged?.Invoke();
                        return;
                    }
                    else
                    {
                        _logService?.Error(LogCategory.JOB, "System",
                            string.Format("作业文件无法解析: {0}，请手动检查文件是否损坏", vppFilePath));
                    }
                }
                catch (Exception ex)
                {
                    _logService?.Error(LogCategory.JOB, "System",
                        string.Format("作业文件加载失败: {0}，错误: {1}。原文件未被修改。", vppFilePath, ex.Message));
                }

                // 文件存在但加载失败 → 创建内存中的空作业，但不覆盖磁盘文件
                _logService?.Warn(LogCategory.JOB, "System",
                    string.Format("[JOB] 已创建内存临时作业（磁盘文件未修改: {0}）", vppFilePath));
                _toolBlock = new CogToolBlock();
                _toolBlock.Name = Path.GetFileNameWithoutExtension(vppFilePath);
                _currentJobPath = vppFilePath;
                OnJobChanged?.Invoke();
                return;
            }

            // 文件确实不存在 → 创建新作业并保存
            _logService?.Info(LogCategory.JOB, "System",
                string.Format("[JOB] 作业文件不存在，已创建新作业: {0}", vppFilePath));
            _toolBlock = new CogToolBlock();
            _toolBlock.Name = Path.GetFileNameWithoutExtension(vppFilePath);
            _currentJobPath = vppFilePath;

            string dir = Path.GetDirectoryName(vppFilePath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);
            InitializeEditor();
            SaveJob();
            OnJobChanged?.Invoke();
        }

        public void Dispose()
        {
            if (_toolBlockEditor != null)
            {
                _toolBlockEditor.Subject = null;
                _toolBlockEditor.Dispose();
                _toolBlockEditor = null;
            }
            _toolBlock?.Dispose();
        }
    }
}
