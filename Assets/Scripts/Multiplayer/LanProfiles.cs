using UnityEngine;

namespace CandyFlick.Multiplayer
{
    // IDs follow Documentation/lan-profile-pictures.txt. Every phone ships this
    // atlas; messages contain only an ID, never pixels or a filesystem path.
    public static class LanProfiles
    {
        public const int Count=60,Default=1;
        public const string Resource="LanProfiles/profiles";
        public static int Clean(int id)=>id>=0&&id<Count?id:Default;
        public static Rect UV(int id)
        {
            id=Clean(id);
            const float texture=1024,cell=104,padding=2,size=100;
            return new Rect((id%8*cell+padding)/texture,1-(id/8*cell+padding+size)/texture,size/texture,size/texture);
        }
    }
}
