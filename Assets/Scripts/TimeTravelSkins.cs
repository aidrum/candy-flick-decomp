using System;
using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    public enum NommieSkin { Original, Medieval, Artist, Pirate, Pharaoh, Greek, Prehistoric, Disco, OmNelle }

    // Optional appearance and voice: the recovered actor root, collider and gameplay stay in place.
    public sealed class TimeTravelSkins : MonoBehaviour
    {
        public const string Preference = "PortNommieSkin";
        const string PreferenceVersion = "PortNommieSkinVersion";
        public const float PrehistoricSize = 1.25f;
        public static readonly string[] Names = Enum.GetNames(typeof(NommieSkin));
        public static int RandomChoice => Names.Length;
        public const int SavedRandomChoice = -1;
        public bool RandomSelected { get; private set; }
        public NommieSkin Selected { get; private set; }
        public GameObject Visual { get; private set; }
        public TimeTravelVoice Voice { get; private set; }
        GameAssets originalAudio;
        Animation player;
        SkinnedMeshRenderer original;
        AnimationClip[] originalClips;
        string[] originalNames;
        Transform antenna;
        Vector3 antennaPosition, antennaScale;
        Quaternion antennaRotation;

        public void Initialize(GameAssets assets = null)
        {
            originalAudio = assets ? assets : Resources.Load<GameAssets>("GameAssets");
            player = GetComponent<Animation>();
            original = GetComponentsInChildren<SkinnedMeshRenderer>(true).Single(r => r.name == "OmNom_Mesh");
            var states = player.Cast<AnimationState>().ToArray();
            originalNames = states.Select(s => s.name).ToArray();
            originalClips = states.Select(s => s.clip).ToArray();
            antenna = GetComponentsInChildren<Transform>(true).First(t=>t.name=="Jt_PonyTail");
            antennaPosition=antenna.localPosition; antennaRotation=antenna.localRotation; antennaScale=antenna.localScale;
            int saved = PlayerPrefs.GetInt(Preference, 0);
            // Index 8 was Random before Om Nelle. Migrate once without changing existing skin indices.
            if(PlayerPrefs.GetInt(PreferenceVersion,0)<1)
            {
                if(saved==8){saved=SavedRandomChoice;PlayerPrefs.SetInt(Preference,saved);}
                PlayerPrefs.SetInt(PreferenceVersion,1);PlayerPrefs.Save();
            }
            // Also accept the previous count-based sentinel when upgrading this selection format.
            if(saved==RandomChoice){saved=SavedRandomChoice;PlayerPrefs.SetInt(Preference,saved);PlayerPrefs.Save();}
            RandomSelected = saved == SavedRandomChoice;
            Apply(RandomSelected ? (NommieSkin)UnityEngine.Random.Range(0, Names.Length)
                : Enum.IsDefined(typeof(NommieSkin), saved) ? (NommieSkin)saved : NommieSkin.Original);
        }

        public void Select(NommieSkin skin)
        {
            if (!Enum.IsDefined(typeof(NommieSkin), skin)) return;
            if (Apply(skin))
            {
                RandomSelected = false;
                PlayerPrefs.SetInt(Preference, (int)skin);
                PlayerPrefs.Save();
            }
        }

        public void SelectRandom()
        {
            RandomSelected = true; RerollRandom();
            PlayerPrefs.SetInt(Preference, SavedRandomChoice); PlayerPrefs.Save();
        }
        public void RerollRandom()
        {
            if (RandomSelected) Apply((NommieSkin)UnityEngine.Random.Range(0, Names.Length));
        }

        // Session appearances never overwrite the player's saved skin or Random choice.
        public bool ApplyForSession(NommieSkin skin) => Enum.IsDefined(typeof(NommieSkin),skin) && Apply(skin);
        bool Apply(NommieSkin skin)
        {
            GameObject prefab = skin == NommieSkin.Original ? null : Resources.Load<GameObject>("TimeTravel/" + skin);
            if (skin != NommieSkin.Original && !prefab)
            {
                Debug.LogError("Missing imported Time Travel skin: " + skin);
                return false;
            }
            player.Stop();
            // Imported skins replace this registered action set. Enumeration also
            // exposes Unity's temporary queued states, which RemoveClip cannot remove by name.
            // Rebind the original clips when leaving Prehistoric, including their wrap modes.
            foreach (string name in originalNames) player.RemoveClip(name);
            for (int i = 0; i < originalNames.Length; i++) player.AddClip(originalClips[i], originalNames[i]);
            if (Visual) { Visual.SetActive(false); Visual.transform.SetParent(null); Destroy(Visual); }
            Selected = skin;
            antenna.localPosition=antennaPosition; antenna.localRotation=antennaRotation; antenna.localScale=antennaScale;
            Voice = skin == NommieSkin.Original ? null : Resources.Load<TimeTravelVoice>("TimeTravelAudio/" + skin);
            original.enabled = !ReplacesBody(skin);
            if (prefab)
            {
                Visual = Instantiate(prefab, transform, false);
                // Skins are cosmetic: enlarge the model while retaining the
                // original actor transform, candy collider and touch hitbox.
                if(skin == NommieSkin.Prehistoric)Visual.transform.localScale *= PrehistoricSize;
                Visual.name = "TimeTravelSkin"; // Imported clip paths are relative to the actor Animation.
                var imported = Visual.GetComponent<TimeTravelVisual>();
                foreach (var binding in imported.clips)
                {
                        player.RemoveClip(binding.action);
                        player.AddClip(binding.clip, binding.action);
                        player[binding.action].wrapMode = binding.loop ? WrapMode.Loop
                            : binding.action == "open" ? WrapMode.ClampForever : WrapMode.Once;
                }
                if(skin != NommieSkin.Prehistoric) imported.Bind(transform);
                foreach (var renderer in Visual.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            }
            else Visual = null;
            player.Play("idle");
            ConstrainAntenna();
            GetComponent<OmNomActor>()?.RefreshRenderers();
            return true;
        }
        static bool ReplacesBody(NommieSkin skin)=>skin==NommieSkin.Prehistoric||skin==NommieSkin.OmNelle;
        public bool Includes(Renderer renderer) => renderer != original || !ReplacesBody(Selected);
        public AudioClip Sound(NommieSound sound) => TimeTravelVoice.Resolve(sound, Voice, originalAudio);
        public void ConstrainAntenna()
        {
            if(!antenna || Selected==NommieSkin.Original || Selected==NommieSkin.Greek || ReplacesBody(Selected))return;
            antenna.localPosition=antennaPosition; antenna.localRotation=antennaRotation;
            antenna.localScale=Vector3.zero; // Collapse the antenna beneath hats without editing the original mesh.
        }
        void LateUpdate()=>ConstrainAntenna();
    }
}
