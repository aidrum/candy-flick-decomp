using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CandyFlick
{
    // The supplied room is authored in scene units; its one-unit marker represents
    // the recovered game's 200-unit card. Keep the native actor and projectile scale.
    public sealed class NonARStage : MonoBehaviour
    {
        public const int StageLayer = 29;
        public const float GroundClearance = .1f; // Native units: separate gameplay from the room card.
        public GameObject Room { get; private set; }
        public Transform Marker { get; private set; }
        public NonARViewControls Navigation { get; private set; }
        public bool Active => Room && Room.activeSelf;
        CandyFlickGame game;
        Transform floor;
        Vector3 originalFloorPosition;
        Vector3 originalPlayerPosition;
        Quaternion originalPlayerRotation;
        readonly Dictionary<Light,bool> sceneLights = new Dictionary<Light,bool>();
        bool lightingOverridden;
        AmbientMode previousAmbientMode;
        Color previousSky, previousEquator, previousGround;
        float previousAmbientIntensity, previousReflectionIntensity;
        SphericalHarmonicsL2 previousAmbientProbe;
        [Serializable]sealed class Lens{public float aspect,x,y,cx,cy;}
        public const string LensPreference="PortNonARCameraLens";
        Lens lens;bool lensDirty;int projectionWidth,projectionHeight;
	public void RememberARProjection(Matrix4x4 projection, float aspect)
	{
		if (float.IsFinite(aspect) && !(aspect <= 0f) && float.IsFinite(projection.m00) && float.IsFinite(projection.m11) && !(projection.m00 <= 0f) && !(projection.m11 <= 0f) && float.IsFinite(projection.m02) && float.IsFinite(projection.m12) && !(Mathf.Abs(projection.m32 + 1f) > 0.001f))
		{
			Lens lens = new Lens
			{
				aspect = aspect,
				x = projection.m00,
				y = projection.m11,
				cx = projection.m02,
				cy = projection.m12
			};
			if (this.lens == null || !(Mathf.Abs(this.lens.aspect - aspect) < 0.0001f) || !(Mathf.Abs(this.lens.x - lens.x) < 0.0001f) || !(Mathf.Abs(this.lens.y - lens.y) < 0.0001f) || !(Mathf.Abs(this.lens.cx - lens.cx) < 0.0001f) || !(Mathf.Abs(this.lens.cy - lens.cy) < 0.0001f))
			{
				this.lens = lens;
				lensDirty = true;
			}
		}
	}

	public void SaveARLens()
	{
		if (lensDirty)
		{
			PlayerPrefs.SetString("PortNonARCameraLens", JsonUtility.ToJson(lens));
			PlayerPrefs.Save();
			lensDirty = false;
		}
	}

	public Matrix4x4 ProjectionFor(float aspect, float near, float far)
	{
		float num = ((lens == null) ? (1f / Mathf.Tan(0.44505894f)) : ((lens.aspect <= 1f) ? lens.x : lens.y));
		float num2 = ((aspect <= 1f) ? num : (num / aspect));
		float num3 = ((aspect <= 1f) ? (num * aspect) : num);
		Matrix4x4 result = Matrix4x4.Perspective(51f, aspect, near, far);
		result.m00 = num2;
		result.m11 = num3;
		if (lens != null)
		{
			if (Mathf.Abs(aspect - lens.aspect) < 0.0001f)
			{
				result.m00 = lens.x;
				result.m11 = lens.y;
				result.m02 = lens.cx;
				result.m12 = lens.cy;
			}
			else if (aspect <= 1f == lens.aspect <= 1f)
			{
				result.m02 = lens.cx * num2 / lens.x;
				result.m12 = lens.cy * num3 / lens.y;
			}
		}
		return result;
	}

	private void RefreshProjection()
	{
		if (Active)
		{
			projectionWidth = Screen.width;
			projectionHeight = Screen.height;
			game.GameCamera.projectionMatrix = ProjectionFor(game.GameCamera.aspect, game.GameCamera.nearClipPlane, game.GameCamera.farClipPlane);
			game.CandyCamera.projectionMatrix = ProjectionFor(game.CandyCamera.aspect, game.CandyCamera.nearClipPlane, game.CandyCamera.farClipPlane);
		}
	}

	private void LateUpdate()
	{
		if (Active && (Screen.width != projectionWidth || Screen.height != projectionHeight))
		{
			RefreshProjection();
		}
	}

        public void Initialize(CandyFlickGame owner)
        {
            game = owner; originalPlayerPosition = game.Player.position; originalPlayerRotation = game.Player.rotation;
            floor=game.World.Find("Floor");originalFloorPosition=floor.localPosition;
            Navigation = new NonARViewControls(owner);
            try{lens=JsonUtility.FromJson<Lens>(PlayerPrefs.GetString(LensPreference,""));}catch(ArgumentException){lens=null;}
            if(lens!=null&&(!float.IsFinite(lens.aspect)||lens.aspect<=0||!float.IsFinite(lens.x)||lens.x<=0||!float.IsFinite(lens.y)||lens.y<=0||!float.IsFinite(lens.cx)||!float.IsFinite(lens.cy)))lens=null;
        }
        public void Begin()
        {
            if (!Room)
            {
                Room = Instantiate(Resources.Load<GameObject>("NonARMode/Core"), game.World);
                Room.name = "Non AR room";
                Room.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                Room.transform.localScale = Vector3.one;
                Marker = Room.transform.Find("Plane");
                float markerWidth = Marker.GetComponent<MeshFilter>().sharedMesh.bounds.size.x * Marker.lossyScale.x;
                Room.transform.localScale = Vector3.one * (200f / markerWidth);
                Room.transform.position -= Marker.position; // Marker surface becomes the native y=0 table plane.
                foreach (var child in Room.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = StageLayer;
                // The room supplies visuals; the recovered floor/actor own candy contacts.
                foreach (var collider in Room.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
                RoomLight("Warm room light", new Vector3(35,160,0), new Color(1,.95f,.88f), 1f);
                RoomLight("Soft room fill", new Vector3(25,-30,0), new Color(1,.98f,.93f), 1f);
            }
            Room.SetActive(true);
            floor.localPosition=originalFloorPosition+Vector3.up*GroundClearance;
            OverrideSceneLighting();
            game.Player.SetPositionAndRotation(new Vector3(0,500,900), Quaternion.LookRotation(new Vector3(0,70,0)-new Vector3(0,500,900)));
            game.GameCamera.ResetProjectionMatrix(); game.CandyCamera.ResetProjectionMatrix();
            game.GameCamera.cullingMask |= 1 << StageLayer;
            RefreshProjection();
            Navigation.Begin(Room.transform.Find("Cube").GetComponent<Renderer>().bounds);
        }
        public void End()
        {
            Navigation?.Cancel();
            RestoreSceneLighting();
            if (!Active) return;
            Room.SetActive(false);
            floor.localPosition=originalFloorPosition;
            game.Player.SetPositionAndRotation(originalPlayerPosition, originalPlayerRotation);
            game.GameCamera.ResetProjectionMatrix(); game.CandyCamera.ResetProjectionMatrix();
        }
        void RoomLight(string name, Vector3 angles, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(Room.transform, false); light.transform.rotation = Quaternion.Euler(angles);
            light.type = LightType.Directional; light.color = color; light.intensity = intensity;
            light.renderMode = LightRenderMode.ForcePixel;
            light.cullingMask = 1 << StageLayer; light.shadows = LightShadows.None;
        }
        void OverrideSceneLighting()
        {
            if (lightingOverridden) return;
            previousAmbientMode = RenderSettings.ambientMode;
            previousSky = RenderSettings.ambientSkyColor; previousEquator = RenderSettings.ambientEquatorColor;
            previousGround = RenderSettings.ambientGroundColor;
            previousAmbientIntensity = RenderSettings.ambientIntensity; previousReflectionIntensity = RenderSettings.reflectionIntensity;
            previousAmbientProbe = RenderSettings.ambientProbe;
            foreach (var light in FindObjectsByType<Light>(FindObjectsInactive.Include))
            {
                if (light.transform.IsChildOf(Room.transform)) continue;
                sceneLights.Add(light, light.enabled); light.enabled = false;
            }
            Color ambient = new Color(.24f,.222f,.192f);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = ambient;
            RenderSettings.ambientIntensity = 1; RenderSettings.reflectionIntensity = 0;
            var probe = new SphericalHarmonicsL2(); probe.AddAmbientLight(ambient);
            RenderSettings.ambientProbe = probe; // Replace the existing sky probe immediately.
            lightingOverridden = true;
        }
        void RestoreSceneLighting()
        {
            if (!lightingOverridden) return;
            foreach (var entry in sceneLights) if (entry.Key) entry.Key.enabled = entry.Value;
            sceneLights.Clear();
            RenderSettings.ambientMode = previousAmbientMode;
            RenderSettings.ambientSkyColor = previousSky; RenderSettings.ambientEquatorColor = previousEquator;
            RenderSettings.ambientGroundColor = previousGround;
            RenderSettings.ambientIntensity = previousAmbientIntensity; RenderSettings.reflectionIntensity = previousReflectionIntensity;
            RenderSettings.ambientProbe = previousAmbientProbe;
            lightingOverridden = false;
        }
        void OnDisable() => RestoreSceneLighting();
    }
}
