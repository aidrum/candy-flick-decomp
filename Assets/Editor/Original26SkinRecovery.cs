using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    /// <summary>Restore the original 2.6 GUISkins, without designing replacement styles.</summary>
    public static class Original26SkinRecovery
    {
        [Serializable] public sealed class Collection { public SkinRecord[] skins; }
        [Serializable] public sealed class SkinRecord
        {
            public string name,font,sha256;
            public StyleRecord[] builtins,custom;
            public SettingsRecord settings;
            public int bytes_consumed;
        }
        [Serializable] public sealed class SettingsRecord
        {
            public bool doubleClickSelectsWord,tripleClickSelectsLine;
            public float[] cursorColor,selectionColor;
            public float cursorFlashSpeed;
        }
        [Serializable] public sealed class StyleRecord
        {
            public string name,font;
            public StateRecord[] states;
            public int[] border,margin,padding,overflow;
            public int fontSize,fontStyle,alignment,textClipping,imagePosition;
            public bool wordWrap,richText,stretchWidth,stretchHeight;
            public float[] contentOffset;
            public float fixedWidth,fixedHeight;
        }
        [Serializable] public sealed class StateRecord { public string background;public float[] color; }

        static Color ColorFrom(float[] data)=>new Color(data[0],data[1],data[2],data[3]);
        static RectOffset OffsetFrom(int[] data)=>new RectOffset(data[0],data[1],data[2],data[3]);
        static T Asset<T>(string path) where T:UnityEngine.Object
        {
            if(string.IsNullOrEmpty(path))return null;
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!asset)throw new InvalidOperationException("Unresolved original 2.6 skin binding: "+path);
            return asset;
        }
        static GUIStyle Restore(StyleRecord source)
        {
            var target=new GUIStyle{name=source.name,font=Asset<Font>(source.font),fontSize=source.fontSize,fontStyle=(FontStyle)source.fontStyle,
                alignment=(TextAnchor)source.alignment,wordWrap=source.wordWrap,richText=source.richText,
                clipping=(TextClipping)source.textClipping,imagePosition=(ImagePosition)source.imagePosition,
                contentOffset=new Vector2(source.contentOffset[0],source.contentOffset[1]),fixedWidth=source.fixedWidth,fixedHeight=source.fixedHeight,
                stretchWidth=source.stretchWidth,stretchHeight=source.stretchHeight,border=OffsetFrom(source.border),margin=OffsetFrom(source.margin),
                padding=OffsetFrom(source.padding),overflow=OffsetFrom(source.overflow)};
            var states=new[]{target.normal,target.hover,target.active,target.focused,target.onNormal,target.onHover,target.onActive,target.onFocused};
            for(int i=0;i<states.Length;i++){states[i].background=Asset<Texture2D>(source.states[i].background);states[i].textColor=ColorFrom(source.states[i].color);}
            return target;
        }

        [MenuItem("Candy Flick/Recover original 2.6 GUISkins")]
        public static void Recover()
        {
            string json=File.ReadAllText("Documentation/Recovered/guiskins26.json");
            // Native built-in defaults can point into Unity 4's engine resources. Those are not
            // used by the game's custom styles. The extractor records them separately.
            var records=JsonUtility.FromJson<Collection>("{\"skins\":"+json+"}");
            var properties=new[]{"box","button","toggle","label","textField","textArea","window","horizontalSlider","horizontalSliderThumb",
                "verticalSlider","verticalSliderThumb","horizontalScrollbar","horizontalScrollbarThumb","horizontalScrollbarLeftButton",
                "horizontalScrollbarRightButton","verticalScrollbar","verticalScrollbarThumb","verticalScrollbarUpButton","verticalScrollbarDownButton","scrollView"};
            foreach(var record in records.skins)
            {
                string path="Assets/Original/Resources/skins/"+record.name+".guiskin";
                var skin=AssetDatabase.LoadAssetAtPath<GUISkin>(path);
                if(!skin)throw new InvalidOperationException("Unity did not import the original GUISkin asset: "+path);
                skin.font=Asset<Font>(record.font);
                for(int i=0;i<properties.Length;i++)typeof(GUISkin).GetProperty(properties[i]).SetValue(skin,Restore(record.builtins[i]));
                skin.customStyles=record.custom.Select(Restore).ToArray();
                skin.settings.doubleClickSelectsWord=record.settings.doubleClickSelectsWord;
                skin.settings.tripleClickSelectsLine=record.settings.tripleClickSelectsLine;
                skin.settings.cursorColor=ColorFrom(record.settings.cursorColor);
                skin.settings.cursorFlashSpeed=record.settings.cursorFlashSpeed;
                skin.settings.selectionColor=ColorFrom(record.settings.selectionColor);
                EditorUtility.SetDirty(skin);
                Debug.Log("RECOVERED_26_SKIN: "+record.name+"; "+skin.customStyles.Length+" original custom styles; "+record.bytes_consumed+" source bytes.");
            }
            AssetDatabase.SaveAssets();
        }
    }
}
