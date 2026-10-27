using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ironfront.UnityPrototype.Editor
{
    public static class HandReloadAssetSetup
    {
        public static void Preflight()
        {
            Debug.Log("AGENT_RESULT {\"job\":\"HandReload.Preflight\",\"ok\":true,\"unity\":\"" + Application.unityVersion +
                "\",\"target\":\"" + EditorUserBuildSettings.activeBuildTarget + "\",\"colorSpace\":\"" + PlayerSettings.colorSpace +
                "\",\"pipeline\":\"" + GraphicsSettings.currentRenderPipeline + "\"}");
        }
    }
}
