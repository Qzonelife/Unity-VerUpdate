using System;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Random = UnityEngine.Random;
public class ZFileUtil
{

    const int ENCRYPT_SIZE = 32;
    static byte[] m_bufCode = null;
    const int randSeed = 66015;
    private const string encodeHead = "AWEncode";
    public static void InitEncrypt()
    {
        if (m_bufCode == null)
        {
            m_bufCode = new byte[ENCRYPT_SIZE];
            Random.seed = randSeed;
            for (int i = 0; i < ENCRYPT_SIZE; ++i)
            {
                m_bufCode[i] = (byte)Random.Range(0, 255);
            }
        }
    }

    //加密
    public static byte[] Encrypt(byte[] pBuf)
    {
        if (pBuf == null)
        {
            return null;
        }
        InitEncrypt();
        byte[] en = Encoding.Default.GetBytes(encodeHead);
        int nSize = pBuf.Length;
        byte[] pNewBuf = new byte[en.Length + nSize];

        en.CopyTo(pNewBuf, 0);
        int temp;
        for (int i = 0; i < nSize; i++)
        {
            temp = pBuf[i];
            temp = temp ^ m_bufCode[i % ENCRYPT_SIZE];
            pNewBuf[i + en.Length] = (byte)temp;
        }
        return pNewBuf;
    }

    //解密
    public static byte[] Decrypt(byte[] pBuf)
    {
        if (pBuf == null)
        {
            return null;
        }
        InitEncrypt();
        int nSize = pBuf.Length;
        byte[] en = Encoding.Default.GetBytes(encodeHead);
        nSize = nSize - en.Length;
        byte[] pNewBuf = new byte[nSize];
        int temp;
        for (int i = 0; i < nSize; i++)
        {
            temp = pBuf[i + en.Length];
            temp = temp ^ m_bufCode[i % ENCRYPT_SIZE];
            pNewBuf[i] = (byte)temp;
        }
        return pNewBuf;
    }

    /// <summary>
    /// 判断文件是否加密,传入bytes
    /// </summary>
    /// <param name="arr"></param>
    /// <returns></returns>
    public static bool IsFileEncrypt(byte[] arr)
    {

        byte[] en = Encoding.Default.GetBytes(encodeHead);
        if (arr.Length < en.Length)
        {
            return false;
        }
        for (int i = 0; i < en.Length; i++)
        {
            if (en[i] != arr[i])
            {
                return false;
            }
        }
        return true;
    }

    //压缩字节
    //1.创建压缩的数据流 
    //2.设定compressStream为存放被压缩的文件流,并设定为压缩模式
    //3.将需要压缩的字节写到被压缩的文件流
    public static byte[] CompressBytes(byte[] bytes)
    {
        using (MemoryStream compressStream = new MemoryStream())
        {
            using (var zipStream = new GZipStream(compressStream, CompressionMode.Compress))
                zipStream.Write(bytes, 0, bytes.Length);
            return compressStream.ToArray();
        }
    }
    //解压缩字节
    //1.创建被压缩的数据流
    //2.创建zipStream对象，并传入解压的文件流
    //3.创建目标流
    //4.zipStream拷贝到目标流
    //5.返回目标流输出字节
    public static byte[] Decompress(byte[] bytes)
    {
        using (var compressStream = new MemoryStream(bytes))
        {
            using (GZipStream zipStream = new GZipStream(compressStream, CompressionMode.Decompress))
            {
                MemoryStream resultStream = CopyTo(zipStream);
                return resultStream.ToArray();

            }
        }
    }
    public static MemoryStream CopyTo(Stream source)
    {
        byte[] buffer = new byte[2048];
        int bytesRead;
        long totalBytes = 0;
        MemoryStream destination = new MemoryStream();
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, bytesRead);
            totalBytes += bytesRead;
        }
        return destination;
    }


    /// <summary>
    /// 计算文件的MD5值
    /// </summary>
    public static string md5file(string file)
    {
        try
        {
            FileStream fs = new FileStream(file, FileMode.Open);
            System.Security.Cryptography.MD5 md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();
            byte[] retVal = md5.ComputeHash(fs);
            fs.Close();

            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < retVal.Length; i++)
            {
                sb.Append(retVal[i].ToString("x2"));
            }
            return sb.ToString();
        }
        catch (Exception ex)
        {
            throw new Exception("md5file() fail, error:" + ex.Message);
        }
    }

    static List<string> files = new List<string>();
    /// <summary>
    /// 遍历目录下所有文件
    /// </summary>
    /// <returns></returns>
    public static List<string> GetAllFiles(string path, bool NotReplace = true)
    {
        files.Clear();
        Recursive(path, NotReplace);
        return files;
    }

    /// <summary>
    /// 遍历目录及其子目录
    /// </summary>
    static void Recursive(string path, bool NotReplace = true)
    {

        string[] names = Directory.GetFiles(path);
        string[] dirs = Directory.GetDirectories(path);
        foreach (string filename in names)
        {
            string ext = Path.GetExtension(filename);
            if (ext.Equals(".meta")) continue;
            files.Add(NotReplace ? filename.Replace('\\', '/') : filename);
        }
        foreach (string dir in dirs)
        {
            Recursive(dir, NotReplace);
        }
    }

    public static long GetFileSize(string path)
    {
        if (File.Exists(path))
        {
            FileInfo f = new FileInfo(path);
            return f.Length;
        }
        return 0;
    }
}
