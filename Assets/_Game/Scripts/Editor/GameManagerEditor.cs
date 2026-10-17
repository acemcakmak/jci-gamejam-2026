using UnityEditor;
using UnityEngine;

namespace Komur.EditorTools
{
    // Game Manager inspector + Kömür menu: buttons to jump straight to each ending (Play mode only).
    [CustomEditor(typeof(GameManager))]
    public class GameManagerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Endings", EditorStyles.boldLabel);
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("Enter Play mode to jump straight to an ending.", MessageType.Info);
                return;
            }
            var gm = (GameManager)target;
            bool over = gm.State == GameState.Won || gm.State == GameState.Lost;
            using (new EditorGUI.DisabledScope(over))
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Village Loves You")) gm.DebugEnding(Ending.Loved);
                if (GUILayout.Button("Unknown Hero")) gm.DebugEnding(Ending.UnknownHero);
                if (GUILayout.Button("All Dead")) gm.DebugEnding(Ending.AllDead);
            }
            if (over && GUILayout.Button("Back to Main Menu")) gm.ReturnToMenu();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Test Boss", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(over || gm.BossAlive || gm.IsCinematic))
                if (GUILayout.Button(gm.BossAlive ? "Boss is here" : "Summon Boss")) gm.DebugSummonBoss();
        }

        private const string Menu = "Kömür/Test Ending/";

        [MenuItem(Menu + "Village Loves You", priority = 60)]
        private static void Loved() => Run(Ending.Loved);

        [MenuItem(Menu + "Unknown Hero", priority = 61)]
        private static void UnknownHero() => Run(Ending.UnknownHero);

        [MenuItem(Menu + "All Dead", priority = 62)]
        private static void AllDead() => Run(Ending.AllDead);

        [MenuItem(Menu + "Village Loves You", true)]
        [MenuItem(Menu + "Unknown Hero", true)]
        [MenuItem(Menu + "All Dead", true)]
        private static bool CanRun()
        {
            var gm = GameManager.I;
            return Application.isPlaying && gm != null && (gm.State == GameState.Intro || gm.State == GameState.Playing);
        }

        [MenuItem("Kömür/Summon Boss", priority = 64)]
        private static void SummonBoss() => GameManager.I?.DebugSummonBoss();

        [MenuItem("Kömür/Summon Boss", true)]
        private static bool CanSummonBoss() => CanRun() && !GameManager.I.BossAlive && !GameManager.I.IsCinematic;

        private static void Run(Ending ending)
        {
            if (GameManager.I != null) GameManager.I.DebugEnding(ending);
        }
    }
}
