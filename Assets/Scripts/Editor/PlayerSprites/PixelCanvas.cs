using System;

// Deliberately free of UnityEngine so the player art can be rendered and previewed outside the editor.

/// <summary>An 8-bit RGBA color.</summary>
public readonly struct Px
{
    public readonly byte r, g, b, a;
    public Px(byte r, byte g, byte b, byte a = 255) { this.r = r; this.g = g; this.b = b; this.a = a; }
    public bool IsClear => a == 0;
    public static readonly Px Clear = default;
}

/// <summary>A 2D point/vector in (design) pixels, y up.</summary>
public readonly struct V2
{
    public readonly float x, y;
    public V2(float x, float y) { this.x = x; this.y = y; }
    public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
    public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
    public static V2 operator *(V2 a, float s) => new V2(a.x * s, a.y * s);
    public float Length => MathF.Sqrt(x * x + y * y);
}

/// <summary>
/// A hard-edged pixel canvas, y up. Shapes cover the pixels whose centers fall inside them, so
/// nothing is anti-aliased and every frame stays crisp pixel art at any resolution.
/// </summary>
public sealed class PixelCanvas
{
    public readonly int Width, Height;
    private readonly Px[] pixels;

    public PixelCanvas(int width, int height)
    {
        Width = width;
        Height = height;
        pixels = new Px[width * height];
    }

    public Px Get(int x, int y) => x < 0 || y < 0 || x >= Width || y >= Height ? Px.Clear : pixels[y * Width + x];

    public void Set(int x, int y, Px c)
    {
        if (x >= 0 && y >= 0 && x < Width && y < Height) pixels[y * Width + x] = c;
    }

    /// <summary>Recolors only pixels that are already painted (used for shading inside a silhouette).</summary>
    public void Tint(int x, int y, Px c)
    {
        if (!Get(x, y).IsClear) Set(x, y, c);
    }

    public void FillRect(Px c, float x0, float y0, float x1, float y1)
    {
        for (int y = (int)MathF.Ceiling(y0 - .5f); y + .5f < y1; y++)
        for (int x = (int)MathF.Ceiling(x0 - .5f); x + .5f < x1; x++)
            Set(x, y, c);
    }

    /// <summary>Every pixel whose center is within <paramref name="radius"/> of the segment a-b.</summary>
    public void Capsule(Px c, V2 a, V2 b, float radius)
    {
        int x0 = (int)MathF.Floor(MathF.Min(a.x, b.x) - radius), x1 = (int)MathF.Ceiling(MathF.Max(a.x, b.x) + radius);
        int y0 = (int)MathF.Floor(MathF.Min(a.y, b.y) - radius), y1 = (int)MathF.Ceiling(MathF.Max(a.y, b.y) + radius);
        V2 ab = b - a;
        float len2 = ab.x * ab.x + ab.y * ab.y;
        for (int y = y0; y <= y1; y++)
        for (int x = x0; x <= x1; x++)
        {
            var p = new V2(x + .5f, y + .5f);
            float t = len2 > 0 ? Math.Clamp(((p.x - a.x) * ab.x + (p.y - a.y) * ab.y) / len2, 0f, 1f) : 0f;
            if ((p - (a + ab * t)).Length <= radius) Set(x, y, c);
        }
    }

    public void Disc(Px c, V2 center, float radius) => Capsule(c, center, center, radius);

    /// <summary>Even-odd scanline fill over pixel centers.</summary>
    public void FillPolygon(Px c, params V2[] pts)
    {
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var p in pts) { minY = MathF.Min(minY, p.y); maxY = MathF.Max(maxY, p.y); }
        var xs = new float[pts.Length];
        for (int y = (int)MathF.Floor(minY); y <= (int)MathF.Ceiling(maxY); y++)
        {
            float cy = y + .5f;
            int n = 0;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                V2 p = pts[i], q = pts[j];
                if ((p.y > cy) == (q.y > cy)) continue;
                xs[n++] = p.x + (cy - p.y) / (q.y - p.y) * (q.x - p.x);
            }
            Array.Sort(xs, 0, n);
            for (int k = 0; k + 1 < n; k += 2)
                for (int x = (int)MathF.Ceiling(xs[k] - .5f); x + .5f < xs[k + 1]; x++)
                    Set(x, y, c);
        }
    }

    /// <summary>Rings the painted silhouette with <paramref name="c"/> (4-connected, one pixel thick).</summary>
    public void Outline(Px c)
    {
        var ring = new bool[pixels.Length];
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            ring[y * Width + x] = Get(x, y).IsClear &&
                (!Get(x - 1, y).IsClear || !Get(x + 1, y).IsClear || !Get(x, y - 1).IsClear || !Get(x, y + 1).IsClear);
        for (int i = 0; i < ring.Length; i++)
            if (ring[i]) pixels[i] = c;
    }

    /// <summary>Copies this canvas into <paramref name="dest"/> with its bottom-left at (dx, dy).</summary>
    public void CopyTo(PixelCanvas dest, int dx, int dy)
    {
        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
            dest.Set(dx + x, dy + y, pixels[y * Width + x]);
    }
}
