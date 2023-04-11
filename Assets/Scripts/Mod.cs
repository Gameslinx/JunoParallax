namespace Assets.Scripts
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using Assets.Scripts.Flight;
    using Assets.Scripts.Flight.GameView.Planet;
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
            Terrain.QuadSphereScript.CreateQuadDataCompleted += OnCreateQuadDataCompleted;
            Terrain.QuadScript.CreateQuadCompleted += OnCreateQuadCompleted;
            Terrain.QuadScript.UnloadQuadCompleted += OnUnloadQuadCompleted;
            Debug.Log("Trying to add event");
            Game.Instance.SceneManager.SceneLoading += OnSceneLoaded;
            Debug.Log("Events added");
            terrainShader = Instance.ResourceLoader.LoadAsset<Shader>("Assets/Resources/Wireframe.shader");

            quadShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Parallax.compute");
            renderShader = Instance.ResourceLoader.LoadAsset<ComputeShader>("Assets/Scripts/Shaders/Cascades.compute");

            gos = new Dictionary<int, GameObject[]>();
        }
        private void OnSceneLoaded(object sender, SceneEventArgs e)
        {
            if (e.Scene == SceneNames.Flight)
            {
                Debug.Log("Scene is flight, evaluating bools:");
                Debug.Log("1" + (Game.Instance.FlightScene == null));
                Debug.Log("2" + (Game.Instance.FlightScene.ViewManager == null));
                Debug.Log("3" + (Game.Instance.FlightScene.ViewManager.GameView == null));
                Debug.Log("4" + (Game.Instance.FlightScene.ViewManager.GameView.Planet == null));
                Debug.Log("Adding scene event...");
                Game.Instance.FlightScene.ViewManager.GameView.Planet.QuadSphereLoading += OnQuadSphereLoading;
                Debug.Log("Done");
                
            }
        }
        private void OnQuadSphereLoading(object sender, PlanetQuadSphereEventArgs e)
        {
            Debug.Log("Sphere loading...");
            managerGO = new GameObject();
            managerGO.transform.SetParent(e.QuadSphere.Transform);                      //Will be disabled whenever parent is disabled

            ScatterManager manager = managerGO.AddComponent<ScatterManager>();          //Once per planet
            ScatterRenderer renderer = managerGO.AddComponent<ScatterRenderer>();       //Ofc, do this for all renderers

            manager.scatterRenderer = renderer;                                         //Ofc, do this for all renderers too

            renderer.scatter = dummyScatter;
            renderer.transform.parent = e.QuadSphere.Transform;

            scatterManagers.Add(e.QuadSphere.PlanetData.Id, manager);
            scatterRenderers.Add(dummyScatter, renderer);
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
        private void OnCreateQuadDataCompleted(object sender, CreateQuadDataEventArgs e)
        {
            
            //string str = UnityEngine.StackTraceUtility.ExtractStackTrace();
            //Debug.Log("Are we on the main thread? " + currentThread.Equals(Thread.CurrentThread));
            //Debug.Log("Stack trace: " + str);
            //if (e.Data.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel)
            //{
            //    return;
            //}
            //
            //GameObject newO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //newO.SetActive(true);
            //newO.transform.position = e.Data.Position.ToVector3();
            //newO.transform.localScale = new Vector3(25, 25, 25);

            //Debug.Log("at 1");
            //TerrainVertex[] verts = e.Data.TerrainMeshData.Item.Vertices;
            //Debug.Log("at 2");
            //GameObject[] objects = new GameObject[verts.Length];
            //Debug.Log("at 3");
            //for (int i = 0; i < verts.Length; i++)
            //{
            //    Debug.Log("loop: " + i);
            //    objects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
            //    objects[i].SetActive(true);
            //    objects[i].transform.position = verts[i].Position;
            //    float4 pos = new float4(verts[i].Position, 1);
            //    float4x4 a = e.Data.Matrix.ToMatrix4x4();
            //    float4 world = Unity.Mathematics.math.mul(a, pos);
            //    objects[i].transform.position = new float3(world.x, world.y, world.z);
            //}
            //Debug.Log("at 4");
            //quadData.Add(e.Data.TerrainMeshData.Item, objects);
        }
        Dictionary<int, GameObject[]> gos;
        private void OnCreateQuadCompleted(object sender, CreateQuadScriptEventArgs e) 
        {
            Debug.Log("Setting shader on quad");
            
            e.Quad.RenderingData.TerrainMaterial = new Material(terrainShader);
            if (e.Quad.RenderingData.TerrainMesh == null || e.Quad.SubdivisionLevel < e.QuadSphere.MaxSubdivisionLevel) 
            {
                return;
            }
            QuadData qd = new QuadData(e.Quad);
            quadData.Add(e.Quad, qd);


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
        }
        private void OnUnloadQuadCompleted(object sender, UnloadQuadScriptEventArgs e)
        {
            if (quadData.ContainsKey(e.Quad))
            {
                quadData[e.Quad].Cleanup();
                quadData.Remove(e.Quad);
            }

            //if (gos.ContainsKey(e.Quad.Id))
            //{
            //    foreach (GameObject go in gos[e.Quad.Id])
            //    {
            //        UnityEngine.Object.Destroy(go);
            //    }
            //    gos.Remove(e.Quad.Id);
            //}
        }
    }
}