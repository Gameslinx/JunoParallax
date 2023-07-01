using Assets.Scripts;
using Assets.Scripts.Terrain;
using ModApi.Common;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using static ModApi.Utilities;

enum ChangeType
{
    Distribution,
    Material,
    Renderer
}
public class ParallaxGUI : MonoBehaviour
{
    public static ScatterManager manager;
    private static Rect window = new Rect(100, 100, 450, 200);
    static Scatter currentScatter;
    static int currentScatterIndex = 0;
    static bool showDistribution = false;
    static bool showLOD1 = false;
    static bool showLOD2 = false;
    static bool showMaterial = false;

    static bool anyValueHasChanged = false;
    static ChangeType currentChangeType;
    static bool visible = false;
    void Start()
    {

    }

    void Update()
    {
        bool flag = UnityEngine.Input.GetKey(KeyCode.LeftAlt) && UnityEngine.Input.GetKeyDown(KeyCode.P); 
        if (flag)
        {
            visible = !visible;
        }
        if (anyValueHasChanged)
        {
            anyValueHasChanged = false;
            if (currentChangeType == ChangeType.Distribution && !currentScatter.inherits)
            {
                foreach (KeyValuePair<QuadScript, QuadData> data in Mod.ParallaxInstance.quadData) 
                { 
                    foreach (ScatterData scatterData in data.Value.data)
                    {
                        if (scatterData.scatter.DisplayName == currentScatter.DisplayName)
                        {
                            scatterData.Cleanup();
                            scatterData.Start();
                        }
                    }
                }
            }
            if (currentChangeType == ChangeType.Material)
            {

            }
            if (currentChangeType == ChangeType.Renderer)
            {
                ScatterRenderer renderer = manager.scatterRenderers.Where(x => x.scatter.DisplayName == currentScatter.DisplayName).First();
                renderer.scatter.maxObjectsToRender = currentScatter.maxObjectsToRender;

                TextureLoader.UnloadAll();
                renderer.Cleanup();
                renderer.Initialize();

                foreach (KeyValuePair<QuadScript, QuadData> data in Mod.ParallaxInstance.quadData)
                {
                    foreach (ScatterData scatterData in data.Value.data)
                    {
                        if (scatterData.scatter.DisplayName == currentScatter.DisplayName)
                        {
                            scatterData.lod0 = renderer.lod0;
                            scatterData.lod1 = renderer.lod1;
                            scatterData.lod2 = renderer.lod2;
                        }
                    }
                }

                //renderer.lod0out.Release();
                //renderer.lod1out.Release();
                //renderer.lod2out.Release();
                //
                //renderer.lod0out = new ComputeBuffer(renderer.scatter.maxObjectsToRender, TransformData.Size(), ComputeBufferType.Append);
                //renderer.lod1out = new ComputeBuffer(renderer.scatter.maxObjectsToRender, TransformData.Size(), ComputeBufferType.Append);
                //renderer.lod2out = new ComputeBuffer(renderer.scatter.maxObjectsToRender, TransformData.Size(), ComputeBufferType.Append);
                //
                //renderer.materialLOD0.SetBuffer("_Properties", renderer.lod0out);
                //renderer.materialLOD1.SetBuffer("_Properties", renderer.lod1out);
                //renderer.materialLOD2.SetBuffer("_Properties", renderer.lod2out);
                //
                //renderer.shader.SetBuffer(renderer.lod0kernel, "LOD0OUT", renderer.lod0out);
                //renderer.shader.SetBuffer(renderer.lod1kernel, "LOD10OUT", renderer.lod1out);
                //renderer.shader.SetBuffer(renderer.lod2kernel, "LOD2OUT", renderer.lod2out);
            }
        }
    }
    private void OnGUI()
    {
        if (!visible) { return; }
        window = GUILayout.Window(GetInstanceID(), window, DrawWindow, "Parallax Configurator");
    }
    static void DrawWindow(int windowID)
    {
        GUILayout.BeginVertical();

        GUIStyle alignment = UnityEngine.GUI.skin.GetStyle("Label");
        alignment.alignment = TextAnchor.MiddleCenter;

        GUILayout.Label("Currently displaying scatter: " + Mod.ParallaxInstance.activeScatters[currentScatterIndex].DisplayName);
        GUILayout.BeginHorizontal();

        // Advance previous scatter
        if (GUILayout.Button("Previous Scatter"))
        {
            currentScatterIndex--;
            if (currentScatterIndex < 0)
            {
                currentScatterIndex = Mod.ParallaxInstance.activeScatters.Length - 1;
            }
        }

        // Advance next scatter
        if (GUILayout.Button("Next Scatter"))
        {
            currentScatterIndex++;
            if (currentScatterIndex == Mod.ParallaxInstance.activeScatters.Length)
            {
                currentScatterIndex = 0;
            }
        }
        
        GUILayout.EndHorizontal();

        currentScatter = Mod.ParallaxInstance.activeScatters[currentScatterIndex];

        if (GUILayout.Button("Show Material Settings"))
        {
            showMaterial = !showMaterial;
        }
        if (showMaterial)
        {
            ShowMaterial(currentScatter.material, MaterialType.LOD0);
        }

        if (GUILayout.Button("Show Distribution Settings"))
        {
            showDistribution = !showDistribution;
        }
        if (showDistribution)
        {
            currentScatter.distribution._PopulationMultiplier = TextAreaLabelInt("Population Multiplier", currentScatter.distribution._PopulationMultiplier, 1, 200, ChangeType.Distribution);
            currentScatter.distribution._SpawnChance = TextAreaLabelFloat("Spawn Chance", currentScatter.distribution._SpawnChance, ChangeType.Distribution);
            currentScatter.distribution._Range = TextAreaLabelFloat("Max Range", currentScatter.distribution._Range, ChangeType.Distribution);
            currentScatter.distribution._Coverage = TextAreaLabelFloat("Coverage", currentScatter.distribution._Coverage, ChangeType.Distribution);
            currentScatter.distribution._MinScale = TextAreaLabelVector("Min Scale", currentScatter.distribution._MinScale, ChangeType.Distribution);
            currentScatter.distribution._MaxScale = TextAreaLabelVector("Max Scale", currentScatter.distribution._MaxScale, ChangeType.Distribution);
            currentScatter.distribution._SizeJitterAmount = TextAreaLabelFloat("Size Randomness", currentScatter.distribution._SizeJitterAmount, ChangeType.Distribution);
            currentScatter.distribution._MinAltitude = TextAreaLabelFloat("Min Altitude", currentScatter.distribution._MinAltitude, ChangeType.Distribution);
            currentScatter.distribution._MaxAltitude = TextAreaLabelFloat("Max Altitude", currentScatter.distribution._MaxAltitude, ChangeType.Distribution);
            if (GUILayout.Button("Show LOD 1"))
            {
                showLOD1 = !showLOD1;
            }
            if (showLOD1)
            {
                currentScatter.distribution.lod0.distance = TextAreaLabelFloat("Distance", currentScatter.distribution.lod0.distance, ChangeType.Distribution);
                GUILayout.Label("-------------------");
                ShowMaterial(currentScatter.distribution.lod0.material, MaterialType.LOD1);
            }
            if (GUILayout.Button("Show LOD 2"))
            {
                showLOD2 = !showLOD2;
            }
            if (showLOD2)
            {
                currentScatter.distribution.lod1.distance = TextAreaLabelFloat("Distance", currentScatter.distribution.lod1.distance, ChangeType.Distribution);
                GUILayout.Label("-------------------");
                ShowMaterial(currentScatter.distribution.lod1.material, MaterialType.LOD2);
            }
        }

        // Debug

        currentScatter.maxObjectsToRender = (int)TextAreaLabelSlider("[DEBUG] Max Objects to Render", currentScatter.maxObjectsToRender, 1, 100000, ChangeType.Renderer);

        GUILayout.EndVertical();
        UnityEngine.GUI.DragWindow();
    }
    enum MaterialType
    {
        LOD0,
        LOD1,
        LOD2
    }
    static void ShowMaterial(ScatterMaterial material, MaterialType type)
    {
        if (material._Shader == null) { return; }

        Material mat;
        ScatterRenderer renderer = manager.scatterRenderers.Where(x => x.scatter.DisplayName == currentScatter.DisplayName).First();
        if (type == MaterialType.LOD0)
        {
            mat = renderer.materialLOD0;
        }
        else if (type == MaterialType.LOD1)
        {
            mat = renderer.materialLOD1;
        }
        else
        {
            mat = renderer.materialLOD2;
        }

        string[] floatKeys = material._Shader.floats.Keys.ToArray();
        string[] vectorKeys = material._Shader.vectors.Keys.ToArray();
        string[] colorKeys = material._Shader.colors.Keys.ToArray();

        for (int i = 0; i < floatKeys.Length; i++)
        {
            string key = floatKeys[i];
            float value = material._Shader.floats[key];
            material._Shader.floats[key] = TextAreaLabelFloat(key, value, ChangeType.Material);
            mat.SetFloat(key, material._Shader.floats[key]);
        }

        for (int i = 0; i < vectorKeys.Length; i++)
        {
            string key = vectorKeys[i];
            Vector3 value = material._Shader.vectors[key];
            material._Shader.vectors[key] = TextAreaLabelVector(key, value, ChangeType.Material);
            mat.SetVector(key, material._Shader.vectors[key]);
        }

        for (int i = 0; i < colorKeys.Length; i++)
        {
            string key = colorKeys[i];
            Color value = material._Shader.colors[key];
            Vector3 newvalue = TextAreaLabelVector(key, new Vector3(value.r, value.g, value.b), ChangeType.Material);
            material._Shader.colors[key] = new Color(newvalue.x, newvalue.y, newvalue.z);
            mat.SetColor(key, material._Shader.colors[key]);
        }

        if (type == MaterialType.LOD0)
        {
            currentScatter.material.castShadows = ((int)TextAreaLabelFloat("Cast Shadows", currentScatter.material.castShadows ? 1f : 0f, ChangeType.Material)) == 1;
            renderer.shadowsLOD0 = currentScatter.material.castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else if (type == MaterialType.LOD1)
        {
            currentScatter.distribution.lod0.material.castShadows = ((int)TextAreaLabelFloat("Cast Shadows", currentScatter.distribution.lod0.material.castShadows ? 1f : 0f, ChangeType.Material)) == 1;
            renderer.shadowsLOD1 = currentScatter.distribution.lod0.material.castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }
        else
        {
            currentScatter.distribution.lod1.material.castShadows = ((int)TextAreaLabelFloat("Cast Shadows", currentScatter.distribution.lod1.material.castShadows ? 1f : 0f, ChangeType.Material)) == 1;
            renderer.shadowsLOD2 = currentScatter.distribution.lod1.material.castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        
    }
    //static LOD ShowLOD(LOD lod)
    //{
    //    
    //}
    // Utils
    private static float TextAreaLabelSlider(string label, float value, float min, float max, ChangeType type)
    {
        //GUILayout.BeginHorizontal();
        float newValue = InputFields.SliderField(label, value, min, max);
        //GUILayout.EndHorizontal();
        if (newValue != value)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }

        return newValue;
    }
    private static string TextAreaLabelString(string label, string value, ChangeType type)
    {
        GUILayout.BeginHorizontal();
        string newValue = InputFields.TexField(label, value);
        GUILayout.EndHorizontal();
        if (newValue != value)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }

        return newValue;
    }
    private static int TextAreaLabelInt(string label, int value, int minValue, int maxValue, ChangeType type)
    {
        GUILayout.BeginHorizontal();
        int newValue = InputFields.IntField(label, value, minValue, maxValue);
        if (newValue != value)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }
        GUILayout.EndHorizontal();

        return newValue;
    }
    private static Vector3 TextAreaLabelVector(string label, Vector3 value, ChangeType type)
    {
        Vector3 newValue = InputFields.VectorField(label, value);
        if (newValue != value)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }
        return newValue;
    }
    private static float TextAreaLabelFloat(string label, float value, ChangeType type)
    {
        GUILayout.BeginHorizontal();
        float newValue = InputFields.FloatField(label, value);
        GUILayout.EndHorizontal();

        if (Mathf.Abs(newValue - value) > 0.001)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }

        return newValue;
    }
    private static Color TextAreaLabelColor(string label, Color value, ChangeType type)
    {
        GUILayout.BeginHorizontal();
        Color newValue = InputFields.ColorField(label, value);
        GUILayout.EndHorizontal();

        if (newValue != value)
        {
            anyValueHasChanged = true;
            currentChangeType = type;
        }

        return newValue;
    }
}
