using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Room Connection")]
public sealed class RoomConnection : ScriptableObject
{
    [Serializable]
    public class Endpoint
    {
        [Tooltip("Full scene asset path. Use the scene picker in the Inspector.")]
        public string scenePath;
        public string zoneId;
        public bool Matches(string scene, string id) => scenePath == scene && zoneId == id;
    }
    public Endpoint a = new Endpoint();
    public Endpoint b = new Endpoint();

    public bool TryGetDestination(string scenePath, string zoneId, out Endpoint destination)
    {
        destination = null;
        if (a == null || b == null || a.scenePath == b.scenePath ||
            string.IsNullOrWhiteSpace(a.zoneId) || string.IsNullOrWhiteSpace(b.zoneId) ||
            string.IsNullOrWhiteSpace(a.scenePath) || string.IsNullOrWhiteSpace(b.scenePath)) return false;
        if (a.Matches(scenePath, zoneId)) destination = b;
        else if (b.Matches(scenePath, zoneId)) destination = a;
        return destination != null;
    }
}
