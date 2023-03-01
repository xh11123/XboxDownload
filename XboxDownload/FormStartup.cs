﻿using System.Diagnostics;
using System.Text;

namespace XboxDownload
{
    public partial class FormStartup : Form
    {
        readonly string appName = Form1.appName;
        public FormStartup()
        {
            InitializeComponent();
            if (Thread.CurrentThread.CurrentCulture.Name != "zh-CN") appName = "XboxDownload";
            if (File.Exists(Environment.GetFolderPath(Environment.SpecialFolder.System) + @"\Tasks\" + appName))
            {
                cbStartup.Checked = true;
            }
        }

        private void ButSubmit_Click(object sender, EventArgs e)
        {
            butSubmit.Enabled = false;

            if (cbStartup.Checked)
            {
                string filePath = Path.GetTempPath() + "XboxDownloadTask.xml";
                string xml = String.Format(Properties.Resource.Task, Application.ExecutablePath);
                File.WriteAllText(filePath, xml, Encoding.GetEncoding("UTF-16"));
                using Process p = new();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.StandardInput.WriteLine("schtasks /create /xml \"" + filePath + "\" /tn \"" + appName + "\" /f");
                p.StandardInput.WriteLine("exit");
                p.WaitForExit();
                File.Delete(filePath);
            }
            else
            {
                using Process p = new();
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.StandardInput.WriteLine("schtasks /delete /tn \"" + Form1.appName + "\" /f");
                p.StandardInput.WriteLine("schtasks /delete /tn \"" + appName + "\" /f");
                p.StandardInput.WriteLine("exit");
                p.WaitForExit();
            }
            this.Close();
        }
    }
}
