using System.Collections.Generic;
using UnityEngine;

namespace CandyFlick
{
    public sealed partial class CandyFlickGame
    {
        public const string PhotoTrackResource="PhotoMusic/music_Boardgame_WinterFun";
        public bool IsPhotoMode {get;private set;}
        public bool PhotoPlacing {get;private set;}
        public IReadOnlyList<PhotoOmNom> PhotoActors=>photoActors;
        public PhotoOmNom PhotoTarget {get;private set;}
        public PhotoOmNom PhotoSelection {get;private set;}
        public bool PhotoCapturing {get;set;}
        readonly List<PhotoOmNom> photoActors=new List<PhotoOmNom>();
        PhotoOmNom movingPhotoActor;
        NommieSkin pendingPhotoSkin;
        Transform photoFloor;
        Vector3 photoFloorOriginalPosition;
        bool photoCandyResolved;
        OmNomActor PhotoFeedTarget
        {
            get
            {
                if(!IsPhotoMode)return null;
                PhotoOmNom nearest=null;float distance=float.PositiveInfinity;
                foreach(var nom in photoActors)
                {
                    float next=(nom.transform.position-Player.position).sqrMagnitude;
                    if(nom.Tracked&&!nom.IsMoving&&next<distance){nearest=nom;distance=next;}
                }
                return nearest?nearest.Actor:null;
            }
        }
        public bool StartPhotoMode()
        {
            GroundSupport.Refresh();
            if(!GroundSupport.Supported || IsLanMatch)return false;
            ar.End();EndPhotoMode();StopAllCoroutines();input.Cancel();Time.timeScale=1;
            IsPhotoMode=IsAR=true;IsNonAR=false;PhotoCapturing=false;
            music.Stop();foreach(var candy in candies)candy.Restore();
            projectile=null;candyIndex=0;releasedElasticPending=false;pendingResults=false;
            photoCandyResolved=false;
            timeForNextShoot=true;hardStabilisation=false;Session=new GameSession();LastScore=0;
            ConfigureNonARBackground();Actor.ResetOriginal();Actor.gameObject.SetActive(false);
            Boosts.Reset();ScreenState=GameScreen.Playing;SetWorldVisible(true);
            // The broad floor follows the lowest placed actor; higher surfaces
            // retain their own local collision patches.
            photoFloor=World.Find("Floor");photoFloorOriginalPosition=photoFloor.localPosition;
            photoFloor.gameObject.SetActive(false);
            GameCamera.clearFlags=CameraClearFlags.Depth;GameCamera.backgroundColor=Color.black;
            BeginPhotoPlacement(Skins.RandomSelected?(NommieSkin)Random.Range(0,TimeTravelSkins.Names.Length):Skins.Selected);
            ar.Begin();PrepareBulletForShoot();StartGameplayMusic();GameStarted?.Invoke();
            return true;
        }
        public void BeginPhotoPlacement(NommieSkin skin)
        {
            if(!IsPhotoMode)return;
            CancelPhotoPlacement();
            CancelCandyGesture();PhotoSelection=null;PhotoTarget=null;movingPhotoActor=null;
            pendingPhotoSkin=skin;PhotoPlacing=true;
        }
        public PhotoOmNom CompletePhotoPlacement(Vuforia.AnchorBehaviour anchor)
        {
            if(!IsPhotoMode||!PhotoPlacing||!anchor)return null;
            PhotoOmNom nom=movingPhotoActor;
            if(!nom)
            {
                var template=Resources.Load<GameObject>("Original26/Geometry").transform.Find("OmNom@All-1");
                var instance=Instantiate(template.gameObject,World);instance.name="Photo Om Nom";
                var actor=instance.AddComponent<OmNomActor>();actor.InitializeOriginal(this);
                var appearance=instance.AddComponent<TimeTravelSkins>();appearance.Initialize(assets);appearance.ApplyForSession(pendingPhotoSkin);
                nom=instance.AddComponent<PhotoOmNom>();nom.Initialize(actor,appearance);photoActors.Add(nom);
            }
            nom.Attach(anchor);nom.Actor.FacePlayer();nom.RememberHeading();
            PhotoPlacing=false;movingPhotoActor=null;RefreshPhotoGround();
            return nom;
        }
        public void SelectPhotoTarget()
        {
            if(!IsPhotoMode||!PhotoTarget||PhotoPlacing||PhotoCapturing)return;
            CancelCandyGesture();PhotoSelection=PhotoTarget;
        }
        public void DeselectPhotoActor()=>PhotoSelection=null;
        public void MovePhotoSelection()
        {
            if(!PhotoSelection)return;
            CancelCandyGesture();movingPhotoActor=PhotoSelection;PhotoSelection=PhotoTarget=null;PhotoPlacing=true;
            movingPhotoActor.SetMoving(true);RefreshPhotoGround();
        }
        public void FacePhotoSelection()
        {
            if(!PhotoSelection)return;
            PhotoSelection.Actor.FacePlayer();PhotoSelection.RememberHeading();PhotoSelection=null;
        }
        public void DeletePhotoSelection()
        {
            if(!PhotoSelection)return;
            var nom=PhotoSelection;PhotoSelection=PhotoTarget=null;
            photoActors.Remove(nom);nom.Release();nom.gameObject.SetActive(false);Destroy(nom.gameObject);RefreshPhotoGround();
        }
        public void CancelPhotoPlacement()
        {
            if(movingPhotoActor)movingPhotoActor.SetMoving(false);
            PhotoPlacing=false;movingPhotoActor=null;RefreshPhotoGround();
        }
        public void DeleteAllPhotoActors()
        {
            CancelPhotoPlacement();PhotoSelection=PhotoTarget=null;CancelCandyGesture();
            foreach(var nom in photoActors)if(nom){nom.Release();nom.gameObject.SetActive(false);Destroy(nom.gameObject);}
            photoActors.Clear();foreach(var candy in candies)candy.Restore();
            projectile=null;timeForNextShoot=true;hardStabilisation=false;RefreshPhotoGround();
            photoCandyResolved=false;
        }
        internal void RefreshPhotoGround()
        {
            if(!IsPhotoMode||!photoFloor)return;
            PhotoOmNom lowest=null;
            foreach(var nom in photoActors)
                if(nom.Placed&&(!lowest||nom.transform.position.y<lowest.transform.position.y))lowest=nom;
            photoFloor.gameObject.SetActive(lowest);
            if(lowest)photoFloor.position=lowest.transform.position;
        }
        internal float PhotoGroundHeightAt(Vector3 point)
        {
            float height=photoFloor&&photoFloor.gameObject.activeSelf?photoFloor.position.y:0;
            foreach(var nom in photoActors)
            {
                if(!nom.Placed||!nom.GroundCollider.enabled||!nom.GroundCollider.gameObject.activeInHierarchy)continue;
                Bounds patch=nom.GroundCollider.bounds;
                if(point.x>=patch.min.x&&point.x<=patch.max.x&&point.z>=patch.min.z&&point.z<=patch.max.z&&point.y>=patch.max.y)
                    height=Mathf.Max(height,patch.max.y);
            }
            return height;
        }
        void TickPhotoMode()
        {
            PhotoTarget=null;
            if(!CanSimulate||PhotoPlacing||PhotoSelection||PhotoCapturing||CandyFlickUI.PopupActive)return;
            Physics.SyncTransforms();Ray ray=GameCamera.ScreenPointToRay(Screen.safeArea.center);
            float closest=float.PositiveInfinity;
            foreach(var nom in photoActors)
                if(nom.Tracked&&!nom.IsMoving&&nom.Actor.GetComponent<Collider>().Raycast(ray,out var hit,closest))
                {PhotoTarget=nom;closest=hit.distance;}
        }
        internal bool AcceptPhotoCandyCatch(CandyProjectile candy)
        {
            if(!candy||!candy.IsShot||candy!=projectile||photoCandyResolved)return false;
            photoCandyResolved=true;
            foreach(var nom in photoActors)nom.Actor.ForgetPhotoCandy(candy);
            return true;
        }
        void LateUpdate()
        {
            if(IsPhotoMode&&CanSimulate)ResolvePhotoCandyOutcome();
        }
        void ResolvePhotoCandyOutcome()
        {
            if(!projectile)return;
            // Pending catches from every actor's Update are resolved before a
            // landing or timeout can generate the single sad reaction.
            if(!photoCandyResolved&&(projectile.HasLanded||!projectile.IsShot))
            {
                photoCandyResolved=true;
                Vector3 position=projectile.IsShot?projectile.transform.position:projectile.LastShotPosition;
                PhotoOmNom closest=null;float distance=float.PositiveInfinity;
                foreach(var nom in photoActors)
                {
                    nom.Actor.ForgetPhotoCandy(projectile);
                    float next=(nom.transform.position-position).sqrMagnitude;
                    if(nom.Tracked&&!nom.IsMoving&&next<distance){distance=next;closest=nom;}
                }
                if(closest)closest.Actor.ReactToPhotoMiss();
            }
            if(!projectile.IsShot)ResolveShot(false,0);
        }
        public bool PhotoCandyAt(Vector2 screenPoint)=>ghostCollider.Raycast(GameCamera.ScreenPointToRay(screenPoint),out _,Mathf.Infinity);
        void EndPhotoMode()
        {
            if(!IsPhotoMode)return;
            DeleteAllPhotoActors();PhotoCapturing=false;IsPhotoMode=false;
            GameplayMusic.Stop();GameplayMusic.clip=null;
            Actor.gameObject.SetActive(true);photoFloor.localPosition=photoFloorOriginalPosition;photoFloor.gameObject.SetActive(true);photoFloor=null;
        }
    }
}
