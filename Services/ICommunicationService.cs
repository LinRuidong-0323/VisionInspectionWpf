using System;
using VisionInspection.Models;

namespace VisionInspection.Services
{
    /// <summary>
    /// 通讯服务接口（TCP/串口等实现此接口）
    /// </summary>
    public interface ICommunicationService
    {
        /// <summary>通讯名称</summary>
        string Name { get; }

        /// <summary>是否正在运行</summary>
        bool IsRunning { get; }

        /// <summary>启动通讯</summary>
        bool Start();

        /// <summary>停止通讯</summary>
        void Stop();

        /// <summary>发送数据</summary>
        void Send(string data);

        /// <summary>发送字节数据</summary>
        void Send(byte[] data);

        /// <summary>接收到数据时触发</summary>
        event Action<string, byte[]> OnDataReceived;

        /// <summary>发送数据时触发</summary>
        event Action<string, byte[]> OnDataSent;

        /// <summary>状态变更时触发</summary>
        event Action<bool> OnStatusChanged;

        /// <summary>错误发生时触发</summary>
        event Action<string> OnError;
    }
}
