using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Generated in the editor from RoomTransition destinations (see RoomLinksBuilder); never edit by hand.
/// A link declared on either doorway resolves travel in both directions.
/// </summary>
public sealed class RoomLinks : ScriptableObject
{
    public const string ResourcePath = "RoomLinks";

    [Serializable]
    public struct Link
    {
        public string fromScene;
        public string fromZone;
        public string toScene;
        public string toZone;
    }

    public List<Link> links = new List<Link>();

    private static RoomLinks instance;
    public static RoomLinks Instance => instance != null ? instance : instance = Resources.Load<RoomLinks>(ResourcePath);

    public bool TryGetDestination(string scenePath, string zoneId, out string destinationScene, out string destinationZone)
    {
        foreach (var link in links)
        {
            if (link.fromScene == scenePath && link.fromZone == zoneId)
            {
                destinationScene = link.toScene; destinationZone = link.toZone;
                return true;
            }
            if (link.toScene == scenePath && link.toZone == zoneId)
            {
                destinationScene = link.fromScene; destinationZone = link.fromZone;
                return true;
            }
        }
        destinationScene = destinationZone = null;
        return false;
    }
}
