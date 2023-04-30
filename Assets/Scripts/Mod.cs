namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Assets.Scripts.Flight;
    using Assets.Scripts.Flight.GameView.Planet;
    using Assets.Scripts.Flight.GameView.Planet.Events;
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
        public Scatter dummyScatter { get; } = new Scatter("DummyScatter", "Dummy Cube Scatter");
        protected override void OnModInitialized()
        {
            base.OnModInitialized();
        }
        public override void OnModLoaded()
        {
            base.OnModLoaded();
            ParallaxInstance = this;

            dummyScatter.Register();

            Debug.Log("Mod loaded");
            Terrain.QuadScript.CreateQuadCompleted += OnCreateQuadCompleted;
            Terrain.QuadScript.UnloadQuadCompleted += OnUnloadQuadCompleted;
            Debug.Log("Trying to add event");
            Assets.Scripts.Flight.GameView.Planet.PlanetScript.Initialized += OnPlanetInitialized;
            Debug.Log("Events added");
            terrainShader = Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/Wireframe.shader");

            quadShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Parallax.compute");
            renderShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Cascades.compute");

            QuadScript.CreateQuadStarted += OnCreateQuadStarted;
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
                                                                                        //Will be disabled whenever parent is disabled
            ScatterManager manager = managerGO.AddComponent<ScatterManager>();          //Once per planet
            ScatterRenderer renderer = managerGO.AddComponent<ScatterRenderer>();       //Ofc, do this for all renderers
            Utils utils = managerGO.AddComponent<Utils>();
            manager.scatterRenderer = renderer;                                         //Ofc, do this for all renderers too
            renderer.scatter = dummyScatter;
            scatterObjects.Add(e.Planet.PlanetData.Id, managerGO);
            scatterManagers.Add(e.Planet.PlanetData.Id, manager);
            scatterRenderers.Add(dummyScatter, renderer);

            renderer.Initialize();
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

            scatterObjects.Remove(id);
            scatterManagers.Remove(id);
            scatterRenderers.Remove(dummyScatter);
        }
        private void OnCreateQuadStarted(object sender, CreateQuadScriptEventArgs e)
        {
            //if (e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel)
            //{
            //    return;
            //}
            CreateQuadData data = e.CreateQuadData;
            float[] dummyNoise = dummyScatter.GetNoiseData(data);
            float[] dummyDistribution = dummyScatter.GetDistributionData(data);

            ScatterNoise sn = new ScatterNoise(dummyDistribution.Clone() as float[], dummyNoise.Clone() as float[]);
            dummyScatter.noise.Add(e.Quad, sn);

            QuadData qd = new QuadData(e.Quad);                                     //Change this to iterate through scatters
            
            quadData.Add(e.Quad, qd);
            MeshDataTerrain meshData = e.CreateQuadData.TerrainMeshData.Item;
            if (meshData.VertexType == typeof(MeshDataTerrain.TerrainVertexBasic))
            {
                var verts = meshData.VerticesBasic;
                for (int i = 0; i < verts.Length; ++i)
                {
                    half col = (half)dummyNoise[i];
                    verts[i].Color = new half4(col, col, col, (half)1);
                }
            }
            else
            {
                var verts = meshData.Vertices;
                for (int i = 0; i < verts.Length; ++i)
                {
                    half col = (half)dummyNoise[i];
                    verts[i].Color = new half4(col, col, col, (half)1);
                }
            }

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
            dummyScatter.noise.Remove(e.Quad);
        }
    }
}