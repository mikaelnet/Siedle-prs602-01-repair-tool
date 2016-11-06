using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Siedle.Prs602.RepairTool
{
    public partial class SiedleRepairToolForm : Form
    {
        private SiedleDatabaseManager _databaseManager;

        public SiedleRepairToolForm()
        {
            InitializeComponent();
        }

        private void ExitButton_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OpenButton_Click(object sender, EventArgs e)
        {
            var result = OpenDatabaseDialog.ShowDialog(this);
            if (result == DialogResult.OK && File.Exists(OpenDatabaseDialog.FileName))
            {
                var writer = new StringWriter();
                try
                {
                    _databaseManager = new SiedleDatabaseManager(OpenDatabaseDialog.FileName, writer);
                    _databaseManager.TrimTexts();
                    LogTextBox.Text = writer.ToString();
                    _databaseManager.FixDescriptionTexts();
                    LogTextBox.Text = writer.ToString();
                    _databaseManager.CreateMissingCards();
                    LogTextBox.Text = writer.ToString();
                    _databaseManager.FindNumberingHoles();
                    LogTextBox.Text = writer.ToString();
                    _databaseManager.TestFlagsValidity();
                    LogTextBox.Text = writer.ToString();
                }
                catch (Exception ex)
                {
                    writer.WriteLine(ex);
                    LogTextBox.Text = writer.ToString();
                }
            }
        }
    }
}
