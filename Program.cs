using System;
using System.Diagnostics;
using System.Windows.Forms;
using AhDung.WinForm;

namespace AhDung
{
    public static class Program
    {
        /// <summary>
        /// 程序主窗口
        /// </summary>
        public static Form MainForm { get; private set; }

        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            //若想全局禁止多开，将第1个参数改为true
            //若不想显示对方，将第2参数置为false
            AppSingleton.Ensure(false, true);

            //响应该事件，以在收到特定消息时显示程序窗体
            //这里的逻辑是：优先根据Process.MainWindowHandle找程序窗体，找不到再用下面Application.Run处指定的MainForm
            //原因是如果程序有多个窗体，例如本例除了主窗体外还有登录窗体，
            //如果只显示主窗体，那么在仅有登录窗体时，showIt相当于没作用，所以要利用MainWindowHandle逮出当前适合显示的窗体，
            //但因为MainWindowHandle并不总是能逮到，例如当所有窗体都Hide时，它返回的是空指针，所以需要手动指定的MainForm作后备
            AppSingleton.ShowCodeReceived += (_, e) => FormHelper.EnsureShow(Control.FromHandle(Process.GetCurrentProcess().MainWindowHandle) as Form ?? MainForm);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            using (var fmLogin = new FmLogin())
            {
                if (fmLogin.ShowDialog() != DialogResult.OK)
                {
                    return;
                }
            }

            Application.Run(MainForm = new FmMain());
        }
    }
}
