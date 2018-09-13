using System.Xml;

public class ApkInfoVo
{
    public static string remoteApkInfo = "http://192.168.30.53/ApkInfo.xml"; //远程的apkinfo配置信息，通过这个路径初始化这个静态对象


    //!!!!!!!!!!!!!!!!!!!!
    //!!!!!!!!!!!!!!!!!!!!
    //!!!!!!!!!!!!!!!!!!!!
    public static string apkName = "buyu_debug.apk"; //版本下载的apk包名字，下载大版本的时候会通过这个名字去下载，需要注意！！
    //!!!!!!!!!!!!!!!!!!!!
    //!!!!!!!!!!!!!!!!!!!!



    public static string remoteVersion = "1.0.0"; //远程库最新版本，
    public static int remoteResId = 1000; //远程库最新的资源id


    public static string remoteVersionPath = "";//远程大版本的下载路径前缀
    public static string remoteResPath = ""; //远程的资源下载路径
    //    public static string remoteVersionPath = "http://192.168.30.165/";//远程大版本的下载路径前缀
    //    public static string remoteResPath = "http://192.168.30.165/"; //远程的资源下载路径
    /// <summary>
    /// 传入xml初始化apkInfo
    /// </summary>
    /// <param name="xmlInfo"></param>
    public static void Init(string xmlInfo)
    {
        XmlDocument apkInfo = new XmlDocument();
        apkInfo.LoadXml(xmlInfo);
        XmlNode root = apkInfo.SelectSingleNode("root");
        XmlNode verNode = root.SelectSingleNode("verInfo");
        remoteVersion = verNode.Attributes["curVersion"].Value;
        remoteResId = int.Parse(verNode.Attributes["curResId"].Value);

        XmlNode verUrlNode = root.SelectSingleNode("remoteVersionPath");
        XmlNode resUrlNode = root.SelectSingleNode("remoteResPath");

        remoteVersionPath = verUrlNode.Attributes["url"].Value;
        remoteResPath = resUrlNode.Attributes["url"].Value;

    }
    /// <summary>
    /// 获取远程的资源xml表
    /// </summary>
    /// <returns></returns>
    public static string GetRemoteVersionXmlPath
    {
        get
        {
            return remoteResPath + remoteVersion + "/" + remoteResId + "/versionInfo.xml";
        }

    }

    /// <summary>
    /// 获取远程大版本下载的路径地址
    /// </summary>
    public static string GetRemoteVersionDownPath
    {
        get
        {
            return remoteVersionPath + remoteVersion + "/" + apkName;
        }
    }
    /// <summary>
    /// 获得断点过程中的临时文件名字，保证能够进行断点下载
    /// </summary>
    /// <returns></returns>
    public static string GetTmpVerName
    {
        get { return apkName + "_" + remoteVersion + ".tmp"; }
    }

    /// <summary>
    /// 获取本地需要安装的apk名字
    /// </summary>
    public static string GetLocalApkName
    {
        get
        {
            string s = apkName.Replace(".apk", "");
            return s + "_" + remoteVersion + ".apk";
        }
    }

    public static string apkDownPath = ""; //apk下载的路径

}

