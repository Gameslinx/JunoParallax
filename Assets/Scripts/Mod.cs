namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Xml;
    using System.Xml.Linq;
    using Assets.Scripts.Flight;
    using Assets.Scripts.Flight.GameView.Planet;
    using Assets.Scripts.Flight.GameView.Planet.Events;
    using Assets.Scripts.Flight.Sim;
    using Assets.Scripts.Terrain;
    using Assets.Scripts.Terrain.CustomData;
    using Assets.Scripts.Terrain.Events;
    using ModApi;
    using ModApi.Common;
    using ModApi.Flight;
    using ModApi.Mods;
    using ModApi.Planet;
    using ModApi.Planet.CustomData;
    using ModApi.Planet.Events;
    using ModApi.Scenes;
    using ModApi.Scenes.Events;
    using ModApi.Settings.Core;
    using ModApi.Settings.Core.Events;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Assertions.Must;
    using UnityEngine.Profiling;
    using static Assets.Scripts.Terrain.MeshDataTerrain;
    using static UnityEngine.Mesh;

    /// <summary>
    /// A singleton object representing this mod that is instantiated and initialize when the mod is loaded.
    /// </summary>
    public class Mod : ModApi.Mods.GameMod
    {
        
        public static Mod ParallaxInstance;

        Shader terrainShader;

        public ComputeShader quadShader;
        public ComputeShader renderShader;

        public Dictionary<Scatter, ScatterRenderer> scatterRenderers = new Dictionary<Scatter, ScatterRenderer>();
        public Dictionary<System.Guid, GameObject> scatterObjects = new Dictionary<Guid, GameObject>();                //Holds the manager GOs for each planet
        public Dictionary<System.Guid, ScatterManager> scatterManagers = new Dictionary<Guid, ScatterManager>();
        public Dictionary<QuadScript, QuadData> quadData = new Dictionary<QuadScript, QuadData>();

        public IQuadSphere currentBody;

        // Memory usage of each compute shader instance in mb
        public float memoryUsagePerComputeShader = 0.4121f;

        float splitDist;
        float[] lodDistances;

        bool quadSphereIsLoading = false;
        bool quadSphereIsUnloading = false;

        private Mod() : base()
        {
        }
        public static Mod Instance { get; } = GetModInstance<Mod>();
        public static string modDataPath = "";
        protected override void OnModInitialized()
        {
            base.OnModInitialized();
        }
        public override void OnModLoaded()
        {
            base.OnModLoaded();
            modDataPath = Path.Combine(Application.persistentDataPath, "Mods", "ParallaxData").Replace("\\", "/");
            string configsPath = modDataPath + "/Configs";
            string shaderBankPath = modDataPath + "/Assets/_Common";
            string settingsPath = modDataPath + "/Assets/_Common";
            TextureLoader.Initialize(modDataPath);

            Profiler.BeginSample("Load Parallax Configs");
            ConfigLoader.LoadShaderBank(shaderBankPath);
            ConfigLoader.LoadSettings(settingsPath);
            ConfigLoader.LoadConfigs(configsPath);
            Profiler.EndSample();

            ParallaxInstance = this;

            PlanetTerrainDataScript.TerrainDataInitializing += OnTerrainDataInitializing;

            Debug.Log("Mod loaded");
            Terrain.QuadScript.CreateQuadCompleted += OnCreateQuadCompleted;
            Terrain.QuadScript.UnloadQuadStarted += OnUnloadQuadStarted;
            Terrain.QuadScript.UnloadQuadCompleted += OnUnloadQuadCompleted;
                //Do this for every scatter, though :)
            Debug.Log("Trying to add event");
            Assets.Scripts.Flight.GameView.Planet.PlanetScript.Initialized += OnPlanetInitialized;
            Debug.Log("Events added");
            terrainShader = Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/Wireframe.shader");

            quadShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Parallax.compute");
            renderShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Cascades.compute");

            Profiler.BeginSample("Initialize shader pool");
            int numShaders = (int)((float)ParallaxSettings.computeShaderMemory / memoryUsagePerComputeShader);
            Debug.Log("Initializing shader pool with " + numShaders + " compute shaders");
            ShaderPool.Initialize(numShaders);
            Profiler.EndSample();

            NumericSetting<float> lodDistance = Game.Instance.QualitySettings.Terrain.LodDistance;
            splitDist = lodDistance;
            lodDistance.Changed += SplitDistChanged;

            QuadScript.CreateQuadStarted += OnCreateQuadStarted;
        }
        public const string keyword = "Parallax Support Scatter (V1)";
        private void OnTerrainDataInitializing(object sender, PlanetTerrainDataEventArgs e)
        {
            Debug.Log("Logging all biomes and subbiomes for planet: " + e.TerrainData.PlanetData.Name);
            foreach (var biome in e.TerrainData.Biomes)
            {
                Debug.Log("Biome: " + biome.name);
                var subBiomes = biome.GetSubBiomes();
                foreach (var subBiome in subBiomes)
                {
                    Debug.Log(" - SubBiome: " + subBiome.Name);
                }
            }
            e.TerrainData.PlanetData.ModKeywords.Add(keyword);
            foreach (ScatterBody body in ConfigLoader.bodies.Values)
            {
                foreach (Scatter scatter in body.scatters.Values)
                {
                    var terrainData = e.TerrainData;
                    var planetData = terrainData.PlanetData;
                    if (planetData.Name == scatter.planetName)
                    {
                        // Add some noise modifiers (and some modifiers to store the noise in custom vertex data)
                        var scatterNoiseXml = XElement.Parse(scatter.ScatterNoiseXml).Elements("Modifier").ToList();
                        terrainData.AddModifiersFromXml(scatterNoiseXml, 0);
                        Dictionary<string, Biome> scatterBiomes = scatter.distribution.biomes;
                        // Loop through all sub biomes and set their custom data (random junk data for testing)
                        foreach (var biome in terrainData.Biomes)
                        {
                            if (scatterBiomes.ContainsKey(biome.name))
                            {
                                Dictionary<string, SubBiome> scatterSubBiomes = scatterBiomes[biome.name].subBiomes;
                                var subBiomes = biome.GetSubBiomes();
                                foreach (var subBiome in subBiomes)
                                {
                                    if (scatterSubBiomes.ContainsKey(subBiome.Name))
                                    {
                                        SubBiome scatterSubBiome = scatterSubBiomes[subBiome.Name];
                                        scatter.SetSubBiomeTerrainData(subBiome.PrimaryData, scatterSubBiome.flatNoiseIntensity);
                                        scatter.SetSubBiomeTerrainData(subBiome.SlopeData, scatterSubBiome.slopeNoiseIntensity);
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
        private void OnPlanetInitialized(object sender, EventArgs e)
        {
            PlanetScriptEventArgs args = (PlanetScriptEventArgs)e;
            Debug.Log("Planet initialized: " + args.PlanetScript.name);
            args.PlanetScript.QuadSphereLoading += OnQuadSphereLoading;
            args.PlanetScript.QuadSphereLoaded += OnQuadSphereLoaded;

            args.PlanetScript.QuadSphereUnloading += OnQuadSphereUnloading;
            args.PlanetScript.QuadSphereUnloaded += OnQuadSphereUnloaded;
        }
        private void OnQuadSphereLoading(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Sphere loading: " + e.Planet.PlanetNode.Name);
            if (!ConfigLoader.bodies.ContainsKey(e.Planet.PlanetNode.Name))
            {
                Debug.Log(" - This is not a Parallax body");
                return;
            }
            quadSphereIsLoading = true;
            // TEMPORARY
            if (scatterObjects.ContainsKey(e.Planet.PlanetData.Id))
            {
                return;
            }

            GameObject managerGO = new GameObject();
            GameObject.DontDestroyOnLoad(managerGO);

            ScatterManager manager = managerGO.AddComponent<ScatterManager>();
            manager.planet = e.Planet;

            ParallaxGUI gui = manager.gameObject.AddComponent<ParallaxGUI>();
            ParallaxGUI.manager = manager;

            Debug.Log("Planet name: " + e.Planet.PlanetNode.Name);
            ScatterBody body = ConfigLoader.bodies[e.Planet.PlanetNode.Name];
            Scatter[] scatters = body.scatters.Values.ToArray();
            activeScatters = scatters;
            Debug.Log("Active scatter count: " + activeScatters.Length);
            foreach (Scatter scatter in scatters)
            {
                //scatter.Register();
                ScatterRenderer renderer = managerGO.AddComponent<ScatterRenderer>();
                manager.scatterRenderers.Add(renderer);
                renderer.scatter = scatter;
                renderer.manager = manager;
                renderer.Initialize();
                scatterRenderers.Add(scatter, renderer);
            }
            Utils utils = managerGO.AddComponent<Utils>();
            
            scatterObjects.Add(e.Planet.PlanetData.Id, managerGO);
            scatterManagers.Add(e.Planet.PlanetData.Id, manager);
        }
        private void OnQuadSphereUnloading(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Sphere unloading: " + e.Planet.PlanetNode.Name);
            if (!ConfigLoader.bodies.ContainsKey(e.Planet.PlanetNode.Name))
            {
                Debug.Log(" - This is not a Parallax body");
                return;
            }
            quadSphereIsUnloading = true;
            Guid id = e.Planet.PlanetData.Id;
            if (!scatterObjects.ContainsKey(id))
            {
                return;
            }

            // Clean up every quad - We don't want this handled automatically because data is reinitialized on lower subdivisions, but we know every quad will be destroyed here
            foreach (KeyValuePair<QuadScript, QuadData> qd in quadData)
            {
                qd.Value.Cleanup();
            }
            // We don't want to clear the dictionary because OnUnloadQuadComplete handles this.

            // The scatter manager and components are automatically destroyed here, but we need to remove it from the dictionary
            ScatterBody body = ConfigLoader.bodies[e.Planet.PlanetNode.Name];
            Scatter[] scatters = body.scatters.Values.ToArray();
            foreach (Scatter scatter in scatters)
            {
                scatterRenderers.Remove(scatter);
            }
            GameObject managerGO = scatterObjects[id];

            scatterObjects.Remove(id);
            scatterManagers.Remove(id);
            
            UnityEngine.Object.Destroy(managerGO);
            
        }
        private void OnQuadSphereUnloaded(object sender, PlanetQuadSphereEventArgs e)
        {
            quadSphereIsUnloading = false;
            Debug.Log("Quad sphere unloaded: " + e.Planet.PlanetData.Name);
        }
        public Scatter[] activeScatters = new Scatter[0];   //Scatters that are currently active right now - This holds every scatter on the current planet
        private void OnCreateQuadStarted(object sender, CreateQuadScriptEventArgs e)
        {
            //float[] distributiond = ConfigLoader.bodies["Luna"].scatters["TinyRocks"].GetNoiseData(e.CreateQuadData);
            //
            //var quadDatad = e.CreateQuadData;
            //var meshData = quadDatad.TerrainMeshData.Item;
            //if (meshData.VertexType == typeof(MeshDataTerrain.TerrainVertexBasic))
            //{
            //    var verts = meshData.VerticesBasic;
            //    for (int b = 0; b < verts.Length; ++b)
            //    {
            //        verts[b].Color = new half4((half)(distributiond[b]), (half)(distributiond[b]), (half)(distributiond[b]), (half)1);
            //    }
            //}
            //else
            //{
            //    var verts = meshData.Vertices;
            //    for (int b = 0; b < verts.Length; ++b)
            //    {
            //        verts[b].Color = new half4((half)(distributiond[b]), (half)(distributiond[b]), (half)(distributiond[b]), (half)1);
            //    }
            //}

            //return;

            Profiler.BeginSample("OnCreateQuadStarted (Parallax)");
            CreateQuadData data = e.CreateQuadData;
            ScatterNoise sn;
            for (int i = 0; i < activeScatters.Length; i++)
            {
                sn = new ScatterNoise(activeScatters[i].GetDistributionData(data), activeScatters[i].GetNoiseData(data));
                activeScatters[i].noise.Add(e.Quad, sn);
            }
            QuadData qd = new QuadData(e.Quad);                                     //Change this to iterate through scatters
            quadData.Add(e.Quad, qd);
            Profiler.EndSample();
        }
        private void OnQuadSphereLoaded(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Quad Sphere Loaded: " + e.Planet.PlanetData.Name);
            if (!ConfigLoader.bodies.ContainsKey(e.Planet.PlanetNode.Name))
            {
                Debug.Log(" - This is not a Parallax body");
                return;
            }
            currentBody = e.QuadSphere;
            CalculateNewLODDistances(splitDist);
            QuadSphereScript script = e.QuadSphere as QuadSphereScript;
            GameObject managerGO = scatterObjects[e.Planet.PlanetData.Id];

            if (e.QuadSphere == null) { Debug.Log("Quad sphere is null??"); }
            if (managerGO == null) { Debug.Log("Manager is null??"); }
            if (managerGO.GetComponent<ScatterManager>() == null) { Debug.Log("Manager is null"); }

            managerGO.GetComponent<ScatterManager>().quadSphere = script;
            //if (scatterObjects.ContainsKey(e.Planet.PlanetData.Id))
            //{
            //    Debug.Log("Skipping parenting, planet already has a manager");
            //    return;
            //}
            managerGO.transform.SetParent(e.QuadSphere.Transform);
            quadSphereIsLoading = false;
        }
        private void OnCreateQuadCompleted(object sender, CreateQuadScriptEventArgs e) 
        {
            if (e.Quad.RenderingData.TerrainMesh == null || e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel - 2) 
            {
                return;
            }
            Profiler.BeginSample("OnCreateQuadComplete (Parallax)");
            // Determine if quad has any parents. If it does, we need to clean up the QuadData. Similarly, on quad unload we need to reinitialize the quaddata on its parent if it has any
            // If quad has just subdivided, clean up parent data:
            if (quadData.ContainsKey(e.Quad.Parent) && e.Quad.Parent.Children[0] == e.Quad)
            {
                quadData[e.Quad.Parent].Pause();
            }

            quadData[e.Quad].RegisterEvents();
            quadData[e.Quad].Initialize();
            //QuadData qd = new QuadData(e.Quad);
            //quadData.Add(e.Quad, qd);
            Profiler.EndSample();
        }
        private void OnUnloadQuadStarted(object sender, UnloadQuadScriptEventArgs e)
        {
            // Quad sphere is unloading - all quads will be destroyed
            if (quadSphereIsUnloading) { return; }

            // For some reason, flying over a planet in the planet studio quickly will result in exceptions because the quad is null. Stop this from happening
            if (e.Quad.Parent.Children == null) { return; }
            Profiler.BeginSample("OnUnloadQuadStarted (Parallax)");
            // We need to reinitialize the parent data, should it be contained
            if (quadData.ContainsKey(e.Quad.Parent))
            {
                // We need to check if the quad is the first in the parent children. That way, we can avoid initializing the data 4 times (each quad under a parent will unload together)
                if (e.Quad.Parent.Children[0] == e.Quad)
                {
                    quadData[e.Quad.Parent].Resume();
                }
            }
            Profiler.EndSample();
        }
        private void OnUnloadQuadCompleted(object sender, UnloadQuadScriptEventArgs e)
        {
            Profiler.BeginSample("OnUnloadQuadComplete (Parallax)");
            quadData[e.Quad].Cleanup();
            quadData.Remove(e.Quad);

            for (int i = 0; i < activeScatters.Length; i++)
            {
                activeScatters[i].noise.Remove(e.Quad);
            }
            Profiler.EndSample();
        }
        // Utilities

        private void SplitDistChanged(object sender, SettingChangedEventArgs<float> e)
        {
            Debug.Log("LOD split distance changed");
            splitDist = e.Setting;
            CalculateNewLODDistances(splitDist);
        }
        // A quad will subdivide if within this distance. From this we can determine what subdivision levels a scatter will occupy
        // NOTE: I think this is inaccurate, it reports 2931m on Droo for subdivision but that seems too close...
        // NOTE 2: On further testing it seems this might be correct. If there are instances of scatters being cut off with straight lines, come look at this
        private void CalculateNewLODDistances(float splitDistance)
        {
            if (currentBody == null) { Debug.Log("Current body null"); return; }
            Debug.Log("Quad sphere maximum subdivision: " + currentBody.MaxSubdivisionLevel);
            lodDistances = new float[currentBody.MaxSubdivisionLevel + 1];
            float quadRootSize = 2 * Mathf.PI * (float)currentBody.PlanetData.Radius / 4f;
            for (int i = 0; i <= currentBody.MaxSubdivisionLevel; i++)
            {
                float num = quadRootSize / Mathf.Pow(2.0f, (float)i);
                lodDistances[i] = num * splitDistance;
                Debug.Log("Subdivision level " + i + " requires a distance of " + lodDistances[i]);
            }
            foreach (Scatter scatter in activeScatters)
            {
                float maxRange = scatter.distribution._Range;
                scatter.minimumSubdivision = currentBody.MaxSubdivisionLevel - lodDistances.Where(x => x < maxRange).Count();  //Get number of subdivisionLevels that can contain this scatter
                Debug.Log("Scatter: " + scatter.DisplayName + " has a minimum subdivision of " + scatter.minimumSubdivision);
            }
        }
    }
}