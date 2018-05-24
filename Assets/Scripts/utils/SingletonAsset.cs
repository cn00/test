﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SingletonAssetBase<T> : ScriptableObject
{
    protected static string AssetPath
    {
        get
        {
            return "Assets/config/" + typeof(T) + ".asset";
        }
    }

    public virtual bool Init()
    {
        return true;
    }

#if UNITY_EDITOR
    public virtual void Save()
    {
        EditorUtility.SetDirty(this);
        AssetDatabase.WriteImportSettingsIfDirty(AssetPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    public void DrawSaveButton()
    {
        var rect = EditorGUILayout.GetControlRect();
        if (GUI.Button(rect.Split(1, 3), "Save Asset"))
        {
            Save();
        }
    }
#endif
}

public class SingletonAsset<T> : SingletonAssetBase<T> where T : SingletonAssetBase<T>
{
    protected static T mInstance = null;

    public static T InstanceRuntime()
    {
        if (mInstance == null)
        {
            // from bundle
            var assetSubPath = AssetPath.Replace("Assets/", "config/");
            AppLog.d("InstanceRuntime 0 {0}", assetSubPath);
            var cachePath = AssetSys.CacheRoot + AssetSys.Instance.GetBundlePath(assetSubPath);
            AppLog.d("InstanceRuntime 0.1 {0}", cachePath);
            if (File.Exists(cachePath))
            {
                AppLog.d("InstanceRuntime 1");
                var bundle = AssetBundle.LoadFromFile(cachePath);
                mInstance = bundle.LoadAsset<T>(BuildConfig.BundleResRoot + assetSubPath);
                AppLog.d("InstanceRuntime 2");
            }
            // from resources
            else
            {
                var respath = AssetPath.Replace("Assets/", "").Replace(".asset", "");
                mInstance = Resources.Load<T>(respath);
                AppLog.d("InstanceRuntime 3 Resources.Load {0}", respath);
            }

            if (mInstance == null)
            {
                AppLog.e("InstanceRuntime 4 CreateInstance {0}", typeof(T));
                mInstance = CreateInstance<T>();
            }
            mInstance.Init();
        }
        return mInstance;
    }

    public static T Instance()
    {
        AppLog.isEditor = Application.isEditor;
        T tmp = null;
#if UNITY_EDITOR
        tmp = InstanceEditor();
#else
        tmp = InstanceRuntime();
#endif
        return tmp;
    }

#if UNITY_EDITOR
    public static T InstanceEditor()
    {
        if (mInstance == null)
        {
            mInstance = AssetDatabase.LoadAssetAtPath<T>(AssetPath);
            if (mInstance == null)
            {
                var dir = Path.GetDirectoryName(AssetPath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                mInstance = CreateInstance<T>();
                AssetDatabase.CreateAsset(mInstance, AssetPath);
            }
            mInstance.Init();
        }
        return mInstance;
    }

    public static void DrawListCount<T2>(List<T2> list)
    {
        var size = list.Count;
        size = EditorGUILayout.DelayedIntField(size);
        if (size < list.Count)
        {
            list.RemoveRange(size, list.Count - size);
        }
        else if (size > list.Count)
        {
            for (var i = list.Count; i < size; ++i)
                list.Add(default(T2));
        }
    }

#endif //UNITY_EDITOR
}
