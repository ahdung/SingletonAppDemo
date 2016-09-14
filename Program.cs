using System;
using System.Windows.Forms;

namespace AhDung
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            //若想全局禁止多开，将第1个参数改为false
            //当找到已存在的进程时，执行MeesageHelper.ShowIt，即向它发送显示窗体的特定消息
            AppSingleton.Ensure(true, MessageHelper.ShowIt);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FmMain());
        }
    }
}
