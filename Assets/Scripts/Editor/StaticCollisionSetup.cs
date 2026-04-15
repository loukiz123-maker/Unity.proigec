using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Editor tool: scans the active scene for MeshRenderer objects whose names match
/// wall/floor/ceiling/environment keywords, assigns the StaticEnvironment layer,
/// and adds a MeshCollider if one is missing.
///
/// Open via: Tools → Static Collision Setup
/// </summary>
public class StaticCollisionSetup : EditorWindow
{
    // Keywords used to identify environment meshes (case-insensitive substring match).
    private static readonly string[] s_EnvironmentKeywords =
    {
        "wall", "floor", "ceiling", "roof", "ground", "stair", "step",
        "pillar", "column", "door", "arch", "room", "corridor", "hallway",
        "environment", "env", "static", "terrain", "barrier", "fence",
        "window", "sill", "baseboard", "trim"
    };

    // Layer index assigned to environment objects.
    private const int k_StaticEnvLayer = 8; // "StaticEnvironment" added to TagManager slot 8

    private bool _convex;
    private bool _previewOnly = true;
    private Vector2 _scroll;
    private List<PreviewItem> _preview = new List<PreviewItem>();

    private class PreviewItem
    {
        public GameObject go;
        public bool willAddCollider;
        public bool willSetLayer;
    }

    [MenuItem("Tools/Static Collision Setup")]
    public static void ShowWindow()
    {
        GetWindow<StaticCollisionSetup>("Static Collision Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Static Collision Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Scans all MeshRenderers in the active scene. Objects whose names contain " +
            "wall/floor/ceiling/etc. keywords will receive a MeshCollider and be moved " +
            "to the 'StaticEnvironment' layer (8).",
            MessageType.Info);

        EditorGUILayout.Space();
        _convex = EditorGUILayout.Toggle("Convex Colliders", _convex);
        EditorGUILayout.HelpBox(
            "Keep Convex OFF for room geometry. Convex is only needed if the object " +
            "must interact with Rigidbodies using Continuous detection.",
            MessageType.None);

        EditorGUILayout.Space();

        if (GUILayout.Button("Scan Scene (Preview)"))
        {
            ScanScene();
            _previewOnly = true;
        }

        if (_preview.Count > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField($"Found {_preview.Count} candidate(s):", EditorStyles.boldLabel);

            _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.MaxHeight(300));
            foreach (var item in _preview)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.ObjectField(item.go, typeof(GameObject), true, GUILayout.Width(220));
                    if (item.willAddCollider)
                        EditorGUILayout.LabelField("[+ MeshCollider]", GUILayout.Width(120));
                    else
                        EditorGUILayout.LabelField("[collider exists]", GUILayout.Width(120));

                    if (item.willSetLayer)
                        EditorGUILayout.LabelField("[Layer → StaticEnvironment]");
                    else
                        EditorGUILayout.LabelField("[layer OK]");
                }
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            using (new EditorGUI.DisabledScope(_preview.Count == 0))
            {
                if (GUILayout.Button("Apply to Scene", GUILayout.Height(36)))
                    ApplyToScene();
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("— Manual override —", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Select any GameObjects in the Hierarchy and click the button below to force-add " +
            "a MeshCollider + StaticEnvironment layer regardless of their names.",
            MessageType.None);
        if (GUILayout.Button("Apply to Selection"))
            ApplyToSelection();
    }

    // -------------------------------------------------------------------------

    private void ScanScene()
    {
        _preview.Clear();
        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None);

        foreach (var mr in renderers)
        {
            string nameLower = mr.gameObject.name.ToLowerInvariant();
            bool isEnvironment = false;
            foreach (var kw in s_EnvironmentKeywords)
            {
                if (nameLower.Contains(kw))
                {
                    isEnvironment = true;
                    break;
                }
            }

            // Also match by parent name one level up (e.g. a "Wall_01" parent).
            if (!isEnvironment && mr.transform.parent != null)
            {
                string parentLower = mr.transform.parent.name.ToLowerInvariant();
                foreach (var kw in s_EnvironmentKeywords)
                {
                    if (parentLower.Contains(kw))
                    {
                        isEnvironment = true;
                        break;
                    }
                }
            }

            if (!isEnvironment)
                continue;

            bool hasCollider = mr.GetComponent<Collider>() != null;
            _preview.Add(new PreviewItem
            {
                go = mr.gameObject,
                willAddCollider = !hasCollider,
                willSetLayer = mr.gameObject.layer != k_StaticEnvLayer
            });
        }
    }

    private void ApplyToScene()
    {
        int addedColliders = 0;
        int changedLayers = 0;

        foreach (var item in _preview)
        {
            if (item.go == null)
                continue;

            Undo.RecordObject(item.go, "Static Collision Setup");

            if (item.willAddCollider)
            {
                var col = Undo.AddComponent<MeshCollider>(item.go);
                col.convex = _convex;
                // cookingOptions: default is fine; CookForFasterSimulation helps Quest performance
                col.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                     MeshColliderCookingOptions.EnableMeshCleaning |
                                     MeshColliderCookingOptions.WeldColocatedVertices;
                addedColliders++;
            }

            if (item.willSetLayer)
            {
                item.go.layer = k_StaticEnvLayer;
                changedLayers++;
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[StaticCollisionSetup] Done: +{addedColliders} MeshColliders, {changedLayers} layer changes.");
        EditorUtility.DisplayDialog("Done",
            $"Added {addedColliders} MeshCollider(s).\n" +
            $"Set {changedLayers} object(s) to StaticEnvironment layer.\n\n" +
            "Don't forget to save the scene (Ctrl+S).",
            "OK");

        ScanScene(); // refresh preview
    }

    private void ApplyToSelection()
    {
        if (Selection.gameObjects.Length == 0)
        {
            EditorUtility.DisplayDialog("No selection", "Select GameObjects in the Hierarchy first.", "OK");
            return;
        }

        int added = 0;
        foreach (var go in Selection.gameObjects)
        {
            Undo.RecordObject(go, "Static Collision Setup (manual)");

            if (go.GetComponent<Collider>() == null && go.GetComponent<MeshFilter>() != null)
            {
                var col = Undo.AddComponent<MeshCollider>(go);
                col.convex = _convex;
                col.cookingOptions = MeshColliderCookingOptions.CookForFasterSimulation |
                                     MeshColliderCookingOptions.EnableMeshCleaning |
                                     MeshColliderCookingOptions.WeldColocatedVertices;
                added++;
            }

            go.layer = k_StaticEnvLayer;
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log($"[StaticCollisionSetup] Manual: processed {Selection.gameObjects.Length} object(s), +{added} colliders.");
    }
}
