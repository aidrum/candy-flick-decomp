using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickUI
    {
        public static string MainMenuArtworkFamily(float width,float height)
        {
            float aspect=width/height;
            return aspect>=.72f?"ipad2":aspect>=.62f?"iphone4":"iphone5";
        }
        public static Rect MainMenuForegroundRect(Rect display,Texture foreground)
        {
            float height=display.width*foreground.height/foreground.width;
            return new Rect(display.x,display.yMax-height,display.width,height);
        }
        public static Rect MainMenuLogoRect(Rect display,float safeTop,Texture logo)
        {
            // The separate logo retains its native 603x374 proportions and an
            // original visual top margin (the PNG itself has transparent padding).
            float width=Mathf.Min(display.width,display.height*.75f);
            float scale=width/(MainMenuArtworkFamily(display.width,display.height)=="ipad2"?768f:640f);
            return new Rect(display.center.x-logo.width*scale/2,safeTop+20*scale,logo.width*scale,logo.height*scale);
        }
        void DrawMainMenuArtwork()
        {
            string family=artworkFamily;
            string foregroundPath,backgroundPath;
            switch(family)
            {
                case "ipad2": foregroundPath="ipad2/main_screen/main_background";backgroundPath="ipad2/main_screen/setting_bgr_ipa";break;
                case "iphone4": foregroundPath="iphone4/main_menu/main_bgr";backgroundPath="iphone4/main_menu/setting_bgr_iph4";break;
                default: foregroundPath="iphone5/main_menu";backgroundPath="iphone5/setting_bgr_iph5";break;
            }
            Background(Texture(backgroundPath));
            Texture2D foreground=Texture(foregroundPath),logo=Texture("all/logo");
            // fullScreen is the physical display in GUI coordinates, including the
            // bottom cutout. The GUI origin is already at the safe area's top.
            GUI.DrawTexture(MainMenuForegroundRect(fullScreen,foreground),foreground);
            GUI.DrawTexture(MainMenuLogoRect(new Rect(0,0,640,height),0,logo),logo);
        }
    }
}
