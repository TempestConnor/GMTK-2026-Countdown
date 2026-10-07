using System;
using System.Collections;
using UnityEngine;

/// <summary>A transient overlay that survives the room scene being replaced.</summary>
public sealed class RoomFade : MonoBehaviour
{
    public const float FadeOutDuration = 0.15f;
    public const float FadeInDuration = 0.15f;
    private UnityEngine.UI.Image curtain;

    public static RoomFade Create()
    {
        var root = new GameObject("Room Fade", typeof(RectTransform), typeof(Canvas));
        DontDestroyOnLoad(root);
        var canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;
        var panel = new GameObject("Curtain", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        panel.transform.SetParent(root.transform, false);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        var fade = root.AddComponent<RoomFade>();
        fade.curtain = panel.GetComponent<UnityEngine.UI.Image>();
        fade.curtain.color = Color.clear;
        fade.curtain.raycastTarget = false;
        return fade;
    }

    public void FadeOut(Action completed) => StartCoroutine(Animate(1f, FadeOutDuration, completed));
    public void FadeIn(Action completed) => StartCoroutine(Animate(0f, FadeInDuration, completed));

    private IEnumerator Animate(float target, float duration, Action completed)
    {
        float initial = curtain.color.a;
        for (float elapsed = 0; elapsed < duration; elapsed += Time.unscaledDeltaTime)
        {
            curtain.color = new Color(0, 0, 0, Mathf.Lerp(initial, target, elapsed / duration));
            yield return null;
        }
        curtain.color = new Color(0, 0, 0, target);
        // Let the fully black overlay render before scene loading can stall a frame.
        yield return null;
        completed?.Invoke();
    }
}
