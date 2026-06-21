using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using VisionInspection.Data;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 用户与权限服务
    /// - 打开软件默认为操作员（不弹登录窗）
    /// - 用户可手动切换登录级别
    /// - 密码从 Config\users.ini 读取
    /// - 三级权限：管理员(全权限) / 工程师(可改视觉&通讯) / 操作员(只看)
    /// </summary>
    public class UserService
    {
        private readonly ILogService _logService;
        private readonly string _iniFilePath;
        private User _currentUser;
        private DateTime _lastActivity;
        private int _autoLockTimeoutSec;

        // 三个内置账号的密码
        private Dictionary<string, string> _passwords;

        /// <summary>当前登录用户</summary>
        public User CurrentUser => _currentUser;

        /// <summary>是否已登录（切换为非操作员角色）</summary>
        public bool IsLoggedInElevated =>
            _currentUser != null && _currentUser.Role != UserRole.Operator;

        /// <summary>是否已锁定</summary>
        public bool IsLocked { get; private set; }

        /// <summary>用户登录事件</summary>
        public event Action<User> OnUserLogin;

        /// <summary>用户锁定事件</summary>
        public event Action OnUserLocked;

        /// <summary>用户解锁事件</summary>
        public event Action OnUserUnlocked;

        public UserService(ILogService logService, int autoLockTimeoutSec = 300)
        {
            _logService = logService;
            _autoLockTimeoutSec = autoLockTimeoutSec;
            _iniFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "users.ini");

            // 加载密码配置
            LoadPasswords();

            // 默认以管理员身份进入
            _currentUser = new User
            {
                UserName = "Admin",
                Role = UserRole.Admin,
                IsEnabled = true
            };
        }

        /// <summary>
        /// 从 users.ini 加载密码
        /// </summary>
        private void LoadPasswords()
        {
            _passwords = new Dictionary<string, string>
            {
                { "Admin", "admin123" },
                { "Engineer", "admin123" },
                { "Operator", "admin123" }
            };

            try
            {
                if (File.Exists(_iniFilePath))
                {
                    var lines = File.ReadAllLines(_iniFilePath, Encoding.UTF8);
                    bool inUsers = false;
                    foreach (var line in lines)
                    {
                        string trimmed = line.Trim();
                        if (trimmed == "[Users]")
                        {
                            inUsers = true;
                            continue;
                        }
                        if (trimmed.StartsWith("["))
                        {
                            inUsers = false;
                            continue;
                        }
                        if (inUsers && trimmed.Contains("="))
                        {
                            var parts = trimmed.Split('=');
                            if (parts.Length >= 2)
                            {
                                string key = parts[0].Trim();
                                string val = parts[1].Trim();
                                if (_passwords.ContainsKey(key))
                                    _passwords[key] = val;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.SYSTEM, "System", $"加载密码配置失败: {ex.Message}，使用默认密码");
            }
        }

        /// <summary>
        /// 保存密码到 users.ini
        /// </summary>
        public bool SavePasswords()
        {
            try
            {
                string dir = Path.GetDirectoryName(_iniFilePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var sb = new StringBuilder();
                sb.AppendLine("[Users]");
                foreach (var kv in _passwords)
                    sb.AppendLine($"{kv.Key}={kv.Value}");
                sb.AppendLine();
                sb.AppendLine("[Settings]");
                sb.AppendLine($"AutoLockTimeoutSec={_autoLockTimeoutSec}");

                File.WriteAllText(_iniFilePath, sb.ToString(), Encoding.UTF8);
                return true;
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.SYSTEM, "System", $"保存密码配置失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 以指定角色登录
        /// </summary>
        public bool Login(string userName, string password)
        {
            string normalizedName = NormalizeUserName(userName);

            if (!_passwords.ContainsKey(normalizedName))
            {
                _logService?.Warn(LogCategory.SYSTEM, "System", $"登录失败：用户 '{userName}' 不存在");
                return false;
            }

            if (_passwords[normalizedName] != password)
            {
                _logService?.Warn(LogCategory.SYSTEM, "System", $"登录失败：用户 '{userName}' 密码错误");
                return false;
            }

            UserRole role = GetRoleForUser(normalizedName);
            _currentUser = new User
            {
                UserName = normalizedName,
                Role = role,
                IsEnabled = true
            };
            _lastActivity = DateTime.Now;
            IsLocked = false;

            _logService?.Info(LogCategory.SYSTEM, _currentUser.UserName,
                $"用户 '{_currentUser.UserName}' ({_currentUser.RoleName}) 已登录");
            OnUserLogin?.Invoke(_currentUser);
            return true;
        }

        /// <summary>
        /// 退出登录，退回操作员
        /// </summary>
        public void Logout()
        {
            if (_currentUser != null)
            {
                string oldUser = _currentUser.UserName;
                _currentUser = new User
                {
                    UserName = "Operator",
                    Role = UserRole.Operator,
                    IsEnabled = true
                };
                IsLocked = false;
                _logService?.Info(LogCategory.SYSTEM, oldUser, $"已注销，退回操作员");
                OnUserLogin?.Invoke(_currentUser);
            }
        }

        /// <summary>
        /// 修改指定用户的密码
        /// </summary>
        public bool ChangePassword(string userName, string oldPassword, string newPassword)
        {
            string normalizedName = NormalizeUserName(userName);

            if (!_passwords.ContainsKey(normalizedName))
                return false;

            if (_passwords[normalizedName] != oldPassword)
                return false;

            _passwords[normalizedName] = newPassword;
            bool saved = SavePasswords();

            _logService?.Info(LogCategory.SETTING, _currentUser?.UserName ?? userName,
                $"用户 '{normalizedName}' 密码已修改");

            return saved;
        }

        /// <summary>
        /// 锁定界面
        /// </summary>
        public void Lock()
        {
            if (_currentUser != null && !IsLocked)
            {
                IsLocked = true;
                _logService?.Info(LogCategory.SYSTEM, _currentUser.UserName, "界面已锁定");
                OnUserLocked?.Invoke();
            }
        }

        /// <summary>
        /// 解锁界面
        /// </summary>
        public bool Unlock(string password)
        {
            if (_currentUser == null || !IsLocked)
                return false;

            if (_passwords.ContainsKey(_currentUser.UserName) &&
                _passwords[_currentUser.UserName] == password)
            {
                IsLocked = false;
                _lastActivity = DateTime.Now;
                OnUserUnlocked?.Invoke();
                return true;
            }
            return false;
        }

        public void RefreshActivity()
        {
            _lastActivity = DateTime.Now;
        }

        public bool ShouldAutoLock()
        {
            if (_autoLockTimeoutSec <= 0 || IsLocked)
                return false;
            // 只有非操作员才需要自动锁定
            if (_currentUser == null || _currentUser.Role == UserRole.Operator)
                return false;
            return (DateTime.Now - _lastActivity).TotalSeconds >= _autoLockTimeoutSec;
        }

        public bool HasPermission(UserRole requiredRole)
        {
            if (_currentUser == null || IsLocked)
                return false;
            return _currentUser.Role <= requiredRole;
        }

        public bool IsAdmin => _currentUser != null && _currentUser.Role == UserRole.Admin;
        public bool IsEngineerOrAbove => _currentUser != null && _currentUser.Role <= UserRole.Engineer;

        /// <summary>
        /// 获取所有可用用户名
        /// </summary>
        public List<string> GetAvailableUsers()
        {
            return _passwords.Keys.ToList();
        }

        /// <summary>
        /// 获取用户的密码（仅 Admin 可用）
        /// </summary>
        public string GetUserPassword(string userName)
        {
            string name = NormalizeUserName(userName);
            return _passwords.ContainsKey(name) ? _passwords[name] : "";
        }

        private string NormalizeUserName(string userName)
        {
            // 支持中文和英文名称
            switch (userName.ToLower())
            {
                case "admin": case "管理员": return "Admin";
                case "engineer": case "工程师": return "Engineer";
                case "operator": case "操作员": return "Operator";
                default: return userName;
            }
        }

        private UserRole GetRoleForUser(string normalizedName)
        {
            switch (normalizedName)
            {
                case "Admin": return UserRole.Admin;
                case "Engineer": return UserRole.Engineer;
                case "Operator": return UserRole.Operator;
                default: return UserRole.Operator;
            }
        }
    }
}
