using System.Collections.Generic;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;
namespace CandyFlick
{
    public sealed partial class LanMultiplayer
    {
        readonly Dictionary<int,LanSlingshotView> remoteSlingshots=new Dictionary<int,LanSlingshotView>();
        void RefreshRemoteSlingshots()
        {
            foreach(var player in Players)
            {
                if(player.id==LocalId)continue;
                var candy=ready.Values.FirstOrDefault(v=>v&&v.Owner==player.id&&v.gameObject.activeSelf);
                bool visible=(candy||player.thrown>0)&&player.hasPose&&player.tracked&&Phase==LanPhase.Playing&&LanRules.EffectiveControl(Controls,player.controlMode)==LaunchMode.Slingshot;
                if(!remoteSlingshots.TryGetValue(player.id,out var view))
                {
                    if(!visible)continue;
                    var root=new GameObject("LAN slingshot "+player.id);root.transform.SetParent(game.World.Find("Candies"),false);root.SetActive(false);
                    view=root.AddComponent<LanSlingshotView>();view.Initialize(player.id);remoteSlingshots[player.id]=view;
                }
                view.gameObject.SetActive(visible);if(visible)view.SetPose(player.cameraPosition,player.cameraRotation,candy?candy.transform:null,Mathf.Max(0,60-Remaining-player.nextThrow+LanRules.ThrowInterval));
            }
        }
        void ClearRemoteSlingshots()
        {
            foreach(var view in remoteSlingshots.Values)if(view){view.gameObject.SetActive(false);Destroy(view.gameObject);}remoteSlingshots.Clear();
        }
    }
}
