using System;
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Threading;
using ThreadPriority = System.Threading.ThreadPriority;


public class VersionManager : MonoBehaviour
{
    //versionManager的一些基本配置开始
    private string gameDataPath = ""; //游戏资源文件存放目录

    private string gameStreamingPath = ""; //游戏内部流数据文件目录，仅读
    //versionManager的一些基本配置结束





    private Thread thread;
    private List<string> downLs = new List<string>();
    private string currDownFile = string.Empty;
    delegate void ThreadSyncEvent(VFStruct data);
    private ThreadSyncEvent m_SyncEvent;
    private Action<VFStruct> func;
    public VFStruct currentDownVf;
    private long totalSize = 0; //需要下载总大小，字节大小
    private long currentSize = 0; //当前下载大小，字节大小
    private VersionVo remoteVersionVo;

    private Action<bool, string> completeAction;//资源更新检测回调事件
    private Action<UpdateState> stateChangeAction; //更新更新状态显示回调
    private Action<long, long> progressChangeAction; //进度条更新回调

    private Action<Action<bool>, string> waitingAction; //设置等待回调
    private Action continueAction; //继续实行的任务，等待回调执行后的事件


    private long currentMax = 0;

    /// <summary>
    /// 设置下载返回
    /// </summary>
    /// <param name="act">更新完成回调，bool 标识是否成功，string为错误码</param>
    public void SetCompleteAction(Action<bool, string> act)
    {
        this.completeAction = act;
    }
    /// <summary>
    /// 设置状态更新回调
    /// </summary>
    /// <param name="cb"></param>
    public void SetStateChagneAction(Action<UpdateState> cb)
    {
        if (cb != null)
        {
            stateChangeAction = cb;
        }
    }
    /// <summary>
    /// 设置进度条回调
    /// </summary>
    /// <param name="cb">long long分别表示当前进度跟最大进度</param>
    public void SetProgressChangeAction(Action<long, long> cb)
    {
        if (cb != null)
        {
            progressChangeAction = cb;
        }
    }
    /// <summary>
    /// 设置等待回调
    /// </summary>
    /// <param name="cb"> 回调方法中包含 bool ,如果为true则继续往下走，string为等待提示 </param>
    public void SetWaitingAction(Action<Action<bool>, string> cb)
    {
        if (cb != null)
        {
            waitingAction = cb;
        }
    }

    /// <summary>
    /// 等待确认事件
    /// </summary>
    /// <param name="continueAction"></param>
    private void Waiting(Action continueAction, string tips)
    {
        if (this.waitingAction != null)
        {
            this.continueAction = continueAction;
            this.waitingAction(OnConfirmCb, tips); //等待确认回调
        }
    }

    /// <summary>
    /// 执行流程回调，是否确认继续
    /// </summary>
    /// <param name="isConfirm"></param>
    private void OnConfirmCb(bool isConfirm)
    {
        if (isConfirm)
        {
            if (this.continueAction != null)
            {
                this.continueAction();
            }
        }
    }

    /// <summary>
    /// 显示进度条内容
    /// </summary>
    /// <param name="progress"></param>
    /// <param name="title"></param>
    private void SetUpdateState(UpdateState title)
    {
        if (stateChangeAction != null)
        {
            stateChangeAction(title);
        }
    }
    /// <summary>
    /// 设置进度值
    /// </summary>
    /// <param name="current"></param>
    /// <param name="max"></param>
    private void SetProgress(long current, long max = -1)
    {
        if (progressChangeAction != null)
        {
            if (max != -1)
            {
                currentMax = max;
            }
            max = max == -1 ? currentMax : max;
            progressChangeAction(current, max);
        }
    }
    /// <summary>
    /// 初始化更新信息
    /// </summary>
    /// <param name="gDataPath">游戏资源路径，保存游戏更新数据</param>
    /// <param name="gStreamingPath">游戏streaming路径，仅读，保存游戏原始数据</param>
    /// <param name="remoteApkInfoUrl">游戏远端apkinfo路径,记录了资源地址跟版本地址</param>
    /// <param name="apkName">游戏包名</param>
    /// <param name="apkDownPath">游戏下载的临时路径</param>
    public void InitVersionManager(string gDataPath, string gStreamingPath, string remoteApkInfoUrl, string apkName, string apkDownPath)
    {
        this.gameDataPath = gDataPath;
        this.gameStreamingPath = gStreamingPath;
        ApkInfoVo.remoteApkInfo = remoteApkInfoUrl;
        ApkInfoVo.apkName = apkName;
        ApkInfoVo.apkDownPath = apkDownPath;
    }



    /// <summary>
    /// 开始初始化资源
    /// </summary>
    public void StartInitRes()
    {
        InitStreamingAssestVersionInfo(delegate
        {
            StartCoroutine(InitRemoteVersionInfo(delegate
            {
                //对比看是否需要进行大版本更新,如果不需要，才继续往下走
                BigVersionChecking();

            }));
        }); //先检测大版本的更新信息，从streamingassets下拿到info

    }

    /// <summary>
    /// 对比检测远程的xml信息
    /// </summary>
    IEnumerator InitRemoteVersionInfo(Action cb)
    {
        WWW apkInfo = new WWW(ApkInfoVo.remoteApkInfo);
        yield return apkInfo;
        if (apkInfo.isDone)
        {
            if (string.IsNullOrEmpty(apkInfo.error)) //没有错误
            {
                ApkInfoVo.Init(apkInfo.text);
                cb();//初始化信息完成，进行回调
            }
            else
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Waiting(CheckVersion, "网络连接失败，请检查网络");
                }
                else
                {
                    Waiting(CheckVersion, "服务器信息错误，请联系客服");
                }
            }
        }
    }

    /// <summary>
    /// 大版本信息检测完毕，开始释放资源
    /// </summary>
    public void BigVerCheckFinish()
    {

        bool isFilesHasInited = Directory.Exists(gameDataPath) && File.Exists(gameDataPath + "files.txt");

        if (isFilesHasInited) //files.txt已经存在了，判断是否需要更新
        {


            ///在这里版本资源已经释放完毕了，但是需要看版本资源是否正确，否则还是需要删除资源重新释放
            //检查是否版本变化了，拿到streamingassets的versionInfo.xml跟沙盒的versionInfo.xml进行判断

            InitCurrentVerVo(delegate //初始化沙盒的versioninfo
            {
                if (currentVo.isVersionNeedUpdate(streamingVerVo.CurrentVersion)) //如果旧包资源需要清空
                {
                    SetUpdateState(UpdateState.RELEASE_RES); //释放资源
                    StartCoroutine(CheckExtRes());
                }
                else
                {
                    OnReleaseFinish();
                }
            });

        }
        else  //files.txt 不存在，先释放资源，或者先检查是否需要下载新的大版本
        {
            SetUpdateState(UpdateState.RELEASE_RES); //释放资源
            StartCoroutine(CheckExtRes());
        }
    }


    IEnumerator CheckExtRes()
    {
        string targetPath = gameDataPath; //
        string orgResPath = gameStreamingPath;
        //将释放资源的时候将原本的资源删除
        if (Directory.Exists(targetPath))
        {
            Directory.Delete(targetPath, true);
        }
        string inFile = orgResPath + "files.txt";
        string outFile = targetPath + "files.txt";
        string tmpTxt = targetPath + "tmpExtFiles.txt"; //临时解压文件的数据
        if (File.Exists(outFile))
        {
            File.Delete(outFile);
        }
        if (!Directory.Exists(targetPath))
        {
            Directory.CreateDirectory(targetPath);

        }

        //先将文件写入到临时信息文件中，保证解压过程不会被中断
        if (Application.platform == RuntimePlatform.Android) //android环境
        {
            WWW www = new WWW(inFile);
            yield return www;
            if (www.isDone)
            {
                File.WriteAllBytes(tmpTxt, www.bytes);
            }
            yield return 0;
        }
        else //编辑器环境
        {
            File.Copy(inFile, tmpTxt, true);
        }
        yield return new WaitForEndOfFrame();


        string totalStr = File.ReadAllText(tmpTxt);
        string[] files = File.ReadAllLines(tmpTxt);

        //SetMaxProgress(files.Length);
        int curCount = 0;
        foreach (var file in files)
        {
            string[] fs = file.Split('|');
            if (fs.Length != 3) //保证格式正确
            {
                continue;
            }
            inFile = orgResPath + fs[0];
            tmpTxt = targetPath + fs[0];
            string dir = Path.GetDirectoryName(tmpTxt);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            if (Application.platform == RuntimePlatform.Android)
            {
                WWW fWww = new WWW(inFile);
                yield return fWww;
                if (fWww.isDone)
                {
                    if (string.IsNullOrEmpty(fWww.error))
                    {
                        File.WriteAllBytes(tmpTxt, fWww.bytes);
                    }
                    else
                    {
                        Debug.Log(File.Exists(tmpTxt));
                        Debug.Log(tmpTxt);
                        Debug.Log(inFile);
                        Debug.Log(fWww.url);
                        Debug.Log("文件解包错误：" + tmpTxt + ":\n" + fWww.error);

                    }


                }
                yield return 0;
            }
            else
            {
                if (File.Exists(tmpTxt))
                {
                    File.Delete(tmpTxt);
                }
                File.Copy(inFile, tmpTxt, true);
            }
            curCount++;
            //SetLoadingProgress(curCount);
            if (curCount % 20 == 0) //分帧，20个文件一次
            {
                continue;
            }
            yield return new WaitForEndOfFrame();
        }
        yield return 0;

        //        if (Application.platform == RuntimePlatform.Android)
        //        {
        //            WWW fWww = new WWW(orgResPath+"versionInfo.xml");
        //            yield return fWww;
        //            if (fWww.isDone)
        //            {
        //                if (string.IsNullOrEmpty(fWww.error))
        //                {
        //                    Debug.LogError(orgResPath + "versionInfo.xml");
        //                    Debug.LogError(fWww.url);
        //                    File.WriteAllBytes(targetPath+"versionInfo.xml", fWww.bytes);
        //                }
        //                else
        //                {
        //  
        //                    Debug.Log(fWww.url);
        //                    Debug.Log("文件解包错误：" + tmpTxt + ":\n" + fWww.error);
        //
        //                }
        //
        //
        //            }
        //            yield return 0;
        //        }
        //最后写入版本的文件，保证解压过程不会被中断
        File.WriteAllText(outFile, totalStr);
        File.Delete(targetPath + "tmpExtFiles.txt"); //删除临时的文件记录
        OnReleaseFinish();
        yield return 0;
    }
    /// <summary>
    /// 资源释放完毕
    /// </summary>
    public void OnReleaseFinish()
    {

        //资源释放完毕
        InitCurrentVerVo(OnStartCheckUpdate);
    }

    public void OnStartCheckUpdate()
    {
        if (Application.internetReachability == NetworkReachability.NotReachable) //当前没有网络
        {

            Waiting(OnStartCheckUpdate, "当前无网络连接，请检查网络状态");
            return;
        }
        SetUpdateState(UpdateState.START_CHECKING);
        SetProgress(0, 0);
        StartAndInitCheckVer();
    }

    /// <summary>
    /// 开始检测版本
    /// </summary>
    private void StartAndInitCheckVer()
    {
        //currentDataPath = GameSetting.DataPath;
        CheckVersion();
    }
    /// <summary>
    /// 初始化下载准备，开启线程
    /// </summary>
    private void InitDownAction()
    {

        m_SyncEvent = OnSyncEvent;
        thread = new Thread(OnUpdate);
        thread.Start();
    }

    /// <summary>
    /// 通知事件
    /// </summary>
    /// <param name="state"></param>
    private void OnSyncEvent(VFStruct data)
    {
        if (this.func != null) func(data);  //回调逻辑层
    }
    /// <summary>
    /// 添加到事件队列
    /// </summary>
    public void AddEvent(VFStruct target, Action<VFStruct> func)
    {
        lock (m_lockObject)
        {
            this.func = func;
            events.Enqueue(target);
        }
    }
    static readonly object m_lockObject = new object();
    static Queue<VFStruct> events = new Queue<VFStruct>();
    void OnUpdate()
    {
        while (true)
        {
            lock (m_lockObject)
            {
                if (events.Count > 0)
                {
                    VFStruct e = events.Dequeue();
                    try
                    {
                        OnDownloadFile(e);
                    }
                    catch (System.Exception ex)
                    {
                        UnityEngine.Debug.LogError(ex.Message);
                    }
                }
            }
            Thread.Sleep(1);
        }

    }
    /// <summary>
    /// 下载文件
    /// </summary>
    void OnDownloadFile(VFStruct vf)
    {
        string url = ApkInfoVo.remoteResPath + remoteVersionVo.CurrentVersion + "/" + vf.resId + "/" + vf.file;
        currDownFile = gameDataPath + vf.file;
        currentDownVf = vf;
        using (WebClient client = new WebClient())
        {
            client.DownloadFileAsync(new System.Uri(url), currDownFile);
            client.DownloadFileCompleted += FileDownComplete;

        }
    }

    private void FileDownComplete(object sender, AsyncCompletedEventArgs e)
    {

        if (e.Error == null)
        {
            m_SyncEvent(currentDownVf);
        }
        else
        {
            //如果下载文件错误，添加一个列表，忽略并抛出
            VFStruct errV = new VFStruct();
            errV.file = currentDownVf.file;
            errV.size = -1;
            errV.md5 = e.Error.ToString();
            m_SyncEvent(errV);
        }
    }


    /// <summary>
    /// 检测版本更新
    /// </summary>
    /// <param name="completeCallBack"></param>
    public void CheckVersion()
    {

        downLs.Clear();
        totalSize = 0;
        currentSize = 0;
        remoteVersionVo = null; //清空当前缓存的远端
        CompareVersion();
    }



    private VersionVo currentVo;
    private void InitCurrentVerVo(Action cb)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            StartCoroutine(LoadCurrentVer(cb, "file://" + gameDataPath + "versionInfo.xml"));
        }
        else
        {
            string currentVersionPath = gameDataPath + "versionInfo.xml";
            currentVo = new VersionVo(currentVersionPath);
            cb();
        }
    }

    private VersionVo streamingVerVo;
    /// <summary>
    /// 通过streamingassest下的路径初始化currentvo，用于检测大版本
    /// </summary>
    /// <param name="cb"></param>
    private void InitStreamingAssestVersionInfo(Action cb)
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            StartCoroutine(LoadStreamingCurrentVer(cb, gameStreamingPath + "versionInfo.xml"));
        }
        else
        {
            string currentVersionPath = gameStreamingPath + "versionInfo.xml";
            streamingVerVo = new VersionVo(currentVersionPath);
            cb();
        }
    }

    /// <summary>
    /// 下载apk内版本
    /// </summary>
    /// <param name="callBack"></param>
    /// <returns></returns>
    IEnumerator LoadStreamingCurrentVer(Action callBack, string pathStr)
    {
        WWW localVer = new WWW(pathStr);
        yield return localVer;
        if (localVer.isDone)
        {
            if (string.IsNullOrEmpty(localVer.error))
            {
                streamingVerVo = new VersionVo(localVer.text, true);
                callBack();
            }
            else
            {
                Debug.Log("versionInfo.xml error:" + localVer.error);
            }
        }

    }

    /// <summary>
    /// 下载本地版本
    /// </summary>
    /// <param name="callBack"></param>
    /// <returns></returns>
    IEnumerator LoadCurrentVer(Action callBack, string pathStr)
    {
        WWW localVer = new WWW(pathStr);
        yield return localVer;
        if (localVer.isDone)
        {
            if (string.IsNullOrEmpty(localVer.error))
            {
                currentVo = new VersionVo(localVer.text, true);
                callBack();
            }
            else
            {
                Debug.Log("versionInfo.xml error:" + localVer.error);
            }
        }

    }


    public void BigVersionChecking()
    {

        if (!CompareBigVersionUpdate()) //对比看是否需要进行大版本更新,如果不需要，才继续往下走
        {
            BigVerCheckFinish();
        }
    }
    /// <summary>
    /// 检测是否需要进行大版本更新
    /// </summary>
    public bool CompareBigVersionUpdate()
    {

        if (streamingVerVo.isVersionNeedUpdate(ApkInfoVo.remoteVersion))
        {
            Debug.Log("需要进行大版本更新");
            //先判断本地是否已经有安装包了,如果有就直接安装
            if (File.Exists(gameDataPath + ApkInfoVo.GetLocalApkName))
            {
                Debug.Log("版本已经下载完毕");
                OnVersionDownComplete();
            }
            else
            {
                Waiting(ConfirmDownVersion, "检测到版本需要更新，是否下载新版本安装包");

            }
            return true;
        }
        else
        {
            return false;
        }
    }

    /// <summary>
    /// 检测是否需要进行资源更新
    /// </summary>
    public void CompareVersion()
    {


        if (currentVo.isResVersionNeedUpdate(ApkInfoVo.remoteResId))
        {

            StartCoroutine(StartDownDiffList());
        }
        else
        {
            DoVersionComplete(true);

        }
    }


    private void ConfirmDownVersion()
    {
        //StartDownVersion(true);
        StartCoroutine(StartDownVersion());//开始下载大版本apk包

    }

    private long currentDownVerLen = 0; //大版本下载的缓存进度

    void OnApplicationQuit()
    {
        if (thread != null)
        {
            thread.Abort();
        }
    }

    IEnumerator StartDownVersion()
    {
        if (!Directory.Exists(gameDataPath))
        {
            Directory.CreateDirectory(gameDataPath);
        }
        string tmpFilePath = gameDataPath + ApkInfoVo.GetTmpVerName;
        string url = ApkInfoVo.GetRemoteVersionDownPath;
        Debug.Log("url is:" + url);

        HttpWebRequest req = HttpWebRequest.Create(url) as HttpWebRequest;
        req.Method = "GET";
        FileStream fs;

        if (File.Exists(tmpFilePath))
        {
            fs = File.OpenWrite(tmpFilePath);
            currentDownVerLen = fs.Length;
            fs.Seek(currentDownVerLen, SeekOrigin.Current);
            req.AddRange((int)currentDownVerLen);
            req.Timeout = 2000;
        }
        else
        {
            fs = new FileStream(tmpFilePath, FileMode.Create, FileAccess.Write);
            currentDownVerLen = 0;
        }



        HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
        Stream stream = resp.GetResponseStream();
        long fileLen = resp.ContentLength + currentDownVerLen;//总文件大小
        SetProgress(0, fileLen);
        int lengthOnce;
        int bufferMaxLength = 1024 * 512;
        while (currentDownVerLen < fileLen)
        {
            byte[] buffer = new byte[bufferMaxLength];
            if (stream.CanRead)
            {
                lengthOnce = stream.Read(buffer, 0, buffer.Length);
                currentDownVerLen += lengthOnce;
                SetProgress(currentDownVerLen);
                fs.Write(buffer, 0, lengthOnce);
            }
            else
            {
                break;
            }
            yield return null;
        }
        resp.Close();
        stream.Close();
        fs.Close();
        fs.Dispose();
        File.Move(tmpFilePath, ApkInfoVo.apkDownPath);
        OnVersionDownComplete();



    }
    /// <summary>
    /// 版本下载完毕
    /// </summary>
    void OnVersionDownComplete()
    {
        stateChangeAction(UpdateState.STATE_VERSION_DOWN_COMPLETE);

    }





    private int tmpDownedCount = 0; //分帧计数器
    /// <summary>
    /// 开始进行差异更新,下载当前最新版本的差异文件
    /// </summary>
    IEnumerator StartDownDiffList()
    {

        WWW remoteVersion = new WWW(ApkInfoVo.GetRemoteVersionXmlPath);
        yield return remoteVersion;
        if (remoteVersion.isDone)
        {
            if (string.IsNullOrEmpty(remoteVersion.error))
            {
                remoteVersionVo = new VersionVo(remoteVersion.text, true);
                remoteVersionVo.InitDifferDict(); //初始化所有版本的更新内容
                Dictionary<string, VFStruct> updateDict = remoteVersionVo.GetDiffFromBeginVer(currentVo.ResVersion);
                List<VFStruct> vfList = new List<VFStruct>();
                foreach (KeyValuePair<string, VFStruct> dict in updateDict)
                {
                    vfList.Add(dict.Value);
                }
                totalSize = remoteVersionVo.CalculateVFLSize(vfList);

                if (Application.internetReachability != NetworkReachability.ReachableViaCarrierDataNetwork) //如果是移动数据网络，则需要提示玩家
                {
                    Waiting(OnDownConfirm, "当前是移动数据网络，资源包大小为" + GetFormatFileSize(totalSize) + ",是否继续进行下载？");
                }
                else
                {
                    OnDownConfirm();
                }
            }
            else
            {
                if (Application.internetReachability == NetworkReachability.NotReachable)
                {
                    Waiting(CompareVersion, "网络连接失败，请检查网络");
                }
            }
        }
    }

    private void OnDownConfirm()
    {
        InitDownAction(); //初始化下载事件
        StartCoroutine(OnDowningFiles());
    }
    IEnumerator OnDowningFiles()
    {
        Dictionary<string, VFStruct> updateDict = remoteVersionVo.GetDiffFromBeginVer(currentVo.ResVersion);
        List<VFStruct> vfList = new List<VFStruct>();
        foreach (KeyValuePair<string, VFStruct> dict in updateDict)
        {
            vfList.Add(dict.Value);
        }
        totalSize = remoteVersionVo.CalculateVFLSize(vfList);
        SetUpdateState(UpdateState.DOWN_RES_VERSION);
        SetProgress(0, totalSize);

        //开始下载文件
        for (int i = 0; i < vfList.Count; i++)
        {
            string localFile = gameDataPath + vfList[i].file; //当前的本地文件
            if (vfList[i].file == "versionInfo.xml") //忽略versionInfo.xml，不需要下载
            {
                Debug.Log("文件已下载：" + vfList[i].file);
                DownFileFinish(vfList[i]);
                continue;
            }
            string fielDir = Path.GetDirectoryName(localFile);
            if (!Directory.Exists(fielDir))
            {
                Directory.CreateDirectory(fielDir);
            }

            bool fileExist = File.Exists(localFile); //文件是否存在，如果不存在就必须要进行下载
            if (fileExist) //如果文件存在，还需要判断md5
            {
                string localMd5 = ZFileUtil.md5file(localFile);
                if (localMd5.Equals(vfList[i].md5))
                {
                    fileExist = true;
                }
                else
                {
                    File.Delete(localFile);
                    fileExist = false;
                }
            }
            if (!fileExist) //如果文件不存在，则需要进行下载
            {
                StartDownFile(vfList[i]);
                while (!IsDownFinish(vfList[i].file))
                {
                    yield return new WaitForEndOfFrame();
                }
                DownFileFinish(vfList[i]);
            }
            else
            {
                Debug.Log("文件已下载：" + vfList[i].file);
                DownFileFinish(vfList[i]);
                tmpDownedCount++;
                //这里做个分帧循环，防止游戏检测重复已有资源的过程中导致卡顿
                if (tmpDownedCount >= 20)
                {
                    tmpDownedCount = 0;
                    yield return new WaitForEndOfFrame();
                }
            }

        }
        ResDownFinish(true);
    }

    public bool IsDownFinish(string file)
    {
        return downLs.Contains(file);
    }

    /// <summary>
    /// 下载文件
    /// </summary>
    /// <param name="vf"></param>
    public void StartDownFile(VFStruct vf)
    {
        AddEvent(vf, OnDownOneFileComplete);
    }

    /// <summary>
    /// 文件下载完成,网络下载事件的回调，将文件添加到缓存列表，下载完成再删除缓存列表
    /// </summary>
    /// <param name="vf"></param>
    public void OnDownOneFileComplete(VFStruct vf)
    {
        if (vf.size >= 0)
        {
            downLs.Add(vf.file);
        }
        else
        {
            Debug.LogError("文件下载错误：" + vf.file + "\n====错误信息：" + vf.md5);
            //让下载流程继续，但是记录下载错误的信息
            downLs.Add(vf.file);
        }

    }

    /// <summary>
    /// 一个文件下载完成，更新通知当前文件下载完毕，包括更新下载进度等
    /// </summary>
    /// <param name="vf"></param>
    public void DownFileFinish(VFStruct vf)
    {
        currentSize += vf.size;
        SetProgress(currentSize);
    }
    /// <summary>
    /// 资源更新结束
    /// </summary>
    public void ResDownFinish(bool isSucc)
    {
        Debug.LogWarning("=============下载文件数：" + downLs.Count);
        if (isSucc) //资源下载成功，更新本地版本信息
        {
            Debug.Log("==============下载成功==================");
            //将版本差异文件信息写入本地
            OnUpdateFinish(isSucc);
        }
        else
        {
            DoVersionComplete(false, UpdateFailStr.ERROR2);

        }
    }

    /// <summary>
    /// 更新结束，更新本地版本号以及资源内容
    /// </summary>
    /// <param name="isSucc"></param>
    public void OnUpdateFinish(bool isSucc)
    {
        remoteVersionVo.WriteVersionInfo(gameDataPath + "versionInfo.xml");
        DoVersionComplete(isSucc);
    }

    private void DoVersionComplete(bool isSucc, string err = "")
    {
        SetUpdateState(UpdateState.STATE_RES_DOWN_COMPLETE);
        SetProgress(1, 1);

        if (this.completeAction != null)
        {
            if (isSucc)
            {
                SetProgress(1, 1);
                this.completeAction(isSucc, err);
            }
            else
            {
                SetProgress(0, 1);
                this.completeAction(isSucc, err);
            }

        }
    }



    /// <summary>
    /// 获得格式化输出,输入是字节大小
    /// </summary>
    /// <param name="val"></param>
    /// <returns></returns>
    public static string GetFormatFileSize(long val)
    {
        double total = val;
        if (total < 1024)
        {
            return "1k";
        }

        string[] desc = new string[] { "k", "M", "G" };

        for (int i = 0; i < desc.Length; i++)
        {
            double vt = total / 1024.0f;
            if (vt < 1024)
            {
                vt = ((int)(vt * 100)) / 100.0f;

                return vt.ToString("#0.00") + desc[i];
            }
            total = total / 1024.0f;
        }
        total = ((int)(total * 100)) / 100.0f;
        return total.ToString("#0.00") + desc[2];

    }

    void OnDestroy()
    {
        if (thread != null)
        {
            thread.Abort();
        }
    }



}

public struct UpdateFailStr
{
    public const string ERROR1 = "版本检测出错，连接不到服务器";
    public const string ERROR2 = "资源下载失败";
}

public enum UpdateState
{
    START_CHECKING = 0,  //开始检测版本
    INIT_REMOTERES = 1,   //开始初始化远程资源
    RELEASE_RES = 2,    //开始释放资源
    DOWN_APK_VERSION = 3, //下载远程apk版本，
    DOWN_RES_VERSION = 4, //下载版本资源
    STATE_WAITING = 5, //等待确认事件
    STATE_RES_DOWN_COMPLETE = 6,
    STATE_VERSION_DOWN_COMPLETE = 7, //版本下载完毕，抛出事件准备安装
}