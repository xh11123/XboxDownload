﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace XboxDownload
{
    public partial class FormCompare : Form
    {
        private readonly string productId;
        private readonly ConcurrentDictionary<String, DataGridViewRow> dicDgvr =  new ConcurrentDictionary<String, DataGridViewRow>();
        private bool discount_ListPrice_1 = false, discount_ListPrice_2 = false, discount_WholesalePrice_1 = false, discount_WholesalePrice_2 = false;
        private string member = null;

        public FormCompare(object js, int index)
        {
            InitializeComponent();

            if (Form1.dpixRatio > 1)
            {
                dataGridView1.RowHeadersWidth = (int)(dataGridView1.RowHeadersWidth * Form1.dpixRatio);
                foreach (DataGridViewColumn col in dataGridView1.Columns)
                    col.Width = (int)(col.Width * Form1.dpixRatio);
            }

            var json = (ClassGame.Game)js;
            var product = json.Products[index];
            this.productId = product.ProductId;
            
            int cbWidth = (int)(135 * Form1.dpixRatio), cbHeight = (int)(16 * Form1.dpixRatio);
            List<Market> lsMarket = new List<Market>();
            lsMarket.AddRange((new List<Market>
            {
                new Market("阿尔及利亚", "DZ", "ar-DZ"),
                new Market("阿曼", "OM", "ar-OM"),
                new Market("埃及", "EG", "ar-EG"),
                new Market("巴基斯坦", "PK", "en-PK"),
                new Market("巴林", "BH", "ar-BH"),
                new Market("保加利亚", "BG", "bg-BG"),
                new Market("冰岛","IS",  "is-IS"),
                new Market("菲律宾", "PH", "en-PH"),
                new Market("哥斯达黎加", "CR", "es-CR"),
                new Market("哈萨克斯坦", "KZ", "ru-KZ"),
                new Market("卡塔尔", "QA", "en-QA"),
                new Market("科威特", "KW", "ar-KW"),
                new Market("肯尼亚", "KE", "en-KE"),
                new Market("黎巴嫩", "LB", "ar-LB"),
                new Market("列支敦士登", "LI", "de-LI"),
                new Market("罗马尼亚", "RO", "ro-RO"),
                new Market("马来西亚", "MY", "en-MY"),
                new Market("毛里塔尼亚乌吉亚", "MR", "ar-MR"),
                new Market("孟加拉", "BD", "en-BD"),
                new Market("秘鲁", "PE", "es-PE"),
                new Market("尼日利亚", "NG", "en-NG"),
                new Market("塞尔维亚", "RS", "en-RS"),
                new Market("泰国", "TH", "th-TH"),
                new Market("特立尼达和多巴哥", "TT", "en-TT"),
                new Market("突尼斯", "TN", "ar-TN"),
                new Market("危地马拉", "GT", "es-GT"),
                new Market("乌克兰", "UA", "uk-UA"),
                new Market("伊拉克", "IQ", "ar-IQ"),
                new Market("印度尼西亚", "ID", "id-ID"),
                new Market("约旦", "JO", "ar-JO"),
                new Market("越南", "VN", "vi-VN")
            }).ToArray());

            List<Market> ls = Form1.lsMarket.Union(lsMarket).ToList<Market>();
            ls.Sort((x, y) => string.Compare(x.name, y.name));
            foreach (Market market in ls)
            {
                string code = market.code;
                string name = market.name;
                string lang = market.lang;
                switch (code)
                {
                    case "DE":
                    case "NL":
                    case "FR":
                    case "SK":
                    case "PT":
                    case "FI":
                    case "IE":
                    case "AT":
                    case "IT":
                    case "BE":
                    case "GR":
                    case "ES":
                        code = "DE";
                        name = "欧元区";
                        lang = "de-DE";
                        break;
                }
                if (dicDgvr.ContainsKey(code)) continue;
                CheckBox cb = new CheckBox()
                {
                    Text = name,
                    Size = new Size(cbWidth, cbHeight),
                    Parent = this.flowLayoutPanel1
                };
                cb.CheckedChanged += new EventHandler(CheckBox_CheckedChanged);
                DataGridViewRow dgvr = new DataGridViewRow();
                dgvr.CreateCells(dataGridView1);
                dgvr.Resizable = DataGridViewTriState.False;
                dgvr.Cells[0].Value = code;
                dgvr.Cells[1].Value = lang;
                switch (code)
                {
                    case "AR":
                    case "EG":
                    case "RU":
                    case "TR":
                        dgvr.Cells[2].Value = name + " (锁)";
                        break;
                    default:
                        dgvr.Cells[2].Value = name;
                        break;
                }
                dgvr.Cells[11].Value = "双击前往";
                cb.Tag = dgvr;
                dicDgvr.TryAdd(code, dgvr);
            }

            /*
            foreach (string code in product.LocalizedProperties[0].Markets)
            {
                if (dicDgvr.ContainsKey(code)) continue;
                CheckBox cb = new CheckBox()
                {
                    Text = code,
                    Size = new Size(cbWidth, cbHeight),
                    Parent = this.flowLayoutPanel1
                };
                cb.CheckedChanged += new EventHandler(CheckBox_CheckedChanged);
                DataGridViewRow dgvr = new DataGridViewRow();
                dgvr.CreateCells(dataGridView1);
                dgvr.Resizable = DataGridViewTriState.False;
                dgvr.Cells[0].Value = code;
                //dgvr.Cells[1].Value = ;
                dgvr.Cells[2].Value = code;
                //dgvr.Cells[11].Value = "双击前往";
                cb.Tag = dgvr;
                dicDgvr.TryAdd(code, dgvr);
            }
            */

            double MSRP = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.MSRP;
            double ListPrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.ListPrice;
            double ListPrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.ListPrice : 0;
            double WholesalePrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.WholesalePrice;
            double WholesalePrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.WholesalePrice : 0;
            
            double priceRatio = 0;
            if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
            {
                discount_ListPrice_1 = true;
                priceRatio = Math.Round(ListPrice_1 / MSRP * 100, 0, MidpointRounding.AwayFromZero);
            }
            if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
            {
                discount_ListPrice_2 = true;
                priceRatio = Math.Round(ListPrice_2 / MSRP * 100, 0, MidpointRounding.AwayFromZero);
                this.member = (product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags != null && product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags[0] == "LegacyDiscountEAAccess") ? "EA Play" : "金会员";
                dataGridView1.Columns["Col_ListPrice_2"].HeaderText = this.member + "折扣";
            }
            if (WholesalePrice_1 > 0)
            {
                discount_WholesalePrice_1 = true;
                discount_WholesalePrice_2 = WholesalePrice_2 > 0 && WholesalePrice_2 < WholesalePrice_1;
            }
            dataGridView1.Columns["Col_ListPrice_1"].Visible = discount_ListPrice_1;
            dataGridView1.Columns["Col_ListPrice_2"].Visible = discount_ListPrice_2;
            dataGridView1.Columns["Col_WholesalePrice_1"].Visible = discount_WholesalePrice_1;
            dataGridView1.Columns["Col_WholesalePrice_2"].Visible = discount_WholesalePrice_2;
            
            if (discount_ListPrice_1 || discount_ListPrice_2 || discount_WholesalePrice_2)
                groupBox2.Text = product.LocalizedProperties[0].ProductTitle + " (折扣: " + priceRatio + "%，剩余：" + (new TimeSpan(product.DisplaySkuAvailabilities[0].Availabilities[0].Conditions.EndDate.Ticks - DateTime.Now.Ticks).Days) + "天，打折时段：" + product.DisplaySkuAvailabilities[0].Availabilities[0].Conditions.StartDate + " - " + product.DisplaySkuAvailabilities[0].Availabilities[0].Conditions.EndDate + ")";
            else if (product.LocalizedProperties[0].EligibilityProperties.Affirmations.Length >= 1)
            {
                string description = product.LocalizedProperties[0].EligibilityProperties.Affirmations[0].Description;
                if (description.Contains("EA Play"))
                    groupBox2.Text = product.LocalizedProperties[0].ProductTitle + " (使用您的 EA Play 会员资格，游戏可享最高9折优惠)";
                else if (description.Contains("Xbox Game Pass"))
                    groupBox2.Text = product.LocalizedProperties[0].ProductTitle + " (使用您的 Xbox Game Pass 会员资格，游戏可享最高8折优惠，附加内容最高9折优惠)";
                else
                    groupBox2.Text = product.LocalizedProperties[0].ProductTitle + " (" + description + ")";
            }
            else
                groupBox2.Text = product.LocalizedProperties[0].ProductTitle;
        }

        private void CheckBox_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox cb = sender as CheckBox;
            DataGridViewRow dgvr = cb.Tag as DataGridViewRow;
            if (cb.Checked)
                dataGridView1.Rows.Add(dgvr);
            else
                dataGridView1.Rows.Remove(dgvr);
            dataGridView1.ClearSelection();
            groupBox1.Text = "选择商店 ("+ dataGridView1.Rows.Count + ")";
        }

        private void DataGridView1_CellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex == -1 || e.RowIndex == -1) return;
            if (dataGridView1.Columns[e.ColumnIndex].Name != "Col_Purchase") return;
            DataGridViewRow dgvr = dataGridView1.Rows[e.RowIndex];
            System.Diagnostics.Process.Start("https://www.microsoft.com/" + dgvr.Cells["Col_Lang"].Value + "/p/_/" + this.productId);
        }

        private void LinkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            foreach(Control control in flowLayoutPanel1.Controls)
            {
                if (control is CheckBox)
                {
                    CheckBox cb = control as CheckBox;
                    cb.Checked = true;
                }
            }
        }

        private void LinkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            foreach (Control control in flowLayoutPanel1.Controls)
            {
                if (control is CheckBox)
                {
                    CheckBox cb = control as CheckBox;
                    cb.Checked = false;
                }
            }
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            List<DataGridViewRow> list = new List<DataGridViewRow>();
            foreach (DataGridViewRow dgvr in dataGridView1.Rows)
            {
                if (!dgvr.Visible) continue;
                list.Add(dgvr);
            }
            if (list.Count >= 1)
            {
                button1.Enabled = false;
                ThreadPool.QueueUserWorkItem(delegate { Price(list); });
            }
        }

        private void DataGridView1_RowPostPaint(object sender, DataGridViewRowPostPaintEventArgs e)
        {
            DataGridView dgv = (DataGridView)sender;
            Rectangle rectangle = new Rectangle(e.RowBounds.Location.X, e.RowBounds.Location.Y, dgv.RowHeadersWidth - 1, e.RowBounds.Height);
            TextRenderer.DrawText(e.Graphics, (e.RowIndex + 1).ToString(), dgv.RowHeadersDefaultCellStyle.Font, rectangle, dgv.RowHeadersDefaultCellStyle.ForeColor, TextFormatFlags.VerticalCenter | TextFormatFlags.Right);
        }

        private void Price(List<DataGridViewRow> list)
        {
            Task[] tasks = new Task[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                int index = i;
                tasks[index] = new Task(() =>
                {
                    if (bClosed) return;
                    DataGridViewRow dgvr = list[index];
                    if (dgvr.Tag == null)
                    {
                        string url = "https://displaycatalog.mp.microsoft.com/v7.0/products?bigIds=" + productId + "&market=" + dgvr.Cells["Col_Code"].Value + "&languages=neutral&MS-CV=DGU1mcuYo0WMMp+F.1";
                        SocketPackage socketPackage = ClassWeb.HttpRequest(url, "GET", null, null, true, false, true, null, null, null, ClassWeb.useragent, null, null, null, null, 0, null);
                        if (bClosed) return;
                        if (Regex.IsMatch(socketPackage.Html, @"^{.+}$", RegexOptions.Singleline))
                        {
                            JavaScriptSerializer js = new JavaScriptSerializer();
                            var json = js.Deserialize<ClassGame.Game>(socketPackage.Html);
                            var product = json.Products[0];
                            if (json != null && json.Products != null && json.Products.Count >= 1 && product.LocalizedProperties != null)
                            {
                                string CurrencyCode = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.CurrencyCode.ToUpperInvariant();
                                double MSRP = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.MSRP;
                                double ListPrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.ListPrice;
                                double ListPrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.ListPrice : 0;
                                double WholesalePrice_1 = product.DisplaySkuAvailabilities[0].Availabilities[0].OrderManagementData.Price.WholesalePrice;
                                double WholesalePrice_2 = product.DisplaySkuAvailabilities[0].Availabilities.Count >= 2 ? product.DisplaySkuAvailabilities[0].Availabilities[1].OrderManagementData.Price.WholesalePrice : 0;
                                if (ListPrice_1 > MSRP) MSRP = ListPrice_1;
                                if (!string.IsNullOrEmpty(CurrencyCode) && MSRP > 0 && CurrencyCode != "CNY" && !Form1.dicExchangeRate.ContainsKey(CurrencyCode))
                                {
                                    ClassGame.ExchangeRate(CurrencyCode);
                                    if (bClosed) return;
                                }
                                double ExchangeRate = Form1.dicExchangeRate.ContainsKey(CurrencyCode) ? Form1.dicExchangeRate[CurrencyCode] : 0;
                                dgvr.Tag = json;
                                if (MSRP > 0)
                                {
                                    dgvr.Cells["Col_CurrencyCode"].Value = CurrencyCode;
                                    dgvr.Cells["Col_MSRP"].Value = MSRP;
                                    if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
                                    {
                                        dgvr.Cells["Col_ListPrice_1"].Value = ListPrice_1;
                                        discount_ListPrice_1 = true;
                                    }
                                    if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
                                    {
                                        dgvr.Cells["Col_ListPrice_2"].Value = ListPrice_2;
                                        discount_ListPrice_2 = true;
                                        if(string.IsNullOrEmpty(this.member))
                                            this.member = (product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags != null && product.DisplaySkuAvailabilities[0].Availabilities[1].Properties.MerchandisingTags[0] == "LegacyDiscountEAAccess") ? "EA Play" : "金会员";
                                    }
                                    if (WholesalePrice_1 > 0)
                                    {
                                        dgvr.Cells["Col_WholesalePrice_1"].Value = WholesalePrice_1;
                                        discount_WholesalePrice_1 = true;
                                        if (WholesalePrice_2 > 0 && WholesalePrice_2 < WholesalePrice_1)
                                        {
                                            dgvr.Cells["Col_WholesalePrice_2"].Value = WholesalePrice_2;
                                            discount_WholesalePrice_2 = true;
                                        }
                                    }
                                    if (ExchangeRate > 0)
                                    {
                                        if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
                                            dgvr.Cells["Col_CNY"].Value = ListPrice_2 * ExchangeRate;
                                        else if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
                                            dgvr.Cells["Col_CNY"].Value = ListPrice_1 * ExchangeRate;
                                        else
                                            dgvr.Cells["Col_CNY"].Value = MSRP * ExchangeRate;
                                        dgvr.Cells["Col_CNYExchangeRate"].Value = ExchangeRate;
                                    }
                                    else if (CurrencyCode == "CNY")
                                    {
                                        if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
                                            dgvr.Cells["Col_CNY"].Value = ListPrice_2;
                                        else if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
                                            dgvr.Cells["Col_CNY"].Value = ListPrice_1;
                                        else
                                            dgvr.Cells["Col_CNY"].Value = MSRP;
                                    }
                                }
                                else dgvr.Cells["Col_CurrencyCode"].Value = "不可用";
                            }
                        }
                    }
                    else if (dgvr.Cells["Col_CNYExchangeRate"].Value == null && dgvr.Cells["Col_MSRP"].Value != null && dgvr.Cells["Col_CurrencyCode"].Value.ToString() != "CNY")
                    {
                        string CurrencyCode = dgvr.Cells["Col_CurrencyCode"].Value.ToString();
                        double MSRP = Convert.ToDouble( dgvr.Cells["Col_MSRP"].Value);
                        if (MSRP > 0)
                        {
                            if (!string.IsNullOrEmpty(CurrencyCode) && MSRP > 0 && CurrencyCode != "CNY" && !Form1.dicExchangeRate.ContainsKey(CurrencyCode))
                            {
                                ClassGame.ExchangeRate(CurrencyCode);
                                if (bClosed) return;
                            }
                            double ExchangeRate = Form1.dicExchangeRate.ContainsKey(CurrencyCode) ? Form1.dicExchangeRate[CurrencyCode] : 0;
                            if (ExchangeRate > 0)
                            {
                                double ListPrice_1 = Convert.ToDouble(dgvr.Cells["Col_ListPrice_1"].Value);
                                double ListPrice_2 = Convert.ToDouble(dgvr.Cells["Col_ListPrice_2"].Value);
                                if (ListPrice_2 > 0 && ListPrice_2 < ListPrice_1 && ListPrice_2 != MSRP)
                                    dgvr.Cells["Col_CNY"].Value = ListPrice_2 * ExchangeRate;
                                else if (ListPrice_1 > 0 && ListPrice_1 != MSRP)
                                    dgvr.Cells["Col_CNY"].Value = ListPrice_1 * ExchangeRate;
                                else
                                    dgvr.Cells["Col_CNY"].Value = MSRP * ExchangeRate;
                                dgvr.Cells["Col_CNYExchangeRate"].Value = ExchangeRate;
                            }
                        }
                    }
                });
                tasks[index].Start();
            }
            Task.WaitAll(tasks);
            if (bClosed) return;
            this.Invoke(new Action(() =>
            {
                List<DataGridViewRow> lsDgvr = new List<DataGridViewRow>();
                for (int i = dataGridView1.Rows.Count - 1; i >= 0; i--)
                {
                    if (dataGridView1.Rows[i].Cells["Col_CNY"].Value == null)
                    {
                        lsDgvr.Add(dataGridView1.Rows[i]);
                        dataGridView1.Rows.RemoveAt(i);
                    }
                }
                dataGridView1.Sort(dataGridView1.Columns["Col_CNY"], ListSortDirection.Ascending);
                if (lsDgvr.Count >= 1)
                {
                    lsDgvr.Reverse();
                    dataGridView1.Rows.AddRange(lsDgvr.ToArray());
                }
                if (dataGridView1.Rows[0].Visible)
                {
                    dataGridView1.Rows[0].Cells["Col_Store"].Selected = true;
                }
                dataGridView1.Columns["Col_ListPrice_2"].HeaderText = this.member + "折扣";
                dataGridView1.Columns["Col_ListPrice_1"].Visible = discount_ListPrice_1;
                dataGridView1.Columns["Col_ListPrice_2"].Visible = discount_ListPrice_2;
                dataGridView1.Columns["Col_WholesalePrice_1"].Visible = discount_WholesalePrice_1;
                dataGridView1.Columns["Col_WholesalePrice_2"].Visible = discount_WholesalePrice_2;
                button1.Enabled = true;
            }));
        }

        bool bClosed = false;
        private void FormCompare_FormClosing(object sender, FormClosingEventArgs e)
        {
            bClosed = true;
        }
    }
}
