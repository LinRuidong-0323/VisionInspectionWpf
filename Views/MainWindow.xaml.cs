using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Cognex.VisionPro;
using Cognex.VisionPro.Display;
using VisionInspection.Data;
using VisionInspection.Models;
using VisionInspection.Services;

namespace VisionInspection.Views
{
    public partial class MainWindow : Window
    {
        // ============ 服务 ============
        private LogService _logService;
        private VisionService _visionService;
        private SimulationImageService _simImageService;
        private TcpCommunicationService _tcpService;
        private UserService _userService;
        private SystemMonitorService _systemMonitor;
        private InspectionResultService _resultService;
        private RecipeService _recipeService;
        private DatabaseHelper _db;
        private TcpConfig _tcpConfig;
        private AppSettings _appSettings;

        // ============ VisionPro 控件 ============
        private CogRecordDisplay _cogDisplay;

        // ============ 子窗口 ============
        private TcpWindow _tcpWindow;
        private ToolBlockEditorForm _editorForm;

        // ============ 日志 ============
        private ObservableCollection<LogRow> _logRows = new ObservableCollection<LogRow>();
        private bool _logPaused;
        private bool _filterInfo = true, _filterWarn = true, _filterError = true;

        // ============ 状态 ============
        private bool _isRunning;
        private string _lastImagePath;
        private string _lastFolderPath;
        private System.Timers.Timer _lockTimer;

        // ================================================================
        // 构造函数
        // ================================================================
        public MainWindow()
        {
            InitializeComponent();
            _appSettings = new AppSettings();
            InitializeServices();
            WireEvents();
        }

        // ================================================================
        // 服务初始化
        // ================================================================
        private void InitializeServices()
        {
            string basePath = AppDomain.CurrentDomain.BaseDirectory;

            _logService = new LogService(basePath, _appSettings.LogMaxFileSizeMB, _appSettings.LogRetentionDays);
            _db = new DatabaseHelper(Path.Combine(basePath, "Data", "vision_data.db"));
            _userService = new UserService(_logService, _appSettings.AutoLockTimeoutSec);
            _visionService = new VisionService(_logService);
            _simImageService = new SimulationImageService(_logService);
            _recipeService = new RecipeService(basePath, _logService);
            _systemMonitor = new SystemMonitorService(1000);
            _resultService = new InspectionResultService(_db, _logService);

            _tcpConfig = new TcpConfig
            {
                Role = TcpRole.Server,
                IPAddress = "127.0.0.1",
                Port = 2000,
                TimeoutMs = 3000,
                ByteOrder = ByteOrder.BigEndian,
                Encoding = "ASCII",
                AutoReplyEnabled = true,
                AutoReplyMessage = "{Result}"
            };
            _tcpService = new TcpCommunicationService(_logService, _tcpConfig);

            _logService.Info(LogCategory.SYSTEM, "System", "程序启动 — 默认操作员模式 (WPF)");
        }

        // ================================================================
        // 事件绑定
        // ================================================================
        private void WireEvents()
        {
            // 模式切换
            CmbRunMode.SelectionChanged += (s, e) =>
            {
                if (CmbRunMode.SelectedIndex == 1)
                {
                    MessageBox.Show("在线模式需要连接相机，将在 Phase 2 支持", "提示",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    CmbRunMode.SelectedIndex = 0;
                }
            };

            // 视觉服务事件
            _simImageService.OnImageReady += img =>
                Dispatcher.BeginInvoke(new Action(() => { if (_cogDisplay != null) _cogDisplay.Image = img; }));

            _visionService.OnJobChanged += () =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    StsJob.Text = "VPP: " + _visionService.CurrentJobName;
                    // 更新独立编辑器窗口
                    if (_editorForm != null && !_editorForm.IsDisposed)
                    {
                        _editorForm.UpdateToolBlock(_visionService.GetToolBlock());
                        TxtEditorStatus.Text = "VPP: " + _visionService.CurrentJobName;
                    }
                }));

            // TCP 状态
            _tcpService.OnStatusChanged += running =>
                Dispatcher.BeginInvoke(new Action(() =>
                    StsTcp.Text = running ? "  TCP: 已连接  " : "  TCP: 未连接  "));

            // 用户
            _userService.OnUserLogin += user =>
                Dispatcher.BeginInvoke(new Action(() =>
                    StsUser.Text = "  用户: " + user.RoleName + "  "));

            _userService.OnUserLocked += () =>
                Dispatcher.BeginInvoke(new Action(() => ShowUnlockDialog()));

            // 配方
            _recipeService.OnRecipeChanged += r =>
                Dispatcher.BeginInvoke(new Action(RefreshRecipeDisplay));

            // 统计
            _resultService.OnStatisticsChanged += () =>
                Dispatcher.BeginInvoke(new Action(RefreshStatistics));

            // 日志
            _logService.OnLogAdded += entry =>
            {
                if (!_logPaused && PassLogFilter(entry))
                    Dispatcher.BeginInvoke(new Action(() => AddLogRow(entry)));
            };

            // 日志筛选
            ChkLogInfo.Checked += (s, e) => { _filterInfo = true; RefreshLogFilter(); };
            ChkLogInfo.Unchecked += (s, e) => { _filterInfo = false; RefreshLogFilter(); };
            ChkLogWarn.Checked += (s, e) => { _filterWarn = true; RefreshLogFilter(); };
            ChkLogWarn.Unchecked += (s, e) => { _filterWarn = false; RefreshLogFilter(); };
            ChkLogError.Checked += (s, e) => { _filterError = true; RefreshLogFilter(); };
            ChkLogError.Unchecked += (s, e) => { _filterError = false; RefreshLogFilter(); };

            // 系统监控
            _systemMonitor.OnDataRefreshed += () =>
                Dispatcher.BeginInvoke(new Action(() =>
                    StsSysInfo.Text = string.Format("CPU {0:F0}%  RAM {1}  D: {2}",
                        _systemMonitor.CpuUsage,
                        _systemMonitor.GetMemoryInfo(),
                        _systemMonitor.GetDriveFreeSpace("D"))));

            // 键盘快捷键
            this.KeyDown += (s, e) =>
            {
                if (e.Key == Key.F5) RunOnce();
                else if (e.Key == Key.F6) StartContinuousRun();
                else if (e.Key == Key.F7) StopContinuousRun();
            };

            // 活动刷新
            this.MouseMove += (s, e) => _userService.RefreshActivity();
            this.KeyDown += (s, e) => _userService.RefreshActivity();

            // 关闭
            this.Closing += (s, e) =>
            {
                var result = MessageBox.Show("确定要退出程序吗？", "确认退出",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }
                _simImageService?.StopPlayback();
                _tcpService?.Stop();
                _lockTimer?.Stop();
                _logService?.Info(LogCategory.SYSTEM, "System", "程序退出");
            };

            // 加载完成
            this.Loaded += (s, e) => OnLoaded();
        }

        // ================================================================
        // 加载完成后初始化
        // ================================================================
        private void OnLoaded()
        {
            // 注入 VisionPro CogRecordDisplay
            _cogDisplay = new CogRecordDisplay();
            _cogDisplay.Dock = System.Windows.Forms.DockStyle.Fill;
            _cogDisplay.HandleCreated += (s2, ev2) =>
            {
                _cogDisplay.AutoFit = true;
            };
            ImageHost.Child = _cogDisplay;

            // 绑定日志 DataGrid
            LogGrid.ItemsSource = _logRows;

            // 创建独立 WinForms 编辑器窗口（默认隐藏，用户通过按钮或菜单打开）
            _editorForm = new ToolBlockEditorForm(null);
            SetupEditorCallbacks();
            _editorForm.Hide();

            // 加载默认 VPP
            string vppPath = _recipeService.EnsureDefaultVpp();
            _visionService.EnsureDefaultJob(vppPath);

            // 更新编辑器窗口的 ToolBlock
            _editorForm.UpdateToolBlock(_visionService.GetToolBlock());
            TxtEditorStatus.Text = "VPP 已加载 — 点击下方按钮打开编辑器";

            RefreshRecipeDisplay();

            // 锁定检查定时器
            _lockTimer = new System.Timers.Timer(5000);
            _lockTimer.Elapsed += (s2, ev) =>
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (_userService.ShouldAutoLock())
                    {
                        _userService.Lock();
                        ShowUnlockDialog();
                    }
                }));
            _lockTimer.Start();

            _logService.Info(LogCategory.SYSTEM, "System",
                "程序就绪 — 配方路径: " + _recipeService.CurrentRecipePath);
        }

        // ================================================================
        // 菜单/工具栏事件处理
        // ================================================================
        private void MenuOpenImage_Click(object s, RoutedEventArgs e) => OpenImage();
        private void MenuOpenFolder_Click(object s, RoutedEventArgs e) => OpenImageFolder();
        private void MenuLoadVpp_Click(object s, RoutedEventArgs e) => LoadVpp();
        private void MenuSaveVpp_Click(object s, RoutedEventArgs e) => SaveVpp();
        private void MenuSaveVppAs_Click(object s, RoutedEventArgs e) => SaveVppAs();
        private void MenuExit_Click(object s, RoutedEventArgs e) => Application.Current.Shutdown();
        private void MenuRunOnce_Click(object s, RoutedEventArgs e) => RunOnce();
        private void MenuRunCont_Click(object s, RoutedEventArgs e) => StartContinuousRun();
        private void MenuStop_Click(object s, RoutedEventArgs e) => StopContinuousRun();
        private void MenuToolEditor_Click(object s, RoutedEventArgs e) => OpenToolBlockEditor();
        private void MenuVarMonitor_Click(object s, RoutedEventArgs e) => ShowVarMonitor();
        private void MenuRecipeNew_Click(object s, RoutedEventArgs e) => NewRecipe();
        private void MenuRecipeCopy_Click(object s, RoutedEventArgs e) => CopyRecipe();
        private void MenuRecipeRename_Click(object s, RoutedEventArgs e) => RenameRecipe();
        private void MenuRecipeSwitch_Click(object s, RoutedEventArgs e) => SwitchRecipe();
        private void MenuTcpOpen_Click(object s, RoutedEventArgs e) => OpenTcp();
        private void MenuUserLogin_Click(object s, RoutedEventArgs e) => ShowLoginDialog();
        private void MenuUserLogout_Click(object s, RoutedEventArgs e) => Logout();
        private void MenuUserLock_Click(object s, RoutedEventArgs e)
        {
            _userService.Lock();
            ShowUnlockDialog();
        }
        private void MenuUserChPwd_Click(object s, RoutedEventArgs e) => ChangePassword();
        private void MenuHelpAbout_Click(object s, RoutedEventArgs e) =>
            MessageBox.Show("VisionInspection v1.0\n机器视觉检测系统\n基于 Cognex VisionPro 9.0\nWPF Edition\n\n(c) 2026", "关于",
                MessageBoxButton.OK, MessageBoxImage.Information);

        private void BtnOpenImage_Click(object s, RoutedEventArgs e) => OpenImage();
        private void BtnOpenFolder_Click(object s, RoutedEventArgs e) => OpenImageFolder();
        private void BtnLoadVpp_Click(object s, RoutedEventArgs e) => LoadVpp();
        private void BtnSaveVpp_Click(object s, RoutedEventArgs e) => SaveVpp();
        private void BtnRunOnce_Click(object s, RoutedEventArgs e) => RunOnce();
        private void BtnRunCont_Click(object s, RoutedEventArgs e) => StartContinuousRun();
        private void BtnStop_Click(object s, RoutedEventArgs e) => StopContinuousRun();
        private void BtnOpenEditor_Click(object s, RoutedEventArgs e) => OpenToolBlockEditor();

        private void BtnLogPause_Click(object s, RoutedEventArgs e)
        {
            _logPaused = !_logPaused;
            ((Button)s).Content = _logPaused ? "继续" : "暂停";
        }
        private void BtnLogClear_Click(object s, RoutedEventArgs e)
        {
            _logRows.Clear();
            _logService.ClearMemoryLogs();
        }

        // ================================================================
        // 图像操作
        // ================================================================
        private void OpenImage()
        {
            var dlg = new System.Windows.Forms.OpenFileDialog
            {
                Title = "打开图片",
                Filter = "图片|*.bmp;*.jpg;*.jpeg;*.png;*.tif;*.tiff",
                InitialDirectory = _lastImagePath
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _lastImagePath = System.IO.Path.GetDirectoryName(dlg.FileName);
                _simImageService.LoadImageDirect(dlg.FileName);
                _logService.Info(LogCategory.CAMERA, "System", "加载图片: " + dlg.FileName);
            }
        }

        private void OpenImageFolder()
        {
            var dlg = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "选择图片文件夹",
                SelectedPath = _lastFolderPath ?? _lastImagePath ?? ""
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                _lastFolderPath = dlg.SelectedPath;
                _simImageService.LoadImageSequence(dlg.SelectedPath);
            }
        }

        // ================================================================
        // VPP 操作
        // ================================================================
        private void LoadVpp()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "加载 VPP 作业",
                Filter = "VisionPro 作业|*.vpp",
                InitialDirectory = _recipeService.CurrentRecipePath
            };
            if (dlg.ShowDialog() == true)
                _visionService.LoadJob(dlg.FileName);
        }

        private void SaveVpp()
        {
            if (!_visionService.IsJobLoaded) { ShowInfo("请先加载或新建 VPP 作业"); return; }
            if (string.IsNullOrEmpty(_visionService.CurrentJobPath)) { SaveVppAs(); return; }
            _visionService.SaveJob();
        }

        private void SaveVppAs()
        {
            string defaultPath = _recipeService.EnsureDefaultVpp();
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "另存 VPP",
                Filter = "VisionPro 作业|*.vpp",
                FileName = "DefaultJob.vpp",
                InitialDirectory = Path.GetDirectoryName(defaultPath)
            };
            if (dlg.ShowDialog() == true)
                _visionService.SaveJobAs(dlg.FileName);
        }

        // ================================================================
        // 运行
        // ================================================================
        private void RunOnce()
        {
            if (!_visionService.IsJobLoaded)
            {
                ShowInfo("请先加载或新建 VPP 作业\n通过 文件→加载VPP 或配方→切换配方");
                return;
            }

            if (_simImageService.CurrentImage != null)
            {
                _logService.Info(LogCategory.JOB, "System",
                    string.Format("运行: {0} ({1}×{2})",
                        _simImageService.CurrentFileName ?? "未知文件",
                        _simImageService.CurrentImage.Width,
                        _simImageService.CurrentImage.Height));
                _visionService.SetInputImage(_simImageService.CurrentImage);
            }
            else
            {
                _logService.Warn(LogCategory.JOB, "System", "RunOnce: 没有加载图片，请先打开图片");
                ShowInfo("请先加载图片\n通过 文件→打开图片 或 图片文件夹");
                return;
            }

            var result = _visionService.RunOnce();
            _resultService.RecordResult(result);
            StsFrame.Text = string.Format("帧率: {0:F1} FPS", 1000.0 / Math.Max(result.CycleTimeMs, 1));
            try
            {
                var runRecord = _visionService.GetLastRunRecord();
                if (runRecord != null) _cogDisplay.Record = runRecord;
                else if (_simImageService.CurrentImage != null) _cogDisplay.Image = _simImageService.CurrentImage;
            }
            catch { }
            UpdateVppOutputDisplay(result);

            // 图片文件夹模式：运行后自动切下一张
            if (_simImageService.SourceType == ImageSourceType.ImageSequence ||
                _simImageService.SourceType == ImageSourceType.VideoFile)
            {
                _simImageService.StepNext();
            }
        }

        private System.Windows.Threading.DispatcherTimer _runTimer;

        private void StartContinuousRun()
        {
            if (_isRunning) return;
            if (!_visionService.IsJobLoaded)
            {
                ShowInfo("请先加载或新建 VPP 作业");
                return;
            }

            _isRunning = true;

            // 单图或图片序列都用定时器反复执行
            _runTimer = new System.Windows.Threading.DispatcherTimer();
            _runTimer.Interval = TimeSpan.FromMilliseconds(_appSettings.SimulationIntervalMs);
            _runTimer.Tick += (s2, ev2) =>
            {
                if (!_isRunning) { _runTimer.Stop(); return; }
                ExecuteRun();
            };
            _runTimer.Start();

            _logService.Info(LogCategory.SYSTEM, "System", "连续运行开始 (" +
                (_simImageService.SourceType == ImageSourceType.SingleImage ? "单图模式" : "序列模式") + ")");
        }

        private void StopContinuousRun()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _runTimer?.Stop();
            _runCount = 0;
            _simImageService.StopPlayback();
            _logService.Info(LogCategory.SYSTEM, "System", "连续运行停止");
        }

        private int _runCount;

        private void ExecuteRun()
        {
            if (!_isRunning || !_visionService.IsJobLoaded) return;

            // 记录当前处理的图片信息
            _runCount++;
            string imgInfo = string.Format("[第{0}次] {1} | {2}×{3} | SourceType={4}",
                _runCount,
                _simImageService.CurrentFileName ?? "未知",
                _simImageService.CurrentImage?.Width ?? 0,
                _simImageService.CurrentImage?.Height ?? 0,
                _simImageService.SourceType);
            _logService.Info(LogCategory.JOB, "System", "连续运行 " + imgInfo);

            if (_simImageService.CurrentImage != null)
                _visionService.SetInputImage(_simImageService.CurrentImage);

            var result = _visionService.RunOnce();
            _resultService.RecordResult(result);

            StsFrame.Text = string.Format("帧率: {0:F1} FPS", 1000.0 / Math.Max(result.CycleTimeMs, 1));
            UpdateVppOutputDisplay(result);
            try
            {
                var record = _visionService.GetLastRunRecord();
                if (record != null) _cogDisplay.Record = record;
                else if (_simImageService.CurrentImage != null)
                    _cogDisplay.Image = _simImageService.CurrentImage;
            }
            catch { }

            // 图片文件夹模式：运行后切下一张，并刷新显示
            if (_simImageService.SourceType == ImageSourceType.ImageSequence ||
                _simImageService.SourceType == ImageSourceType.VideoFile)
            {
                _simImageService.StepNext();
                // 新图立即显示
                if (_simImageService.CurrentImage != null)
                    _cogDisplay.Image = _simImageService.CurrentImage;
            }
        }

        // ================================================================
        // 配方操作
        // ================================================================
        private void NewRecipe()
        {
            if (!CheckEngineer()) return;
            string input = Microsoft.VisualBasic.Interaction.InputBox(
                "请输入新配方名称:", "新建配方", _recipeService.GetNextRecipeName());
            if (!string.IsNullOrWhiteSpace(input))
            {
                try { _recipeService.CreateRecipe(input); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void CopyRecipe()
        {
            if (!CheckEngineer()) return;
            var recipes = _recipeService.GetAllRecipes();
            if (recipes.Count == 0) { ShowInfo("没有可复制的配方"); return; }
            string src = Microsoft.VisualBasic.Interaction.InputBox("源配方名称:", "复制配方", recipes[0].Name);
            string dst = Microsoft.VisualBasic.Interaction.InputBox("新配方名称:", "复制配方", _recipeService.GetNextRecipeName());
            if (!string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(dst))
            {
                try { _recipeService.CopyRecipe(src, dst); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void RenameRecipe()
        {
            if (!CheckEngineer()) return;
            string src = Microsoft.VisualBasic.Interaction.InputBox("当前配方名称:", "重命名配方", _recipeService.CurrentRecipeName);
            string dst = Microsoft.VisualBasic.Interaction.InputBox("新配方名称:", "重命名配方", "");
            if (!string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(dst))
            {
                try { _recipeService.RenameRecipe(src, dst); }
                catch (Exception ex) { ShowError(ex.Message); }
            }
        }

        private void SwitchRecipe()
        {
            var recipes = _recipeService.GetAllRecipes();
            string names = string.Join(", ", recipes.Select(r => r.Name));
            string sel = Microsoft.VisualBasic.Interaction.InputBox(
                "可用配方: " + names + "\n\n输入配方名称:", "切换配方", _recipeService.CurrentRecipeName);
            if (!string.IsNullOrWhiteSpace(sel))
            {
                if (_recipeService.SwitchRecipe(sel))
                {
                    string vppPath = _recipeService.CurrentVppPath;
                    if (File.Exists(vppPath))
                        _visionService.LoadJob(vppPath);
                    else
                        _visionService.NewJob();
                    RefreshRecipeDisplay();
                }
                else
                {
                    ShowError("配方 '" + sel + "' 不存在");
                }
            }
        }

        private void RefreshRecipeDisplay()
        {
            StsRecipe.Text = "配方: " + _recipeService.CurrentRecipeName;
            StsJob.Text = "VPP: " + (_visionService.IsJobLoaded ? _visionService.CurrentJobName : "未加载");
        }

        // ================================================================
        // 通讯
        // ================================================================
        private void OpenTcp()
        {
            if (_tcpWindow == null || !_tcpWindow.IsLoaded)
            {
                _tcpWindow = new TcpWindow(_tcpService, _tcpConfig);
                _tcpWindow.Owner = this;
                _tcpWindow.Closed += (s2, ev2) => _tcpWindow = null;
                WireTcpCallbacks();
            }
            _tcpWindow.Show();
            _tcpWindow.Activate();
        }

        private void WireTcpCallbacks()
        {
            if (_tcpWindow == null) return;
            _tcpWindow.ReconfigureAndStart = (role, ip, port, enc, byteOrder,
                timeout, startDelim, endDelim, separator,
                autoReply, autoReplyMsg, autoReconnect, reconnectInterval, reconnectMax) =>
            {
                _tcpService.Stop();
                var cfg = new TcpConfig
                {
                    Role = role == "Server" ? TcpRole.Server : TcpRole.Client,
                    IPAddress = ip, Port = port, Encoding = enc,
                    ByteOrder = byteOrder == "BigEndian" ? ByteOrder.BigEndian : ByteOrder.LittleEndian,
                    StartDelimiter = startDelim, EndDelimiter = endDelim, Separator = separator,
                    AutoReplyEnabled = autoReply, AutoReplyMessage = autoReplyMsg,
                    TimeoutMs = int.TryParse(timeout, out int to) ? to : 3000,
                };
                _tcpConfig = cfg;
                _tcpService = new TcpCommunicationService(_logService, cfg);
                _tcpService.OnStatusChanged += running =>
                    Dispatcher.BeginInvoke(new Action(() =>
                        StsTcp.Text = running ? "  TCP: 已连接  " : "  TCP: 未连接  "));
                _tcpService.OnError += errMsg =>
                    Dispatcher.BeginInvoke(new Action(() =>
                        _logService.Error(LogCategory.TCP, "System", "TCP 错误: " + errMsg)));
                _logService.Info(LogCategory.SETTING, "System",
                    "TCP 已启动: " + role + " " + ip + ":" + port);
                return _tcpService;
            };
            _tcpWindow.StopService = () => _tcpService.Stop();
        }

        // ================================================================
        // 用户
        // ================================================================
        private void ShowLoginDialog()
        {
            var dlg = new LoginDialog(_userService, false);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void ShowUnlockDialog()
        {
            var dlg = new LoginDialog(_userService, true);
            dlg.Owner = this;
            dlg.ShowDialog();
        }

        private void Logout()
        {
            _userService.Logout();
            StsUser.Text = "  用户: 操作员  ";
            _logService.Info(LogCategory.SYSTEM, "System", "已退回操作员模式");
        }

        private void ChangePassword()
        {
            var user = _userService.CurrentUser;
            if (user == null || user.Role == UserRole.Operator)
            {
                ShowInfo("请先登录为工程师或管理员");
                return;
            }
            string oldPwd = Microsoft.VisualBasic.Interaction.InputBox("旧密码:", "修改密码", "");
            string newPwd = Microsoft.VisualBasic.Interaction.InputBox("新密码:", "修改密码", "");
            if (!string.IsNullOrWhiteSpace(oldPwd) && !string.IsNullOrWhiteSpace(newPwd))
            {
                if (_userService.ChangePassword(user.UserName, oldPwd, newPwd))
                    ShowInfo("密码修改成功，已保存到 Config\\users.ini");
                else
                    ShowError("旧密码错误");
            }
        }

        // ================================================================
        // 变量监视
        // ================================================================
        private void ShowVarMonitor()
        {
            if (!_visionService.IsJobLoaded) { ShowInfo("请先加载作业"); return; }
            var vars = _visionService.GetToolBlockVariables();
            string msg = string.Join("\n", vars.Select(kv => kv.Key + " = " + kv.Value));
            MessageBox.Show(string.IsNullOrEmpty(msg) ? "当前无输出变量" : msg,
                "变量监视", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ================================================================
        // 独立 WinForms 编辑器窗口
        // ================================================================
        private void SetupEditorCallbacks()
        {
            if (_editorForm == null) return;
            _editorForm.SaveCallback = () =>
            {
                if (_visionService.IsJobLoaded)
                {
                    if (string.IsNullOrEmpty(_visionService.CurrentJobPath))
                    {
                        // 新建的作业，需要另存为
                        string defaultPath = _recipeService.EnsureDefaultVpp();
                        var dlg = new Microsoft.Win32.SaveFileDialog
                        {
                            Title = "保存 VPP 作业",
                            Filter = "VisionPro 作业|*.vpp",
                            FileName = "DefaultJob.vpp",
                            InitialDirectory = System.IO.Path.GetDirectoryName(defaultPath)
                        };
                        if (dlg.ShowDialog() == true)
                        {
                            _visionService.SaveJobAs(dlg.FileName);
                            return true;
                        }
                        return false;
                    }
                    else
                    {
                        return _visionService.SaveJob();
                    }
                }
                return false;
            };
            _editorForm.GetJobNameCallback = () => _visionService.CurrentJobName;
        }

        private void OpenToolBlockEditor()
        {
            if (_editorForm == null || _editorForm.IsDisposed)
            {
                _editorForm = new ToolBlockEditorForm(_visionService.GetToolBlock());
                SetupEditorCallbacks();
            }
            _editorForm.UpdateToolBlock(_visionService.GetToolBlock());

            if (_visionService.IsJobLoaded)
            {
                _editorForm.Text = "VisionPro ToolBlock 编辑器 - " + _visionService.CurrentJobName;
                TxtEditorStatus.Text = "编辑器已打开: " + _visionService.CurrentJobName;
            }
            _editorForm.Show();
            _editorForm.Activate();
        }

        // ================================================================
        // 统计刷新
        // ================================================================
        private void RefreshStatistics()
        {
            if (_resultService == null) return;
            TxtStatOk.Text = _resultService.OkCount.ToString();
            TxtStatNg.Text = _resultService.NgCount.ToString();
            TxtStatTotal.Text = _resultService.TotalCount.ToString();
            TxtStatYield.Text = _resultService.YieldRate.ToString("F1") + "%";
            TxtStatCycle.Text = _resultService.LastCycleTimeMs.ToString("F1") + " ms";
            UpdateStatsBarChart();
        }

        private void BtnStatClear_Click(object s, RoutedEventArgs e)
        {
            _resultService?.ResetCounters();
            UpdateStatsBarChart();
            _logService.Info(LogCategory.SYSTEM, "System", "统计数据已清除");
        }

        /// <summary>刷新柱状图</summary>
        private void UpdateStatsBarChart()
        {
            if (_resultService == null) return;

            double maxVal = Math.Max(_resultService.TotalCount, 1);
            DrawBar(BarOk, _resultService.OkCount / maxVal, "#009600", _resultService.OkCount.ToString());
            DrawBar(BarNg, _resultService.NgCount / maxVal, "#DC3545", _resultService.NgCount.ToString());
            DrawBar(BarTotal, _resultService.TotalCount / maxVal, "#0078D7", _resultService.TotalCount.ToString());
            DrawBar(BarYield, _resultService.YieldRate / 100.0, "#17A2B8", _resultService.YieldRate.ToString("F1") + "%");
        }

        private void DrawBar(StackPanel panel, double ratio, string color, string value)
        {
            panel.Children.Clear();
            int barHeight = (int)(ratio * 100);
            if (barHeight < 4 && ratio > 0) barHeight = 4;

            // 数值标签
            panel.Children.Add(new System.Windows.Controls.TextBlock
            {
                Text = value,
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new System.Windows.Thickness(0, 0, 0, 2)
            });

            // 柱子
            var bar = new System.Windows.Controls.Border
            {
                Height = barHeight,
                Width = 46,
                Background = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFrom(color),
                CornerRadius = new System.Windows.CornerRadius(4, 4, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            panel.Children.Add(bar);
        }

        // ================================================================
        // 日志
        // ================================================================
        private bool PassLogFilter(LogEntry entry)
        {
            if (entry.Level == LogLevel.ERROR && !_filterError) return false;
            if (entry.Level == LogLevel.WARN && !_filterWarn) return false;
            if (entry.Level == LogLevel.INFO && !_filterInfo) return false;
            return true;
        }

        private void RefreshLogFilter()
        {
            _logRows.Clear();
            foreach (var entry in _logService.GetLogs())
            {
                if (PassLogFilter(entry))
                    AddLogRow(entry);
            }
        }

        private void AddLogRow(LogEntry entry)
        {
            // 最新日志插入到最前面（置顶）
            _logRows.Insert(0, new LogRow
            {
                Time = entry.Timestamp.ToString("HH:mm:ss.fff"),
                Category = entry.Category.ToString(),
                Level = entry.Level.ToString(),
                User = entry.UserName,
                Message = entry.Message
            });
            // 限制内存中最多 500 行
            while (_logRows.Count > 500)
                _logRows.RemoveAt(_logRows.Count - 1);
        }

        // ================================================================
        // 权限检查
        // ================================================================
        private void UpdateVppOutputDisplay(InspectionResult result)
        {
            // VPP 输出变量写入日志
            if (result.Variables != null && result.Variables.Count > 0)
            {
                foreach (var kv in result.Variables)
                {
                    string val = kv.Value?.ToString() ?? "null";
                    _logService.Info(LogCategory.JOB, "System",
                        string.Format("输出 [{0}] = {1}", kv.Key, val));
                }
            }
            // VPP 报错写入日志
            if (result.ErrorMessages != null)
            {
                foreach (var err in result.ErrorMessages)
                    _logService.Error(LogCategory.JOB, "System", err);
            }
            // 写入判定结果
            _logService.Info(LogCategory.JOB, "System",
                string.Format("判定: {0}  耗时: {1:F1}ms  文件: {2}",
                result.VerdictDisplay, result.CycleTimeMs,
                _simImageService.CurrentFileName ?? "--"));
        }

        private bool CheckEngineer()
        {
            if (!_userService.IsEngineerOrAbove)
            {
                ShowInfo("需要工程师或管理员权限\n请通过 用户→登录 切换账号");
                return false;
            }
            return true;
        }

        private void ShowInfo(string msg) =>
            MessageBox.Show(msg, "提示", MessageBoxButton.OK, MessageBoxImage.Information);

        private void ShowError(string msg) =>
            MessageBox.Show(msg, "错误", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    /// <summary>
    /// 日志行数据类（DataGrid 绑定）
    /// </summary>
    public class LogRow
    {
        public string Time { get; set; }
        public string Category { get; set; }
        public string Level { get; set; }
        public string User { get; set; }
        public string Message { get; set; }
    }
}
