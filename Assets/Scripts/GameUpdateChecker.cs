using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

namespace CandyFlick
{
    // One asynchronous stable-release check per launch. Failures never interrupt the game.
    public sealed class GameUpdateChecker : MonoBehaviour
    {
        public const string Preference="PortCheckForUpdates";
        public const string LatestReleaseUrl="https://api.github.com/repos/aidrum/candy-flick-decomp/releases/latest";
        public const string ReleasesPageUrl="https://github.com/aidrum/candy-flick-decomp/releases/latest";
        public bool ChecksEnabled {get;private set;}
        public bool CheckedThisLaunch {get;private set;}
        public AvailableUpdate Pending {get;private set;}
        CandyFlickUI ui;
        UnityWebRequest request;
        [Serializable] sealed class Release {public string tag_name,html_url;public bool draft,prerelease;}
        public sealed class AvailableUpdate
        {
            public readonly string Version,Url;
            public AvailableUpdate(string version,string url){Version=version;Url=url;}
        }
        public void Initialize(CandyFlickUI userInterface)
        {
            ui=userInterface;ChecksEnabled=PlayerPrefs.GetInt(Preference,1)==1;
        }
        void Start()
        {
            // Editor previews and scripted validation stay deterministic. Explicit checks still work.
            if(!Application.isEditor)CheckForUpdates();
        }
        public void ToggleChecks()
        {
            ChecksEnabled=!ChecksEnabled;
            PlayerPrefs.SetInt(Preference,ChecksEnabled?1:0);PlayerPrefs.Save();
            if(ChecksEnabled)CheckForUpdates();
        }
        public void CheckForUpdates()
        {
            if(!ChecksEnabled||CheckedThisLaunch)return;
            CheckedThisLaunch=true;StartCoroutine(CheckLatestRelease());
        }
        IEnumerator CheckLatestRelease()
        {
            using(var download=UnityWebRequest.Get(LatestReleaseUrl))
            {
                request=download;
                try
                {
                    download.timeout=15;
                    download.SetRequestHeader("User-Agent","CandyFlick/"+Application.version);
                    download.SetRequestHeader("Accept","application/vnd.github+json");
                    UnityWebRequestAsyncOperation operation=null;
                    try{operation=download.SendWebRequest();}catch(Exception){}
                    if(operation!=null)
                    {
                        yield return operation;
                        if(download.result==UnityWebRequest.Result.Success&&download.responseCode==200)
                            AcceptRelease(download.downloadHandler.text);
                    }
                }
                finally{request=null;}
            }
        }
        void AcceptRelease(string json)
        {
            if(TryFindUpdate(json,Application.version,out var update))Pending=update;
        }
        void Update()
        {
            if(!ChecksEnabled||Pending==null||!ui||!ui.CanShowUpdatePrompt)return;
            var update=Pending;Pending=null;
            ui.ShowUpdateAvailable(update.Version,update.Url);
        }
        public static bool TryFindUpdate(string json,string currentVersion,out AvailableUpdate update)
        {
            update=null;
            if(!TryVersion(currentVersion,out var current,out _))return false;
            Release release;
            try{release=JsonUtility.FromJson<Release>(json);}catch(Exception){return false;}
            if(release==null||release.draft||release.prerelease||!TryVersion(release.tag_name,out var latest,out bool prerelease)||prerelease||latest<=current)return false;
            string url=ReleasesPageUrl;
            if(Uri.TryCreate(release.html_url,UriKind.Absolute,out var page)&&page.Scheme==Uri.UriSchemeHttps&&
                page.Host.Equals("github.com",StringComparison.OrdinalIgnoreCase)&&page.AbsolutePath.StartsWith("/aidrum/candy-flick-decomp/releases/",StringComparison.Ordinal))url=page.AbsoluteUri;
            update=new AvailableUpdate(release.tag_name.Trim().TrimStart('v','V'),url);return true;
        }
        static bool TryVersion(string text,out Version version,out bool prerelease)
        {
            version=null;prerelease=false;if(string.IsNullOrWhiteSpace(text))return false;
            text=text.Trim().TrimStart('v','V');int suffix=text.IndexOfAny(new[]{'-','+'});
            if(suffix>=0){if(suffix==text.Length-1)return false;prerelease=text[suffix]=='-';text=text.Substring(0,suffix);}
            if(!text.Contains("."))text+=".0";
            if(!Version.TryParse(text,out var parsed))return false;
            version=new Version(parsed.Major,parsed.Minor,Math.Max(0,parsed.Build),Math.Max(0,parsed.Revision));return true;
        }
        void OnDestroy(){if(request!=null)request.Abort();}
    }
}
