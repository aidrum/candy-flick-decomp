using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public bool CanShowUpdatePrompt=>initialized&&game.ScreenState==GameScreen.Menu&&!PopupActive&&!hideGui&&
            !transition.Waiting&&transition.Alpha<=0&&menuEntryAlpha<=0&&ActiveAchievementNotification==null;
        public void ShowUpdateAvailable(string version,string url)
            =>ShowChoicePopup(L("A new version of Candy Flick is available.\nVersion: {0}",version),"Download","Cancel",()=>Application.OpenURL(url));
    }
}
