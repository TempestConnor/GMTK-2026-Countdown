using UnityEngine;

// One-shot anime "eye glint" burst played when a banish fires: a soft halo, a four-point star and a
// long horizontal lens streak, all built from two procedural textures so it needs no art assets.
// Each Play() spawns its own short-lived object, which destroys itself when the animation ends.
public class BanishFlash : MonoBehaviour
{
    [System.Serializable]
    public struct Settings
    {
        [Tooltip("Main tint of the halo, streaks and star.")]
        public Color color;
        [Tooltip("Tint of the hot center; white reads as the brightest point.")]
        public Color coreColor;
        [Tooltip("World-unit width of the long horizontal lens streak at full size.")]
        public float streakLength;
        [Tooltip("World-unit size of the four-point star and halo at full size.")]
        public float starSize;
        [Tooltip("Total lifetime in seconds (unscaled, so the aim slow-mo never stretches it).")]
        public float duration;
        public string sortingLayer;
        public int sortingOrder;

        public static Settings Default => new Settings
        {
            color = new Color(1f, 0.32f, 0.08f, 1f),
            coreColor = new Color(1f, 0.95f, 0.85f, 1f),
            streakLength = 48f,
            starSize = 9f,
            duration = 0.55f,
            sortingLayer = "Player",
            sortingOrder = 100,
        };
    }

    private const int TextureSize = 128;
    private const float RiseFraction = 0.08f;
    private static Sprite glowSprite;
    private static Sprite streakSprite;
    private static Material unlitMaterial;

    private struct Layer
    {
        public SpriteRenderer renderer;
        public Vector2 startScale;
        public Vector2 endScale;
        public float alpha;
    }

    private Layer[] layers;
    private Transform star;
    private float elapsed;
    private float duration;

    public static void Play(Vector3 position, Settings settings)
    {
        var go = new GameObject("BanishFlash");
        go.transform.position = position;
        go.AddComponent<BanishFlash>().Build(settings);
    }

    private void Build(Settings s)
    {
        duration = Mathf.Max(0.05f, s.duration);
        star = new GameObject("Star").transform;
        star.SetParent(transform, false);
        star.localRotation = Quaternion.Euler(0f, 0f, -12f);

        Color hot = Color.Lerp(s.color, s.coreColor, 0.45f);
        float st = s.starSize;
        layers = new[]
        {
            // Wide soft halo that bleeds the color over the whole area.
            MakeLayer(transform, "Halo", GlowSprite, s.color, 0.55f, new Vector2(st * 0.6f, st * 0.6f), new Vector2(st * 1.4f, st * 1.4f), s, 0),
            // Long lens streak across the screen, plus a thinner hot line inside it.
            MakeLayer(transform, "Streak", StreakSprite, s.color, 0.9f, new Vector2(s.streakLength * 0.35f, st * 0.09f), new Vector2(s.streakLength, st * 0.035f), s, 1),
            MakeLayer(transform, "StreakCore", StreakSprite, hot, 1f, new Vector2(s.streakLength * 0.25f, st * 0.035f), new Vector2(s.streakLength * 0.8f, st * 0.012f), s, 2),
            // Four-point star: long horizontal arm, shorter vertical, faint diagonals.
            MakeLayer(star, "StarH", StreakSprite, hot, 1f, new Vector2(st * 1.6f, st * 0.16f), new Vector2(st * 1.1f, st * 0.07f), s, 3),
            MakeLayer(star, "StarV", StreakSprite, hot, 1f, new Vector2(st * 0.16f, st * 1.1f), new Vector2(st * 0.07f, st * 0.7f), s, 3),
            MakeLayer(star, "DiagA", StreakSprite, s.color, 0.6f, new Vector2(st * 0.45f, st * 0.05f), new Vector2(st * 0.3f, st * 0.02f), s, 3, 45f),
            MakeLayer(star, "DiagB", StreakSprite, s.color, 0.6f, new Vector2(st * 0.45f, st * 0.05f), new Vector2(st * 0.3f, st * 0.02f), s, 3, -45f),
            // Blinding center.
            MakeLayer(transform, "Core", GlowSprite, s.coreColor, 1f, new Vector2(st * 0.3f, st * 0.3f), new Vector2(st * 0.12f, st * 0.12f), s, 4),
        };
        Apply(0f);
    }

    private static Layer MakeLayer(Transform parent, string name, Sprite sprite, Color color, float alpha,
        Vector2 startScale, Vector2 endScale, Settings s, int orderOffset, float rotation = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(0f, 0f, rotation);
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = color;
        if (UnlitMaterial != null) sr.sharedMaterial = UnlitMaterial;
        sr.sortingLayerName = s.sortingLayer;
        sr.sortingOrder = s.sortingOrder + orderOffset;
        return new Layer { renderer = sr, startScale = startScale, endScale = endScale, alpha = alpha };
    }

    private void Update()
    {
        elapsed += Time.unscaledDeltaTime;
        if (elapsed >= duration)
        {
            Destroy(gameObject);
            return;
        }
        Apply(elapsed / duration);
    }

    private void Apply(float t)
    {
        // Snap on almost instantly, then decay with a long tail.
        float intensity = t < RiseFraction ? t / RiseFraction : Mathf.Pow(1f - (t - RiseFraction) / (1f - RiseFraction), 2f);
        float grow = 1f - Mathf.Pow(1f - Mathf.Clamp01(t / 0.35f), 3f);

        foreach (var layer in layers)
        {
            Vector2 scale = Vector2.LerpUnclamped(layer.startScale, layer.endScale, grow);
            layer.renderer.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            var c = layer.renderer.color;
            c.a = layer.alpha * intensity;
            layer.renderer.color = c;
        }
        star.localRotation = Quaternion.Euler(0f, 0f, Mathf.Lerp(-12f, 8f, grow));
    }

    private static Material UnlitMaterial
    {
        get
        {
            if (unlitMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
                if (shader != null) unlitMaterial = new Material(shader) { name = "BanishFlashUnlit" };
            }
            return unlitMaterial;
        }
    }

    // Radial falloff: bright center fading smoothly to transparent at the edge.
    private static Sprite GlowSprite => glowSprite != null ? glowSprite : (glowSprite = MakeSprite((x, y) =>
    {
        float r = Mathf.Clamp01(Mathf.Sqrt(x * x + y * y));
        return Mathf.Pow(1f - r, 2.5f);
    }));

    // Horizontal line that tapers to points at both ends and fades sharply off its center line.
    private static Sprite StreakSprite => streakSprite != null ? streakSprite : (streakSprite = MakeSprite((x, y) =>
    {
        float along = Mathf.Pow(1f - Mathf.Abs(x), 1.6f);
        float across = Mathf.Exp(-y * y * 9f);
        return along * across * Mathf.Clamp01(1f - Mathf.Abs(y));
    }));

    private static Sprite MakeSprite(System.Func<float, float, float> alpha)
    {
        var tex = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear,
            hideFlags = HideFlags.DontSave,
        };
        var pixels = new Color32[TextureSize * TextureSize];
        for (int py = 0; py < TextureSize; py++)
        for (int px = 0; px < TextureSize; px++)
        {
            float x = (px + 0.5f) / TextureSize * 2f - 1f;
            float y = (py + 0.5f) / TextureSize * 2f - 1f;
            pixels[py * TextureSize + px] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha(x, y)));
        }
        tex.SetPixels32(pixels);
        tex.Apply(false, true);
        // Pixels-per-unit equal to the texture size makes the sprite 1x1 world unit, so scale = size.
        var sprite = Sprite.Create(tex, new Rect(0, 0, TextureSize, TextureSize), new Vector2(0.5f, 0.5f), TextureSize);
        sprite.hideFlags = HideFlags.DontSave;
        return sprite;
    }
}
