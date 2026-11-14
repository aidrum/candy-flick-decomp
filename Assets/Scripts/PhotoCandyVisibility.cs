using System.Collections.Generic;
using UnityEngine;

namespace CandyFlick
{
    // Suppress only rendering, around each camera render. Physics, readiness,
    // tracking visibility and inactive launcher objects keep their current state.
    public sealed class PhotoCandyVisibility : MonoBehaviour
    {
        CandyFlickGame game;
        Transform[] roots;
        readonly List<Renderer> renderers = new List<Renderer>();
        readonly Dictionary<Renderer, bool> previous = new Dictionary<Renderer, bool>();
        public bool IsHiding { get; private set; }
        public void Initialize(CandyFlickGame owner, params Transform[] hiddenRoots)
        {
            game = owner; roots = hiddenRoots;
        }
        public void Begin(bool hide) { End(); IsHiding = hide; }
        public void End() { IsHiding = false; Restore(); }
        void OnEnable() { Camera.onPreCull += BeforeRender; Camera.onPostRender += AfterRender; }
        void OnDisable() { Camera.onPreCull -= BeforeRender; Camera.onPostRender -= AfterRender; End(); }
        void BeforeRender(Camera camera)
        {
            if (!IsHiding || !game || (camera != game.GameCamera && camera != game.CandyCamera)) return;
            foreach (var root in roots)
            {
                if (!root) continue;
                // Legacy candy effects create their renderers lazily on first use.
                root.GetComponentsInChildren(true, renderers);
                foreach (var renderer in renderers)
                {
                    if (!previous.ContainsKey(renderer)) previous.Add(renderer, renderer.forceRenderingOff);
                    renderer.forceRenderingOff = true;
                }
            }
        }
        void AfterRender(Camera camera)
        {
            if (game && (camera == game.GameCamera || camera == game.CandyCamera)) Restore();
        }
        void Restore()
        {
            foreach (var entry in previous) if (entry.Key) entry.Key.forceRenderingOff = entry.Value;
            previous.Clear();
        }
    }
}
