#ifndef BANISH_OUTLINE_INCLUDED
#define BANISH_OUTLINE_INCLUDED

// Inner sprite outline shown on Banishables while the banish reticule is up. Include after
// Lit2DCommon.hlsl / 2DCommon.hlsl (needs _MainTex). Banishable.cs drives _OutlineAmount and
// _OutlineColor per renderer from a MaterialPropertyBlock; at 0 (the default) this is a no-op.
float4 _MainTex_TexelSize;

#define BANISH_OUTLINE_PULSE_SPEED 8.0
// How far past the solid rim the soft inner glow reaches, as a multiple of _OutlineWidth.
#define BANISH_OUTLINE_GLOW_REACH 3.0
#define BANISH_OUTLINE_GLOW_STRENGTH 0.55

// Sprite meshes are trimmed to the sprite's rect, so the outline is drawn inward: an opaque texel
// is edge if anything within the given distance is transparent or falls off the sprite.
// Sampled at LOD 0: called from an unrolled loop, and sprites here have no mips anyway.
half BanishOutlineAlpha(float2 uv)
{
    if (any(uv < 0) || any(uv > 1)) return 0;
    return SAMPLE_TEXTURE2D_LOD(_MainTex, sampler_MainTex, uv, 0).a;
}

half BanishOutlineEdge(float2 uv, float2 o)
{
    half edge = 0;
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2( o.x, 0)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2(-o.x, 0)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2(0,  o.y)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2(0, -o.y)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2( o.x,  o.y)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2(-o.x,  o.y)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2( o.x, -o.y)));
    edge = max(edge, 1 - BanishOutlineAlpha(uv + float2(-o.x, -o.y)));
    return step(0.5, edge);
}

// 1 on the solid rim, fading to 0 across the inner glow band.
half BanishOutlineMask(float2 uv, half width)
{
    if (BanishOutlineAlpha(uv) < 0.5) return 0;

    float2 texel = _MainTex_TexelSize.xy * width;
    if (BanishOutlineEdge(uv, texel) > 0) return 1;

    // A few rings outward from the rim give a stepped falloff that suits the pixel-art sprites.
    const int rings = 3;
    [unroll] for (int i = 1; i <= rings; i++)
    {
        float reach = 1 + (BANISH_OUTLINE_GLOW_REACH - 1) * i / rings;
        if (BanishOutlineEdge(uv, texel * reach) > 0)
            return BANISH_OUTLINE_GLOW_STRENGTH * (1 - (i - 1) / (half)rings);
    }
    return 0;
}

// Unlit on purpose so the glow reads the same regardless of 2D lighting. The pulse brightens toward
// a hotter yellow rather than white (white washes it out in linear space) and never dims;
// _Time follows Time.timeScale, so it slows with the aim slow-mo.
half3 ApplyBanishOutline(half3 color, float2 uv, half amount, half width, half4 outlineColor)
{
    if (amount < 0.001) return color;

    half mask = BanishOutlineMask(uv, width) * amount * outlineColor.a;
    half pulse = 0.5 + 0.5 * sin(_Time.y * BANISH_OUTLINE_PULSE_SPEED);
    half3 glow = lerp(outlineColor.rgb, half3(1, 1, 0.08), pulse * 0.6);
    return lerp(color, glow, mask);
}

#endif
