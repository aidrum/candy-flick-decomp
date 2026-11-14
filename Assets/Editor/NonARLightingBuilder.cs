using UnityEditor;
using UnityEngine;

namespace CandyFlickEditor
{
    public static class NonARLightingBuilder
    {
        public static void Apply()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Resources/NonARMode/Materials"}))
            {
                var material=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                void Set(string name,float value){if(material.HasProperty(name))material.SetFloat(name,value);}
                Set("_Glossiness",.08f);Set("_GlossMapScale",.08f);Set("_Metallic",0);
                Set("_SpecularHighlights",0);Set("_GlossyReflections",0);
                material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");material.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
                if(material.name.StartsWith("candyflick_table"))material.color=Color.white;
                EditorUtility.SetDirty(material);
            }
            AssetDatabase.SaveAssets();
        }
    }
}
