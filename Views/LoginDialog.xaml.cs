using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionInspection.Services;

namespace VisionInspection.Views
{
    public partial class LoginDialog : Window
    {
        private readonly UserService _userService;
        private readonly bool _unlockMode;

        public LoginDialog(UserService userService, bool unlockMode = false)
        {
            InitializeComponent();
            _userService = userService;
            _unlockMode = unlockMode;

            if (_unlockMode)
            {
                Title = "解锁";
                CmbUser.IsEnabled = false;
            }
        }

        private void TxtPassword_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) DoLogin();
        }

        private void BtnLogin_Click(object sender, RoutedEventArgs e)
        {
            DoLogin();
        }

        private void DoLogin()
        {
            string userName = (CmbUser.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "Admin";
            string password = TxtPassword.Password;

            if (string.IsNullOrEmpty(password))
            {
                MessageBox.Show("请输入密码", "提示");
                return;
            }

            bool success;
            if (_unlockMode)
                success = _userService.Unlock(password);
            else
                success = _userService.Login(userName, password);

            if (success)
                Close();
            else
                MessageBox.Show(_unlockMode ? "密码错误" : "用户名或密码错误", "登录失败",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
