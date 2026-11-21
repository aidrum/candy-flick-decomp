using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CandyFlick;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CandyFlickEditor
{
    [InitializeOnLoad]
    public static class ModelCreditsValidation
    {
        const string Key="CandyFlickModelCreditsTest",Folder="Documentation/ModelCredits";
        static CandyFlickGame game;static CandyFlickUI ui;static int stage;static double next;
        static readonly List<string> checks=new List<string>();
        static ModelCreditsValidation(){if(SessionState.GetBool(Key,false)){EditorApplication.update+=Tick;Application.logMessageReceived+=OnLog;}}
        public static void Run()
        {
            SessionState.SetBool(Key+"Audio",AudioListener.pause);
            SessionState.SetString(Key+"Scene",EditorSceneManager.playModeStartScene?AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene):"");
            EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(ProjectBuilder.ScenePath);
            Directory.CreateDirectory(Folder);if(File.Exists(Folder+"/failure.txt"))File.Delete(Folder+"/failure.txt");
            Resolution(640,1136);SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();
        }
        static void Resolution(int w,int h)=>typeof(Original26VisualValidation).GetMethod("SetReferenceResolution",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{w,h});
        static void Check(bool value,string label){if(!value)throw new Exception(label);checks.Add(label);}
        static void Click(Vector2 point)=>typeof(SkinMenuValidation).GetMethod("Click",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{point});
        static void Capture(string name)=>ScreenCapture.CaptureScreenshot(Folder+"/"+name+".png");
        static void ShowAbout(){game.Show(GameScreen.Settings);typeof(CandyFlickUI).GetField("settingsPage",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(ui,"About");}
        static void Tick()
        {
            if(!EditorApplication.isPlaying||EditorApplication.timeSinceStartup<next)return;
            next=EditorApplication.timeSinceStartup+.8;
            try
            {
                if(!game){game=UnityEngine.Object.FindAnyObjectByType<CandyFlickGame>();if(!game)return;ui=game.GetComponent<CandyFlickUI>();AudioListener.pause=true;Application.runInBackground=true;return;}
                switch(stage++)
                {
                    case 0: game.ReturnToMenu();ShowAbout();break;
                    case 1:
                        Check(ui.CreditsNextRect.xMin>320&&Mathf.Abs(ui.CreditsNextRect.xMin-(640-ui.CreditsBackRect.xMax))<.01f,"Main credits forward arrow mirrors the native Back button on the right");
                        Capture("credits-main");break;
                    case 2: Click(ui.CreditsNextRect.center);break;
                    case 3:
                        Check(game.ScreenState==GameScreen.Settings&&ui.SettingsPage=="ModelCredits","Mirrored right button opens Model Credits");
                        Check(CandyFlickUI.ModelCreditsTitle=="Model Credits","Second page has the requested Model Credits title");ValidateCopy();Capture("model-credits");break;
                    case 4: ui.Back();break;
                    case 5: Check(ui.SettingsPage=="About","Hardware Back returns from Model Credits to main credits");Click(ui.CreditsNextRect.center);break;
                    case 6: Check(ui.SettingsPage=="ModelCredits","Model Credits can be opened again");break;
                    case 7: Click(ui.CreditsBackRect.center);break;
                    case 8: Check(ui.SettingsPage=="About","Model Credits Back button returns to main credits");Click(ui.CreditsBackRect.center);break;
                    case 9: Check(ui.SettingsPage==null&&game.ScreenState==GameScreen.Settings,"Main credits Back still returns to main settings");Resolution(540,960);break;
                    case 10: ShowAbout();break;
                    case 11: Click(ui.CreditsNextRect.center);break;
                    case 12: Check(ui.SettingsPage=="ModelCredits","Mirrored credits navigation works on the scaled 540x960 canvas");Capture("model-credits-scaled");break;
                    case 13: Click(ui.CreditsBackRect.center);break;
                    case 14: Check(ui.SettingsPage=="About","Scaled Model Credits Back returns to main credits");break;
                    default:
                        File.WriteAllText("Documentation/model-credits-validation.json",JsonUtility.ToJson(new Evidence{utc=DateTime.UtcNow.ToString("o"),checks=checks.ToArray()},true));Finish(null);break;
                }
            }
            catch(Exception e){Finish(e.ToString());}
        }
        static void ValidateCopy()
        {
            string[] names={"Coffee Mug","Lunch Plate","Cookie"},slugs={"6toBy","6toBA","6toBB"};
            Check(CandyFlickUI.ModelCreditText.Length==4&&CandyFlickUI.ModelCreditText[3]=="Om Nelle Model: MlopsFunny","Model credits include all original attributions and the exact Om Nelle model credit");
            Check(ui.AboutVersion.Contains(Application.version),"Main credits display the actual running game version");
            Check(CandyFlickUI.AboutTitle=="Om Nom: Candy Flick+","Main credits identify the game as Om Nom: Candy Flick+");
            Check(CandyFlickUI.FormatAboutVersion("Version 2.6","9.8.7")=="Version 9.8.7","Credits version formatting follows future application versions as well");
            for(int i=0;i<3;i++)
            {
                string copy=CandyFlickUI.ModelCreditText[i];
                Check(copy.Contains("\""+names[i]+" (School Project)\"")&&copy.Contains("https://skfb.ly/"+slugs[i])&&copy.Contains("by Ole Gunnar Isager is licensed under Creative Commons Attribution (http://creativecommons.org/licenses/by/4.0/)."),names[i]+": full supplied attribution, author, model URL and license URL are present");
            }
            int previous=PortLocalization.Language;
            try
            {
                for(int language=0;language<9;language++)
                {
                    PortLocalization.Language=language;
                    foreach(float height in new[]{853f,1136f,1422f})
                    {
                        Font font=language==6?Resources.Load<Font>("Localization/CandyFlickKorean"):language==1?game.assets.alternateFont:game.assets.font;
                        var style=new GUIStyle{font=font,fontSize=30,wordWrap=true};
                        Rect[] entries=CandyFlickUI.ModelCreditsLayout(style,height);
                        for(int i=0;i<entries.Length;i++)
                        {
                            string copy=PortLocalization.Translate(CandyFlickUI.ModelCreditText[i]);
                            Check(entries[i].yMax<=height-175&&entries[i].height>=style.CalcHeight(new GUIContent(copy),entries[i].width)&&(i==0||entries[i-1].yMax<entries[i].y),"Complete localized model attribution fits: "+PortLocalization.CatalogData().languages[language]+", entry "+i+", height "+height);
                        }
                    }
                }
            }
            finally{PortLocalization.Language=previous;}
        }

        static void OnLog(string message,string stack,LogType type){if((type==LogType.Exception||type==LogType.Error||type==LogType.Assert)&&!stack.Contains("UnityEditor.Search.SearchDatabase"))Finish(message+"\n"+stack);}
        static void Finish(string error)
        {
            SessionState.SetBool(Key,false);EditorApplication.update-=Tick;Application.logMessageReceived-=OnLog;
            AudioListener.pause=SessionState.GetBool(Key+"Audio",false);string scene=SessionState.GetString(Key+"Scene","");EditorSceneManager.playModeStartScene=string.IsNullOrEmpty(scene)?null:AssetDatabase.LoadAssetAtPath<SceneAsset>(scene);
            if(error!=null)File.WriteAllText(Folder+"/failure.txt",error);EditorApplication.ExitPlaymode();
        }
        [Serializable]class Evidence{public string utc;public string[] checks;}
    }
}
