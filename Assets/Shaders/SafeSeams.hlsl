#ifndef SAFE_SEAMS_INCLUDED
#define SAFE_SEAMS_INCLUDED

// Open seams between a SafeBox and the safe region it touches, uploaded by SafeSeams.cs. Include after
// Lit2DCommon.hlsl (needs _MainTex). Renderers opt in through _SafeSeamRole / _SafeSeamPlane, set from a
// MaterialPropertyBlock at runtime; everything else (role 0) is untouched, and so is edit mode.
#define SAFE_SEAM_MAX 32
float4 _SafeSeamRects[SAFE_SEAM_MAX]; // World-space min (xy) and max (zw).
float4 _SafeSeamInfo[SAFE_SEAM_MAX];  // x: plane (0 = A, 1 = B), y: role the rect applies to.
float _SafeSeamCount;
half4 _SafeSeamTileInterior;

#define SAFE_SEAM_ROLE_BOX 1
#define SAFE_SEAM_ROLE_TERRAIN 2

bool InSafeSeam(float2 positionWS, half role, half plane)
{
    if (role < 0.5) return false;
    int count = min((int)_SafeSeamCount, SAFE_SEAM_MAX);
    for (int i = 0; i < count; i++)
    {
        float4 r = _SafeSeamRects[i];
        if (abs(_SafeSeamInfo[i].x - plane) < 0.5 && abs(_SafeSeamInfo[i].y - role) < 0.5 &&
            all(positionWS >= r.xy) && all(positionWS < r.zw))
            return true;
    }
    return false;
}

// Returns the vertex color to pass into CommonLitFragment. Box frames vanish (their fill shows through);
// terrain is redrawn as plain tile interior, keeping 2D lighting.
half4 ApplySafeSeam(half4 color, float2 uv, float2 positionWS, half role, half plane)
{
    if (!InSafeSeam(positionWS, role, plane)) return color;
    if (role < 1.5) return half4(color.rgb, 0);
    half4 texel = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
    // Keep the renderer's own tint, so the patch matches the untouched interior around it.
    return color * _SafeSeamTileInterior / max(texel, half4(0.001, 0.001, 0.001, 0.001));
}

#endif
