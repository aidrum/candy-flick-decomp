using UnityEngine;

namespace CandyFlick.Original26
{
    // Compatibility replacement for removed Unity 4 GUITexture. Field name is retained for animation.
    public sealed class LegacyGUITexture26 : MonoBehaviour
    {
        public Texture2D texture;
        public Color m_Color = new Color(.5f, .5f, .5f, .5f);
        public Rect pixelInset;
        public void Draw(float width, float height)
        {
            if (!texture || !isActiveAndEnabled) return;
            Vector3 position = transform.position, scale = transform.lossyScale;
            Rect rect = new Rect((position.x - scale.x * .5f) * width + pixelInset.x,
                (position.y - scale.y * .5f) * height + pixelInset.y,
                scale.x * width + pixelInset.width, scale.y * height + pixelInset.height);
            rect.y = height - rect.yMax;
            Color saved = GUI.color; GUI.color = m_Color * 2; // Original Hidden/Internal-GUITexture.
            GUI.DrawTexture(rect, texture); GUI.color = saved;
        }
    }
}
