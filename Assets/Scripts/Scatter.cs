using Assets.Scripts.Terrain.CustomData;
using Assets.Scripts.Terrain;
using ModApi.Planet.CustomData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct DistributionData
{
    public int _PopulationMultiplier;
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
public class Scatter
{
    public DistributionData distributionData;
    public string Id { get; }
    public string DisplayName { get; }
    public string DistributionQuadId { get; }
    public int DistributionQuadIndex { get; private set; }
    public string DistributionVertexId { get; }
    public int DistributionVertexIndex { get; private set; }
    public string NoiseQuadId { get; }
    public int NoiseQuadIndex { get; private set; }
    public string NoiseVertexId { get; }
    public int NoiseVertexIndex { get; private set; }
    public Dictionary<QuadScript, ScatterNoise> noise = new Dictionary<QuadScript, ScatterNoise>();
    public Scatter(string id, string displayName)
    {
        distributionData = new DistributionData();
        distributionData._PopulationMultiplier = 1;

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
        this.DistributionVertexIndex = CustomSubBiomeTerrainData.Register<CustomSubBiomeTerrainDataFloatInput, CustomPlanetVertexDataFloat>(
            DistributionVertexId, () => new CustomSubBiomeTerrainDataFloatSliderInput(this.DisplayName, "Adjusts the distribution value for this object", 0, 2));

        // Register per-quad data to store the sub-biome distribution results
        this.DistributionQuadIndex = CustomCreateQuadData.Register<CustomCreateQuadDataFloat>(
            this.DistributionQuadId, () => new CustomCreateQuadDataFloat(this.DistributionVertexId));
    }

    public float[] GetNoiseData(CreateQuadData quadData)
    {
        return ((CustomCreateQuadDataFloat)quadData.CustomData[this.NoiseQuadIndex]).Values;
    }

    public float[] GetDistributionData(CreateQuadData quadData)
    {
        return ((CustomCreateQuadDataFloat)quadData.CustomData[this.DistributionQuadIndex]).Values;
    }
}