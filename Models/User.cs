using System;

namespace VisionInspection.Models
{
    /// <summary>
    /// 用户实体
    /// </summary>
    public class User
    {
        /// <summary>用户ID（自增主键）</summary>
        public int Id { get; set; }

        /// <summary>用户名（唯一）</summary>
        public string UserName { get; set; }

        /// <summary>密码哈希值</summary>
        public string PasswordHash { get; set; }

        /// <summary>角色（Admin/Engineer/Operator）</summary>
        public UserRole Role { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>最后登录时间</summary>
        public DateTime? LastLoginAt { get; set; }

        /// <summary>是否启用</summary>
        public bool IsEnabled { get; set; }

        public User()
        {
            UserName = "";
            PasswordHash = "";
            Role = UserRole.Operator;
            CreatedAt = DateTime.Now;
            IsEnabled = true;
        }

        /// <summary>
        /// 获取角色的中文描述
        /// </summary>
        public string RoleName
        {
            get
            {
                switch (Role)
                {
                    case UserRole.Admin: return "管理员";
                    case UserRole.Engineer: return "工程师";
                    case UserRole.Operator: return "操作员";
                    default: return "未知";
                }
            }
        }
    }
}
