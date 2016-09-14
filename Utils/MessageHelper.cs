using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AhDung
{
    /// <summary>
    /// Win32消息辅助类
    /// </summary>
    public static class MessageHelper
    {
        // 约定该消息作为显示窗口的消息
        const int ShowMsg = 0x80F0;
        const int ShowWParam = 0x8F0;
        const int ShowLParam = 0x8F;

        /// <summary>
        /// 当接收到显示窗口消息后
        /// </summary>
        public static event EventHandler ShowMessageReceived;

        static MessageHelper()
        {
            Application.AddMessageFilter(new MsgFilter());
        }

        private static void RaiseShowMessageReceived()
        {
            if (ShowMessageReceived != null)
            {
                ShowMessageReceived(null, null);
            }
        }

        /// <summary>
        /// 向指定进程发送约定的显示窗口的消息
        /// </summary>
        /// <param name="pid">进程ID</param>
        public static void ShowIt(uint pid)
        {
            try
            {
                PostThreadMessage(
                    (uint)Process.GetProcessById((int)pid).Threads[0].Id,//向该进程的主线程发送消息
                    ShowMsg, (IntPtr)ShowWParam, (IntPtr)ShowLParam);
            }
            catch { }
        }

        private class MsgFilter : IMessageFilter
        {
            public bool PreFilterMessage(ref Message m)
            {
                if (m.Msg == ShowMsg
                    && (int)m.WParam == ShowWParam
                    && (int)m.LParam == ShowLParam)
                {
                    RaiseShowMessageReceived();
                    return true;
                }
                return false;
            }
        }

        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);
    }
}
