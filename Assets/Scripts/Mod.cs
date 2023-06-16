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
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Assertions.Must;
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
            TextureLoader.Initialize(modDataPath);

            ConfigLoader.LoadShaderBank(shaderBankPath);
            ConfigLoader.LoadConfigs(configsPath);

            ParallaxInstance = this;

            PlanetTerrainDataScript.TerrainDataInitializing += OnTerrainDataInitializing;

            Debug.Log("Mod loaded");
            Terrain.QuadScript.CreateQuadCompleted += OnCreateQuadCompleted;
            Terrain.QuadScript.UnloadQuadCompleted += OnUnloadQuadCompleted;
                //Do this for every scatter, though :)
            Debug.Log("Trying to add event");
            Assets.Scripts.Flight.GameView.Planet.PlanetScript.Initialized += OnPlanetInitialized;
            Debug.Log("Events added");
            terrainShader = Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/Wireframe.shader");

            quadShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Parallax.compute");
            renderShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Cascades.compute");

            QuadScript.CreateQuadStarted += OnCreateQuadStarted;
        }
        public const string keyword = "Parallax Support Scatter (V1)";
        private void OnTerrainDataInitializing(object sender, PlanetTerrainDataEventArgs e)
        {
            if (e.TerrainData.PlanetData.ModKeywords.Contains(keyword))
            {
                return;
            }
            e.TerrainData.PlanetData.ModKeywords.Add(keyword);
            foreach (ScatterBody body in ConfigLoader.bodies.Values)
            {
                foreach (Scatter scatter in body.scatters.Values)
                {
                    var terrainData = e.TerrainData;
                    var planetData = terrainData.PlanetData;
                    Debug.Log("Planet data name: " + planetData.Name);
                    //if (planetData.Author == "Jundroo" || planetData.Author == "NathanMikeska")
                    if (planetData.Name == scatter.planetName)
                    {

                            // Add some noise modifiers (and some modifiers to store the noise in custom vertex data)
                            var scatterNoiseXml = XElement.Parse(scatter.ScatterNoiseXml).Elements("Modifier").ToList();
                            terrainData.AddModifiersFromXml(scatterNoiseXml, 0);

                            // Loop through all sub biomes and set their custom data (random junk data for testing)
                            foreach (var biome in terrainData.Biomes)
                            {
                                var subBiomes = biome.GetSubBiomes();
                                foreach (var subBiome in subBiomes)
                                {
                                    Debug.Log("Setting sub biome data");
                                    scatter.SetSubBiomeTerrainData(subBiome.PrimaryData, 1f);
                                    scatter.SetSubBiomeTerrainData(subBiome.SlopeData, 1f);
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
        }
        private void OnQuadSphereLoading(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Sphere loading: " + e.Planet.PlanetNode.Name);
            GameObject managerGO = new GameObject();
            GameObject.DontDestroyOnLoad(managerGO);

            ScatterManager manager = managerGO.AddComponent<ScatterManager>();
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
            Guid id = e.Planet.PlanetData.Id;
            if (!scatterObjects.ContainsKey(id))
            {
                return;
            }
            //The scatter manager and components are automatically destroyed here, but we need to remove it from the dictionary
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
        public Scatter[] activeScatters;   //Scatters that are currently active right now - This holds every scatter on the current planet
        private void OnCreateQuadStarted(object sender, CreateQuadScriptEventArgs e)
        {
            //if (e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel)
            //{
            //    return;
            //}
            CreateQuadData data = e.CreateQuadData;

            for (int i = 0; i < activeScatters.Length; i++)
            {
                float[] dummyNoise = activeScatters[i].GetNoiseData(data);
                float[] dummyDistribution = activeScatters[i].GetDistributionData(data);

                ScatterNoise sn = new ScatterNoise(dummyDistribution, dummyNoise);
                activeScatters[i].noise.Add(e.Quad, sn);
            }
            
            QuadData qd = new QuadData(e.Quad);                                     //Change this to iterate through scatters
            
            quadData.Add(e.Quad, qd);


            //if (activeScatters.Length > 0)
            //{
            //    float[] dummyNoise = activeScatters[0].GetNoiseData(data);
            //    float[] dummyDistribution = activeScatters[0].GetDistributionData(data);
            //    Material mat = new Material(ParallaxInstance.ResourceLoader.LoadAsset<Shader>("Assets/Scripts/Shaders/ShowColor.shader"));
            //    e.Quad.RenderingData.TerrainMaterial = mat;
            //    if (data.TerrainMeshData.Item.VertexType == typeof(MeshDataTerrain.TerrainVertex))
            //    {
            //        var verts = data.TerrainMeshData.Item.Vertices;
            //        for (int i = 0; i < verts.Length; i++)
            //        {
            //            verts[i].Color = new half4((half)dummyNoise[i], (half)dummyNoise[i], (half)dummyDistribution[i], (half)1);
            //        }
            //    }
            //    else
            //    {
            //        var verts = data.TerrainMeshData.Item.VerticesBasic;
            //        for (int i = 0; i < verts.Length; i++)
            //        {
            //            verts[i].Color = new half4((half)dummyNoise[i], (half)dummyNoise[i], (half)dummyDistribution[i], (half)1);
            //        }
            //    }
            //}

        }
        private void OnQuadSphereLoaded(object sender, PlanetQuadSphereEventArgs e)
        {
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
        }
        private void OnCreateQuadCompleted(object sender, CreateQuadScriptEventArgs e) 
        {
            if (e.Quad.RenderingData.TerrainMesh == null || e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel) 
            {
                return;
            }
            QuadData qd = quadData[e.Quad];
            qd.RegisterEvents();
            qd.Initialize();
            //QuadData qd = new QuadData(e.Quad);
            //quadData.Add(e.Quad, qd);
        }
        private void OnUnloadQuadCompleted(object sender, UnloadQuadScriptEventArgs e)
        {
            quadData[e.Quad].Cleanup();
            quadData.Remove(e.Quad);

            for (int i = 0; i < activeScatters.Length; i++)
            {
                activeScatters[i].noise.Remove(e.Quad);
            }
        }
    }
}