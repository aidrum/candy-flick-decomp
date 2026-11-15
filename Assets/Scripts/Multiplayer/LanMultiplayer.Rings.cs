using System;
using System.Collections.Generic;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class LanMultiplayer
    {
        readonly Dictionary<int,LanRingView> lanRings=new Dictionary<int,LanRingView>();
        readonly Dictionary<int,int> ringCandies=new Dictionary<int,int>();
        readonly Dictionary<int,float> ringExpires=new Dictionary<int,float>();
        float ringBearing;
        void InitializeLanRings()
        {
            if(!IsHost)return;
            ringBearing=UnityEngine.Random.Range(0f,360f/Match.Players.Count);
            for(int i=0;i<Match.Players.Count;i++)SpawnLanRing(i,1);
        }
        void SpawnLanRing(int id,int generation)
        {
            bool relocating=lanRings.TryGetValue(id,out var previous)&&previous;
            Vector3 oldPosition=relocating?previous.State.position:Vector3.zero;
            if(relocating)previous.Dispose();
            // Spread the initial set around Om Nom. Every later placement samples
            // the full circle independently of players, ring IDs or old sectors.
            Vector3 radial=Vector3.forward,position=Vector3.zero;
            for(int attempt=0;attempt<8;attempt++)
            {
                float angle=generation==1?(ringBearing+id*360f/Match.Players.Count+UnityEngine.Random.Range(-8f,8f))*Mathf.Deg2Rad:UnityEngine.Random.Range(0f,Mathf.PI*2);
                radial=new Vector3(Mathf.Sin(angle),0,Mathf.Cos(angle));
                position=game.Actor.transform.position+radial*UnityEngine.Random.Range(520f,690f);
                position.y=UnityEngine.Random.Range(300f,500f);
                if(!relocating||Vector3.Distance(position,oldPosition)>=40)break;
            }
            if(relocating&&Vector3.Distance(position,oldPosition)<40)position.y=oldPosition.y<400?oldPosition.y+100:oldPosition.y-100;
            var state=new LanRingState{id=id,generation=generation,position=position,rotation=Quaternion.LookRotation(-radial)};
            lanRings[id]=CreateLanRing(state);
            ringCandies.Remove(id);ringExpires[id]=Match.Elapsed+15;
        }
        LanRingView CreateLanRing(LanRingState state)
        {
            var wrapper=Instantiate(Resources.Load<GameObject>("Original26/BoostV"),game.World);
            wrapper.name="LAN ring "+state.id;
            var view=wrapper.transform.Find("boostv2").gameObject.AddComponent<LanRingView>();
            view.Initialize(this,wrapper,state,IsHost);return view;
        }
        void RefreshLanRings()
        {
            if(Match.Phase!=LanPhase.Playing)return;
            foreach(int id in lanRings.Keys.ToArray())
                if(!lanRings[id].State.hit&&Match.Elapsed>=ringExpires[id])SpawnLanRing(id,lanRings[id].State.generation+1);
        }
        void ReleaseCandyRings(int candyId)
        {
            foreach(int id in ringCandies.Where(p=>p.Value==candyId).Select(p=>p.Key).ToArray())
                if(lanRings.TryGetValue(id,out var ring))SpawnLanRing(id,ring.State.generation+1);
        }
        public bool TryPassLanRing(LanRingView ring,LanCandyView candy)
        {
            if(!InMatch||!IsHost||Phase!=LanPhase.Playing||!ring||ring.State.hit||!candy||!candy.Flying||candy.Landed||
                !lanRings.TryGetValue(ring.State.id,out var current)||current!=ring||!flying.TryGetValue(candy.Id,out var shot)||shot!=candy)return false;
            // Like ScoreBehaviour.temp_boost, the native vertical bonus is assigned,
            // not stacked. It only becomes points if this same candy is later eaten.
            candy.RingBonus=GameRules.VerticalRingBonus;
            ringCandies[ring.State.id]=candy.Id;ring.ApplyHit();return true;
        }
        internal void PlayLanRingSound()=>game.Play(game.assets.ring);
        void ApplyRemoteRings()
        {
            var states=remoteState.rings??Array.Empty<LanRingState>();
            foreach(var state in states)
            {
                if(!lanRings.TryGetValue(state.id,out var view)||view.State.generation!=state.generation)
                {
                    if(view)view.Dispose();view=CreateLanRing(state);lanRings[state.id]=view;
                }
                else if(state.hit&&!view.State.hit)view.ApplyHit();
            }
            foreach(int id in lanRings.Keys.ToArray())if(!states.Any(s=>s.id==id)){lanRings[id].Dispose();lanRings.Remove(id);}
        }
        void ClearLanRings()
        {
            foreach(var view in lanRings.Values)if(view)view.Dispose();lanRings.Clear();ringCandies.Clear();ringExpires.Clear();
        }
    }
}
