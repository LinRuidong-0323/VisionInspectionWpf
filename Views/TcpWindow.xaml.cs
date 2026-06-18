using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionInspection.Models;
using VisionInspection.Services;
using VisionInspection.Utils;

namespace VisionInspection.Views
{
    public partial class TcpWindow : Window
    {
        private ICommunicationService _commService;
        private ObservableCollection<CommLogRow> _rows = new ObservableCollection<CommLogRow>();

        public Func<string, string, int, string, string, string, string, string, string, bool, string, bool, int, int, ICommunicationService> ReconfigureAndStart { get; set; }
        public Action StopService { get; set; }

        public TcpWindow(ICommunicationService commService, TcpConfig currentConfig)
        {
            InitializeComponent();
            GridLog.ItemsSource = _rows;
            LoadConfig(currentConfig);
            BindCommService(commService);
        }

        private void LoadConfig(TcpConfig cfg)
        {
            CmbRole.SelectedIndex = cfg.Role == TcpRole.Server ? 0 : 1;
            TxtIp.Text = cfg.IPAddress;
            TxtPort.Text = cfg.Port.ToString();
            TxtTimeout.Text = cfg.TimeoutMs.ToString();
            CmbEncoding.SelectedIndex = GetEncodingIndex(cfg.Encoding);
            CmbByteOrder.SelectedIndex = cfg.ByteOrder == ByteOrder.BigEndian ? 0 : 1;
            TxtStartDelim.Text = cfg.StartDelimiter;
            TxtEndDelim.Text = cfg.EndDelimiter;
            TxtSeparator.Text = cfg.Separator;
            ChkAutoReply.IsChecked = cfg.AutoReplyEnabled;
            TxtAutoReply.Text = cfg.AutoReplyMessage;
        }

        private int GetEncodingIndex(string enc)
        {
            switch (enc?.ToUpper())
            {
                case "UTF8": return 1;
                case "HEX": return 2;
                default: return 0;
            }
        }

        public void BindCommService(ICommunicationService service)
        {
            if (_commService != null)
            {
                _commService.OnDataReceived -= OnData;
                _commService.OnDataSent -= OnData;
                _commService.OnStatusChanged -= OnStatus;
            }
            _commService = service;
            if (_commService != null)
            {
                _commService.OnDataReceived += OnData;
                _commService.OnDataSent += OnData;
                _commService.OnStatusChanged += OnStatus;
                UpdateButtons(_commService.IsRunning);
            }
        }

        private void OnData(string source, byte[] data)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                string hex = ByteFormatHelper.BytesToHexString(data);
                string ascii = ByteFormatHelper.BytesToReadableAscii(data);
                _rows.Insert(0, new CommLogRow
                {
                    Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                    Direction = (source == "SEND" ? "SEND" : "RECV"),
                    Hex = hex,
                    Ascii = ascii
                });
                while (_rows.Count > 1000) _rows.RemoveAt(_rows.Count - 1);
                TxtDetail.AppendText(
                    "[" + DateTime.Now.ToString("HH:mm:ss.fff") + "] " +
                    (source == "SEND" ? "SEND" : "RECV") + ": " + hex + "\n");
                TxtDetail.ScrollToEnd();
            }));
        }

        private void OnStatus(bool running)
        {
            Dispatcher.BeginInvoke(new Action(() => UpdateButtons(running)));
        }

        private void UpdateButtons(bool running)
        {
            BtnStart.IsEnabled = !running;
            BtnStop.IsEnabled = running;
        }

        private void BtnStart_Click(object s, RoutedEventArgs e)
        {
            string role = CmbRole.SelectedIndex == 0 ? "Server" : "Client";
            string ip = TxtIp.Text.Trim();
            int.TryParse(TxtPort.Text, out int port);

            StopService?.Invoke();

            if (ReconfigureAndStart != null)
            {
                var svc = ReconfigureAndStart(
                    role, ip, port,
                    (CmbEncoding.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "ASCII",
                    (CmbByteOrder.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "BigEndian",
                    TxtTimeout.Text, TxtStartDelim.Text, TxtEndDelim.Text, TxtSeparator.Text,
                    ChkAutoReply.IsChecked == true, TxtAutoReply.Text,
                    ChkAutoReconnect.IsChecked == true,
                    int.TryParse(TxtReconnectInterval.Text, out int iv) ? iv : 5,
                    int.TryParse(TxtReconnectMax.Text, out int mx) ? mx : 3
                );
                if (svc != null)
                {
                    BindCommService(svc);
                    svc.Start();
                }
            }
        }

        private void BtnStop_Click(object s, RoutedEventArgs e)
        {
            _commService?.Stop();
        }

        private void BtnSend_Click(object s, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtSend.Text))
            {
                _commService?.Send(TxtSend.Text);
                TxtSend.Clear();
            }
        }

        private void TxtSend_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnSend_Click(s, null);
        }

        private void BtnAdvanced_Click(object s, RoutedEventArgs e)
        {
            PanelAdvanced.Visibility = PanelAdvanced.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
            BtnAdvanced.Content = PanelAdvanced.Visibility == Visibility.Visible ? "▾ 高级" : "▸ 高级";
        }

        private void ChkAutoReply_Changed(object s, RoutedEventArgs e)
        {
            TxtAutoReply.IsEnabled = ChkAutoReply.IsChecked == true;
        }

        private void BtnClear_Click(object s, RoutedEventArgs e)
        {
            _rows.Clear();
            TxtDetail.Clear();
        }

        private void Window_Closed(object s, EventArgs e)
        {
            if (_commService != null)
            {
                _commService.OnDataReceived -= OnData;
                _commService.OnDataSent -= OnData;
                _commService.OnStatusChanged -= OnStatus;
            }
        }
    }

    public class CommLogRow
    {
        public string Time { get; set; }
        public string Direction { get; set; }
        public string Hex { get; set; }
        public string Ascii { get; set; }
    }
}
