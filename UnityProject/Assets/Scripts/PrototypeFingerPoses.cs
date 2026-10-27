using UnityEngine;
namespace Ironfront.UnityPrototype
{
    // Joint order: index, middle, ring, pinky, thumb; MCP/PIP/DIP for each.
    // Authored against evaluated Unity meshes. Thumb opposition is independent
    // from finger flexion; the slide grip is a different shape from a fist.
    internal static class PrototypeFingerPoses
    {
        public static readonly Vector3[] PistolSupport = {
            new Vector3(-54.3f, 0.0f, 0.5f), new Vector3(-27.4f, 0.0f, 0.0f), new Vector3(-29.0f, 0.0f, 0.0f),
            new Vector3(-51.4f, 0.0f, 5.0f), new Vector3(-39.1f, 0.0f, 0.0f), new Vector3(-35f, 0.0f, 0.0f),
            new Vector3(-50.5f, 0f, 9.5f), new Vector3(-38.0f, 0f, 0f), new Vector3(-31.0f, 0f, 0f),
            new Vector3(-68.0f, 0.0f, 13.5f), new Vector3(-20.8f, 0.0f, 0.0f), new Vector3(-17.5f, 0.0f, 0.0f),
            new Vector3(-45.3f, -9.0f, 0.0f), new Vector3(0f, 0.0f, 0.0f), new Vector3(0f, 0.0f, 0.0f),
        };
        public static readonly Vector3[] WeaponGrip = {
            new Vector3(-38.6f, 0.0f, 7.0f), new Vector3(-34.9f, 0.0f, 0.0f), new Vector3(-75f, 0.0f, 0.0f),
            new Vector3(-41f, 0f, -5.5f), new Vector3(-58f, 0f, 0f), new Vector3(-32f, 0f, 0f),
            new Vector3(-42.0f, 0.0f, -11.0f), new Vector3(-48.8f, 0.0f, 0.0f), new Vector3(-37.5f, 0.0f, 0.0f),
            new Vector3(-63.6f, 0.0f, -14.5f), new Vector3(-24.4f, 0.0f, 0.0f), new Vector3(-21.5f, 0.0f, 0.0f),
            new Vector3(-33.6f, 19.5f, -5.5f), new Vector3(-13.5f, 0.0f, 0.0f), new Vector3(0f, 0.0f, 0.0f),
        };
        public static readonly Vector3[] Magazine = {
            new Vector3(-71.3f, 0.0f, 2.0f), new Vector3(-44.6f, 0.0f, 0.0f), new Vector3(-26.5f, 0.0f, 0.0f),
            new Vector3(-52.4f, 0.0f, 3.5f), new Vector3(-72.9f, 0.0f, 0.0f), new Vector3(-13.5f, 0.0f, 0.0f),
            new Vector3(-44.2f, 0.0f, 11.0f), new Vector3(-60.8f, 0.0f, 0.0f), new Vector3(-28.5f, 0.0f, 0.0f),
            new Vector3(-63.8f, 0.0f, 17.0f), new Vector3(-25.4f, 0.0f, 0.0f), new Vector3(-19.5f, 0.0f, 0.0f),
            new Vector3(-15.2f, -6.5f, 14.0f), new Vector3(-45f, 0.0f, 0.0f), new Vector3(-40f, 0.0f, 0.0f),
        };
        public static readonly Vector3[] Slide = {
            new Vector3(-65f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(-18f, 0f, 0f),
            new Vector3(-75f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(-18f, 0f, 0f),
            new Vector3(-65f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(-18f, 0f, 0f),
            new Vector3(-45f, 0f, 0f), new Vector3(-10f, 0f, 0f), new Vector3(-18f, 0f, 0f),
            new Vector3(-81.7f, 30.0f, -15.0f), new Vector3(-45f, 0.0f, 0.0f), new Vector3(-19.5f, 0.0f, 0.0f),
        };
        public static readonly Vector3[] RifleSupport = {
            new Vector3(-60f,0f,0f),
            new Vector3(-25f,0f,0f),
            new Vector3(-20f,0f,0f),
            new Vector3(-65f,0f,0f),
            new Vector3(-25f,0f,0f),
            new Vector3(-20f,0f,0f),
            new Vector3(-60f,0f,0f),
            new Vector3(-25f,0f,0f),
            new Vector3(-20f,0f,0f),
            new Vector3(-50f,0f,0f),
            new Vector3(-25f,0f,0f),
            new Vector3(-20f,0f,0f),
            new Vector3(-25f,-20f,15f),
            new Vector3(-15f,0f,0f),
            new Vector3(-10f,0f,0f),
        };
        public static readonly Vector3[] RifleCharge = {
            new Vector3(-65f,0f,0f),
            new Vector3(-65f,0f,0f),
            new Vector3(-30f,0f,0f),
            new Vector3(-75f,0f,0f),
            new Vector3(-60f,0f,0f),
            new Vector3(-25f,0f,0f),
            new Vector3(-70f,0f,0f),
            new Vector3(-80f,0f,0f),
            new Vector3(-40f,0f,0f),
            new Vector3(-75f,0f,0f),
            new Vector3(-80f,0f,0f),
            new Vector3(-45f,0f,0f),
            new Vector3(-65f,20f,-10f),
            new Vector3(-35f,0f,0f),
            new Vector3(-20f,0f,0f),
        };
        public static Vector3 Relaxed(int digit, int joint) => new Vector3(digit == 4 ? -6f : joint == 0 ? -9f : -14f, 0, 0);
    }
}
