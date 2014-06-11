////////////////////////////////////////////////////////////////////////////////
//�v���O������      �FWeb�X�V�`�F�b�N
//�쐬��            �F�v�ہ@�R�m
//�쐬��            �F2013.10.22
//�R���p�C��        �Fcsc WebAnalyzer.cs
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
    private ILogger m_logger = null;//���K�[
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
            {    //���O�t�@�C�����w�肳��Ă��Ȃ������ꍇ
                m_logger = new ConsoleLogger();
            }
            else
            {    //���O�t�@�C�����w�肳��Ă����ꍇ
                FileLogger fileLogger = FileLogger.Instance;
                fileLogger.Open(logPath);
                #if DEBUG
                fileLogger.Level = MessageLevel.DEBUG;
                #else
                fileLogger.Level = MessageLevel.INFO;
                #endif
                m_logger = fileLogger;
            }
            m_logger.Info("Web�X�V�`�F�b�N�J�n");
            List<WebSite> list = this.GetWebsiteInfo(conf);
            string tablePath = conf.GetSetting("/target/table");
            UpdateCheck(list, tablePath);
            if(m_Changed)
            {
                Console.WriteLine("");
                MessageBox.Show("Web�T�C�g�ύX�����o���܂����B");
                Console.WriteLine("");
            }
            m_logger.Info("Web�X�V�`�F�b�N�I��");
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
    {    //Web�T�C�g�̍X�V�L�����`�F�b�N����
        Dictionary<string, WebSite> dic = null;
        IFormatter formatter = new BinaryFormatter();
        if(File.Exists(TablePath))
        {    //�O��̃`�F�b�N���ʃt�@�C�������݂����ꍇ
            Stream sIn = new FileStream(TablePath, FileMode.Open, 
                                            FileAccess.Read, FileShare.Read);
            dic = (Dictionary<string, WebSite>)formatter.Deserialize(sIn);
            sIn.Close();
        }
        else
        {    //�O��`�F�b�N���ʃt�@�C�������݂��Ȃ������ꍇ
            dic = new Dictionary<string, WebSite>();
        }
        //Web�T�C�g������
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
                    sb.Append("[�ύX] ");
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
                m_logger.Debug("[�ǉ�] " + webSite.url);
            }
        }
        //�������ʂ��t�@�C���ɕۑ�
        Stream sOut = new FileStream(TablePath, FileMode.Create, 
                                        FileAccess.Write, FileShare.None);
        formatter.Serialize(sOut, dic);
        sOut.Close();
    }
    //--------------------------------------------------------------------------
    private List<WebSite> GetWebsiteInfo(Config conf)
    {    //Web�T�C�g�̏���ݒ�t�@�C������ǂݍ���
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
//�ݒ���N���X
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
    {    //xpath�Ŏw�肳�ꂽ�m�[�h�̃e�L�X�g��Ԃ�
        XmlNode node = m_xmlConfig.SelectSingleNode(xpath);
        return (node != null) ? node.InnerText : "";
    }
    //--------------------------------------------------------------------------
    public XmlNodeList GetNodeList(string xpath)
    {    //xpath�Ŏw�肳�ꂽ�m�[�h���X�g��Ԃ�
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
//Web�T�C�g�N���X
////////////////////////////////////////////////////////////////////////////////
[Serializable]
class WebSite
{
    public string url{get; set;}//URL
    public string title{get; set;}//�^�C�g��
    public string hash{get; set; }//�n�b�V���l
    //--------------------------------------------------------------------------
    public WebSite(){}
    public WebSite(string url, string title)
    {
        this.url = url;
        this.title = title;
    }
    //--------------------------------------------------------------------------
    public string GetHashOfContens()
    {    //HTTP���X�|���X�̃n�b�V���l��Ԃ�
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
//���K�[�C���^�t�F�[�X
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
//�R���\�[�����K�[�i�b��Łj
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
//�t�@�C�����K�[
////////////////////////////////////////////////////////////////////////////////
class FileLogger : ILogger
{
    private StreamWriter m_sw = null;
    private static FileLogger instance = new FileLogger();
    private string m_Path = "";//���O�t�@�C���̃p�X
    public MessageLevel Level{get; set; }//�o�̓��x��
    //--------------------------------------------------------------------------
    private FileLogger(){}//�R���X�g���N�^�i�������j
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
    {    //�C���X�^���X�擾
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
