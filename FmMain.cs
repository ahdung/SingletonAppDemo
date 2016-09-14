using System;
using System.Drawing;
using System.Windows.Forms;

namespace AhDung
{
    public partial class FmMain : Form
    {
        bool _exit;

        public FmMain()
        {
            InitializeComponent();
            
            this.Text = AppInfo.TitleAndVer;
            tray.Icon = Icon.FromHandle(Properties.Resources.DemoIcon.GetHicon());

            //响应该事件，以在收到特定消息时显示自身
            MessageHelper.ShowMessageReceived += (s, e) => ShowForm();
        }

        private void FmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_exit && e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                tray.Visible = true;
                e.Cancel = true;
            }
        }

        private void ShowForm()
        {
            tray.Visible = false;
            this.Visible = true;
            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = FormWindowState.Normal;
            }
            this.Activate();//内含置顶动作
        }

        private void showFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ShowForm();
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            _exit = true;
            this.Close();
        }

        private void tray_DoubleClick(object sender, EventArgs e)
        {
            showFormToolStripMenuItem.PerformClick();
        }
    }
}
