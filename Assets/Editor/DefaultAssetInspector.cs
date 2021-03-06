﻿using UnityEngine;
using UnityEditor;
using System.IO;

using NPOI.HSSF.UserModel;
using NPOI.SS.UserModel;
using NPOI.XSSF.UserModel;
using System;
using System.Linq;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class SheetStruct
{
    public string Name;
    public ISheet Sheet { get; set; }

    public int RowIdxA = 1;
    public int ColumnIdxA = 0;

    public int RowPerPage = 10;
    public int ColumnPerPage = 5;
    public string PinColumn = "";
    int PinColumnIdx
    {
        get
        {
            return NPOI.ExcelExtension.NameColumn(PinColumn);
        }
    }

    public void DrawInspector(int indent = 0, GUILayoutOption[] guiOpts = null)
    {
        RowIdxA = (int)RowIdxA < 0 ? 0 : RowIdxA;
        RowPerPage = (int)RowPerPage < 0 ? 0 : RowPerPage;
        ColumnIdxA = (int)ColumnIdxA < 0 ? 0 : ColumnIdxA;
        ColumnPerPage = (int)ColumnPerPage < 0 ? 0 : ColumnPerPage;

        Sheet.SheetName = Name;
        PinColumn = PinColumn.ToUpper();

        var hasPinColumn = !string.IsNullOrEmpty(PinColumn);

        var head = Sheet.Row(0);
        // column name
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("\\", new GUILayoutOption[]
        {
            GUILayout.Width(30),
            GUILayout.ExpandWidth(false),
        });
        if(hasPinColumn)
        {
            EditorGUILayout.LabelField(PinColumn, guiOpts);
        }
        for(int i = (int)ColumnIdxA; i < head.LastCellNum && i < (int)(ColumnIdxA + ColumnPerPage - (hasPinColumn ? 1 : 0)); ++i)
        {
            EditorGUILayout.LabelField(NPOI.ExcelExtension.ColumnName(i), guiOpts);
        }
        EditorGUILayout.EndHorizontal();

        // head
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField((head.Cell(0).RowIndex + 1).ToString(), new GUILayoutOption[]
        {
            GUILayout.Width(30),
            GUILayout.ExpandWidth(false),
        });
        if (hasPinColumn)
        {
            head.Cell(PinColumnIdx).Draw(null, guiOpts);
            // EditorGUILayout.LabelField(head.Cell(PinColumnIdx).SValue(), guiOpts);
        }
        head.Draw((int)ColumnIdxA, (int)(ColumnIdxA + ColumnPerPage - (hasPinColumn ? 1 : 0)), guiOpts, false);
        EditorGUILayout.EndHorizontal();

        // content
        var Rows = Sheet.Rows((int)RowIdxA, (int)(RowIdxA + RowPerPage));
        foreach (var r in Rows)
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField((r.Cell(0).RowIndex + 1).ToString(), new GUILayoutOption[]
            {
                GUILayout.Width(30),
                GUILayout.ExpandWidth(false),
            });
            if (hasPinColumn)
            {
                r.Cell(PinColumnIdx).Draw(null, guiOpts);
            }
            r.Draw((int)ColumnIdxA, (int)(ColumnIdxA + ColumnPerPage - (hasPinColumn ? 1 : 0)), guiOpts, false);
            EditorGUILayout.EndHorizontal();
        }

        var rect = EditorGUILayout.GetControlRect();
        var sn = 5;
        var idx = -1;
        if (GUI.Button(rect.Split(++idx, sn), "PageDown"))
        {
            RowIdxA += RowPerPage;
        }
        if (GUI.Button(rect.Split(++idx, sn), "PageUp"))
        {
            RowIdxA -= RowPerPage;
        }
        // PinColumn = (int)PinColumn < 0 ? 0 : PinColumn;
    }
}

public class BookStruct
{
    public string Name;
    public IWorkbook Book {get; set;}
    public List<SheetStruct> Sheets = new List<SheetStruct>();
    public void DrawInspector(int indent = 0, GUILayoutOption[] guiOpts = null)
    {
        // base.DrawInspector();
        var rect = EditorGUILayout.GetControlRect();
        var sn = 5;
        var idx = -1;
        if (GUI.Button(rect.Split(++idx, sn), "Save"))
        {
            Book.Write(Name);
        }

        guiOpts = new GUILayoutOption[]
        {
                GUILayout.Width(30),
                GUILayout.ExpandWidth(true),
        };
        foreach (var s in Sheets)
        {
            Inspector.DrawComObj(s.Name, s, null, () => {
                s.DrawInspector(0, guiOpts);
            });
        }

        if(GUI.changed)
        {
            if (Book is XSSFWorkbook)
            {
                XSSFFormulaEvaluator.EvaluateAllFormulaCells(Book);
            }
            else
            {
                HSSFFormulaEvaluator.EvaluateAllFormulaCells(Book);
            }
        }
    }
}

[CanEditMultipleObjects, CustomEditor(typeof(DefaultAsset))]
public partial class DefaultAssetInspector : Editor
{
    object mTarget = null;
    string assetPath = null;
    public void OnEnable()
    {
        assetPath = AssetDatabase.GetAssetPath(target);
        if (assetPath.IsExcel())
        {
            var book = ExcelUtils.Open(assetPath);
            var tmp = new BookStruct();
            tmp.Name = assetPath;
            tmp.Book = book;
            foreach(var sheet in book.AllSheets())
            {
                var ss = new SheetStruct();
                ss.Name = sheet.SheetName;
                ss.Sheet = sheet;
                tmp.Sheets.Add(ss);
            }
            mTarget = tmp;
        }
    }

    void DrawText()
    {
        string luaFile = File.ReadAllText(assetPath);
        string text;
        if (base.targets.Length > 1)
        {
            text = Path.GetFileName(assetPath);
        }
        else
        {
            var MaxTextPreviewLength = EditorConfig.Instance().MConfig.MaxTextPreviewLength;
            text = luaFile;
            if (text.Length > MaxTextPreviewLength + 3)
            {
                text = text.Substring(0, MaxTextPreviewLength) + "...";
            }
        }

        GUIStyle style = "ScriptText";
        Rect rect = GUILayoutUtility.GetRect(new GUIContent(text), style);
        rect.x = 0f;
        rect.y -= 3f;
        rect.width = EditorGUIUtility.currentViewWidth + 1f;
        GUI.Box(rect, text, style);
    }

    public override void OnInspectorGUI()
    {
        Inspector.DrawComObj("EditorConfig", EditorConfig.Instance().MConfig);

        bool enabled = GUI.enabled;
        GUI.enabled = true;
        if(!File.Exists(assetPath))return;
        if(assetPath.IsExcel())
        {
            
            if (mTarget != null)
            {
                (mTarget as BookStruct).DrawInspector();
            }
        }
        else if (assetPath.IsText())
        {
            DrawText();
        }
        GUI.enabled = enabled;
    }
}