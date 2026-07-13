using System;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace VisionEnvironmentTool
{
    static class Program
    {
        /// <summary>
        /// 应用程序主入口点。检查管理员权限，不足则重新以管理员身份启动。
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // 检查管理员权限
            if (!IsAdministrator())
            {
                // 重新以管理员身份启动
                try
                {
                    ProcessStartInfo psi = new ProcessStartInfo
                    {
                        FileName = Application.ExecutablePath,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch (Exception)
                {
                    MessageBox.Show(
                        "本工具需要以管理员权限运行。\n" +
                        "请右键点击程序 → 以管理员身份运行。",
                        "权限不足",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }
                return;
            }

            Application.Run(new MainForm());
        }

        /// <summary>
        /// 检测当前进程是否以管理员权限运行
        /// </summary>
        private static bool IsAdministrator()
        {
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
        }
    }
}