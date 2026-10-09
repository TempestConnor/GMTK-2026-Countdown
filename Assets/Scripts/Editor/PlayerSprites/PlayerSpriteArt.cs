using System;
using System.Collections.Generic;
using System.Linq;

// Deliberately free of UnityEngine so the player art can be rendered and previewed outside the editor.

/// <summary>
/// Procedural pixel art for the player: a compact facility robot with a big head block, a wide
/// green visor and a short antenna, built from the same flat blue-grey metal and light top lips
/// as the terrain. States are told apart by whole-body shape (stretch, squash, lean, arms out)
/// and by the visor, not by small limb details. Every pose is authored in "design pixels" on a
/// reference body of <see cref="RefWidth"/> x <see cref="RefHeight"/> design pixels, stretched to
/// the real hitbox at 32 px per unit, origin at the hitbox's bottom-center, x toward the facing direction, y up.
/// Frames are drawn at the real hitbox's pixel size times <see cref="VisualScaleX"/>/
/// <see cref="VisualScaleY"/>, so resizing the hitbox redraws crisp art instead of stretching it.
/// Facing left and reversed gravity are handled at runtime by flipping the sprite.
/// </summary>
public static class PlayerSpriteArt
{
    public const float RefWidth = 26, RefHeight = 51;
    // Room around the hitbox for limbs, the antenna and effects that reach past it.
    private const float RefPadX = 7, RefPadY = 6;

    // Drawn body size relative to the hitbox (1 = exactly the hitbox). Raising it enlarges the art; the
    // feet stay on the hitbox floor and wall/box contacts are authored with Edge() so hands and
    // boots still land on the hitbox edges.
    public const float VisualScaleX = 1f, VisualScaleY = 1f;

    // Terrain metals: the head matches the floor plates, the body the blue wall panels, each with
    // a light lip on top. Green is reserved for the player (visor, antenna tip, chest light).
    private static readonly Px Outline = new Px(14, 16, 24);
    private static readonly Px HeadLip = new Px(222, 226, 232);
    private static readonly Px Head = new Px(172, 176, 186);
    private static readonly Px HeadShade = new Px(116, 120, 132);
    private static readonly Px BodyLip = new Px(110, 136, 186);
    private static readonly Px Body = new Px(64, 78, 112);
    private static readonly Px BodyShade = new Px(42, 52, 78);
    private static readonly Px Limb = new Px(132, 138, 152);
    private static readonly Px LimbShade = new Px(84, 90, 106);
    private static readonly Px FacePlate = new Px(24, 28, 40);
    private static readonly Px VisorHi = new Px(226, 255, 232);
    private static readonly Px Visor = new Px(96, 255, 128);
    private static readonly Px VisorDim = new Px(32, 150, 72);
    private static readonly Px SparkCore = new Px(236, 255, 240);
    private static readonly Px Spark = new Px(110, 255, 150);
    private static readonly Px Dust = new Px(196, 204, 216);

    private const float HipY = 17, ShoulderY = 28;
    private const float Thigh = 8, Shin = 7.5f, UpperArm = 6, Forearm = 6;

    public enum HandShape { Fist, Flat, Grip }
    public enum VisorMode { Normal, Blink, Scan, Flash }
    public enum SparkSize { None, Big, Small }

    public struct Arm
    {
        public V2 hand;          // Relative to the body offset.
        public HandShape shape;
        public bool inFront;     // Back arm drawn over the body (raised to the visor, flung forward).
        public Arm(float x, float y, HandShape shape = HandShape.Fist, bool inFront = false)
        {
            hand = new V2(x, y);
            this.shape = shape;
            this.inFront = inFront;
        }
    }

    public sealed class Pose
    {
        public V2 body;                      // Torso and head offset (bob, crouch).
        public float lean;                   // Forward shear per design pixel above the hips.
        public float squash;                 // + wider and shorter, - taller and thinner (about the feet).
        public V2 backFoot = new V2(-3, 0), frontFoot = new V2(3, 0); // Foot soles, absolute.
        public Arm backArm = new Arm(-6, 18), frontArm = new Arm(5, 18);
        public float antenna;                // Antenna tip sway, + forward.
        public VisorMode visor;
        public int scan;                     // Scan dot position (0-3) for VisorMode.Scan.
        public bool chestLight;
        public SparkSize spark;
        public V2[] dust = Array.Empty<V2>();

        public Pose With(Action<Pose> edit)
        {
            var copy = (Pose)MemberwiseClone();
            edit(copy);
            return copy;
        }
    }

    public sealed class Clip
    {
        public readonly PlayerAnimState state;
        public readonly float fps;
        public readonly bool loop;
        public readonly Pose[] frames;
        public Clip(PlayerAnimState state, float fps, bool loop, IEnumerable<Pose> frames)
        {
            this.state = state;
            this.fps = fps;
            this.loop = loop;
            this.frames = frames.ToArray();
        }
    }

    /// <summary>Pixel layout of one frame for a hitbox of the given pixel size.</summary>
    public readonly struct Layout
    {
        public readonly int frameWidth, frameHeight;
        public readonly float originX, originY, scaleX, scaleY; // Hitbox bottom-center inside the frame.
        public Layout(int hitboxWidth, int hitboxHeight)
        {
            scaleX = hitboxWidth * VisualScaleX / RefWidth;
            scaleY = hitboxHeight * VisualScaleY / RefHeight;
            // Padding covers the reference padding plus however far the enlarged body overhangs the hitbox.
            int padX = (int)MathF.Ceiling(RefPadX * scaleX + hitboxWidth * (VisualScaleX - 1) / 2);
            int padBottom = (int)MathF.Ceiling(RefPadY * scaleY);
            int padTop = padBottom + (int)MathF.Ceiling(hitboxHeight * (VisualScaleY - 1));
            frameWidth = hitboxWidth + 2 * padX;
            frameHeight = hitboxHeight + padBottom + padTop;
            originX = padX + hitboxWidth / 2f;
            originY = padBottom;
        }
    }

    // ---------------------------------------------------------------- Animations

    /// <summary>The snap clips' length in seconds; the runtime holds the snap pose this long.</summary>
    public const float SnapFps = 14;

    public static IReadOnlyList<Clip> Clips()
    {
        var clips = new List<Clip>
        {
            new Clip(PlayerAnimState.Idle, 6, true, Frames(8, Idle)),
            new Clip(PlayerAnimState.Walk, 12, true, Frames(8, Walk)),
            new Clip(PlayerAnimState.Rise, 8, true, Frames(2, Rise)),
            new Clip(PlayerAnimState.Fall, 8, true, Frames(2, Fall)),
            new Clip(PlayerAnimState.WallSlide, 8, true, Frames(2, WallSlide)),
            new Clip(PlayerAnimState.WallJump, 12, false, Frames(3, WallJump)),
            new Clip(PlayerAnimState.GrabIdle, 3, true, Frames(2, i => Grip(Idle(i * 4)).With(p => p.lean = .14f))),
            new Clip(PlayerAnimState.GrabPush, 8, true, Frames(6, i => Grip(Shuffle(i, 1, .22f)))),
            new Clip(PlayerAnimState.GrabPull, 8, true, Frames(6, i => Grip(Shuffle(i, -1, -.12f)))),
            new Clip(PlayerAnimState.GrabAir, 8, true, Frames(2, i => Grip(Air(i)))),
            new Clip(PlayerAnimState.AimIdle, 6, true, Frames(4, i => Aim(Idle(i), i))),
            new Clip(PlayerAnimState.AimWalk, 12, true, Frames(8, i => Aim(Walk(i), i / 2))),
            new Clip(PlayerAnimState.AimAir, 8, true, Frames(2, i => Aim(Air(i), i * 2))),
            new Clip(PlayerAnimState.AimSlide, 8, true, Frames(2, i => Aim(WallSlide(i), i * 2, backArm: true))),
            new Clip(PlayerAnimState.SnapIdle, SnapFps, false, Frames(SnapFrames, i => Snap(Idle(0), i))),
            new Clip(PlayerAnimState.SnapWalk, SnapFps, false, Frames(SnapFrames, i => Snap(Walk(i), i))),
            new Clip(PlayerAnimState.SnapAir, SnapFps, false, Frames(SnapFrames, i => Snap(Air(i % 2), i))),
            new Clip(PlayerAnimState.SnapSlide, SnapFps, false, Frames(SnapFrames, i => Snap(WallSlide(i % 2), i, backArm: true))),
        };
        var missing = Enum.GetValues(typeof(PlayerAnimState)).Cast<PlayerAnimState>().Except(clips.Select(c => c.state)).ToList();
        if (missing.Count > 0) throw new InvalidOperationException("No player clip for: " + string.Join(", ", missing));
        return clips;
    }

    public const int SnapFrames = 5;

    private static IEnumerable<Pose> Frames(int count, Func<int, Pose> frame) => Enumerable.Range(0, count).Select(frame);

    // A design-pixel x measured on the hitbox, compensated for the enlarged drawing, so contact
    // points (wall palm, kick-off foot, box grip) still meet the hitbox's edges.
    private static float Edge(float x) => x / VisualScaleX;

    // Standing: a slow one-pixel bob with the antenna lagging behind, and a blink once per loop.
    private static Pose Idle(int i) => new Pose
    {
        body = new V2(0, i >= 3 && i <= 6 ? -1 : 0),
        antenna = i >= 4 && i <= 7 ? -1 : 0,
        visor = i == 7 ? VisorMode.Blink : VisorMode.Normal,
    };

    // Eight-frame walk: a big bounce, forward lean, wide stride and swinging arms.
    private static Pose Walk(int i)
    {
        float phase = i / 8f * MathF.PI * 2;
        float c = MathF.Cos(phase);
        return new Pose
        {
            body = new V2(0, i % 4 == 0 ? -2 : i % 4 == 2 ? 0 : -1),
            lean = .1f,
            frontFoot = Step(phase, 6, 0),
            backFoot = Step(phase + MathF.PI, 6, 0),
            frontArm = new Arm(4 - 5 * c, 18),
            backArm = new Arm(-5 + 5 * c, 18),
            antenna = -1 - (i % 4 == 0 ? 1 : 0),
        };
    }

    // Short, braced steps while moving a box; direction -1 steps backward (pulling).
    private static Pose Shuffle(int i, int direction, float lean)
    {
        float phase = direction * i / 6f * MathF.PI * 2;
        return new Pose
        {
            body = new V2(direction < 0 ? -1 : 0, i % 3 == 0 ? -2 : -1),
            lean = lean,
            frontFoot = Step(phase, 3, direction < 0 ? 1 : -1),
            backFoot = Step(phase + MathF.PI, 3, direction < 0 ? -1 : -4),
            antenna = direction > 0 ? -1 : 1,
        };
    }

    private static V2 Step(float phase, float stride, float center)
    {
        float s = MathF.Sin(phase);
        return new V2(center + stride * MathF.Cos(phase), s < 0 ? -3 * s : 0); // Lifted while swinging forward.
    }

    // Takeoff: stretched tall, one fist punched up, legs tucked, antenna swept back.
    private static Pose Rise(int i) => new Pose
    {
        squash = -.08f,
        frontFoot = new V2(4, 7),
        backFoot = new V2(-2, 2),
        frontArm = new Arm(10, 37),
        backArm = new Arm(-9, 18),
        antenna = -2 - i,
    };

    // Dropping: squat and wide, both arms flung up and out, legs apart, antenna flicking.
    private static Pose Fall(int i) => new Pose
    {
        squash = .05f,
        frontFoot = new V2(6, 3),
        backFoot = new V2(-6, 2),
        frontArm = new Arm(12, 34 + i),
        backArm = new Arm(-12, 35 - i, inFront: false),
        antenna = i == 0 ? 1 : 2,
    };

    // A calmer airborne stance that keeps the arms free for aiming and snapping.
    private static Pose Air(int i) => new Pose
    {
        frontFoot = new V2(3, 5),
        backFoot = new V2(-3, 2),
        antenna = 1 + i,
    };

    // Facing the wall (at the hitbox's front edge): palm high on it, foot braced, other arm out.
    private static Pose WallSlide(int i) => new Pose
    {
        body = new V2(1, 0),
        frontArm = new Arm(Edge(11.5f), 36, HandShape.Flat),
        backArm = new Arm(-11, 24),
        frontFoot = new V2(Edge(9), 7),
        backFoot = new V2(-2, 0),
        antenna = 2,
        dust = i == 0
            ? new[] { new V2(Edge(13), 38), new V2(Edge(12), 41), new V2(Edge(13), 10) }
            : new[] { new V2(Edge(12), 43), new V2(Edge(13), 37), new V2(Edge(12), 13), new V2(Edge(13), 9) },
    };

    // Kicking off a wall (now behind, at the hitbox's back edge) and sailing away from it.
    private static Pose WallJump(int i) => i switch
    {
        0 => new Pose
        {
            body = new V2(-1, -3), lean = .12f,
            backFoot = new V2(Edge(-11), 5), frontFoot = new V2(4, 8),
            frontArm = new Arm(10, 26), backArm = new Arm(-10, 22),
            antenna = -2,
            dust = new[] { new V2(Edge(-13), 4), new V2(Edge(-13), 8), new V2(Edge(-12), 6) },
        },
        1 => new Pose
        {
            squash = -.1f, lean = .18f,
            backFoot = new V2(-8, 3), frontFoot = new V2(3, 6),
            frontArm = new Arm(11, 36), backArm = new Arm(-10, 26),
            antenna = -3,
        },
        _ => Rise(1).With(p => p.antenna = -2),
    };

    // Both hands straight out, flat on a box face just past the hitbox's front edge.
    private static Pose Grip(Pose p) => p.With(q =>
    {
        q.frontArm = new Arm(Edge(13), 22, HandShape.Grip);
        q.backArm = new Arm(Edge(12), 25, HandShape.Grip);
    });

    // Aiming: a hand raised to the side of the visor, which turns into a scanning dot; chest lit.
    private static Pose Aim(Pose p, int i, bool backArm = false) => p.With(q =>
    {
        if (backArm) q.backArm = new Arm(-10, 37, HandShape.Flat); // Front hand stays on the wall.
        else q.frontArm = new Arm(11, 34, HandShape.Flat);
        q.visor = VisorMode.Scan;
        q.scan = new[] { 0, 1, 2, 3 }[i % 4];
        q.chestLight = true;
    });

    // Banishing: a crouched wind-up, then both arms flung wide with a flash of the visor and sparks at the hands.
    private static Pose Snap(Pose p, int i, bool backArm = false) => p.With(q =>
    {
        (Arm front, Arm back) arms = i switch
        {
            0 => (new Arm(6, 19), new Arm(-7, 19)),
            1 => (new Arm(9, 30), new Arm(-9, 30)),
            2 => (new Arm(13, 30), new Arm(-13, 30)),
            3 => (new Arm(13, 29), new Arm(-13, 29)),
            _ => (new Arm(7, 22), new Arm(-8, 22)),
        };
        if (backArm) q.backArm = arms.back; // Front hand stays on the wall.
        else (q.frontArm, q.backArm) = arms;
        if (i == 0) q.squash = MathF.Max(q.squash, .06f);
        q.spark = i == 2 ? SparkSize.Big : i == 3 ? SparkSize.Small : SparkSize.None;
        if (i == 2) q.visor = VisorMode.Flash;
        q.chestLight = i >= 1 && i <= 3;
    });

    // ---------------------------------------------------------------- Drawing

    public static PixelCanvas Render(Pose p, Layout layout)
    {
        var canvas = new PixelCanvas(layout.frameWidth, layout.frameHeight);
        new Painter(canvas, layout, p).Draw();
        return canvas;
    }

    private sealed class Painter
    {
        private readonly PixelCanvas c;
        private readonly Layout l;
        private readonly Pose p;
        private readonly V2 headShift; // Whole-pixel head offset from the body offset and lean.

        public Painter(PixelCanvas canvas, Layout layout, Pose pose)
        {
            c = canvas;
            l = layout;
            p = pose;
            headShift = new V2(MathF.Round(p.body.x + p.lean * (40 - HipY)), p.body.y);
        }

        // Design pixels -> canvas pixels, squashed or stretched about the feet.
        private V2 T(V2 d) => new V2(
            l.originX + d.x * (1 + p.squash) * l.scaleX,
            l.originY + d.y * (1 - p.squash) * l.scaleY);
        private V2 T(float x, float y) => T(new V2(x, y));
        private float R(float r) => r * (l.scaleX + l.scaleY) / 2;
        // A torso point at rest -> its posed position (body offset plus lean above the hips).
        private V2 Torso(float x, float y) => new V2(x + p.body.x + p.lean * (y - HipY), y + p.body.y);
        private V2 HeadPt(float x, float y) => new V2(x, y) + headShift;

        private void Rect(Px col, float x0, float y0, float x1, float y1)
        {
            V2 a = T(x0, y0), b = T(x1, y1);
            c.FillRect(col, a.x, a.y, b.x, b.y);
        }
        private void HeadRect(Px col, float x0, float y0, float x1, float y1)
        {
            V2 a = HeadPt(x0, y0), b = HeadPt(x1, y1);
            Rect(col, a.x, a.y, b.x, b.y);
        }
        private void Dot(Px col, V2 d) => Rect(col, d.x, d.y, d.x + 1, d.y + 1);
        private void Poly(Px col, params V2[] pts) => c.FillPolygon(col, pts.Select(T).ToArray());
        private void Line(Px col, V2 a, V2 b, float r) => c.Capsule(col, T(a), T(b), R(r));
        private void Disc(Px col, V2 d, float r) => c.Disc(col, T(d), R(r));

        public void Draw()
        {
            if (!p.backArm.inFront) DrawArm(p.backArm, back: true);
            DrawLeg(p.backFoot, new V2(-3, HipY), back: true);
            DrawLeg(p.frontFoot, new V2(2, HipY), back: false);
            DrawTorso();
            DrawHead();
            if (p.backArm.inFront) DrawArm(p.backArm, back: true);
            DrawArm(p.frontArm, back: false);
            c.Outline(Outline);
            DrawEffects();
        }

        private void DrawLeg(V2 foot, V2 hipRest, bool back)
        {
            V2 hip = hipRest + p.body, ankle = foot + new V2(0, 2.5f);
            V2 knee = Joint(hip, ankle, Thigh, Shin, bend: 1); // Knees bend forward.
            Px limb = back ? LimbShade : Limb;
            Line(limb, hip, knee, 2);
            Line(limb, knee, ankle, 1.8f);
            // Block foot, toe forward, with a lit top edge like the floor plates.
            Rect(back ? BodyShade : Body, foot.x - 3, foot.y, foot.x + 4, foot.y + 3);
            Rect(back ? Body : BodyLip, foot.x - 3, foot.y + 2, foot.x + 4, foot.y + 3);
        }

        private void DrawTorso()
        {
            Poly(Body, Torso(-6, 17.5f), Torso(-4.5f, 16), Torso(5.5f, 16), Torso(7, 17.5f),
                Torso(7, 28.5f), Torso(5.5f, 30), Torso(-4.5f, 30), Torso(-6, 28.5f));
            Poly(BodyShade, Torso(-6, 17.5f), Torso(-4.5f, 16), Torso(-3.5f, 16), Torso(-3.5f, 28.5f), Torso(-6, 28.5f));
            Poly(BodyShade, Torso(-4.5f, 16), Torso(5.5f, 16), Torso(5.5f, 17.5f), Torso(-4.5f, 17.5f));
            Poly(BodyLip, Torso(-4.5f, 28.5f), Torso(7, 28.5f), Torso(5.5f, 30), Torso(-4.5f, 30));
            // Chest light: dim at rest, lit while aiming or banishing.
            Poly(FacePlate, Torso(1, 21), Torso(6, 21), Torso(6, 25), Torso(1, 25));
            Poly(p.chestLight ? Visor : VisorDim, Torso(2, 22), Torso(5, 22), Torso(5, 24), Torso(2, 24));
            // Neck.
            Poly(LimbShade, Torso(-2, 29.5f), Torso(3, 29.5f), Torso(3, 33), Torso(-2, 33));
        }

        private void DrawHead()
        {
            // Antenna behind the head, its tip echoing the visor.
            V2 antennaBase = HeadPt(-4, 47.5f), antennaTip = HeadPt(-4 + p.antenna, 51.5f);
            Line(HeadShade, antennaBase, antennaTip, .7f);
            Disc(p.visor == VisorMode.Flash ? VisorHi : Visor, antennaTip, 1.3f);

            Poly(Head, HeadPt(-8, 34), HeadPt(-6.5f, 32), HeadPt(7.5f, 32), HeadPt(9, 33.5f),
                HeadPt(9, 46.5f), HeadPt(7.5f, 48), HeadPt(-6.5f, 48), HeadPt(-8, 46.5f));
            Poly(HeadShade, HeadPt(-8, 34), HeadPt(-6.5f, 32), HeadPt(-5.5f, 32), HeadPt(-5.5f, 46.5f), HeadPt(-8, 46.5f));
            HeadRect(HeadShade, -6.5f, 32, 7.5f, 34);                              // Jaw in shadow.
            Poly(HeadLip, HeadPt(-6.5f, 46), HeadPt(9, 46), HeadPt(7.5f, 48), HeadPt(-6.5f, 48)); // Lit top lip.
            HeadRect(HeadShade, -4, 39, -2, 42);                                   // Ear bolt.
            HeadRect(HeadLip, -4, 41, -3, 42);

            // Wide visor in a dark faceplate one pixel proud of the face.
            HeadRect(FacePlate, -1, 36, 10, 45);
            switch (p.visor)
            {
                case VisorMode.Normal:
                    HeadRect(VisorDim, 0, 38, 10, 43);
                    HeadRect(Visor, 1, 39, 10, 43);
                    HeadRect(VisorHi, 3, 41, 6, 42);
                    break;
                case VisorMode.Blink:
                    HeadRect(VisorDim, 0, 40, 10, 41);
                    break;
                case VisorMode.Scan: // Dim band with a bright dot sweeping across it.
                    HeadRect(VisorDim, 0, 38, 10, 43);
                    float x = 1 + 2 * p.scan;
                    HeadRect(Visor, x, 38, x + 3, 43);
                    HeadRect(VisorHi, x + 1, 39, x + 2, 42);
                    break;
                case VisorMode.Flash:
                    HeadRect(VisorHi, -1, 37, 10, 44);
                    break;
            }
        }

        private void DrawArm(Arm arm, bool back)
        {
            V2 shoulder = Torso(back ? -3 : 3, ShoulderY);
            V2 hand = arm.hand + p.body;
            V2 elbow = Joint(shoulder, hand, UpperArm, Forearm, bend: -1); // Elbows bend backward.
            bool shaded = back && !arm.inFront;
            Px limb = shaded ? LimbShade : Limb;
            Line(limb, shoulder, elbow, 1.7f);
            Line(limb, elbow, hand, 1.6f);
            Px mitt = shaded ? HeadShade : Head;
            switch (arm.shape)
            {
                case HandShape.Flat:
                case HandShape.Grip:
                    Rect(mitt, hand.x - 1.5f, hand.y - 2, hand.x + 1.5f, hand.y + 2);
                    Rect(shaded ? Head : HeadLip, hand.x + .5f, hand.y - 2, hand.x + 1.5f, hand.y + 2); // Palm on the surface.
                    break;
                default:
                    Disc(mitt, hand, 2.1f);
                    if (!shaded) Dot(HeadLip, hand + new V2(-.5f, .5f));
                    break;
            }
        }

        private void DrawEffects()
        {
            foreach (var d in p.dust) Dot(Dust, d + new V2(0, p.body.y));
            if (p.spark == SparkSize.None) return;
            foreach (var arm in new[] { p.frontArm, p.backArm })
            {
                if (arm.shape != HandShape.Fist) continue; // A hand on a wall or box doesn't spark.
                V2 s = arm.hand + p.body + new V2(arm.hand.x > 0 ? 2 : -2, 2);
                if (p.spark == SparkSize.Big)
                {
                    Dot(SparkCore, s);
                    for (int k = 1; k <= 2; k++)
                    {
                        Dot(Spark, s + new V2(k, 0));
                        Dot(Spark, s + new V2(-k, 0));
                        Dot(Spark, s + new V2(0, k));
                        Dot(Spark, s + new V2(0, -k));
                    }
                    Dot(Spark, s + new V2(2, 2));
                    Dot(Spark, s + new V2(-2, 2));
                    Dot(Spark, s + new V2(2, -2));
                    Dot(Spark, s + new V2(-2, -2));
                }
                else
                {
                    Dot(Spark, s + new V2(3, 0));
                    Dot(Spark, s + new V2(-3, 1));
                    Dot(Spark, s + new V2(1, 3));
                    Dot(Spark, s + new V2(-1, -3));
                }
            }
        }

        /// <summary>Two-bone IK: the joint between root and tip, bending toward <paramref name="bend"/> (+1 = forward/left normal).</summary>
        private static V2 Joint(V2 root, V2 tip, float a, float b, float bend)
        {
            V2 d = tip - root;
            float len = MathF.Min(d.Length, (a + b) * .999f);
            if (len < .001f) return root + new V2(0, -a);
            V2 dir = d * (1 / d.Length);
            float along = (a * a - b * b + len * len) / (2 * len);
            float h = MathF.Sqrt(MathF.Max(0, a * a - along * along));
            return root + dir * along + new V2(-dir.y, dir.x) * (h * bend);
        }
    }
}
