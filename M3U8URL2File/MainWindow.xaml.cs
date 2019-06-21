using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MessageBox = System.Windows.MessageBox;

namespace M3U8URL2File
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            TextBox_Regex.Text = "(https?|ftp|file)://[-A-Za-z0-9+&@#/%?=~_|!:,.;]+[-A-Za-z0-9+&@#/%=~_|]";
            TextBox_Input.Focus();
        }

        private void TextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9]+");
            e.Handled = re.IsMatch(e.Text);
        }

        private void Button_Match_Click(object sender, RoutedEventArgs e)
        {
            if (TextBox_StartIndex.Text == "")
                TextBox_StartIndex.Text = "1";

            Regex regex = new Regex(@TextBox_Regex.Text);
            TextBox_MatchCount.Text = regex.Matches(TextBox_Input.Text).Count.ToString();
            if (TextBox_MatchCount.Text == "0")
                Button_Down.IsEnabled = false;
            MatchCollection mc = Regex.Matches(TextBox_Input.Text, @TextBox_Regex.Text, RegexOptions.IgnoreCase);
            StringBuilder sb = new StringBuilder();
            foreach (Match m in mc)
            {
                sb.Append(m.Value + "\r\n");
            }
            TextBox_Input.Text = sb.ToString().Trim();

            if (TextBox_Input.Text.Trim() == "")
                return;

            Button_Down.IsEnabled = true;
        }

        private void Button_Down_Click(object sender, RoutedEventArgs e)
        {
            FolderBrowserDialog openFileDialog = new FolderBrowserDialog();  //选择文件夹
            openFileDialog.Description = "选择一个目录，M3U8文件们将会下载到此处";
            string dir = "";
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                dir = openFileDialog.SelectedPath;
            }

            if (dir == "")
                return;

            int startIndex = Convert.ToInt32(TextBox_StartIndex.Text);
            int initIndex = startIndex;
            int total = Convert.ToInt32(TextBox_MatchCount.Text);
            string input = TextBox_Input.Text;
            Button_Down.IsEnabled = false;
            Button_Match.IsEnabled = false;

            try
            {
                ThreadPool.QueueUserWorkItem((object state) =>
                {
                    foreach (var url in input.Split('\n'))
                    {
                        WebClient down = new WebClient();
                        down.Encoding = Encoding.UTF8;
                        string outpath = "";
                        string m3u8_baseUrl = GetBaseUrl(url);
                        string m3u8_temp = down.DownloadString(url);

                        ArrayList lines = new ArrayList();
                        lines.Clear();
                        using (StringReader sr = new StringReader(m3u8_temp))  //判断m3u8是否需要增加baseurl
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                lines.Add(line);
                                if (lines.Count > 1 && lines[lines.Count - 2].ToString().StartsWith("#EXTINF:"))
                                {
                                    if (!lines[lines.Count - 1].ToString().StartsWith("http"))
                                    {
                                        lines[lines.Count - 1] = CombineURL(m3u8_baseUrl, lines[lines.Count - 1].ToString());
                                    }
                                }
                            }
                        }

                        double time = 0;
                        //计算时长
                        if (m3u8_temp.Contains("#EXTINF:"))
                        {
                            Regex regex = new Regex("#EXTINF:(.*),");
                            MatchCollection matches = regex.Matches(m3u8_temp);
                            foreach (Match match in matches)
                            {
                                time += Convert.ToDouble(match.Groups[1].Value);
                            }
                        }
                        if (time < 0)
                            time = 0;
                        this.Dispatcher.BeginInvoke(new Action(() =>
                        {
                            if (time != 0)
                                outpath = dir + "\\" + TextBox_Prefix.Text + "_" + (startIndex++).ToString("000") + "_" + FormatTime(Convert.ToInt32(time)) + ".m3u8";
                            else
                                outpath = dir + "\\" + TextBox_Prefix.Text + "_" + FormatTime(Convert.ToInt32(time)) + ".m3u8";
                            StreamWriter writer = new StreamWriter(outpath, false);  //false代表替换而不是追加
                            for (int j = 0; j < lines.Count; j++)
                            {
                                writer.WriteLine(lines[j]);
                            }
                            writer.Close();
                            TextBox_MatchCount.Text = (startIndex - 1) + "/" + (total + initIndex - 1);
                        }));
                    }

                    this.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        TextBox_MatchCount.Text = "0";
                        Button_Match.IsEnabled = true;
                    }));
                    MessageBox.Show("下载完成！");
                }, null);
            }
            catch (Exception)
            {
                TextBox_MatchCount.Text = "0";
                Button_Match.IsEnabled = true;
            }
        }

        //此函数用于格式化输出时长
        public static String FormatTime(Int32 time)
        {
            TimeSpan ts = new TimeSpan(0, 0, time);
            string str = "";
            str = (ts.Hours.ToString("00") == "00" ? "" : ts.Hours.ToString("00") + "h") + ts.Minutes.ToString("00") + "m" + ts.Seconds.ToString("00") + "s";
            return str;
        }

        /// <summary>
        /// 从url中截取字符串充当baseurl
        /// </summary>
        /// <param name="m3u8url"></param>
        /// <returns></returns>
        public string GetBaseUrl(string m3u8url)
        {
            string url = m3u8url;
            if (url.Contains("?"))
                url = url.Remove(url.LastIndexOf('?'));
            url = url.Substring(0, url.LastIndexOf('/') + 1);
            return url;
        }

        /// <summary>
        /// 拼接Baseurl和RelativeUrl
        /// </summary>
        /// <param name="baseurl">Baseurl</param>
        /// <param name="url">RelativeUrl</param>
        /// <returns></returns>
        public string CombineURL(string baseurl, string url)
        {
            if (baseurl == "")
                baseurl = url;

            Uri uri1 = new Uri(baseurl);  //这里直接传完整的URL即可
            Uri uri2 = new Uri(uri1, url);
            url = uri2.ToString();

            return url;
        }
    }
}
