using System;
using System.IO;
using System.Linq;
using CandyFlick;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Vuforia;

namespace CandyFlickEditor
{
    public static class ProjectBuilder
    {
        public const string ScenePath="Assets/Scenes/CandyFlick.unity";
        public const string LoadingScenePath="Assets/Scenes/Loading.unity";
        public static string[] StartupScenes => new[]{LoadingScenePath,ScenePath};
        public static void ConfigureStartupScenes()
        {
            EditorBuildSettings.scenes=StartupScenes.Select(path=>new EditorBuildSettingsScene(path,true)).ToArray();
        }
        static T Load<T>(string path) where T:UnityEngine.Object
        {
            var asset=AssetDatabase.LoadAssetAtPath<T>(path);
            if(!asset)throw new InvalidOperationException("Missing asset: "+path);
            return asset;
        }
        static AudioClip Audio(string name)=>Load<AudioClip>("Assets/Original/AudioClip/"+name);

        [MenuItem("Candy Flick/Prepare project")]
        public static void PrepareProject()
        {
            Directory.CreateDirectory("Assets/Scenes");
            Directory.CreateDirectory("Assets/Settings");
            foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{"Assets/Original/AnimationClip"}))
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                clip.legacy=true;
                EditorUtility.SetDirty(clip);
            }
            // Unity 4 materials store old map entries; explicitly reconnect the important original maps.
            RepairMaterials();
            var assets=AssetDatabase.LoadAssetAtPath<GameAssets>("Assets/Resources/GameAssets.asset");
            if(!assets){assets=ScriptableObject.CreateInstance<GameAssets>();AssetDatabase.CreateAsset(assets,"Assets/Resources/GameAssets.asset");}
            assets.omNom=Load<GameObject>("Assets/Resources/Recovered/OmNom.prefab");
            assets.candy=Load<GameObject>("Assets/Resources/Recovered/Candy.prefab");
            assets.alternateCandy=Load<GameObject>("Assets/Resources/Recovered/CandyAlternate.prefab");
            assets.slingshot=Load<GameObject>("Assets/Resources/Recovered/Slingshot.prefab");
            assets.font=Load<Font>("Assets/Original/Font/GoodDog.otf");
            assets.alternateFont=Load<Font>("Assets/Original/Font/segoeprb.ttf");
            assets.languages=new[]{"English","Russian","Dutch","French","German","Italian","Korean","Portuguese","Spanish"}
                .Select(name=>Load<TextAsset>("Assets/Original/TextAsset/"+name+".txt")).ToArray();
            assets.menu=Load<Texture2D>("Assets/Original/Resources/images/iphone5/main_menu.png");
            assets.background=Load<Texture2D>("Assets/Original/Resources/images/all/background_new.png");
            assets.marker=Load<Texture2D>("Assets/Original/Texture2D/NewTarget_scaled.png");
            assets.markerAlternate=Load<Texture2D>("Assets/Original/Texture2D/CandyFlickImageTarget_scaled.png");
            assets.shadow=Load<Texture2D>("Assets/Original/Resources/images/all/shadow.png");
            assets.candyIcon=Load<Texture2D>("Assets/Original/Texture2D/candy_tex-1.png");
            assets.music=Audio("menu_music.mp3");assets.button=Audio("button_02.wav");assets.shoot=Audio("swish_1.wav");
            assets.openMouth=Audio("monster_open.wav");assets.closeMouth=Audio("monster_close.wav");
            assets.chew=Audio("monster_chewing (cut-mp3.com).mp3");assets.sad=Audio("moster_sad.wav");assets.ring=Audio("ring_3.wav");assets.win=Audio("win.wav");
            assets.hits=Enumerable.Range(1,10).Select(i=>Audio("hit_"+i.ToString("00")+".wav")).ToArray();
            assets.misses=new[]{Audio("miss_01.wav"),Audio("miss_05.wav")};
            assets.swishes=Enumerable.Range(1,3).Select(i=>Audio("swish_"+i+".wav")).ToArray();
            assets.elasticShoot=Audio("shot_1_1.wav"); assets.elasticStretch=Audio("stretch_01_1-1.wav");
            assets.tap=Audio("interactive_tap.wav"); assets.loveTouch=Audio("interactive_wink_Om03.wav"); assets.maximumScore=Audio("maximum_score.wav");
            assets.cameraShutter=Audio("Camera.wav");
            foreach(var target in new[]{assets.marker,assets.markerAlternate}) {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(target));
                if(!importer.isReadable || importer.textureCompression!=TextureImporterCompression.Uncompressed) {
                    importer.isReadable=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();
                }
            }
            EditorUtility.SetDirty(assets);
            ConfigureAndroid();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var root=new GameObject("Candy Flick");root.AddComponent<CandyFlickGame>().assets=assets;
            EditorSceneManager.SaveScene(scene,ScenePath);
            ConfigureStartupScenes();
            AssetDatabase.SaveAssets();
            Debug.Log("CANDY_FLICK_PROJECT_READY: Unity "+Application.unityVersion+", original assets, Player Settings preserved, Vuforia image tracking with the original 2.6 target database (optional ARCore Ground mode).");
        }

        static void RepairMaterials()
        {
            foreach(string guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/Original"}))
            {
                string path=AssetDatabase.GUIDToAssetPath(guid);
                var material=Load<Material>(path);
                string sourcePath=path.Replace("Assets/Original/","assetripper/project/ExportedProject/Assets/");
                if(!File.Exists(sourcePath))continue;
                string yaml=File.ReadAllText(sourcePath);
                var match=System.Text.RegularExpressions.Regex.Match(yaml,@"name: _MainTex\s+second:\s+m_Texture: \{fileID: \d+, guid: ([a-f0-9]+)");
                if(match.Success)
                {
                    var tex=AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(match.Groups[1].Value));
                    if(tex)material.mainTexture=tex;
                }
                EditorUtility.SetDirty(material);
            }
        }

        public static void ConfigureAndroid()
        {
            // Prepare runtime dependencies only. Build identity, signing, icons and every
            // other Player Setting belong to the project and must survive preparation/builds.
            ConfigureVuforia();
            foreach(string path in new[]{"Assets/AppIcon/icon_dynamic_front.png","Assets/AppIcon/icon_dynamic_back.png"}) {
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                if(importer && importer.textureCompression!=TextureImporterCompression.Uncompressed) { importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport(); }
            }
            var icon=AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Original/Brand/AppIcon.png");
            if(icon) {
                var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(icon));
                if(importer.textureCompression!=TextureImporterCompression.Uncompressed) { importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport(); }
            }
            var graphics=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var shaders=graphics.FindProperty("m_AlwaysIncludedShaders");
            // BulletBehaviour selects its original fade shader by name at runtime.
            foreach(string shaderName in new[]{"Unlit/Texture","Sprites/Default","Transparent/VertexLit"})
            {
                Shader shader=Shader.Find(shaderName);bool included=false;
                for(int i=0;i<shaders.arraySize;i++)if(shaders.GetArrayElementAtIndex(i).objectReferenceValue==shader)included=true;
                if(!included){int index=shaders.arraySize;shaders.InsertArrayElementAtIndex(index);shaders.GetArrayElementAtIndex(index).objectReferenceValue=shader;}
            }
            graphics.ApplyModifiedPropertiesWithoutUndo();
        }

        public static void ConfigureVuforia()
        {
            const string path="Assets/Resources/VuforiaConfiguration.asset";
            var asset=AssetDatabase.LoadAssetAtPath<VuforiaConfiguration>(path);
            if(!asset) { asset=UnityEngine.Object.Instantiate(VuforiaConfiguration.Instance);AssetDatabase.CreateAsset(asset,path); }
            foreach(var config in new[]{asset,VuforiaConfiguration.Instance}) {
            if(File.Exists("key.txt"))config.Vuforia.LicenseKey=File.ReadAllText("key.txt").Trim();
            config.Vuforia.DelayedInitialization=true;
            config.Vuforia.CameraFocusMode=FocusMode.FOCUS_MODE_CONTINUOUSAUTO;
            config.Vuforia.CameraDeviceMode=CameraMode.MODE_OPTIMIZE_QUALITY;
            config.Vuforia.MaxSimultaneousImageTargets=1;
            config.Vuforia.VirtualSceneScaleFactor=1;
            config.DeviceTracker.AutoInitAndStartTracker=false;
            config.DeviceTracker.ARCoreRequirementSetting=VuforiaConfiguration.DeviceTrackerConfiguration.ARCoreRequirement.OPTIONAL;
            config.DeviceTracker.AutoImportArcoreSetting=true;
            config.VideoBackground.VideoBackgroundEnabled=true;
            EditorUtility.SetDirty(config);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem("Candy Flick/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureAndroid();
            if(string.IsNullOrWhiteSpace(VuforiaConfiguration.Instance.Vuforia.LicenseKey))throw new InvalidOperationException("Place the port's Vuforia Basic key in key.txt before building Android.");
            AssetDatabase.SaveAssets();
            EditorUserBuildSettings.buildAppBundle=false;
            Build("Builds/Android/CandyFlick.apk",BuildTarget.Android);
        }
        [MenuItem("Candy Flick/Build Android App Bundle")]
        public static void BuildAndroidBundle()
        {
            ConfigureAndroid();AssetDatabase.SaveAssets();EditorUserBuildSettings.buildAppBundle=true;
            Build("Builds/Android/CandyFlick.aab",BuildTarget.Android);
        }
        [MenuItem("Candy Flick/Build Windows preview")]
        public static void BuildWindows()=>Build("Builds/Windows/CandyFlick.exe",BuildTarget.StandaloneWindows64);
        static void Build(string output,BuildTarget target)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=StartupScenes,locationPathName=output,target=target,options=BuildOptions.None});
            File.WriteAllText("Documentation/last-build.json",JsonUtility.ToJson(new BuildEvidence{target=target.ToString(),result=report.summary.result.ToString(),errors=(int)report.summary.totalErrors,warnings=(int)report.summary.totalWarnings,bytes=report.summary.totalSize.ToString(),output=Path.GetFullPath(output)},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Build failed: "+report.summary.result);
            Debug.Log("CANDY_FLICK_BUILD_SUCCEEDED: "+Path.GetFullPath(output));
        }
        [Serializable]sealed class BuildEvidence{public string target,result,bytes,output;public int errors,warnings;}
    }
}
