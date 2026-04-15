using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;
using Unity.XR.CoreUtils;

/// <summary>
/// One-click VR scene setup for Quest 3 / Device Simulator testing.
///
/// Open via:  Tools → VR Scene Setup
///
/// What it does:
///   1. Removes the plain Main Camera (XR Origin has its own camera).
///   2. Adds XR Interaction Setup prefab  (XR Interaction Manager + EventSystem).
///   3. Adds XR Origin (XR Rig) prefab   (CharacterController + CharacterControllerDriver
///                                         + DynamicMoveProvider + SnapTurnProvider
///                                         + ContinuousTurnProvider + controllers).
///   4. Adds XR Device Simulator prefab  (WASD + mouse in Editor Play Mode).
///   5. Fixes CharacterController radius for reliable wall collision.
///
/// Device Simulator controls (built into the XRI sample prefab):
///   WASD          — move HMD body in world space
///   Q / E         — move down / up
///   Left Shift    — hold for 5× speed boost
///   Mouse (RMB)   — rotate head / HMD
///   Right Ctrl    — hold to manipulate Right controller instead of HMD
///   Left Ctrl     — hold to manipulate Left controller
///   G             — toggle grip
///   T             — toggle trigger
/// </summary>
public class VRSceneSetup : EditorWindow
{
    // Asset GUIDs — discovered from .meta files in this project.
    private const string k_XROriginPrefabGuid         = "f6336ac4ac8b4d34bc5072418cdc62a0";
    private const string k_XRInteractionSetupGuid     = "895f6f3c2d334633b5800312285058d2";
    private const string k_XRDeviceSimulatorGuid      = "18ddb545287c546e19cc77dc9fbb2189";

    // Recommended CC radius: 0.3 m gives solid wall collisions on Quest 3.
    // The prefab ships with 0.1, which lets players clip through thin meshes.
    private const float  k_CCRadius                   = 0.3f;
    private const float  k_CCStepOffset               = 0.3f;

    private bool _addSimulator = true;

    [MenuItem("Tools/VR Scene Setup")]
    public static void ShowWindow() =>
        GetWindow<VRSceneSetup>("VR Scene Setup");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("VR Scene Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configures the active scene with a full XR Origin rig ready for:\n" +
            "  • Quest 3 / OpenXR hardware testing\n" +
            "  • XR Device Simulator (keyboard + mouse in Editor)\n\n" +
            "The XR Origin prefab already contains:\n" +
            "  ✓ CharacterController  (blocks wall passage)\n" +
            "  ✓ CharacterControllerDriver  (syncs capsule to HMD height)\n" +
            "  ✓ DynamicMoveProvider  (Action-based continuous move)\n" +
            "  ✓ SnapTurnProvider + ContinuousTurnProvider\n" +
            "  ✓ Left / Right controller interactors\n" +
            "  ✓ XRI Default Input Actions wired to all providers",
            MessageType.Info);

        EditorGUILayout.Space();

        // Status indicators
        bool hasInteractionSetup = FindFirstObjectByType<XRInteractionManager>() != null;
        bool hasXROrigin         = FindFirstObjectByType<XROrigin>() != null;
        bool hasSimulator        = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                                      .Any(mb => mb != null &&
                                          mb.GetType().Name == "XRDeviceSimulator");

        DrawStatus("XR Interaction Setup (Manager + EventSystem)", hasInteractionSetup);
        DrawStatus("XR Origin (XR Rig)",                           hasXROrigin);
        DrawStatus("XR Device Simulator",                          hasSimulator);

        EditorGUILayout.Space();
        _addSimulator = EditorGUILayout.Toggle("Include Device Simulator", _addSimulator);
        EditorGUILayout.HelpBox(
            "Disable Device Simulator for final Quest 3 builds — it overrides real HMD input.",
            MessageType.None);

        EditorGUILayout.Space();
        if (GUILayout.Button("Setup VR Scene", GUILayout.Height(44)))
            RunSetup();

        if (hasXROrigin)
        {
            EditorGUILayout.Space();
            if (GUILayout.Button("Fix CharacterController Radius → 0.3 m"))
                FixCCRadius();

            var origin = FindFirstObjectByType<XROrigin>();
            var cc = origin != null ? origin.GetComponent<CharacterController>() : null;
            if (cc != null)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Current CharacterController:", EditorStyles.boldLabel);
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.FloatField("Radius",      cc.radius);
                    EditorGUILayout.FloatField("Height",      cc.height);
                    EditorGUILayout.FloatField("Skin Width",  cc.skinWidth);
                    EditorGUILayout.FloatField("Step Offset", cc.stepOffset);
                }
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Device Simulator Controls", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "WASD       — move body (world space)\n" +
            "Q / E      — move down / up\n" +
            "Left Shift — 5× speed\n" +
            "Mouse      — look / rotate HMD  (hold RMB to enter look mode)\n" +
            "Right Ctrl — manipulate Right controller\n" +
            "Left Ctrl  — manipulate Left controller\n" +
            "G / T      — grip / trigger\n" +
            "F1         — toggle Simulator UI overlay",
            MessageType.None);
    }

    // -------------------------------------------------------------------------

    private void RunSetup()
    {
        bool changed = false;

        // 1 — Remove plain Main Camera (XR Origin ships with its own XR camera).
        changed |= RemovePlainMainCamera();

        // 2 — XR Interaction Setup (Interaction Manager + EventSystem + UI Input).
        if (FindFirstObjectByType<XRInteractionManager>() == null)
        {
            changed |= InstantiatePrefab(k_XRInteractionSetupGuid, "XR Interaction Setup");
        }
        else
        {
            Debug.Log("[VRSceneSetup] XR Interaction Manager already present — skipping.");
        }

        // 3 — XR Origin (XR Rig).
        GameObject originGO = null;
        if (FindFirstObjectByType<XROrigin>() == null)
        {
            originGO = InstantiatePrefabAndReturn(k_XROriginPrefabGuid, "XR Origin (XR Rig)");
            changed  = originGO != null;
        }
        else
        {
            Debug.Log("[VRSceneSetup] XR Origin already present — skipping creation.");
            var existing = FindFirstObjectByType<XROrigin>();
            if (existing != null) originGO = existing.gameObject;
        }

        // Fix the CharacterController radius on whichever origin we have.
        if (originGO != null)
            changed |= FixCCRadiusOn(originGO);

        // 4 — XR Device Simulator.
        if (_addSimulator)
        {
            bool simExists = FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Any(mb => mb != null && mb.GetType().Name == "XRDeviceSimulator");

            if (!simExists)
                changed |= InstantiatePrefab(k_XRDeviceSimulatorGuid, "XR Device Simulator");
            else
                Debug.Log("[VRSceneSetup] XR Device Simulator already present — skipping.");
        }

        if (changed)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Repaint();
            EditorUtility.DisplayDialog("VR Scene Setup",
                "Done! Save the scene: Ctrl+S\n\n" +
                "Press Play to test with the Device Simulator.\n" +
                "WASD moves the player body, mouse rotates the head.\n\n" +
                "Disable XR Device Simulator GameObject before building for Quest 3.",
                "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("VR Scene Setup", "Scene already set up — nothing changed.", "OK");
        }
    }

    // -------------------------------------------------------------------------

    private static bool RemovePlainMainCamera()
    {
        var cam = Camera.main;
        if (cam == null) return false;

        // Only remove a plain camera — leave it alone if it has XR components.
        bool hasXRComponents = cam.GetComponent<XROrigin>() != null ||
                               cam.GetComponentInParent<XROrigin>() != null;
        if (hasXRComponents) return false;

        string name = cam.gameObject.name;
        Undo.DestroyObjectImmediate(cam.gameObject);
        Debug.Log($"[VRSceneSetup] Removed plain camera: '{name}'.");
        return true;
    }

    private static bool InstantiatePrefab(string guid, string label)
    {
        return InstantiatePrefabAndReturn(guid, label) != null;
    }

    private static GameObject InstantiatePrefabAndReturn(string guid, string label)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError($"[VRSceneSetup] Prefab '{label}' not found. GUID: {guid}");
            return null;
        }

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogError($"[VRSceneSetup] Could not load prefab at path: {path}");
            return null;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
        Undo.RegisterCreatedObjectUndo(instance, $"Add {label}");
        Debug.Log($"[VRSceneSetup] Added '{label}' to scene.");
        return instance;
    }

    private static bool FixCCRadiusOn(GameObject originGO)
    {
        // CC lives on the XROrigin root in the XRI starter prefab.
        var cc = originGO.GetComponent<CharacterController>();
        if (cc == null) return false;

        bool needsFix = !Mathf.Approximately(cc.radius, k_CCRadius) ||
                        !Mathf.Approximately(cc.stepOffset, k_CCStepOffset);
        if (!needsFix) return false;

        Undo.RecordObject(cc, "Fix CC Radius");
        cc.radius     = k_CCRadius;
        cc.stepOffset = k_CCStepOffset;
        Debug.Log($"[VRSceneSetup] CharacterController radius → {k_CCRadius}, stepOffset → {k_CCStepOffset}.");
        return true;
    }

    private void FixCCRadius()
    {
        var origin = FindFirstObjectByType<XROrigin>();
        if (origin == null) { Debug.LogWarning("[VRSceneSetup] No XR Origin found."); return; }
        bool changed = FixCCRadiusOn(origin.gameObject);
        if (changed) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Repaint();
    }

    private static void DrawStatus(string label, bool ok)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            var style = new GUIStyle(EditorStyles.label)
                { normal = { textColor = ok ? new Color(0.2f, 0.8f, 0.2f) : new Color(1f, 0.4f, 0.3f) } };
            EditorGUILayout.LabelField(ok ? "✓" : "✗", style, GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
        }
    }
}
