////////////////////////////////////////////////////////////////////////////////
//プログラム名      ：Web更新チェック
//作成者            ：久保　由仁
//作成日            ：2013.10.22
//コンパイル        ：csc WebAnalyzer.cs
////////////////////////////////////////////////////////////////////////////////
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;
////////////////////////////////////////////////////////////////////////////////
enum MessageLevel{DEBUG, INFO, WARN, ERROR}
////////////////////////////////////////////////////////////////////////////////
public class WebAnalyzer
{
    private ILogger m_logger = null;//ロガー
    private bool m_Changed = false;
    //--------------------------------------------------------------------------
    public static void Main(string[] args)
    {
        WebAnalyzer application = new WebAnalyzer();
        application.Run(args);
    }
    //--------------------------------------------------------------------------
    private void Run(string[] args)
    {
        try
        {
            Console.WriteLine("WebAnalyzer START");
            Config conf = new Config();
            string logPath = conf.GetSetting("/target/log");
            if(logPath.Length == 0)
            {    //ログファイルが指定されていなかった場合
                m_logger = new ConsoleLogger();
            }
            else
            {    //ログファイルが指定されていた場合
                FileLogger fileLogger = FileLogger.Instance;
                fileLogger.Open(logPath);
                #if DEBUG
                fileLogger.Level = MessageLevel.DEBUG;
                #else
                fileLogger.Level = MessageLevel.INFO;
                #endif
                m_logger = fileLogger;
            }
            m_logger.Info("Web更新チェック開始");
            List<WebSite> list = this.GetWebsiteInfo(conf);
            string tablePath = conf.GetSetting("/target/table");
            UpdateCheck(list, tablePath);
            if(m_Changed)
            {
                Console.WriteLine("");
                MessageBox.Show("Webサイト変更を検出しました。");
                Console.WriteLine("");
            }
            m_logger.Info("Web更新チェック終了");
            Console.WriteLine("WebAnalyzer END");
        }
        catch(Exception ex)
        {
            MessageBox.Show(ex.ToString());
            if(m_logger != null)
            {
                m_logger.Error(ex.ToString());
            }
            Console.WriteLine(ex.ToString());
        }
        finally
        {
            if(m_logger != null)
            {
                m_logger.Close();
            }
        }
    }
    //--------------------------------------------------------------------------
    private void UpdateCheck(List<WebSite> list, string TablePath)
    {    //Webサイトの更新有無をチェックする
        Dictionary<string, WebSite> dic = null;
        IFormatter formatter = new BinaryFormatter();
        if(File.Exists(TablePath))
        {    //前回のチェック結果ファイルが存在した場合
            Stream sIn = new FileStream(TablePath, FileMode.Open, 
                                            FileAccess.Read, FileShare.Read);
            dic = (Dictionary<string, WebSite>)formatter.Deserialize(sIn);
            sIn.Close();
        }
        else
        {    //前回チェック結果ファイルが存在しなかった場合
            dic = new Dictionary<string, WebSite>();
        }
        //Webサイトを巡回
        foreach(WebSite webSite in list)
        {
            Console.WriteLine(webSite.url );
            string hashValue = webSite.GetHashOfContens();
            if(dic.ContainsKey(webSite.url))
            {
                if(dic[webSite.url].hash != hashValue)
                {
                    m_Changed = true;
                    StringBuilder sb = new StringBuilder();
                    sb.Append("[変更] ");
                    sb.Append(webSite.title);
                    sb.Append("(" + webSite.url+ ")");
                    m_logger.Info(sb.ToString());
                    webSite.hash = hashValue;
                    dic[webSite.url] = webSite;
                }
            }
            else
            {
                webSite.hash = hashValue;
                dic[webSite.url] = webSite;
                m_logger.Debug("[追加] " + webSite.url);
            }
        }
        //調査結果をファイルに保存
        Stream sOut = new FileStream(TablePath, FileMode.Create, 
                                        FileAccess.Write, FileShare.None);
        formatter.Serialize(sOut, dic);
        sOut.Close();
    }
    //--------------------------------------------------------------------------
    private List<WebSite> GetWebsiteInfo(Config conf)
    {    //Webサイトの情報を設定ファイルから読み込む
        List<WebSite> list = new List<WebSite>();
        XmlNodeList NodeList = conf.GetNodeList("/target/website");
        foreach(XmlNode Node in NodeList)
        {
            string url = Config.GetInnerText(Node, "url");
            string title = Config.GetInnerText(Node, "title");
            WebSite website = new WebSite(url, title);
            list.Add(website);
        }
        return list;
    }
}
////////////////////////////////////////////////////////////////////////////////
//設定情報クラス
////////////////////////////////////////////////////////////////////////////////
class Config
{
    private const string XMLPATH = "target.xml";
    private XmlDocument m_xmlConfig = new XmlDocument();
    //--------------------------------------------------------------------------
    public Config()
    {
        m_xmlConfig.Load(XMLPATH);
    }
    //--------------------------------------------------------------------------
    public string GetSetting(string xpath)
    {    //xpathで指定されたノードのテキストを返す
        XmlNode node = m_xmlConfig.SelectSingleNode(xpath);
        return (node != null) ? node.InnerText : "";
    }
    //--------------------------------------------------------------------------
    public XmlNodeList GetNodeList(string xpath)
    {    //xpathで指定されたノードリストを返す
        XmlNodeList NodeList = m_xmlConfig.SelectNodes(xpath);
        return NodeList;
    }
    //--------------------------------------------------------------------------
    public static String GetInnerText(XmlNode node, string xpath)
    {
        XmlNode Node = node.SelectSingleNode(xpath);
        return (node != null) ? Node.InnerText : "";
    }
}
////////////////////////////////////////////////////////////////////////////////
//Webサイトクラス
////////////////////////////////////////////////////////////////////////////////
[Serializable]
class WebSite
{
    public string url{get; set;}//URL
    public string title{get; set;}//タイトル
    public string hash{get; set; }//ハッシュ値
    //--------------------------------------------------------------------------
    public WebSite(){}
    public WebSite(string url, string title)
    {
        this.url = url;
        this.title = title;
    }
    //--------------------------------------------------------------------------
    public string GetHashOfContens()
    {    //HTTPレスポンスのハッシュ値を返す
        try
        {
            HashAlgorithm HashFunc = SHA256Managed.Create();
            WebRequest req = WebRequest.Create(this.url);
            WebResponse rsp = req.GetResponse();
            Stream stm = rsp.GetResponseStream();
            byte[] hash = null;
            if(stm != null)
            {
                hash = HashFunc.ComputeHash(stm);
                BinaryReader reader = new BinaryReader(stm);
                reader.Close();
                stm.Close();
            }
            rsp.Close();
            StringBuilder sb = new StringBuilder();
            if(hash != null)
            {
                for(int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("X2"));
                }
            }
            return sb.ToString();
        }
        catch(WebException WebEx)
        {
            WebEx.ToString();
            return "";
        }
    }
}
////////////////////////////////////////////////////////////////////////////////
//ロガーインタフェース
////////////////////////////////////////////////////////////////////////////////
interface ILogger
{
    void Debug(string message);
    void Info(string message);
    void Warn(string message);
    void Error(string message);
    void Close();
    void Open();
}
////////////////////////////////////////////////////////////////////////////////
//コンソールロガー（暫定版）
////////////////////////////////////////////////////////////////////////////////
class ConsoleLogger : ILogger
{
    public void Open()
    {
    }
    //--------------------------------------------------------------------------
    public void Close()
    {
    }
    //--------------------------------------------------------------------------
    public void Debug(string message)
    {
        this.Info(message);
    }
    //--------------------------------------------------------------------------
    public void Info(string message)
    {
        String datetime = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");
        System.Console.WriteLine("[" + datetime + "]" + message);
    }
    //--------------------------------------------------------------------------
    public void Warn(string message)
    {
        this.Info(message);
    }
    //--------------------------------------------------------------------------
    public void Error(string message)
    {
        this.Info(message);
    }
}
////////////////////////////////////////////////////////////////////////////////
//ファイルロガー
////////////////////////////////////////////////////////////////////////////////
class FileLogger : ILogger
{
    private StreamWriter m_sw = null;
    private static FileLogger instance = new FileLogger();
    private string m_Path = "";//ログファイルのパス
    public MessageLevel Level{get; set; }//出力レベル
    //--------------------------------------------------------------------------
    private FileLogger(){}//コンストラクタ（無効化）
    //--------------------------------------------------------------------------
    public string Path
    {
        get
        {
            return m_Path;
        }
        set
        {
            string strPath = value;
            string datetime = DateTime.Now.ToString("yyyyMMddHHmmss");
            m_Path = strPath.Replace("{0}", datetime);
        }
    }
    //--------------------------------------------------------------------------
    public static FileLogger Instance
    {    //インスタンス取得
        get
        {
            return instance;
        }
    }
    //--------------------------------------------------------------------------
    public void Open()
    {
        m_sw = new StreamWriter(this.Path, false, Encoding.GetEncoding("UTF-8"));
    }
    //--------------------------------------------------------------------------
    public void Open(string path)
    {
        this.Path = path;
        this.Open();
    }
    //--------------------------------------------------------------------------
    public void Close()
    {
        m_sw.Close();
        m_sw = null;
    }
    //--------------------------------------------------------------------------
    public void Debug(string message)
    {
        if(Level == MessageLevel.DEBUG)
        {
            this.WriteLine("[DEBUG]" + message);
        }
    }
    //--------------------------------------------------------------------------
    public void Info(string message)
    {
        if(Level == MessageLevel.DEBUG 
            || Level == MessageLevel.INFO)
        {
            this.WriteLine("[INFO]" + message);
        }
    }
    //--------------------------------------------------------------------------
    public void Warn(string message)
    {
        if(Level == MessageLevel.DEBUG
           || Level == MessageLevel.INFO
           || Level == MessageLevel.WARN)
        {
            this.WriteLine("[WARN]" + message);
        }
    }
    //--------------------------------------------------------------------------
    public void Error(string message)
    {
        this.WriteLine("[ERROR]" + message);
    }
    //--------------------------------------------------------------------------
    private void WriteLine(string message)
    {
        string now = DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss.fff");
        m_sw.WriteLine("[" + now + "]" + message);
        m_sw.Flush();
    }
}
////////////////////////////////////////////////////////////////////////////////
