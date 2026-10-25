using UnityEditor;
using UnityEditor.SceneManagement;
namespace Ironfront.UnityPrototype.Editor
{
    public static class WeaponArtPlaySmoke
    {
        public static void Run()
        {
            WeaponArtAssetSetup.Build();
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");EditorApplication.isPlaying=true;
        }
    }
}
