﻿using NetFwTypeLib;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.AccessControl;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XboxDownload
{
    public partial class Form1 : Form
    {
        internal static Boolean bServiceFlag = false, bRecordLog = true, bStartup = false;
        internal static ConcurrentDictionary<String, Byte[]> dicHost = new ConcurrentDictionary<String, Byte[]>();
        internal static List<Market> lsMarket = new List<Market>();
        internal static float dpixRatio = 1;
        private readonly DataTable dtHost = new DataTable("Hosts");
        private readonly DnsListen dnsListen;
        private readonly HttpListen httpListen;
        private readonly HttpsListen httpsListen;

        internal readonly static String appName = "Xbox下载助手", hostPath = Application.StartupPath + "\\Hosts";

        public Form1()
        {
            InitializeComponent();

            //清理历史遗留文件
            if (Directory.Exists(Application.StartupPath + "\\Store"))
            {
                Directory.Delete(Application.StartupPath + "\\Store", true);
            }
            if (File.Exists(Application.StartupPath + "\\Domain"))
            {
                File.Delete(Application.StartupPath + "\\Domain");
            }
            if (File.Exists(Application.StartupPath + "\\IP列表(assets1.xboxlive.cn).txt"))
            {
                File.Delete(Application.StartupPath + "\\IP列表(assets1.xboxlive.cn).txt");
            }
            if (File.Exists(Application.StartupPath + "\\使用说明.docx"))
            {
                File.Delete(Application.StartupPath + "\\使用说明.docx");
            }
            if (File.Exists(Application.StartupPath + "\\IP.uplaypc-s-ubisoft.cdn.ubi.com.txt"))
            {
                File.Delete(Application.StartupPath + "\\IP.uplaypc-s-ubisoft.cdn.ubi.com.txt");
            }

            if (!File.Exists(Application.StartupPath + "\\Interop.TaskScheduler.dll"))
                ThreadPool.QueueUserWorkItem(delegate { UpdateFile.Download("Interop.TaskScheduler.dll"); });
            Form1.dpixRatio = Environment.OSVersion.Version.Major >= 10 ? CreateGraphics().DpiX / 96 : Program.Utility.DpiX / 96;
            if (Form1.dpixRatio > 1)
            {
                foreach (ColumnHeader col in lvLog.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
                dgvIpList.RowHeadersWidth = (int)(dgvIpList.RowHeadersWidth * Form1.dpixRatio);
                foreach (DataGridViewColumn col in dgvIpList.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
                dgvHosts.RowHeadersWidth = (int)(dgvHosts.RowHeadersWidth * Form1.dpixRatio);
                foreach (DataGridViewColumn col in dgvHosts.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
                dgvDevice.RowHeadersWidth = (int)(dgvDevice.RowHeadersWidth * Form1.dpixRatio);
                foreach (DataGridViewColumn col in dgvDevice.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
                foreach (ColumnHeader col in lvGame.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
            }

            dnsListen = new DnsListen(this);
            httpListen = new HttpListen(this);
            httpsListen = new HttpsListen(this);

            tbDnsIP.Text = Properties.Settings.Default.DnsIP;
            tbComIP.Text = Properties.Settings.Default.ComIP;
            tbCnIP.Text = Properties.Settings.Default.CnIP;
            tbAppIP.Text = Properties.Settings.Default.AppIP;
            tbPSIP.Text = Properties.Settings.Default.PSIP;
            tbEAIP.Text = Properties.Settings.Default.EAIP;
            ckbEACDN.Checked = Properties.Settings.Default.EACDN;
            tbBattleIP.Text = Properties.Settings.Default.BattleIP;
            ckbBattleCDN.Checked = Properties.Settings.Default.BattleCDN;
            tbEpicIP.Text = Properties.Settings.Default.EpicIP;
            ckbEpicCDN.Checked = Properties.Settings.Default.EpicCDN;
            ckbEAProtocol.Checked = Properties.Settings.Default.EAProtocol;
            ckbRedirect.Checked = Properties.Settings.Default.Redirect;
            ckbTruncation.Checked = Properties.Settings.Default.Truncation;
            ckbLocalUpload.Checked = Properties.Settings.Default.LocalUpload;
            if (string.IsNullOrEmpty(Properties.Settings.Default.LocalPath))
                Properties.Settings.Default.LocalPath = Application.StartupPath + "\\Upload";
            tbLocalPath.Text = Properties.Settings.Default.LocalPath;
            cbListenIP.SelectedIndex = Properties.Settings.Default.ListenIP;
            ckbDnsService.Checked = Properties.Settings.Default.DnsService;
            ckbHttpService.Checked = Properties.Settings.Default.HttpService;
            ckbMicrosoftStore.Checked = Properties.Settings.Default.MicrosoftStore;
            ckbEAStore.Checked = Properties.Settings.Default.EAStore;
            ckbBattleStore.Checked = Properties.Settings.Default.BattleStore;
            ckbEpicStore.Checked = Properties.Settings.Default.EpicStore;
            ckbSteamStore.Checked = Properties.Settings.Default.SteamStore;
            ckbDoH.Checked = Properties.Settings.Default.DoH;

            IPAddress[] ipAddresses = Array.FindAll(Dns.GetHostEntry(string.Empty).AddressList, a => a.AddressFamily == AddressFamily.InterNetwork);
            cbLocalIP.Items.AddRange(ipAddresses);
            if (cbLocalIP.Items.Count >= 1)
            {
                int index = 0;
                if (!string.IsNullOrEmpty(Properties.Settings.Default.LocalIP))
                {
                    for (int i = 0; i < cbLocalIP.Items.Count; i++)
                    {
                        string ip = cbLocalIP.Items[i].ToString();
                        if (Properties.Settings.Default.LocalIP == ip)
                        {
                            index = i;
                            break;
                        }
                        else if (Properties.Settings.Default.LocalIP.StartsWith(Regex.Replace(ip, @"\d+$", "")))
                            index = i;
                    }
                }
                cbLocalIP.SelectedIndex = index;
            }

            cbImportIP.SelectedIndex = 0;

            dtHost.Columns.Add("Enable", typeof(Boolean));
            dtHost.Columns.Add("HostName", typeof(String));
            dtHost.Columns.Add("IPv4", typeof(String));
            dtHost.Columns.Add("Remark", typeof(String));
            if (File.Exists(hostPath))
            {
                try
                {
                    dtHost.ReadXml(hostPath);
                }
                catch { }
                dtHost.AcceptChanges();
            }
            dgvHosts.DataSource = dtHost;
            AddHost();
            cbDomainName.SelectedIndex = 0;
            cbDohDNS.SelectedIndex = 0;

            Form1.lsMarket.AddRange((new List<Market>
            {
                new Market("台湾", "TW", "zh-TW"),
                new Market("香港", "HK", "zh-HK"),
                new Market("日本", "JP", "ja-JP"),
                new Market("美国", "US", "en-US"),

                new Market("阿根廷", "AR", "es-AR"),
                new Market("阿联酋", "AE", "ar-AE"),
                new Market("爱尔兰" ,"IE", "en-IE"),
                new Market("奥地利", "AT", "de-AT"),
                new Market("澳大利亚", "AU", "en-AU"),
                new Market("巴西", "BR", "pt-BR"),
                new Market("比利时", "BE", "nl-BE"),
                new Market("波兰", "PL", "pl-PL"),
                new Market("丹麦", "DK", "da-DK"),
                new Market("德国", "DE", "de-DE"),
                new Market("俄罗斯", "RU", "ru-RU"),
                new Market("法国", "FR", "fr-FR"),
                new Market("芬兰", "FI", "fi-FI"),
                new Market("哥伦比亚", "CO", "es-CO"),
                new Market("韩国", "KR", "ko-KR"),
                new Market("荷兰", "NL", "nl-NL"),
                new Market("加拿大", "CA", "en-CA"),
                new Market("捷克共和国", "CZ", "cs-CZ"),
                //new Market("美国", "US", "en-US"),
                new Market("墨西哥", "MX", "es-MX"),
                new Market("南非", "ZA", "en-ZA"),
                new Market("挪威", "NO", "nb-NO"),
                new Market("葡萄牙", "PT", "pt-PT"),
                //new Market("日本", "JP", "ja-JP"),
                new Market("瑞典", "SE", "sv-SE"),
                new Market("瑞士", "CH", "de-CH"),
                new Market("沙特阿拉伯", "SA", "ar-SA"),
                new Market("斯洛伐克", "SK", "sk-SK"),
                //new Market("台湾", "TW", "zh-TW"),
                new Market("土尔其", "TR", "tr-TR"),
                new Market("西班牙", "ES", "es-ES"),
                new Market("希腊", "GR", "el-GR"),
                //new Market("香港", "HK", "zh-HK"),
                new Market("新加坡", "SG", "en-SG"),
                new Market("新西兰", "NZ", "en-NZ"),
                new Market("匈牙利", "HU", "hu-HU"),
                new Market("以色列", "IL", "he-IL"),
                new Market("意大利", "IT", "it-IT"),
                new Market("印度", "IN", "en-IN"),
                new Market("英国", "GB", "en-GB"),
                new Market("智利", "CL", "es-CL"),
                new Market("中国", "CN", "zh-CN")
            }).ToArray());
            cbGameMarket.Items.AddRange(Form1.lsMarket.ToArray());
            cbGameMarket.SelectedIndex = 0;
            pbGame.Image = pbGame.InitialImage;
            ToolTip toolTip1 = new ToolTip
            {
                AutoPopDelay = 30000,
                IsBalloon = true
            };
            toolTip1.SetToolTip(this.labelDNS, "常用 DNS 服务器\n114.114.114.114 (114)\n180.76.76.76 (百度)\n223.5.5.5 (阿里)\n119.29.29.29 (腾讯)\n208.67.220.220 (OpenDns)\n8.8.8.8 (Google)\n168.126.63.1 (韩国)");
            toolTip1.SetToolTip(this.labelCom, "包括以下com游戏下载域名\nassets1.xboxlive.com\nassets2.xboxlive.com\ndlassets.xboxlive.com\ndlassets2.xboxlive.com\nd1.xboxlive.com\nd2.xboxlive.com\nxvcf1.xboxlive.com\nxvcf2.xboxlive.com\n\n以上域名不能使用 cn IP");
            toolTip1.SetToolTip(this.labelCn, "包括以下cn游戏下载域名\nassets1.xboxlive.cn\nassets2.xboxlive.cn\ndlassets.xboxlive.cn\ndlassets2.xboxlive.cn\nd1.xboxlive.cn\nd2.xboxlive.cn\n\n以上域名可以共用 com IP");
            toolTip1.SetToolTip(this.labelApp, "包括以下应用下载域名\ndl.delivery.mp.microsoft.com\ntlu.dl.delivery.mp.microsoft.com");
            toolTip1.SetToolTip(this.labelPS, "包括以下游戏下载域名\ngst.prod.dl.playstation.net\ngs2.ww.prod.dl.playstation.net\nzeus.dl.playstation.net\nares.dl.playstation.net");
            toolTip1.SetToolTip(this.labelEA, "包括以下游戏下载域名\norigin-a.akamaihd.net\n\n速度不正常请点击右下角 “修复 EA app”");
            toolTip1.SetToolTip(this.labelBattle, "包括以下游戏下载域名\nblzddist1-a.akamaihd.net\nblzddist2-a.akamaihd.net\nblzddist3-a.akamaihd.net");
            toolTip1.SetToolTip(this.labelEpic, "包括以下游戏下载域名\nepicgames-download1-1251447533.file.myqcloud.com");
            toolTip1.SetToolTip(this.ckbDoH, "使用 阿里云DoH(加密DNS) 解析域名IP，\n防止DNS被劫持污染。\nXbox各种联网问题可以勾选此选项。");

            LinkRefreshDrive_LinkClicked(null, null);
            if (bStartup)
            {
                ButStart_Click(null, null);
                if (Properties.Settings.Default.EAStore) ThreadPool.QueueUserWorkItem(delegate { RestartService("EABackgroundService"); });
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            if (DateTime.Compare(DateTime.Now, new DateTime(Properties.Settings.Default.NextUpdate)) >= 0)
            {
                tsmUpdate.Enabled = false;
                ThreadPool.QueueUserWorkItem(delegate { UpdateFile.Start(true, this); });
            }
        }

        private void TsmUpdate_Click(object sender, EventArgs e)
        {
            tsmUpdate.Enabled = false;
            Thread thread = new Thread(new ThreadStart(() => UpdateFile.Start(false, this)))
            {
                IsBackground = true
            };
            thread.Start();
        }

        private void TsmiStartup_Click(object sender, EventArgs e)
        {
            FormStartup dialog = new FormStartup();
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void TsmiExit_Click(object sender, EventArgs e)
        {
            this.FormClosing -= new FormClosingEventHandler(this.Form1_FormClosing);
            Form1_FormClosed(null, null);
        }

        private void NotifyIcon1_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                TsmiShow_Click(null, null);
            }
        }

        private void TsmiShow_Click(object sender, EventArgs e)
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.Activate();
        }

        private void TsmProductManual_Click(object sender, EventArgs e)
        {
            tsmProductManual.Enabled = false;
            FileInfo fi = new FileInfo(Application.StartupPath + "\\" + UpdateFile.pdfFile);
            if (!fi.Exists)
            {
                UpdateFile.bDownloadEnd = false;
                ThreadPool.QueueUserWorkItem(delegate { UpdateFile.Download(fi.Name); });
                while (!UpdateFile.bDownloadEnd)
                {
                    Application.DoEvents();
                }
                fi.Refresh();
            }
            if (fi.Exists)
                Process.Start(fi.FullName);
            else
                MessageBox.Show("文件不存在", "Error", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
            tsmProductManual.Enabled = true;
        }

        private void TsmTeaching_Click(object sender, EventArgs e)
        {
            var tsm = sender as ToolStripMenuItem;
            Process.Start(tsm.Tag.ToString());
        }

        private void TsmAbout_Click(object sender, EventArgs e)
        {
            FormAbout dialog = new FormAbout();
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.KeyCode)
            {
                case Keys.M:
                    if (e.Control && e.Alt)
                    {
                        using (FileStream fs = File.Create(Application.ExecutablePath + ".md5"))
                        {
                            Byte[] b = new UTF8Encoding(true).GetBytes(UpdateFile.GetPathMD5(Application.ExecutablePath));
                            fs.Write(b, 0, b.Length);
                            fs.Close();
                        }
                    }
                    break;
            }
        }

        bool bTips = true;
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
            if (bTips && !bStartup)
            {
                bTips = false;
                this.notifyIcon1.ShowBalloonTip(5, appName, "最小化到系统托盘", ToolTipIcon.Info);
            }
            e.Cancel = true;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (bServiceFlag) ButStart_Click(null, null);
            if (Form1.bStartup) Application.Exit();
            this.Dispose();
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            this.Show();
            switch (tabControl1.SelectedTab.Name)
            {
                case "tabGames":
                    if (Environment.OSVersion.Version.Major >= 10)
                    {
                        if (cbGameXGP1.Items.Count == 0 || cbGameXGP1.Items[0].ToString().Contains("(加载失败)") || cbGameXGP1.Items[cbGameXGP1.Items.Count - 1].ToString().Contains("(加载失败)"))
                        {
                            cbGameXGP1.Items.Clear();
                            cbGameXGP1.Items.Add(new Product("最受欢迎 Xbox Game Pass 游戏 (加载中)", "0"));
                            cbGameXGP1.SelectedIndex = 0;
                            ThreadPool.QueueUserWorkItem(delegate { GameXGPRecentlyAdded(1); });
                        }
                        if (cbGameXGP2.Items.Count == 0 || cbGameXGP2.Items[0].ToString().Contains("(加载失败)") || cbGameXGP2.Items[cbGameXGP2.Items.Count - 1].ToString().Contains("(加载失败)"))
                        {
                            cbGameXGP2.Items.Clear();
                            cbGameXGP2.Items.Add(new Product("近期新增 Xbox Game Pass 游戏 (加载中)", "0"));
                            cbGameXGP2.SelectedIndex = 0;
                            ThreadPool.QueueUserWorkItem(delegate { GameXGPRecentlyAdded(2); });
                        }
                    }
                    else if (cbGameXGP1.Items.Count == 0)
                    {
                        cbGameXGP1.Items.Add(new Product("最受欢迎 Xbox Game Pass 游戏 (不支持)", "0"));
                        cbGameXGP1.SelectedIndex = 0;
                        cbGameXGP2.Items.Add(new Product("近期新增 Xbox Game Pass 游戏 (不支持)", "0"));
                        cbGameXGP2.SelectedIndex = 0;
                    }
                    if (flpGameWithGold.Controls.Count == 0)
                    {
                        ThreadPool.QueueUserWorkItem(delegate { GameWithGold(); });
                    }
                    break;
                case "tabTool":
                    if (cbAppxDrive.Items.Count == 0)
                    {
                        LinkAppxRefreshDrive_LinkClicked(null, null);
                        GetEACdn();
                    }
                    break;
            }
        }

        #region 选项卡-服务
        private void ButBrowse_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog dlg = new FolderBrowserDialog
            {
                Description = "选择本地上传文件夹",
                SelectedPath = tbLocalPath.Text
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                tbLocalPath.Text = dlg.SelectedPath;
            }
        }

        public void ButStart_Click(object sender, EventArgs e)
        {
            butStart.Enabled = false;
            if (bServiceFlag)
            {
                bServiceFlag = false;
                WriteHost(false);
                if (string.IsNullOrEmpty(Properties.Settings.Default.DnsIP)) tbDnsIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.ComIP)) tbComIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.CnIP)) tbCnIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.AppIP)) tbAppIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.PSIP)) tbPSIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.EAIP)) tbEAIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.BattleIP)) tbBattleIP.Clear();
                if (string.IsNullOrEmpty(Properties.Settings.Default.EpicIP)) tbEpicIP.Clear();
                pictureBox1.Image = Properties.Resources.Xbox1;
                linkTestDns.Enabled = linkEADesktopRecovery.Enabled = false;
                butStart.Text = "开始监听";
                foreach (Control control in this.groupBox1.Controls)
                {
                    if ((control is TextBox || control is CheckBox || control is Button || control is ComboBox) && control != butStart)
                        control.Enabled = true;
                }
                cbLocalIP.Enabled = true;
                dnsListen.Close();
                httpListen.Close();
                httpsListen.Close();
                Program.SystemSleep.RestoreForCurrentThread();
                if (Properties.Settings.Default.MicrosoftStore) ThreadPool.QueueUserWorkItem(delegate { RestartService("DoSvc"); });
            }
            else
            {
                bool bRuleAdd = true;
                try
                {
                    INetFwPolicy2 policy2 = (INetFwPolicy2)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwPolicy2"));
                    foreach (INetFwRule rule in policy2.Rules)
                    {
                        if (rule.Name == Form1.appName)
                        {
                            if (bRuleAdd && rule.ApplicationName == Application.ExecutablePath && rule.Direction == NET_FW_RULE_DIRECTION_.NET_FW_RULE_DIR_IN && rule.Protocol == (int)NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_ANY && rule.Action == NET_FW_ACTION_.NET_FW_ACTION_ALLOW && rule.Profiles == (int)NET_FW_PROFILE_TYPE2_.NET_FW_PROFILE2_ALL && rule.Enabled)
                                bRuleAdd = false;
                            else
                                policy2.Rules.Remove(rule.Name);
                        }
                        else if (String.Equals(rule.ApplicationName, Application.ExecutablePath, StringComparison.CurrentCultureIgnoreCase))
                        {
                            policy2.Rules.Remove(rule.Name);
                        }
                    }
                    if (bRuleAdd)
                    {
                        INetFwRule rule = (INetFwRule)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwRule"));
                        rule.Name = Form1.appName;
                        rule.ApplicationName = Application.ExecutablePath;
                        rule.Enabled = true;
                        policy2.Rules.Add(rule);
                    }
                }
                catch { }

                string dnsIP = string.Empty;
                if (!string.IsNullOrEmpty(tbDnsIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbDnsIP.Text, out IPAddress ipAddress))
                    {
                        dnsIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("DNS 服务器 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbDnsIP.Focus();
                        return;
                    }
                }
                string comIP = string.Empty;
                if (!string.IsNullOrEmpty(tbComIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbComIP.Text, out IPAddress ipAddress))
                    {
                        comIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 com 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbComIP.Focus();
                        return;
                    }
                }
                string cnIP = string.Empty;
                if (!string.IsNullOrEmpty(tbCnIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbCnIP.Text, out IPAddress ipAddress))
                    {
                        cnIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 cn 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbCnIP.Focus();
                        return;
                    }
                }
                string appIP = string.Empty;
                if (!string.IsNullOrEmpty(tbAppIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbAppIP.Text, out IPAddress ipAddress))
                    {
                        appIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定应用下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbAppIP.Focus();
                        return;
                    }
                }
                string psIP = string.Empty;
                if (!string.IsNullOrEmpty(tbPSIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbPSIP.Text, out IPAddress ipAddress))
                    {
                        psIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 PSN 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbPSIP.Focus();
                        return;
                    }
                }
                string eaIP = string.Empty;
                if (!string.IsNullOrEmpty(tbEAIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbEAIP.Text, out IPAddress ipAddress))
                    {
                        eaIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 EA 下载域名 IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbEAIP.Focus();
                        return;
                    }
                }
                string battleIP = string.Empty;
                if (!string.IsNullOrEmpty(tbBattleIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbBattleIP.Text, out IPAddress ipAddress))
                    {
                        battleIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 战网国际服域名IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbBattleIP.Focus();
                        return;
                    }
                }
                string epicIP = string.Empty;
                if (!string.IsNullOrEmpty(tbEpicIP.Text.Trim()))
                {
                    if (IPAddress.TryParse(tbEpicIP.Text, out IPAddress ipAddress))
                    {
                        epicIP = ipAddress.ToString();
                    }
                    else
                    {
                        MessageBox.Show("指定 Epic 下载域名IP 不正确", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        tbEpicIP.Focus();
                        return;
                    }
                }

                Properties.Settings.Default.DnsIP = dnsIP;
                Properties.Settings.Default.ComIP = comIP;
                Properties.Settings.Default.CnIP = cnIP;
                Properties.Settings.Default.AppIP = appIP;
                Properties.Settings.Default.PSIP = psIP;
                Properties.Settings.Default.EAIP = eaIP;
                Properties.Settings.Default.EACDN = ckbEACDN.Checked;
                Properties.Settings.Default.EAProtocol = ckbEAProtocol.Checked;
                Properties.Settings.Default.BattleIP = battleIP;
                Properties.Settings.Default.BattleCDN = ckbBattleCDN.Checked;
                Properties.Settings.Default.EpicIP = epicIP;
                Properties.Settings.Default.EpicCDN = ckbEpicCDN.Checked;
                Properties.Settings.Default.Redirect = ckbRedirect.Checked;
                Properties.Settings.Default.Truncation = ckbTruncation.Checked;
                Properties.Settings.Default.LocalUpload = ckbLocalUpload.Checked;
                Properties.Settings.Default.LocalPath = tbLocalPath.Text;
                Properties.Settings.Default.ListenIP = cbListenIP.SelectedIndex;
                Properties.Settings.Default.DnsService = ckbDnsService.Checked;
                Properties.Settings.Default.HttpService = ckbHttpService.Checked;
                Properties.Settings.Default.MicrosoftStore = ckbMicrosoftStore.Checked;
                Properties.Settings.Default.EAStore = ckbEAStore.Checked;
                Properties.Settings.Default.BattleStore = ckbBattleStore.Checked;
                Properties.Settings.Default.EpicStore = ckbEpicStore.Checked;
                Properties.Settings.Default.SteamStore = ckbSteamStore.Checked;
                Properties.Settings.Default.DoH = ckbDoH.Checked;
                Properties.Settings.Default.Save();

                string resultInfo = string.Empty;
                using (Process p = new Process())
                {
                    p.StartInfo = new ProcessStartInfo("netstat", @"-aon")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false,
                        RedirectStandardOutput = true
                    };
                    p.Start();
                    resultInfo = p.StandardOutput.ReadToEnd();
                    p.Close();
                }
                Match result = Regex.Match(resultInfo, @"(?<protocol>TCP|UDP)\s+(?<ip>[^\s]+):(?<port>80|443|53)\s+[^\s]+\s+(?<status>[^\s]+\s+)?(?<pid>\d+)");
                if (result.Success)
                {
                    ConcurrentDictionary<Int32, Process> dic = new ConcurrentDictionary<Int32, Process>();
                    StringBuilder sb = new StringBuilder();
                    while (result.Success)
                    {
                        string ip = result.Groups["ip"].Value;
                        if (ip == "0.0.0.0" || ip == Properties.Settings.Default.LocalIP)
                        {
                            string protocol = result.Groups["protocol"].Value;
                            if (protocol == "TCP" && result.Groups["status"].Value.Trim() == "LISTENING" || protocol == "UDP")
                            {
                                int port = Convert.ToInt32(result.Groups["port"].Value);
                                if (port == 53 && Properties.Settings.Default.DnsService || port == 80 && Properties.Settings.Default.HttpService || port == 443 && Properties.Settings.Default.EAStore)
                                {
                                    int pid = int.Parse(result.Groups["pid"].Value);
                                    if (!dic.ContainsKey(pid) && pid != 0)
                                    {
                                        sb.AppendLine(protocol + "\t" + ip + ":" + port);
                                        if (pid == 4)
                                        {
                                            dic.TryAdd(pid, null);
                                            sb.AppendLine("系统服务");
                                        }
                                        else
                                        {
                                            try
                                            {
                                                Process proc = Process.GetProcessById(pid);
                                                dic.TryAdd(pid, proc);
                                                string filename = proc.MainModule.FileName;
                                                sb.AppendLine(filename);
                                            }
                                            catch
                                            {
                                                sb.AppendLine("未知");
                                            }
                                        }
                                    }
                                }
                            }
                        }
                        result = result.NextMatch();
                    }
                    if (dic.Count >= 1 && MessageBox.Show("检测到以下端口被占用\n" + sb.ToString() + "\n是否尝试强制结束占用端口程序？", "启用服务失败", MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        foreach (var item in dic)
                        {
                            if (item.Key == 4)
                            {
                                ServiceController[] services = ServiceController.GetServices();
                                foreach (ServiceController service in services)
                                {
                                    switch (service.ServiceName)
                                    {
                                        case "MsDepSvc":        //Web Deployment Agent Service (MsDepSvc)
                                        case "PeerDistSvc":     //BranchCache (PeerDistSvc)
                                        case "ReportServer":    //SQL Server Reporting Services (ReportServer)
                                        case "SyncShareSvc":    //Sync Share Service (SyncShareSvc)
                                        case "W3SVC":           //World Wide Web Publishing Service (W3SVC)
                                        //case "WAS":             //Internet Information Server (WAS, IISADMIN)
                                        //case "IISADMIN":
                                            if (service.Status == ServiceControllerStatus.Running)
                                            {
                                                service.Stop();
                                                service.WaitForStatus(ServiceControllerStatus.Stopped);
                                            }
                                            break;
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    item.Value.Kill();
                                }
                                catch { }
                            }
                        }
                    }
                }

                bServiceFlag = true;
                pictureBox1.Image = Properties.Resources.Xbox2;
                butStart.Text = "停止监听";
                foreach (Control control in this.groupBox1.Controls)
                {
                    if (control is TextBox || control is CheckBox || control is Button || control is ComboBox)
                        control.Enabled = false;
                }
                cbLocalIP.Enabled = false;
                WriteHost(true);
                if (Properties.Settings.Default.MicrosoftStore) ThreadPool.QueueUserWorkItem(delegate { RestartService("DoSvc"); });
                if (Properties.Settings.Default.DnsService)
                {
                    linkTestDns.Enabled = true;
                    Thread thread = new Thread(new ThreadStart(dnsListen.Listen))
                    {
                        IsBackground = true
                    };
                    thread.Start();
                }
                if (Properties.Settings.Default.HttpService)
                {
                    Thread thread = new Thread(new ThreadStart(httpListen.Listen))
                    {
                        IsBackground = true
                    };
                    thread.Start();
                }
                if (Properties.Settings.Default.EAStore || Properties.Settings.Default.SteamStore)
                {
                    if(Properties.Settings.Default.EAStore) linkEADesktopRecovery.Enabled = true;
                    Thread thread = new Thread(new ThreadStart(httpsListen.Listen))
                    {
                        IsBackground = true
                    };
                    thread.Start();
                }
                Program.SystemSleep.PreventForCurrentThread(false);
            }
            butStart.Enabled = true;
        }

        private void WriteHost(bool add)
        {
            if (!(Properties.Settings.Default.MicrosoftStore || Properties.Settings.Default.EAStore || Properties.Settings.Default.BattleStore || Properties.Settings.Default.EpicStore || Properties.Settings.Default.SteamStore)) return;

            string sHostsPath = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
            try
            {
                FileInfo fi = new FileInfo(sHostsPath);
                if (!fi.Exists)
                {
                    StreamWriter sw = fi.CreateText();
                    sw.Close();
                    fi.Refresh();
                }
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes = FileAttributes.Normal;
                FileSecurity fSecurity = fi.GetAccessControl();
                fSecurity.AddAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                string sHosts = string.Empty;
                using (StreamReader sw = new StreamReader(sHostsPath))
                {
                    sHosts = sw.ReadToEnd();
                }
                sHosts = Regex.Replace(sHosts, @"# Added by " + Form1.appName + "\r\n(.*\r\n)+# End of " + Form1.appName + "\r\n", "");
                if (add)
                {
                    string comIP = string.IsNullOrEmpty(Properties.Settings.Default.ComIP) ? Properties.Settings.Default.LocalIP : Properties.Settings.Default.ComIP;
                    if (!Properties.Settings.Default.DnsService && Properties.Settings.Default.HttpService && Properties.Settings.Default.MicrosoftStore && string.IsNullOrEmpty(Properties.Settings.Default.ComIP))
                        tbComIP.Text = comIP;
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("# Added by " + Form1.appName);
                    if (Properties.Settings.Default.MicrosoftStore)
                    {
                        sb.AppendLine(comIP + " assets1.xboxlive.com");
                        sb.AppendLine(comIP + " assets2.xboxlive.com");
                        sb.AppendLine(comIP + " dlassets.xboxlive.com");
                        sb.AppendLine(comIP + " dlassets2.xboxlive.com");
                        sb.AppendLine(comIP + " d1.xboxlive.com");
                        sb.AppendLine(comIP + " d2.xboxlive.com");
                        sb.AppendLine(comIP + " xvcf1.xboxlive.com");
                        sb.AppendLine(comIP + " xvcf2.xboxlive.com");
                        if (!string.IsNullOrEmpty(Properties.Settings.Default.CnIP))
                        {
                            sb.AppendLine(Properties.Settings.Default.CnIP + " assets1.xboxlive.cn");
                            sb.AppendLine(Properties.Settings.Default.CnIP + " assets2.xboxlive.cn");
                            sb.AppendLine(Properties.Settings.Default.CnIP + " dlassets.xboxlive.cn");
                            sb.AppendLine(Properties.Settings.Default.CnIP + " dlassets2.xboxlive.cn");
                            sb.AppendLine(Properties.Settings.Default.CnIP + " d1.xboxlive.cn");
                            sb.AppendLine(Properties.Settings.Default.CnIP + " d2.xboxlive.cn");
                        }
                        if (!string.IsNullOrEmpty(Properties.Settings.Default.AppIP))
                        {
                            sb.AppendLine(Properties.Settings.Default.AppIP + " dl.delivery.mp.microsoft.com");
                            sb.AppendLine(Properties.Settings.Default.AppIP + " tlu.dl.delivery.mp.microsoft.com");
                        }
                    }
                    if (Properties.Settings.Default.EAStore)
                    {
                        if (Properties.Settings.Default.EACDN)
                        {
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " api1.origin.com");
                            //sb.AppendLine(Properties.Settings.Default.LocalIP + " ssl-lvlt.cdn.ea.com");
                            sb.AppendLine("0.0.0.0 ssl-lvlt.cdn.ea.com");
                        }
                        if (!string.IsNullOrEmpty(Properties.Settings.Default.EAIP))
                        {
                            sb.AppendLine(Properties.Settings.Default.EAIP + " origin-a.akamaihd.net");
                        }
                    }
                    if (Properties.Settings.Default.BattleStore)
                    {
                        if (Properties.Settings.Default.BattleCDN)
                        {
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " us.cdn.blizzard.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " eu.cdn.blizzard.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " kr.cdn.blizzard.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " level3.blizzard.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " blizzard.gcdn.cloudn.co.kr");
                        }
                        if (!string.IsNullOrEmpty(Properties.Settings.Default.BattleIP))
                        {
                            sb.AppendLine(Properties.Settings.Default.BattleIP + " blzddist1-a.akamaihd.net");
                            sb.AppendLine(Properties.Settings.Default.BattleIP + " blzddist2-a.akamaihd.net");
                            sb.AppendLine(Properties.Settings.Default.BattleIP + " blzddist3-a.akamaihd.net");
                        }
                    }
                    if (Properties.Settings.Default.EpicStore)
                    {
                        if (Properties.Settings.Default.EpicCDN)
                        {
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " epicgames-download1.akamaized.net");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " download.epicgames.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " download2.epicgames.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " download3.epicgames.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " download4.epicgames.com");
                            sb.AppendLine(Properties.Settings.Default.LocalIP + " fastly-download.epicgames.com");
                        }
                        if (!string.IsNullOrEmpty(Properties.Settings.Default.EpicIP))
                        {
                            sb.AppendLine(Properties.Settings.Default.EpicIP + " epicgames -download1-1251447533.file.myqcloud.com"); 
                        }
                    }
                    if (Properties.Settings.Default.SteamStore)
                    {
                        sb.AppendLine(Properties.Settings.Default.LocalIP + " store.steampowered.com");
                        sb.AppendLine(Properties.Settings.Default.LocalIP + " steamcommunity.com");
                        sb.AppendLine("0.0.0.0 fonts.googleapis.com");
                    }
                    foreach (var host in dicHost)
                    {
                        if (host.Key == Environment.MachineName)
                            continue;
                        sb.AppendLine(string.Format("{0}.{1}.{2}.{3} {4}", host.Value[0], host.Value[1], host.Value[2], host.Value[3], host.Key));
                    }
                    sb.AppendLine("# End of " + Form1.appName);
                    sHosts = sb.ToString() + sHosts;
                }
                using (StreamWriter sw = new StreamWriter(sHostsPath, false))
                {
                    sw.Write(sHosts.Trim() + "\r\n");
                }
                fSecurity.RemoveAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
            }
            catch (Exception e)
            {
                if (add) MessageBox.Show("修改系统Hosts文件失败，错误信息：" + e.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void RestartService(string servicename)
        {
            ServiceController[] services = ServiceController.GetServices();
            foreach (ServiceController service in services)
            {
                if (service.ServiceName.Equals(servicename))
                {
                    try
                    {
                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped);
                        }
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                    }
                    catch { }
                    break;
                }
            }
        }

        private void LvLog_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lvLog.SelectedItems.Count == 1)
                {
                    tsmCopy.Visible = true;
                    tsmUseIP.Visible = tsmExportRule.Visible = tsmSpeedTest.Visible = false;
                    contextMenuStrip1.Show(MousePosition.X, MousePosition.Y);
                }
            }
        }

        private void TsmCopy_Click(object sender, EventArgs e)
        {
            string text = lvLog.SelectedItems[0].SubItems[1].Text;
            Clipboard.SetDataObject(text);
            if (Regex.IsMatch(text, @"^https?://(origin-a\.akamaihd\.net|ssl-lvlt\.cdn\.ea\.com|lvlt\.cdn\.ea\.com)"))
            {
                MessageBox.Show("离线包安装方法：下载完成后删除安装目录下的所有文件，把解压缩文件复制到安装目录，回到 EA app 或者 Origin 选择继续下载，等待游戏验证完成后即可。", "提示信息", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private void CbLocalIP_SelectedIndexChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.LocalIP = cbLocalIP.Text;
            Properties.Settings.Default.Save();
        }

        private void LinkTestDns_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormDns dialog = new FormDns();
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void LinkEADesktopRecovery_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MessageBox.Show("EA app 没法正常监听或者下载，请先退出 EA app, 然后再点击 “是” 修复。", "修复 EA app", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                bool completed = false;
                ServiceController[] services = ServiceController.GetServices();
                foreach (ServiceController service in services)
                {
                    if (service.ServiceName.Equals("EABackgroundService"))
                    {
                        Process[] processes = Process.GetProcessesByName("EADesktop");
                        if (processes.Length == 1)
                        {
                            try
                            {
                                processes[0].Kill();
                            }
                            catch { }
                        }
                        if (service.Status == ServiceControllerStatus.Running)
                        {
                            service.Stop();
                            service.WaitForStatus(ServiceControllerStatus.Stopped);
                        }
                        service.Start();
                        service.WaitForStatus(ServiceControllerStatus.Running);
                        MessageBox.Show("修复完成，请重启 EA app. \n\n如果还不能正常工作，可以点击 EA app 左上角功能菜单\n帮助 -> 应用程序恢复 -> 清理缓存", "修复 EA app", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        completed = true;
                        break;
                    }
                }
                if (completed)
                {
                    using (var key = Microsoft.Win32.Registry.LocalMachine)
                    {
                        var rk = key.OpenSubKey(@"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop");
                        if (rk == null) rk = key.OpenSubKey(@"SOFTWARE\Electronic Arts\EA Desktop");
                        if (rk != null)
                        {
                            string path = (string)rk.GetValue("LauncherAppPath", null);
                            if (File.Exists(path))
                                Process.Start(path);
                            rk.Close();
                        }
                    }
                }
                else
                {
                    MessageBox.Show("没有找到服务。", "修复 EA app", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void CbRecordLog_CheckedChanged(object sender, EventArgs e)
        {
            bRecordLog = ckbRecordLog.Checked;
        }

        private void LinkClearLog_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            lvLog.Items.Clear();
        }
        #endregion

        #region 选项卡-测速
        private void DgvIpList_CellMouseDoubleClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex == -1) return;
            if (e.Button == MouseButtons.Left && dgvIpList.Columns[dgvIpList.CurrentCell.ColumnIndex].Name == "Col_Speed" && dgvIpList.Rows[e.RowIndex].Tag != null)
            {
                string msg = dgvIpList.Rows[e.RowIndex].Tag.ToString().Trim();
                if (!string.IsNullOrEmpty(msg))
                    MessageBox.Show(msg, "Request Headers", MessageBoxButtons.OK, MessageBoxIcon.None);
            }
        }

        private void DgvIpList_CellMouseDown(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.Button != MouseButtons.Right) return;
            string host = string.Empty;
            Match result = Regex.Match(groupBox4.Text, @"\((?<host>[^\)]+)\)");
            if (result.Success) host = result.Groups["host"].Value;
            dgvIpList.ClearSelection();
            DataGridViewRow dgvr = dgvIpList.Rows[e.RowIndex];
            dgvr.Selected = true;
            tsmCopy.Visible = false;
            tsmUseIP.Visible = tsmExportRule.Visible = true;
            foreach (ToolStripMenuItem tsm in this.tsmUseIP.DropDownItems)
            {
                if (tsm.Name == "tsmUseIPHosts")
                    continue;
                tsm.Visible = false;
            }
            switch (host)
            {
                case "assets1.xboxlive.com":
                case "assets2.xboxlive.com":
                case "dlassets.xboxlive.com":
                case "dlassets2.xboxlive.com":
                case "d1.xboxlive.com":
                case "d2.xboxlive.com":
                case "xvcf1.xboxlive.com":
                case "xvcf2.xboxlive.com":
                    tsmUseIPXbox.Text = "指定 com 下载域名 IP";
                    tsmUseIPXbox.Visible = tsmUseIPXbox2.Visible = true;
                    break;
                case "assets1.xboxlive.cn":
                case "assets2.xboxlive.cn":
                case "dlassets.xboxlive.cn":
                case "dlassets2.xboxlive.cn":
                case "d1.xboxlive.cn":
                case "d2.xboxlive.cn":
                    tsmUseIPXbox.Text = "指定 cn 下载域名 IP (碰到安装停止可使用国际域名测速)";
                    tsmUseIPXbox.Visible = true;
                    break;
                case "dl.delivery.mp.microsoft.com":
                case "tlu.dl.delivery.mp.microsoft.com":
                    tsmUseIPApp.Visible = true;
                    break;
                case "gst.prod.dl.playstation.net":
                case "gs2.ww.prod.dl.playstation.net":
                case "zeus.dl.playstation.net":
                case "ares.dl.playstation.net":
                    tsmUseIPPS.Visible = true;
                    break;
                case "origin-a.akamaihd.net":
                case "blzddist1-a.akamaihd.net":
                    tsmUseIPEaBattle.Visible = tsmUseIPNS.Visible = true;
                    break;
                case "epicgames-download1-1251447533.file.myqcloud.com":
                    tsmUseIPEpic.Visible = true;
                    break;
                default:
                    break;
            }
            tsmSpeedTest.Visible = true;
            tsmSpeedTest.Enabled = !isSpeedTest;
            contextMenuStrip1.Show(MousePosition.X, MousePosition.Y);
        }

        private void TsmUseIP_Click(object sender, EventArgs e)
        {
            if (bServiceFlag)
            {
                MessageBox.Show("请先停止监听后再设置。", "使用指定IP", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if (dgvIpList.SelectedRows.Count != 1) return;
            tabControl1.SelectedTab = tabService;
            DataGridViewRow dgvr = dgvIpList.SelectedRows[0];
            string ip = dgvr.Cells["Col_IP"].Value.ToString();
            ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
            switch (tsmi.Name)
            {
                case "tsmUseIPXbox":
                    if (tsmUseIPXbox.Text == "指定 com 下载域名 IP")
                    {
                        tbComIP.Text = ip;
                        tbComIP.Focus();
                    }
                    else
                    {
                        tbCnIP.Text = ip;
                        tbCnIP.Focus();
                    }
                    break;
                case "tsmUseIPXbox2":
                    tbComIP.Text = ip;
                    tbCnIP.Text = ip;
                    tbComIP.Focus();
                    break;
                case "tsmUseIPApp":
                    tbAppIP.Text = ip;
                    tbAppIP.Focus();
                    break;
                case "tsmUseIPXboxApp":
                    if (tsmUseIPXbox.Text == "指定 com 下载域名 IP")
                    {
                        tbComIP.Text = ip;
                        tbComIP.Focus();
                    }
                    else
                    {
                        tbCnIP.Text = ip;
                        tbCnIP.Focus();
                    }
                    tbAppIP.Text = ip;
                    break;
                case "tsmUseIPPS":
                    tbPSIP.Text = ip;
                    tbPSIP.Focus();
                    break;
                case "tsmUseIPEaBattle":
                    tbEAIP.Text = tbBattleIP.Text = ip;
                    tbEAIP.Focus();
                    break;
                case "tsmUseIPEpic":
                    tbEpicIP.Text = ip;
                    tbEpicIP.Focus();
                    break;
            }
        }

        private void TsmUseIPNS_Click(object sender, EventArgs e)
        {
            if (dgvIpList.SelectedRows.Count != 1) return;
            DataGridViewRow dgvr = dgvIpList.SelectedRows[0];
            string[] hostNames = new string[] { "atum.hac.lp1.d4c.nintendo.net" };
            foreach (string hostName in hostNames)
            {
                DataRow[] rows = dtHost.Select("HostName='" + hostName + "'");
                if (rows.Length >= 1)
                {
                    rows[0]["Enable"] = true;
                    rows[0]["IPv4"] = dgvr.Cells["Col_IP"].Value;
                    rows[0]["Remark"] = "Nintendo Switch下载域名";
                }
                else
                {
                    DataRow dr = dtHost.NewRow();
                    dr["Enable"] = true;
                    dr["HostName"] = hostName;
                    dr["IPv4"] = dgvr.Cells["Col_IP"].Value;
                    dr["Remark"] = "Nintendo Switch下载域名";
                    dtHost.Rows.Add(dr);
                }
            }
            ButHostSave_Click(null, null);
            tabControl1.SelectedTab = tabHosts;
        }

        private void TsmUseIPHosts_Click(object sender, EventArgs e)
        {
            if (dgvIpList.SelectedRows.Count != 1) return;
            DataGridViewRow dgvr = dgvIpList.SelectedRows[0];
            string host = dgvIpList.Tag.ToString();
            string ip = dgvr.Cells["Col_IP"].Value.ToString();

            string sHostsPath = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
            try
            {
                FileInfo fi = new FileInfo(sHostsPath);
                if (!fi.Exists)
                {
                    StreamWriter sw = fi.CreateText();
                    sw.Close();
                    fi.Refresh();
                }
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes = FileAttributes.Normal;
                FileSecurity fSecurity = fi.GetAccessControl();
                fSecurity.AddAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                string sHosts = string.Empty;
                using (StreamReader sw = new StreamReader(sHostsPath))
                {
                    sHosts = sw.ReadToEnd();
                }
                StringBuilder sb = new StringBuilder();
                string msg = string.Empty;
                switch (host)
                {
                    case "assets1.xboxlive.com":
                    case "assets2.xboxlive.com":
                    case "dlassets.xboxlive.com":
                    case "dlassets2.xboxlive.com":
                    case "d1.xboxlive.com":
                    case "d2.xboxlive.com":
                    case "xvcf1.xboxlive.com":
                    case "xvcf2.xboxlive.com":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+(assets1|assets2|dlassets|dlassets2|d1|d2|xvcf1|xvcf2)\.xboxlive\.com\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " assets1.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " assets2.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " dlassets.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " dlassets2.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " d1.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " d2.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " xvcf1.xboxlive.com # " + Form1.appName);
                        sb.AppendLine(ip + " xvcf2.xboxlive.com # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString();
                        ThreadPool.QueueUserWorkItem(delegate { RestartService("DoSvc"); });
                        break;
                    case "assets1.xboxlive.cn":
                    case "assets2.xboxlive.cn":
                    case "dlassets.xboxlive.cn":
                    case "dlassets2.xboxlive.cn":
                    case "d1.xboxlive.cn":
                    case "d2.xboxlive.cn":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+(assets1|assets2|dlassets|dlassets2|d1|d2|xvcf1|xvcf2)\.xboxlive\.cn\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " assets1.xboxlive.cn # " + Form1.appName);
                        sb.AppendLine(ip + " assets2.xboxlive.cn # " + Form1.appName);
                        sb.AppendLine(ip + " dlassets.xboxlive.cn # " + Form1.appName);
                        sb.AppendLine(ip + " dlassets2.xboxlive.cn # " + Form1.appName);
                        sb.AppendLine(ip + " d1.xboxlive.cn # " + Form1.appName);
                        sb.AppendLine(ip + " d2.xboxlive.cn # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString();
                        ThreadPool.QueueUserWorkItem(delegate { RestartService("DoSvc"); });
                        break;
                    case "dl.delivery.mp.microsoft.com":
                    case "tlu.dl.delivery.mp.microsoft.com":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+(tlu\.)?dl\.delivery\.mp\.microsoft\.com\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " dl.delivery.mp.microsoft.com # " + Form1.appName);
                        sb.AppendLine(ip + " tlu.dl.delivery.mp.microsoft.com # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString();
                        ThreadPool.QueueUserWorkItem(delegate { RestartService("DoSvc"); });
                        break;
                    case "gst.prod.dl.playstation.net":
                    case "gs2.ww.prod.dl.playstation.net":
                    case "zeus.dl.playstation.net":
                    case "ares.dl.playstation.net":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+(gst\.prod|gs2\.ww\.prod|zeus|ares)\.dl\.playstation\.net\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " gst.prod.dl.playstation.net # " + Form1.appName);
                        sb.AppendLine(ip + " gs2.ww.prod.dl.playstation.net # " + Form1.appName);
                        sb.AppendLine(ip + " zeus.dl.playstation.net # " + Form1.appName);
                        sb.AppendLine(ip + " ares.dl.playstation.net # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString();
                        break;
                    case "origin-a.akamaihd.net":
                    case "blzddist1-a.akamaihd.net":
                    case "atum.hac.lp1.d4c.nintendo.net":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+((origin-a|blzddist1-a)\.akamaihd\.net|ssl-lvlt\.cdn\.ea\.com|atum\.hac\.lp1\.d4c\.nintendo\.net)\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine("0.0.0.0 ssl-lvlt.cdn.ea.com # " + Form1.appName);
                        sb.AppendLine(ip + " origin-a.akamaihd.net # " + Form1.appName);
                        sb.AppendLine(ip + " blzddist1-a.akamaihd.net # " + Form1.appName);
                        sb.AppendLine(ip + " atum.hac.lp1.d4c.nintendo.net # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString() + "\nOrigin 的用户可以在“工具 -> EA Origin 切换CDN服务器”中指定使用 Akamai。\n\n暴雪战网只能用监听方式加速。";
                        break;
                    case "epicgames-download1-1251447533.file.myqcloud.com":
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+" + host + @"\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " " + host + " # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString() + "\n\n需要重启 Epic Games Launcher 才能生效。";
                        break;
                    default:
                        sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+" + host + @"\s+# " + Form1.appName + "\r\n", "");
                        sb.AppendLine(ip + " " + host + " # " + Form1.appName);
                        msg = "系统Hosts文件写入成功，以下规则已写入系统Hosts文件\n\n" + sb.ToString();
                        break;
                }
                using (StreamWriter sw = new StreamWriter(sHostsPath, false))
                {
                    sw.Write(sHosts.Trim() + "\r\n" + sb.ToString());
                }
                fSecurity.RemoveAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                MessageBox.Show(msg, "写入系统Hosts文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("写入系统Hosts文件失败，错误信息：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void TsmExportRule_Click(object sender, EventArgs e)
        {
            if (dgvIpList.SelectedRows.Count != 1) return;
            DataGridViewRow dgvr = dgvIpList.SelectedRows[0];
            string host = dgvIpList.Tag.ToString();
            string ip = dgvr.Cells["Col_IP"].Value.ToString();
            StringBuilder sb = new StringBuilder();
            ToolStripMenuItem tsmi = sender as ToolStripMenuItem;
            switch (host)
            {
                case "assets1.xboxlive.com":
                case "assets2.xboxlive.com":
                case "dlassets.xboxlive.com":
                case "dlassets2.xboxlive.com":
                case "d1.xboxlive.com":
                case "d2.xboxlive.com":
                case "xvcf1.xboxlive.com":
                case "xvcf2.xboxlive.com":
                    if (tsmi.Name == "tsmDNSmasp")
                    {
                        sb.AppendLine("address=/assets1.xboxlive.com/" + ip);
                        sb.AppendLine("address=/assets2.xboxlive.com/" + ip);
                        sb.AppendLine("address=/dlassets.xboxlive.com/" + ip);
                        sb.AppendLine("address=/dlassets2.xboxlive.com/" + ip);
                        sb.AppendLine("address=/d1.xboxlive.com/" + ip);
                        sb.AppendLine("address=/d2.xboxlive.com/" + ip);
                        sb.AppendLine("address=/xvcf1.xboxlive.com/" + ip);
                        sb.AppendLine("address=/xvcf2.xboxlive.com/" + ip);
                    }
                    else
                    {
                        sb.AppendLine(ip + " assets1.xboxlive.com");
                        sb.AppendLine(ip + " assets2.xboxlive.com");
                        sb.AppendLine(ip + " dlassets.xboxlive.com");
                        sb.AppendLine(ip + " dlassets2.xboxlive.com");
                        sb.AppendLine(ip + " d1.xboxlive.com");
                        sb.AppendLine(ip + " d2.xboxlive.com");
                        sb.AppendLine(ip + " xvcf1.xboxlive.com");
                        sb.AppendLine(ip + " xvcf2.xboxlive.com");
                    }
                    break;
                case "assets1.xboxlive.cn":
                case "assets2.xboxlive.cn":
                case "dlassets.xboxlive.cn":
                case "dlassets2.xboxlive.cn":
                case "d1.xboxlive.cn":
                case "d2.xboxlive.cn":
                    if (tsmi.Name == "tsmDNSmasp")
                    {
                        sb.AppendLine("address=/assets1.xboxlive.cn/" + ip);
                        sb.AppendLine("address=/assets2.xboxlive.cn/" + ip);
                        sb.AppendLine("address=/dlassets.xboxlive.cn/" + ip);
                        sb.AppendLine("address=/dlassets2.xboxlive.cn/" + ip);
                        sb.AppendLine("address=/d1.xboxlive.cn/" + ip);
                        sb.AppendLine("address=/d2.xboxlive.cn/" + ip);
                    }
                    else
                    {
                        sb.AppendLine(ip + " assets1.xboxlive.cn");
                        sb.AppendLine(ip + " assets2.xboxlive.cn");
                        sb.AppendLine(ip + " dlassets.xboxlive.cn");
                        sb.AppendLine(ip + " dlassets2.xboxlive.cn");
                        sb.AppendLine(ip + " d1.xboxlive.cn");
                        sb.AppendLine(ip + " d2.xboxlive.cn");
                    }
                    break;
                case "dl.delivery.mp.microsoft.com":
                case "tlu.dl.delivery.mp.microsoft.com":
                    if (tsmi.Name == "tsmDNSmasp")
                    {
                        sb.AppendLine("address=/dl.delivery.mp.microsoft.com/" + ip);
                        sb.AppendLine("address=/tlu.dl.delivery.mp.microsoft.com/" + ip);
                    }
                    else
                    {
                        sb.AppendLine(ip + " dl.delivery.mp.microsoft.com");
                        sb.AppendLine(ip + " tlu.dl.delivery.mp.microsoft.com");
                    }
                    break;
                case "gst.prod.dl.playstation.net":
                case "gs2.ww.prod.dl.playstation.net":
                case "zeus.dl.playstation.net":
                case "ares.dl.playstation.net":
                    if (tsmi.Name == "tsmDNSmasp")
                    {
                        sb.AppendLine("address=/gst.prod.dl.playstation.net/" + ip);
                        sb.AppendLine("address=/gs2.ww.prod.dl.playstation.net/" + ip);
                        sb.AppendLine("address=/zeus.dl.playstation.net/" + ip);
                        sb.AppendLine("address=/ares.dl.playstation.net/" + ip);
                    }
                    else
                    {
                        sb.AppendLine(ip + " gst.prod.dl.playstation.net");
                        sb.AppendLine(ip + " gs2.ww.prod.dl.playstation.net");
                        sb.AppendLine(ip + " zeus.dl.playstation.net");
                        sb.AppendLine(ip + " ares.dl.playstation.net");
                    }
                    break;
                case "origin-a.akamaihd.net":
                case "blzddist1-a.akamaihd.net":
                    if (tsmi.Name == "tsmDNSmasp")
                    {
                        sb.AppendLine("address=/ssl-lvlt.cdn.ea.com/0.0.0.0");
                        sb.AppendLine("address=/origin-a.akamaihd.net/" + ip);
                        sb.AppendLine("address=/blzddist1-a.akamaihd.net/" + ip);
                        sb.AppendLine("address=/atum.hac.lp1.d4c.nintendo.net/" + ip);
                    }
                    else
                    {
                        sb.AppendLine("0.0.0.0 ssl-lvlt.cdn.ea.com");
                        sb.AppendLine(ip + " origin-a.akamaihd.net");
                        sb.AppendLine(ip + " blzddist1-a.akamaihd.net");
                        sb.AppendLine(ip + " atum.hac.lp1.d4c.nintendo.net");
                    }
                    break;
                default:
                    if (tsmi.Name == "tsmDNSmasp")
                        sb.AppendLine("address=/" + host + "/" + ip);
                    else
                        sb.AppendLine(ip + " " + host);
                    break;
            }
            Clipboard.SetDataObject(sb.ToString());
            MessageBox.Show("以下规则已复制到剪贴板\n\n" + sb.ToString(), "导出规则", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void TsmSpeedTest_Click(object sender, EventArgs e)
        {
            if (dgvIpList.SelectedRows.Count != 1) return;
            List<DataGridViewRow> ls = new List<DataGridViewRow>
            {
                dgvIpList.SelectedRows[0]
            };
            dgvIpList.ClearSelection();
            if (string.IsNullOrEmpty(tbDlUrl.Text) && flpTestUrl.Controls.Count >= 1)
            {
                LinkLabel link = flpTestUrl.Controls[0] as LinkLabel;
                tbDlUrl.Text = link.Tag.ToString();
            }
            string dlFile = tbDlUrl.Text.Trim();
            isSpeedTest = true;
            butSpeedTest.Enabled = false;
            Col_IP.SortMode = Col_ASN.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = Col_Speed.SortMode = DataGridViewColumnSortMode.NotSortable;
            ThreadPool.QueueUserWorkItem(delegate { SpeedTest(ls, dlFile); });
        }

        private void LinkFindIpArea_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (dgvIpList.Rows.Count == 0)
            {
                MessageBox.Show("请先导入IP。", "IP列表为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            FormFindIpArea dialog = new FormFindIpArea();
            dialog.ShowDialog();
            string key = dialog.key;
            dialog.Dispose();
            if (!string.IsNullOrEmpty(key))
            {
                key = key.Replace("\\", "\\\\")
                    .Replace("(", "\\(")
                    .Replace(")", "\\)")
                    .Replace("[", "\\[")
                    .Replace("]", "\\]")
                    .Replace("{", "\\{")
                    .Replace("}", "\\}")
                    .Replace(".", "\\.")
                    .Replace("+", "\\+")
                    .Replace("*", "\\*")
                    .Replace("?", "\\?")
                    .Replace("^", "\\^")
                    .Replace("$", "\\$")
                    .Replace("|", "\\|");
                key = ".*?" + Regex.Replace(key, @"\s+", ".*?") + ".*?";
                Regex reg = new Regex(@key);
                int rowIndex = 0;
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    string ASN = dgvr.Cells["Col_ASN"].Value.ToString();
                    if (reg.IsMatch(ASN))
                    {
                        dgvr.Cells["Col_Check"].Value = true;
                        dgvIpList.Rows.Remove(dgvr);
                        dgvIpList.Rows.Insert(rowIndex, dgvr);
                        rowIndex++;
                    }
                }
                if (rowIndex >= 1) dgvIpList.Rows[0].Cells[0].Selected = true;
            }
        }

        private void CkbASN_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = (CheckBox)sender;
            string network = cb.Text;
            bool isChecked = cb.Checked;
            foreach (DataGridViewRow dgvr in dgvIpList.Rows)
            {
                string ASN = dgvr.Cells["Col_ASN"].Value.ToString();
                switch (network)
                {
                    case "电信":
                        if (ASN.EndsWith(" 电信"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "联通":
                        if (ASN.EndsWith(" 联通"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "移动":
                        if (ASN.EndsWith(" 移动"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "香港":
                        if (ASN.StartsWith("中国香港") || ASN.StartsWith("香港"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "台湾":
                        if (ASN.StartsWith("中国台湾") || ASN.StartsWith("台湾"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "日本":
                        if (ASN.StartsWith("日本"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "韩国":
                        if (ASN.StartsWith("韩国"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    case "美国":
                        if (ASN.StartsWith("美国"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                    default:
                        if (!Regex.IsMatch(ASN, " (电信|联通|移动)$") && !Regex.IsMatch(ASN, "^(中国香港|香港|中国台湾|台湾|日本|韩国|美国)"))
                            dgvr.Cells["Col_Check"].Value = isChecked;
                        break;
                }
            }
        }

        private void CbImportIP_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbImportIP.SelectedIndex == 0) return;

            dgvIpList.Rows.Clear();
            flpTestUrl.Controls.Clear();
            tbDlUrl.Clear();
            cbImportIP.Enabled = false;

            string host = string.Empty;
            switch (cbImportIP.SelectedIndex)
            {
                case 1:
                    host = "assets1.xboxlive.cn";
                    break;
                case 2:
                    host = "assets1.xboxlive.com";
                    break;
                case 3:
                    host = "tlu.dl.delivery.mp.microsoft.com";
                    break;
                case 4:
                    host = "gst.prod.dl.playstation.net";
                    break;
                case 5:
                    host = "origin-a.akamaihd.net";
                    break;
                case 6:
                    host = "epicgames-download1-1251447533.file.myqcloud.com";
                    break;
            }
            dgvIpList.Tag = host;
            groupBox4.Text = "IP 列表 (" + host + ")";

            bool update = true;
            FileInfo fi = new FileInfo(Application.StartupPath + "\\IP." + host + ".txt");
            if (fi.Exists) update = DateTime.Compare(DateTime.Now, fi.LastWriteTime.AddHours(24)) >= 0;
            if (update)
            {
                UpdateFile.bDownloadEnd = false;
                ThreadPool.QueueUserWorkItem(delegate { UpdateFile.Download(fi.Name); });
                while (!UpdateFile.bDownloadEnd)
                {
                    Application.DoEvents();
                }
                fi.Refresh();
            }
            string content = string.Empty;
            if (fi.Exists)
            {
                using (StreamReader sr = fi.OpenText())
                {
                    content = sr.ReadToEnd();
                }
            }

            List<DataGridViewRow> list = new List<DataGridViewRow>();
            Match result = Regex.Match(content, @"(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})\s*\((?<ASN>[^\)]+)\)|(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})(?<ASN>.+)\dms|^\s*(?<IP>\d{0,3}\.\d{0,3}\.\d{0,3}\.\d{0,3})\s*$", RegexOptions.Multiline);
            if (result.Success)
            {
                while (result.Success)
                {
                    string ip = result.Groups["IP"].Value;
                    string ASN = result.Groups["ASN"].Value.Trim();

                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvIpList);
                    dgvr.Resizable = DataGridViewTriState.False;
                    if (ASN.EndsWith(" 电信"))
                        dgvr.Cells[0].Value = ckbChinaTelecom.Checked;
                    if (ASN.EndsWith(" 联通"))
                        dgvr.Cells[0].Value = ckbChinaUnicom.Checked;
                    if (ASN.EndsWith(" 移动"))
                        dgvr.Cells[0].Value = ckbChinaMobile.Checked;
                    if (ASN.StartsWith("中国香港") || ASN.StartsWith("香港"))
                        dgvr.Cells[0].Value = ckbHK.Checked;
                    if (ASN.StartsWith("中国台湾") || ASN.StartsWith("台湾"))
                        dgvr.Cells[0].Value = ckbTW.Checked;
                    if (ASN.StartsWith("日本"))
                        dgvr.Cells[0].Value = ckbJapan.Checked;
                    if (ASN.StartsWith("韩国"))
                        dgvr.Cells[0].Value = ckbKorea.Checked;
                    if (ASN.StartsWith("美国"))
                        dgvr.Cells[0].Value = ckbUS.Checked;
                    if (!Regex.IsMatch(ASN, " (电信|联通|移动)$") && !Regex.IsMatch(ASN, "^(中国香港|香港|中国台湾|台湾|日本|韩国|美国)"))
                        dgvr.Cells[0].Value = ckbOther.Checked;
                    dgvr.Cells[1].Value = ip;
                    dgvr.Cells[2].Value = ASN;
                    list.Add(dgvr);
                    result = result.NextMatch();
                }
                if (list.Count >= 1)
                {
                    dgvIpList.Rows.AddRange(list.ToArray());
                    dgvIpList.ClearSelection();
                    AddTestUrl(host);
                }
            }
            cbImportIP.Enabled = true;
        }

        private void LinkExportIP_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (dgvIpList.Rows.Count == 0) return;
            string host = dgvIpList.Tag.ToString();
            SaveFileDialog dlg = new SaveFileDialog
            {
                InitialDirectory = Application.StartupPath,
                Title = "导出数据",
                Filter = "文本文件(*.txt)|*.txt",
                FileName = "导出IP(" + host + ")"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine(host);
                sb.AppendLine("");
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (dgvr.Cells["Col_Speed"].Value != null && !string.IsNullOrEmpty(dgvr.Cells["Col_Speed"].Value.ToString()))
                        sb.AppendLine(dgvr.Cells["Col_IP"].Value + "\t(" + dgvr.Cells["Col_ASN"].Value + ")\t" + dgvr.Cells["Col_TTL"].Value + "|" + dgvr.Cells["Col_RoundtripTime"].Value + "|" + dgvr.Cells["Col_Speed"].Value);
                    else
                        sb.AppendLine(dgvr.Cells["Col_IP"].Value + "\t(" + dgvr.Cells["Col_ASN"].Value + ")");
                }
                using (FileStream fs = File.Create(dlg.FileName))
                {
                    Byte[] log = new UTF8Encoding(true).GetBytes(sb.ToString());
                    fs.Write(log, 0, log.Length);
                    fs.Close();
                }
            }
        }

        private void LinkImportIPManual_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormImportIP dialog = new FormImportIP();
            dialog.ShowDialog();
            string host = dialog.host;
            DataTable dt = dialog.dt;
            dialog.Dispose();
            if (dt != null && dt.Rows.Count >= 1)
            {
                cbImportIP.SelectedIndex = 0;
                dgvIpList.Rows.Clear();
                flpTestUrl.Controls.Clear();
                tbDlUrl.Clear();
                dgvIpList.Tag = host;
                List<DataGridViewRow> list = new List<DataGridViewRow>();
                groupBox4.Text = "IP 列表 (" + dgvIpList.Tag + ")";
                foreach (DataRow dr in dt.Select("", "ASN, IpLong"))
                {
                    string ASN = dr["ASN"].ToString();
                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvIpList);
                    dgvr.Resizable = DataGridViewTriState.False;
                    if (ASN.EndsWith(" 电信"))
                        dgvr.Cells[0].Value = ckbChinaTelecom.Checked;
                    if (ASN.EndsWith(" 联通"))
                        dgvr.Cells[0].Value = ckbChinaUnicom.Checked;
                    if (ASN.EndsWith(" 移动"))
                        dgvr.Cells[0].Value = ckbChinaMobile.Checked;
                    if (ASN.StartsWith("中国香港") || ASN.StartsWith("香港"))
                        dgvr.Cells[0].Value = ckbHK.Checked;
                    if (ASN.StartsWith("中国台湾") || ASN.StartsWith("台湾"))
                        dgvr.Cells[0].Value = ckbTW.Checked;
                    if (ASN.StartsWith("日本"))
                        dgvr.Cells[0].Value = ckbJapan.Checked;
                    if (ASN.StartsWith("韩国"))
                        dgvr.Cells[0].Value = ckbKorea.Checked;
                    if (ASN.StartsWith("美国"))
                        dgvr.Cells[0].Value = ckbUS.Checked;
                    if (!Regex.IsMatch(ASN, " (电信|联通|移动)$") && !Regex.IsMatch(ASN, "^(中国香港|香港|中国台湾|台湾|日本|韩国|美国)"))
                        dgvr.Cells[0].Value = ckbOther.Checked;
                    dgvr.Cells[1].Value = dr["IP"];
                    dgvr.Cells[2].Value = dr["ASN"];
                    list.Add(dgvr);
                }
                if (list.Count >= 1)
                {
                    dgvIpList.Rows.AddRange(list.ToArray());
                    dgvIpList.ClearSelection();
                    AddTestUrl(host);
                }
            }
        }

        private void AddTestUrl(string host)
        {
            switch (host)
            {
                case "assets1.xboxlive.com":
                case "assets2.xboxlive.com":
                case "dlassets.xboxlive.com":
                case "dlassets2.xboxlive.com":
                case "d1.xboxlive.com":
                case "d2.xboxlive.com":
                case "xvcf1.xboxlive.com":
                case "xvcf2.xboxlive.com":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.com/6/3cd1df82-6c9b-4ee2-923c-c62efb01cfaa/0698b936-d300-4451-b9a0-0be0514bbbe5/1.3472.45456.0.78ccfdb5-8bda-4d9d-92cc-20dbbfc2771f/Microsoft.254428597CFE2_1.3472.45456.0_neutral__8wekyb3d8bbwe_xs.xvc",
                            Text = "光环:无限",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb2 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.com/7/502842e2-4df9-4fb6-92f0-1103b466f9b5/7401a627-f4a2-461f-af22-7ee7b7e26b9a/3.455.709.0.c5a5568c-ff15-4526-a8c4-baf88c1e1924/Microsoft.624F8B84B80_3.455.709.0_neutral__8wekyb3d8bbwe_xs.xvc",
                            Text = "极限竞速:地平线5",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb2.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb3 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.com/Z/b7f5457e-f45c-425d-83b5-ecd508afe699/65307831-308b-4f1b-bb57-8b10e748da82/1.1.945.0.e1aa6466-85c5-440c-bb9e-e211d7757f37/Microsoft.HalifaxBaseGame_1.1.945.0_x64__8wekyb3d8bbwe",
                            Text = "战争机器5",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb3.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
                case "assets1.xboxlive.cn":
                case "assets2.xboxlive.cn":
                case "dlassets.xboxlive.cn":
                case "dlassets2.xboxlive.cn":
                case "d1.xboxlive.cn":
                case "d2.xboxlive.cn":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.cn/6/3cd1df82-6c9b-4ee2-923c-c62efb01cfaa/0698b936-d300-4451-b9a0-0be0514bbbe5/1.3472.45456.0.78ccfdb5-8bda-4d9d-92cc-20dbbfc2771f/Microsoft.254428597CFE2_1.3472.45456.0_neutral__8wekyb3d8bbwe_xs.xvc",
                            Text = "光环:无限",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb2 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.cn/7/502842e2-4df9-4fb6-92f0-1103b466f9b5/7401a627-f4a2-461f-af22-7ee7b7e26b9a/3.455.709.0.c5a5568c-ff15-4526-a8c4-baf88c1e1924/Microsoft.624F8B84B80_3.455.709.0_neutral__8wekyb3d8bbwe_xs.xvc",
                            Text = "极限竞速:地平线5",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb2.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb3 = new LinkLabel()
                        {
                            Tag = "http://assets1.xboxlive.cn/Z/b7f5457e-f45c-425d-83b5-ecd508afe699/65307831-308b-4f1b-bb57-8b10e748da82/1.1.945.0.e1aa6466-85c5-440c-bb9e-e211d7757f37/Microsoft.HalifaxBaseGame_1.1.945.0_x64__8wekyb3d8bbwe",
                            Text = "战争机器5",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb3.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
                case "dl.delivery.mp.microsoft.com":
                case "tlu.dl.delivery.mp.microsoft.com":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "9MV0B5HZVK9Z",
                            Text = "Xbox",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb2 = new LinkLabel()
                        {
                            Tag = "9WZDNCRFJ3TJ",
                            Text = "NETFLIX",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb2.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb3 = new LinkLabel()
                        {
                            Tag = "9NXQXXLFST89",
                            Text = "Disney+",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb3.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
                case "gst.prod.dl.playstation.net":
                case "gs2.ww.prod.dl.playstation.net":
                case "zeus.dl.playstation.net":
                case "ares.dl.playstation.net":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "http://gst.prod.dl.playstation.net/gst/prod/00/PPSA01559_00/app/pkg/3/f_74b53478b371caae3fa56806be11f158fdbdc12d5dbf943fd070bb9d1f7536e8/HP0102-PPSA01559_00-VILLAGEFULLGAMEX_0.pkg",
                            Text = "生化危机8(PS5)",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb2 = new LinkLabel()
                        {
                            Tag = "http://gs2.ww.prod.dl.playstation.net/gs2/appkgo/prod/CUSA18045_00/4/f_9671561044a3d7c67e7258ff87e2da8e486cc36cb73ebbef61faa91e6fc56bcd/f/HP0102-CUSA18045_00-VILLAGEFULLGAMEX_0.pkg",
                            Text = "生化危机8(PS4)",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb2.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb3 = new LinkLabel()
                        {
                            Tag = "http://zeus.dl.playstation.net/cdn/UP1004/NPUB31154_00/eISFknCNDxqSsVVywSenkJdhzOIfZjrqKHcuGBHEGvUxQJksdPvRNYbIyWcxFsvH.pkg",
                            Text = "侠盗猎车手5(PS3)",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb3.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
                case "origin-a.akamaihd.net":
                case "blzddist1-a.akamaihd.net":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "https://origin-a.akamaihd.net/Origin-Client-Download/origin/live/OriginThinSetup.exe",
                            Text = "Origin",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                        LinkLabel lb2 = new LinkLabel()
                        {
                            Tag = "http://blzddist1-a.akamaihd.net/tpr/odin/data/c9/7b/c97b6e3ca2079a2b8e9dea3efdd1ea90",
                            Text = "Call of Duty: Warzone(暴雪战网)",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb2.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
                case "epicgames-download1-1251447533.file.myqcloud.com":
                    {
                        LinkLabel lb1 = new LinkLabel()
                        {
                            Tag = "https://epicgames-download1-1251447533.file.myqcloud.com/Builds/UnrealEngineLauncher/Installers/Win32/EpicInstaller-13.0.0.msi?launcherfilename=EpicInstaller-13.0.0.msi",
                            Text = "Epic Games Launcher",
                            AutoSize = true,
                            Parent = this.flpTestUrl
                        };
                        lb1.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkTestUrl_LinkClicked);
                    }
                    break;
            }
        }

        readonly ConcurrentDictionary<String, MsAppUrl> dicMsAppUrl = new ConcurrentDictionary<String, MsAppUrl>();
        class MsAppUrl
        {
            public string url;
            public DateTime time;
        }
        Thread threadMsAppUrl = null;

        private void LinkTestUrl_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel link = sender as LinkLabel;
            string url = link.Tag.ToString();
            if (Regex.IsMatch(url, @"^https?://"))
            {
                tbDlUrl.Text = url;
            }
            else if (Regex.IsMatch(url, @"[0-9A-Z]{12}"))
            {
                if (dicMsAppUrl.ContainsKey(url))
                {
                    if (DateTime.Compare(dicMsAppUrl[url].time.AddMinutes(15), DateTime.Now) >= 0)
                        tbDlUrl.Text = dicMsAppUrl[url].url;
                    else
                        dicMsAppUrl.TryRemove(url, out _);
                }
                if (!dicMsAppUrl.ContainsKey(url))
                {
                    if (threadMsAppUrl != null && threadMsAppUrl.IsAlive) threadMsAppUrl.Abort();
                    threadMsAppUrl = new Thread(() => GetMsAppUrl(url)) { IsBackground = true };
                    threadMsAppUrl.Start();
                }
            }
        }

        private void GetMsAppUrl(string productId)
        {
            this.Invoke(new Action(() =>
            {
                tbDlUrl.Text = "正在获取下载链接，请稍候...";
            }));
            double tmpSize = 0;
            MsAppUrl appurl = null;
            SocketPackage socketPackage = ClassWeb.HttpRequest("https://store.rg-adguard.net/api/GetFiles", "POST", "type=ProductId&url=" + productId + "&ring=RP&lang=zh-CN", null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
            Match result = Regex.Match(socketPackage.Html, @"<tr [^>]+><td><a href=""(?<url>https?:\/\/tlu\.dl\.delivery\.mp\.microsoft\.com\/filestreamingservice\/files\/[^""]+)"" [^>]+>[^<]+</a></td><td [^>]+>[^<]+</td><td [^>]+>[^<]+</td><td [^>]+>(?<size>[^\s]+) (?<unit>MB|GB)</td></tr>");
            while (result.Success)
            {
                if (double.TryParse(result.Groups["size"].Value, out double size))
                {
                    if (result.Groups["unit"].Value == "GB") size *= 1024;
                    if (size > tmpSize)
                    {
                        tmpSize = size;
                        appurl = new MsAppUrl
                        {
                            url = result.Groups["url"].Value,
                            time = DateTime.Now
                        };
                    }
                }
                result = result.NextMatch();
            }
            if (appurl != null)
                dicMsAppUrl.AddOrUpdate(productId, appurl, (oldkey, oldvalue) => appurl);
            this.Invoke(new Action(() =>
            {
                if (dicMsAppUrl.ContainsKey(productId))
                    tbDlUrl.Text = dicMsAppUrl[productId].url;
                else
                    tbDlUrl.Clear();
            }));
        }

        private void LinkHostsClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MessageBox.Show("是否确认清除系统Hosts文件？", "清除系统Hosts文件", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) != DialogResult.Yes)
                return;
            string sHostsPath = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
            try
            {
                FileInfo fi = new FileInfo(sHostsPath);
                if (!fi.Exists)
                {
                    StreamWriter sw = fi.CreateText();
                    sw.Close();
                    fi.Refresh();
                }
                if ((fi.Attributes & FileAttributes.ReadOnly) != 0)
                    fi.Attributes = FileAttributes.Normal;
                FileSecurity fSecurity = fi.GetAccessControl();
                fSecurity.AddAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                string sHosts = string.Empty;
                using (StreamReader sw = new StreamReader(sHostsPath))
                {
                    sHosts = sw.ReadToEnd();
                }
                sHosts = Regex.Replace(sHosts, @"\d{1,3}\.\d{1,3}\.\d{1,3}\.\d{1,3}\s+.+\s+# " + Form1.appName + "\r\n", "");
                using (StreamWriter sw = new StreamWriter(sHostsPath, false))
                {
                    sw.Write(sHosts.Trim() + "\r\n");
                }
                fSecurity.RemoveAccessRule(new FileSystemAccessRule("Administrators", FileSystemRights.FullControl, AccessControlType.Allow));
                fi.SetAccessControl(fSecurity);
                MessageBox.Show("系统Hosts文件清除成功", "清除系统Hosts文件", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("清除系统Hosts文件失败，错误信息：" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LinkHostsEdit_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string sHostsPath = Environment.SystemDirectory + "\\drivers\\etc\\hosts";
            if (File.Exists(sHostsPath))
                Process.Start("notepad.exe", sHostsPath);
            else
                MessageBox.Show("Hosts 文件不存在", "Error", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
        }

        bool isSpeedTest = false;
        Thread threadSpeedTest = null;
        private void ButSpeedTest_Click(object sender, EventArgs e)
        {
            if (!isSpeedTest)
            {
                if (dgvIpList.Rows.Count == 0)
                {
                    MessageBox.Show("请先导入IP。", "IP列表为空", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                List<DataGridViewRow> ls = new List<DataGridViewRow>();
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (Convert.ToBoolean(dgvr.Cells["Col_Check"].Value))
                    {
                        ls.Add(dgvr);
                    }
                }
                if (ls.Count == 0)
                {
                    MessageBox.Show("请勾选需要测试IP。", "选择测试IP", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }
                if (threadMsAppUrl != null && threadMsAppUrl.IsAlive)
                {
                    threadMsAppUrl.Abort();
                    tbDlUrl.Clear();
                }
                if (string.IsNullOrEmpty(tbDlUrl.Text) && flpTestUrl.Controls.Count >= 1)
                {
                    LinkLabel link = flpTestUrl.Controls[0] as LinkLabel;
                    tbDlUrl.Text = link.Tag.ToString();
                }
                string dlFile = tbDlUrl.Text.Trim();

                int rowIndex = 0;
                foreach (DataGridViewRow dgvr in ls.ToArray())
                {
                    dgvIpList.Rows.Remove(dgvr);
                    dgvIpList.Rows.Insert(rowIndex, dgvr);
                    rowIndex++;
                }
                dgvIpList.Rows[0].Cells[0].Selected = true;

                isSpeedTest = true;
                butSpeedTest.Text = "停止测速";
                ckbChinaTelecom.Enabled = ckbChinaUnicom.Enabled = ckbChinaMobile.Enabled = ckbHK.Enabled = ckbTW.Enabled = ckbJapan.Enabled = ckbKorea.Enabled = ckbUS.Enabled = ckbOther.Enabled = linkFindIpArea.Enabled = linkExportIP.Enabled = cbImportIP.Enabled = linkImportIPManual.Enabled = flpTestUrl.Enabled = tbDlUrl.Enabled = false;
                Col_IP.SortMode = Col_ASN.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = Col_Speed.SortMode = DataGridViewColumnSortMode.NotSortable;
                Col_Check.ReadOnly = true;
                threadSpeedTest = new Thread(new ThreadStart(() =>
                {
                    SpeedTest(ls, dlFile);
                }))
                {
                    IsBackground = true
                };
                threadSpeedTest.Start();
            }
            else
            {
                if (threadSpeedTest != null && threadSpeedTest.IsAlive) threadSpeedTest.Abort();
                foreach (DataGridViewRow dgvr in dgvIpList.Rows)
                {
                    if (dgvr.Cells["Col_Speed"].Value != null && dgvr.Cells["Col_Speed"].Value.ToString() == "正在测试")
                    {
                        dgvr.Cells["Col_Speed"].Value = null;
                        break;
                    }
                }
                butSpeedTest.Text = "开始测速";
                ckbChinaTelecom.Enabled = ckbChinaUnicom.Enabled = ckbChinaMobile.Enabled = ckbHK.Enabled = ckbTW.Enabled = ckbJapan.Enabled = ckbKorea.Enabled = ckbUS.Enabled = ckbOther.Enabled = linkFindIpArea.Enabled = linkExportIP.Enabled = cbImportIP.Enabled = linkImportIPManual.Enabled = flpTestUrl.Enabled = tbDlUrl.Enabled = true;
                Col_IP.SortMode = Col_ASN.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = Col_Speed.SortMode = DataGridViewColumnSortMode.Automatic;
                Col_Check.ReadOnly = false;
                isSpeedTest = false;
                GC.Collect();
            }
        }

        private void SpeedTest(List<DataGridViewRow> ls, string dlFile)
        {
            string[] headers = new string[] { "Range: bytes=0-104857599" }; //100M
            //string[] headers = new string[] { "Range: bytes=0-1048575" }; //1M

            Stopwatch sw = new Stopwatch();
            foreach (DataGridViewRow dgvr in ls)
            {
                if (Regex.IsMatch(dlFile, @"^[0-9A-Z]{12}$"))
                {
                    GetMsAppUrl(dlFile);
                    dlFile = tbDlUrl.Text;
                }

                string ip = dgvr.Cells["Col_IP"].Value.ToString();
                dgvr.Cells["Col_TTL"].Value = null;
                dgvr.Cells["Col_RoundtripTime"].Value = null;
                dgvr.Cells["Col_Speed"].Value = "正在测试";
                dgvr.Cells["Col_Speed"].Style.ForeColor = Color.Empty;
                dgvr.Tag = null;

                using (Ping p1 = new Ping())
                {
                    try
                    {
                        PingReply reply = p1.Send(ip);
                        if (reply.Status == IPStatus.Success)
                        {
                            dgvr.Cells["Col_TTL"].Value = reply.Options.Ttl;
                            dgvr.Cells["Col_RoundtripTime"].Value = reply.RoundtripTime;
                        }
                    }
                    catch { }
                }
                if (!string.IsNullOrEmpty(dlFile))
                {
                    sw.Restart();
                    SocketPackage socketPackage = ClassWeb.HttpRequest(dlFile, "GET", null, null, true, false, false, null, null, headers, null, null, null, null, null, 0, null, 15000, 15000, 1, ip, true);
                    sw.Stop();
                    if (dgvr.Index >= 0)
                    {
                        dgvr.Tag = string.IsNullOrEmpty(socketPackage.Err) ? socketPackage.Headers : socketPackage.Err;
                        if (socketPackage.Headers.StartsWith("HTTP/1.1 206"))
                        {
                            dgvr.Cells["Col_Speed"].Value = Math.Round((double)(socketPackage.Buffer.Length) / sw.ElapsedMilliseconds * 1000 / 1024 / 1024, 2, MidpointRounding.AwayFromZero);
                        }
                        else
                        {
                            dgvr.Cells["Col_Speed"].Value = (double)0;
                            dgvr.Cells["Col_Speed"].Style.ForeColor = Color.Red;
                        }
                    }
                }
                else dgvr.Cells["Col_Speed"].Value = null;
            }
            this.Invoke(new Action(() =>
            {
                butSpeedTest.Text = "开始测速";
                ckbChinaTelecom.Enabled = ckbChinaUnicom.Enabled = ckbChinaMobile.Enabled = ckbHK.Enabled = ckbTW.Enabled = ckbJapan.Enabled = ckbKorea.Enabled = ckbUS.Enabled = ckbOther.Enabled = linkFindIpArea.Enabled = linkExportIP.Enabled = cbImportIP.Enabled = linkImportIPManual.Enabled = flpTestUrl.Enabled = tbDlUrl.Enabled = true;
                Col_IP.SortMode = Col_ASN.SortMode = Col_Speed.SortMode = Col_TTL.SortMode = Col_RoundtripTime.SortMode = DataGridViewColumnSortMode.Automatic;
                Col_Check.ReadOnly = false;
                butSpeedTest.Enabled = true;
            }));
            isSpeedTest = false;
            GC.Collect();
        }
        #endregion

        #region 选项卡-域名
        private void DgvHosts_DefaultValuesNeeded(object sender, DataGridViewRowEventArgs e)
        {
            e.Row.Cells["Col_Enable"].Value = true;
        }

        private void DgvHosts_CellValidating(object sender, DataGridViewCellValidatingEventArgs e)
        {
            switch (dgvHosts.Columns[e.ColumnIndex].Name)
            {
                case "Col_IPv4":
                    if (e.FormattedValue.ToString().Trim() != "")
                    {
                        if (!IPAddress.TryParse(e.FormattedValue.ToString().Trim(), out _))
                        {
                            e.Cancel = true;
                            dgvHosts.Rows[e.RowIndex].ErrorText = "不是有效IPv4地址";
                        }
                    }
                    break;
            }
        }

        private void DgvHosts_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            switch (dgvHosts.Columns[e.ColumnIndex].Name)
            {
                case "Col_HostName":
                    dgvHosts.CurrentCell.Value = Regex.Replace(dgvHosts.CurrentCell.FormattedValue.ToString(), @"^(https?://)?([^/|:]+).*$", "$2");
                    break;
                case "Col_IPv4":
                    dgvHosts.CurrentCell.Value = dgvHosts.CurrentCell.FormattedValue.ToString().Trim();
                    break;
            }
        }

        private void CbDomainName_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDomainName.SelectedIndex <= 0) return;
            Match result = Regex.Match(cbDomainName.Text, @"(?<remark>[^\(]+)(\((?<hostname>.+)\))?");
            if (result.Success)
            {
                string remark = result.Groups["remark"].Value;
                if (remark == "Xbox360主机本地上传")
                {
                    string[] hostNames = new string[] { "download.xbox.com", "download.xbox.com.edgesuite.net", "xbox-ecn102.vo.msecnd.net" };
                    foreach (string hostName in hostNames)
                    {
                        DataRow[] rows = dtHost.Select("HostName='" + hostName + "'");
                        if (rows.Length >= 1)
                        {
                            rows[0]["Enable"] = true;
                            rows[0]["IPv4"] = Properties.Settings.Default.LocalIP;
                            rows[0]["Remark"] = "Xbox360主机下载域名";
                        }
                        else
                        {
                            DataRow dr = dtHost.NewRow();
                            dr["Enable"] = true;
                            dr["HostName"] = hostName;
                            dr["IPv4"] = Properties.Settings.Default.LocalIP;
                            dr["Remark"] = "Xbox360主机下载域名";
                            dtHost.Rows.Add(dr);
                        }
                    }
                }
                else
                {
                    string dnsServer;
                    switch (cbDohDNS.SelectedIndex)
                    {
                        case 1:
                            dnsServer = "doh.pub";
                            break;
                        case 2:
                            dnsServer = "doh.360.cn";
                            break;
                        case 3:
                            dnsServer = "8.8.8.8";
                            break;
                        default:
                            dnsServer = "dns.alidns.com";
                            break;
                    }
                    string hostname = result.Groups["hostname"].Value.ToLower();
                    DataRow[] rows = dtHost.Select("HostName='" + hostname + "'");
                    DataRow dr;
                    if (rows.Length >= 1)
                    {
                        dr = rows[0];
                    }
                    else
                    {
                        dr = dtHost.NewRow();
                        dr["Enable"] = true;
                        dr["HostName"] = hostname;
                        dtHost.Rows.Add(dr);
                    }
                    dr["IPv4"] = null;
                    Task.Run(() =>
                    {
                        dr["IPv4"] = ClassDNS.DoH(hostname, dnsServer);
                    });
                    dr["Remark"] = remark;
                    dgvHosts.ClearSelection();
                    foreach (DataGridViewRow dgvr in dgvHosts.Rows)
                    {
                        if (dgvr.IsNewRow) break;
                        if (dgvr.Cells["Col_HostName"].Value.ToString() == hostname)
                        {
                            dgvr.Cells["Col_IPv4"].Selected = true;
                            break;
                        }
                    }
                }
            }
        }

        private void ButHostSave_Click(object sender, EventArgs e)
        {
            dtHost.AcceptChanges();
            if (dtHost.Rows.Count >= 1)
                dtHost.WriteXml(hostPath);
            else if (File.Exists(hostPath))
                File.Delete(hostPath);
            AddHost();

            if (bServiceFlag) WriteHost(true);
        }

        private void ButHostReset_Click(object sender, EventArgs e)
        {
            dtHost.RejectChanges();
            dgvHosts.ClearSelection();
        }

        private void LinkHostClear_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            for (int i = dgvHosts.Rows.Count - 2; i >= 0; i--)
            {
                dgvHosts.Rows.RemoveAt(i);
            }
        }

        private void AddHost()
        {
            dicHost.Clear();
            foreach (DataRow dr in dtHost.Rows)
            {
                string hostName = dr["HostName"].ToString().Trim().ToLower();
                if (Convert.ToBoolean(dr["Enable"]) && !string.IsNullOrEmpty(hostName) && !dicHost.ContainsKey(hostName))
                {
                    if (IPAddress.TryParse(dr["IPv4"].ToString().Trim(), out IPAddress ip))
                    {
                        Byte[] ipByte = ip.GetAddressBytes();
                        dicHost.AddOrUpdate(hostName, ipByte, (oldkey, oldvalue) => ipByte);
                    }
                }
            }
            dgvHosts.ClearSelection();
        }
        #endregion

        #region 选项卡-硬盘
        private void ButScan_Click(object sender, EventArgs e)
        {
            dgvDevice.Rows.Clear();
            butEnabelPc.Enabled = butEnabelXbox.Enabled = false;
            List<DataGridViewRow> list = new List<DataGridViewRow>();

            ManagementClass mc = new ManagementClass("Win32_DiskDrive");
            ManagementObjectCollection moc = mc.GetInstances();
            foreach (ManagementObject mo in moc)
            {
                string sDeviceID = mo.Properties["DeviceID"].Value.ToString();
                string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
                if (string.Equals(mbr.Substring(0, 892), ClassMbr.MBR))
                {
                    string mode = mbr.Substring(1020);
                    DataGridViewRow dgvr = new DataGridViewRow();
                    dgvr.CreateCells(dgvDevice);
                    dgvr.Resizable = DataGridViewTriState.False;
                    dgvr.Tag = mode;
                    dgvr.Cells[0].Value = sDeviceID;
                    dgvr.Cells[1].Value = mo.Properties["Model"].Value;
                    dgvr.Cells[2].Value = mo.Properties["InterfaceType"].Value;
                    dgvr.Cells[3].Value = ClassMbr.ConvertBytes(Convert.ToUInt64(mo.Properties["Size"].Value));
                    if (mode == "99CC")
                        dgvr.Cells[4].Value = "Xbox 模式";
                    else if (mode == "55AA")
                        dgvr.Cells[4].Value = "PC 模式";
                    list.Add(dgvr);
                }
            }
            if (list.Count >= 1)
            {
                dgvDevice.Rows.AddRange(list.ToArray());
                dgvDevice.ClearSelection();
            }
        }

        private void DgvDevice_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex == -1) return;
            string mode = dgvDevice.Rows[e.RowIndex].Tag.ToString();
            if (mode == "99CC")
            {
                butEnabelPc.Enabled = true;
                butEnabelXbox.Enabled = false;
            }
            else if (mode == "55AA")
            {
                butEnabelPc.Enabled = false;
                butEnabelXbox.Enabled = true;
            }
        }

        private void ButEnabelPc_Click(object sender, EventArgs e)
        {
            if (dgvDevice.SelectedRows.Count != 1) return;
            if (Environment.OSVersion.Version.Major < 10)
            {
                MessageBox.Show("低于Win10操作系统转换后会蓝屏，请升级操作系统。", "操作系统版本过低", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            string sDeviceID = dgvDevice.SelectedRows[0].Cells["Col_DeviceID"].Value.ToString();
            string mode = dgvDevice.SelectedRows[0].Tag.ToString();
            string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
            if (mode == "99CC" && mbr.Substring(0, 892) == ClassMbr.MBR && mbr.Substring(1020) == mode)
            {
                string newMBR = mbr.Substring(0, 1020) + "55AA";
                if (ClassMbr.WriteMBR(sDeviceID, ClassMbr.HexToByte(newMBR)))
                {
                    dgvDevice.SelectedRows[0].Tag = "55AA";
                    dgvDevice.SelectedRows[0].Cells["Col_Mode"].Value = "PC 模式";
                    dgvDevice.ClearSelection();
                    butEnabelPc.Enabled = false;
                    using (Process p = new Process())
                    {
                        p.StartInfo.FileName = "diskpart.exe";
                        p.StartInfo.RedirectStandardInput = true;
                        p.StartInfo.CreateNoWindow = true;
                        p.StartInfo.UseShellExecute = false;
                        p.Start();
                        p.StandardInput.WriteLine("rescan");
                        p.StandardInput.WriteLine("exit");
                        p.Close();
                    }
                    MessageBox.Show("成功转换PC模式。", "转换PC模式", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
        }

        private void ButEnabelXbox_Click(object sender, EventArgs e)
        {
            if (dgvDevice.SelectedRows.Count != 1) return;
            string sDeviceID = dgvDevice.SelectedRows[0].Cells["Col_DeviceID"].Value.ToString();
            string mode = dgvDevice.SelectedRows[0].Tag.ToString();
            string mbr = ClassMbr.ByteToHexString(ClassMbr.ReadMBR(sDeviceID));
            if (mode == "55AA" && mbr.Substring(0, 892) == ClassMbr.MBR && mbr.Substring(1020) == mode)
            {
                string newMBR = mbr.Substring(0, 1020) + "99CC";
                if (ClassMbr.WriteMBR(sDeviceID, ClassMbr.HexToByte(newMBR)))
                {
                    dgvDevice.SelectedRows[0].Tag = "99CC";
                    dgvDevice.SelectedRows[0].Cells["Col_Mode"].Value = "Xbox 模式";
                    dgvDevice.ClearSelection();
                    butEnabelXbox.Enabled = false;
                    MessageBox.Show("成功转换Xbox模式。", "转换Xbox模式", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
        }

        private void Tb_Enter(object sender, EventArgs e)
        {
            BeginInvoke((Action)delegate
            {
                (sender as TextBox).SelectAll();
            });
        }

        private void ButDownload_Click(object sender, EventArgs e)
        {
            string url = tbDownloadUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (!Regex.IsMatch(url, @"^https?\:\/\/"))
            {
                if (!url.StartsWith("/")) url = "/" + url;
                url = "http://assets1.xboxlive.cn" + url;
                tbDownloadUrl.Text = url;
            }

            tbFilePath.Text = string.Empty;
            byte[] bFileBuffer = null;
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, false, null, null, new string[] { "Range: bytes=0-4095" }, null, null, null, null, null, 0, null);
            if (!string.IsNullOrEmpty(socketPackage.Err))
            {
                MessageBox.Show("下载失败，错误信息：" + socketPackage.Err, "下载失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                bFileBuffer = socketPackage.Buffer;
            }
            Analysis(bFileBuffer);
        }

        private void ButOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Open an Xbox Package"
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string sFilePath = ofd.FileName;
            tbDownloadUrl.Text = "";
            tbFilePath.Text = sFilePath;

            FileStream fs = null;
            try
            {
                fs = new FileStream(sFilePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            if (fs != null)
            {
                int len = fs.Length >= 49152 ? 49152 : (int)fs.Length;
                byte[] bFileBuffer = new byte[len];
                fs.Read(bFileBuffer, 0, len);
                fs.Close();
                Analysis(bFileBuffer);
            }
        }

        private void Analysis(byte[] bFileBuffer)
        {
            tbContentId.Text = tbProductID.Text = tbBuildID.Text = tbFileTimeCreated.Text = tbDriveSize.Text = tbPackageVersion.Text = string.Empty;
            linkCopyContentID.Enabled = linkRename.Enabled = false;
            if (bFileBuffer != null && bFileBuffer.Length >= 4096)
            {
                using (MemoryStream ms = new MemoryStream(bFileBuffer))
                {
                    BinaryReader br = null;
                    try
                    {
                        br = new BinaryReader(ms);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    if (br != null)
                    {
                        br.BaseStream.Position = 0x200;
                        if (Encoding.Default.GetString(br.ReadBytes(0x8)) == "msft-xvd")
                        {
                            br.BaseStream.Position = 0x210;
                            tbFileTimeCreated.Text = DateTime.FromFileTime(BitConverter.ToInt64(br.ReadBytes(0x8), 0)).ToString();

                            br.BaseStream.Position = 0x218;
                            tbDriveSize.Text = ClassMbr.ConvertBytes(BitConverter.ToUInt64(br.ReadBytes(0x8), 0)).ToString();

                            br.BaseStream.Position = 0x220;
                            tbContentId.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x39C;
                            tbProductID.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x3AC;
                            tbBuildID.Text = (new Guid(br.ReadBytes(0x10))).ToString();

                            br.BaseStream.Position = 0x3BC;
                            ushort PackageVersion1 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3BE;
                            ushort PackageVersion2 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3C0;
                            ushort PackageVersion3 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            br.BaseStream.Position = 0x3C2;
                            ushort PackageVersion4 = BitConverter.ToUInt16(br.ReadBytes(0x2), 0);
                            tbPackageVersion.Text = PackageVersion4 + "." + PackageVersion3 + "." + PackageVersion2 + "." + PackageVersion1;
                            linkCopyContentID.Enabled = true;
                            if (!string.IsNullOrEmpty(tbFilePath.Text))
                            {
                                string filename = Path.GetFileName(tbFilePath.Text).ToLowerInvariant();
                                if (filename != tbContentId.Text.ToLowerInvariant() && !filename.EndsWith(".msixvc")) linkRename.Enabled = true;
                            }
                        }
                        else
                        {
                            MessageBox.Show("不是有效文件", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        br.Close();
                    }
                }
            }
            else
            {
                MessageBox.Show("不是有效文件", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LinkCopyContentID_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string sContentID = tbContentId.Text;
            if (!string.IsNullOrEmpty(sContentID))
            {
                Clipboard.SetDataObject(sContentID.ToUpper());
            }
        }

        private void LinkRename_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MessageBox.Show(string.Format("是否确认重命名本地文件？\n\n修改前文件名：{0}\n修改后文件名：{1}", Path.GetFileName(tbFilePath.Text), tbContentId.Text.ToUpperInvariant()), "重命名本地文件", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                FileInfo fi = new FileInfo(tbFilePath.Text);
                try
                {
                    fi.MoveTo(Path.GetDirectoryName(tbFilePath.Text) + "\\" + tbContentId.Text.ToUpperInvariant());
                }
                catch (Exception ex)
                {
                    MessageBox.Show("重命名本地文件失败，错误信息：" + ex.Message, "重命名本地文件", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                linkRename.Enabled = false;
            }
        }
        #endregion

        #region 选项卡-游戏
        private void ButGame_Click(object sender, EventArgs e)
        {
            string url = tbGameUrl.Text.Trim();
            if (string.IsNullOrEmpty(url)) return;
            if (Regex.IsMatch(url, "^https?://marketplace.xbox.com/"))
            {
                pbGame.Image = pbGame.InitialImage;
                tbGameTitle.Clear();
                tbGameDeveloperName.Clear();
                tbGameCategory.Clear();
                tbGameOriginalReleaseDate.Clear();
                cbGameBundled.Items.Clear();
                tbGamePrice.Clear();
                tbGameDescription.Clear();
                tbGameLanguages.Clear();
                lvGame.Items.Clear();
                butGame.Enabled = false;
                linkCompare.Enabled = false;
                linkGameWebsite.Enabled = false;
                this.cbGameBundled.SelectedIndexChanged -= new EventHandler(this.CbGameBundled_SelectedIndexChanged);
                Market market = (Market)cbGameMarket.SelectedItem;
                string region = market.lang;
                url = Regex.Replace(url, @"(/[a-zA-Z]{2}-[a-zA-Z]{2})?/Product/", "/" + market.lang + "/Product/");
                linkGameWebsite.Links[0].LinkData = url;
                tbGameUrl.Text = url;
                ThreadPool.QueueUserWorkItem(delegate { Xbox360Marketplace(url, region); });
            }
            else
            {
                Match result = Regex.Match(url, @"/(?<productId>[a-zA-Z0-9]{12})/?$|/(?<productId>[a-zA-Z0-9]{12})(\?|#)|/(?<productId>[a-zA-Z0-9]{12})/0001|^(?<productId>[a-zA-Z0-9]{12})$");
                if (result.Success)
                {
                    pbGame.Image = pbGame.InitialImage;
                    tbGameTitle.Clear();
                    tbGameDeveloperName.Clear();
                    tbGameCategory.Clear();
                    tbGameOriginalReleaseDate.Clear();
                    cbGameBundled.Items.Clear();
                    tbGamePrice.Clear();
                    tbGameDescription.Clear();
                    tbGameLanguages.Clear();
                    lvGame.Items.Clear();
                    butGame.Enabled = false;
                    linkCompare.Enabled = false;
                    linkGameWebsite.Enabled = false;
                    this.cbGameBundled.SelectedIndexChanged -= new EventHandler(this.CbGameBundled_SelectedIndexChanged);
                    Market market = (Market)cbGameMarket.SelectedItem;
                    string productId = result.Groups["productId"].Value.ToUpperInvariant();
                    url = "https://www.microsoft.com/" + market.lang + "/p/_/" + productId;
                    linkGameWebsite.Links[0].LinkData = url;
                    ThreadPool.QueueUserWorkItem(delegate { XboxStore(market, productId); });
                }
                else
                {
                    MessageBox.Show("无效 URL/ProductId", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        string query = string.Empty;
        private void TbGameSearch_TextChanged(object sender, EventArgs e)
        {
            string query = tbGameSearch.Text.Trim();
            if (string.IsNullOrEmpty(query))
            {
                lbGameSearch.Items.Clear();
                lbGameSearch.Visible = false;
                this.query = query;
                return;
            }
            if (this.query == query) return;
            this.query = query;
            ThreadPool.QueueUserWorkItem(delegate { GameSearch(query); });
        }

        private void TbGameSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (int)Keys.Down)
            {
                if (lbGameSearch.Items.Count >= 1)
                {
                    lbGameSearch.Focus();
                    lbGameSearch.SelectedIndex = lbGameSearch.SelectedIndex < lbGameSearch.Items.Count - 1 ? lbGameSearch.SelectedIndex + 1 : lbGameSearch.Items.Count - 1;
                }
            }
            else if (e.KeyValue == (int)Keys.Up)
            {
                if (lbGameSearch.Items.Count >= 1)
                {
                    lbGameSearch.Focus();
                    lbGameSearch.SelectedIndex = lbGameSearch.SelectedIndex > 1 ? lbGameSearch.SelectedIndex - 1 : 0;
                }
            }
        }

        private void TbGameSearch_Leave(object sender, EventArgs e)
        {
            if (lbGameSearch.Focused == false)
            {
                lbGameSearch.Visible = false;
            }
        }

        private void TbGameSearch_Enter(object sender, EventArgs e)
        {
            if (lbGameSearch.Items.Count >= 1)
            {
                lbGameSearch.Visible = true;
            }
        }

        private void LbGameSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyValue == (int)Keys.Enter)
            {
                Product product = (Product)lbGameSearch.SelectedItem;
                lbGameSearch.Visible = false;
                tbGameUrl.Text = "https://www.microsoft.com/store/productId/" + product.id;
                if (butGame.Enabled) ButGame_Click(null, null);
            }
        }

        private void LbGameSearch_DoubleClick(object sender, EventArgs e)
        {
            if (lbGameSearch.SelectedItem != null)
            {
                Product product = (Product)lbGameSearch.SelectedItem;
                lbGameSearch.Visible = false;
                tbGameUrl.Text = "https://www.microsoft.com/store/productId/" + product.id;
                if (butGame.Enabled) ButGame_Click(null, null);
            }
        }

        private void LbGameSearch_Leave(object sender, EventArgs e)
        {
            if (tbGameSearch.Focused == false)
            {
                lbGameSearch.Visible = false;
            }
        }

        private void GameSearch(string query)
        {
            Thread.Sleep(300);
            if (this.query != query) return;
            string language;
            switch (Thread.CurrentThread.CurrentCulture.Name)
            {
                case "zh-HK":
                case "zh-TW":
                    language = "zh-TW";
                    break;
                default:
                    language = "zh-TW";
                    break;
            }
            string url = "https://www.microsoft.com/services/api/v3/suggest?market=" + language + "&clientId=7F27B536-CF6B-4C65-8638-A0F8CBDFCA65&sources=Microsoft-Terms%2CIris-Products%2CDCatAll-Products&filter=ExcludeDCatProducts%3ADCatDevices-Products%2CDCatSoftware-Products%2CDCatBundles-Products%2BClientType%3AStoreWeb&counts=5%2C1%2C5&query=" + ClassWeb.UrlEncode(query);
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
            if (this.query != query) return;
            List<Product> lsProduct = new List<Product>();
            if (Regex.IsMatch(socketPackage.Html, @"^{.+}$", RegexOptions.Singleline))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var json = js.Deserialize<ClassGame.Search>(socketPackage.Html);
                if (json != null && json.ResultSets != null && json.ResultSets.Count >= 1)
                {
                    foreach (var resultSets in json.ResultSets)
                    {
                        foreach (var suggest in resultSets.Suggests)
                        {
                            if (suggest.Source != "Games") continue;
                            var BigCatalogId = Array.FindAll(suggest.Metas.ToArray(), a => a.Key == "BigCatalogId");
                            if (BigCatalogId.Length == 1)
                            {
                                lsProduct.Add(new Product(suggest.Title, BigCatalogId[0].Value));
                            }
                        }
                    }
                }
            }
            this.Invoke(new Action(() =>
            {
                lbGameSearch.Items.Clear();
                if (lsProduct.Count >= 1)
                {
                    int height = (int)(15 * Form1.dpixRatio);
                    lbGameSearch.Items.AddRange(lsProduct.ToArray());
                    lbGameSearch.Height = (lsProduct.Count <= 8 ? lsProduct.Count * height : 8 * height);
                    lbGameSearch.Visible = true;
                }
                else
                {
                    lbGameSearch.Visible = false;
                }
            }));
        }

        private void GameXGPRecentlyAdded(int sort)
        {
            ComboBox cb;
            string siglId1 = string.Empty, siglId2 = string.Empty, text1 = string.Empty, text2 = string.Empty;
            if (sort == 1)
            {
                cb = cbGameXGP1;
                siglId1 = "eab7757c-ff70-45af-bfa6-79d3cfb2bf81";
                siglId2 = "a884932a-f02b-40c8-a903-a008c23b1df1";
                text1 = "最受欢迎 Xbox Game Pass 主机游戏 ({0})";
                text2 = "最受欢迎 Xbox Game Pass 电脑游戏 ({0})";
            }
            else
            {
                cb = cbGameXGP2;
                siglId1 = "f13cf6b4-57e6-4459-89df-6aec18cf0538";
                siglId2 = "163cdff5-442e-4957-97f5-1050a3546511";
                text1 = "近期新增 Xbox Game Pass 主机游戏 ({0})";
                text2 = "近期新增 Xbox Game Pass 电脑游戏 ({0})";
            }
            List<Product> lsProduct1 = new List<Product>();
            List<Product> lsProduct2 = new List<Product>();
            Task[] tasks = new Task[2];
            tasks[0] = new Task(() =>
            {
                lsProduct1 = GetXGPGames(siglId1, text1);
            });
            tasks[1] = new Task(() =>
            {
                lsProduct2 = GetXGPGames(siglId2, text2);
            });
            Array.ForEach(tasks, x => x.Start());
            Task.WaitAll(tasks);
            List<Product> lsProduct = lsProduct1.Union(lsProduct2).ToList<Product>();
            if (lsProduct.Count >= 1)
            {
                this.Invoke(new Action(() =>
                {
                    cb.Items.Clear();
                    cb.Items.AddRange(lsProduct.ToArray());
                    cb.SelectedIndex = 0;
                }));
            }
        }

        private List<Product> GetXGPGames(string siglId, string text)
        {
            List<Product> lsProduct = new List<Product>();
            List<string> lsBundledId = new List<string>();
            string url = "https://catalog.gamepass.com/sigls/v2?id=" + siglId + "&language=zh-Hans&market=US";
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null, 60000, 60000);
            Match result = Regex.Match(socketPackage.Html, @"\{""id"":""(?<ProductId>[a-zA-Z0-9]{12})""\}");
            while (result.Success)
            {
                lsBundledId.Add(result.Groups["ProductId"].Value.ToLowerInvariant());
                result = result.NextMatch();
            }
            if (lsBundledId.Count >= 1)
            {
                url = "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=" + string.Join(",", lsBundledId.ToArray()) + "&market=US&languages=zh-Hans&MS-CV=DGU1mcuYo0WMMp+F.1";
                socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null, 60000, 60000);
                if (Regex.IsMatch(socketPackage.Html, @"^{.+}$", RegexOptions.Singleline))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var json = js.Deserialize<ClassGame.Game>(socketPackage.Html);
                    if (json != null && json.Products != null && json.Products.Count >= 1)
                    {
                        lsProduct.Add(new Product(string.Format(text, json.Products.Count), "0"));
                        foreach (var product in json.Products)
                        {
                            lsProduct.Add(new Product("  " + product.LocalizedProperties[0].ProductTitle, product.ProductId));
                        }
                    }
                }
            }
            if (lsProduct.Count == 0)
                lsProduct.Add(new Product(string.Format(text, "加载失败"), "0"));
            return lsProduct;
        }

        private void CbGame_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox cb = sender as ComboBox;
            if (cb.SelectedIndex <= 0) return;
            Product product = (Product)cb.SelectedItem;
            if (product.id == "0") return;
            tbGameUrl.Text = "https://www.microsoft.com/p/_/" + product.id;
            foreach (var item in cbGameMarket.Items)
            {
                Market market = (Market)item;
                if (market.lang == "zh-TW")
                {
                    cbGameMarket.SelectedItem = item;
                    break;
                }
            }
            if (butGame.Enabled) ButGame_Click(null, null);
        }

        private void LinkGameChinese_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            FormChinese dialog = new FormChinese();
            dialog.ShowDialog();
            dialog.Dispose();
            if (!string.IsNullOrEmpty(dialog.productid))
            {
                tbGameUrl.Text = "https://www.microsoft.com/p/_/" + dialog.productid;
                foreach (var item in cbGameMarket.Items)
                {
                    Market market = (Market)item;
                    if (market.lang == "zh-CN")
                    {
                        cbGameMarket.SelectedItem = item;
                        break;
                    }
                }
                if (butGame.Enabled) ButGame_Click(null, null);
            }
        }

        private void GameWithGold()
        {
            ConcurrentDictionary<String, string[]> dicGamesWithGold = new ConcurrentDictionary<String, string[]>();
            //https://www.xbox.com/en-US/live/gold/js/globalContent.js
            SocketPackage socketPackage = ClassWeb.HttpRequest("https://www.xbox.com/en-US/live/gold/js/gwg-globalContent.js", "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null, 60000, 60000);
            Match result = Regex.Match(Regex.Replace(socketPackage.Html, @"globalContentOld.+", "", RegexOptions.Singleline), @"""(?<langue>[^""]+)"": \{\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopytitlenowgame1"": ""(?<keyCopytitlenowgame1>[^""]+)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyLinknowgame1"": ""(?<keyLinknowgame1>[^""]*)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopydatesnowgame1"": ""(?<keyCopydatesnowgame1>[^""]*)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopytitlenowgame2"": ""(?<keyCopytitlenowgame2>[^""]+)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyLinknowgame2"": ""(?<keyLinknowgame2>[^""]*)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopydatesnowgame2"": ""(?<keyCopydatesnowgame2>[^""]*)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopytitlenowgame3"": ""(?<keyCopytitlenowgame3>[^""]+)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyLinknowgame3"": ""(?<keyLinknowgame3>[^""]*)"",\n(\s+""[^""]+"": ""[^""]*"",\n)+\s+""keyCopydatesnowgame3"": ""(?<keyCopydatesnowgame3>[^""]*)""");
            while (result.Success)
            {
                string lengue = result.Groups["langue"].Value.ToLowerInvariant();
                string keyCopytitlenowgame1 = result.Groups["keyCopytitlenowgame1"].Value;
                string keyCopytitlenowgame2 = result.Groups["keyCopytitlenowgame2"].Value;
                string keyCopytitlenowgame3 = result.Groups["keyCopytitlenowgame3"].Value;
                string keyLinknowgame1 = result.Groups["keyLinknowgame1"].Value;
                string keyLinknowgame2 = result.Groups["keyLinknowgame2"].Value;
                string keyLinknowgame3 = result.Groups["keyLinknowgame3"].Value;
                string keyCopydatesnowgame1 = result.Groups["keyCopydatesnowgame1"].Value;
                string keyCopydatesnowgame2 = result.Groups["keyCopydatesnowgame2"].Value;
                string keyCopydatesnowgame3 = result.Groups["keyCopydatesnowgame3"].Value;
                if (lengue == "zh-tw")
                {
                    if (!string.IsNullOrEmpty(keyLinknowgame1))
                    {
                        string[] detail1 = new string[] { lengue, keyCopytitlenowgame1, keyLinknowgame1, keyCopydatesnowgame1 };
                        dicGamesWithGold.AddOrUpdate(keyLinknowgame1, detail1, (oldkey, oldvalue) => detail1);
                    }
                    if (!string.IsNullOrEmpty(keyLinknowgame2))
                    {
                        string[] detail2 = new string[] { lengue, keyCopytitlenowgame2, keyLinknowgame2, keyCopydatesnowgame2 };
                        dicGamesWithGold.AddOrUpdate(keyLinknowgame2, detail2, (oldkey, oldvalue) => detail2);
                    }
                    if (!string.IsNullOrEmpty(keyLinknowgame3))
                    {
                        string[] detail3 = new string[] { lengue, keyCopytitlenowgame3, keyLinknowgame3, keyCopydatesnowgame3 };
                        dicGamesWithGold.AddOrUpdate(keyLinknowgame3, detail3, (oldkey, oldvalue) => detail3);
                    }
                }
                else
                {
                    if (!string.IsNullOrEmpty(keyLinknowgame1) && !dicGamesWithGold.ContainsKey(keyLinknowgame1))
                    {
                        string[] detail1 = new string[] { lengue, keyCopytitlenowgame1, keyLinknowgame1, keyCopydatesnowgame1 };
                        dicGamesWithGold.TryAdd(keyLinknowgame1, detail1);
                    }
                    if (!string.IsNullOrEmpty(keyLinknowgame2) && !dicGamesWithGold.ContainsKey(keyLinknowgame2))
                    {
                        string[] detail2 = new string[] { lengue, keyCopytitlenowgame2, keyLinknowgame2, keyCopydatesnowgame2 };
                        dicGamesWithGold.TryAdd(keyLinknowgame2, detail2);
                    }
                    if (!string.IsNullOrEmpty(keyLinknowgame3) && !dicGamesWithGold.ContainsKey(keyLinknowgame3))
                    {
                        string[] detail3 = new string[] { lengue, keyCopytitlenowgame3, keyLinknowgame3, keyCopydatesnowgame3 };
                        dicGamesWithGold.TryAdd(keyLinknowgame3, detail3);
                    }
                }
                result = result.NextMatch();
            }
            if (dicGamesWithGold.Count >= 1)
            {
                this.Invoke(new Action(() =>
                {
                    flpGameWithGold.Controls.Clear();
                    foreach (var item in dicGamesWithGold)
                    {
                        LinkLabel lb = new LinkLabel()
                        {
                            Tag = item.Value[0],
                            Text = item.Value[1] + "\n" + item.Value[3].Replace(" ", ""),
                            TextAlign = ContentAlignment.TopCenter,
                            AutoSize = true,
                            Parent = this.flpGameWithGold
                        };
                        string keyLinknowgame = item.Value[2];
                        if (keyLinknowgame.Contains("www.xbox.com/games/"))
                            keyLinknowgame = Regex.Replace(keyLinknowgame, @"/games/", "/" + item.Value[0] + "/games/");
                        else if (keyLinknowgame.Contains("www.microsoft.com/p/"))
                            keyLinknowgame = Regex.Replace(keyLinknowgame, @"/p/", "/" + item.Value[0] + "/p/");
                        else if (keyLinknowgame.Contains("marketplace.xbox.com/Product/"))
                            keyLinknowgame = Regex.Replace(keyLinknowgame, @"/Product/", "/" + item.Value[0] + "/Product/");
                        lb.Links.Add(0, item.Value[1].Length, keyLinknowgame);
                        lb.LinkClicked += new LinkLabelLinkClickedEventHandler(this.LinkGameWithGold_LinkClicked);
                    }
                    if (flpGameWithGold.VerticalScroll.Visible)
                    {
                        groupBox7.Height = (int)(groupBox7.Height + 30 * Form1.dpixRatio);
                        flpGameWithGold.Height = (int)(flpGameWithGold.Height + 30 * Form1.dpixRatio);
                    }
                }));
            }
        }

        private void LinkGameWithGold_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            LinkLabel lb = sender as LinkLabel;
            string langue = lb.Tag.ToString();
            tbGameUrl.Text = e.Link.LinkData as string;
            bool find = false;
            foreach (var item in cbGameMarket.Items)
            {
                Market market = (Market)item;
                if (market.lang.ToLowerInvariant() == langue)
                {
                    cbGameMarket.SelectedItem = item;
                    find = true;
                    break;
                }
            }
            if (!find)
            {
                cbGameMarket.Items.Add(new Market(langue, Regex.Replace(langue, "^[^-]+-", ""), langue));
                cbGameMarket.SelectedIndex = cbGameMarket.Items.Count - 1;
            }
            if (butGame.Enabled) ButGame_Click(null, null);
        }

        private void CbGameBundled_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbGameBundled.SelectedIndex < 0) return;
            tbGameTitle.Clear();
            tbGameDeveloperName.Clear();
            tbGameCategory.Clear();
            tbGameOriginalReleaseDate.Clear();
            tbGamePrice.Clear();
            tbGameDescription.Clear();
            tbGameLanguages.Clear();
            lvGame.Items.Clear();
            linkCompare.Enabled = false;
            linkGameWebsite.Enabled = false;

            var market = (Market)cbGameBundled.Tag;
            var json = (ClassGame.Game)gbGameInfo.Tag;
            GameAnalysis(market, json, cbGameBundled.SelectedIndex);
        }

        private void Xbox360Marketplace(string url, string region)
        {
            cbGameBundled.Tag = null;
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
            string title = string.Empty, developerName = string.Empty, category = string.Empty, originalReleaseDate = string.Empty, description = string.Empty;
            Match result = Regex.Match(socketPackage.Html, @"<title>(?<title>.+)</title>");
            if (result.Success) title = result.Groups["title"].Value;
            lock (ClassWeb.docLock)
            {
                ClassWeb.SetHtmlDocument(socketPackage.Html, false);
                if (ClassWeb.doc != null && ClassWeb.doc.Body != null)
                {
                    HtmlElement hec = ClassWeb.doc.GetElementById("ProductPublishing");
                    if (hec != null)
                    {
                        if (hec.Children.Count == 4)
                        {
                            result = Regex.Match(hec.OuterHtml, @"<LI><LABEL>[^<]+</LABEL>(?<releasedate>[^<]+)\r\n<LI><LABEL>[^<]+</LABEL>(?<developer>[^<]+)\r\n<LI><LABEL>[^<]+</LABEL>(?<publisher>[^<]+)\r\n<LI><LABEL>[^<]+</LABEL>(?<genres>[^<]+)");
                            if (result.Success)
                            {
                                if (DateTime.TryParse(result.Groups["releasedate"].Value, System.Globalization.CultureInfo.CreateSpecificCulture(region), System.Globalization.DateTimeStyles.None, out DateTime dt))
                                    originalReleaseDate = dt.ToShortDateString();
                                developerName = result.Groups["developer"].Value.Replace("&amp;", "&").Trim();
                                category = result.Groups["genres"].Value.Replace("&amp;", "&").Trim();
                            }
                        }
                        else if (hec.Children.Count == 3)
                        {
                            result = Regex.Match(hec.OuterHtml, @"<LABEL>[^<]+</LABEL>(?<g1>[^<]+)\r\n<LI><LABEL>[^<]+</LABEL>(?<g2>[^<]+)\r\n<LI><LABEL>[^<]+</LABEL>(?<g3>[^<]+)");
                            if (result.Success)
                            {
                                if (DateTime.TryParse(result.Groups["g1"].Value, System.Globalization.CultureInfo.CreateSpecificCulture(region), System.Globalization.DateTimeStyles.None, out DateTime dt))
                                {
                                    originalReleaseDate = dt.ToShortDateString();
                                    developerName = result.Groups["g2"].Value.Replace("&amp;", "&").Trim();
                                    category = result.Groups["g3"].Value.Replace("&amp;", "&").Trim();
                                }
                                else
                                {
                                    developerName = result.Groups["g1"].Value.Replace("&amp;", "&").Trim();
                                    category = result.Groups["g3"].Value.Replace("&amp;", "&").Trim();
                                }
                            }
                        }
                    }
                    hec = ClassWeb.doc.GetElementById("overview1");
                    if (hec != null)
                    {
                        description = hec.InnerText.Trim();
                        result = Regex.Match(hec.OuterHtml, @"<IMG[^>]+src=""(?<boxart>[^""]+)"">");
                        if (result.Success)
                        {
                            try
                            {
                                pbGame.LoadAsync(result.Groups["boxart"].Value);
                            }
                            catch { }
                        }
                    }
                }
                ClassWeb.ObjectDisposed();
            }
            this.Invoke(new Action(() =>
            {
                tbGameTitle.Text = title;
                tbGameDeveloperName.Text = developerName;
                tbGameCategory.Text = category;
                tbGameOriginalReleaseDate.Text = originalReleaseDate;
                tbGameDescription.Text = description;
                butGame.Enabled = true;
                linkGameWebsite.Enabled = true;
            }));
        }

        private void XboxStore(Market market, string productId)
        {
            cbGameBundled.Tag = market;
            string url = "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=" + productId + "&market=" + market.code + "&languages=" + market.lang + ",neutral&MS-CV=DGU1mcuYo0WMMp+F.1";
            SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
            if (Regex.IsMatch(socketPackage.Html, @"^{.+}$", RegexOptions.Singleline))
            {
                JavaScriptSerializer js = new JavaScriptSerializer();
                var json = js.Deserialize<ClassGame.Game>(socketPackage.Html);
                if (json != null && json.Products != null && json.Products.Count >= 1)
                {
                    GameAnalysis(market, json, 0);
                }
                else
                {
                    this.Invoke(new Action(() =>
                    {
                        MessageBox.Show("无效 URL/ProductId", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        butGame.Enabled = true;
                    }));
                }
            }
            else
            {
                this.Invoke(new Action(() =>
                {
                    MessageBox.Show("无法连接服务器，请稍候再试。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    butGame.Enabled = true;
                }));
            }
        }

        internal static ConcurrentDictionary<String, Double> dicExchangeRate = new ConcurrentDictionary<String, Double>();

        private void GameAnalysis(Market market, ClassGame.Game json, int index)
        {
            string title = string.Empty, developerName = string.Empty, description = string.Empty;
            var product = json.Products[index];
            List<string> bundledId = new List<string>();
            List<ListViewItem> lsDownloadUrl = new List<ListViewItem>();
            var localizedPropertie = product.LocalizedProperties;
            if (localizedPropertie != null && localizedPropertie.Count >= 1)
            {
                title = localizedPropertie[0].ProductTitle;
                developerName = localizedPropertie[0].DeveloperName;
                description = localizedPropertie[0].ProductDescription;
                string imageUri = string.Empty, tmpUri = null;
                int imgMin = 0;
                foreach (var image in localizedPropertie[0].Images)
                {
                    if (image.ImagePurpose == "Logo" || image.ImagePurpose == "BoxArt") //Poster, BrandedKeyArt
                    {
                        if (image.Width >= 300 && image.Width == image.Height)
                        {
                            if (string.IsNullOrEmpty(imageUri))
                            {
                                imgMin = image.Width;
                                imageUri = image.Uri;
                            }
                            else if (image.Width < imgMin)
                            {
                                imgMin = image.Width;
                                imageUri = image.Uri;
                            }
                        }
                    }
                    if (image.Width >= 300 && image.Width == image.Height)
                        tmpUri = image.Uri;
                }
                if (string.IsNullOrEmpty(imageUri)) imageUri = tmpUri;
                if (!string.IsNullOrEmpty(imageUri))
                {
                    try
                    {
                        pbGame.LoadAsync("http:" + imageUri);
                    }
                    catch { }
                }
            }

            string originalReleaseDate = string.Empty;
            var marketProperties = product.MarketProperties;
            if (marketProperties != null && marketProperties.Count >= 1)
            {
                originalReleaseDate = marketProperties[0].OriginalReleaseDate.ToString("d");
            }

            string category = string.Empty;
            var properties = product.Properties;
            if (properties != null)
            {
                category = properties.Category;
            }

            string languages = string.Empty;
            if (product.DisplaySkuAvailabilities != null)
            {
                foreach (var displaySkuAvailabilitie in product.DisplaySkuAvailabilities)
                {
                    if (displaySkuAvailabilitie.Sku.SkuType == "full")
                    {
                        if (displaySkuAvailabilitie.Sku.Properties.Packages != null)
                        {
                            foreach (var Packages in displaySkuAvailabilitie.Sku.Properties.Packages)
                            {
                                List<ClassGame.PlatformDependencies> platformDependencie = Packages.PlatformDependencies;
                                List<ClassGame.PackageDownloadUris> packageDownloadUri = Packages.PackageDownloadUris;
                                if (platformDependencie != null && packageDownloadUri != null && Packages.PlatformDependencies.Count >= 1 && packageDownloadUri.Count >= 1)
                                {
                                    string url = packageDownloadUri[0].Uri;
                                    if (url == "https://productingestionbin1.blob.core.windows.net") url = "";
                                    switch (platformDependencie[0].PlatformName)
                                    {
                                        case "Windows.Xbox":
                                            if (Packages.PackageRank == 51000) //packageDownloadUri[0].Uri.EndsWith("_xs.xvc")
                                                lsDownloadUrl.Add(new ListViewItem(new string[] { "Xbox Series X|S", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), url }));
                                            else
                                                lsDownloadUrl.Add(new ListViewItem(new string[] { "Xbox One", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), url }));
                                            break;
                                        case "Windows.Desktop":
                                            lsDownloadUrl.Add(new ListViewItem(new string[] { "微软商店(PC)", market.name, ClassMbr.ConvertBytes(Packages.MaxDownloadSizeInBytes), url }));
                                            break;
                                    }
                                    if (Packages.Languages != null) languages = string.Join(", ", Packages.Languages);
                                }
                            }
                        }
                        if (displaySkuAvailabilitie.Sku.Properties.BundledSkus != null)
                        {
                            foreach (var BundledSkus in displaySkuAvailabilitie.Sku.Properties.BundledSkus)
                            {
                                bundledId.Add(BundledSkus.BigId);
                            }
                        }
                        break;
                    }
                }
            }

            List<Product> lsProduct = new List<Product>();
            if (bundledId.Count >= 1 && json.Products.Count == 1)
            {
                string url = "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=" + string.Join(",", bundledId.ToArray()) + "&market=" + market.code + "&languages=" + market.lang + ",neutral&MS-CV=DGU1mcuYo0WMMp+F.1";
                SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
                if (Regex.IsMatch(socketPackage.Html, @"^{.+}$", RegexOptions.Singleline))
                {
                    JavaScriptSerializer js = new JavaScriptSerializer();
                    var json2 = js.Deserialize<ClassGame.Game>(socketPackage.Html);
                    if (json2 != null && json2.Products != null && json2.Products.Count >= 1)
                    {
                        json.Products.AddRange(json2.Products);
                        lsProduct.Add(new Product("在此捆绑包中(" + json2.Products.Count + ")", ""));
                        foreach (var product2 in json2.Products)
                        {
                            lsProduct.Add(new Product(product2.LocalizedProperties[0].ProductTitle, product2.ProductId));
                        }
                    }
                }
            }

            if (index == 0) gbGameInfo.Tag = json;
            string CurrencyCode = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.CurrencyCode.ToUpperInvariant();
            double MSRP = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.MSRP;
            double ListPrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.ListPrice;
            double ListPrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.ListPrice : 0;
            double WholesalePrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.WholesalePrice;
            double WholesalePrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.WholesalePrice : 0;
            if (ListPrice_1 > MSRP) MSRP = ListPrice_1;
            if (!string.IsNullOrEmpty(CurrencyCode) && MSRP > 0 && CurrencyCode != "CNY" && !dicExchangeRate.ContainsKey(CurrencyCode))
            {
                ClassGame.ExchangeRate(CurrencyCode);
            }
            double ExchangeRate = dicExchangeRate.ContainsKey(CurrencyCode) ? dicExchangeRate[CurrencyCode] : 0;

            this.Invoke(new Action(() =>
            {
                tbGameTitle.Text = title;
                tbGameDeveloperName.Text = developerName;
                tbGameCategory.Text = category;
                tbGameOriginalReleaseDate.Text = originalReleaseDate;
                if (lsProduct.Count >= 1)
                {
                    cbGameBundled.Items.AddRange(lsProduct.ToArray());
                    cbGameBundled.SelectedIndex = 0;
                    this.cbGameBundled.SelectedIndexChanged += new EventHandler(this.CbGameBundled_SelectedIndexChanged);
                }
                tbGameDescription.Text = description;
                tbGameLanguages.Text = languages;
                if (MSRP > 0)
                {

                    StringBuilder sb = new StringBuilder();
                    sb.Append(string.Format("币种: {0}, 建议零售价: {1}", CurrencyCode, String.Format("{0:N}", MSRP)));
                    if (ExchangeRate > 0)
                    {
                        sb.Append(string.Format("({0})", String.Format("{0:N}", MSRP * ExchangeRate)));
                    }
                    if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
                    {
                        sb.Append(string.Format(", 普通折扣{0}%: {1}", Math.Round(ListPrice_1 / MSRP * 100, 0, MidpointRounding.AwayFromZero), String.Format("{0:N}", ListPrice_1)));
                        if (ExchangeRate > 0)
                        {
                            sb.Append(string.Format("({0})", String.Format("{0:N}", ListPrice_1 * ExchangeRate)));
                        }
                    }
                    if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
                    {
                        string member = (product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags != null && product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags[0] == "LegacyDiscountEAAccess") ? "EA Play" : "金会员";
                        sb.Append(string.Format(", {0}折扣{1}%: {2}", member, Math.Round(ListPrice_2 / MSRP * 100, 0, MidpointRounding.AwayFromZero), String.Format("{0:N}", ListPrice_2)));
                        if (ExchangeRate > 0)
                        {
                            sb.Append(string.Format("({0})", String.Format("{0:N}", ListPrice_2 * ExchangeRate)));
                        }
                    }
                    if (WholesalePrice_1 > 0)
                    {
                        sb.Append(string.Format(", 批发价: {0}", String.Format("{0:N}", WholesalePrice_1)));
                        if (ExchangeRate > 0)
                        {
                            sb.Append(string.Format("({0})", String.Format("{0:N}", WholesalePrice_1 * ExchangeRate)));
                        }
                        if (WholesalePrice_2 > 0 && WholesalePrice_2 < WholesalePrice_1)
                        {
                            sb.Append(string.Format(", 批发价折扣{0}%: {1}", Math.Round(WholesalePrice_2 / WholesalePrice_1 * 100, 0, MidpointRounding.AwayFromZero), String.Format("{0:N}", WholesalePrice_2)));
                            if (ExchangeRate > 0)
                            {
                                sb.Append(string.Format("({0})", String.Format("{0:N}", WholesalePrice_2 * ExchangeRate)));
                            }
                        }
                    }
                    if (ExchangeRate > 0)
                    {
                        sb.Append(string.Format(", CNY汇率: {0}", ExchangeRate));
                    }
                    tbGamePrice.Text = sb.ToString();
                    linkCompare.Enabled = true;
                }
                if (lsDownloadUrl.Count >= 1)
                {
                    lsDownloadUrl.Sort((x, y) => string.Compare(x.SubItems[0].Text, y.SubItems[0].Text));
                    lvGame.Items.AddRange(lsDownloadUrl.ToArray());
                }
                butGame.Enabled = true;
                linkGameWebsite.Enabled = true;
            }));
        }

        private void LinkGameWebsite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            string url = e.Link.LinkData.ToString();
            url = url.Replace("/ar-AE/", "/en-AE/");
            Process.Start(url);
        }

        private void LinkCompare_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            int index = cbGameBundled.SelectedIndex == -1 ? 0 : cbGameBundled.SelectedIndex;
            FormCompare dialog = new FormCompare(gbGameInfo.Tag, index);
            dialog.ShowDialog();
            dialog.Dispose();
        }

        private void LvGame_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lvGame.SelectedItems.Count == 1 && !string.IsNullOrEmpty(lvGame.SelectedItems[0].SubItems[3].Text))
                {
                    tsmCopyUrl2.Enabled = Regex.IsMatch(lvGame.SelectedItems[0].SubItems[3].Text, @"\.xboxlive\.com");
                    contextMenuStrip2.Show(MousePosition.X, MousePosition.Y);
                }
            }
        }

        private void TsmCopyUrl1_Click(object sender, EventArgs e)
        {
            Clipboard.SetDataObject(lvGame.SelectedItems[0].SubItems[3].Text);
        }

        private void TsmCopyUrl2_Click(object sender, EventArgs e)
        {
            string url = lvGame.SelectedItems[0].SubItems[3].Text;
            url = url.Replace(".xboxlive.com", ".xboxlive.cn");
            Clipboard.SetDataObject(url);
        }

        private void LinkAppxAdd_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            tabControl1.SelectedTab = tabTool;
            tbAppxFilePath.Focus();
        }
        #endregion

        #region 选项卡-工具
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x219)
            {
                switch (m.WParam.ToInt32())
                {
                    case 0x8000: //U盘插入
                    case 0x8004: //U盘卸载
                        LinkRefreshDrive_LinkClicked(null, null);
                        break;
                    default:
                        break;
                }
            }
            base.WndProc(ref m);
        }

        private void LinkRefreshDrive_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            cbDrive.Items.Clear();
            DriveInfo[] driverList = Array.FindAll(DriveInfo.GetDrives(), a => a.DriveType == DriveType.Removable);
            if (driverList.Length >= 1)
            {
                cbDrive.Items.AddRange(driverList);
                cbDrive.SelectedIndex = 0;
            }
            else
            {
                labelStatusDrive.Text = "当前U盘状态：没有找到U盘";
            }
        }

        private void CbDrive_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (cbDrive.Items.Count >= 1)
            {
                string driverName = cbDrive.Text;
                DriveInfo driveInfo = new DriveInfo(driverName);
                if (driveInfo.DriveType == DriveType.Removable)
                {
                    if (driveInfo.IsReady && driveInfo.DriveFormat == "NTFS")
                    {
                        List<string> listStatus = new List<string>();
                        if (File.Exists(driverName + "$ConsoleGen8Lock"))
                            listStatus.Add(rbXboxOne.Text + " 回国");
                        else if (File.Exists(driverName + "$ConsoleGen8"))
                            listStatus.Add(rbXboxOne.Text + " 出国");
                        if (File.Exists(driverName + "$ConsoleGen9Lock"))
                            listStatus.Add(rbXboxSeries.Text + " 回国");
                        else if (File.Exists(driverName + "$ConsoleGen9"))
                            listStatus.Add(rbXboxSeries.Text + " 出国");
                        if (listStatus.Count >= 1)
                            labelStatusDrive.Text = "当前U盘状态：" + string.Join(", ", listStatus.ToArray());
                        else
                            labelStatusDrive.Text = "当前U盘状态：未转换";
                    }
                    else
                    {
                        labelStatusDrive.Text = "当前U盘状态：不是NTFS格式";
                    }
                }
            }
        }

        private void ButConsoleRegionUnlock_Click(object sender, EventArgs e)
        {
            ConsoleRegion(true);
        }

        private void ButConsoleRegionLock_Click(object sender, EventArgs e)
        {
            ConsoleRegion(false);
        }

        private void ConsoleRegion(bool unlock)
        {
            if (cbDrive.Items.Count == 0)
            {
                MessageBox.Show("请插入U盘。", "没有找到U盘", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            labelStatusDrive.Text = "当前U盘状态：制作中，请稍候...";
            string driverName = cbDrive.Text;
            DriveInfo driveInfo = new DriveInfo(driverName);
            if (driveInfo.DriveType == DriveType.Removable)
            {
                if (!driveInfo.IsReady || driveInfo.DriveFormat != "NTFS")
                {
                    string show, caption, cmd;
                    if (driveInfo.IsReady && driveInfo.DriveFormat == "FAT32")
                    {
                        show = "当前U盘格式 " + driveInfo.DriveFormat + "，是否把U盘转换为 NTFS 格式？\n\n注意，如果U盘有重要数据请先备份!\n\n当前U盘位置： " + driverName + "，容量：" + ClassMbr.ConvertBytes(Convert.ToUInt64(driveInfo.TotalSize)) + "\n取消转换请按\"否(N)\"";
                        caption = "转换U盘格式";
                        cmd = "convert " + Regex.Replace(driverName, @"\\$", "") + " /fs:ntfs /x";
                    }
                    else
                    {
                        show = "当前U盘格式 " + (driveInfo.IsReady ? driveInfo.DriveFormat : "RAW") + "，是否把U盘格式化为 NTFS？\n\n警告，格式化将删除U盘中的所有文件!\n警告，格式化将删除U盘中的所有文件!\n警告，格式化将删除U盘中的所有文件!\n\n当前U盘位置： " + driverName + "，容量：" + (driveInfo.IsReady ? ClassMbr.ConvertBytes(Convert.ToUInt64(driveInfo.TotalSize)) : "未知") + "\n取消格式化请按\"否(N)\"";
                        caption = "格式化U盘";
                        cmd = "format " + Regex.Replace(driverName, @"\\$", "") + " /fs:ntfs /q";
                    }
                    if (MessageBox.Show(show, caption, MessageBoxButtons.YesNo, MessageBoxIcon.Warning, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
                    {
                        string outputString;
                        using (Process p = new Process())
                        {
                            p.StartInfo.FileName = "cmd.exe";
                            p.StartInfo.UseShellExecute = false;
                            p.StartInfo.RedirectStandardInput = true;
                            p.StartInfo.RedirectStandardError = true;
                            p.StartInfo.RedirectStandardOutput = true;
                            p.StartInfo.CreateNoWindow = true;
                            p.Start();

                            p.StandardInput.WriteLine(cmd);
                            p.StandardInput.WriteLine("exit");

                            p.StandardInput.Close();
                            outputString = p.StandardOutput.ReadToEnd();
                            p.WaitForExit();
                            p.Close();
                        }
                    }
                }
                if (driveInfo.IsReady && driveInfo.DriveFormat == "NTFS")
                {
                    if (File.Exists(driverName + "$ConsoleGen8"))
                        File.Delete(driverName + "$ConsoleGen8");
                    if (File.Exists(driverName + "$ConsoleGen9"))
                        File.Delete(driverName + "$ConsoleGen9");
                    if (File.Exists(driverName + "$ConsoleGen8Lock"))
                        File.Delete(driverName + "$ConsoleGen8Lock");
                    if (File.Exists(driverName + "$ConsoleGen9Lock"))
                        File.Delete(driverName + "$ConsoleGen9Lock");
                    if (rbXboxOne.Checked)
                    {
                        using (File.Create(driverName + (unlock ? "$ConsoleGen8" : "$ConsoleGen8Lock"))) { }
                    }
                    else if (rbXboxSeries.Checked)
                    {
                        using (File.Create(driverName + (unlock ? "$ConsoleGen9" : "$ConsoleGen9Lock"))) { }
                    }
                }
                else
                {
                    MessageBox.Show("U盘不是NTFS格式，请重新格式化NTFS格式后再转换。", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                CbDrive_SelectedIndexChanged(null, null);
            }
            else
            {
                labelStatusDrive.Text = "当前U盘状态：" + driverName + " 设备不存在";
            }
        }

        private void LinkAppxRefreshDrive_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            cbAppxDrive.Items.Clear();
            DriveInfo[] driverList = Array.FindAll(DriveInfo.GetDrives(), a => a.DriveType == DriveType.Fixed);
            if (driverList.Length >= 1)
            {
                cbAppxDrive.Items.AddRange(driverList);
                cbAppxDrive.SelectedIndex = 0;
            }
        }

        private void ButAppxOpenFile_Click(object sender, EventArgs e)
        {
            OpenFileDialog ofd = new OpenFileDialog
            {
                Title = "Open an Xbox Package"
            };
            if (ofd.ShowDialog() != DialogResult.OK)
                return;

            string sFilePath = ofd.FileName;
            tbAppxFilePath.Text = sFilePath;
        }

        private void ButAppxInstall_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(tbAppxFilePath.Text)) return;
            if (Environment.OSVersion.Version.Major < 10)
            {
                MessageBox.Show("需要Win10操作系统。", "操作系统版本过低", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            using (FileStream fs = File.Create(".install_appx.ps1"))
            {
                Byte[] byteArray = new UTF8Encoding(true).GetBytes("Add-AppxPackage -Path \"" + tbAppxFilePath.Text + "\" -Volume \"" + cbAppxDrive.Text + "\"");
                fs.Write(byteArray, 0, byteArray.Length);
                fs.Close();
            }
            File.SetAttributes(".install_appx.ps1", FileAttributes.Hidden);
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "cmd.exe";
                p.StartInfo.UseShellExecute = false;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardError = true;
                p.StartInfo.CreateNoWindow = false;
                p.Start();
                p.StandardInput.WriteLine("powershell -executionpolicy remotesigned -file \".install_appx.ps1\"");
                p.StandardInput.WriteLine("del /a/f/q \".install_appx.ps1\"");
                p.StandardInput.WriteLine("exit");
            }
            tbAppxFilePath.Clear();
        }

        private void GetEACdn()
        {
            string eaCoreIni = string.Empty;
            using (var key = Microsoft.Win32.Registry.LocalMachine)
            {
                var rk = key.OpenSubKey(@"SOFTWARE\WOW6432Node\Origin");
                if (rk == null) rk = key.OpenSubKey(@"SOFTWARE\Origin");
                if (rk != null)
                {
                    string originPath = (string)rk.GetValue("OriginPath", null);
                    if (File.Exists(originPath))
                    {
                        linkEaOriginRepair.Links[0].LinkData = originPath;
                        linkEaOriginRepair.Enabled = true;
                        eaCoreIni = Path.GetDirectoryName(originPath) + "\\EACore.ini";
                    }
                    rk.Close();
                }
            }
            if (string.IsNullOrEmpty(eaCoreIni))
            {
                labelStatusEACdn.Text += "没有安装 Origin";
                return;
            }
            gpEACdn.Tag = eaCoreIni;
            string str = string.Empty;
            using (StreamReader sw = new StreamReader(eaCoreIni))
            {
                str = sw.ReadToEnd();
            }
            Match result = Regex.Match(str, @"CdnOverride=(.+)");
            if (result.Success)
                labelStatusEACdn.Text += Thread.CurrentThread.CurrentCulture.TextInfo.ToTitleCase(result.Groups[1].Value.Trim());
            else
                labelStatusEACdn.Text += "自动";
        }

        private void ButEACdn_Click(object sender, EventArgs e)
        {
            if (gpEACdn.Tag == null)
            {
                MessageBox.Show("没有安装 Origin", "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                return;
            }
            string cdn = string.Empty, status;
            if (rbEACdn1.Checked)
            {
                cdn = "[connection]\r\nEnvironmentName=production\r\n\r\n[Feature]\r\nCdnOverride=akamai\r\n";
                status = "当前使用CDN：Akamai";
            }
            else if (rbEACdn2.Checked)
            {
                cdn = "[connection]\r\nEnvironmentName=production\r\n\r\n[Feature]\r\nCdnOverride=level3\r\n";
                status = "当前使用CDN：Level3";
            }
            else
            {
                status = "当前使用CDN：自动";
            }
            using (StreamWriter sw = new StreamWriter(gpEACdn.Tag.ToString(), false))
            {
                sw.Write(cdn);
            }
            labelStatusEACdn.Text = status;
        }

        private void LinkEaOriginRepair_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            if (MessageBox.Show("此操作将删除Origin缓存文件和登录信息，执行下一步之前请先退出Origin，是否继续？", "修复 EA Origin", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button2) == DialogResult.Yes)
            {
                Process[] processes = Process.GetProcessesByName("Origin");
                if (processes.Length == 0)
                {
                    try
                    {
                        DirectoryInfo di1 = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\Origin");
                        if (di1.Exists) di1.Delete(true);
                        DirectoryInfo di2 = new DirectoryInfo(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\AppData\\Local\\Origin");
                        if (di2.Exists) di2.Delete(true);
                    }
                    catch { }
                    Process.Start(e.Link.LinkData.ToString());
                }
                else
                {
                    MessageBox.Show("请先退出 Origin。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Asterisk);
                }
            }
        }
        #endregion

        private void Dgv_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y, dgv.RowHeadersWidth - 1, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), dgv.RowHeadersDefaultCellStyle.Font, rectangle, dgv.RowHeadersDefaultCellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        delegate void CallbackTextBox(TextBox tb, string str);
        public void SetTextBox(TextBox tb, string str)
        {
            if (tb.InvokeRequired)
            {
                CallbackTextBox d = new CallbackTextBox(SetTextBox);
                Invoke(d, new object[] { tb, str });
            }
            else tb.Text = str;
        }

        delegate void CallbackSaveLog(string status, string content, string ip, int argb);
        public void SaveLog(string status, string content, string ip, int argb = 0)
        {
            if (lvLog.InvokeRequired)
            {
                CallbackSaveLog d = new CallbackSaveLog(SaveLog);
                Invoke(d, new object[] { status, content, ip, argb });
            }
            else
            {
                ListViewItem listViewItem = new ListViewItem(new string[] { status, content, ip, DateTime.Now.ToString("HH:mm:ss.fff") });
                if (argb >= 1) listViewItem.ForeColor = Color.FromArgb(argb);
                lvLog.Items.Insert(0, listViewItem);
            }
        }

        class DoubleBufferListView : ListView
        {
            public DoubleBufferListView()
            {
                SetStyle(ControlStyles.DoubleBuffer | ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint, true);
                UpdateStyles();
            }
        }
    }
}