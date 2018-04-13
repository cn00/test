﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;
using System.Linq;
using XLua;
using System.Text;

using BundleManifest = System.Collections.Generic.List<BundleConfig.GroupInfo>;

public static class BytesExtension
{
    public static string Utf8String(this byte[] self)
    {
        return Encoding.UTF8.GetString(self);
    }
}

[LuaCallCSharp]
public class UpdateSys : SingleMono<UpdateSys>
{

    Version mLocalVersion;
    Version mRemoteVersion;

    BundleManifest mLocalManifest = new  BundleManifest();//<string/*path*/, Md5SchemeInfo>
    BundleManifest mRemoteManifest = new BundleManifest();//<string/*path*/, Md5SchemeInfo>

    object mDiffListLock = new object();
    List<BundleConfig.BundleInfo> mDiffList = new List<BundleConfig.BundleInfo>();// <string/*path*/, Md5SchemeInfo>

    bool mAllDownloadOK = false;

    public bool SysEnter()
    {
        return true;
    }

    public override IEnumerator Init()
    {
        yield return base.Init();
    }

    public IEnumerator GetLocalVersion()
    {
        var cacheUrl = AssetSys.CacheRoot + "resversion.txt";
        var localVersionUrl = "file://" + cacheUrl;
        AppLog.d("GetLocalVersion: {0}", localVersionUrl);

        if(File.Exists(cacheUrl))
        {
            yield return AssetSys.Www(localVersionUrl, (WWW www) =>
            {
                if(string.IsNullOrEmpty(www.error))
                {
                    // 覆盖硬编码版本号
                    BundleConfig.Instance().Version = new AppVersion(www.text.Trim());
                }
                else
                {
                    AppLog.e(www.error);
                }
            });
        }
        mLocalVersion = BundleConfig.Instance().Version.V;
        AppLog.d("LocalVersion {0}", mLocalVersion.ToString());

        yield return null;
    }

    public IEnumerator GetRemoteVersion()
    {
        var remoteVersionUrl = AssetSys.HttpRoot + "resversion.txt";
        AppLog.d(remoteVersionUrl);
        yield return AssetSys.Www(remoteVersionUrl, (WWW www) =>
        {
            if(string.IsNullOrEmpty(www.error))
            {
                mRemoteVersion = new Version(www.text.Trim());
                AppLog.d("RemoteVersion {0}", mRemoteVersion.ToString());
            }
            else
            {
                mRemoteVersion = mLocalVersion;
                AppLog.e(remoteVersionUrl + ": " + www.error);
            }
        });
        yield return null;
    }

    public IEnumerator GetLocalManifest()
    {
        var cachePath = BundleConfig.LocalManifestPath;

        if(!File.Exists(cachePath))
        {
            yield break;
        }

        //var s = File.ReadAllText(cachePath);
        //mLocalManifest = YamlHelper.Deserialize<BundleManifest>(s);

        var localMd5Url = "file://" + cachePath;
        yield return AssetSys.Www(localMd5Url, (WWW www) =>
        {
            if(string.IsNullOrEmpty(www.error))
            {
                mLocalManifest = YamlHelper.Deserialize<BundleManifest>(www.text);
            }
            else
            {
                AppLog.e("get local md5 list error: " + www.error);
            }
        });

        yield return null;
    }

    public IEnumerator GetRemoteManifest()
    {
        var remoteManifestUrl = AssetSys.HttpRoot + mRemoteVersion + "/" + BundleConfig.ManifestName + BundleConfig.CompressedExtension;

        byte[] bytes = null;
        yield return AssetSys.Www(remoteManifestUrl, (WWW www) =>
        {
            if(string.IsNullOrEmpty(www.error))
            {
                bytes = www.bytes;
            }
            else
            {
                AppLog.e(remoteManifestUrl+ ": " + www.error);
            }
        });
        var outStream = new MemoryStream();
        BundleHelper.DecompressFileLZMA(new MemoryStream(bytes), outStream);
        //AssetSys.AsyncSave(cachePath, outStream.GetBuffer(), outStream.Length);

        var s = outStream.GetBuffer().Utf8String();
        mRemoteManifest = YamlHelper.Deserialize<BundleManifest>(s);
        outStream.Dispose();
        yield return null;
    }

    /// <summary>
    /// 下载差异资源
    /// </summary>
    public IEnumerator DownloadDiffFiles()
    {
        int count = 0;
        foreach(var i in mDiffList)
        {
            var subPath = i.Name;
            var cachePath = AssetSys.CacheRoot + subPath;
            var cacheLzmaPath = AssetSys.CacheRoot + subPath + BundleConfig.CompressedExtension;
            var diffFileUrl = AssetSys.HttpRoot +  mRemoteVersion + "/" + subPath + BundleConfig.CompressedExtension;
            AppLog.d(diffFileUrl);

            //yield return AssetSys.Www(fileUrl, (WWW www) =>
            var task = AssetSys.Www(diffFileUrl, (WWW www) =>
            {
                if(string.IsNullOrEmpty(www.error))
                {
                    var dir = Path.GetDirectoryName(cachePath);
                    if(!Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }
                    //// 异步存盘
                    //MemoryStream outStream = new MemoryStream();
                    //BundleHelper.DecompressFileLZMA(new MemoryStream(www.bytes), outStream);
                    //AssetSys.AsyncSave(cachePath, outStream.GetBuffer(), outStream.Length);
                    AssetSys.AsyncSave(cacheLzmaPath, www.bytes);

                    // or 同步存盘
                    AppLog.d("update: {0}", cachePath);
                    var fstream = new FileStream(cachePath, FileMode.Create);
                    BundleHelper.DecompressFileLZMA(new MemoryStream(www.bytes), fstream);

                    ++count;
                    if(count == mDiffList.Count)
                        mAllDownloadOK = true;
                    Updated(subPath);
                }
                else
                {
                    AppLog.e("DownloadDiffFiles: " + i + www.error);
                }
            });

            if(count % 10 == 0)
                yield return StartCoroutine(task);
            else
                StartCoroutine(task);
        }
    }

    /// <summary>
    /// 需要更新的资源
    /// </summary>
    public void Diff()
    {
        foreach(var group in mRemoteManifest)
        {
            foreach(var i in group.Bundles)
            {
                var lgroup = mLocalManifest.Find(j => j.Name == group.Name);
                var lversion = "";
                if(lgroup != null)
                {
                    var lbundle = lgroup.Bundles.Find(l => l.Name == i.Name);
                    if(lbundle.Md5 == i.Md5)
                        continue;
                    lversion = lbundle.Md5;
                }
                AppLog.d("diff: {0}:[{1}-{2}]", i.Name, i.Md5, lversion);
                mDiffList.Add(i);
            }
        }
    }

    /// <summary>
    /// 检查更新
    /// </summary>
    public IEnumerator CheckUpdate()
    {
        var isOK = false;
        // 是否需要更新
        yield return GetLocalVersion();
        yield return GetRemoteVersion();
        //if(LocalVersion != RemoteVersion)// 允许回档到历史版本?
        {
            // download md5 list & uncompress & clean zip
            yield return GetLocalManifest();
            yield return GetRemoteManifest();

            Diff();

            yield return DownloadDiffFiles();

            BundleConfig.Instance().Version = mRemoteVersion;

            // 更新完成后保存
            var cacheUrl = AssetSys.CacheRoot + "resversion.txt";
            var strRemoteVersion = mRemoteVersion.ToString();
            byte[] bytes = System.Text.Encoding.Default.GetBytes(strRemoteVersion);
            AssetSys.AsyncSave(cacheUrl, bytes);
        }
        //else
        //{
        //    AppLog.d("no update");
        //}

        yield return null;
    }

    public bool NeedUpdate(string subPath)
    {
        BundleConfig.BundleInfo info = mDiffList.Find(i=>i.Name == subPath);
        return info != null;
    }

    /// <summary>
    /// 将更新过的md5更新到本地
    /// </summary>
    public void Updated(string subPath)
    {
        AppLog.d("Updated: {0}", subPath);
        var dirs = subPath.Split('/');
        //lock(mDiffListLock)
        {
            var localGroup = mLocalManifest.Find(i => i.Name == dirs[0]);
            if(localGroup == null)
            {
                localGroup = new BundleConfig.GroupInfo()
                {
                    Name = dirs[0]
                };
                mLocalManifest.Add(localGroup);
            }

            var newi = mDiffList.Find(i => i.Name == subPath);
            if(newi != null)
            {
                var old = localGroup.Bundles.Find(i => i.Name == subPath);
                localGroup.Bundles.Remove(old);
                localGroup.Bundles.Add(newi);
                SaveManifest(mLocalManifest, BundleConfig.LocalManifestPath);

                mDiffList.Remove(newi);
                AppLog.d("Updated: {0}={1}", newi.Name, newi.Md5);
            }
            
            AssetSys.Instance.UnloadBundle(subPath, false);
        }
    }

    public static void SaveManifest(BundleManifest manifest, string path)
    {
        var yaml = YamlHelper.Serialize(manifest, path);
    }
}
