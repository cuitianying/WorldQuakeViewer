﻿using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Drawing;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using static LL2FERC.LL2FERC;
using static WorldQuakeViewer.CtrlForm;
using static WorldQuakeViewer.Util_Class;
using static WorldQuakeViewer.Util_Func;

namespace WorldQuakeViewer
{
    /// <summary>
    /// 色々
    /// </summary>
    public class Util_Conv
    {
        /// <summary>
        /// マグニチュードからレベルに変換します。
        /// </summary>
        /// <param name="mag">マグニチュード</param>
        /// <returns>レベル(<M4.5<M6.0<M7.0<M8.0<)</returns>
        public static int Mag2Level(double mag)
        {
            if (mag < 4.5)
                return 1;
            else if (mag < 6.0)
                return 2;
            else if (mag < 7.0)
                return 3;
            else if (mag < 8.0)
                return 4;
            else
                return 5;
        }

        /// <summary>
        /// データから各処理の文字列に変換します。
        /// </summary>
        /// <param name="data">データ</param>
        /// <param name="updatePros">フォーマット置換処理名</param>
        /// <param name="isNew">新規か</param>
        /// <param name="viewIndex">表示用置換の場合表示インデックス</param>
        /// <returns>各処理の文字列</returns>
        public static string Data2String(Data data, FormatPros updatePros, bool isNew, int viewIndex = 0)
        {
            string format;
            switch (updatePros)
            {
                case FormatPros.View:
                    if (viewIndex == 0)
                        throw new ArgumentException($"表示インデックス({viewIndex})が不正です。", nameof(viewIndex));
                    else if (viewIndex == -1)
                        format = pastConfig.View.DisplayTextFormat;
                    else
                        format = config.Views[viewIndex].DisplayTextFormat;
                    break;
                case FormatPros.Bouyomichan:
                    format = config.Datas[(int)data.Author].Bouyomi.Format;
                    break;
                case FormatPros.Socket:
                    format = config.Datas[(int)data.Author].Socket.Format;
                    break;
                case FormatPros.Webhook:
                    format = config.Datas[(int)data.Author].Webhook.Format;
                    break;
                case FormatPros.LogE:
                    format = config.Datas[(int)data.Author].LogE.Format;
                    break;
                default:
                    throw new ArgumentException($"更新処理名({updatePros})が不正です。", nameof(updatePros));
            }
            DateTimeOffset timeUser = data.Time.ToLocalTime();
            Lat2String(data.Lat, out string lat10, out string latNS, out string latNSJP, out string lat60d, out string lat60m, out string lat60s);
            Lon2String(data.Lon, out string lon10, out string lonEW, out string lonEWJP, out string lon60d, out string lon60m, out string lon60s);
            FormatReplaces f = new FormatReplaces
            {
                Author = data.Author.ToString(),
                ID = data.ID2,
                HypoJP = NameJP(data.Lat, data.Lon),
                HypoEN = NameEN(data.Lat, data.Lon),
                Lat10 = lat10,
                LatNS = latNS,
                LatNSJP = latNSJP,
                Lat60d = lat60d,
                Lat60m = lat60m,
                Lat60s = lat60s,
                Lon10 = lon10,
                LonEW = lonEW,
                LonEWJP = lonEWJP,
                Lon60d = lon60d,
                Lon60m = lon60m,
                Lon60s = lon60s,
                Depth = data.Depth.ToString(),
                MagType = data.MagType,
                Mag = data.Mag.ToString("0.0######"),
                MMI = data.MMI == null ? "" : data.MMI.ToString(),
                MMIAra = MMI2Ara(data.MMI),
                AlertJP = Alert2JP(data.Alert),
                AlertEN = data.Alert ?? "",
                Source = data.Source,
                UpdateJP = isNew ? "" : "更新",
                UpdateEN = isNew ? "" : "update",
            };

            foreach (Match match in Regex.Matches(format, "\\{.*?\\}"))
            {
                //Console.WriteLine(match.Value);
                string[] texts = match.Value.Replace("{", "").Replace("}", "").Split('*');
                if (texts.Count() == 2)
                {
                    switch (texts[0])
                    {
                        case "TimeUTC":
                            format = format.Replace(match.Value, data.Time.ToString(texts[1]));
                            continue;
                        case "TimeUser":
                            format = format.Replace(match.Value, timeUser.ToString(texts[1]));
                            continue;
                        case "MMI":
                            format = data.MMI == null ? format.Replace(match.Value, "") : format.Replace(match.Value, texts[1]);
                            continue;
                        case "Alert":
                            format = NoValueCheck(data.Alert) ? format.Replace(match.Value, "") : format.Replace(match.Value, texts[1]);
                            continue;
                        case "Source":
                            format = NoValueCheck(data.Source) ? format.Replace(match.Value, "") : format.Replace(match.Value, texts[1]);
                            continue;
                    }
                }
                ExeLog($"[Data2String]警告:パターンが処理できません。(値:{match.Value})。設定を確認してください。", true);
            }

            //.Replace("[]",f.)
            format = format
                .Replace("\\n", "\n")
                .Replace("[Author]", f.Author).Replace("[ID]", f.ID)
                .Replace("[HypoJP]", f.HypoJP).Replace("[HypoEN]", f.HypoEN)
                .Replace("[Lat10]", f.Lat10).Replace("[LatNS]", f.LatNS).Replace("[LatNSJP]", f.LatNSJP)
                .Replace("[Lat60d]", f.Lat60d).Replace("[Lat60m]", f.Lat60m).Replace("[Lat60s]", f.Lat60s)
                .Replace("[Lon10]", f.Lon10).Replace("[LonEW]", f.LonEW).Replace("[LonEWJP]", f.LonEWJP)
                .Replace("[Lon60d]", f.Lon60d).Replace("[Lon60m]", f.Lon60m).Replace("[Lon60s]", f.Lon60s)
                .Replace("[Depth]", f.Depth)
                .Replace("[MagType]", f.MagType).Replace("[Mag]", f.Mag)
                .Replace("[MMI]", f.MMI).Replace("[MMIAra]", f.MMIAra)
                .Replace("[AlertJP]", f.AlertJP).Replace("[AlertEN]", f.AlertEN)
                .Replace("[Source]", f.Source)
                .Replace("[UpdateJP]", f.UpdateJP).Replace("[UpdateEN]", f.UpdateEN)
                .Replace("[dataJSON]", JsonConvert.SerializeObject(data))
                .Replace("[formatJSON]", JsonConvert.SerializeObject(f));
            foreach (Config.Data_.LogE_.TextReplace_ replace in config.Datas[(int)data.Author].LogE.TextReplace)
                if (replace.OldValue != "")
                    format = format.Replace(replace.OldValue, replace.NewValue);
            return format;
        }

        /// <summary>
        /// 緯度を各stringに変換します。
        /// </summary>
        /// <param name="lat">緯度</param>
        /// <param name="lat10">緯度(十進数)</param>
        /// <param name="latNS">NまたはS</param>
        /// <param name="latNSJP">北緯または南緯</param>
        /// <param name="lat60d">緯度(六十進数・度)</param>
        /// <param name="lat60m">緯度(六十進数・分)</param>
        /// <param name="lat60s">緯度(六十進数・秒)</param>
        public static void Lat2String(double lat, out string lat10, out string latNS, out string latNSJP, out string lat60d, out string lat60m, out string lat60s)
        {
            lat10 = lat.ToString();
            latNS = lat >= 0 ? "N" : "S";
            latNSJP = lat >= 0 ? "北緯" : "南緯";
            lat = Math.Abs(lat);
            double deg = Math.Floor(lat);
            double min = Math.Floor((lat - deg) * 60);
            double sec = Math.Floor((lat - deg - min / 60) * 3600);
            lat60d = deg.ToString();
            lat60m = min.ToString();
            lat60s = sec.ToString();
        }

        /// <summary>
        /// 経度を各stringに変換します。
        /// </summary>
        /// <param name="lon">経度</param>
        /// <param name="lon10">経度(十進数)</param>
        /// <param name="lonEW">NまたはS</param>
        /// <param name="lonEWJP">東経または西経</param>
        /// <param name="lon60d">経度(六十進数・度)</param>
        /// <param name="lon60m">経度(六十進数・分)</param>
        /// <param name="lon60s">経度(六十進数・秒)</param>
        public static void Lon2String(double lon, out string lon10, out string lonEW, out string lonEWJP, out string lon60d, out string lon60m, out string lon60s)
        {
            lon10 = lon.ToString();
            lonEW = lon >= 0 ? "E" : "W";
            lonEWJP = lon >= 0 ? "東経" : "西経";
            lon = Math.Abs(lon);
            double deg = Math.Floor(lon);
            double min = Math.Floor((lon - deg) * 60);
            double sec = Math.Floor((lon - deg - min / 60) * 3600);
            lon60d = deg.ToString();
            lon60m = min.ToString();
            lon60s = sec.ToString();
        }

        /// <summary>
        /// MMIのアラビア数字を求めます。
        /// </summary>
        /// <param name="mmi">改正メルカリ震度階級</param>
        /// <returns>MMIのアラビア数字</returns>
        public static string MMI2Ara(double? mmi)
        {
            return mmi < 1.5 ? "I" : mmi < 2.5 ? "II" : mmi < 3.5 ? "III" : mmi < 4.5 ? "IV" : mmi < 5.5 ? "V" : mmi < 6.5 ? "VI" : mmi < 7.5 ? "VII" : mmi < 8.5 ? "VIII" : mmi < 9.5 ? "IX" : mmi < 10.5 ? "X" : mmi < 11.5 ? "XI" : mmi >= 11.5 ? "XII" : "-";
        }

        /// <summary>
        /// 英語のアラートを日本語に変換します。
        /// </summary>
        /// <param name="alert">アラート(英語)</param>
        /// <returns>アラート(日本語)</returns>
        public static string Alert2JP(string alert)
        {
            switch (alert)
            {
                case "green":
                    return "緑";
                case "yellow":
                    return "黄";
                case "orange":
                    return "橙";
                case "red":
                    return "赤";
                case "pending":
                    return "保留中";
                default:
                    return "-";
            }
        }

        /// <summary>
        /// テキスト形式からリスト形式に変換します。
        /// </summary>
        /// <param name="info">変換元</param>
        /// <param name="dataAuthor">データ元</param>
        /// <returns>リスト形式のデータ</returns>
        public static Data Text2Data(string[] info, DataAuthor dataAuthor)
        {
            return new Data
            {
                Author = dataAuthor,
                ID = info[0],
                ID2 = info[8],
                Time = DateTimeOffset.Parse($"{info[1]}Z".Replace("ZZ", "Z")),//Zがついていないものがある　つけないとLocalTime判定になる
                Hypo = NameJP(double.Parse(info[2]), double.Parse(info[3])),
                Lat = double.Parse(info[2]),
                Lon = double.Parse(info[3]),
                Depth = double.Parse(info[4]),
                MagType = info[9],
                Mag = double.Parse(info[10]),
                Source = info[7],
            };
        }

        /// <summary>
        /// QuakeML形式からリスト形式に変換します。
        /// </summary>
        /// <remarks>EMSCの場合ID2を確認</remarks>
        /// <param name="info">変換元</param>
        /// <param name="ns">xmlの名前空間</param>
        /// <param name="dataAuthor">データ元</param>
        /// <returns>リスト形式のデータ</returns>
        public static Data QuakeML2Data(XmlNode info, XmlNamespaceManager ns, DataAuthor dataAuthor)
        {
            XmlNode origin = info.SelectSingleNode("qml:origin", ns);
            XmlNodeList magnitudes = info.SelectNodes("qml:magnitude", ns);
            if (magnitudes.Count == 0)
                return null;
            string id = ((XmlElement)info).GetAttribute("publicID").Split('/').Last().Replace(".quakeml", "");//USGSには.quakemlが付く
            int magIndex = -1;
            for (int i = magnitudes.Count - 1; i >= 0; i--)
            {
                if (magnitudes[i].SelectSingleNode("qml:stationCount", ns) == null || int.Parse(magnitudes[i].SelectSingleNode("qml:stationCount", ns).InnerText) >= config.Datas[(int)dataAuthor].MinObsPoints)
                {//EMSCはMが一定以上だとstationCountがない?
                    magIndex = i;
                    break;
                }
            }
            if (magIndex == -1)
                return null;
            return new Data
            {
                Author = dataAuthor,
                ID = id,
                ID2 = id,
                Time = DateTimeOffset.Parse($"{origin.SelectSingleNode("qml:time/qml:value", ns).InnerText}Z".Replace("ZZ", "Z")),
                UpdtTime = info.SelectSingleNode("qml:creationInfo/qml:creationTime", ns) != null ? DateTimeOffset.Parse($"{info.SelectSingleNode("qml:creationInfo/qml:creationTime", ns).InnerText}Z".Replace("ZZ", "Z")) : DateTimeOffset.MinValue,
                Hypo = NameJP(double.Parse(origin.SelectSingleNode("qml:latitude/qml:value", ns).InnerText), double.Parse(origin.SelectSingleNode("qml:longitude/qml:value", ns).InnerText)),
                Lat = double.Parse(origin.SelectSingleNode("qml:latitude/qml:value", ns).InnerText),
                Lon = double.Parse(origin.SelectSingleNode("qml:longitude/qml:value", ns).InnerText),
                Depth = double.Parse(origin.SelectSingleNode("qml:depth/qml:value", ns).InnerText) / 1000d,
                MagType = magnitudes[magIndex].SelectSingleNode("qml:type", ns).InnerText,
                Mag = double.Parse(magnitudes[magIndex].SelectSingleNode("qml:mag/qml:value", ns).InnerText)
            };
        }

        /// <summary>
        /// GeoJSON形式からリスト形式に変換します。
        /// </summary>
        /// <param name="feature">変換元(feature)</param>
        /// <param name="dataAuthor">データ元</param>
        /// <returns>リスト形式のデータ</returns>
        public static Data GeoJSON2Data(JToken feature, DataAuthor dataAuthor)
        {
            JToken properties = feature.SelectToken("properties");
            switch (dataAuthor)
            {
                case DataAuthor.USGS:
                    return new Data
                    {
                        Author = dataAuthor,
                        ID = (string)feature.SelectToken("id"),
                        ID2 = (string)feature.SelectToken("id"),
                        Time = DateTimeOffset.FromUnixTimeMilliseconds((long)properties.SelectToken("time")),
                        UpdtTime = DateTimeOffset.FromUnixTimeMilliseconds((long)properties.SelectToken("updated")),
                        Hypo = NameJP((double)feature.SelectToken("geometry.coordinates[1]"), (double)feature.SelectToken("geometry.coordinates[0]")),
                        Lat = (double)feature.SelectToken("geometry.coordinates[1]"),
                        Lon = (double)feature.SelectToken("geometry.coordinates[0]"),
                        Depth = (double)feature.SelectToken("geometry.coordinates[2]"),
                        MagType = (string)properties.SelectToken("magType"),
                        Mag = (double)properties.SelectToken("mag"),
                        MMI = (double?)properties.SelectToken("mmi"),
                        Alert = (string)properties.SelectToken("alert")
                    };
                case DataAuthor.EMSC:
                    return new Data
                    {
                        Author = dataAuthor,
                        ID = (string)properties.SelectToken("source_id"),
                        ID2 = (string)properties.SelectToken("source_id"),
                        Time = DateTimeOffset.Parse((string)properties.SelectToken("time")),//Zが付いてるはず
                        UpdtTime = DateTimeOffset.Parse((string)properties.SelectToken("lastupdate")),
                        Hypo = NameJP((double)properties.SelectToken("lat"), (double)properties.SelectToken("lon")),
                        Lat = (double)properties.SelectToken("lat"),
                        Lon = (double)properties.SelectToken("lon"),
                        Depth = (double)properties.SelectToken("depth"),
                        MagType = (string)properties.SelectToken("magtype"),
                        Mag = (double)properties.SelectToken("mag"),
                        Source = (string)properties.SelectToken("auth")
                    };
                case DataAuthor.Other:
                    throw new ArgumentException($"未対応のデータ元({dataAuthor})です(USGSやEMSCのGeoJSONはここでは処理できません。別のタイプを指定してください。)。", nameof(dataAuthor));
                default:
                    throw new ArgumentException($"未対応のデータ元({dataAuthor})です。", nameof(dataAuthor));
            }
        }

        /// <summary>
        /// seismicportalのEventIDでIDを変換します。
        /// </summary>
        /// <remarks>失敗した場合元のIDを返します。</remarks>
        /// <param name="sourceID">元のID</param>
        /// <param name="oldAuthor">元のデータ元</param>
        /// <param name="newAuthor">返すデータ元</param>
        /// <returns>変換されたID</returns>
        public static async Task<string> IDconvert(string sourceID, IDauthor oldAuthor, IDauthor newAuthor)
        {
            try
            {
                ExeLog("[IDconvert]ID変換中...", true);
                string res = await client.GetStringAsync($"https://seismicportal.eu/eventid/api/convert?source_id={sourceID}&source_catalog={oldAuthor}&out_catalog={newAuthor}&format=text");
                return res.Split('\n')[1].Split('|')[3];
            }
            catch (HttpRequestException ex)
            {
                ExeLog($"[IDconvert]エラー:{ex.Message}", true);
            }
            catch (Exception ex)
            {
                ExeLog($"[IDconvert]エラー:{ex.Message}", true);
                LogSave(ex);
            }
            return sourceID;
        }

        /// <summary>
        /// 描画用マグニチュード別色
        /// </summary>
        /// <param name="mag">マグニチュード</param>
        /// <param name="latestORhistory">最新の場合1、履歴の場合2</param>
        /// <param name="viewIndex">表示インデックス</param>
        /// <returns>マグニチュード別の色</returns>
        public static Brush Mag2Brush(double mag, int latestORhistory, int viewIndex)
        {
            if (mag < 6)
            {
                switch (latestORhistory)
                {
                    case 1:
                        if (viewIndex == -1)
                            return new SolidBrush(pastConfig.View.Colors.Main_Latest_Text_Color);
                        else
                            return new SolidBrush(config.Views[viewIndex].Colors.Main_Latest_Text_Color);
                    case 2:
                        if (viewIndex == -1)
                            return new SolidBrush(pastConfig.View.Colors.Main_History_Text_Color);
                        else
                            return new SolidBrush(config.Views[viewIndex].Colors.Main_History_Text_Color);
                    default:
                        ExeLog($"[Mag2Brush]警告:latestORhistory:{latestORhistory}は不正です。", true);
                        try
                        {
                            throw new ArgumentException($"<警告>latestORhistory:{latestORhistory}は不正です。", nameof(latestORhistory));
                        }
                        catch (Exception ex)
                        {
                            LogSave(ex);
                        }
                        if (viewIndex == -1)
                            return new SolidBrush(pastConfig.View.Colors.Main_Latest_Text_Color);
                        else
                            return new SolidBrush(config.Views[viewIndex].Colors.Main_Latest_Text_Color);
                }
            }
            else if (mag < 8)
                return Brushes.Yellow;
            else
                return Brushes.Red;
        }

        /// <summary>
        /// 描画用アラート別色
        /// </summary>
        /// <param name="alert">アラート</param>
        /// <param name="latestORhistory">最新の場合1、履歴の場合2</param>
        /// <param name="viewIndex">表示インデックス</param>
        /// <returns>アラート別の色</returns>
        public static Color Alert2Color(string alert, int latestORhistory, int viewIndex)
        {
            switch (alert)
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
                case "":
                case "null":
                case null:
                    break;
                default:
                    ExeLog($"[Alert2Color]警告:alert:{alert}は未確認です。", true);
                    try
                    {
                        throw new ArgumentException($"<警告>alert:{alert}は未確認です。", nameof(alert));
                    }
                    catch (Exception ex)
                    {
                        LogSave(ex);
                    }
                    break;
            }
            switch (latestORhistory)
            {
                case 1:
                    if (viewIndex == -1)
                        return pastConfig.View.Colors.Main_Latest_Back_Color;
                    else
                        return config.Views[viewIndex].Colors.Main_Latest_Back_Color;
                case 2:
                    if (viewIndex == -1)
                        return pastConfig.View
                            .Colors.Main_History_Back_Color;
                    else
                        return config.Views[viewIndex].Colors.Main_History_Back_Color;
                default:
                    ExeLog($"[Alert2Color]警告:latestORhistory:{latestORhistory}は不正です。", true);
                    try
                    {
                        throw new ArgumentException($"<警告>latestORhistory:{latestORhistory}は不正です。", nameof(latestORhistory));
                    }
                    catch (Exception ex)
                    {
                        LogSave(ex);
                    }
                    return config.Views[viewIndex].Colors.Main_Latest_Back_Color;
            }
        }

        /// <summary>
        /// 値がないか判定します。
        /// </summary>
        /// <param name="value"></param>
        /// <returns>値がない場合True</returns>
        public static bool NoValueCheck(string value)
        {
            if (value == null || value == "" || value == "null" || value == "-")
                return true;
            return false;
        }

        /// <summary>
        /// 震度を変換します。
        /// </summary>
        /// <remarks>値の名前: 0:改正メルカリ震度階級 1:最大速度(cm/s) 2:気象庁震度階級</remarks>
        /// <param name="fromValue">元の値</param>
        /// <param name="beforeIndex">元の値の名前</param>
        /// <param name="afterIndex">変換後の値の名前</param>
        /// <returns>変換後の値(エラー時はメッセージ表示してNaNを返す)</returns>
        public static double IntConvert(double fromValue, int beforeIndex, int afterIndex)
        {
            try
            {
                if (beforeIndex == afterIndex)
                    return fromValue;
                switch (beforeIndex)
                {
                    case 0:
                        switch (afterIndex)
                        {
                            case 1:
                                return MMI2PGV(fromValue);
                            case 2:
                                return PGV2JI(MMI2PGV(fromValue));
                            default:
                                throw new ArgumentException("変換名称が不正です。", nameof(afterIndex));
                        }
                    case 1:
                        switch (afterIndex)
                        {
                            case 0:
                                return PGV2MMI(fromValue);
                            case 2:
                                return PGV2JI(fromValue);
                            default:
                                throw new ArgumentException("変換名称が不正です。", nameof(afterIndex));
                        }
                    case 2:
                        switch (afterIndex)
                        {
                            case 0:
                                return PGV2MMI(JI2PGV(fromValue));
                            case 1:
                                return JI2PGV(fromValue);
                            default:
                                throw new ArgumentException("変換名称が不正です。", nameof(afterIndex));
                        }
                    default:
                        throw new ArgumentException("変換名称が不正です。", nameof(beforeIndex));
                }
            }
            catch (Exception ex)
            {
                ExeLog($"[IntConvert]エラー:{ex.Message}", true);
                MessageBox.Show(topMost, "変換に失敗しました。値を確認してください。内容:" + ex.Message, "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            return double.NaN;
        }

        /// <summary>
        /// 改正メルカリ震度階級から最大速度を計算します。
        /// </summary>
        /// <param name="mmi">改正メルカリ震度階級</param>
        /// <returns>計算された最大速度</returns>
        public static double MMI2PGV(double mmi)
        {
            if (mmi < 4.4)
                return Math.Pow(10, (mmi - 3.5) / 1.5);
            else
                return Math.Pow(10, (mmi - 2.5) / 3.155);
        }

        /// <summary>
        /// 最大速度から気象庁震度階級を計算します。
        /// </summary>
        /// <param name="pgv">最大速度</param>
        /// <returns>計算された気象庁震度階級</returns>
        public static double PGV2JI(double pgv)
        {
            if (pgv < 7)
                return 2.165 + 2.262 * Math.Log10(pgv);
            else
                return 2.002 + 2.603 * Math.Log10(pgv) - 0.213 * Math.Pow(Math.Log10(pgv), 2);
        }

        /// <summary>
        /// 最大速度から改正メルカリ震度階級を計算します。
        /// </summary>
        /// <param name="pgv">最大速度</param>
        /// <returns>計算された改正メルカリ震度階級</returns>
        public static double PGV2MMI(double pgv)
        {
            if (pgv < 4)
                return 3.5 + 1.5 * Math.Log10(pgv);
            else
                return 2.5 + 3.155 * Math.Log10(pgv);
        }

        /// <summary>
        /// 気象庁震度階級から最大速度を計算します。
        /// </summary>
        /// <param name="ji">気象庁震度階級</param>
        /// <returns>計算された最大速度</returns>
        public static double JI2PGV(double ji)
        {
            if (ji < 4)
                return Math.Pow(10, (ji - 2.165) / 2.262);
            else//Microsoft math solverで計算
                return Math.Pow(10, -2d * Math.Sqrt(-250d * ji / 213d + 8481313d / 725904d) + 47d / 426d + 6d);
        }
    }
}
