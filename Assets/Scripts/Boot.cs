﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class Boot : SingleMono<Boot>
{
    // Use this for initialization
    public override IEnumerator Init()
    {
        AppLog.d("App.Init 0 AssetSys");
        yield return AssetSys.Instance.Init();

        AppLog.d("App.Init 1 LuaSys");
        yield return LuaSys.Instance.Init();

        AppLog.d("App.Init 1 boot");
        GameObject root = null;
        yield return AssetSys.Instance.GetAsset<GameObject>("ui/boot/boot.prefab", obj =>
        {
            root = obj;
        });
        var ui = GameObject.Instantiate(root);
        var uiluamono = ui.GetComponent<LuaMonoBehaviour>();
        uiluamono.enabled = true;
    }

    private void Awake()
    {
        AppLog.isEditor  = Application.isEditor;
        AppLog.d("App.Awake 0");
        StartCoroutine(Init());
    }

}
