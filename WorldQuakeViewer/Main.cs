﻿using CoreTweet;
using LL2FERC;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using WorldQuakeViewer.Properties;

namespace WorldQuakeViewer//todo:discordに送るやつを追加
{
    public partial class MainForm : Form
    {
        public static readonly string Version = "1.1.0α6";//こことアセンブリを変える
        public static DateTime StartTime = new DateTime();
        public static int AccessedEMSC = 0;
        public static int AccessedUSGS = 0;
        public string LatestURL = "";
        public static bool NoFirst = false;//最初はツイートとかしない
        public static string ExeLogs = "";
        public static History EMSCHist = new History();
        public static Dictionary<string, History> USGSHist = new Dictionary<string, History>();//EQID,Data
        public string LatestUSGSText = "";
        public string LatestEMSCText = "";
        public static Bitmap bitmap = new Bitmap(1600, 1000);
        public static Bitmap bitmap_USGS = new Bitmap(800, 1000);
        public static FontFamily font;
        public MainForm()
        {
            InitializeComponent();
        }
        private void MainForm_Load(object sender, EventArgs e)//ExeLog($"");
        {
            ExeLog($"[Main]起動処理開始");
            StartTime = DateTime.Now;
            ErrorText.Text = "フォント読み込み中…";
            try
            {
                while (!File.Exists("Font\\Koruri-Regular.ttf"))
                {
                    if (!Directory.Exists("Font"))
                        Directory.CreateDirectory("Font");
                    Process.Start("https://koruri.github.io/");
                    Process.Start("explorer.exe", "Font");
                    DialogResult Result = MessageBox.Show($"フォントファイルが見つかりません。ダウンロードサイトとFontフォルダを開きます。\"Koruri-Regular.ttf\"をFontフォルダにコピーしてください。", "WQV_FontCheck", MessageBoxButtons.AbortRetryIgnore, MessageBoxIcon.Exclamation);
                    if (Result == DialogResult.Ignore)
                        break;
                    if (Result != DialogResult.Retry)
                    {
                        Application.Exit();
                        break;//これないとプロセス無限起動される
                    }
                    Thread.Sleep(1000);//念のため
                }
                ExeLog($"[Main]フォントファイルOK");
                PrivateFontCollection pfc = new PrivateFontCollection();
                pfc.AddFontFile("Font\\Koruri-Regular.ttf");
                font = pfc.Families[0];
                ExeLog($"[Main]フォントOK");
            }
            catch
            {
                ExeLog($"[Main]フォント確認に失敗");
            }
            ErrorText.Text = "設定読み込み中…";
            Configuration Config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            if (File.Exists("UserSetting.xml"))//AppDataに保存
            {
                if (!Directory.Exists(Config.FilePath.Replace("\\user.config", "")))//実質更新時
                    Directory.CreateDirectory(Config.FilePath.Replace("\\user.config", ""));
                File.Copy("UserSetting.xml", Config.FilePath, true);
                ExeLog($"[Main]設定ファイルをAppDataにコピー");
            }
            SettingReload();
            ErrorText.Text = "設定の読み込みが完了しました。";
            ExeLog($"[Main]設定読み込み完了");
            EMSCget.Enabled = true;
        }

        private async void EMSCget_Tick(object sender, EventArgs e)//TODO:処理を複数にするか？(処理自体は最新のだけでいい)
        {
            ExeLog($"[EMSC]取得開始");
            //次の0/30秒までの時間を計算
            DateTime now = DateTime.Now;
            DateTime Next15Second = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0).AddMinutes(1);
            DateTime Next45Second = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 30);
            if (now.Second >= 29)//念のため29にしとく
                Next45Second = Next45Second.AddMinutes(1);
            EMSCget.Interval = (int)Math.Max(10000, Math.Min((Next15Second - now).TotalMilliseconds, (Next45Second - now).TotalMilliseconds));
            ExeLog($"[EMSC]次回実行まであと{EMSCget.Interval}ms");
            try
            {
                //https://www.emsc-csem.org/Earthquake/earthquake.php?id=
                //https://www.seismicportal.eu/fdsnws/event/1/query?limit=1&format=text&minmag=5.0
                //#EventID        |Time                    |Latitude|Longitude|Depth/km|Author|Catalog |Contributor|ContributorID|MagType|Magnitude|MagAuthor|EventLocationName
                //20230701_0000038|2023-07-01T03:29:25.534Z|-31.7703|-68.7812 |30.3    |NEIC  |EMSC-RTS|NEIC       |1522821      |mb     |5.3      |NEIC     |SAN JUAN, ARGENTINA
                // 0              | 1                      | 2      | 3       | 4      | 5    | 6      | 7         | 8           | 9     | 10      | 11      | 12
                //?               |                        |        |         |        |ソース|全部同? |ソース     |ID           |       |         |ソース   |
                WebClient wc = new WebClient();
                ErrorText.Text = "[EMSC]取得中…";
                string text = await wc.DownloadStringTaskAsync(new Uri("https://www.seismicportal.eu/fdsnws/event/1/query?limit=1&format=text&minmag=5.0"));
                ExeLog($"[EMSC]処理開始");
                AccessedEMSC++;
                string[] texts = text.Split('\n')[1].Split('|');
                string time = texts[1];
                DateTime Time = DateTime.Parse(time);
                long Time_long = Time.Ticks;
                DateTimeOffset TimeOff = Time.ToLocalTime();
                string TimeSt = Convert.ToString(TimeOff).Replace("+0", " UTC +0").Replace("+1", " UTC +1").Replace("-0", " UTC -0").Replace("+1", " UTC -1");
                string TimeJP = TimeOff.DateTime.ToString("d日HH時mm分ss秒");
                string lat = texts[2];
                double Lat = double.Parse(lat);
                string lon = texts[3];
                double Lon = double.Parse(lon);
                Lat2String(Lat, out string LatStLongJP, out string LatDisplay);
                Lon2String(Lon, out string LonStLongJP, out string LonDisplay);
                string depth = texts[4];
                double Depth = double.Parse(depth);
                string DepthSt = depth + "km";
                string source = texts[5];
                string id = texts[8];
                string URL = "https://www.emsc-csem.org/Earthquake/earthquake.php?id=" + id;
                string magType = texts[9];
                string mag = texts[10];
                double Mag = double.Parse(mag);
                string MagSt = Mag.ToString("0.0");
                int hypoCode = LL2FERCode.Code(Lat, Lon);
                string hypoJP = LL2FERCode.Name_JP(hypoCode);
                string hypoEN = texts[12];
                string MagTypeWithSpace = magType.Length == 3 ? magType : magType.Length == 2 ? "   " + magType : "      " + magType;

                string LogText = $"EMSC地震情報【{magType}{MagSt}】{TimeSt}\n{hypoJP}({hypoEN})\n{LatDisplay},{LonDisplay}　{DepthSt}\n{URL}";
                string BouyomiText = $"EMSC地震情報。{TimeJP}発生、マグニチュード{MagSt}、震源、{hypoJP.Replace(" ", "、").Replace("/", "、")}、{LatStLongJP}、{LonStLongJP}、深さ{DepthSt}キロメートル。";

                History history = new History
                {
                    URL = URL,
                    TweetID = 0,//更新の場合は上書き前に変更するから0でおｋ

                    Time = Time_long,
                    HypoJP = hypoJP,
                    HypoEN = hypoEN,//()つかない
                    Lat = Lat,
                    Lon = Lon,
                    Depth = Depth,
                    MagType = magType,
                    Mag = Mag
                };
                int SoundLevel = 0;//音声判別用 初報ほど,M大きいほど高い
                bool New = false;
                bool NewUpdt = false;
                int i = 0;
                if (EMSCHist.ID == id)//同じか更新
                {
                    if (Settings.Default.Update_EMSC_Time)
                        if (EMSCHist.Time != history.Time)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]Time:{EMSCHist.Time}->{history.Time}");
                        }
                    if (Settings.Default.Update_EMSC_HypoJP)
                        if (EMSCHist.HypoJP != history.HypoJP)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]HypoJP:{EMSCHist.HypoJP}->{history.HypoJP}");
                        }
                    if (Settings.Default.Update_EMSC_HypoEN)
                        if (EMSCHist.HypoEN != history.HypoEN)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]HypoEN:{EMSCHist.HypoEN}->{history.HypoEN}");
                        }
                    if (Settings.Default.Update_EMSC_LatLon)
                        if (EMSCHist.Lat != history.Lat || EMSCHist.Lon != history.Lon)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]Lat:{EMSCHist.Lat}->{history.Lat}, Lon:{EMSCHist.Lon}->{history.Lon}");
                        }
                    if (Settings.Default.Update_EMSC_Depth)
                        if (EMSCHist.Depth != history.Depth)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]Depth:{EMSCHist.Depth}->{history.Depth}");
                        }
                    if (Settings.Default.Update_EMSC_MagType)
                        if (EMSCHist.MagType != history.MagType)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]MagType:{EMSCHist.MagType}->{history.MagType}");
                        }
                    if (Settings.Default.Update_EMSC_Mag)
                        if (EMSCHist.Mag != history.Mag)
                        {
                            NewUpdt = true;
                            ExeLog($"[EMSC]Mag:{EMSCHist.Mag}->{history.Mag}");
                        }
                    if (NewUpdt)
                    {
                        LogText = LogText.Replace("EMSC地震情報", "EMSC地震情報(更新)");
                        BouyomiText = BouyomiText.Replace("EMSC地震情報", "EMSC地震情報、更新");
                        ExeLog($"[EMSC]{id}更新検知");
                    }
                }
                else
                {
                    New = true;
                    NewUpdt = true;
                    ExeLog($"[EMSC]{id}初回");
                }

                if (NewUpdt)
                {
                    EMSCHist = history;
                    LogSave("Log\\M4.5+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", id);
                    if (Settings.Default.Socket_Enable)
                        SendSocket(LogText);
                    if (SoundLevel < 1 && Settings.Default.Sound_45_Enable)//SoundLevel上昇+M4.5以上有効
                        SoundLevel = New ? 2 : 1;
                    if (Mag >= 6)
                    {
                        LogSave("Log\\M6.0+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", id);

                        if (SoundLevel < 3 && Settings.Default.Sound_60_Enable)
                            SoundLevel = New ? 4 : 3;
                        if (Mag >= 8)
                        {
                            LogSave("Log\\M8.0+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", id);
                            if (SoundLevel < 5 && Settings.Default.Sound_80_Enable)
                                SoundLevel = New ? 6 : 5;
                        }
                    }
                    if (i == 0)
                        LatestEMSCText = LogText;
                    if (Mag >= Settings.Default.Bouyomichan_LowerMagnitudeLimit)
                        if (Settings.Default.Bouyomichan_Enable)
                            Bouyomichan(BouyomiText);
                    if (Mag >= Settings.Default.Tweet_LowerMagnitudeLimit)
                        if (Settings.Default.Tweet_Enable)
                            Tweet(LogText, "EMSC", id);
                }
                else
                    ExeLog($"[EMSC][{i}] 内容更新なし");

                ErrorText.Text = "[EMSC]描画中…";
                ExeLog($"[EMSC]描画開始");
                Graphics g = Graphics.FromImage(bitmap);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 30)), 0, 0, 800, 1000);

                int locX = Lon > 0 ? (int)Math.Round((Lon + 90d) * 10d, MidpointRounding.AwayFromZero) : (int)Math.Round((Lon + 450d) * 10d, MidpointRounding.AwayFromZero);
                int locY = (int)Math.Round((90d - Lat) * 10d, MidpointRounding.AwayFromZero);
                int locX_image = 400 - locX;
                int locY_image = Math.Min(200, Math.Max(-800, 600 - locY));
                g.FillRectangle(new SolidBrush(Color.FromArgb(60, 60, 90)), 0, 200, 800, 800);
                ImageCheck("map.png");
                g.DrawImage(Image.FromFile("Image\\map.png"), locX_image, locY_image, 5400, 1800);
                ColorMap[] ColorChange = new ColorMap[]
                {
                    new ColorMap()
                };
                ColorChange[0].OldColor = Color.Black;
                ColorChange[0].NewColor = Color.Transparent;
                ImageAttributes ia = new ImageAttributes();
                ia.SetRemapTable(ColorChange);
                ImageCheck("hypo.png");
                g.DrawImage(Image.FromFile("Image\\hypo.png"), new Rectangle(360, locY + locY_image - 40, 80, 80), 0, 0, 80, 80, GraphicsUnit.Pixel, ia);

                g.FillRectangle(new SolidBrush(Color.FromArgb(128, 0, 0, 30)), 480, 950, 320, 50);
                g.DrawString("地図データ:Natural Earth", new Font(font, 19), Brushes.White, 490, 956);

                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 30)), 0, 0, 800, 200);
                g.FillRectangle(new SolidBrush(Color.FromArgb(30, 30, 60)), 4, 30, 792, 166);
                Brush color = Mag2Brush(Mag);
                g.DrawString($"EMSC地震情報(M5.0+)                                {TimeSt}", new Font(font, 17), Brushes.White, 0, 0);
                g.DrawString($"{hypoJP}\n({hypoEN})\n{LatDisplay}, {LonDisplay}   深さ:{DepthSt}\nID:{id}  ソース:{source}", new Font(font, 21), color, 4, 32);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 30)), 796, 0, 4, 200);
                g.DrawString(MagTypeWithSpace, new Font(font, 20), color, 590, 160);
                g.DrawString(MagSt, new Font(font, 50), color, 670, 100);
                g.DrawImage(bitmap_USGS, 800, 0, 800, 1000);
                if (!NoFirst)
                {
                    g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 30)), 800, 0, 800, 1000);
                    g.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200)), 800, 0, 800, 1000);
                }
                ExeLog($"[EMSC]描画完了");

                g.Dispose();
                MainImage.BackgroundImage = null;
                MainImage.BackgroundImage = bitmap;
                wc.Dispose();
            }
            catch (WebException ex)
            {
                ErrorText.Text = $"[EMSC]ネットワークエラーが発生しました。内容:{ex.Message}";
            }
            catch (Exception ex)
            {
                LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main Version:{Version}\n{ex}");
                ErrorText.Text = $"[EMSC]エラーが発生しました。エラーログの内容を報告してください。内容:{ex.Message}";
            }
            if (!ErrorText.Text.Contains("エラー"))
                ErrorText.Text = "";
            NoFirst = true;
            USGSget.Enabled = true;
            ExeLog("[EMSC]処理終了");
        }

        private async void USGSget_Tick(object sender, EventArgs e)
        {
            ExeLog($"[USGS]取得開始");
            //次の15/45秒までの時間を計算
            DateTime now = DateTime.Now;
            DateTime Next15Second = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 15);
            DateTime Next45Second = Next15Second.AddSeconds(30);
            if (now.Second >= 14)//念のため14にしとく
                Next15Second = Next15Second.AddMinutes(1);
            if (now.Second >= 44)//念のため44にしとく
                Next45Second = Next45Second.AddMinutes(1);
            USGSget.Interval = (int)Math.Max(10000, Math.Min((Next15Second - now).TotalMilliseconds, (Next45Second - now).TotalMilliseconds));
            ExeLog($"[USGS]次回実行まであと{USGSget.Interval}ms");
            try
            {
                ErrorText.Text = "[USGS]取得中…";
                WebClient wc = new WebClient();
                string json_ = await wc.DownloadStringTaskAsync(new Uri("https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/4.5_week.geojson"));
                ExeLog($"[USGS]処理開始");
                AccessedUSGS++;
                JObject json = JObject.Parse(json_);
                json_ = "";//早く処分(多分効果ほぼない) 
                int SoundLevel = 0;//音声判別用 初報ほど,M大きいほど高い
                int DatasCount = Math.Min(Settings.Default.Update_USGS_MaxCount, (int)json.SelectToken("metadata.count"));
                string[] IDs = new string[6];
                for (int i = DatasCount - 1; i >= 0; i--)//送信の都合上古い順に
                {
                    JToken features = json.SelectToken($"features[{i}]");
                    bool New = false;//音声判別用
                    string ID = (string)features.SelectToken("id");
                    ExeLog($"[USGS]処理[{i}]:{ID}");
                    if (i < 6)
                        IDs[i] = ID;
                    JToken propertie = features.SelectToken("properties");
                    long Updated = (long)propertie.SelectToken("updated");
                    DateTimeOffset Update = DateTimeOffset.FromUnixTimeMilliseconds(Updated).ToLocalTime();
                    string UpdateTime = $"{Update:yyyy/MM/dd HH:mm:ss}";
                    long LastUpdated = USGSHist.ContainsKey(ID) ? USGSHist[ID].Update : 0;
                    ErrorText.Text = $"処理中…[{DatasCount - i}/{DatasCount}]";
                    if (Updated != LastUpdated)//新規か更新
                    {
                        ExeLog($"[USGS][{i}] 更新時刻変化検知({LastUpdated}->{Updated})");
                        double? MMI = (double?)propertie.SelectToken("mmi");
                        string MMISt = $"({MMI})".Replace("()", "-");//(1.2)/-
                        string MaxInt = MMI < 1.5 ? "I" : MMI < 2.5 ? "II" : MMI < 3.5 ? "III" : MMI < 4.5 ? "IV" : MMI < 5.5 ? "V" : MMI < 6.5 ? "VI" : MMI < 7.5 ? "VII" : MMI < 8.5 ? "VIII" : MMI < 9.5 ? "IX" : MMI < 10.5 ? "X" : MMI < 11.5 ? "XI" : MMI >= 11.5 ? "XII" : "-";
                        long Time = (long)propertie.SelectToken("time");
                        DateTimeOffset DataTimeOff = DateTimeOffset.FromUnixTimeMilliseconds(Time).ToLocalTime();
                        string TimeSt = Convert.ToString(DataTimeOff).Replace("+0", " UTC +0").Replace("+1", " UTC +1").Replace("-0", " UTC -0").Replace("+1", " UTC -1");
                        string TimeJP = DataTimeOff.DateTime.ToString("d日HH時mm分ss秒");
                        double Mag = (double)propertie.SelectToken("mag");
                        string MagSt = Mag.ToString("0.0#");
                        string MagType = (string)propertie.SelectToken("magType");
                        double Lat = (double)features.SelectToken("geometry.coordinates[1]");
                        double Lon = (double)features.SelectToken("geometry.coordinates[0]");
                        Lat2String(Lat, out double LatShort, out string LatStDecimal, out string LatStShort, out string LatStLong, out string LatStLongJP, out string LatDisplay);
                        Lon2String(Lon, out double LonShort, out string LonStDecimal, out string LonStShort, out string LonStLong, out string LonStLongJP, out string LonDisplay);
                        string Alert = (string)propertie.SelectToken("alert");
                        string AlertJP = Alert == null ? "アラート:-" : $"アラート:{Alert.Replace("green", "緑").Replace("yellow", "黄").Replace("orange", "オレンジ").Replace("red", "赤").Replace("pending", "保留中")}";
                        double Depth = (double)features.SelectToken("geometry.coordinates[2]");
                        string DepthSt = Depth == (int)Depth ? $"(深さ:{Depth}km?)" : $"深さ:約{(int)Math.Round(Depth, MidpointRounding.AwayFromZero)}km";
                        string DepthLong = Depth == (int)Depth ? DepthSt : $"深さ:{Depth}km";
                        string HypoJP = LL2FERCode.Name_JP(LL2FERCode.Code(Lat, Lon));
                        string HypoEN = $"({(string)propertie.SelectToken("place")})";
                        string URL = (string)propertie.SelectToken("url");
                        string LogText = $"USGS地震情報【{MagType}{MagSt}】{TimeSt}\n{HypoJP}{HypoEN}\n{LatDisplay},{LonDisplay}　{Depth}\n推定最大改正メルカリ震度階級:{MaxInt}{MMISt.Replace("-", "")}　{AlertJP.Replace("アラート:-", "")}\n{URL}";
                        string BouyomiText = $"USGS地震情報。{TimeJP}発生、マグニチュード{MagSt}、震源、{HypoJP.Replace(" ", "、").Replace("/", "、")}、{LatStLongJP}、{LonStLongJP}、深さ{DepthLong.Replace("深さ:", "")}。{$"推定最大改正メルカリ震度階級{MMISt.Replace("(", "").Replace(")", "")}。".Replace("推定最大改正メルカリ震度階級-。", "")}{AlertJP.Replace("アラート:-", "")}";
                        string MagTypeWithSpace = MagType.Length == 3 ? MagType : MagType.Length == 2 ? "   " + MagType : "      " + MagType;
                        History history = new History
                        {
                            URL = URL,
                            Update = Updated,
                            TweetID = 0,//更新の場合は上書き前に変更するから0でおｋ

                            Display1 = $"{TimeSt} 発生  ID:{ID}\n{HypoJP}\n{LatDisplay}, {LonDisplay}   {DepthLong}\n推定最大改正メルカリ震度階級:{MaxInt}{MMISt.Replace("-", "")}",
                            Display2 = $"{MagTypeWithSpace}",
                            Display3 = $"{MagSt}",

                            Time = Time,
                            HypoJP = HypoJP,
                            HypoEN = HypoEN,//()付く
                            Lat = Lat,
                            Lon = Lon,
                            Depth = Depth,
                            MagType = MagType,
                            Mag = Mag,
                            MMI = MMI,
                            Alert = Alert
                        };
                        bool NewUpdt = false;
                        if (!USGSHist.ContainsKey(ID))//Keyないと探したときエラーになるから別化
                            NewUpdt = true;
                        else
                        {
                            if (Settings.Default.Update_USGS_Time)
                                if (USGSHist[ID].Time != history.Time)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]Time:{USGSHist[ID].Time}->{history.Time}");
                                }
                            if (Settings.Default.Update_USGS_HypoJP)
                                if (USGSHist[ID].HypoJP != history.HypoJP)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]HypoJP:{USGSHist[ID].HypoJP}->{history.HypoJP}");
                                }
                            if (Settings.Default.Update_USGS_HypoEN)
                                if (USGSHist[ID].HypoEN != history.HypoEN)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]HypoEN:{USGSHist[ID].HypoEN}->{history.HypoEN}");
                                }
                            if (Settings.Default.Update_USGS_LatLon)
                                if (USGSHist[ID].Lat != history.Lat || USGSHist[ID].Lon != history.Lon)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]Lat:{USGSHist[ID].Lat}->{history.Lat}, Lon:{USGSHist[ID].Lon}->{history.Lon}");
                                }
                            if (Settings.Default.Update_USGS_Depth)
                                if (USGSHist[ID].Depth != history.Depth)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]Depth:{USGSHist[ID].Depth}->{history.Depth}");
                                }
                            if (Settings.Default.Update_USGS_MagType)
                                if (USGSHist[ID].MagType != history.MagType)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]MagType:{USGSHist[ID].MagType}->{history.MagType}");
                                }
                            if (Settings.Default.Update_USGS_Mag)
                                if (USGSHist[ID].Mag != history.Mag)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]Mag:{USGSHist[ID].Mag}->{history.Mag}");
                                }
                            if (Settings.Default.Update_USGS_MMI)
                                if (USGSHist[ID].MMI != history.MMI)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]MMI:{USGSHist[ID].MMI}->{history.MMI}");
                                }
                            if (Settings.Default.Update_USGS_Alert)
                                if (USGSHist[ID].Alert != history.Alert)
                                {
                                    NewUpdt = true;
                                    ExeLog($"[USGS]Alert:{USGSHist[ID].Alert}->{history.Alert}");
                                }
                            LogText = LogText.Replace("USGS地震情報", "USGS地震情報(更新)");
                            BouyomiText = BouyomiText.Replace("USGS地震情報", "USGS地震情報、更新");
                        }
                        if (NewUpdt)
                        {
                            if (USGSHist.ContainsKey(ID))//更新
                            {

                                ExeLog($"[USGS]{ID}更新検知");
                                history.TweetID = USGSHist[ID].TweetID;
                                USGSHist[ID] = history;
                            }
                            else//new
                            {
                                ExeLog($"[USGS]{ID}初回");
                                New = true;
                                USGSHist.Add(ID, history);
                            }
                            LogSave("Log\\M4.5+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", ID);
                            if (Settings.Default.Socket_Enable)
                                SendSocket(LogText);
                            if (SoundLevel < 1 && Settings.Default.Sound_45_Enable)//SoundLevel上昇+M4.5以上有効
                                SoundLevel = New ? 2 : 1;
                            if (Mag >= 6)
                            {
                                LogSave("Log\\M6.0+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", ID);

                                if (SoundLevel < 3 && Settings.Default.Sound_60_Enable)
                                    SoundLevel = New ? 4 : 3;
                                if (Mag >= 8)
                                {
                                    LogSave("Log\\M8.0+", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Version:{Version}\n{LogText}", ID);
                                    if (SoundLevel < 5 && Settings.Default.Sound_80_Enable)
                                        SoundLevel = New ? 6 : 5;
                                }
                            }
                            if (i == 0)
                                LatestUSGSText = LogText;
                            if (Mag >= Settings.Default.Bouyomichan_LowerMagnitudeLimit || MMI >= Settings.Default.Bouyomichan_LowerMMILimit)
                                if (Settings.Default.Bouyomichan_Enable)
                                    Bouyomichan(BouyomiText);
                            if (Mag >= Settings.Default.Tweet_LowerMagnitudeLimit || MMI >= Settings.Default.Tweet_LowerMMILimit)
                                if (Settings.Default.Tweet_Enable)
                                    Tweet(LogText, "USGS", ID);
                        }
                        else
                            ExeLog($"[USGS][{i}] 内容更新なし(更新:{UpdateTime})");
                    }
                }
                ErrorText.Text = "[USGS]描画中…";
                Graphics g = Graphics.FromImage(bitmap_USGS);
                g.FillRectangle(new SolidBrush(Color.FromArgb(0, 0, 30)), 0, 0, 800, 1000);
                g.DrawRectangle(new Pen(Color.FromArgb(200, 200, 200)), 0, 0, 800, 1000);
                g.DrawString($"USGS地震情報(M4.5+)                                              Version:{Version}", new Font(font, 20), Brushes.White, 2, 2);
                for (int i = 0; i < 6; i++)
                {
                    if (USGSHist.Count > i)//データ不足対処
                    {
                        History hist = USGSHist[IDs[i]];
                        Brush color = Mag2Brush(hist.Mag);
                        g.FillRectangle(new SolidBrush(Alert2Color(hist.Alert)), 4, 40 + 160 * i, 792, 156);
                        g.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 90)), 8, 44 + 160 * i, 784, 148);
                        g.DrawString(hist.Display1, new Font(font, 19), color, 8, 44 + 160 * i);
                        g.DrawString(hist.Display2, new Font(font, 19), color, 570, 154 + 160 * i);
                        g.DrawString(hist.Display3, new Font(font, 50), color, 640, 110 + 160 * i);
                    }
                    else
                        g.FillRectangle(new SolidBrush(Color.FromArgb(45, 45, 90)), 4, 40 + 160 * i, 792, 156);
                }
                g = null;
                g = Graphics.FromImage(bitmap);
                g.DrawImage(bitmap_USGS, 800, 0, 800, 1000);
                MainImage.BackgroundImage = null;
                MainImage.BackgroundImage = bitmap;
                g.Dispose();

                if (SoundLevel == 1)
                    Sound("M45u.wav");
                else if (SoundLevel == 2)
                    Sound("M45.wav");
                else if (SoundLevel == 3)
                    Sound("M60u.wav");
                else if (SoundLevel == 4)
                    Sound("M60.wav");
                else if (SoundLevel == 5)
                    Sound("M80u.wav");
                else if (SoundLevel == 6)
                    Sound("M80.wav");
                ExeLog($"[USGS]ログ保持数:{USGSHist.Count}");
                wc.Dispose();
            }
            catch (WebException ex)
            {
                ErrorText.Text = $"[USGS]ネットワークエラーが発生しました。内容:{ex.Message}";
            }
            catch (Exception ex)
            {
                LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main Version:{Version}\n{ex}");
                ErrorText.Text = $"[USGS]エラーが発生しました。エラーログの内容を報告してください。内容:{ex.Message}";
            }
            if (!ErrorText.Text.Contains("エラー"))
                ErrorText.Text = "";
            NoFirst = true;
            ExeLog("[USGS]処理終了");
        }
        /// <summary>
        /// ログを保存します。
        /// </summary>
        /// <param name="SaveDirectory">保存するディレクトリ。</param>
        /// <param name="SaveText">保存するテキスト。</param>
        /// <param name="ID">地震ログ保存時用地震ID。</param>
        public static void LogSave(string SaveDirectory, string SaveText, string ID = "unknown")
        {
            DateTime NowTime = DateTime.Now;
            if (Directory.Exists("Log") == false)
                Directory.CreateDirectory("Log");
            if (!Directory.Exists(SaveDirectory))
                Directory.CreateDirectory(SaveDirectory);
            if (SaveDirectory == "Log")
                File.WriteAllText($"Log\\log.txt", SaveText);
            else if (SaveDirectory == "Log\\ErrorLog")
            {
                if (File.Exists($"Log\\ErrorLog\\{NowTime:yyyyMM}.txt"))
                    SaveText += "\n--------------------------------------------------\n" + File.ReadAllText($"Log\\ErrorLog\\{NowTime:yyyyMM}.txt");
                File.WriteAllText($"Log\\ErrorLog\\{NowTime:yyyyMM}.txt", SaveText);
            }
            else if (SaveDirectory.Contains("Log\\M"))
            {
                if (!Directory.Exists($"{SaveDirectory}\\{NowTime:yyyyMM}"))
                    Directory.CreateDirectory($"{SaveDirectory}\\{NowTime:yyyyMM}");
                if (!Directory.Exists($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:dd}"))
                    Directory.CreateDirectory($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:dd}");
                if (File.Exists($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:dd}\\{NowTime:yyyyMMdd}_{ID}.txt"))
                    SaveText = File.ReadAllText($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:dd}\\{NowTime:yyyyMMdd}_{ID}.txt") + "\n--------------------------------------------------\n" + SaveText;
                File.WriteAllText($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:dd}\\{NowTime:yyyyMMdd}_{ID}.txt", SaveText);
            }
            else
            {
                if (!Directory.Exists($"{SaveDirectory}\\{NowTime:yyyyMM}"))
                    Directory.CreateDirectory($"{SaveDirectory}\\{NowTime:yyyyMM}");
                if (File.Exists($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:yyyyMMdd}.txt"))
                    SaveText = File.ReadAllText($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:yyyyMMdd}.txt") + "\n--------------------------------------------------\n" + SaveText;
                File.WriteAllText($"{SaveDirectory}\\{NowTime:yyyyMM}\\{NowTime:yyyyMMdd}.txt", SaveText);
            }
        }
        /// <summary>
        /// ツイートします。
        /// </summary>
        /// <param name="Text">ツイートするテキスト。</param>
        /// <param name="source">データ元</param>
        /// <param name="ID">リプライ判別用ID。</param>
        public async void Tweet(string Text, string source, string ID)
        {
            if (NoFirst)
                try
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;
                    Tokens tokens;
                    try
                    {
                        tokens = Tokens.Create(Settings.Default.Tweet_ConsumerKey, Settings.Default.Tweet_ConsumerSecret, Settings.Default.Tweet_AccessToken, Settings.Default.Tweet_AccessSecret);
                    }
                    catch
                    {
                        throw new Exception("Tokenが正しくありません。");
                    }
                    Status status = new Status();

                    if (source == "EMSC")
                    {
                        if (EMSCHist.TweetID != 0)
                            try
                            {
                                ExeLog($"[Tweet]ツイート(リプライ)中…(ID:{EMSCHist.TweetID})");
                                status = await tokens.Statuses.UpdateAsync(new { status = Text, in_reply_to_status_id = EMSCHist.TweetID });
                            }
                            catch
                            {
                                ExeLog($"[Tweet]ツイート(リプライ)失敗、リトライ中…");
                                status = await tokens.Statuses.UpdateAsync(new { status = Text });
                            }
                        else
                        {
                            ExeLog($"[Tweet]ツイート中…");
                            status = await tokens.Statuses.UpdateAsync(new { status = Text });
                        }
                        EMSCHist.TweetID = status.Id;
                    }
                    else if (source == "USGS")
                    {
                        if (USGSHist[ID].TweetID != 0)
                            try
                            {
                                ExeLog($"[Tweet]ツイート(リプライ)中…(ID:{USGSHist[ID].TweetID})");
                                status = await tokens.Statuses.UpdateAsync(new { status = Text, in_reply_to_status_id = USGSHist[ID].TweetID });
                            }
                            catch
                            {
                                ExeLog($"[Tweet]ツイート(リプライ)失敗、リトライ中…");
                                status = await tokens.Statuses.UpdateAsync(new { status = Text });
                            }
                        else
                        {
                            ExeLog($"[Tweet]ツイート中…");
                            status = await tokens.Statuses.UpdateAsync(new { status = Text });
                        }
                        USGSHist[ID].TweetID = status.Id;
                    }
                    ExeLog($"[Tweet]ツイート成功(ID:{status.Id})");
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"ツイートに失敗しました。わからない場合エラーログの内容を報告してください。内容:{ex.Message}";
                    LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main,Tweet Version:{Version}\n{ex}");
                }
        }
        /// <summary>
        /// Socket通信で送信します。
        /// </summary>
        /// <param name="Text">送信する文。</param>
        public void SendSocket(string Text)
        {
            if (NoFirst)
                try
                {
                    ExeLog($"[SendSocket]Socket送信中…({Settings.Default.Socket_Host}:{Settings.Default.Socket_Port})");
                    IPEndPoint iPEndPoint = new IPEndPoint(IPAddress.Parse(Settings.Default.Socket_Host), Settings.Default.Socket_Port);
                    using (TcpClient tcpClient = new TcpClient())
                    {
                        tcpClient.Connect(iPEndPoint);
                        using (NetworkStream networkStream = tcpClient.GetStream())
                        {
                            byte[] Bytes = new byte[4096];
                            Bytes = Encoding.UTF8.GetBytes(Text);
                            networkStream.Write(Bytes, 0, Bytes.Length);
                        }
                    }
                    ExeLog($"[SendSocket]Socket送信成功");
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"Socket送信に失敗しました。わからない場合エラーログの内容を報告してください。内容:{ex.Message}";
                    LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main,Socket Version:{Version}\n{ex}");
                }
        }
        /// <summary>
        /// 棒読みちゃんに読み上げ指令を送ります。
        /// </summary>
        /// <param name="Text">読み上げさせる文。</param>
        public void Bouyomichan(string Text)
        {
            if (NoFirst)
                try
                {
                    ExeLog($"[Bouyomichan]棒読みちゃん送信中…");
                    byte[] Message = Encoding.UTF8.GetBytes(Text);
                    int Length = Message.Length;
                    byte Code = 0;
                    short Command = 0x0001;
                    short Speed = Settings.Default.Bouyomichan_Speed;
                    short Tone = Settings.Default.Bouyomichan_Tone;
                    short Volume = Settings.Default.Bouyomichan_Volume;
                    short Voice = Settings.Default.Bouyomichan_Voice;
                    using (TcpClient TcpClient = new TcpClient(Settings.Default.Bouyomichan_Host, Settings.Default.Bouyomichan_Port))
                    using (NetworkStream NetworkStream = TcpClient.GetStream())
                    using (BinaryWriter BinaryWriter = new BinaryWriter(NetworkStream))
                    {
                        BinaryWriter.Write(Command);
                        BinaryWriter.Write(Speed);
                        BinaryWriter.Write(Tone);
                        BinaryWriter.Write(Volume);
                        BinaryWriter.Write(Voice);
                        BinaryWriter.Write(Code);
                        BinaryWriter.Write(Length);
                        BinaryWriter.Write(Message);
                    }
                    ExeLog($"[Bouyomichan]棒読みちゃん送信成功");
                }
                catch (Exception ex)
                {
                    ErrorText.Text = $"棒読みちゃんへの送信に失敗しました。わからない場合エラーログの内容を報告してください。内容:{ex.Message}";
                    LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main,Bouyomichan Version:{Version}\n{ex}");
                }
        }
        /// <summary>
        /// 実行ログを保存・表示します。
        /// </summary>
        /// <param name="Text">保存するテキスト。</param>
        /// <remarks>タイムスタンプは自動で追加されます。</remarks>
        public static void ExeLog(string Text)
        {
            if (Settings.Default.Log_Enable)
                ExeLogs += $"{DateTime.Now:HH:mm:ss.ffff} {Text}\n";
            Console.WriteLine(Text);
        }
        /// <summary>
        /// 設定を読み込みます。
        /// </summary>
        /// <remarks>即時サイズ変更を行います。</remarks>
        public void SettingReload()
        {
            ExeLog($"[SettingReload]設定読み込み開始");
            Settings.Default.Reload();
            Configuration Config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
            if (File.Exists(Config.FilePath))
                File.Copy(Config.FilePath, "UserSetting.xml", true);
            if (Settings.Default.Display_HideHistory)
                if (Settings.Default.Display_HideHistoryMap)
                    ClientSize = new Size(400, 100);
                else
                    ClientSize = new Size(400, 500);
            else
                ClientSize = new Size(800, 500);
            ExeLogAutoDelete.Interval = Settings.Default.Log_DeleteTime * 1000;
            ExeLog($"[SettingReload]設定読み込み終了");
        }
        public static SoundPlayer Player = null;
        /// <summary>
        /// 音声を再生します。
        /// </summary>
        /// <param name="SoundFile">再生するSoundフォルダの中の音声ファイル。</param>
        public static void Sound(string SoundFile)
        {
            if (NoFirst)
                try
                {
                    ExeLog($"[Sound]音声再生開始(Sound\\{SoundFile})");
                    if (Player != null)
                    {
                        Player.Stop();
                        Player.Dispose();
                        Player = null;
                    }
                    Player = new SoundPlayer($"Sound\\{SoundFile}");
                    Player.Play();
                    ExeLog($"[Sound]音声再生成功");
                }
                catch (Exception ex)
                {
                    LogSave("Log\\Error", $"Time:{DateTime.Now:yyyy/MM/dd HH:mm:ss} Location:Main,Sound Version:{Version}\n{ex}");
                }
        }
        private void RCsetting_Click(object sender, EventArgs e)
        {
            ExeLog($"[RC]設定表示");
            SettingsForm Settings = new SettingsForm();
            Settings.FormClosed += SettingForm_FormClosed;//閉じたとき呼び出し
            Settings.Show();
        }
        private void SettingForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            ExeLog($"[RC]設定終了");
            SettingReload();
            NoFirst = false;//処理量増加時用
            ErrorText.Text = "設定を再読み込みしました。一部の設定は情報受信または再起動が必要です。";
        }
        private void RCusgsmap_Click(object sender, EventArgs e)
        {
            Process.Start("https://earthquake.usgs.gov/earthquakes/map/");
        }
        private void RCusgsthis_Click(object sender, EventArgs e)
        {
            Process.Start(LatestURL);
        }
        private void RCreboot_Click(object sender, EventArgs e)
        {
            Application.Restart();
        }
        private void RCexit_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }
        private void RCgithub_Click(object sender, EventArgs e)
        {
            Process.Start("https://github.com/Ichihai1415/WorldQuakeViewer");
        }
        private void RCtwitter_Click(object sender, EventArgs e)
        {
            Process.Start("https://twitter.com/ProjectS31415_1");
        }
        private void RCtsunami_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.tsunami.gov/");
        }
        private void RCinfopage_Click(object sender, EventArgs e)
        {
            Process.Start("https://Ichihai1415.github.io/programs/released/WQV");
        }
        private void RCMapEWSC_Click(object sender, EventArgs e)
        {
            Process.Start("https://www.emsc-csem.org/#2w");
        }
        private void RCEarlyEst_Click(object sender, EventArgs e)
        {
            Process.Start("http://early-est.rm.ingv.it/warning.html");
        }
        private void MainForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            ExeLog($"[Main]実行終了");
            LogSave("Log", ExeLogs);
        }
        private void RC1CacheClear_Click(object sender, EventArgs e)
        {
            DialogResult allow = MessageBox.Show("動作ログ、地震ログを消去してよろしいですか？消去した場合処理は起動時と同じようになります。", "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
            if (allow == DialogResult.Cancel)
                return;
            ExeLogs = "";
            USGSHist = new Dictionary<string, History>();
            NoFirst = false;
        }

        private void RC1ExeLogOpen_Click(object sender, EventArgs e)
        {
            if (Settings.Default.Log_Enable)
            {
                LogSave("Log", ExeLogs);
                Process.Start("notepad.exe", "Log\\log.txt");
            }
            else
            {
                DialogResult dr = MessageBox.Show("動作ログの保存がオフになっています。有効にしますか？", "確認", MessageBoxButtons.OKCancel, MessageBoxIcon.Question);
                if (dr == DialogResult.OK)
                {
                    Settings.Default.Log_Enable = true;
                    Settings.Default.Save();
                    Configuration Config = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.PerUserRoamingAndLocal);
                    File.Copy(Config.FilePath, "UserSetting.xml", true);
                    ExeLog($"[RC]動作ログの保存をオンにしました");
                }
            }
        }

        private void ExeLogAutoDelete_Tick(object sender, EventArgs e)//規定は3600000ms(1h)
        {
            ExeLogs = "";
        }

        private void RC1IntConvert_Click(object sender, EventArgs e)
        {
            IntConvert intConverter = new IntConvert();
            intConverter.Show();
        }

        private void RC1TextCopy_Click(object sender, EventArgs e)
        {
            Clipboard.SetText(LatestUSGSText);
        }

        public static Brush Mag2Brush(double Mag)
        {
            if (Mag < 6)
                return Brushes.White;
            else if (Mag < 8)
                return Brushes.Yellow;
            else
                return Brushes.Red;
        }
        public static Color Alert2Color(string Alert)
        {
            switch (Alert)
            {
                case "green":
                    return Color.Green;
                case "yellow":
                    return Color.Yellow;
                case "orange":
                    return Color.Orange;
                case "red":
                    return Color.Red;
                case "pending":
                    return Color.DimGray;
                default:
                    return Color.FromArgb(45, 45, 90);
            }
        }
        public static void Lat2String(double Lat, out string LatStLongJP, out string LatDisplay)
        {
            double LatShort = Math.Round(Lat, 2, MidpointRounding.AwayFromZero);
            string LatStDecimal = Lat > 0 ? $"{LatShort}°N" : $"{-LatShort}°S";
            TimeSpan LatTime = TimeSpan.FromHours(Lat);
            string LatStShort = Lat > 0 ? $"{(int)Lat}ﾟ{LatTime.Minutes}'N" : $"{(int)-Lat}ﾟ{-LatTime.Minutes}'S";
            LatStLongJP = Settings.Default.Text_LatLonDecimal ? Lat > 0 ? $"北緯{Lat}度" : $"南緯{-Lat}度" : Lat > 0 ? $"北緯{(int)Lat}度{LatTime.Minutes}分{LatTime.Seconds}秒" : $"南緯{(int)-Lat}度{-LatTime.Minutes}分{-LatTime.Seconds}秒";
            LatDisplay = Settings.Default.Text_LatLonDecimal ? LatStDecimal : LatStShort;
        }
        public static void Lat2String(double Lat, out double LatShort, out string LatStDecimal, out string LatStShort, out string LatStLong, out string LatStLongJP, out string LatDisplay)
        {
            //Lat2String(Lat, out double LatShort, out string LatStDecimal, out string LatStShort, out string LatStLong, out string LatStLongJP, out string LatDisplay);
            LatShort = Math.Round(Lat, 2, MidpointRounding.AwayFromZero);
            LatStDecimal = Lat > 0 ? $"{LatShort}°N" : $"{-LatShort}°S";
            TimeSpan LatTime = TimeSpan.FromHours(Lat);
            LatStShort = Lat > 0 ? $"{(int)Lat}ﾟ{LatTime.Minutes}'N" : $"{(int)-Lat}ﾟ{-LatTime.Minutes}'S";
            LatStLong = Lat > 0 ? $"{(int)Lat}ﾟ{LatTime.Minutes}'{LatTime.Seconds}\"N" : $"{(int)-Lat} ﾟ {-LatTime.Minutes} '";
            LatStLongJP = Settings.Default.Text_LatLonDecimal ? Lat > 0 ? $"北緯{Lat}度" : $"南緯{-Lat}度" : Lat > 0 ? $"北緯{(int)Lat}度{LatTime.Minutes}分{LatTime.Seconds}秒" : $"南緯{(int)-Lat}度{-LatTime.Minutes}分{-LatTime.Seconds}秒";
            LatDisplay = Settings.Default.Text_LatLonDecimal ? LatStDecimal : LatStShort;
        }
        public static void Lon2String(double Lon, out string LonStLongJP, out string LonDisplay)
        {
            double LonShort = Math.Round(Lon, 2, MidpointRounding.AwayFromZero);
            string LonStDecimal = Lon > 0 ? $"{LonShort}°E" : $"{-LonShort}°W";
            TimeSpan LonTime = TimeSpan.FromHours(Lon);
            string LonStShort = Lon > 0 ? $"{(int)Lon}ﾟ{LonTime.Minutes}'E" : $"{(int)-Lon}ﾟ{-LonTime.Minutes}'W";
            LonStLongJP = Settings.Default.Text_LatLonDecimal ? Lon > 0 ? $"東経{Lon}度" : $"西経{-Lon}度" : Lon > 0 ? $"東経{(int)Lon}度{LonTime.Minutes}分{LonTime.Seconds}秒" : $"西経{(int)-Lon}度{-LonTime.Minutes}分{-LonTime.Seconds}秒";
            LonDisplay = Settings.Default.Text_LatLonDecimal ? LonStDecimal : LonStShort;
        }
        public static void Lon2String(double Lon, out double LonShort, out string LonStDecimal, out string LonStShort, out string LonStLong, out string LonStLongJP, out string LonDisplay)
        {
            //Lon2String(Lon, out double LonShort, out string LonStDecimal, out string LonStShort, out string LonStLong, out string LonStLongJP, out string LonDisplay);
            LonShort = Math.Round(Lon, 2, MidpointRounding.AwayFromZero);
            LonStDecimal = Lon > 0 ? $"{LonShort}°E" : $"{-LonShort}°W";
            TimeSpan LonTime = TimeSpan.FromHours(Lon);
            LonStShort = Lon > 0 ? $"{(int)Lon}ﾟ{LonTime.Minutes}'E" : $"{(int)-Lon}ﾟ{-LonTime.Minutes}'W";
            LonStLong = Lon > 0 ? $"{(int)Lon}ﾟ{LonTime.Minutes}'{LonTime.Seconds}\"E" : $"{(int)-Lon} ﾟ {-LonTime.Minutes} '";
            LonStLongJP = Settings.Default.Text_LatLonDecimal ? Lon > 0 ? $"東経{Lon}度" : $"西経{-Lon}度" : Lon > 0 ? $"東経{(int)Lon}度{LonTime.Minutes}分{LonTime.Seconds}秒" : $"西経{(int)-Lon}度{-LonTime.Minutes}分{-LonTime.Seconds}秒";
            LonDisplay = Settings.Default.Text_LatLonDecimal ? LonStDecimal : LonStShort;
        }
        public static void ImageCheck(string FileName)
        {
            if (!Directory.Exists("Image"))
            {
                Directory.CreateDirectory("Image");
                ExeLog($"[ImageCheck]Imageフォルダを作成しました");
            }
            if (!File.Exists($"Image\\{FileName}"))
            {
                Bitmap image;
                switch (FileName)
                {
                    case "map.png":
                        image = Resources.map;
                        break;
                    case "hypo.png":
                        image = Resources.hypo;
                        break;
                    default:
                        throw new Exception("画像のコピーに失敗しました。", new ArgumentException($"指定された画像({FileName})はResourcesにありません。"));
                }
                image.Save($"Image\\{FileName}", ImageFormat.Png);
                ExeLog($"[ImageCheck]画像(\"Image\\{FileName}\")をコピーしました");
            }
        }
    }
}
