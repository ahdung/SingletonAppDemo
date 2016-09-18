using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AhDung
{
    /// <summary>
    /// Win32消息辅助类
    /// <para>- 记得注册事件ShowMessageReceived</para>
    /// <para>- 建议使用本类ShowForm方法显示窗体</para>
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

        /// <summary>
        /// 显示窗体。含置顶动作
        /// </summary>
        public static void ShowForm(Form form)
        {
            form.Visible = true;

            if (form.IsHandleCreated
                && form.WindowState == FormWindowState.Minimized)
            {
                //地道的还原。好处是如果窗口是在最大化的状态下最小化的
                //该方法能让窗口恢复最大化
                //而form.WindowState = FormWindowState.Normal则只会让窗口恢复为正常状态
                ShowWindow(form.Handle, 9/*SW_RESTORE*/);
            }

            //hack招数。摁个无用空键，解决NT6下直接Activate可能会无效的问题
            //问题具体表现是任务栏图标闪烁，但窗体不会被激活
            SendKeys.Flush();
            SendKeys.Send("^");//ctrl

            form.Activate();
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

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostThreadMessage(uint threadId, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
