using System;
using System.Windows.Forms;
using AhDung.WinForm;

namespace AhDung
{
    public partial class FmMain : Form
    {
        public FmMain()
        {
            InitializeComponent();

            this.Text = AppInfo.TitleAndVer;
            tray.Icon = Properties.Resources.DemoIcon;

            //响应该事件，以在收到特定消息时显示自身
            AppSingleton.ShowCodeReceived += showFormToolStripMenuItem_Click;
        }

        private void FmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                tray.Visible = true;
                this.Hide();
            }
        }

        private void showFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
            tray.Visible = false;
            FormHelper.EnsureShow(this);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void tray_DoubleClick(object sender, EventArgs e)
        {
            showFormToolStripMenuItem.PerformClick();
        }

        private void btnExit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
    }
}
