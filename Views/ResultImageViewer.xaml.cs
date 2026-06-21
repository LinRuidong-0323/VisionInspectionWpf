using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Cognex.VisionPro;
using Cognex.VisionPro.Display;

namespace VisionInspection.Views
{
    public partial class ResultImageViewer : Window
    {
        private ObservableCollection<ResultEntry> _entries = new ObservableCollection<ResultEntry>();
        private CogRecordDisplay _previewDisplay;

        public ResultImageViewer()
        {
            InitializeComponent();
            LstHistory.ItemsSource = _entries;

            _previewDisplay = new CogRecordDisplay();
            _previewDisplay.Dock = System.Windows.Forms.DockStyle.Fill;
            ResultImageHost.Child = _previewDisplay;

            LoadHistory();
        }

        private void LoadHistory()
        {
            string imageDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResultImages");
            if (!Directory.Exists(imageDir)) return;

            foreach (var file in Directory.GetFiles(imageDir, "*.png")
                .OrderByDescending(f => f))
            {
                try
                {
                    var entry = new ResultEntry
                    {
                        FilePath = file,
                        Time = File.GetCreationTime(file).ToString("MM-dd HH:mm:ss"),
                        Display = Path.GetFileName(file)
                    };
                    _entries.Add(entry);
                }
                catch { }
            }
        }

        public static void SaveResultImage(ICogImage image, string verdict, string fileName)
        {
            if (image == null) return;

            var capturedImage = image;
            var capturedVerdict = verdict;
            var capturedFileName = fileName;

            System.Threading.Tasks.Task.Run(() =>
            {
                try
                {
                    string imageDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ResultImages");
                    Directory.CreateDirectory(imageDir);
                    string time = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                    string safeName = Path.GetFileNameWithoutExtension(capturedFileName ?? "unknown");
                    string savePath = Path.Combine(imageDir,
                        string.Format("{0}_{1}_{2}.png", time, capturedVerdict, safeName));

                    var imgFile = new Cognex.VisionPro.ImageFile.CogImageFileTool();
                    imgFile.Operator.Open(savePath, Cognex.VisionPro.ImageFile.CogImageFileModeConstants.Write);
                    imgFile.InputImage = capturedImage;
                    imgFile.Run();
                    imgFile.Dispose();
                }
                catch { }
            });
        }

        private void LstHistory_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (LstHistory.SelectedItem is ResultEntry entry && File.Exists(entry.FilePath))
            {
                try
                {
                    var bmp = new System.Drawing.Bitmap(entry.FilePath);
                    var image = new CogImage24PlanarColor(bmp);
                    _previewDisplay.Image = image;
                    _previewDisplay.AutoFit = true;
                    TxtResultInfo.Text = entry.Display;
                }
                catch { }
            }
        }
    }

    public class ResultEntry
    {
        public string FilePath { get; set; }
        public string Time { get; set; }
        public string Display { get; set; }
    }
}
