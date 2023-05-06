using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using ModApi.Common.Extensions;
using UI.Xml;

public class ConfigLoader : MonoBehaviour
{
    public static XElement[] configs;
    public static Dictionary<string, ScatterBody> bodies = new Dictionary<string, ScatterBody>();
    public static void LoadConfigs(string directoryPath)
    {
        // Load all configs
        configs = Directory.GetFiles(directoryPath, "*.xml").Select(filePath => XElement.Load(filePath)).ToArray();
        Debug.Log("Attempting to load " + configs.Length + " Parallax configs");
        // For each config
        foreach (XElement config in configs)
        {
            // Get all planet nodes
            Debug.Log("Name: " + config.Name);
            List<XElement> planetNodes = config.Elements("CelestialBody").ToList();
            Debug.Log("Got planet nodes");
            foreach (XElement planetNode in planetNodes)
            {
                // Get celestial body name attribute
                string planetName = planetNode.Attribute("name").Value;
                ScatterBody body = new ScatterBody(planetName);
                Debug.Log("Parsing planet: " + planetName);

                // Get all scatters on this body
                List<XElement> scatters = planetNode.Element("Scatters").Elements("Scatter").ToList();
                foreach (XElement scatter in scatters)
                {
                    // Create new scatter
                    string scatterName = scatter.Attribute("name").Value;
                    Scatter thisScatter = new Scatter(planetName + "_" + scatterName, scatterName);
                    Debug.Log("Parsing scatter: " + scatterName);

                    DistributionData distribution = new DistributionData();

                    // Parse distribution node
                    XElement distributionNode = scatter.Element("Distribution");
                    distribution._PopulationMultiplier = distributionNode.Element("populationMultiplier").Value.ToInt();
                    distribution._SpawnChance = distributionNode.Element("spawnChance").Value.ToFloat();
                    thisScatter.distribution = distribution;
                    Debug.Log("Parsed distribution");

                    // Parse noise
                    XElement noiseNode = distributionNode.Element("PersistentNoise");
                    string modifier = GetNoiseProperties(noiseNode, thisScatter);
                    thisScatter.ScatterNoiseXml = modifier;

                    // Parse LODs
                    XElement[] lodNodes = distributionNode.Element("LODs").Elements().ToArray();
                    LOD lod0 = new LOD();
                    LOD lod1 = new LOD();
                    lod0.distance = lodNodes[0].Element("distance").Value.ToFloat();
                    lod1.distance = lodNodes[1].Element("distance").Value.ToFloat();
                    lod0.material = ParseScatterMaterial(lodNodes[0].Element("Material"));
                    lod1.material = ParseScatterMaterial(lodNodes[1].Element("Material"));

                    thisScatter.distribution.lod0 = lod0;
                    thisScatter.distribution.lod1 = lod1;

                    Debug.Log("Parsed LODs");

                    // Parse material node
                    XElement materialNode = scatter.Element("Material");
                    ScatterMaterial material = ParseScatterMaterial(materialNode);
                    thisScatter.material = material;

                    Debug.Log("Parsed Material");

                    thisScatter.Register();

                    body.scatters.Add(scatterName, thisScatter);
                }

                bodies.Add(planetName, body);
            }
        }
        Debug.Log("Config loading complete");
    }
    public static ScatterMaterial ParseScatterMaterial(XElement materialNode)
    {
        ScatterMaterial material = new ScatterMaterial();
        material._Shader = materialNode.GetStringAttribute("shader", "InstancedCutout");
        material._Mesh = materialNode.GetStringAttribute("mesh", "Assets/_Common/Sphere.obj");
        material._MainTex = materialNode.Element("mainTex").Value;
        material._Normal = materialNode.Element("normal").Value;
        material._Color = ParseColor(materialNode.Element("color").Value);

        return material;

        // Add methods for parsing material based on shader
    }
    public static Color ParseColor(string color)
    {
        // Color format must be RGB or RGBA
        string[] components = color.Replace(" ", string.Empty).Trim().Split(',');
        return new Color(components[0].ToFloat(), components[1].ToFloat(), components[2].ToFloat());
    }
    public static string GetNoiseProperties(XElement noiseNode, Scatter scatter)    //This looks really hacky and I promise it is but it works :D
    {
        string name = noiseNode.Element("name").Value;
        string noiseType = noiseNode.Element("noiseType").Value;
        string seed = noiseNode.Element("seed").Value;
        string frequency = noiseNode.Element("frequency").Value;
        string strength = noiseNode.Element("strength").Value;
        string interpolation = noiseNode.Element("interpolation").Value;

        string modifier = $"<Modifiers>\n\t<Modifier type=\"VertexData.VertexDataNoise\" enabled=\"true\" name=\"{name}\" container=\"Scatter Noise\" basicView=\"true\" pass=\"Height\" noiseType=\"{noiseType}\" maskDataIndex=\"-1\" seed=\"{seed}\" lockSeed=\"false\" frequency=\"{frequency}\" strength=\"{strength}\" interpolation=\"{interpolation}\" dataIndex=\"7\" />\n\t<Modifier type=\"VertexData.CustomData.UpdateCustomDataFloat\" enabled=\"true\" name=\"Update Custom Data (Float)\" container=\"Scatter Noise\" pass=\"Height\" customDataId=\"{scatter.Id}_Noise_Vertex\" dataIndex=\"7\" />\n</Modifiers>";

        Debug.Log("Modifier parsed from config to XML:" + modifier);
        return modifier;
    }
    // Get names of planets in each config
    public static void ListConfigs()
    {
        
    }
}
