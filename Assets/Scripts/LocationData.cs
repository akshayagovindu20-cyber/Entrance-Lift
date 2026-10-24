using System;
using System.Collections.Generic;
using UnityEngine;

namespace CampusTour
{
    public enum HotspotType
    {
        Info,
        Navigation
    }

    [Serializable]
    public class HotspotDefinition
    {
        [Tooltip("Title or identifier of the hotspot")]
        public string title = "Information";

        [Tooltip("Type: Info or Navigation")]
        public HotspotType type = HotspotType.Info;

        [Tooltip("Detailed description shown in the info popup")]
        [TextArea(2, 4)]
        public string description = "Hotspot details.";

        [Tooltip("Horizontal angle (-180 to 180 degrees) relative to center")]
        [Range(-180f, 180f)]
        public float yawDegrees = 0f;

        [Tooltip("Vertical angle (-80 to 80 degrees)")]
        [Range(-80f, 80f)]
        public float pitchDegrees = 0f;

        [Tooltip("Distance from camera")]
        public float distance = 5f;

        [Tooltip("Target location index if this is a Navigation hotspot")]
        public int targetLocationIndex = -1;
    }

    [Serializable]
    public class LocationData
    {
        [Tooltip("Display name of the campus location")]
        public string name = "New Location";

        [Tooltip("Relative video filename inside Assets/StreamingAssets/Videos/")]
        public string videoFileName = "GoPro360.mp4";

        [Tooltip("Short description displayed on banner/popup")]
        [TextArea(2, 5)]
        public string description = "Explore the surroundings using the 360-degree GoPro video.";

        [Tooltip("Whether this location currently has an assigned playable video")]
        public bool isAvailable = true;

        [Tooltip("Hotspots present in this location")]
        public List<HotspotDefinition> hotspots = new List<HotspotDefinition>();
    }
}
