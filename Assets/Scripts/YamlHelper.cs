﻿using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

public class YamlHelper
{
    public static string Serialize<T>(T obj)
    {
        var serializer = new YamlDotNet.Serialization.SerializerBuilder()
            // .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(obj);
        return yaml;
    }
    public static string Serialize<T>(T obj, string path)
    {
        var yaml = Serialize(obj);

        var dir = Path.GetDirectoryName(path);
        if(!Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var writer = new StreamWriter(path);
        writer.Write(yaml);
        writer.Flush();
        writer.Close();
        return yaml;
    }

    public static T Deserialize<T>(MemoryStream stream)
    {
        return Deserialize<T>(stream.GetBuffer().Utf8String());
    }

    public static T Deserialize<T>(string yaml)
    {
        var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
        var obj = deserializer.Deserialize<T>(yaml);
        return obj;
    }
}
