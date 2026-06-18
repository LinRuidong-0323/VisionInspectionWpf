using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Timers;
using Cognex.VisionPro;
using Cognex.VisionPro.ImageFile;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 仿真图像服务
    /// 支持单张图片、图片序列、AVI视频作为图像源
    /// </summary>
    public class SimulationImageService : IDisposable
    {
        private readonly ILogService _logService;
        private CogImageFileTool _imageFileTool;
        private System.Timers.Timer _playbackTimer;
        private List<string> _imageSequence;
        private int _sequenceIndex;

        /// <summary>当前图像</summary>
        public ICogImage CurrentImage { get; private set; }

        /// <summary>当前图像文件名</summary>
        public string CurrentFileName { get; private set; }

        /// <summary>图像源类型</summary>
        public ImageSourceType SourceType { get; set; }

        /// <summary>是否正在连续播放</summary>
        public bool IsPlaying { get; private set; }

        /// <summary>连续播放间隔（毫秒）</summary>
        public int PlaybackIntervalMs { get; set; } = 500;

        /// <summary>总帧数（仅序列/视频）</summary>
        public int TotalFrames => _imageSequence?.Count ?? 0;

        /// <summary>当前帧索引（仅序列/视频）</summary>
        public int CurrentFrameIndex => _sequenceIndex;

        /// <summary>帧率（每秒帧数）</summary>
        public double FrameRate { get; private set; }

        /// <summary>新图像加载完成事件</summary>
        public event Action<ICogImage> OnImageReady;

        /// <summary>播放状态变更事件</summary>
        public event Action<bool> OnPlaybackChanged;

        public SimulationImageService(ILogService logService)
        {
            _logService = logService;
            _imageFileTool = new CogImageFileTool();
            _imageSequence = new List<string>();
        }

        /// <summary>
        /// 加载单张图片
        /// </summary>
        public bool LoadImage(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"图片文件不存在: {filePath}");
                return false;
            }

            try
            {
                var bitmap = new Bitmap(filePath);
                CurrentImage = new CogImage24PlanarColor(bitmap);
                SourceType = ImageSourceType.SingleImage;
                _imageSequence.Clear();
                _logService?.Info(LogCategory.CAMERA, "System", $"已加载图片: {filePath}");
                OnImageReady?.Invoke(CurrentImage);
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"加载图片失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 使用 VisionPro 原生方式加载图片
        /// </summary>
        public bool LoadImageDirect(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"图片文件不存在: {filePath}");
                return false;
            }

            try
            {
                _imageFileTool.Operator.Open(filePath, CogImageFileModeConstants.Read);
                _imageFileTool.Run();
                CurrentImage = _imageFileTool.OutputImage;
                CurrentFileName = Path.GetFileName(filePath);
                _imageSequence.Clear();
                // 不改变 SourceType —— 由调用方决定是单图还是序列
                _logService?.Info(LogCategory.CAMERA, "System", $"已加载图片: {filePath}");
                OnImageReady?.Invoke(CurrentImage);
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"加载图片失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载图片序列（文件夹中的所有图片）
        /// </summary>
        public bool LoadImageSequence(string folderPath)
        {
            if (!Directory.Exists(folderPath))
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"文件夹不存在: {folderPath}");
                return false;
            }

            try
            {
                // 获取文件夹下所有文件，不预先过滤扩展名
                var allFiles = Directory.GetFiles(folderPath)
                    .OrderBy(f => f).ToList();

                _logService?.Info(LogCategory.CAMERA, "System",
                    string.Format("文件夹文件总数: {0}", allFiles.Count));

                if (allFiles.Count == 0)
                {
                    _logService?.Warn(LogCategory.CAMERA, "System", "文件夹为空");
                    return false;
                }

                // 逐个尝试载入，能打开的加入序列
                _imageSequence = new List<string>();
                foreach (var f in allFiles)
                {
                    try
                    {
                        _imageFileTool.Operator.Open(f, CogImageFileModeConstants.Read);
                        _imageFileTool.Run();
                        _imageSequence.Add(f);
                    }
                    catch (Exception ex)
                    {
                        _logService?.Warn(LogCategory.CAMERA, "System",
                            string.Format("无法打开，已跳过: {0} ({1})", Path.GetFileName(f), ex.Message));
                    }
                }

                if (_imageSequence.Count == 0)
                {
                    _logService?.Warn(LogCategory.CAMERA, "System", "没有可识别的图片文件");
                    return false;
                }

                _sequenceIndex = 0;
                CurrentFileName = Path.GetFileName(_imageSequence[0]);
                SourceType = ImageSourceType.ImageSequence;
                // 显示第一张
                _imageFileTool.Operator.Open(_imageSequence[0], CogImageFileModeConstants.Read);
                _imageFileTool.Run();
                CurrentImage = _imageFileTool.OutputImage;
                OnImageReady?.Invoke(CurrentImage);

                _logService?.Info(LogCategory.CAMERA, "System",
                    string.Format("已加载图片序列: {0} 张图片", _imageSequence.Count));
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"加载图片序列失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 加载视频文件（AVI 等）
        /// </summary>
        public bool LoadVideo(string filePath)
        {
            if (!File.Exists(filePath))
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"视频文件不存在: {filePath}");
                return false;
            }

            try
            {
                _imageFileTool.Operator.Open(filePath, CogImageFileModeConstants.Read);
                // Video mode: use sequence
                _imageFileTool.Run();
                CurrentImage = _imageFileTool.OutputImage;

                SourceType = ImageSourceType.VideoFile;
                _logService?.Info(LogCategory.CAMERA, "System", $"已加载视频: {filePath}");
                OnImageReady?.Invoke(CurrentImage);
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.CAMERA, "System", $"加载视频失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 单步执行：下一帧（序列/视频模式专用）
        /// </summary>
        public bool StepNext()
        {
            if (_imageSequence == null || _imageSequence.Count == 0)
                return false;

            _sequenceIndex++;
            if (_sequenceIndex >= _imageSequence.Count)
                _sequenceIndex = 0;

            // 直接加载不改变 SourceType
            string filePath = _imageSequence[_sequenceIndex];
            _imageFileTool.Operator.Open(filePath, CogImageFileModeConstants.Read);
            _imageFileTool.Run();
            CurrentImage = _imageFileTool.OutputImage;
            CurrentFileName = Path.GetFileName(filePath);
            OnImageReady?.Invoke(CurrentImage);
            return true;
        }

        /// <summary>
        /// 单步执行：上一帧
        /// </summary>
        public bool StepPrevious()
        {
            if (_imageSequence == null || _imageSequence.Count == 0)
                return false;

            _sequenceIndex--;
            if (_sequenceIndex < 0)
            {
                _sequenceIndex = _imageSequence.Count - 1;
            }

            LoadImageDirect(_imageSequence[_sequenceIndex]);
            return true;
        }

        /// <summary>
        /// 开始连续播放
        /// </summary>
        public void StartPlayback()
        {
            if (IsPlaying)
                return;

            IsPlaying = true;
            _playbackTimer = new System.Timers.Timer(PlaybackIntervalMs);
            _playbackTimer.Elapsed += OnPlaybackTick;
            _playbackTimer.AutoReset = true;
            _playbackTimer.Start();
            _logService?.Info(LogCategory.CAMERA, "System", "开始连续播放");
            OnPlaybackChanged?.Invoke(true);
        }

        /// <summary>
        /// 停止连续播放
        /// </summary>
        public void StopPlayback()
        {
            if (!IsPlaying)
                return;

            IsPlaying = false;
            if (_playbackTimer != null)
            {
                _playbackTimer.Stop();
                _playbackTimer.Dispose();
                _playbackTimer = null;
            }
            _logService?.Info(LogCategory.CAMERA, "System", "停止连续播放");
            OnPlaybackChanged?.Invoke(false);
        }

        private void OnPlaybackTick(object sender, ElapsedEventArgs e)
        {
            if (!IsPlaying)
                return;

            if (_imageSequence != null && _imageSequence.Count > 0)
            {
                StepNext();
            }
        }

        /// <summary>
        /// 获取图像序列列表（供UI TreeView显示）
        /// </summary>
        public List<string> GetImageSequenceFiles()
        {
            return _imageSequence?.ToList() ?? new List<string>();
        }

        public void Dispose()
        {
            StopPlayback();
            _imageFileTool?.Dispose();
            CurrentImage = null;
        }
    }
}
