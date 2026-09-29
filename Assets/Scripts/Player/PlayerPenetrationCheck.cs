using UnityEngine;

// Compatibility component: preserves existing prefab/scene references and collider settings.
// New killable objects should use DamageablePenetrationCheck directly.
[AddComponentMenu("")]
public sealed class PlayerPenetrationCheck : DamageablePenetrationCheck { }
