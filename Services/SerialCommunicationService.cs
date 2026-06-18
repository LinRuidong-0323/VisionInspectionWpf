using System;
using System.IO.Ports;
using System.Text;
using System.Threading;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 串口通讯服务（Phase 1 预留，与 TCP 共用 ICommunicationService 接口）
    /// </summary>
    public class SerialCommunicationService : ICommunicationService, IDisposable
    {
        private readonly ILogService _logService;
        private readonly SerialConfig _config;
        private SerialPort _serialPort;
        private CancellationTokenSource _cts;

        public string Name => $"COM ({_config?.PortName ?? "N/A"})";
        public bool IsRunning { get; private set; }

        public event Action<string, byte[]> OnDataReceived;
        public event Action<string, byte[]> OnDataSent;
        public event Action<bool> OnStatusChanged;
        public event Action<string> OnError;

        public SerialCommunicationService(ILogService logService, SerialConfig config)
        {
            _logService = logService;
            _config = config ?? new SerialConfig();
        }

        public bool Start()
        {
            if (IsRunning)
            {
                OnError?.Invoke("串口已在运行中");
                return false;
            }

            try
            {
                _serialPort = new SerialPort(_config.PortName, _config.BaudRate, _config.Parity, _config.DataBits, _config.StopBits);
                _serialPort.Handshake = _config.Handshake;
                _serialPort.ReadTimeout = 500;
                _serialPort.WriteTimeout = 500;
                _serialPort.Open();

                IsRunning = true;
                _cts = new CancellationTokenSource();
                OnStatusChanged?.Invoke(true);

                _logService?.Info(LogCategory.COM, "System",
                    $"串口已打开: {_config.PortName}, {_config.BaudRate}bps");

                var recvThread = new Thread(ReceiveLoop);
                recvThread.IsBackground = true;
                recvThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                OnError?.Invoke($"串口打开失败: {ex.Message}");
                _logService?.Error(LogCategory.COM, "System", $"串口打开失败: {ex.Message}");
                return false;
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _cts?.Cancel();

            if (_serialPort != null)
            {
                try { _serialPort.Close(); } catch { }
                try { _serialPort.Dispose(); } catch { }
                _serialPort = null;
            }

            OnStatusChanged?.Invoke(false);
            _logService?.Info(LogCategory.COM, "System", "串口已关闭");
        }

        private void ReceiveLoop()
        {
            while (IsRunning && _serialPort != null && _serialPort.IsOpen && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        byte[] buffer = new byte[_serialPort.BytesToRead];
                        int bytesRead = _serialPort.Read(buffer, 0, buffer.Length);

                        if (bytesRead > 0)
                        {
                            byte[] received = new byte[bytesRead];
                            Array.Copy(buffer, received, bytesRead);

                            string hexStr = BitConverter.ToString(received).Replace("-", " ");
                            _logService?.Info(LogCategory.COM, "System", $"[RECV] Hex:{hexStr}");

                            OnDataReceived?.Invoke(_config.PortName, received);
                        }
                    }
                    else
                    {
                        Thread.Sleep(20);
                    }
                }
                catch (TimeoutException)
                {
                    // 正常超时，继续循环
                }
                catch (Exception ex)
                {
                    _logService?.Error(LogCategory.COM, "System", $"接收异常: {ex.Message}");
                    break;
                }
            }
        }

        public void Send(string data)
        {
            if (!IsRunning || _serialPort == null || !_serialPort.IsOpen)
            {
                OnError?.Invoke("串口未打开，无法发送");
                return;
            }

            try
            {
                byte[] bytes = Encoding.ASCII.GetBytes(data);
                _serialPort.Write(bytes, 0, bytes.Length);

                string hexStr = BitConverter.ToString(bytes).Replace("-", " ");
                _logService?.Info(LogCategory.COM, "System", $"[SEND] Hex:{hexStr}");
                OnDataSent?.Invoke("", bytes);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"串口发送失败: {ex.Message}");
                _logService?.Error(LogCategory.COM, "System", $"发送失败: {ex.Message}");
            }
        }

        public void Send(byte[] data)
        {
            if (!IsRunning || _serialPort == null || !_serialPort.IsOpen)
            {
                OnError?.Invoke("串口未打开，无法发送");
                return;
            }

            try
            {
                _serialPort.Write(data, 0, data.Length);

                string hexStr = BitConverter.ToString(data).Replace("-", " ");
                _logService?.Info(LogCategory.COM, "System", $"[SEND] Hex:{hexStr}");
                OnDataSent?.Invoke("", data);
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"串口发送失败: {ex.Message}");
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
