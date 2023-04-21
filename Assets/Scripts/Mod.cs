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
    using Assets.Scripts.Terrain.Events;
    using ModApi;
    using ModApi.Common;
    using ModApi.Flight;
    using ModApi.Mods;
    using ModApi.Planet;
    using ModApi.Planet.Events;
    using ModApi.Scenes;
    using ModApi.Scenes.Events;
    using Unity.Mathematics;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.Assertions.Must;
    using static Assets.Scripts.Terrain.MeshDataTerrain;

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
        public Dictionary<System.Guid, ScatterManager> scatterManagers = new Dictionary<Guid, ScatterManager>();
        public Dictionary<QuadScript, QuadData> quadData = new Dictionary<QuadScript, QuadData>();
        public Scatter dummyScatter;
        public GameObject managerGO;
        private Mod() : base()
        {
        }
        public static Mod Instance { get; } = GetModInstance<Mod>();
        protected override void OnModInitialized()
        {
            base.OnModInitialized();
        }
        public override void OnModLoaded()
        {
            base.OnModLoaded();
            ParallaxInstance = this;

            dummyScatter = new Scatter();   //Change to loop over scatters and such

            Debug.Log("Mod loaded");
            Terrain.QuadScript.CreateQuadCompleted += OnCreateQuadCompleted;
            Terrain.QuadScript.UnloadQuadCompleted += OnUnloadQuadCompleted;
            Debug.Log("Trying to add event");
            Assets.Scripts.Flight.GameView.Planet.PlanetScript.Initialized += OnPlanetInitialized;
            Debug.Log("Events added");
            terrainShader = Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/Wireframe.shader");

            quadShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Parallax.compute");
            renderShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Cascades.compute");
        }
        private void OnPlanetInitialized(object sender, EventArgs e)
        {
            PlanetScriptEventArgs args = (PlanetScriptEventArgs)e;
            Debug.Log("Planet initialized: " + args.PlanetScript.name);
            args.PlanetScript.QuadSphereLoading += OnQuadSphereLoading;
            args.PlanetScript.QuadSphereLoaded += OnQuadSphereLoaded;
        }
        private void OnQuadSphereLoading(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Sphere loading: " + e.Planet.PlanetNode.Name);
            managerGO = new GameObject();
            
            

                                  //Will be disabled whenever parent is disabled
            ScatterManager manager = managerGO.AddComponent<ScatterManager>();          //Once per planet
            ScatterRenderer renderer = managerGO.AddComponent<ScatterRenderer>();       //Ofc, do this for all renderers
            Utils utils = managerGO.AddComponent<Utils>();
            manager.scatterRenderer = renderer;                                         //Ofc, do this for all renderers too
            renderer.scatter = dummyScatter;
            scatterManagers.Add(e.Planet.PlanetData.Id, manager);
            scatterRenderers.Add(dummyScatter, renderer);

            renderer.Initialize();
        }

        private void OnQuadSphereLoaded(object sender, PlanetQuadSphereEventArgs e)
        {
            managerGO.transform.SetParent(e.QuadSphere.Transform);
            QuadSphereScript script = e.QuadSphere as QuadSphereScript;
            managerGO.GetComponent<ScatterManager>().quadSphere = script;
            Debug.Log("Script loaded, frame position is " + script.FramePosition);
        }
        private void OnFlightInitialized(IFlightScene scene)
        {
            //scene.ViewManager.GameView.Planet.QuadSphere.FrameStateRecalculated += OnFrameStateRecalculated;
            Debug.Log("Flight initialized");
            
            Debug.Log("Event added");
        }
        private void OnFrameStateRecalculated(object sender, QuadSphereFrameStateRecalculatedEventArgs e)
        {
          
        }
        private void OnCreateQuadCompleted(object sender, CreateQuadScriptEventArgs e) 
        {
            Debug.Log("Setting shader on quad");
            
            
            if (e.Quad.RenderingData.TerrainMesh == null || e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel) 
            {
                return;
            }
            Material terrainMaterial = new Material(terrainShader);
            terrainMaterial.SetColor("_Color", new Color(1, 1, 1, 0.2f));
            e.Quad.RenderingData.TerrainMaterial = terrainMaterial;

            //AdvancedSubdivision asd = new AdvancedSubdivision(e.Quad);

            QuadData qd = new QuadData(e.Quad);
            quadData.Add(e.Quad, qd);

            //GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            //go.transform.SetParent(e.Quad.QuadSphere.transform, false);
            //go.transform.localPosition = e.Quad.PlanetPosition.ToVector3();
            //go.transform.localScale = Vector3.one * 50;
            
            //
            //Vector3[] verts = e.Quad.RenderingData.TerrainMesh.vertices;
            //GameObject[] gameObjects = new GameObject[verts.Length];
            //
            //for (int i = 0; i < verts.Length; i++)
            //{
            //    GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //    //go.transform.SetParent(e.Quad.QuadSphere.transform, false);
            //    go.transform.localScale = Vector3.one * 5f;
            //    go.transform.position = GetQuadToWorldMatrix(e.Quad).MultiplyPoint(verts[i]);//verts[i] + e.Quad.PlanetPosition.ToVector3();
            //}
            //gos.Add(e.Quad.Id, gameObjects);

            return;
            

        }
        private void OnUnloadQuadCompleted(object sender, UnloadQuadScriptEventArgs e)
        {
            if (quadData.ContainsKey(e.Quad))
            {
                quadData[e.Quad].Cleanup();
                quadData.Remove(e.Quad);
            }
        }
    }
}