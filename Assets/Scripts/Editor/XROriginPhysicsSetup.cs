using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Editor tool: configures CharacterController + CharacterControllerDriver on the XR Origin
/// so the player cannot walk through walls on Quest 3.
///
/// Open via: Tools → XR Origin Physics Setup
///
/// What it does:
///   1. Finds the XROrigin in the active scene.
///   2. Adds a CharacterController on XROrigin.Origin (the "Rig" child) if missing.
///   3. Configures CC parameters for Quest 3 / VR (skin width, step offset, slope limit).
///   4. Adds CharacterControllerDriver on the XROrigin root if missing and links it to
///      the first ContinuousMoveProviderBase found in the scene.
///   5. Ensures there is NO Rigidbody on the XR Origin (it conflicts with CC).
///   6. Sets the XR Origin layer to StaticEnvironment collision mask (optional).
/// </summary>
public class XROriginPhysicsSetup : EditorWindow
{
    // Recommended CharacterController values for a standing VR player (Quest 3).
    private const float k_Radius      = 0.3f;   // ~30 cm, enough to block doorframes
    private const float k_Height      = 1.8f;   // default capsule height (CC Driver overrides this at runtime)
    private const float k_SkinWidth   = 0.08f;  // default Unity value; prevents jitter at corners
    private const float k_StepOffset  = 0.3f;   // max step height (stairs)
    private const float k_SlopeLimit  = 45f;    // max walkable slope in degrees
    private const float k_MinMoveDistance = 0f; // CC only moves if displacement > this

    [MenuItem("Tools/XR Origin Physics Setup")]
    public static void ShowWindow()
    {
        GetWindow<XROriginPhysicsSetup>("XR Origin Physics Setup");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("XR Origin Physics Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configures CharacterController + CharacterControllerDriver on the XR Origin " +
            "so the player physically collides with walls.\n\n" +
            "Requires: XR Origin in the active scene, XR Interaction Toolkit 2.5.x.",
            MessageType.Info);

        EditorGUILayout.Space();

        var origin = FindFirstObjectByType<XROrigin>();
        if (origin == null)
        {
            EditorGUILayout.HelpBox(
                "No XR Origin found in the active scene.\n" +
                "Add one via: GameObject → XR → XR Origin (Action-based)",
                MessageType.Warning);

            if (GUILayout.Button("Add XR Origin (Action-based) to Scene"))
                AddXROriginToScene();
            return;
        }

        EditorGUILayout.HelpBox($"XR Origin found: '{origin.name}'", MessageType.None);
        EditorGUILayout.Space();

        var rig = origin.Origin; // This is the "Rig" child with the actual CC
        var cc  = rig != null ? rig.GetComponent<CharacterController>() : null;
        var ccd = origin.GetComponent<CharacterControllerDriver>();
        var rb  = rig != null ? rig.GetComponent<Rigidbody>() : null;

        DrawStatus("CharacterController on Rig",        cc  != null);
        DrawStatus("CharacterControllerDriver on Origin", ccd != null);
        DrawStatus("No Rigidbody (conflict avoided)",    rb  == null);

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Physics Setup", GUILayout.Height(40)))
            Apply(origin);

        if (cc != null)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Current CharacterController values:", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.FloatField("Radius",            cc.radius);
                EditorGUILayout.FloatField("Height",            cc.height);
                EditorGUILayout.FloatField("Skin Width",        cc.skinWidth);
                EditorGUILayout.FloatField("Step Offset",       cc.stepOffset);
                EditorGUILayout.FloatField("Slope Limit",       cc.slopeLimit);
                EditorGUILayout.FloatField("Min Move Distance", cc.minMoveDistance);
            }
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Anti-tunneling (Physics Settings)", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "To prevent tunneling at low FPS:\n" +
            "• Edit → Project Settings → Physics\n" +
            "  - Default Contact Offset: 0.01\n" +
            "  - Sleep Threshold: 0.005\n" +
            "CharacterController does NOT use Rigidbody, so CCD (Continuous Collision " +
            "Detection) is not needed — the CC itself is already sweep-based.",
            MessageType.None);

        if (GUILayout.Button("Open Physics Project Settings"))
            SettingsService.OpenProjectSettings("Project/Physics");
    }

    // -------------------------------------------------------------------------

    private static void DrawStatus(string label, bool ok)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(ok ? "✓" : "✗", GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
        }
    }

    private static void Apply(XROrigin origin)
    {
        var rig = origin.Origin;
        if (rig == null)
        {
            Debug.LogError("[XROriginPhysicsSetup] XROrigin.Origin (Rig) is null.");
            return;
        }

        // 1. Remove Rigidbody — it conflicts with CharacterController locomotion.
        var rb = rig.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Undo.DestroyObjectImmediate(rb);
            Debug.Log("[XROriginPhysicsSetup] Removed Rigidbody from Rig (conflicts with CC).");
        }

        // 2. Add / configure CharacterController on the Rig.
        var cc = rig.GetComponent<CharacterController>();
        if (cc == null)
        {
            cc = Undo.AddComponent<CharacterController>(rig);
            Debug.Log("[XROriginPhysicsSetup] Added CharacterController to Rig.");
        }

        Undo.RecordObject(cc, "XR Origin Physics Setup - CC");
        cc.radius          = k_Radius;
        cc.height          = k_Height;
        cc.skinWidth       = k_SkinWidth;
        cc.stepOffset      = k_StepOffset;
        cc.slopeLimit      = k_SlopeLimit;
        cc.minMoveDistance = k_MinMoveDistance;
        // Center is managed at runtime by CharacterControllerDriver (tracks camera height).

        // 3. Add CharacterControllerDriver on the XROrigin root.
        var ccd = origin.GetComponent<CharacterControllerDriver>();
        if (ccd == null)
        {
            ccd = Undo.AddComponent<CharacterControllerDriver>(origin.gameObject);
            Debug.Log("[XROriginPhysicsSetup] Added CharacterControllerDriver to XR Origin.");
        }

        // Link the first ContinuousMoveProviderBase found in the scene.
        if (ccd.locomotionProvider == null)
        {
            var provider = FindFirstObjectByType<ContinuousMoveProviderBase>();
            if (provider != null)
            {
                Undo.RecordObject(ccd, "XR Origin Physics Setup - CCD");
                ccd.locomotionProvider = provider;
                Debug.Log($"[XROriginPhysicsSetup] Linked CharacterControllerDriver to '{provider.name}'.");
            }
            else
            {
                Debug.LogWarning("[XROriginPhysicsSetup] No ContinuousMoveProviderBase found in scene. " +
                    "Open the XR Origin prefab or add an ActionBasedContinuousMoveProvider to the scene, " +
                    "then re-run setup.");
            }
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        Debug.Log("[XROriginPhysicsSetup] Setup complete. Save the scene (Ctrl+S).");
        EditorUtility.DisplayDialog("XR Origin Physics Setup",
            "Done!\n\n" +
            "CharacterController added to Rig.\n" +
            "CharacterControllerDriver added to XR Origin.\n\n" +
            "CharacterControllerDriver will resize the capsule at runtime " +
            "to match the player's real height (HMD position).\n\n" +
            "Save the scene: Ctrl+S",
            "OK");
    }

    private static void AddXROriginToScene()
    {
        // Open the GameObject > XR menu equivalent via built-in prefab.
        // The XRI package registers this in the GameObject menu.
        // We invoke it via EditorApplication so the user can confirm.
        EditorUtility.DisplayDialog(
            "Add XR Origin",
            "In the Unity menu bar go to:\n" +
            "  GameObject → XR → XR Origin (Action-based)\n\n" +
            "This adds the full XR Origin rig with controllers, hands, and locomotion providers. " +
            "Then re-open this window to configure physics.",
            "OK");
    }
}
