using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

public class VersionVo
{

    XmlDocument xdoc = new XmlDocument();

    /// <summary>
    /// 更新版本的信息列表，记录所有版本中的差异文件 ,isAsXml，是否作为xml数据传入，默认为xml路径
    /// </summary>
    public Dictionary<int, List<VFStruct>> vStructDict = new Dictionary<int, List<VFStruct>>();
    public VersionVo(string tar, bool isAsXml = false)
    {
        if (isAsXml)
        {
            xdoc.LoadXml(tar);
        }
        else
        {
            xdoc.Load(tar);
        }

        XmlNode node = xdoc.SelectSingleNode("version");
        currentVersion = node.Attributes["currentVersion"].Value;
        resVersion = int.Parse(node.Attributes["resVersion"].Value);

    }

    /// <summary>
    /// 初始化差异文件字典，保存了所有版本中的差异资源
    /// </summary>
    public void InitDifferDict()
    {
        vStructDict.Clear();
        XmlNode rootNode = xdoc.SelectSingleNode("version");
        XmlNodeList resList = rootNode.SelectNodes("vList");
        foreach (XmlNode node in resList)
        {
            int resId = int.Parse(node.Attributes["resId"].Value);
            XmlNodeList resNodeDetail = node.SelectNodes("res");
            List<VFStruct> vfList = new List<VFStruct>();
            foreach (XmlNode resNode in resNodeDetail)
            {
                VFStruct vs = new VFStruct(resNode, resId);
                vfList.Add(vs);
            }
            if (!vStructDict.ContainsKey(resId))
            {
                vStructDict.Add(resId, vfList);
            }
        }
    }

    /// <summary>
    /// 计算差异文件大小总和
    /// </summary>
    /// <returns></returns>
    public long CalculateDifferSize()
    {
        long totalSize = 0;
        foreach (KeyValuePair<int, List<VFStruct>> val in this.vStructDict)
        {
            for (int i = 0; i < val.Value.Count; i++)
            {
                totalSize += val.Value[i].size;
            }
        }
        return totalSize;
    }

    public long CalculateVFLSize(List<VFStruct> list)
    {
        long totalSize = 0;
        for (int i = 0; i < list.Count; i++)
        {
            totalSize += list[i].size;
        }
        return totalSize;
    }


    private string currentVersion;
    private int resVersion;

    //a.b.c ,三位比较方式从高中低
    public string CurrentVersion
    {
        get { return this.currentVersion; }
    }
    //abcd ,四位
    public int ResVersion
    {
        get { return this.resVersion; }
        set
        {
            this.resVersion = value;
            XmlNode node = xdoc.SelectSingleNode("version");
            node.Attributes["resVersion"].Value = value.ToString();

        }
    }

    /// <summary>
    ///  输入当前版本的版本号，自动获取到最新版本的资源差异列表
    /// </summary>
    /// <param name="resId"></param>
    /// <returns></returns>
    public Dictionary<string, VFStruct> GetDiffFromBeginVer(int resId)
    {

        Dictionary<string, VFStruct> diffDict = new Dictionary<string, VFStruct>(); //key是文件名，如果字典中已经有了，就检查最新的
        int tmpResId = resId;
        //差异资源列表中可能存在重复资源，需要进行去重，只保留最新的差异文件,直接使用字典保存的方式去重
        while (true)
        {
            tmpResId++;
            if (!vStructDict.ContainsKey(tmpResId))
            {
                break;
            }
            else
            {
                List<VFStruct> vfLs = vStructDict[tmpResId]; //拿到资源列表
                for (int i = 0; i < vfLs.Count; i++) //遍历添加
                {
                    VFStruct vs = vfLs[i];
                    if (diffDict.ContainsKey(vs.file))//如果字典中已经存在这个差异文件，则比较版本号，保存版本号大的
                    {
                        VFStruct ovs = diffDict[vs.file];
                        if (vs.IsNewerThan(ovs)) //新的资源版本更新，直接替换
                        {
                            diffDict.Remove(ovs.file);
                            diffDict.Add(vs.file, vs);
                        }
                    }
                    else
                    {
                        diffDict.Add(vs.file, vs);
                    }
                }
            }

        }
        return diffDict;
    }

    /// <summary>
    /// 更新资源版本的差异信息
    /// </summary>
    /// <param name="resVersion"></param>
    /// <param name="info"></param>
    public void AppenVersionDetail(int resVersion, string info)
    {
        XmlNode node = xdoc.SelectSingleNode("version");
        XmlNodeList nodeList = node.SelectNodes("vList"); //列表
        XmlNode tarNode = null;
        for (int i = 0; i < nodeList.Count; i++)
        {
            if (nodeList[i].Attributes["resId"].Value == resVersion.ToString()) //找到对应的xmlNode
            {
                tarNode = nodeList[i];
            }
        }
        if (tarNode != null)
        {
            node.RemoveChild(tarNode);
        }

        //版本信息插入
        tarNode = xdoc.CreateElement("vList");
        XmlAttribute resAttr = xdoc.CreateAttribute("resId");
        resAttr.Value = resVersion.ToString();
        tarNode.Attributes.Append(resAttr);

        info = info.Replace("\r", "");
        string[] details = info.Split('\n');
        for (int i = 0; i < details.Length; i++)
        {
            string[] detailsArr = details[i].Split('|');
            if (detailsArr.Length != 3)
            {
                continue;
            }
            XmlNode nodeDetail = xdoc.CreateElement("res");
            XmlAttribute fileDetail = xdoc.CreateAttribute("file");
            fileDetail.Value = detailsArr[0];
            XmlAttribute md5Detail = xdoc.CreateAttribute("md5");
            md5Detail.Value = detailsArr[1];
            XmlAttribute fileSize = xdoc.CreateAttribute("size");
            fileSize.Value = detailsArr[2];

            nodeDetail.Attributes.Append(fileDetail);
            nodeDetail.Attributes.Append(md5Detail);
            nodeDetail.Attributes.Append(fileSize);
            tarNode.AppendChild(nodeDetail);
        }

        node.AppendChild(tarNode);


        //        XmlNode tarResNode =  node.SelectSingleNode("vList");
        //        if (tarResNode == null)
        //        {
        //            tarResNode = xdoc.CreateElement(resVersion);
        //        }
        //        node.AppendChild(tarResNode);

    }

    public void WriteVersionInfo(string path)
    {
        if (xdoc != null)
        {
            StreamWriter sw = new StreamWriter(path, false, new UTF8Encoding(false));
            xdoc.Save(sw);
        }
    }

    /// <summary>
    /// 是否需要进行大版本更新
    /// </summary>
    /// <param name="version"></param>
    /// <returns></returns>
    public bool isVersionNeedUpdate(string version)
    {
        string[] mvs = this.CurrentVersion.Split('.');
        string[] tarVs = version.Split('.');
        if (mvs.Length == tarVs.Length)
        {
            for (int i = 0; i < mvs.Length; i++)
            {
                if (mvs[i] == tarVs[i])
                {
                    continue;
                }
                return int.Parse(mvs[i]) < int.Parse(tarVs[i]);
            }
        }

        return false;
    }
    /// <summary>
    /// 是否存在资源更新
    /// </summary>
    /// <param name="resVersion"></param>
    /// <returns></returns>
    public bool isResVersionNeedUpdate(int resVersion)
    {

        return this.ResVersion < resVersion;
    }


}


/// <summary>
/// 版本差异信息的结构，包含文件路径，文件md5值，文件大小
/// </summary>
public struct VFStruct
{
    public VFStruct(XmlNode node, int resId)
    {
        this.file = node.Attributes["file"].Value;
        this.md5 = node.Attributes["md5"].Value;
        this.size = long.Parse(node.Attributes["size"].Value);
        this.resId = resId;

    }
    /// <summary>
    /// 判断当前是否资源号更新
    /// </summary>
    /// <param name="vf"></param>
    /// <returns></returns>
    public bool IsNewerThan(VFStruct vf)
    {
        return this.resId > vf.resId;
    }

    public string file;
    public string md5;
    public long size;
    public int resId;
}
