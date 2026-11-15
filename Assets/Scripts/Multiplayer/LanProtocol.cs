using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace CandyFlick.Multiplayer
{
    public enum LanPhase { Lobby, FindingCard, Playing, Paused, Finished }
    [Serializable] public sealed class LanPlayer
    {
        public int id, fed, score, baseScore, ringScore, thrown, lastSequence;
        public int avatar=LanProfiles.Default;
        public int candyChoice,readySkin;
        public string name="Player";
        public bool tracked, paused, hasPose, groundSupported, calibrated;
        public string groundReason="Ground support has not been checked.";
        public float nextThrow;
        public Vector3 cameraPosition, candyPosition;
        public Quaternion cameraRotation=Quaternion.identity,candyRotation=Quaternion.identity;
    }
    [Serializable] public sealed class LanCandyState
    {
        public int id,owner,skin;public Vector3 position;public Quaternion rotation;public bool ready,landed;
    }
    [Serializable] public sealed class LanRingState
    {
        public int id,generation;public Vector3 position;public Quaternion rotation=Quaternion.identity;public bool hit;
    }
    [Serializable] public sealed class LanGroundState
    {
        public int generation,placement;public bool originReady,placed,reusable;public float markerWidth;
        public Vector3 offset;public Quaternion rotation=Quaternion.identity;
    }
    [Serializable] public sealed class LanSnapshot
    {
        public int epoch,skin,nomEvent;public bool groundMode;public LanGroundState ground=new LanGroundState();
        public LanPhase phase;public float remaining=60,actorTime;
        public Vector3 actorPosition;public Quaternion actorRotation=Quaternion.identity;
        public string actorClip="idle",eventClip="",message="";
        public LanPlayer[] players=Array.Empty<LanPlayer>();public LanCandyState[] candies=Array.Empty<LanCandyState>();
        public LanRingState[] rings=Array.Empty<LanRingState>();
    }
    [Serializable] public sealed class LanMessage
    {
        public int version=LanRules.Version,playerId=-1,epoch,sequence,candyChoice;
        public int avatar=LanProfiles.Default;
        public string kind,name,text;
        public bool tracked,paused,groundSupported,calibrated;public string groundReason;public int groundGeneration;
        public Vector3 cameraPosition,candyPosition;
        public Quaternion cameraRotation=Quaternion.identity,candyRotation=Quaternion.identity;
        public float strength,angle,ratio;
        public LanSnapshot state;
    }
    [Serializable] public sealed class LanRoom
    {
        public string magic="CandyFlickLAN",name,roomId,address;
        public int version=LanRules.Version,port,count;
        public double seen;
    }
    public static class LanRules
    {
        public const int Version=9,MaxPlayers=10;public const float MatchSeconds=60,ThrowInterval=.8f;
        public static string CleanName(string value)
        {
            string clean=new string((value??"").Where(c=>!char.IsControl(c)&&c!='<'&&c!='>').ToArray()).Trim();
            if(clean.Length==0)return "Player";
            var elements=StringInfo.ParseCombiningCharacters(clean);
            return elements.Length>12?clean.Substring(0,elements[12]):clean;
        }
        public static bool Finite(Vector3 v)=>float.IsFinite(v.x)&&float.IsFinite(v.y)&&float.IsFinite(v.z);
        public static bool Finite(Quaternion q)=>float.IsFinite(q.x)&&float.IsFinite(q.y)&&float.IsFinite(q.z)&&float.IsFinite(q.w)&&Quaternion.Dot(q,q)>.5f&&Quaternion.Dot(q,q)<1.5f;
        public static bool ValidPose(LanMessage m)=>Finite(m.cameraPosition)&&Finite(m.candyPosition)&&Finite(m.cameraRotation)&&Finite(m.candyRotation)&&
            m.cameraPosition.magnitude<12000 && m.cameraPosition.y>30 && m.cameraPosition.y<10000 && Vector3.Distance(m.cameraPosition,m.candyPosition)<2000;
        public static int[] Winners(IEnumerable<LanPlayer> players)
        {
            var list=players.ToArray();if(list.Length==0)return Array.Empty<int>();int best=list.Max(p=>p.score);return list.Where(p=>p.score==best).Select(p=>p.id).ToArray();
        }
    }
    // Pure host-owned match rules. Unscaled networking is independent of this active-play clock.
    public sealed class LanMatch
    {
        public readonly List<LanPlayer> Players=new List<LanPlayer>();
        public LanPhase Phase {get;private set;}=LanPhase.Lobby;
        public float Remaining {get;private set;}=60;
        public float Elapsed=>60-Remaining;
        public int Epoch {get;private set;}
        bool began;
        public bool Start(bool keepCalibration=false)
        {
            if(Players.Count<2||Players.Count>LanRules.MaxPlayers||Phase!=LanPhase.Lobby)return false;
            Epoch++;Remaining=60;began=false;Phase=LanPhase.FindingCard;
            foreach(var p in Players){p.fed=p.score=p.baseScore=p.ringScore=p.thrown=p.lastSequence=0;p.nextThrow=0;p.tracked=p.paused=p.hasPose=false;if(!keepCalibration)p.calibrated=false;}
            return true;
        }
        public void RefreshGate()
        {
            if(Phase==LanPhase.Lobby||Phase==LanPhase.Finished)return;
            bool ready=Players.Count>=2&&Players.All(p=>p.tracked&&!p.paused);
            Phase=ready?LanPhase.Playing:began?LanPhase.Paused:LanPhase.FindingCard;if(ready)began=true;
        }
        public void Step(float dt)
        {
            RefreshGate();if(Phase!=LanPhase.Playing||!float.IsFinite(dt)||dt<0)return;
            Remaining=Mathf.Max(0,Remaining-dt);if(Remaining<=.0001f){Remaining=0;Phase=LanPhase.Finished;}
        }
        public bool AcceptThrow(int id,int epoch,int sequence)
        {
            RefreshGate();var p=Players.Find(x=>x.id==id);
            if(Phase!=LanPhase.Playing||p==null||epoch!=Epoch||sequence<=p.lastSequence||Elapsed+.0001f<p.nextThrow)return false;
            p.lastSequence=sequence;p.thrown++;p.nextThrow=Elapsed+LanRules.ThrowInterval;return true;
        }
        public void Feed(int owner,float distance,int ringBonus)
        {
            if(Phase!=LanPhase.Playing)return;
            var p=Players.Find(x=>x.id==owner);if(p==null)return;
            p.fed++;p.baseScore+=GameRules.DistanceScoreUnits(distance);
            p.ringScore+=ringBonus;p.score=p.baseScore+p.ringScore;
        }
        public void Lobby(bool keepCalibration=false){Phase=LanPhase.Lobby;Remaining=60;began=false;foreach(var p in Players){p.tracked=p.paused=false;if(!keepCalibration)p.calibrated=false;}}
    }
}
