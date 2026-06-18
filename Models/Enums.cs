using System;

namespace VisionInspection.Models
{
    /// <summary>
    /// 日志分类标识
    /// </summary>
    public enum LogCategory
    {
        CAMERA,
        JOB,
        TCP,
        COM,
        SETTING,
        RECIPE,
        SYSTEM
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        DEBUG,
        INFO,
        WARN,
        ERROR
    }

    /// <summary>
    /// 用户角色（三级权限）
    /// </summary>
    public enum UserRole
    {
        /// <summary>管理员：完全控制</summary>
        Admin,
        /// <summary>工程师：可修改视觉工具、作业、配方和通讯参数，不可管理用户</summary>
        Engineer,
        /// <summary>操作员：仅能启动/停止、切换画面、查看结果</summary>
        Operator
    }

    /// <summary>
    /// 通讯协议类型
    /// </summary>
    public enum CommunicationType
    {
        TCPServer,
        TCPClient,
        SerialPort,
        ModbusTCP,
        ModbusRTU
    }

    /// <summary>
    /// TCP 通讯角色
    /// </summary>
    public enum TcpRole
    {
        Server,
        Client
    }

    /// <summary>
    /// 字节序
    /// </summary>
    public enum ByteOrder
    {
        BigEndian,
        LittleEndian
    }

    /// <summary>
    /// 图像源类型
    /// </summary>
    public enum ImageSourceType
    {
        SingleImage,
        ImageSequence,
        VideoFile,
        Camera
    }

    /// <summary>
    /// 检测判定结果
    /// </summary>
    public enum InspectionVerdict
    {
        /// <summary>未运行</summary>
        Unknown,
        /// <summary>通过</summary>
        OK,
        /// <summary>不通过</summary>
        NG
    }

    /// <summary>
    /// 程序运行模式
    /// </summary>
    public enum RunMode
    {
        /// <summary>离线模式（本地图片/视频）</summary>
        Offline,
        /// <summary>在线模式（真实相机）</summary>
        Online
    }
}
