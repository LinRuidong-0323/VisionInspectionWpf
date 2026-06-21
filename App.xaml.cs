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
                string msg = ex.ExceptionObject?.ToString() ?? "未知错误";
                LogToFile("严重错误: " + msg);
                MessageBox.Show("严重错误: " + msg, "错误");
            };
            DispatcherUnhandledException += (s, ex) =>
            {
                LogToFile("未处理的异常: " + ex.Exception);
                MessageBox.Show("未处理的异常: " + ex.Exception.Message, "错误");
                ex.Handled = true;
            };
        }

        private static void LogToFile(string msg)
        {
            try
            {
                string dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Log");
                System.IO.Directory.CreateDirectory(dir);
                string file = System.IO.Path.Combine(dir,
                    string.Format("crash_{0}.log", DateTime.Now.ToString("yyyyMMdd")));
                System.IO.File.AppendAllText(file,
                    string.Format("[{0:yyyy-MM-dd HH:mm:ss}] {1}\n", DateTime.Now, msg));
            }
            catch { }
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
