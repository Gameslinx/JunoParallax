using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public struct DistributionData
{
    public int _PopulationMultiplier;
}
public class Scatter
{
    public DistributionData distributionData;
    public Scatter()
    {
        distributionData = new DistributionData();
        distributionData._PopulationMultiplier = 5;
    }
}