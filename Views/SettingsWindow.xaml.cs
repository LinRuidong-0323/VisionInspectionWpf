using System;
using System.Windows;
using VisionInspection.Models;

namespace VisionInspection.Views
{
    public partial class SettingsWindow : Window
    {
        private AppSettings _settings;

        public SettingsWindow(AppSettings settings)
        {
            InitializeComponent();
            _settings = settings;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // 相机原图
            ChkRawImage.IsChecked = _settings.RawImageEnabled;
            switch (_settings.RawImageFormat)
            {
                case "png": CmbRawFormat.SelectedIndex = 1; break;
                case "jpg": CmbRawFormat.SelectedIndex = 2; break;
                default: CmbRawFormat.SelectedIndex = 0; break;
            }
            TxtRawRetention.Text = _settings.RawImageRetentionDays.ToString();
            if (!string.IsNullOrEmpty(_settings.LastRawImageCleanupDate))
                LblRawLastClean.Text = "上次清理：" + _settings.LastRawImageCleanupDate;
            else
                LblRawLastClean.Text = "尚未执行过自动清理";

            // 检测效果图
            ChkImageSave.IsChecked = _settings.ImageSaveEnabled;
            switch (_settings.ImageSaveCondition)
            {
                case "OK": CmbImageCondition.SelectedIndex = 1; break;
                case "NG": CmbImageCondition.SelectedIndex = 2; break;
                default: CmbImageCondition.SelectedIndex = 0; break;
            }
            TxtImageRetention.Text = _settings.ImageRetentionDays.ToString();
            if (!string.IsNullOrEmpty(_settings.LastImageCleanupDate))
                LblImageLastClean.Text = "上次清理：" + _settings.LastImageCleanupDate;
            else
                LblImageLastClean.Text = "尚未执行过自动清理";

            // 日志
            TxtLogRetention.Text = _settings.LogRetentionDays.ToString();
            TxtLogMaxSize.Text = _settings.LogMaxFileSizeMB.ToString();
            if (!string.IsNullOrEmpty(_settings.LastLogCleanupDate))
                LblLogLastClean.Text = "上次清理：" + _settings.LastLogCleanupDate;
            else
                LblLogLastClean.Text = "尚未执行过自动清理";
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            // 验证原图输入
            if (!int.TryParse(TxtRawRetention.Text, out int rawDays) || rawDays < 0)
            {
                MessageBox.Show("原图保留天数请输入有效数字（≥0）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 验证效果图输入
            if (!int.TryParse(TxtImageRetention.Text, out int imageDays) || imageDays < 0)
            {
                MessageBox.Show("效果图保留天数请输入有效数字（≥0）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            // 验证日志输入
            if (!int.TryParse(TxtLogRetention.Text, out int logDays) || logDays < 0)
            {
                MessageBox.Show("日志保留天数请输入有效数字（≥0）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (!int.TryParse(TxtLogMaxSize.Text, out int logMaxSizeMB) || logMaxSizeMB < 1)
            {
                MessageBox.Show("日志单文件上限请输入有效数字（≥1 MB）", "输入错误", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // 保存相机原图设置
            _settings.RawImageEnabled = ChkRawImage.IsChecked == true;
            if (CmbRawFormat.SelectedIndex == 1)
                _settings.RawImageFormat = "png";
            else if (CmbRawFormat.SelectedIndex == 2)
                _settings.RawImageFormat = "jpg";
            else
                _settings.RawImageFormat = "bmp";
            _settings.RawImageRetentionDays = rawDays;

            // 保存检测效果图设置
            _settings.ImageSaveEnabled = ChkImageSave.IsChecked == true;
            if (CmbImageCondition.SelectedIndex == 1)
                _settings.ImageSaveCondition = "OK";
            else if (CmbImageCondition.SelectedIndex == 2)
                _settings.ImageSaveCondition = "NG";
            else
                _settings.ImageSaveCondition = "All";
            _settings.ImageRetentionDays = imageDays;

            // 保存日志设置
            _settings.LogRetentionDays = logDays;
            _settings.LogMaxFileSizeMB = logMaxSizeMB;

            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
