using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.U2D.Sprites;
using UnityEngine;

/// <summary>
/// Draws the player's sprite sheet from <see cref="PlayerSpriteArt"/> at the Player prefab's hitbox
/// size, slices it, writes one AnimationClip per <see cref="PlayerAnimState"/>, and rebuilds the
/// Animator's base layer as one Any State transition per state (driven by the "state" int that
/// playerController2 sets). Changing the prefab's CapsuleCollider2D size or offset regenerates
/// everything automatically, so the art is redrawn crisply instead of stretched.
/// </summary>
public static class PlayerSpriteGenerator
{
    private const string MenuPath = "Tools/Player/Generate Player Sprites";
    public const string PrefabPath = "Assets/Prefabs/Entities/Player.prefab";
    private const string SheetPath = "Assets/Tiles/Sprites/PlayerSheet.png";
    private const string ClipFolder = "Assets/Animations/Player";
    private const string ControllerPath = "Assets/Animations/PlayerAnimatorController.controller";
    public const string StateParameter = "state";
    // Same density as the Box sprite; the hitbox's world size times this is the drawn body size.
    private const int PixelsPerUnit = 32;
    // Bump after changing PlayerSpriteArt so every checkout regenerates on its next script reload.
    private const string StyleVersion = "5";
    private const int Columns = 10;

    private static readonly EditorCurveBinding SpriteBinding =
        EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");

    [MenuItem(MenuPath)]
    public static void Generate()
    {
        if (!TryReadHitbox(out var size, out var offset))
        {
            Debug.LogError($"Player sprites: no CapsuleCollider2D on {PrefabPath}.");
            return;
        }
        int width = Mathf.Max(8, Mathf.RoundToInt(size.x * PixelsPerUnit));
        int height = Mathf.Max(16, Mathf.RoundToInt(size.y * PixelsPerUnit));
        var layout = new PlayerSpriteArt.Layout(width, height);
        var clips = PlayerSpriteArt.Clips();

        var names = new List<string>();
        foreach (var clip in clips)
            for (int i = 0; i < clip.frames.Length; i++)
                names.Add($"Player_{clip.state}_{i}");
        WriteSheet(clips, layout);
        // The sprite pivot sits on the transform origin: the hitbox center minus the collider offset.
        var pivot = new Vector2(
            (layout.originX - offset.x * PixelsPerUnit) / layout.frameWidth,
            (layout.originY + height / 2f - offset.y * PixelsPerUnit) / layout.frameHeight);
        var sprites = ImportSheet(names, layout, pivot);

        var built = clips.ToDictionary(c => c.state, c => WriteClip(c, sprites));
        BuildController(built);
        AssignDefaultSprite(sprites[names[0]]);

        var importer = AssetImporter.GetAtPath(SheetPath);
        importer.userData = Signature(size, offset);
        importer.SaveAndReimport();
        AssetDatabase.SaveAssets();
        Debug.Log($"Player sprites: {names.Count} frames at {width}x{height} px body ({layout.frameWidth}x{layout.frameHeight} px frames), {clips.Count} clips.");
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateGenerate() => CanGenerate();

    private static bool CanGenerate() => !EditorApplication.isPlayingOrWillChangePlaymode && !EditorApplication.isCompiling;

    private static bool pending;

    /// <summary>Regenerates on the next editor tick if the style or the prefab's hitbox changed.</summary>
    public static void RegenerateIfOutdated()
    {
        if (pending) return;
        pending = true;
        EditorApplication.delayCall += () =>
        {
            pending = false;
            if (!CanGenerate() || !TryReadHitbox(out var size, out var offset)) return;
            if (AssetImporter.GetAtPath(SheetPath)?.userData != Signature(size, offset)) Generate();
        };
    }

    [InitializeOnLoadMethod]
    private static void GenerateIfOutdated() => RegenerateIfOutdated();

    private static string Signature(Vector2 size, Vector2 offset) =>
        $"{StyleVersion}|{size.x:R}x{size.y:R}|{offset.x:R},{offset.y:R}|{PixelsPerUnit}";

    private static bool TryReadHitbox(out Vector2 size, out Vector2 offset)
    {
        var capsule = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath)?.GetComponent<CapsuleCollider2D>();
        size = capsule != null ? capsule.size : default;
        offset = capsule != null ? capsule.offset : default;
        return capsule != null;
    }

    // ---------------------------------------------------------------- Sheet

    // Frames sit in a grid, one transparent pixel around each so neighbors never bleed together.
    private static int SlotWidth(PlayerSpriteArt.Layout l) => l.frameWidth + 2;
    private static int SlotHeight(PlayerSpriteArt.Layout l) => l.frameHeight + 2;

    private static Vector2Int SlotOrigin(int index, int rows, PlayerSpriteArt.Layout l) =>
        new(index % Columns * SlotWidth(l) + 1, (rows - 1 - index / Columns) * SlotHeight(l) + 1);

    private static void WriteSheet(IReadOnlyList<PlayerSpriteArt.Clip> clips, PlayerSpriteArt.Layout layout)
    {
        var frames = clips.SelectMany(c => c.frames).ToList();
        int rows = (frames.Count + Columns - 1) / Columns;
        var sheet = new PixelCanvas(Columns * SlotWidth(layout), rows * SlotHeight(layout));
        for (int i = 0; i < frames.Count; i++)
        {
            var origin = SlotOrigin(i, rows, layout);
            PlayerSpriteArt.Render(frames[i], layout).CopyTo(sheet, origin.x, origin.y);
        }

        var colors = new Color32[sheet.Width * sheet.Height];
        for (int y = 0; y < sheet.Height; y++)
        for (int x = 0; x < sheet.Width; x++)
        {
            var p = sheet.Get(x, y);
            colors[y * sheet.Width + x] = new Color32(p.r, p.g, p.b, p.a);
        }
        var texture = new Texture2D(sheet.Width, sheet.Height, TextureFormat.RGBA32, false);
        texture.SetPixels32(colors);
        File.WriteAllBytes(SheetPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(SheetPath, ImportAssetOptions.ForceSynchronousImport);
    }

    private static Dictionary<string, Sprite> ImportSheet(List<string> names, PlayerSpriteArt.Layout layout, Vector2 pivot)
    {
        var importer = (TextureImporter)AssetImporter.GetAtPath(SheetPath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.spritePixelsPerUnit = PixelsPerUnit;
        importer.filterMode = FilterMode.Point;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.wrapMode = TextureWrapMode.Clamp;

        var factory = new SpriteDataProviderFactories();
        factory.Init();
        var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
        provider.InitSpriteEditorDataProvider();
        // Reuse existing sprite IDs so regenerating keeps clip and prefab references stable.
        var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);
        int rows = (names.Count + Columns - 1) / Columns;
        var rects = names.Select((name, i) =>
        {
            var origin = SlotOrigin(i, rows, layout);
            return new SpriteRect
            {
                name = name,
                rect = new Rect(origin.x, origin.y, layout.frameWidth, layout.frameHeight),
                alignment = SpriteAlignment.Custom,
                pivot = pivot,
                spriteID = existing.TryGetValue(name, out var id) ? id : GUID.Generate(),
            };
        }).ToArray();
        provider.SetSpriteRects(rects);
        provider.GetDataProvider<ISpriteNameFileIdDataProvider>()
            .SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)));
        provider.Apply();
        importer.SaveAndReimport();

        return AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToDictionary(s => s.name);
    }

    // ---------------------------------------------------------------- Animation

    private static AnimationClip WriteClip(PlayerSpriteArt.Clip source, Dictionary<string, Sprite> sprites)
    {
        if (!AssetDatabase.IsValidFolder(ClipFolder)) AssetDatabase.CreateFolder("Assets/Animations", "Player");
        string path = $"{ClipFolder}/Player_{source.state}.anim";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.frameRate = source.fps;
        // Unity gives the last key a full frame of its own, and one-shot clips hold on it.
        var keys = source.frames.Select((_, i) => new ObjectReferenceKeyframe
        {
            time = i / source.fps,
            value = sprites[$"Player_{source.state}_{i}"],
        }).ToArray();
        AnimationUtility.SetObjectReferenceCurve(clip, SpriteBinding, keys);
        var settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = source.loop;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
        return clip;
    }

    // Every state is reachable from Any State on "state == value": the selection rules live in
    // playerController2.Animation.cs, so the graph never needs hand-maintained transitions.
    private static void BuildController(Dictionary<PlayerAnimState, AnimationClip> clips)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                         ?? AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        if (controller.parameters.All(p => p.name != StateParameter))
            controller.AddParameter(StateParameter, AnimatorControllerParameterType.Int);

        var machine = controller.layers[0].stateMachine;
        foreach (var t in machine.anyStateTransitions) machine.RemoveAnyStateTransition(t);
        foreach (var s in machine.states) machine.RemoveState(s.state);

        int index = 0;
        foreach (var (state, clip) in clips)
        {
            var node = machine.AddState(state.ToString(), new Vector3(300 + index % 3 * 230, index / 3 * 60));
            node.motion = clip;
            node.writeDefaultValues = false;
            var transition = machine.AddAnyStateTransition(node);
            transition.AddCondition(AnimatorConditionMode.Equals, (int)state, StateParameter);
            transition.hasExitTime = false;
            transition.duration = 0;
            transition.canTransitionToSelf = false;
            if (state == PlayerAnimState.Idle) machine.defaultState = node;
            index++;
        }
        EditorUtility.SetDirty(controller);
    }

    // The prefab shows the first idle frame in the editor, untinted (the sheet carries its own colors).
    private static void AssignDefaultSprite(Sprite sprite)
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var renderer = root.GetComponent<SpriteRenderer>();
            if (renderer == null || (renderer.sprite == sprite && renderer.color == Color.white)) return;
            renderer.sprite = sprite;
            renderer.color = Color.white;
            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }
}

/// <summary>Redraws the player sprites when the Player prefab (and so possibly its hitbox) is saved.</summary>
internal sealed class PlayerHitboxWatcher : AssetPostprocessor
{
    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (imported.Contains(PlayerSpriteGenerator.PrefabPath)) PlayerSpriteGenerator.RegenerateIfOutdated();
    }
}
