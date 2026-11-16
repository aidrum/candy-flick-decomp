using System;
using System.Collections.Generic;
using System.Linq;
using CandyFlick.Multiplayer;
using CandyFlick.Original26;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class LanMultiplayer
    {
        readonly Dictionary<int,LanCandyView> flying=new Dictionary<int,LanCandyView>();
        readonly Dictionary<int,LanCandyView> ready=new Dictionary<int,LanCandyView>();
        readonly Dictionary<int,LanCandyState> remoteTargets=new Dictionary<int,LanCandyState>();
        readonly List<(LanCandyView candy,double time)> contacts=new List<(LanCandyView,double)>();
        int nextCandy,nomEvent,seenEvent;string nomClip="idle",eventClip="";
        float nomBusyUntil,nextJump,jumpStart=-1,jumpDuration;Vector3 jumpFrom,jumpTo;
        Animation nomAnimation;
        void InitializeLanWorld()
        {
            ClearLanWorld();nextCandy=nomEvent=seenEvent=0;nomClip="idle";eventClip="";nomBusyUntil=0;nextJump=20;jumpStart=-1;
            if(IsHost)foreach(var player in Match.Players)player.readySkin=CandySkinSettings.Roll(player.candyChoice);
            nomAnimation=game.Actor.GetComponent<Animation>();game.Boosts.Reset();SetNomAction("idle");InitializeLanRings();
        }
        LanCandyView CreateCandy(int id,int owner,bool shot)
        {
            var source=shot?game.World.Find("Candies/Candy1").gameObject:game.CandyGhost.gameObject;
            var instance=Instantiate(source,game.World.Find("Candies"));instance.name="LAN candy "+id;instance.SetActive(true);
            var view=instance.AddComponent<LanCandyView>();view.Initialize(this,id,owner,shot,IsHost);
            return view;
        }
        void SpawnHostCandy(LanPlayer player,float strength,float ratio,float angle)
        {
            var view=CreateCandy(++nextCandy,player.id,true);view.Born=Match.Elapsed;
            view.ScoreDistance=(int)LaunchMath26.HorizontalDistance(player.cameraPosition,game.World.Find("FrogPos").position);
            view.transform.SetPositionAndRotation(player.candyPosition,player.candyRotation);view.SetSkin(player.readySkin);
            player.readySkin=CandySkinSettings.Roll(player.candyChoice);nextSnapshot=0;
            float distance=LaunchMath26.HorizontalDistance(player.cameraPosition,game.Actor.transform.position);
            float speed=LaunchMath26.CalculateSpeed(strength,player.cameraPosition.y,distance);
            Quaternion launch=LaunchMath26.ShootRotation(player.cameraRotation.eulerAngles.y,ratio,angle);
            view.Body.linearVelocity=view.Body.angularVelocity=Vector3.zero;
            view.Body.AddForce(launch*Vector3.forward*speed,ForceMode.Impulse);view.Body.AddRelativeTorque(Vector3.up*speed*speed,ForceMode.Impulse);
            flying.Add(view.Id,view);
        }
        public void QueueLanContact(LanCandyView candy,double time)
        {
            if(InMatch&&IsHost&&Phase==LanPhase.Playing)contacts.Add((candy,time));
        }
        void ResolveLanContacts()
        {
            if(Match.Phase!=LanPhase.Playing)return;
            foreach(var contact in contacts)
            {
                var candy=contact.candy;
                if(!candy||!flying.ContainsKey(candy.Id)||!candy.Flying||(candy.Landed&&candy.LandedAt<=contact.time)||Match.Phase!=LanPhase.Playing)continue;
                jumpStart=-1;Match.Feed(candy.Owner,candy.ScoreDistance,candy.RingBonus);RemoveFlying(candy.Id);SetNomAction("close_eat");nomBusyUntil=Match.Elapsed+ClipLength("close_eat");
                nomEvent++;eventClip="eat";PlayNomEvent("eat");
            }
            contacts.Clear();
        }
        void RemoveFlying(int id){if(flying.TryGetValue(id,out var candy)){flying.Remove(id);ReleaseCandyRings(id);if(candy){candy.gameObject.SetActive(false);Destroy(candy.gameObject);}}}
        void RefreshLanWorld()
        {
            RefreshLanRings();
            foreach(var candy in flying.Values.ToArray())if(Match.Elapsed-candy.Born>=4)RemoveFlying(candy.Id);
            foreach(var player in Match.Players)
            {
                if(player.id==LocalId)continue;
                if(!ready.TryGetValue(player.id,out var view)){view=CreateCandy(-player.id-1,player.id,false);ready.Add(player.id,view);}
                bool visible=player.hasPose&&player.tracked&&Phase==LanPhase.Playing&&Match.Elapsed>=player.nextThrow;
                view.SetSkin(player.readySkin);view.gameObject.SetActive(visible);
                if(visible)view.transform.SetPositionAndRotation(player.candyPosition,player.candyRotation);
            }
        }
        public void TickActor()
        {
            if(!InMatch||!IsHost||!Playing)return;
            float clock=Match.Elapsed;
            if(jumpStart>=0)
            {
                float fraction=Mathf.Clamp01((clock-jumpStart-.15f)/Mathf.Max(.1f,jumpDuration-.3f));
                game.Actor.transform.position=Vector3.Lerp(jumpFrom,jumpTo,fraction);
                if(clock-jumpStart>=jumpDuration){jumpStart=-1;SetNomAction("idle");nextJump=clock+4;}
                return;
            }
            if(clock<nomBusyUntil)return;
            if(clock>=nextJump)
            {
                jumpFrom=game.Actor.transform.position;
                jumpTo=jumpFrom.sqrMagnitude<1?new[]{Vector3.left,Vector3.right,Vector3.forward,Vector3.back}[UnityEngine.Random.Range(0,4)]*150:Vector3.zero;
                jumpDuration=ClipLength("jump");jumpStart=clock;SetNomAction("jump");return;
            }
            var nearest=flying.Values.Where(c=>!c.Landed).OrderBy(c=>(c.transform.position-game.Actor.transform.position).sqrMagnitude).FirstOrDefault();
            if(nearest&&Vector3.Distance(nearest.transform.position,game.Actor.transform.position)<=800)
            {
                Vector3 toward=nearest.transform.position-game.Actor.transform.position;toward.y=0;
                if(toward.sqrMagnitude>1)game.Actor.transform.rotation=Quaternion.Slerp(game.Actor.transform.rotation,Quaternion.LookRotation(toward),Time.deltaTime*6);
                if(nomClip!="open"){SetNomAction("open");nomEvent++;eventClip="open";PlayNomEvent("open");}
            }
            else
            {
                if(nomClip!="idle")SetNomAction("idle");
                game.Actor.FacePlayer(); // Idle facing is decided by the host, then replicated.
            }
        }
        float ClipLength(string clip)=>nomAnimation&&nomAnimation[clip]!=null?nomAnimation[clip].length:1;
        void SetNomAction(string clip)
        {
            nomClip=clip;if(!nomAnimation||nomAnimation[clip]==null)return;
            nomAnimation.CrossFade(clip,game.Skins.Selected==NommieSkin.Prehistoric?.08f:.2f);
        }
        void PlayNomEvent(string action)
        {
            if(action=="open")game.Play(game.Skins.Sound(NommieSound.Open));
            else if(action=="eat"){game.Play(game.Skins.Sound(NommieSound.Close));game.Play(game.Skins.Sound(NommieSound.Chew));game.Play(game.assets.hits[UnityEngine.Random.Range(0,game.assets.hits.Length)]);}
        }
        LanSnapshot BuildSnapshot()
        {
            var state=new LanSnapshot{epoch=Match.Epoch,phase=Match.Phase,remaining=Match.Remaining,skin=(int)game.Skins.Selected,controls=Controls,groundMode=GroundMode,ground=Ground,
                players=Match.Players.ToArray(),actorPosition=game.Actor.transform.position,actorRotation=game.Actor.transform.rotation,
                actorClip=nomClip,actorTime=nomAnimation&&nomAnimation[nomClip]!=null?nomAnimation[nomClip].time:0,nomEvent=nomEvent,eventClip=eventClip};
            if(!activeMatch)return state;
            var objects=flying.Values.Select(c=>new LanCandyState{id=c.Id,owner=c.Owner,skin=c.Skin,landed=c.Landed,position=c.transform.position,rotation=c.transform.rotation}).ToList();
            foreach(var player in Match.Players)if(player.hasPose&&player.tracked&&Phase==LanPhase.Playing&&Match.Elapsed>=player.nextThrow)
                objects.Add(new LanCandyState{id=-player.id-1,owner=player.id,skin=player.readySkin,position=player.candyPosition,rotation=player.candyRotation,ready=true});
            state.candies=objects.ToArray();state.rings=lanRings.Values.Select(r=>r.State).ToArray();return state;
        }
        void ApplyRemoteSnapshot()
        {
            ApplyRemoteRings();
            remoteTargets.Clear();
            foreach(var value in remoteState.candies??Array.Empty<LanCandyState>())
            {
                if(value.ready&&value.owner==LocalId)continue;
                remoteTargets[value.id]=value;
                var table=value.ready?ready:flying;
                if(!table.TryGetValue(value.id,out var view)){view=CreateCandy(value.id,value.owner,!value.ready);table[value.id]=view;view.transform.SetPositionAndRotation(value.position,value.rotation);}
                view.SetSkin(value.skin);view.SetLanded(value.landed,game.World.Find("Floor").GetComponent<Collider>().bounds.max.y);
            }
            foreach(var table in new[]{ready,flying})foreach(int id in table.Keys.ToArray())if(!remoteTargets.ContainsKey(id))
            {var obj=table[id];table.Remove(id);if(obj){obj.gameObject.SetActive(false);Destroy(obj.gameObject);}}
            if(nomAnimation&&nomAnimation[remoteState.actorClip]!=null)
            {
                var state=nomAnimation[remoteState.actorClip];
                if(nomClip!=remoteState.actorClip){nomAnimation.CrossFade(remoteState.actorClip,.1f);state.time=remoteState.actorTime;}
                else if(Mathf.Abs(state.time-remoteState.actorTime)>.15f)state.time=remoteState.actorTime;
                nomClip=remoteState.actorClip;
            }
            if(remoteState.nomEvent!=seenEvent){seenEvent=remoteState.nomEvent;PlayNomEvent(remoteState.eventClip);}
        }
        void UpdateRemoteWorld()
        {
            foreach(var pair in remoteTargets)
            {
                var value=pair.Value;var table=value.ready?ready:flying;if(!table.TryGetValue(pair.Key,out var view))continue;
                float blend=Mathf.Clamp01(Time.unscaledDeltaTime*25);
                view.transform.position=Vector3.Lerp(view.transform.position,value.position,blend);view.transform.rotation=Quaternion.Slerp(view.transform.rotation,value.rotation,blend);
            }
            game.Actor.transform.position=Vector3.Lerp(game.Actor.transform.position,remoteState.actorPosition,Mathf.Clamp01(Time.unscaledDeltaTime*20));
            game.Actor.transform.rotation=Quaternion.Slerp(game.Actor.transform.rotation,remoteState.actorRotation,Mathf.Clamp01(Time.unscaledDeltaTime*20));
        }
        void ClearLanWorld()
        {
            ClearRemoteSlingshots();ClearLanRings();
            foreach(var view in flying.Values.Concat(ready.Values))if(view){view.gameObject.SetActive(false);Destroy(view.gameObject);}
            flying.Clear();ready.Clear();contacts.Clear();remoteTargets.Clear();
        }
    }
}
