using UnityEngine;

namespace CandyFlick
{
    public sealed class CandySkinSettings : MonoBehaviour
    {
        public const string Preference="PortCandySkin";
        public const int Count=3,RandomChoice=3;
        public int Selected {get;private set;}
        public int HeldSkin {get;private set;}
        CandyFlickGame game;CandySkinVisual ghost;
        public static int CleanChoice(int value)=>value>=0&&value<=RandomChoice?value:0;
        public static int CleanSkin(int value)=>value>=0&&value<Count?value:0;
        public static int Roll(int choice)=>choice==RandomChoice?Random.Range(0,Count):CleanSkin(choice);
        public void Initialize(CandyFlickGame owner)
        {
            game=owner;Selected=CleanChoice(PlayerPrefs.GetInt(Preference,0));
            ghost=Visual(owner.CandyGhost);PrepareNext();
        }
        public void Select(int choice)
        {
            if(game.IsLanMatch)return;
            Selected=CleanChoice(choice);PlayerPrefs.SetInt(Preference,Selected);PlayerPrefs.Save();
            game.LAN?.SetCandyChoice(Selected);PrepareNext();
        }
        public void PrepareNext()
        {
            HeldSkin=game.IsLanMatch?game.LAN.LocalCandySkin:Roll(Selected);ghost.SetSkin(HeldSkin);
        }
        void Update()
        {
            if(!game||!game.IsLanMatch)return;
            HeldSkin=game.LAN.LocalCandySkin;ghost.SetSkin(HeldSkin);
        }
        public static CandySkinVisual Visual(Transform root)
        {
            var visual=root.GetComponent<CandySkinVisual>();if(!visual)visual=root.gameObject.AddComponent<CandySkinVisual>();
            visual.Initialize();return visual;
        }
        public void ApplyShot(Transform root)=>Visual(root).SetSkin(HeldSkin);
    }
}
