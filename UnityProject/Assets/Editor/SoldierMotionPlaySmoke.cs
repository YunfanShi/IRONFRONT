using UnityEditor;
using UnityEditor.SceneManagement;
namespace Ironfront.UnityPrototype.Editor
{
    public static class SoldierMotionPlaySmoke
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            EditorApplication.isPlaying=true;
        }
    }
}
