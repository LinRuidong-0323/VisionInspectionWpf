using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using VisionInspection.Utils;

namespace VisionInspection.Views
{
    public partial class SerialWindow : Window
    {
        private SerialPort _serialPort;
        private ObservableCollection<CommLogRow> _rows = new ObservableCollection<CommLogRow>();

        public SerialWindow()
        {
            InitializeComponent();
            GridLog.ItemsSource = _rows;
            RefreshPorts();
        }

        private void RefreshPorts()
        {
            CmbPort.Items.Clear();
            foreach (string name in SerialPort.GetPortNames())
                CmbPort.Items.Add(name);
            if (CmbPort.Items.Count > 0) CmbPort.SelectedIndex = 0;
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e) => RefreshPorts();

        private void BtnOpen_Click(object s, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen) return;
            if (CmbPort.SelectedItem == null) return;

            try
            {
                _serialPort = new SerialPort(
                    CmbPort.SelectedItem.ToString(),
                    int.Parse((CmbBaud.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "9600"),
                    GetParity(),
                    int.Parse((CmbDataBits.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "8"),
                    GetStopBits());
                _serialPort.DataReceived += OnDataReceived;
                _serialPort.Open();

                BtnOpen.IsEnabled = false;
                BtnClose.IsEnabled = true;
                AddLog("INFO", "串口已打开: " + CmbPort.SelectedItem);
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "打开失败: " + ex.Message);
            }
        }

        private void BtnClose_Click(object s, RoutedEventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
                _serialPort = null;
            }
            BtnOpen.IsEnabled = true;
            BtnClose.IsEnabled = false;
            AddLog("INFO", "串口已关闭");
        }

        private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            try
            {
                byte[] buffer = new byte[_serialPort.BytesToRead];
                _serialPort.Read(buffer, 0, buffer.Length);
                Dispatcher.BeginInvoke(new Action(() => AddRow("RECV", buffer)));
            }
            catch { }
        }

        private void BtnSend_Click(object s, RoutedEventArgs e)
        {
            if (_serialPort == null || !_serialPort.IsOpen) return;
            if (string.IsNullOrEmpty(TxtSend.Text)) return;
            try
            {
                byte[] data = System.Text.Encoding.ASCII.GetBytes(TxtSend.Text);
                _serialPort.Write(data, 0, data.Length);
                AddRow("SEND", data);
                TxtSend.Clear();
            }
            catch (Exception ex)
            {
                AddLog("ERROR", "发送失败: " + ex.Message);
            }
        }

        private void TxtSend_KeyDown(object s, KeyEventArgs e)
        {
            if (e.Key == Key.Enter) BtnSend_Click(s, null);
        }

        private void BtnClear_Click(object s, RoutedEventArgs e)
        {
            _rows.Clear();
            TxtDetail.Clear();
        }

        private void AddRow(string direction, byte[] data)
        {
            string hex = ByteFormatHelper.BytesToHexString(data);
            string ascii = ByteFormatHelper.BytesToReadableAscii(data);
            _rows.Insert(0, new CommLogRow
            {
                Time = DateTime.Now.ToString("HH:mm:ss.fff"),
                Direction = direction,
                Hex = hex,
                Ascii = ascii
            });
            while (_rows.Count > 500) _rows.RemoveAt(_rows.Count - 1);
            TxtDetail.AppendText(string.Format("[{0:HH:mm:ss.fff}] {1}: {2}\n",
                DateTime.Now, direction, hex));
            TxtDetail.ScrollToEnd();
        }

        private void AddLog(string level, string msg)
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(string.Format("[{0}] {1}", level, msg));
            AddRow(level, data);
        }

        private Parity GetParity()
        {
            var content = (CmbParity.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (content == "Odd") return Parity.Odd;
            if (content == "Even") return Parity.Even;
            return Parity.None;
        }

        private StopBits GetStopBits()
        {
            var content = (CmbStopBits.SelectedItem as ComboBoxItem)?.Content?.ToString();
            if (content == "1.5") return StopBits.OnePointFive;
            if (content == "2") return StopBits.Two;
            return StopBits.One;
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            if (_serialPort != null && _serialPort.IsOpen)
            {
                _serialPort.Close();
                _serialPort.Dispose();
            }
        }
    }
}
