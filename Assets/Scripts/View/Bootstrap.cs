// The whole game is created from code, so the scene can stay empty (no scene file to merge or break).
using UnityEngine;

namespace CommunityWorld
{
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (Object.FindObjectOfType<GameController>() != null) return;
            GameObject go = new GameObject("Community World");
            go.AddComponent<GameController>();
        }
    }
}
