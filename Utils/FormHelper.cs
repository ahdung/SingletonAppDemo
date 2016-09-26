using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AhDung.WinForm
{
    /// <summary>
    /// 窗体辅助类
    /// </summary>
    public static class FormHelper
    {
        /// <summary>
        /// 确保窗体前端显示。内有跨线程处理
        /// </summary>
        public static void EnsureShow(Form form)
        {
            if (form == null || form.IsDisposed)
            {
                return;
            }

            if (form.InvokeRequired)
            {
                form.Invoke(new Action<Form>(ShowInternal), form);
            }
            else
            {
                ShowInternal(form);
            }
        }

        private static void ShowInternal(Form form)
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

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    }
}
