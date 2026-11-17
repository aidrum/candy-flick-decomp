using System.Collections.Generic;
using System.Linq;
using CandyFlick.Multiplayer;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class LanMultiplayer
    {
        sealed class LauncherAudio
        {
            public GameObject root;public AudioSource stretch,release,swish;public Vector3 position;
        }
        readonly Dictionary<int,LauncherAudio> launcherAudio=new Dictionary<int,LauncherAudio>();
        readonly Dictionary<int,int> collectionNotes=new Dictionary<int,int>();
        internal static float LauncherVolume(float nativeDistance)=>1f/Mathf.Max(1,nativeDistance/500f);
        AudioSource LauncherSource(GameObject root,AudioClip clip,bool loop=false)
        {
            var source=root.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=0;source.loop=loop;source.clip=clip;return source;
        }
        LauncherAudio GetLauncherAudio(int owner)
        {
            if(launcherAudio.TryGetValue(owner,out var audio))return audio;
            var root=new GameObject("LAN launcher audio "+owner);root.transform.SetParent(transform,false);
            audio=new LauncherAudio{root=root,stretch=LauncherSource(root,game.assets.elasticStretch,true),release=LauncherSource(root,game.assets.elasticShoot),swish=LauncherSource(root,null)};
            launcherAudio[owner]=audio;return audio;
        }
        // Reliable host-issued cues avoid dropped or coalesced snapshot counters.
        void PublishLanSound(string sound,int owner,int clipIndex=0)
        {
            var player=Match.Players.Find(p=>p.id==owner);if(player==null)return;
            var cue=new LanMessage{kind="sound",epoch=Epoch,playerId=owner,sound=sound,clipIndex=clipIndex,
                controlMode=(int)LanRules.EffectiveControl(Controls,player.controlMode),candyPosition=player.candyPosition};
            ReceiveLanSound(cue);
            if(sound=="launch")network.Broadcast(cue);
            else foreach(var peer in network.Peers)if(peer.Id==owner)peer.Send(cue);
        }
        void PublishLanNomSound(string action)
        {
            var cue=new LanMessage{kind="sound",epoch=Epoch,playerId=LocalId,sound="nom",text=action};
            ReceiveLanSound(cue);network.Broadcast(cue);
        }
        void ReceiveLanSound(LanMessage cue)
        {
            if(!activeMatch||cue.epoch!=Epoch||!game.SoundEnabled||!Players.Any(p=>p.id==cue.playerId))return;
            if(cue.sound=="nom"){PlayNomEvent(cue.text);return;}
            if(cue.sound=="collect")
            {
                if(cue.playerId==LocalId&&cue.clipIndex>=0&&cue.clipIndex<game.assets.hits.Length)game.Play(game.assets.hits[cue.clipIndex]);
            }
            else if(cue.sound=="launch"&&cue.playerId!=LocalId&&LanRules.Finite(cue.candyPosition))
            {
                var audio=GetLauncherAudio(cue.playerId);audio.position=cue.candyPosition;
                float volume=LauncherVolume(Vector3.Distance(game.Player.position,audio.position));
                audio.release.volume=audio.swish.volume=volume;
                if(LanRules.CleanControlMode(cue.controlMode)==LaunchMode.Slingshot)audio.release.Play();
                if(cue.clipIndex>=0&&cue.clipIndex<game.assets.swishes.Length){audio.swish.clip=game.assets.swishes[cue.clipIndex];audio.swish.Play();}
            }
        }
        void LanCollected(int owner)
        {
            collectionNotes.TryGetValue(owner,out int note);
            int last=Mathf.Max(0,game.assets.hits.Length-1);
            PublishLanSound("collect",owner,Mathf.Min(note,last));collectionNotes[owner]=Mathf.Min(note+1,last);
        }
        internal void LanMissed(int owner){if(IsHost)collectionNotes[owner]=0;}
        void RefreshLanAudio()
        {
            foreach(var player in Players)
            {
                if(player.id==LocalId)continue;
                bool audible=Playing&&game.SoundEnabled&&player.tracked&&player.hasPose;
                bool stretching=audible&&player.stretching&&60-Remaining>=player.nextThrow&&LanRules.EffectiveControl(Controls,player.controlMode)==LaunchMode.Slingshot;
                if(stretching)GetLauncherAudio(player.id);
                if(!launcherAudio.TryGetValue(player.id,out var audio))continue;
                // A release stays at the launch position; the moving loop follows the hand.
                audio.stretch.volume=LauncherVolume(Vector3.Distance(game.Player.position,player.candyPosition));
                audio.release.volume=audio.swish.volume=LauncherVolume(Vector3.Distance(game.Player.position,audio.position));
                if(stretching){if(!audio.stretch.isPlaying)audio.stretch.Play();}else audio.stretch.Stop();
                if(!audible){audio.release.Stop();audio.swish.Stop();}
            }
        }
        void ClearLanAudio()
        {
            foreach(var audio in launcherAudio.Values)if(audio.root){audio.root.SetActive(false);Destroy(audio.root);}
            launcherAudio.Clear();collectionNotes.Clear();
        }
    }
}
