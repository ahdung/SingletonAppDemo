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
            //若想全局禁止多开，将第1个参数改为true
            //若不想显示对方，将第2参数置为false
            AppSingleton.Ensure(false, true);

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new FmMain());
        }
    }
}
