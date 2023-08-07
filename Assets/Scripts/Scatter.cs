using Assets.Scripts.Terrain.CustomData;
using Assets.Scripts.Terrain;
using ModApi.Planet.CustomData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ModApi.Planet;
using ModApi.Planet.Events;
using System.Xml.Linq;
using System.Linq;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Assets.Scripts;
using Unity.Mathematics;

public struct DistributionData
{
    public int _PopulationMultiplier;
    public float _SpawnChance;
    public float _Range;
    public float _SizeJitterAmount;
    public float _Coverage;
    public float _MinAltitude;
    public float _MaxAltitude;
    public uint _AlignToTerrainNormal;
    public float _MaxNormalDeviance;
    public float _Seed;
    public bool _RidgedNoise;
    public Vector3 _MinScale;
    public Vector3 _MaxScale;
    public LOD lod0;
    public LOD lod1;
    public Dictionary<string, Biome> biomes;    //Biome name, noise intensities
}
public struct LOD
{
    public float distance;
    public ScatterMaterial material;
}
public struct Biome
{
    public Dictionary<string, SubBiome> subBiomes;
}
public struct SubBiome
{
    public float flatNoiseIntensity;
    public float slopeNoiseIntensity;
}
public struct ScatterMaterial
{
    public ScatterShader _Shader;
    public bool castShadows;
    public string _Mesh;
}
public class ScatterNoise
{
    public float[] distribution;
    public float[] noise;
    public ScatterNoise(float[] distribution, float[] noise)
    {
        this.distribution = distribution;
        this.noise = noise;
    }
}
// Holds the scatters for each planet
public class ScatterBody
{
    public string bodyName;                                                             //Name of the CelestialBody
    public Dictionary<string, Scatter> scatters = new Dictionary<string, Scatter>();    //String corresponds to the Scatter name
    public ScatterBody(string bodyName)
    {
        this.bodyName = bodyName;
    }
}
public class Scatter
{
    public string planetName = "Droo";
    public string mesh;

    public DistributionData distribution;
    public ScatterMaterial material;

    public bool inherits = false;
    public string inheritsFrom = "";

    public float cullRadius = 0;
    public float cullLimit = 0;

    public float sqrRange = 0;

    // No use adding scatters to quads that will always be out of range. This value is 100 by default, and set by ScatterManager
    public int minimumSubdivision = 100;

    public int maxObjectsToRender = 0;

    public bool sharesNoise = false;
    public Scatter sharesNoiseWith; //If this scatter uses the same noise parameters as another scatter, this will be set to that scatter. Otherwise, it'll point to this scatter

    public IQuadSphere quadSphere;

    public string Id { get; }
    public string DisplayName { get; }
    public string DistributionQuadId { get; }
    public int DistributionQuadIndex { get; private set; }
    public string DistributionVertexId { get; }
    public int DistributionVertexIndex { get; private set; }
    public int DistributionSubBiomeIndex { get; private set; }
    public string NoiseQuadId { get; }
    public int NoiseQuadIndex { get; private set; }
    public string NoiseVertexId { get; }
    public int NoiseVertexIndex { get; private set; }
    public Dictionary<QuadScript, ScatterNoise> noise = new Dictionary<QuadScript, ScatterNoise>();
    // Store all matrix data on ALL MAX LEVEL quads with this scatter on
    // Transforms: GameObject is parented to the quad sphere transform, and the Quad's planetposition is added to this local position
    public LinkedList<RawColliderData> colliderData = new LinkedList<RawColliderData>();
    public List<ColliderData> collidersToAdd = new List<ColliderData>();
    public List<ColliderData> collidersToRemove = new List<ColliderData>();
    public Dictionary<Matrix4x4, GameObject> activeObjects = new Dictionary<Matrix4x4, GameObject>();
    bool isProcessingColliderData = false;
    public ScatterRenderer renderer;
    public Scatter(string id, string displayName)
    {
        distribution = new DistributionData();
        distribution._PopulationMultiplier = 1;
        this.Id = id;
        this.DisplayName = displayName;
        this.DistributionVertexId = $"{id}_Distribution_Vertex";
        this.DistributionQuadId = $"{id}_Distribution_Quad";
        this.NoiseVertexId = $"{id}_Noise_Vertex";
        this.NoiseQuadId = $"{id}_Noise_Quad";
    }
    public void AddColliderData(RawColliderData data)
    {
        colliderData.AddLast(data);
    }
    public void RemoveColliderData(RawColliderData data)
    {
        colliderData.Remove(data);    // Discard the matrix4x4[] out
    }
    public async void ProcessColliderData()
    {
        if (isProcessingColliderData || colliderData.Count == 0) { return; }
        isProcessingColliderData = true;
        // Get number of objects within range
        Quaterniond localRotation = new Quaterniond(colliderData.First.Value.quad.QuadSphere.transform.parent.localRotation);
        Vector3 cameraPosition = Camera.main.transform.position;
        collidersToAdd.Clear();
        collidersToRemove.Clear();
        await Task.Run(() =>
        {
            Matrix4x4 mat = Matrix4x4.identity;

            Matrix4x4 quadToWorld;
            Matrix4x4d m = new Matrix4x4d();

            Vector3 position = Vector3.zero;
            Vector3 vert1;
            Vector3 vert2;
            Vector3 vert3;
            Vector3 avgNormal;
            QuadData qd;
            
            Vector3 quadPosition;
            float quadDiagDist = 0;

            foreach (RawColliderData data in colliderData)
            {
                List<Matrix4x4> toAdd = new List<Matrix4x4>();
                List<Matrix4x4> toRemove = new List<Matrix4x4>();
                // Compute quad to world matrix
                m.SetTRS(data.quad.QuadSphere.FramePosition, localRotation, Vector3.one);
                quadToWorld = m.ToMatrix4x4();
                Vector3d qpos = data.quad.RenderingData.LocalPosition;
                quadToWorld.m03 = (float)((m.m00 * qpos.x) + (m.m01 * qpos.y) + (m.m02 * qpos.z) + m.m03);
                quadToWorld.m13 = (float)((m.m10 * qpos.x) + (m.m11 * qpos.y) + (m.m12 * qpos.z) + m.m13);
                quadToWorld.m23 = (float)((m.m20 * qpos.x) + (m.m21 * qpos.y) + (m.m22 * qpos.z) + m.m23);

                quadPosition.x = quadToWorld.m03;
                quadPosition.y = quadToWorld.m13;
                quadPosition.z = quadToWorld.m23;

                quadDiagDist = (float)Vector3d.Distance(data.quad.RenderingData.BoundingBox.Max, data.quad.RenderingData.BoundingBox.Min);

                if (Vector3.SqrMagnitude(cameraPosition - quadPosition) > quadDiagDist * quadDiagDist * 2.25f)
                {
                    continue;
                }

                qd = Mod.ParallaxInstance.quadData[data.quad];
                for (int i = 0; i < data.data.Length; i++)
                {
                    //mat = Matrix4x4.TRS(data.data[i].pos, Quaternion.Euler(0, data.data[i].rot, 0), data.data[i].scale);
                    //position = mat.GetColumn(3);
                    position = quadToWorld.MultiplyPoint(data.data[i].pos);

                    uint triIndex = data.data[i].index;
                    int index1 = qd.triangleData[triIndex];
                    int index2 = qd.triangleData[triIndex + 1];
                    int index3 = qd.triangleData[triIndex + 2];

                    vert1 = qd.vertexData[index1];
                    vert2 = qd.vertexData[index2];
                    vert3 = qd.vertexData[index3];

                    avgNormal = Vector3.Normalize(Vector3.Cross(vert2 - vert1, vert3 - vert1));
                    if (distribution._AlignToTerrainNormal == 0)
                    {
                        avgNormal = Vector3.Normalize((Vector3)data.quad.SphereNormal);
                    }
                    Utils.GetTRSMatrix(data.data[i].pos, new Vector3(0, data.data[i].rot, 0), data.data[i].scale, avgNormal, ref mat);

                    if (Vector3.SqrMagnitude(cameraPosition - position) < 625)
                    {
                        if (!activeObjects.ContainsKey(mat))
                        {
                            toAdd.Add(mat);
                            
                            activeObjects.Add(mat, null);
                        }
                    }
                    else
                    {
                        if (activeObjects.ContainsKey(mat))
                        {
                            toRemove.Add(mat);
                            
                            //activeObjects.Remove(mat);
                        }
                    }
                }
                collidersToAdd.Add(new ColliderData(data.quad, toAdd));
                collidersToRemove.Add(new ColliderData(data.quad, toRemove));
            }
            
        });

        // Now process the objects in range and add colliders
        GameObject go;
        Vector3 pos = Vector3.zero;
        Vector3 rot1 = Vector3.zero;
        Vector3 rot2 = Vector3.zero;
        Vector3 scale = Vector3.one;
        foreach (ColliderData data in collidersToAdd)
        {
            List<Matrix4x4> matrices = data.data;
            foreach (Matrix4x4 matrix in matrices)
            {
                go = ColliderPool.Retrieve();
                go.transform.SetParent(data.quad.QuadSphere.transform, false);
                pos.x = matrix.m03; pos.y = matrix.m13; pos.z = matrix.m23;

                rot1 = matrix.GetColumn(2);
                rot2 = matrix.GetColumn(1);

                scale.x = new Vector4(matrix.m00, matrix.m10, matrix.m20, matrix.m30).magnitude;
                scale.y = new Vector4(matrix.m01, matrix.m11, matrix.m21, matrix.m31).magnitude;
                scale.z = new Vector4(matrix.m02, matrix.m12, matrix.m22, matrix.m32).magnitude;
                go.transform.localPosition = data.quad.PlanetPosition.ToVector3() + pos;
                go.transform.localRotation = Quaternion.LookRotation(rot1, rot2);
                go.transform.localScale = scale;

                go.GetComponent<MeshFilter>().sharedMesh = renderer.meshLod0;

                go.SetActive(true);

                activeObjects[matrix] = go;
            }
        }
        foreach (ColliderData data in collidersToRemove)
        {
            List<Matrix4x4> matrices = data.data;
            foreach (Matrix4x4 matrix in matrices)
            {
                go = activeObjects[matrix];
                go.SetActive(false);
                ColliderPool.Return(go);
                activeObjects.Remove(matrix);
            }
        }
        isProcessingColliderData = false;
    }
    public void Register()
    {
        // Register custom per-vertex data to hold the noise results
        this.NoiseVertexIndex = CustomPlanetVertexData.Register<CustomPlanetVertexDataFloat>(this.NoiseVertexId);

        // Register per-quad data to store the per-vertex data noise results
        this.NoiseQuadIndex = CustomCreateQuadData.Register<CustomCreateQuadDataFloat>(this.NoiseQuadId, () => new CustomCreateQuadDataFloat(this.NoiseVertexId));

        // Register custom sub-biome data to adjust the distribution values per sub-biome
        this.DistributionSubBiomeIndex = CustomSubBiomeTerrainData.Register<CustomSubBiomeTerrainDataFloatInput, CustomPlanetVertexDataFloat>(
            this.DistributionVertexId, () => new CustomSubBiomeTerrainDataFloatSliderInput(this.DisplayName, "Adjusts the distribution value for this object", 0, 2), true);
        this.DistributionVertexIndex = CustomPlanetVertexData.GetIndex(this.DistributionVertexId);
        //
        // Register per-quad data to store the sub-biome distribution results
        this.DistributionQuadIndex = CustomCreateQuadData.Register<CustomCreateQuadDataFloat>(
            this.DistributionQuadId, () => new CustomCreateQuadDataFloat(this.DistributionVertexId));
    }
    public void Unregister()
    {
        // Not implemented yet
    }
    public float[] GetNoiseData(CreateQuadData quadData)
    {
        return ((CustomCreateQuadDataFloat)quadData.CustomData[this.NoiseQuadIndex]).Values;
    }

    public float[] GetDistributionData(CreateQuadData quadData)
    {
        return ((CustomCreateQuadDataFloat)quadData.CustomData[this.DistributionQuadIndex]).Values;
    }

    public void SetSubBiomeTerrainData(SubBiomeTerrainData subBiomeTerrainData, float value)
    {
        ((CustomSubBiomeTerrainDataFloatSliderInput)subBiomeTerrainData.CustomData[this.DistributionSubBiomeIndex]).Value = value;
    }
    public string ScatterNoiseXml = "";
//@"<Modifiers>
//    <Modifier type=""VertexData.VertexDataNoise"" enabled=""true"" name=""Noise"" container=""Scatter Noise"" basicView=""true"" pass=""Height"" noiseType=""Perlin"" maskDataIndex=""-1"" seed=""0"" lockSeed=""false"" frequency=""10500"" strength=""1"" interpolation=""Quintic"" dataIndex=""7"" />
//    <Modifier type=""VertexData.CustomData.UpdateCustomDataFloat"" enabled=""true"" name=""Update Custom Data (Float)"" container=""Scatter Noise"" pass=""Height"" customDataId=""Droo_Cubes_Noise_Vertex"" dataIndex=""7"" />
//</Modifiers>";
}