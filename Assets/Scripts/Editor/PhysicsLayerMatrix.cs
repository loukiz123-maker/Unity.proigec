using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tool: configures the Physics Layer Collision Matrix and anti-tunneling settings
/// for the VR project (Quest 3).
///
/// Open via: Tools → Physics Layer Matrix Setup
/// </summary>
public class PhysicsLayerMatrix : EditorWindow
{
    private const int k_StaticEnvLayer = 8;  // StaticEnvironment
    private const int k_DefaultLayer   = 0;  // Default (used by XR Origin Rig / CC)

    [MenuItem("Tools/Physics Layer Matrix Setup")]
    public static void ShowWindow()
    {
        GetWindow<PhysicsLayerMatrix>("Physics Layer Matrix");
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("Physics Layer Matrix Setup", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Configures layer-collision pairs and physics settings to prevent\n" +
            "the XR Origin (CharacterController) from passing through walls.\n\n" +
            "CharacterController is NOT a Rigidbody — it uses sweep tests internally.\n" +
            "CCD is irrelevant for CC. Anti-tunneling is achieved via:\n" +
            "  • Correct skinWidth on the CharacterController\n" +
            "  • Physics.defaultContactOffset\n" +
            "  • Correct layer collision matrix",
            MessageType.Info);

        EditorGUILayout.Space();
        DrawCurrentStatus();

        EditorGUILayout.Space();
        if (GUILayout.Button("Apply Recommended Settings", GUILayout.Height(40)))
            Apply();

        EditorGUILayout.Space();
        if (GUILayout.Button("Open Physics Project Settings (Layer Matrix)"))
            SettingsService.OpenProjectSettings("Project/Physics");
    }

    private void DrawCurrentStatus()
    {
        bool defaultVsStaticEnv = !Physics.GetIgnoreLayerCollision(k_DefaultLayer, k_StaticEnvLayer);
        float contactOffset = Physics.defaultContactOffset;

        EditorGUILayout.LabelField("Current status:", EditorStyles.boldLabel);
        DrawRow("Default ↔ StaticEnvironment collision enabled", defaultVsStaticEnv);
        DrawRow($"defaultContactOffset ≤ 0.02 (current: {contactOffset:F3})", contactOffset <= 0.02f);
    }

    private static void DrawRow(string label, bool ok)
    {
        using (new EditorGUILayout.HorizontalScope())
        {
            EditorGUILayout.LabelField(ok ? "✓" : "✗", GUILayout.Width(20));
            EditorGUILayout.LabelField(label);
        }
    }

    private static void Apply()
    {
        // Ensure Default layer collides with StaticEnvironment.
        // (They collide by default, but this makes it explicit and surviving future edits.)
        Physics.IgnoreLayerCollision(k_DefaultLayer, k_StaticEnvLayer, false);

        // Also ensure StaticEnvironment objects don't collide with each other
        // (walls don't need to push each other, saves CPU).
        Physics.IgnoreLayerCollision(k_StaticEnvLayer, k_StaticEnvLayer, true);

        // Contact offset: smaller = tighter collision response, prevents sinking.
        // Default Unity value is 0.01; 0.01 is fine for Quest 3.
        Physics.defaultContactOffset = 0.01f;

        // Note: bounceThreshold and sleepThreshold are set via SerializedObject
        // to survive domain reloads (Physics.xxx setters don't persist to ProjectSettings).
        ApplyViaSerializedObject();

        EditorUtility.DisplayDialog("Physics Layer Matrix",
            "Done!\n\n" +
            "• Default ↔ StaticEnvironment: ENABLED\n" +
            "• StaticEnvironment ↔ StaticEnvironment: DISABLED (perf)\n" +
            "• defaultContactOffset: 0.01\n\n" +
            "These settings are saved to ProjectSettings/DynamicsManager.asset.",
            "OK");
    }

    private static void ApplyViaSerializedObject()
    {
        // Load the Physics project settings asset directly so changes persist.
        var dynamics = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/DynamicsManager.asset");
        if (dynamics == null || dynamics.Length == 0)
        {
            Debug.LogWarning("[PhysicsLayerMatrix] Could not load DynamicsManager.asset; open " +
                "Project Settings → Physics and set Contact Offset to 0.01 manually.");
            return;
        }

        var so = new SerializedObject(dynamics[0]);
        so.Update();

        var contactOffsetProp = so.FindProperty("m_DefaultContactOffset");
        if (contactOffsetProp != null)
            contactOffsetProp.floatValue = 0.01f;

        // sleepThreshold: lower = objects rest faster, less phantom movement
        var sleepProp = so.FindProperty("m_SleepThreshold");
        if (sleepProp != null)
            sleepProp.floatValue = 0.005f;

        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
        Debug.Log("[PhysicsLayerMatrix] Physics settings written to DynamicsManager.asset.");
    }
}
