using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 配方管理服务
    /// 配方 = Recipe 文件夹下的子文件夹，每个文件夹包含 VPP 作业等
    /// 结构：Recipe\1\xxx.vpp, Recipe\2\xxx.vpp ...
    /// </summary>
    public class RecipeService
    {
        private readonly string _recipeRoot;
        private readonly ILogService _logService;
        private string _currentRecipeName;

        /// <summary>当前配方名称</summary>
        public string CurrentRecipeName => _currentRecipeName;

        /// <summary>当前配方的 VPP 文件路径</summary>
        public string CurrentVppPath
        {
            get
            {
                var recipe = GetRecipe(_currentRecipeName);
                return recipe?.DefaultVppPath ?? "";
            }
        }

        /// <summary>当前配方文件夹路径</summary>
        public string CurrentRecipePath =>
            Path.Combine(_recipeRoot, _currentRecipeName);

        /// <summary>配方变更事件</summary>
        public event Action<Recipe> OnRecipeChanged;

        /// <summary>配方列表变更事件</summary>
        public event Action OnRecipeListChanged;

        public RecipeService(string basePath, ILogService logService)
        {
            _recipeRoot = Path.Combine(basePath, "Recipe");
            _logService = logService;

            // 确保配方根目录存在
            Directory.CreateDirectory(_recipeRoot);

            // 如果没有默认配方，创建 "1"
            if (!Directory.Exists(Path.Combine(_recipeRoot, "1")))
            {
                CreateRecipe("1");
            }

            _currentRecipeName = "1";
        }

        /// <summary>
        /// 获取所有配方列表
        /// </summary>
        public List<Recipe> GetAllRecipes()
        {
            var recipes = new List<Recipe>();

            try
            {
                foreach (var dir in Directory.GetDirectories(_recipeRoot))
                {
                    var recipe = new Recipe
                    {
                        Name = Path.GetFileName(dir),
                        FolderPath = dir,
                        CreatedAt = Directory.GetCreationTime(dir),
                        VppFiles = Directory.GetFiles(dir, "*.vpp")
                            .Select(f => Path.GetFileName(f))
                            .ToList()
                    };
                    recipes.Add(recipe);
                }
            }
            catch (Exception ex)
            {
                _logService?.Error(LogCategory.RECIPE, "System", $"获取配方列表失败: {ex.Message}");
            }

            return recipes.OrderBy(r => r.Name).ToList();
        }

        /// <summary>
        /// 获取指定配方
        /// </summary>
        public Recipe GetRecipe(string name)
        {
            string path = Path.Combine(_recipeRoot, name);
            if (!Directory.Exists(path))
                return null;

            return new Recipe
            {
                Name = name,
                FolderPath = path,
                CreatedAt = Directory.GetCreationTime(path),
                VppFiles = Directory.GetFiles(path, "*.vpp")
                    .Select(f => Path.GetFileName(f))
                    .ToList()
            };
        }

        /// <summary>
        /// 创建新配方
        /// </summary>
        public Recipe CreateRecipe(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("配方名称不能为空");

            string path = Path.Combine(_recipeRoot, name);
            if (Directory.Exists(path))
                throw new ArgumentException($"配方 '{name}' 已存在");

            Directory.CreateDirectory(path);
            _logService?.Info(LogCategory.RECIPE, "System", $"已创建配方: {name}");

            var recipe = new Recipe
            {
                Name = name,
                FolderPath = path,
                CreatedAt = DateTime.Now,
                VppFiles = new List<string>()
            };

            OnRecipeListChanged?.Invoke();
            return recipe;
        }

        /// <summary>
        /// 复制配方
        /// </summary>
        public Recipe CopyRecipe(string sourceName, string newName)
        {
            string sourcePath = Path.Combine(_recipeRoot, sourceName);
            if (!Directory.Exists(sourcePath))
                throw new ArgumentException($"源配方 '{sourceName}' 不存在");

            string destPath = Path.Combine(_recipeRoot, newName);
            if (Directory.Exists(destPath))
                throw new ArgumentException($"目标配方 '{newName}' 已存在");

            // 复制整个文件夹
            CopyDirectory(sourcePath, destPath);

            _logService?.Info(LogCategory.RECIPE, "System",
                $"已复制配方: '{sourceName}' → '{newName}'");

            OnRecipeListChanged?.Invoke();
            return GetRecipe(newName);
        }

        /// <summary>
        /// 重命名配方
        /// </summary>
        public bool RenameRecipe(string oldName, string newName)
        {
            string oldPath = Path.Combine(_recipeRoot, oldName);
            string newPath = Path.Combine(_recipeRoot, newName);

            if (!Directory.Exists(oldPath))
                throw new ArgumentException($"配方 '{oldName}' 不存在");
            if (Directory.Exists(newPath))
                throw new ArgumentException($"名称 '{newName}' 已存在");

            Directory.Move(oldPath, newPath);

            if (_currentRecipeName == oldName)
                _currentRecipeName = newName;

            _logService?.Info(LogCategory.RECIPE, "System",
                $"已重命名配方: '{oldName}' → '{newName}'");

            OnRecipeListChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 删除配方
        /// </summary>
        public bool DeleteRecipe(string name)
        {
            if (name == "1")
            {
                _logService?.Warn(LogCategory.RECIPE, "System", "不能删除默认配方");
                return false;
            }

            string path = Path.Combine(_recipeRoot, name);
            if (!Directory.Exists(path))
                return false;

            Directory.Delete(path, true);
            _logService?.Info(LogCategory.RECIPE, "System", $"已删除配方: {name}");

            if (_currentRecipeName == name)
                SwitchRecipe("1");

            OnRecipeListChanged?.Invoke();
            return true;
        }

        /// <summary>
        /// 切换当前配方
        /// </summary>
        public bool SwitchRecipe(string name)
        {
            var recipe = GetRecipe(name);
            if (recipe == null)
            {
                _logService?.Error(LogCategory.RECIPE, "System", $"配方 '{name}' 不存在");
                return false;
            }

            string oldRecipe = _currentRecipeName;
            _currentRecipeName = name;

            _logService?.Info(LogCategory.RECIPE, "System",
                $"[RECIPE] Recipe: \"{oldRecipe}\" → \"{name}\"");

            OnRecipeChanged?.Invoke(recipe);
            return true;
        }

        /// <summary>
        /// 获取下一个可用的配方编号
        /// </summary>
        public string GetNextRecipeName()
        {
            var recipes = GetAllRecipes();
            int maxNum = 1;
            foreach (var r in recipes)
            {
                if (int.TryParse(r.Name, out int n) && n >= maxNum)
                    maxNum = n + 1;
            }
            return maxNum.ToString();
        }

        /// <summary>
        /// 确保当前配方有默认 VPP 文件
        /// </summary>
        public string EnsureDefaultVpp()
        {
            string vppPath = CurrentVppPath;
            string dir = Path.GetDirectoryName(vppPath);

            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            return vppPath;
        }

        private void CopyDirectory(string sourceDir, string destDir)
        {
            Directory.CreateDirectory(destDir);

            foreach (var file in Directory.GetFiles(sourceDir))
            {
                string destFile = Path.Combine(destDir, Path.GetFileName(file));
                File.Copy(file, destFile, true);
            }

            foreach (var dir in Directory.GetDirectories(sourceDir))
            {
                string destSubDir = Path.Combine(destDir, Path.GetFileName(dir));
                CopyDirectory(dir, destSubDir);
            }
        }
    }
}
