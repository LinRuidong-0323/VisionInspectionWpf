using System;
using System.Text;

namespace VisionInspection.Utils
{
    /// <summary>
    /// 字节格式转换工具
    /// </summary>
    public static class ByteFormatHelper
    {
        /// <summary>
        /// 十六进制字符串转字节数组
        /// </summary>
        public static byte[] HexStringToBytes(string hex)
        {
            hex = hex.Replace(" ", "").Replace("-", "").Replace("\r", "").Replace("\n", "");
            if (hex.Length % 2 != 0)
                throw new ArgumentException("十六进制字符串长度必须为偶数");

            int len = hex.Length / 2;
            byte[] bytes = new byte[len];
            for (int i = 0; i < len; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// 字节数组转十六进制字符串
        /// </summary>
        public static string BytesToHexString(byte[] bytes, string separator = " ")
        {
            return BitConverter.ToString(bytes).Replace("-", separator);
        }

        /// <summary>
        /// 尝试将字节数组解析为 ASCII 字符串（不可打印字符用 <XX> 表示）
        /// </summary>
        public static string BytesToReadableAscii(byte[] bytes)
        {
            var sb = new StringBuilder();
            foreach (byte b in bytes)
            {
                if (b >= 32 && b < 127)
                    sb.Append((char)b);
                else
                    sb.Append($"<{b:X2}>");
            }
            return sb.ToString();
        }

        /// <summary>
        /// 格式化字节大小为人性化文本
        /// </summary>
        public static string FormatBytes(long bytes)
        {
            string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < suffixes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {suffixes[order]}";
        }
    }
}
