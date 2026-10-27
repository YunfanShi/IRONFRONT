using System;
using UnityEngine;

namespace Ironfront.UnityPrototype
{
    // Evaluated samples from the user's Kevin Iglesias pack. All coordinates
    // are semantic weapon space, in metres relative to the source right wrist.
    // Gameplay owns reload duration; this class only samples presentation.
    public sealed class PrototypeSoldierPackMotion
    {
        [Serializable] public sealed class Frame
        {
            public Vector3 leftHand, leftElbow, rightElbow, leftShoulder, rightShoulder, gunPosition;
            public Quaternion leftRotation, rightRotation, gunRotation;
            public Quaternion[] fingers = new Quaternion[30];
        }
        [Serializable] public sealed class Clip
        {
            public string key, sourceAction;
            public float duration, sampleRate;
            public int firstFrame, lastFrame;
            public Frame[] frames;
        }
        [Serializable] sealed class Library { public string source, packageSha256; public Clip[] clips; public Vector3[] fingerRestDirections; }
        static Library library;
        public readonly Clip Hold, Reload;
        public Vector3[] FingerRestDirections => library?.fingerRestDirections;
        public readonly Frame Pose = new Frame(), View = new Frame();
        public bool Valid => Hold != null && Reload != null && Hold.frames.Length > 1 && Reload.frames.Length > 1;
        public PrototypeSoldierPackMotion(bool pistol)
        {
            if (library == null)
            {
                var text = Resources.Load<TextAsset>("Animation/SoldierPackTrial");
                if (text != null) library = JsonUtility.FromJson<Library>(text.text);
            }
            if (library?.clips == null) return;
            Hold = Array.Find(library.clips, c => c.key == (pistol ? "pistolHold" : "rifleHold"));
            Reload = Array.Find(library.clips, c => c.key == "rifleReload");
        }
        public void Evaluate(bool reload, float phase, float elapsed)
        {
            var clip = reload ? Reload : Hold;
            Sample(clip, reload ? phase : Mathf.Repeat(elapsed, clip.duration) / clip.duration, Pose);
        }
        public static void Sample(Clip clip, float phase, Frame output)
        {
            float value = Mathf.Clamp01(phase) * (clip.frames.Length - 1);
            int i = Mathf.Min((int)value, clip.frames.Length - 2); float t = value - i;
            Frame a=clip.frames[i], b=clip.frames[i+1];
            output.leftHand=Vector3.Lerp(a.leftHand,b.leftHand,t);
            output.leftElbow=Vector3.Lerp(a.leftElbow,b.leftElbow,t);
            output.rightElbow=Vector3.Lerp(a.rightElbow,b.rightElbow,t);
            output.leftShoulder=Vector3.Lerp(a.leftShoulder,b.leftShoulder,t);
            output.rightShoulder=Vector3.Lerp(a.rightShoulder,b.rightShoulder,t);
            output.gunPosition=Vector3.Lerp(a.gunPosition,b.gunPosition,t);
            output.leftRotation=Quaternion.Slerp(a.leftRotation,b.leftRotation,t);
            output.rightRotation=Quaternion.Slerp(a.rightRotation,b.rightRotation,t);
            output.gunRotation=Quaternion.Slerp(a.gunRotation,b.gunRotation,t);
            for(int n=0;n<30;n++)output.fingers[n]=Quaternion.Slerp(a.fingers[n],b.fingers[n],t);
        }
    }
}
