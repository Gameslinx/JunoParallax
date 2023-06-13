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

public struct DistributionData
{
    public int _PopulationMultiplier;
    public float _SpawnChance;
    public float _Range;
    public Vector3 _MinScale;
    public Vector3 _MaxScale;
    public LOD lod0;
    public LOD lod1;
}
public struct LOD
{
    public float distance;
    public ScatterMaterial material;
}

public struct ScatterMaterial
{
    public ScatterShader _Shader;
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
    public void Register()
    {
        // Register custom per-vertex data to hold the noise results
        this.NoiseVertexIndex = CustomPlanetVertexData.Register<CustomPlanetVertexDataFloat>(this.NoiseVertexId);

        // Register per-quad data to store the per-vertex data noise results
        this.NoiseQuadIndex = CustomCreateQuadData.Register<CustomCreateQuadDataFloat>(this.NoiseQuadId, () => new CustomCreateQuadDataFloat(this.NoiseVertexId));

        // Register custom sub-biome data to adjust the distribution values per sub-biome
        this.DistributionSubBiomeIndex = CustomSubBiomeTerrainData.Register<CustomSubBiomeTerrainDataFloatInput, CustomPlanetVertexDataFloat>(
            this.DistributionVertexId, () => new CustomSubBiomeTerrainDataFloatSliderInput(this.DisplayName, "Adjusts the distribution value for this object", 0, 2));
        this.DistributionVertexIndex = CustomPlanetVertexData.GetIndex(this.DistributionVertexId);

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