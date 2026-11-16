using System.Collections;
using System.Linq;
using UnityEngine;

namespace CandyFlick
{
    // FrogBehaviour: retain the original rig, clip settings and scene scale.
    public sealed class OmNomActor : MonoBehaviour
    {
        CandyFlickGame game;
        Animation animationPlayer;
        CandyProjectile bullet, pendingCandy;
        double pendingCatchTime;
        double lastStatusPhysicsTime;
        Vector3 startPosition, lastPosition, targetPosition;
        Quaternion startRotation;
        float distanceToBullet, startIdleTime, startJumpTime, moveProgress;
        public bool Moving=>moveProgress>0&&moveProgress<1;
        int frogMode, frogStatus, frogPosition, hitAudioIndex;
        bool tapped, isCandyTouched;
        float loveTouchTicker, startHeartTime;
        Renderer[] pauseRenderers;
        public Vector3 Mouth => transform.TransformPoint(new Vector3(0, .25f, 0));
        public void InitializeOriginal(CandyFlickGame owner)
        {
            game = owner; animationPlayer = GetComponent<Animation>();
            startPosition = lastPosition = targetPosition = transform.position;
            startRotation = transform.rotation;
            // The original FrogPauseHider components are on OmNom_Mesh and Shadow.
            RefreshRenderers();
            foreach (var renderer in GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            FrogIdle();
        }
        public void RefreshRenderers() => pauseRenderers = GetComponentsInChildren<Renderer>(true);
        public void ResetOriginal()
        {
            StopAllCoroutines(); bullet = pendingCandy = null; frogStatus = frogPosition = hitAudioIndex = 0; // OnGameRestart 0x240dc resets the combo pitch.
            tapped = isCandyTouched = false; loveTouchTicker = startHeartTime = 0;
            Vector3 groundedStart=startPosition+(game.IsNonAR?Vector3.up*NonARStage.GroundClearance:Vector3.zero);
            transform.SetPositionAndRotation(groundedStart, startRotation);
            lastPosition = targetPosition = groundedStart; moveProgress = 0; FrogIdle();
        }
        internal void MoveGround(float heightChange)
        {
            Vector3 offset=Vector3.up*heightChange;
            transform.position+=offset;lastPosition+=offset;targetPosition+=offset;
        }
        void Fade(string name, float seconds = .3f)
        {
            if(game.Skins && game.Skins.Selected==NommieSkin.Prehistoric) seconds=name=="open"?.045f:.1f;
            animationPlayer.CrossFade(name,seconds);
        }
        void FrogIdle() { startIdleTime = Time.time; Fade("idle"); frogMode = 0; game.OnReadyForShoot(); }
        public void Play(string name, bool loop = false)
        {
            if (!animationPlayer[name]) return;
            Fade(name); frogMode = name == "idle" ? 0 : 2;
        }
        public void OnBulletShoot(CandyProjectile candy)
        {
            pendingCandy = null; isCandyTouched = false; frogStatus = 1; bullet = candy;
            distanceToBullet = Vector3.Distance(transform.position, candy.transform.position);
            lastStatusPhysicsTime = Time.fixedTimeAsDouble;
        }
        public void OnCancelShoot() { isCandyTouched = false; }
        void Update()
        {
            if (!game || !game.CanSimulate) return;
            if(game.IsLanMatch) { game.LAN.TickActor(); return; }
            // Resolve after all physics contacts for the step have been reported, so a
            // simultaneous floor/actor contact cannot depend on callback order.
            if (pendingCandy)
            {
                var candidate = pendingCandy; pendingCandy = null;
                if (candidate == bullet && candidate.CanBeEatenAt(pendingCatchTime)) EatCandy();
            }
            UpdateFrogTouching();
            // A fast renderer can run several times between physics steps. An
            // unchanged position in those frames does not mean the candy stopped.
            double physicsTime = Time.fixedTimeAsDouble;
            if (physicsTime != lastStatusPhysicsTime)
            {
                lastStatusPhysicsTime = physicsTime;
                UpdateFrogStatus();
            }
            bool canJump = game.Session.round > 1;
            if (frogMode == 0)
            {
                if (!canJump && Time.time - startIdleTime >= 5)
                {
                    startIdleTime = Time.time;
                    var random=new System.Random();
                    int variation=game.Skins.Selected==NommieSkin.Prehistoric?(random.Next(2)==0?2:4):random.Next(2,5);
                    Fade("idle"+variation);
                    animationPlayer.PlayQueued("idle");
                }
                // ARM 0x22a6a explicitly loads 2.0; Ghidra's NEON pseudocode loses this literal.
                if (canJump && Time.time - startJumpTime >= 2) JumpOmNomRandom();
            }
            if (!animationPlayer.isPlaying) FrogIdle();
        }
        void UpdateFrogStatus()
        {
            if (frogStatus == 0 || !bullet) return;
            float distance = Vector3.Distance(transform.position, bullet.transform.position);
            if (frogStatus == 1 && distance <= 800)
            {
                frogStatus = 2; FacePlayer(); frogMode = 1;
                Fade("open"); game.Play(game.Skins.Sound(NommieSound.Open));
            }
            // Native UpdateFrogStatus compares successive distances exactly, even after Restore.
            if (distanceToBullet - distance == 0) BulletMiss();
            else distanceToBullet = distance;
        }
        void OnCollisionEnter(Collision collision)
        {
            if (!bullet || collision.gameObject != bullet.gameObject || !bullet.IsShot || pendingCandy) return;
            pendingCandy = bullet; pendingCatchTime = Time.fixedTimeAsDouble;
        }
        void EatCandy()
        {
            int bonus = bullet.RingBonus;
            bullet.Restore(); bullet = null; frogStatus = 0; frogMode = 2;
            Fade("close_eat", .3f);
            game.Play(game.Skins.Sound(NommieSound.Close)); game.Play(game.Skins.Sound(NommieSound.Chew));
            game.Play(game.assets.hits[hitAudioIndex]); hitAudioIndex = (hitAudioIndex + 1) % game.assets.hits.Length;
            game.ResolveShot(true, bonus);
        }
        void BulletMiss()
        {
            bullet = pendingCandy = null; frogStatus = 0; frogMode = 2;
            Fade("close_sad"); game.Play(game.Skins.Sound(NommieSound.Close)); game.Play(game.Skins.Sound(NommieSound.Sad));
            game.ResolveShot(false, 0);
        }
        public void OnPreShoot()
        {
            if(game.IsLanMatch)return;
            isCandyTouched = true;
            if (game.Session.round > 1 && !animationPlayer.IsPlaying("jump"))
            {
                StartCoroutine(WaitAndRotate(.6f)); FrogIdle(); JumpOmNomRandom(); return;
            }
            float yaw = transform.eulerAngles.y;
            transform.LookAt(game.Player); float delta = transform.eulerAngles.y - yaw;
            if (delta > 5) { animationPlayer.Play("turn_right"); frogMode = 3; }
            else if (delta < -5) { animationPlayer.Play("turn_left"); frogMode = 3; }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        IEnumerator WaitAndRotate(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            OnPreShootRotation();
        }
        void OnPreShootRotation()
        {
            float yaw = transform.eulerAngles.y;
            transform.LookAt(game.Player); float delta = transform.eulerAngles.y - yaw;
            if (delta > 5) { animationPlayer.Play("turn_right"); frogMode = 3; }
            else if (delta < -5) { animationPlayer.Play("turn_left"); frogMode = 3; }
            transform.rotation = Quaternion.Euler(0, yaw, 0);
        }
        public void FacePlayer() { transform.LookAt(game.Player); transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0); }
        void UpdateFrogTouching()
        {
            bool touched = false; Vector3 point = Vector3.zero; Vector2 delta = Vector2.zero;
            bool cameraGesture = game.IsNonAR && (Input.touchCount > 1 || game.NonARBackground.Navigation.BlocksPetting);
            if (cameraGesture) { tapped = false; loveTouchTicker = 0; }
            Collider collider = GetComponent<Collider>();
            for (int i = 0; !cameraGesture && i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (!collider.Raycast(game.GameCamera.ScreenPointToRay(touch.position), out RaycastHit hit, Mathf.Infinity)) continue;
                point = hit.point; delta = touch.deltaPosition; touched = true; break;
            }
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!cameraGesture && Input.touchCount == 0 && Input.GetMouseButton(0) && collider.Raycast(game.GameCamera.ScreenPointToRay(Input.mousePosition), out RaycastHit mouseHit, Mathf.Infinity))
            { point = mouseHit.point; delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")); touched = true; }
#endif
            if (touched)
            {
                if (frogMode == 0)
                {
                    loveTouchTicker += Time.deltaTime;
                    if (loveTouchTicker >= .15f)
                    {
                        tapped = false;
                        string side = transform.InverseTransformPoint(point).x > 0 ? "_left" : "_right";
                        frogMode = 4; game.Play(game.Skins.Sound(NommieSound.Pet));
                        animationPlayer["touch" + side].wrapMode = WrapMode.Loop;
                        animationPlayer.Play("touch" + side + "_in"); animationPlayer.PlayQueued("touch" + side);
                    }
                    else tapped = true;
                }
                if (frogMode == 4 && delta.magnitude > 10 && Time.time - startHeartTime >= .8f) InstantiateHeart();
            }
            else
            {
                if (tapped) { frogMode = 6; animationPlayer.Play("tap"); game.Play(game.Skins.Sound(NommieSound.Tap)); tapped = false; }
                if (frogMode == 4)
                {
                    if (animationPlayer.IsPlaying("touch_left") || animationPlayer.IsPlaying("touch_left_in")) Fade("touch_left_out");
                    if (animationPlayer.IsPlaying("touch_right") || animationPlayer.IsPlaying("touch_right_in")) Fade("touch_right_out");
                    frogMode = 5;
                }
                loveTouchTicker = 0;
            }
        }
        void InstantiateHeart()
        {
            startHeartTime = Time.time;
            Vector3 position = transform.position - transform.forward * 100; position.y = 100;
            Vector3 angles = transform.eulerAngles; angles.y += 180;
            var wrapper = Instantiate(Resources.Load<GameObject>("Original26/Heart"), game.World);
            var root = wrapper.transform.Find("HeartGlobalPos"); root.SetPositionAndRotation(position, Quaternion.Euler(angles));
            var animation = root.GetComponent<Animation>(); animation["heart_anim"].speed = 1.5f; animation.Play("heart_anim");
            Destroy(wrapper, 3);
        }
        void JumpOmNomRandom()
        {
            int next = 0; Vector3 offset = Vector3.zero;
            if (frogPosition == 0)
            {
                int[] positions = { 3, 4, 1, 2 }; next = positions[new System.Random().Next(4)];
                offset = next == 1 ? Vector3.right : next == 2 ? Vector3.left : next == 3 ? Vector3.back : Vector3.forward;
            }
            else offset = frogPosition == 1 ? Vector3.left : frogPosition == 2 ? Vector3.right : frogPosition == 3 ? Vector3.forward : Vector3.back;
            StartCoroutine(WaitAndMoveForJump(offset * 150)); frogPosition = next;
            Fade("jump"); animationPlayer.CrossFadeQueued("idle");
            frogMode = 0; startJumpTime = Time.time;
        }
        IEnumerator WaitAndMoveForJump(Vector3 offset)
        {
            yield return new WaitForSeconds(.15f);
            transform.position += offset;
        }
        void LateUpdate()
        {
            if (game)
            {
                bool visible = game.ScreenState == GameScreen.Playing && (game.IsLanMatch ? game.LAN.LocalTracking : !game.IsAR || game.ar.Tracking);
                foreach (var renderer in pauseRenderers) renderer.enabled = visible && (!game.Skins || game.Skins.Includes(renderer));
            }
            if (!game || !game.CanSimulate || game.IsLanMatch) return;
            if (frogMode == 3)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(game.Player.position - transform.position), Time.deltaTime * 6);
                transform.eulerAngles = new Vector3(0, transform.eulerAngles.y, 0);
            }
            if (moveProgress == 0 && transform.position != lastPosition) targetPosition = transform.position;
            if (lastPosition != targetPosition)
            {
                moveProgress = Mathf.Min(1, moveProgress + Time.deltaTime / (animationPlayer["jump"].length - .15f - .15f));
                transform.position = Vector3.Lerp(lastPosition, targetPosition, moveProgress);
                if (moveProgress == 1) { moveProgress = 0; lastPosition = targetPosition; }
            }
        }
        public static Bounds CalculateBounds(GameObject obj)
        {
            var renderers = obj.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.gameObject.activeInHierarchy).ToArray();
            Bounds bounds = renderers.Length > 0 ? renderers[0].bounds : new Bounds(obj.transform.position, Vector3.one);
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
