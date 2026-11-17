using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    // Local translation of the original 2.6 GameCenterManager, without authentication.
    public sealed class LocalAchievements
    {
        public const string SaveKey="PortAchievementsV1";
        [Serializable] public sealed class Entry { public string id,name,description,metric;public int target;public float[] uv; }
        [Serializable] public sealed class Catalog { public Entry[] entries;public float[] locked; }
        [Serializable] sealed class Record {public string id;public int best;}
        [Serializable] sealed class Save {public int hits,super,rings,throws;public List<Record> records=new List<Record>();}
        public Catalog Definitions {get;}
        Save saved;
        readonly List<Entry> newlyEarned=new List<Entry>();
        public event Action<Entry> Earned;
        public event Action Reloaded;
        int points,hitsRow,misses,feedingIndex;bool feedingPattern;
        public LocalAchievements()
        {
            Definitions=JsonUtility.FromJson<Catalog>(Resources.Load<TextAsset>("Achievements/catalog").text);
            Reload();
        }
        public void Reload()
        {
            try{saved=JsonUtility.FromJson<Save>(PlayerPrefs.GetString(SaveKey,""));}catch(ArgumentException){saved=null;}
            if(saved==null)saved=new Save();if(saved.records==null)saved.records=new List<Record>();
            newlyEarned.Clear();BeginGame();Reloaded?.Invoke();
        }
        public void BeginGame(){points=hitsRow=misses=feedingIndex=0;feedingPattern=true;}
        public int Progress(Entry entry)=>Mathf.Clamp(saved.records.FirstOrDefault(r=>r!=null&&r.id==entry.id)?.best??0,0,entry.target);
        public bool Unlocked(Entry entry)=>Progress(entry)>=entry.target;
        public int UnlockedCount=>Definitions.entries.Count(Unlocked);
        static int Increment(int value)=>Math.Max(0,value)>=int.MaxValue?int.MaxValue:Math.Max(0,value)+1;
        void Report(string metric,int value)
        {
            foreach(var entry in Definitions.entries.Where(e=>e.metric==metric))
            {
                int best=Mathf.Clamp(value,0,entry.target);var record=saved.records.FirstOrDefault(r=>r!=null&&r.id==entry.id);
                if(record==null){record=new Record{id=entry.id};saved.records.Add(record);}
                bool wasUnlocked=record.best>=entry.target;
                record.best=Math.Max(record.best,best);
                if(!wasUnlocked&&record.best>=entry.target)newlyEarned.Add(entry);
            }
        }
        void SaveProgress()
        {
            PlayerPrefs.SetString(SaveKey,JsonUtility.ToJson(saved));PlayerPrefs.Save();
            var earned=newlyEarned.ToArray();newlyEarned.Clear();
            foreach(var entry in earned)Earned?.Invoke(entry);
        }
        public void ThrowCandy(){saved.throws=Increment(saved.throws);Report("throws",saved.throws);SaveProgress();}
        public void Resolve(bool hit,bool super,bool ringBonus,int score,bool moving)
        {
            // Original feeding pattern checks the first five results of a game.
            if(feedingIndex<5){feedingPattern &= hit==(feedingIndex%2==0);feedingIndex++;}
            if(hit)
            {
                saved.hits=Increment(saved.hits);Report("hits",saved.hits);
                if(super){saved.super=Increment(saved.super);Report("super",saved.super);}
                if(ringBonus){saved.rings=Increment(saved.rings);Report("rings",saved.rings);}
                hitsRow++;Report("streak",hitsRow);
                points+=Math.Max(0,score);Report("points",points); // Native earned points, excluding continuation bonuses.
                if(moving)Report("moving",1);
                if(feedingIndex==5&&feedingPattern)Report("pattern",1);
            }
            else {hitsRow=0;misses++;Report("misses",misses);} // Misses accumulate within this game, not necessarily consecutively.
            SaveProgress();
        }
    }
}
