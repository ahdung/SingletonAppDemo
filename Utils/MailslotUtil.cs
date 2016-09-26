using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Security;

namespace AhDung
{
    /// <summary>
    /// 邮槽工具类。邮槽名不区分大小写
    /// </summary>
    [SuppressUnmanagedCodeSecurity]
    public static class MailslotUtil
    {
        static IntPtr _mailslot;

        /// <summary>
        /// 建立邮槽。一个程序同时只能建一个
        /// </summary>
        /// <param name="name">邮槽名</param>
        /// <exception cref="Win32Exception" />
        public static void Create(string name)//, int maxSize = 0)
        {
            if (IsHandleValid(_mailslot))
            {
                throw new Exception("邮槽已建！");
            }
            _mailslot = CreateMailslot(@"\\.\mailslot\" + name, 2/*单个消息最大大小（字节）*/, -1, IntPtr.Zero);
            if (!IsHandleValid(_mailslot))
            {
                throw new Win32Exception();
            }
        }

        /// <summary>
        /// 读取short。无消息时会阻塞
        /// </summary>
        /// <exception cref="Win32Exception" />
        public static short Read()
        {
            var buffer = new byte[2];
            int i;
            if (!ReadFile(_mailslot, buffer, 2, out i, IntPtr.Zero))
            {
                throw new Win32Exception();
            }
            return BitConverter.ToInt16(buffer, 0);
        }

        /// <summary>
        /// 往指定邮槽写入值
        /// </summary>
        /// <param name="name">邮槽名（不含前缀）</param>
        /// <param name="value">要写入的值</param>
        /// <exception cref="Win32Exception" />
        public static void Write(string name, short value)
        {
            IntPtr hFile = IntPtr.Zero;
            try
            {
                int errCode;
                hFile = CreateFile(name, out errCode);
                if (!IsHandleValid(hFile))
                {
                    throw new Win32Exception(errCode);
                }
                int i;
                if (!WriteFile(hFile, BitConverter.GetBytes(value), 2, out i, IntPtr.Zero))
                {
                    throw new Win32Exception();
                }
            }
            finally
            {
                if (IsHandleValid(hFile)) { CloseHandle(hFile); }
            }
        }

        /// <summary>
        /// 判断邮槽是否存在
        /// </summary>
        public static bool Exists(string name)
        {
            IntPtr hFile = IntPtr.Zero;
            try
            {
                int errCode;
                hFile = CreateFile(name, out errCode);
                return errCode != 2; //ERROR_FILE_NOT_FOUND 
            }
            finally
            {
                if (IsHandleValid(hFile)) { CloseHandle(hFile); }
            }
        }

        /// <summary>
        /// 关闭邮槽
        /// </summary>
        public static void Close()
        {
            if (IsHandleValid(_mailslot))
            {
                CloseHandle(_mailslot);
                _mailslot = IntPtr.Zero;
            }
        }

        /// <summary>
        /// 打开邮槽
        /// </summary>
        private static IntPtr CreateFile(string name, out int errCode)
        {
            var hFile = CreateFile(@"\\.\mailslot\" + name,
                0x40000000,//GENERIC_WRITE
                FileShare.Read,
                IntPtr.Zero,
                FileMode.Open,
                0,
                IntPtr.Zero);
            errCode = Marshal.GetLastWin32Error();
            return hFile;
        }

        /// <summary>
        /// 检测邮槽句柄有效性
        /// </summary>
        private static bool IsHandleValid(IntPtr handle)
        {
            return handle != IntPtr.Zero
                && handle != (IntPtr)(-1);
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateMailslot(string name, int nMaxMessageSize, int lReadTimeout, //读取超时(ms)。0=不等待；-1=一直等待（MAILSLOT_WAIT_FOREVER）
            IntPtr lpSecurityAttributes);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToRead, out int lpNumberOfBytesRead, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(IntPtr hFile, byte[] lpBuffer, int nNumberOfBytesToWrite, out int lpNumberOfBytesWritten, IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateFile(string lpFileName, int dwDesiredAccess, FileShare dwShareMode,
            IntPtr lpSecurityAttributes, FileMode dwCreationDisposition, int dwFlagsAndAttributes, IntPtr hTemplateFile);
    }
}
