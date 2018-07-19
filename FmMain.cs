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

            Text = AppInfo.TitleAndVer;
            tray.Icon = Properties.Resources.DemoIcon;
        }

        private void FmMain_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                e.Cancel = true;
                this.Hide();
            }
        }

        private void showFormToolStripMenuItem_Click(object sender, EventArgs e)
        {
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

        private void FmMain_VisibleChanged(object sender, EventArgs e)
        {
            tray.Visible = !Visible;
        }
    }
}
