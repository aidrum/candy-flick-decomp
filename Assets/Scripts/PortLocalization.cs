using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CandyFlick
{
    // Port-only UI text. Protocol messages remain language-neutral English keys;
    // each receiving device formats them in its own selected game language.
    public static class PortLocalization
    {
        [Serializable] public sealed class Entry { public string key; public string[] text; }
        [Serializable] public sealed class Catalog { public string[] languages; public Entry[] entries; }
        sealed class Template { public Entry entry; public Regex match; public int[] arguments; }
        static Dictionary<string,Entry> strings;
        static Template[] templates;
        public static int Language {get;set;}
        public static Catalog Data {get;private set;}
        static void Load()
        {
            if(strings!=null)return;
            Data=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Localization/port").text);
            strings=Data.entries.ToDictionary(e=>e.key,StringComparer.Ordinal);
            templates=Data.entries.Where(e=>e.key.Contains("{0}")).Select(e=>
            {
                var indices=new List<int>();var pattern=new StringBuilder();int offset=0;
                bool numeric=e.key=="{0}s"||e.key=="{0} points"||e.key.StartsWith("Room ",StringComparison.Ordinal)||e.key.EndsWith("players calibrated",StringComparison.Ordinal);
                foreach(Match placeholder in Regex.Matches(e.key,@"\{(\d+)\}"))
                {
                    pattern.Append(Regex.Escape(e.key.Substring(offset,placeholder.Index-offset))).Append(numeric?"([0-9]+)":"(.*?)");
                    indices.Add(int.Parse(placeholder.Groups[1].Value));offset=placeholder.Index+placeholder.Length;
                }
                pattern.Append(Regex.Escape(e.key.Substring(offset)));
                return new Template{entry=e,match=new Regex("\\A"+pattern+"\\z",RegexOptions.Singleline),arguments=indices.ToArray()};
            }).ToArray();
        }
        static string Value(Entry entry)=>entry.text[Mathf.Clamp(Language,0,8)];
        public static string Translate(string source)
        {
            if(string.IsNullOrEmpty(source)||Language==0)return source;
            Load();
            if(strings.TryGetValue(source,out var entry))return Value(entry);
            foreach(var template in templates)
            {
                var match=template.match.Match(source);if(!match.Success)continue;
                object[] arguments=new object[template.arguments.Max()+1];
                for(int i=0;i<template.arguments.Length;i++)arguments[template.arguments[i]]=match.Groups[i+1].Value;
                return string.Format(CultureInfo.InvariantCulture,Value(template.entry),arguments);
            }
            return source; // Native localized strings, player names and SDK details.
        }
        public static string Format(string source,params object[] arguments)=>string.Format(CultureInfo.InvariantCulture,Translate(source),arguments);
        public static Catalog CatalogData(){Load();return Data;}
    }

    public sealed partial class CandyFlickUI
    {
        string L(string source,params object[] arguments)=>arguments.Length==0?PortLocalization.Translate(source):PortLocalization.Format(source,arguments);
        Font koreanPortFont;
        Font PortFont=>text.CurrentLanguage==6?(koreanPortFont?koreanPortFont:(koreanPortFont=Resources.Load<Font>("Localization/CandyFlickKorean"))):text.CurrentLanguage==1?game.assets.alternateFont:game.assets.font;
        void LanLabel(Rect rect,string message,GUIStyle style)
        {
            message=L(message);style=new GUIStyle(style);
            while(style.fontSize>16&&style.CalcHeight(new GUIContent(message),rect.width)>rect.height)style.fontSize--;
            GUI.Label(rect,message,style);
        }
    }
}
