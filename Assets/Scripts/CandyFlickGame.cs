using System.Collections;
using CandyFlick.Original26;
using UnityEngine;

namespace CandyFlick
{
    public enum GameScreen { Menu, Playing, Paused, Results, Settings, Help, Marker, Tutorial, TargetOptions, Skins, LanLobby }
    public enum LaunchMode { Flick = 0, TapHold = 1, Slingshot = 2 }

    // Unity 6 / Android coordinator; native method addresses identify translated gameplay paths.
    public sealed partial class CandyFlickGame : MonoBehaviour
    {
        public GameAssets assets;
        public GameUpdateChecker Updates {get;private set;}
        public GameScreen ScreenState { get; private set; }
        public LaunchMode ControlMode { get; private set; } = LaunchMode.Slingshot;
        public const string ControlModePreference = "PortControlMode";
        public const float GameplayFarClip = 1000000f; // 1 km at 1,000 native units per metre.
        public const string CameraFpsLockPreference = "PortLockFpsToCamera";
        public bool LockFpsToCamera { get; private set; }
        public GameSession Session { get; private set; } = new GameSession();
        public OmNomActor Actor { get; private set; }
        public TimeTravelSkins Skins { get; private set; }
        public CandySkinSettings CandySkins {get;private set;}
        public Camera GameCamera { get; private set; }
        public Camera CandyCamera { get; private set; }
        public Transform World { get; private set; }
        public Transform Player { get; private set; }
        public Transform CandyGhost { get; private set; }
        public LanMultiplayer LAN { get; private set; }
        public LocalAchievements Achievements {get;private set;}
        public bool IsLanMatch => LAN && LAN.InMatch;
        public bool CanSimulate => ScreenState == GameScreen.Playing && (IsLanMatch ? LAN.Playing : !IsAR || ar.Tracking);
        public bool Ready => CanSimulate && (IsPhotoMode ? PhotoFeedTarget && !PhotoPlacing && !PhotoSelection && !PhotoCapturing && !projectile && timeForNextShoot : IsLanMatch ? LAN.Ready : !Session.IsOver && !projectile && timeForNextShoot);
        public bool SoundEnabled { get; private set; }
        public bool MusicEnabled { get; private set; }
        public int Best => PlayerPrefs.GetInt("BestScore", 0);
        public int LastScore { get; private set; }
        public int NextGameBonus { get; private set; }
        public string Message { get; private set; }
        public bool IsAR { get; private set; }
        public bool IsDragging => isCandyTapped && input.IsTouchBegan;
        public Vector2 DragStart => input.Start;
        public Vector2 DragNow => input.Position;
        public float NativeDistance => LaunchMath26.HorizontalDistance(Player.position, PhotoFeedTarget ? PhotoFeedTarget.transform.position : Actor.transform.position);
        public float ShotDistance => NativeDistance / 100f;
        public float ScoreDistance => LaunchMath26.HorizontalDistance(Player.position, World.Find("FrogPos").position);
        public float TableDistance => ShotDistance;
        public int PossibleScore => GameRules.DistanceScoreUnits(ScoreDistance, Session.IsSuperCandy) + (projectile ? projectile.RingBonus : 0);
        public ARMarkerMode ar;
        public BoostManager26 Boosts { get; private set; }
        public Vector3[] RingCenters { get; } = new Vector3[1];
        public event System.Action GameStarted;
        public event System.Action<bool> CandyResolved;
        public event System.Action BoostPassed;

        readonly TouchFlickListener26 input = new TouchFlickListener26();
        AudioSource effects, music, elasticAudio;
        Vector3 previousFingerPosition;
        bool pausingElastic;
        CandyProjectile projectile;
        CandyProjectile[] candies;
        GameObject slingshot, animElastic, superGhost;
        bool releasedElasticPending;
        Collider ghostCollider, touchPlane;
        Vector3 originalCandyPos, slingCandyPos, mousePrevious, stablePosition;
        Quaternion stableRotation;
        float shotDistance, messageUntil;
        int candyIndex;
        bool isCandyTapped, timeForNextShoot = true, hardStabilisation, pendingResults, resetFeatureBonus;

        void Awake()
        {
            if (!assets) assets = Resources.Load<GameAssets>("GameAssets");
            Application.targetFrameRate = 60;
            LockFpsToCamera = PlayerPrefs.GetInt(CameraFpsLockPreference, 0) == 1;
            QualitySettings.antiAliasing = 2;
            UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep;
            Physics.gravity = new Vector3(0, -2000, 0);
            Time.fixedDeltaTime = 0.01f; Time.maximumDeltaTime = 0.33333334f;
            SoundEnabled = PlayerPrefs.GetInt("IsSound", 1) == 1;
            MusicEnabled = PlayerPrefs.GetInt("IsMusic", 1) == 1;
            ControlMode = PlayerPrefs.GetInt(ControlModePreference, (int)LaunchMode.Slingshot) == (int)LaunchMode.Flick
                ? LaunchMode.Flick : LaunchMode.Slingshot;
            input.mode = (TouchMode26)ControlMode;
            effects = gameObject.AddComponent<AudioSource>();
            music = gameObject.AddComponent<AudioSource>(); music.clip = assets.music; music.loop = true;
            InitializeExtraOptions();
            if (MusicEnabled && !music.isPlaying) music.Play();
            Achievements=new LocalAchievements();
            BuildWorld();
            NonARBackground = gameObject.AddComponent<NonARStage>(); NonARBackground.Initialize(this);
            PhotoVisibility = gameObject.AddComponent<PhotoCandyVisibility>();
            PhotoVisibility.Initialize(this, World.Find("Candies"), CandyGhost, slingshot.transform, animElastic.transform);
            input.Began += OnTouchBegan; input.Tweaker += OnTouchTweaker;
            input.Flick += OnTouchFlick;
            input.TweakerCancel += () => { isCandyTapped = false; Actor.OnCancelShoot(); ResetCandyPos(); };
            input.Hold += touch => OnTouchHold(touch);
            var ui=gameObject.AddComponent<CandyFlickUI>();ui.game = this;
            Updates=gameObject.AddComponent<GameUpdateChecker>();Updates.Initialize(ui);
            ar = gameObject.AddComponent<ARMarkerMode>(); ar.Initialize(this);
            LAN = gameObject.AddComponent<LanMultiplayer>(); LAN.Initialize(this);
            ScreenState = GameScreen.Menu; SetWorldVisible(false);
        }

        void BuildWorld()
        {
            World = Instantiate(Resources.Load<GameObject>("Original26/Geometry")).transform;
            World.name = "Original 2.6 scene units";
            Player = World.Find("Player");
            GameCamera = Player.Find("Camera").GetComponent<Camera>();
            CandyCamera = Player.Find("candycamera").GetComponent<Camera>();
            GameCamera.farClipPlane = CandyCamera.farClipPlane = GameplayFarClip;
            GameCamera.tag = "MainCamera"; GameCamera.gameObject.AddComponent<AudioListener>();
            Actor = World.Find("OmNom@All-1").gameObject.AddComponent<OmNomActor>(); Actor.InitializeOriginal(this);
            Skins = Actor.gameObject.AddComponent<TimeTravelSkins>(); Skins.Initialize(assets);
            CandyGhost = Player.Find("CandyGhost"); ghostCollider = CandyGhost.GetComponent<Collider>();
            touchPlane = Player.Find("CandyTouchPlane").GetComponent<Collider>();
            slingshot = Player.Find("elastic").gameObject; animElastic = Player.Find("test").gameObject;
            animElastic.SetActive(false);
            slingshot.AddComponent<SlingshotVisual>().Initialize(this,Player,false);
            // CorrectCandyPosition (iPhone5: y-25, z+70), then PlayerBehaviour.Start 0x24a14
            // stores startAnimPos = corrected + (0,0,-100); non-Tweaker modes place the ghost there.
            originalCandyPos = CandyGhost.localPosition + new Vector3(0, -25, 70 - 100);
            slingCandyPos = Player.Find("CandySlingshotSlot").localPosition;
            ConfigureGround();
            superGhost = CandyGhost.Find("CandyGhost/SuperEffect").gameObject;
            elasticAudio = Player.Find("AudioElastic").gameObject.AddComponent<AudioSource>();
            elasticAudio.clip = assets.elasticStretch; elasticAudio.loop = true; elasticAudio.playOnAwake = false;
            var pool = World.Find("Candies");
            candies = new CandyProjectile[3];
            string[] names = { "Candy1", "Candy2", "Candy3" };
            for (int i = 0; i < names.Length; i++)
            { candies[i] = pool.Find(names[i]).gameObject.AddComponent<CandyProjectile>(); candies[i].Initialize(this); }
            CandySkins=gameObject.AddComponent<CandySkinSettings>();CandySkins.Initialize(this);
            Boosts = gameObject.AddComponent<BoostManager26>(); Boosts.Initialize(this);
            stablePosition = Player.position; stableRotation = Player.localRotation;
            touchPlane.gameObject.SetActive(ControlMode == LaunchMode.Slingshot);
            ResetCandyPos();
        }

        // Compatibility: the serialized Floor is a flat built-in Plane with a convex MeshCollider
        // at y=-750, while Om Nom, FrogPos, the candy shadows and the AR marker surface are at y=0.
        // A flat convex hull is not a reliable collider in modern PhysX, and at -750 the candy
        // visibly sinks through the table. Use a solid box whose top face is the marker plane.
        void ConfigureGround()
        {
            Transform floor = World.Find("Floor");
            if (!floor) return;
            var mesh = floor.GetComponent<MeshCollider>();
            PhysicsMaterial material = mesh ? mesh.sharedMaterial : null;
            if (mesh) Destroy(mesh);
            floor.localPosition = new Vector3(floor.localPosition.x, 0, floor.localPosition.z);
            var box = floor.gameObject.AddComponent<BoxCollider>();
            const float thickness = 200; // Native units; candies move at most ~25 units per .01 s step.
            box.size = new Vector3(10, thickness / floor.lossyScale.y, 10); // Built-in Plane is 10x10.
            box.center = new Vector3(0, -box.size.y / 2, 0);
            box.sharedMaterial = material;
        }

        public void StartGame(bool augmented = false)
        {
            EndPhotoMode();
            resetFeatureBonus = ScreenState == GameScreen.Playing || ScreenState == GameScreen.Paused;
            StopAllCoroutines(); input.Cancel(); Time.timeScale = 1;
            IsNonAR = NonARModeEnabled && !IsLanMatch; IsAR = (augmented || IsLanMatch) && !IsNonAR;
            music.Stop();
            foreach (var candy in candies) candy.Restore();
            projectile = null; candyIndex = 0; releasedElasticPending = false;
            Session = new GameSession { bonus = NextGameBonus };
            if(!IsLanMatch)Achievements.BeginGame();
            LastScore = 0; pendingResults = false; timeForNextShoot = true; hardStabilisation = false;
            Skins.RerollRandom();
            ConfigureNonARBackground(); Actor.ResetOriginal();
            if (IsNonAR) Actor.FacePlayer();
            ScreenState = GameScreen.Playing; SetWorldVisible(true);
            GameCamera.clearFlags = IsAR ? CameraClearFlags.Depth : CameraClearFlags.SolidColor;
            GameCamera.backgroundColor = Color.black;
            if (IsAR && !(IsLanMatch && LAN.SkipEditorCamera)) ar.Begin(); else ar.End();
            Boosts.Reset(); Boosts.OnNewRound(Session.round); PrepareBulletForShoot(); StartGameplayMusic(); GameStarted?.Invoke();
        }

        void Update()
        {
            if(IsPhotoMode&&!string.IsNullOrEmpty(ar.PhotoSupportFailure))
            {
                string reason=ar.PhotoSupportFailure;ReturnToMenu();GetComponent<CandyFlickUI>().ShowPhotoSupportWarning(reason);return;
            }
            SyncGameplayMusic();
            if (Time.unscaledTime > messageUntil) Message = "";
            if (Input.GetKeyDown(KeyCode.Escape) && !CandyFlickUI.PopupActive)
            {
                if (ScreenState == GameScreen.Playing && IsPhotoMode && PhotoSelection) DeselectPhotoActor();
                else if (ScreenState == GameScreen.Playing) Pause();
                else if (ScreenState == GameScreen.Paused) Resume();
                else if (ScreenState == GameScreen.Tutorial || ScreenState == GameScreen.Help || ScreenState == GameScreen.Settings || ScreenState == GameScreen.TargetOptions || ScreenState == GameScreen.Marker || ScreenState == GameScreen.Skins)
                    GetComponent<CandyFlickUI>().Back();
                else ReturnToMenu();
            }
            if (ScreenState == GameScreen.Playing) Time.timeScale = IsLanMatch ? (LAN.Playing ? 1 : 0) : IsAR && !ar.Tracking ? 0 : 1;
            if(IsPhotoMode) TickPhotoMode();
            if(Ready)releasedElasticPending=false;
            bool released=CanSimulate&&releasedElasticPending&&ControlMode==LaunchMode.Slingshot;
            animElastic.SetActive(released);CandyGhost.gameObject.SetActive(Ready);
            slingshot.SetActive(CanSimulate&&ControlMode==LaunchMode.Slingshot&&(Ready||released));
            if (IsAR && ar.ConsumePlacementInput()) return;
            if (!CanSimulate) return;
            if (IsNonAR && NonARBackground.Navigation.PollInput()) return;
            if (Input.touchCount > 0)
            {
                Touch touch = Input.GetTouch(Input.touchCount - 1); // Original uses the last touch.
                SampleInput(touch.phase, touch.position, touch.deltaPosition);
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            else
            {
                // Editor compatibility: a mouse gesture supplies the original touch sampler.
                Vector2 point = Input.mousePosition, delta = point - (Vector2)mousePrevious;
                if (Input.GetMouseButtonDown(0)) SampleInput(TouchPhase.Began, point, Vector2.zero);
                else if (Input.GetMouseButtonUp(0)) SampleInput(TouchPhase.Ended, point, delta);
                else if (Input.GetMouseButton(0)) SampleInput(delta == Vector2.zero ? TouchPhase.Stationary : TouchPhase.Moved, point, delta);
                mousePrevious = point;
            }
#endif
        }

        public void SampleInput(TouchPhase phase, Vector2 position, Vector2 delta)
        {
            if (!CanSimulate) return;
            if (CandyFlickUI.PopupActive) { input.Cancel(); return; } // Touches belong to the popup.
            input.Sample(phase, position, delta, Time.time, Time.deltaTime, UnityEngine.Screen.width, UnityEngine.Screen.height);
            if (input.IsTouchBegan && isCandyTapped && ControlMode == LaunchMode.Slingshot)
            {
                Physics.SyncTransforms(); // CandyPlane.Update, 0x220dc: raycast the original collider.
                // A forward push stays at rest; only a backward pull stretches the sling.
                if(position.y>=input.Start.y) { ResetCandyPos(); return; }
                Ray ray=GameCamera.ScreenPointToRay(position);
                if (touchPlane.Raycast(ray, out RaycastHit hit, Mathf.Infinity)) MoveCandyToPoint(hit.point);
                else
                {
                    // A wide portrait AR projection can extend below the original
                    // finite collider. Continue an owned pull on the same plane.
                    var plane=new Plane(touchPlane.transform.up,touchPlane.bounds.center);
                    if(plane.Raycast(ray,out float distance)) MoveCandyToPoint(ray.GetPoint(distance));
                }
            }
        }
        void OnTouchBegan(Vector2 point)
        {
            isCandyTapped = false;
            if (!Ready) return;
            Physics.SyncTransforms();
            isCandyTapped = ghostCollider.Raycast(GameCamera.ScreenPointToRay(point), out _, Mathf.Infinity);
            if (ControlMode == LaunchMode.Slingshot)
            {
                if (isCandyTapped && TouchFlickListener26.CheckTouchPosSlingshot(point, UnityEngine.Screen.width, UnityEngine.Screen.height)) Actor.OnPreShoot();
            }
            else if (CanStartFlick(point)) Actor.OnPreShoot(); // OnTouchBegan 0x25b24, non-Tweaker branch.
        }
        // PlayerBehaviour.CheckTouchPosition 0x254c4 with InitTouchRadius 0x24bb8 (IS_IPHONE: radius = width/2).
        public static bool CheckTouchPosition(Vector2 start)
        {
            int width = UnityEngine.Screen.width, height = UnityEngine.Screen.height;
            if ((start.x > width * .8f || start.x < width * .2f) && start.y < height * .15f) return false;
            Vector2 center = new Vector2(width / 2, height);
            return Vector2.Distance(center, new Vector2(start.x, height - start.y)) <= width / 2;
        }
        bool CanStartFlick(Vector2 point)=>CheckTouchPosition(point)||(IsNonAR&&ghostCollider.Raycast(GameCamera.ScreenPointToRay(point),out _,Mathf.Infinity));
        void OnTouchTweaker(TouchFlick26 touch)
        {
            if (Ready && isCandyTapped && TouchFlickListener26.CheckTouchPosSlingshot(touch.startTouch, UnityEngine.Screen.width, UnityEngine.Screen.height))
                PrepareAndShootBullet(LaunchMath26.SlingshotStrength(touch.distance), 0, -touch.angle);
            isCandyTapped = false; ResetCandyPos();
        }
        void OnTouchFlick(TouchFlick26 touch)
            => ShootCheckedTouch(touch, touch.speed, touch.distance, touch.angle);
        void OnTouchHold(TouchFlick26 touch)
            => ShootCheckedTouch(touch, LaunchMath26.HoldStrength(touch.holdTime), 0, 0);
        void ShootCheckedTouch(TouchFlick26 touch, float speed, float ratio, float angle)
        {
            if (Ready && CanStartFlick(touch.startTouch)) PrepareAndShootBullet(speed, ratio, angle);
        }

        // PrepareAndShootBullet 0x255f8 / ShootBullet 0x25fd0 / ObjectShooter.Shoot 0x26264.
        public void PrepareAndShootBullet(float rawSpeed, float angleXRatio, float angle)
        {
            if (!Ready) return;
            if(IsLanMatch) { LAN.Throw(rawSpeed,angleXRatio,angle); ShowShotFeedback(); return; }
            projectile = candies[candyIndex]; candyIndex = (candyIndex + 1) % candies.Length;
            projectile.transform.SetPositionAndRotation(CandyGhost.position, CandyGhost.rotation);
            shotDistance = (int)ScoreDistance;
            Quaternion saved = Player.localRotation;
            Player.localRotation = LaunchMath26.ShootRotation(Player.localEulerAngles.y, angleXRatio, angle);
            float speed = CalculateLaunchSpeed(rawSpeed);
            CandySkins.ApplyShot(projectile.transform);
            projectile.LaunchImpulse(Player.forward * speed, Vector3.up * speed * speed, Session.IsSuperCandy);
            if(IsPhotoMode)photoCandyResolved=false;
            if(!IsPhotoMode) Achievements.ThrowCandy();
            if(IsPhotoMode) { foreach(var nom in PhotoActors) if(nom.Tracked) nom.Actor.OnBulletShoot(projectile); }
            else Actor.OnBulletShoot(projectile);
            Player.localRotation = saved;
            hardStabilisation = true; timeForNextShoot = false;
            ShowShotFeedback();
        }
        internal float CalculateLaunchSpeed(float rawSpeed)
        {
            var target=PhotoFeedTarget;
            float height=target?Mathf.Max(500,Player.position.y-target.transform.position.y):Player.position.y;
            float distance=target?LaunchMath26.HorizontalDistance(Player.position,target.transform.position):NativeDistance;
            return LaunchMath26.CalculateSpeed(rawSpeed,height,distance);
        }
        void ShowShotFeedback()
        {
            CandyGhost.gameObject.SetActive(false);
            elasticAudio.Stop();
            // Native 2.6 always forces Tweaker mode, so its unreachable Flick path never showed this
            // recoil. The port-only Flick switch has no slingshot: skip the elastic clip and sound.
            bool sling = ControlMode == LaunchMode.Slingshot;
            releasedElasticPending=sling;slingshot.SetActive(sling);
            if(sling){animElastic.SetActive(true);var recoil=animElastic.GetComponent<Animation>();recoil.Stop();recoil.wrapMode=WrapMode.ClampForever;recoil[recoil.clip.name].wrapMode=WrapMode.ClampForever;recoil.Play();}
            if (SoundEnabled)
            {
                if(!IsLanMatch&&!IsPhotoMode)effects.Stop();
                if (sling) effects.PlayOneShot(assets.elasticShoot);
                Play(assets.swishes[Random.Range(0, assets.swishes.Length - 1)]);
            }
            LastScore = 0;
        }

        public void SetTrackedView(Vector3 position, Quaternion rotation, Matrix4x4 projection, bool snap = false)
        {
            float blend = snap ? 1 : Time.unscaledDeltaTime * (hardStabilisation ? 5 : 18); // CameraStabilisation; keep the image aligned while paused.
            stablePosition = Vector3.Lerp(stablePosition, position, blend);
            stableRotation = Quaternion.Lerp(stableRotation, rotation, blend);
            Player.SetPositionAndRotation(stablePosition, stableRotation);
            SetProjection(GameCamera, projection);
            CandyCamera.projectionMatrix = GameCamera.projectionMatrix; // Original CandyCamera.Update.
        }
        static void SetProjection(Camera camera, Matrix4x4 projection)
        {
            float near = camera.nearClipPlane, far = camera.farClipPlane;
            projection.m22 = -(far + near) / (far - near); projection.m23 = -2 * far * near / (far - near);
            camera.projectionMatrix = projection;
        }
        float lanElasticMotionUntil;
        internal bool LanElasticMoving => IsLanMatch && Ready && isCandyTapped && ControlMode==LaunchMode.Slingshot && Time.unscaledTime<lanElasticMotionUntil;
        void ResetCandyPos() { elasticAudio.Stop(); CandyGhost.localPosition = ControlMode == LaunchMode.Slingshot ? slingCandyPos : originalCandyPos; }
        void MoveCandyToPoint(Vector3 point)
        {
            if (!isCandyTapped) return;
            CandyGhost.position = point;
            Vector3 local = Player.InverseTransformPoint(point);
            float distance = Vector3.Distance(local, previousFingerPosition);
            if(IsLanMatch&&distance>.25f)lanElasticMotionUntil=Time.unscaledTime+.12f;
            if (distance > .25f && !elasticAudio.isPlaying)
            { elasticAudio.volume = 1; pausingElastic = false; if (SoundEnabled) elasticAudio.Play(); }
            else if (distance < .25f && elasticAudio.isPlaying)
            { pausingElastic = true; StartCoroutine(PauseElasticSound()); }
            previousFingerPosition = local;
        }
        IEnumerator PauseElasticSound()
        {
            yield return new WaitForSeconds(.03f);
            if (!pausingElastic) yield break;
            elasticAudio.volume -= .1f;
            if (elasticAudio.volume == 0) { elasticAudio.volume = 1; elasticAudio.Pause(); }
            else StartCoroutine(PauseElasticSound());
        }
        void PrepareBulletForShoot(bool nextCandy=true)
        {
            if(nextCandy)CandySkins.PrepareNext();
            if(Ready){releasedElasticPending=false;animElastic.SetActive(false);}
            ResetCandyPos(); isCandyTapped = false; CandyGhost.gameObject.SetActive(Ready);
            if (superGhost) superGhost.SetActive(Session.IsSuperCandy);
        }
        public void ResolveShot(bool hit, int ringBonus)
        {
            if (!projectile) return;
            if(IsPhotoMode) { projectile.Restore(); projectile=null; photoCandyResolved=false; timeForNextShoot=true; hardStabilisation=false; PrepareBulletForShoot(); return; }
            bool super=Session.IsSuperCandy;
            LastScore = Session.RecordUnits(hit, shotDistance, ringBonus); projectile = null;
            if(!IsLanMatch)Achievements.Resolve(hit,super,ringBonus>0,LastScore,Actor.Moving);
            timeForNextShoot = true; hardStabilisation = false; Boosts.CheckBoost();
            PrepareBulletForShoot(); // Refresh the next candy's super effect after the score advances.
            CandyResolved?.Invoke(hit);
            if (Session.IsOver)
            {
                NextGameBonus = Session.baseTotal == 550 ? (resetFeatureBonus ? 0 : Session.bonus) + 20 : 0;
                if (Session.CompleteScore > Best) PlayerPrefs.SetInt("BestScore", Session.CompleteScore);
                PlayerPrefs.SetInt("TimesPlayed", PlayerPrefs.GetInt("TimesPlayed", 0) + 1); PlayerPrefs.Save();
                pendingResults = true;
            }
            else Boosts.OnNewRound(Session.round);
        }
        // ScoreBehaviour waits for FrogIdle's OnReadyForShoot notification before showing results.
        public void OnReadyForShoot()
        {
            if (!pendingResults) return;
            pendingResults = false; ScreenState = GameScreen.Results; Play(assets.win);
        }
        public void NextRound() => StartGame(IsAR);
        public void Pause() { if (ScreenState != GameScreen.Playing) return; input.Cancel(); NonARBackground.Navigation.Cancel(); ScreenState = GameScreen.Paused; Time.timeScale = 0; SyncGameplayMusic(); }
        public void Resume() { ScreenState = GameScreen.Playing; Time.timeScale = IsLanMatch?(LAN.Playing?1:0):1; SyncGameplayMusic(); }
        public void ReturnToMenu()=>ReturnToMenu(false);
        internal void ReturnToMenu(bool keepGround)
        {
            if(LAN && (LAN.InMatch || ScreenState==GameScreen.LanLobby)) LAN.CloseRoom();
            StopAllCoroutines(); input.Cancel(); Time.timeScale = 1;
            foreach (var candy in candies) candy.Restore();
            projectile = null; pendingResults = false; Boosts.Reset(); if(keepGround)ar.RetainLanGround();else ar.End(); EndPhotoMode(); IsAR = false; SetWorldVisible(false); ScreenState = GameScreen.Menu;
            StopGameplayMusic(); PhotoVisibility.End();
            NonARBackground.End(); IsNonAR = false;
            if (MusicEnabled && !music.isPlaying) music.Play();
        }
        void SetWorldVisible(bool visible)
        {
            foreach (var renderer in World.GetComponentsInChildren<Renderer>(true)) renderer.forceRenderingOff = !visible;
            CandyCamera.enabled = visible;
        }
        public void Show(GameScreen screen) { ScreenState = screen; }
        public void StopMenuMusic() { music.Stop(); }
        // Port-only preference; both gesture implementations remain the recovered 2.6 logic.
        public void SetMode(LaunchMode mode)=>ApplyControlMode(mode,true);
        internal void ApplySessionControlMode(LaunchMode mode)=>ApplyControlMode(mode,false);
        void ApplyControlMode(LaunchMode mode,bool persist)
        {
            if(mode != LaunchMode.Flick && mode != LaunchMode.Slingshot) return;
            input.Cancel(); isCandyTapped = false; releasedElasticPending=false; animElastic.SetActive(false); Actor.OnCancelShoot();
            ControlMode = mode; input.mode = (TouchMode26)mode;
            touchPlane.gameObject.SetActive(mode == LaunchMode.Slingshot); // OnTouchModeChange 0x24e80.
            if(persist){PlayerPrefs.SetInt(ControlModePreference,(int)mode);PlayerPrefs.Save();if(LAN)LAN.SetControlPreference(mode);}
            PrepareBulletForShoot(false);
        }
        public void ToggleControlMode() => SetMode(ControlMode == LaunchMode.Slingshot ? LaunchMode.Flick : LaunchMode.Slingshot);
        internal void CancelCandyGesture()
        {
            input.Cancel(); isCandyTapped = false; Actor.OnCancelShoot(); ResetCandyPos();
        }
        public void ToggleCameraFpsLock()
        {
            LockFpsToCamera = !LockFpsToCamera;
            PlayerPrefs.SetInt(CameraFpsLockPreference, LockFpsToCamera ? 1 : 0); PlayerPrefs.Save();
            ar.RefreshFrameRate();
        }
        public void ToggleSound() { SoundEnabled = !SoundEnabled; PlayerPrefs.SetInt("IsSound", SoundEnabled ? 1 : 0); PlayerPrefs.Save(); }
        public void ToggleMusic()
        {
            MusicEnabled = !MusicEnabled; PlayerPrefs.SetInt("IsMusic", MusicEnabled ? 1 : 0);
            if (MusicEnabled && !gameplayMusicSession) music.Play(); else music.Stop();
            SyncGameplayMusic(); PlayerPrefs.Save();
        }
        public void ResetProgress()
        {
            int language=PlayerPrefs.GetInt("Language",PortLocalization.Language);
            NextGameBonus = 0; PlayerPrefs.DeleteAll(); PlayerPrefs.SetInt("Language",language);
            Achievements.Reload(); SetMode(LaunchMode.Slingshot);
            Skins.Select(NommieSkin.Original);CandySkins.Select(0);
            LockFpsToCamera = false; ar.RefreshFrameRate();
            HideCandyInPictures = InGameMusicEnabled = true; NonARModeEnabled = GroundARModeEnabled = false; SyncGameplayMusic();
        }
        public void Play(AudioClip clip, float volume = 1) { if (SoundEnabled && clip) effects.PlayOneShot(clip, volume); }
        public void Say(string text, float seconds) { Message = text; messageUntil = Time.unscaledTime + seconds; }
        public void RingHit(int index) { Play(assets.ring); BoostPassed?.Invoke(); }
        public void OnTrackingChanged(bool tracking)
        {
            if (tracking) return;
            input.Cancel(); isCandyTapped = false; animElastic.SetActive(false);
        }
        // Editor inspection helper; device launching never solves a trajectory toward the target.
        public void UpdateTableDistance(float distance)
        {
            if (IsAR || IsNonAR || projectile || IsDragging) return;
            Player.position = new Vector3(0, 900, Actor.transform.position.z - Mathf.Clamp(distance, 3, 11.05f) * 100);
            stablePosition = Player.position;
        }
        void OnApplicationPause(bool paused) { if (paused && ScreenState == GameScreen.Playing) Pause(); }
        void OnDestroy() { if(LAN)LAN.CloseRoom(); Time.timeScale = 1; if (World) Destroy(World.gameObject); }
    }
}
