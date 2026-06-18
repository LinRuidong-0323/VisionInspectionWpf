using System;
using System.Collections.Generic;
using System.IO;

namespace VisionInspection.Models
{
    /// <summary>
    /// 配方实体
    /// 一个配方 = Recipe 文件夹下的一个子文件夹，包含 VPP 作业、相机参数、通讯配置
    /// </summary>
    public class Recipe
    {
        /// <summary>配方名称（即文件夹名）</summary>
        public string Name { get; set; }

        /// <summary>配方文件夹完整路径</summary>
        public string FolderPath { get; set; }

        /// <summary>创建时间</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>配方下的 VPP 文件列表</summary>
        public List<string> VppFiles { get; set; }

        public Recipe()
        {
            Name = "";
            FolderPath = "";
            CreatedAt = DateTime.Now;
            VppFiles = new List<string>();
        }

        /// <summary>
        /// 获取默认 VPP 文件路径（首个 .vpp 文件）
        /// </summary>
        public string DefaultVppPath
        {
            get
            {
                if (VppFiles.Count > 0)
                    return VppFiles[0];
                return Path.Combine(FolderPath, "DefaultJob.vpp");
            }
        }

        public override string ToString()
        {
            return Name;
        }
    }
}
