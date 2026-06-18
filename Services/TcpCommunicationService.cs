using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// TCP 通讯服务实现
    /// 支持 Server（监听多客户端）和 Client 两种角色
    /// </summary>
    public class TcpCommunicationService : ICommunicationService, IDisposable
    {
        private readonly ILogService _logService;
        private readonly TcpConfig _config;

        // Server 模式
        private TcpListener _listener;
        private readonly List<TcpClient> _clients = new List<TcpClient>();
        private readonly object _clientsLock = new object();
        private CancellationTokenSource _cts;
        private Thread _acceptThread;

        // Client 模式
        private TcpClient _client;
        private NetworkStream _clientStream;

        public string Name => $"TCP ({(_config.Role == TcpRole.Server ? "Server" : "Client")})";
        public bool IsRunning { get; private set; }

        public event Action<string, byte[]> OnDataReceived;
        public event Action<string, byte[]> OnDataSent;
        public event Action<bool> OnStatusChanged;
        public event Action<string> OnError;

        public TcpCommunicationService(ILogService logService, TcpConfig config)
        {
            _logService = logService;
            _config = config ?? new TcpConfig();
        }

        public bool Start()
        {
            if (IsRunning)
            {
                OnError?.Invoke("TCP 通讯已在运行中");
                return false;
            }

            try
            {
                _cts = new CancellationTokenSource();

                if (_config.Role == TcpRole.Server)
                    return StartServer();
                else
                    return StartClient();
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"启动 TCP 通讯失败: {ex.Message}");
                _logService?.Error(LogCategory.TCP, "System", $"启动失败: {ex.Message}");
                return false;
            }
        }

        private bool StartServer()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Parse(_config.IPAddress), _config.Port);
                _listener.Start();
                IsRunning = true;
                OnStatusChanged?.Invoke(true);

                _logService?.Info(LogCategory.TCP, "System",
                    $"TCP Server 已启动: {_config.IPAddress}:{_config.Port}");

                _acceptThread = new Thread(AcceptClientsLoop);
                _acceptThread.IsBackground = true;
                _acceptThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                throw new Exception($"TCP Server 启动失败: {ex.Message}", ex);
            }
        }

        private bool StartClient()
        {
            try
            {
                _client = new TcpClient();
                _client.Connect(_config.IPAddress, _config.Port);
                _clientStream = _client.GetStream();
                IsRunning = true;
                OnStatusChanged?.Invoke(true);

                _logService?.Info(LogCategory.TCP, "System",
                    $"TCP Client 已连接: {_config.IPAddress}:{_config.Port}");

                // 启动接收线程
                var recvThread = new Thread(() => ReceiveLoop(_client, _clientStream, "client"));
                recvThread.IsBackground = true;
                recvThread.Start();

                return true;
            }
            catch (Exception ex)
            {
                IsRunning = false;
                throw new Exception($"TCP Client 连接失败: {ex.Message}", ex);
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            IsRunning = false;
            _cts?.Cancel();

            if (_listener != null)
            {
                try { _listener.Stop(); } catch { }
                _listener = null;
            }

            lock (_clientsLock)
            {
                foreach (var client in _clients)
                {
                    try { client.Close(); } catch { }
                }
                _clients.Clear();
            }

            if (_client != null)
            {
                try { _client.Close(); } catch { }
                _client = null;
            }

            _clientStream = null;
            OnStatusChanged?.Invoke(false);
            _logService?.Info(LogCategory.TCP, "System", "TCP 通讯已停止");
        }

        private void AcceptClientsLoop()
        {
            while (IsRunning && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    var remoteEndPoint = client.Client.RemoteEndPoint as IPEndPoint;
                    string clientId = remoteEndPoint?.ToString() ?? "unknown";

                    lock (_clientsLock)
                    {
                        _clients.Add(client);
                    }

                    _logService?.Info(LogCategory.TCP, "System", $"客户端已连接: {clientId}");

                    // 为每个客户端启动独立的接收线程
                    var stream = client.GetStream();
                    var recvThread = new Thread(() => ReceiveLoop(client, stream, clientId));
                    recvThread.IsBackground = true;
                    recvThread.Start();
                }
                catch (ObjectDisposedException) { break; }
                catch (SocketException) { break; }
                catch (Exception ex)
                {
                    if (IsRunning)
                        _logService?.Error(LogCategory.TCP, "System", $"接受客户端连接异常: {ex.Message}");
                }
            }
        }

        private void ReceiveLoop(TcpClient client, NetworkStream stream, string clientId)
        {
            byte[] buffer = new byte[8192];

            while (IsRunning && client.Connected && !_cts.Token.IsCancellationRequested)
            {
                try
                {
                    if (stream.DataAvailable)
                    {
                        int bytesRead = stream.Read(buffer, 0, buffer.Length);
                        if (bytesRead > 0)
                        {
                            byte[] received = new byte[bytesRead];
                            Array.Copy(buffer, received, bytesRead);

                            string hexStr = BitConverter.ToString(received).Replace("-", " ");
                            string asciiStr = TryParseAscii(received);

                            _logService?.Info(LogCategory.TCP, "System",
                                $"[RECV] [{clientId}] Hex:{hexStr} | {asciiStr}");

                            // 处理帧解析
                            var frames = ParseFrames(received);

                            foreach (var frame in frames)
                            {
                                OnDataReceived?.Invoke(clientId, frame);

                                // 自动回复
                                if (_config.AutoReplyEnabled && !string.IsNullOrEmpty(_config.AutoReplyMessage))
                                {
                                    string reply = _config.AutoReplyMessage; // 表达式替换由上层处理
                                    byte[] replyBytes = EncodeMessage(reply);
                                    SendToClient(client, stream, replyBytes);
                                }
                            }
                        }
                    }
                    else
                    {
                        Thread.Sleep(10);
                    }
                }
                catch (IOException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logService?.Error(LogCategory.TCP, "System", $"接收异常 [{clientId}]: {ex.Message}");
                    break;
                }
            }

            // 客户端断开
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }
            try { client.Close(); } catch { }
            _logService?.Info(LogCategory.TCP, "System", $"客户端已断开: {clientId}");
        }

        public void Send(string data)
        {
            byte[] bytes = EncodeMessage(data);
            Send(bytes);
        }

        public void Send(byte[] data)
        {
            if (!IsRunning)
            {
                OnError?.Invoke("TCP 通讯未运行，无法发送");
                return;
            }

            try
            {
                if (_config.Role == TcpRole.Server)
                {
                    lock (_clientsLock)
                    {
                        foreach (var client in _clients.ToList())
                        {
                            if (client.Connected)
                            {
                                try
                                {
                                    var stream = client.GetStream();
                                    SendToClient(client, stream, data);
                                }
                                catch { }
                            }
                        }
                    }
                }
                else
                {
                    if (_client != null && _client.Connected && _clientStream != null)
                    {
                        SendToClient(_client, _clientStream, data);
                    }
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke($"发送失败: {ex.Message}");
                _logService?.Error(LogCategory.TCP, "System", $"发送失败: {ex.Message}");
            }
        }

        private void SendToClient(TcpClient client, NetworkStream stream, byte[] data)
        {
            stream.Write(data, 0, data.Length);
            stream.Flush();

            string hexStr = BitConverter.ToString(data).Replace("-", " ");
            string asciiStr = TryParseAscii(data);
            _logService?.Info(LogCategory.TCP, "System", $"[SEND] Hex:{hexStr} | {asciiStr}");
            OnDataSent?.Invoke("", data);
        }

        private List<byte[]> ParseFrames(byte[] data)
        {
            var frames = new List<byte[]>();

            if (string.IsNullOrEmpty(_config.StartDelimiter) && string.IsNullOrEmpty(_config.EndDelimiter) && string.IsNullOrEmpty(_config.Separator))
            {
                // 无帧格式，整个数据包作为一帧
                frames.Add(data);
                return frames;
            }

            // 简单帧解析：根据起始符和结束符拆分
            // 为简化实现，此处按结束符拆分
            if (!string.IsNullOrEmpty(_config.EndDelimiter))
            {
                byte endByte = Convert.ToByte(_config.EndDelimiter, 16);
                var currentFrame = new List<byte>();

                foreach (byte b in data)
                {
                    currentFrame.Add(b);
                    if (b == endByte)
                    {
                        frames.Add(currentFrame.ToArray());
                        currentFrame.Clear();
                    }
                }
                if (currentFrame.Count > 0)
                    frames.Add(currentFrame.ToArray());
            }
            else
            {
                frames.Add(data);
            }

            return frames;
        }

        private byte[] EncodeMessage(string message)
        {
            switch (_config.Encoding.ToUpper())
            {
                case "UTF8":
                    return System.Text.Encoding.UTF8.GetBytes(message);
                case "HEX":
                    return HexStringToBytes(message);
                case "ASCII":
                default:
                    return System.Text.Encoding.ASCII.GetBytes(message);
            }
        }

        private byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "");
            // 奇数长度补 0
            if (hex.Length % 2 != 0)
                hex = "0" + hex;
            int len = hex.Length;
            if (len == 0) return new byte[0];

            byte[] bytes = new byte[len / 2];
            for (int i = 0; i < len; i += 2)
            {
                try
                {
                    bytes[i / 2] = Convert.ToByte(hex.Substring(i, 2), 16);
                }
                catch
                {
                    // 非法 HEX 字符，退回 ASCII 编码
                    return System.Text.Encoding.ASCII.GetBytes(hex);
                }
            }
            return bytes;
        }

        private string TryParseAscii(byte[] data)
        {
            var sb = new StringBuilder();
            foreach (byte b in data)
            {
                if (b >= 32 && b < 127)
                    sb.Append((char)b);
                else
                    sb.Append($"<{b:X2}>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 获取已连接的客户端数量
        /// </summary>
        public int ConnectedClientCount
        {
            get
            {
                lock (_clientsLock)
                {
                    return _clients.Count(c => c.Connected);
                }
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
