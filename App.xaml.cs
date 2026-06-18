using System;
using System.Threading;
using System.Windows;

namespace VisionInspection
{
    public partial class App : Application
    {
        private static Mutex _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 禁止多开
            bool createdNew;
            _mutex = new Mutex(true, "VisionInspection_SingleInstance_V2", out createdNew);
            if (!createdNew)
            {
                MessageBox.Show("VisionInspection 已经在运行中。", "提示",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                _mutex = null;
                Shutdown();
                return;
            }

            base.OnStartup(e);
            AppDomain.CurrentDomain.UnhandledException += (s, ex) =>
            {
                MessageBox.Show("严重错误: " + (ex.ExceptionObject?.ToString() ?? "未知"),
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            };
            DispatcherUnhandledException += (s, ex) =>
            {
                MessageBox.Show("未处理的异常: " + ex.Exception.Message,
                    "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                ex.Handled = true;
            };
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_mutex != null)
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
                _mutex = null;
            }
            base.OnExit(e);
        }
    }
}
