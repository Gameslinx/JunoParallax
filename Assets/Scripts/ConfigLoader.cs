using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.Xml.Linq;
using System.IO;
using ModApi.Common.Extensions;
using UI.Xml;
using System;
using System.Text.RegularExpressions;

// Need a method of storing names and values of each parameter for the shader defined in the shader bank
public class ScatterShader : ICloneable
{
    public string name;
    public string resourceName;
    public Dictionary<string, string> textures = new Dictionary<string, string>();
    public Dictionary<string, float> floats = new Dictionary<string, float>();
    public Dictionary<string, Vector3> vectors = new Dictionary<string, Vector3>();
    public Dictionary<string, Vector2> scales = new Dictionary<string, Vector2>();
    public Dictionary<string, Color> colors = new Dictionary<string, Color>();

    public Material material;   //This is used in the GUI for setting variables on the material, it just points to a reference of the material in Renderer
    // We want to be able to clone the template and assign its values on a scatter
    public object Clone()
    {
        var clone = new ScatterShader();
        clone.name = name;
        clone.resourceName = resourceName;
        foreach (var texture in textures)
        {
            clone.textures.Add(texture.Key, texture.Value);
        }
        foreach (var floatValue in floats)
        {
            clone.floats.Add(floatValue.Key, floatValue.Value);
        }
        foreach (var vector in vectors)
        {
            clone.vectors.Add(vector.Key, vector.Value);
        }
        foreach (var scale in scales)
        {
            clone.scales.Add(scale.Key, scale.Value);
        }
        foreach (var color in colors)
        {
            clone.colors.Add(color.Key, color.Value);
        }
        return clone;
    }
    public Material AssignMaterialVariables(Material material)
    {
        foreach (KeyValuePair<string, string> texture in textures)
        {
            Texture2D tex = TextureLoader.LoadTexture(texture.Value);

            material.SetTexture(texture.Key, tex);
        }
        foreach (KeyValuePair<string, float> floatValue in floats)
        {
            material.SetFloat(floatValue.Key, floatValue.Value);
        }
        foreach (KeyValuePair<string, Vector3> vector in vectors)
        {
            material.SetVector(vector.Key, vector.Value);
        }
        foreach (KeyValuePair<string, Vector2> scale in scales)
        {
            material.SetTextureScale(scale.Key, scale.Value);   // Key must be the name of the texture
        }
        foreach (KeyValuePair<string, Color> color in colors)
        {
            material.SetColor(color.Key, color.Value);
        }
        this.material = material;
        return material;
    }
    public bool TryGetColor(string name, out Color color)
    {
        if (colors.ContainsKey(name))
        {
            color = colors[name];
            return true;
        }
        color = Color.black;
        return false;
    }
    public bool TryGetFloat(string name, out float value)
    {
        if (floats.ContainsKey(name))
        {
            value = floats[name];
            return true;
        }
        value = 0;
        return false;
    }
}
public static class ParallaxSettings
{
    public static float rangeMultiplier = 1;
    public static float densityMultiplier = 1;
    public static float lodChangeMultiplier = 1;
    public static bool castShadows = true;
    public static bool receiveShadows = true;

    public static bool enableDynamicLOD = false;
    public static float minLODFactor = 0.5f;
    public static float maxLODFactor = 1.0f;
    public static float targetFPS = 59.9f;

    public static bool enableColliders = false;
    public static int collisionSizeThreshold = 2;
    public static int computeShaderMemory = 2048;
}
public class ConfigLoader : MonoBehaviour
{
    public static XElement shaderBank;
    public static XElement[] configs;
    public static XElement settings;
    public static Dictionary<string, ScatterBody> bodies = new Dictionary<string, ScatterBody>();
    public static Dictionary<string, ScatterShader> shaderTemplates = new Dictionary<string, ScatterShader>();

    // Load the shader definitions
    public static void LoadShaderBank(string directoryPath)
    {
        shaderBank = Directory.GetFiles(directoryPath, "ShaderBank.xml").Select(filePath => XElement.Load(filePath)).First();
        Debug.Log("Attempting to load ShaderBank config");
        //XElement bankNode = shaderBank.Element("ParallaxShaderBank");
        List<XElement> shaderNodes = shaderBank.Elements("Shader").ToList();
        // For every shader defined in the shader bank node
        foreach (XElement shader in shaderNodes )
        {
            string name = shader.GetStringAttribute("name");
            Debug.Log("Shader: " + name);
            XElement propertiesNode = shader.Element("Properties");

            // Defined as such in the config so that when using reflection to assign the values to the shader instance, the correct types are used
            // Means expanding or modifying the shader bank is easy
            // So, parse each parameter and set their default values

            XElement texturesNode = propertiesNode.Element("Textures");
            XElement floatsNode = propertiesNode.Element("Floats");
            XElement vectorsNode = propertiesNode.Element("Vectors");
            XElement scalesNode = propertiesNode.Element("Scales");
            XElement colorsNode = propertiesNode.Element("Colors");

            ScatterShader scatterShader = new ScatterShader();
            scatterShader.name = name;
            scatterShader.resourceName = name.Replace("Custom/", string.Empty);
            // Parse textures
            foreach (XElement element in texturesNode.Elements())
            {
                scatterShader.textures.Add(element.Attribute("name").Value.ToString(), "");
            }
            // Parse floats
            foreach (XElement element in floatsNode.Elements())
            {
                scatterShader.floats.Add(element.Attribute("name").Value.ToString(), 0);
            }
            // Parse vectors
            foreach (XElement element in vectorsNode.Elements())
            {
                scatterShader.vectors.Add(element.Attribute("name").Value.ToString(), Vector3.one);
            }
            // Parse scales
            foreach (XElement element in scalesNode.Elements())
            {
                scatterShader.scales.Add(element.Attribute("name").Value.ToString(), Vector2.one);
            }
            // Parse colors
            foreach (XElement element in colorsNode.Elements())
            {
                scatterShader.colors.Add(element.Attribute("name").Value.ToString(), Color.white);
            }
            shaderTemplates.Add(name, scatterShader);
            Debug.Log(" - Parsed " + shader.Name);
        }
    }
    public static void LoadSettings(string directoryPath)
    {
        settings = Directory.GetFiles(directoryPath, "ParallaxSettings.xml").Select(filePath => XElement.Load(filePath)).First();
        XElement qualityNode = settings.Element("QualitySettings");
        ParallaxSettings.rangeMultiplier = qualityNode.Element("scatterRangeMultiplier").Value.ToFloat();
        ParallaxSettings.densityMultiplier = qualityNode.Element("scatterDensityMultiplier").Value.ToFloat();
        ParallaxSettings.lodChangeMultiplier = qualityNode.Element("scatterLODChangeDistanceMultiplier").Value.ToFloat();
        ParallaxSettings.castShadows = qualityNode.Element("castShadows").Value.ToBoolean();
        ParallaxSettings.receiveShadows = qualityNode.Element("receiveShadows").Value.ToBoolean();

        XElement renderNode = settings.Element("RendererSettings");
        ParallaxSettings.enableDynamicLOD = renderNode.Element("enableDynamicLOD").Value.ToBoolean();
        ParallaxSettings.minLODFactor = renderNode.Element("minLODFactor").Value.ToFloat();
        ParallaxSettings.maxLODFactor = renderNode.Element("maxLODFactor").Value.ToFloat();
        ParallaxSettings.targetFPS = renderNode.Element("targetFPS").Value.ToFloat();

        XElement generalNode = settings.Element("GeneralSettings");
        ParallaxSettings.enableColliders = generalNode.Element("enableColliders").Value.ToBoolean();
        ParallaxSettings.collisionSizeThreshold = generalNode.Element("minimumSizeForColliders").Value.ToInt();
        ParallaxSettings.computeShaderMemory = generalNode.Element("memoryReservedForComputeShaders").Value.ToInt();
    }
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
                    // Create new scatter and parse optional properties
                    string scatterName = scatter.Attribute("name").Value;
                    Scatter thisScatter = new Scatter(planetName + "_" + scatterName, scatterName);
                    thisScatter.planetName = planetName;
                    Debug.Log("Parsing scatter: " + scatterName);

                    XElement inheritsFrom = scatter.Element("inheritsFrom");
                    thisScatter.inherits = inheritsFrom == null ? false : true;
                    thisScatter.inheritsFrom = inheritsFrom == null ? "" : inheritsFrom.Value;

                    XElement sharesNoiseWith = scatter.Element("sharesNoiseWith");
                    thisScatter.sharesNoise = sharesNoiseWith == null ? false : true;
                    thisScatter.sharesNoiseWith = sharesNoiseWith == null ? thisScatter : body.scatters[sharesNoiseWith.Value];

                    XElement maxObjectsToRender = scatter.Element("maxObjects");
                    thisScatter.maxObjectsToRender = maxObjectsToRender == null ? 1000 : maxObjectsToRender.Value.ToInt();

                    thisScatter.collisionLevel = scatter.Element("collisionLevel").Value.ToInt();

                    DistributionData distribution = new DistributionData();

                    // Parse distribution node
                    XElement distributionNode = scatter.Element("Distribution");
                    distribution._PopulationMultiplier = distributionNode.Element("populationMultiplier").Value.ToInt();
                    distribution._SpawnChance = distributionNode.Element("spawnChance").Value.ToFloat();
                    distribution._Range = distributionNode.Element("range").Value.ToFloat();
                    distribution._Seed = distributionNode.Element("seed").Value.ToFloat();
                    distribution._MinScale = distributionNode.Element("minScale").Value.ToVector3();
                    distribution._MaxScale = distributionNode.Element("maxScale").Value.ToVector3();
                    distribution._SizeJitterAmount = distributionNode.Element("sizeJitterAmount").Value.ToFloat();
                    distribution._Coverage = distributionNode.Element("coverage").Value.ToFloat();
                    distribution._MinAltitude = distributionNode.Element("minAltitude").Value.ToFloat();
                    distribution._MaxAltitude = distributionNode.Element("maxAltitude").Value.ToFloat();
                    bool alignToTerrainNormal = distributionNode.Element("alignToTerrainNormal").Value.ToBoolean();
                    distribution._AlignToTerrainNormal = alignToTerrainNormal == true ? (uint)1 : (uint)0;
                    distribution._MaxNormalDeviance = distributionNode.Element("maxNormalDeviance").Value.ToFloat();
                    distribution._RidgedNoise = false;

                    // Override
                    XElement biomeOverride = distributionNode.Element("biomeCutoff");
                    distribution._BiomeOverride = biomeOverride == null ? 0.5f : biomeOverride.Value.ToFloat();

                    thisScatter.distribution = distribution;
                    Debug.Log("Parsed distribution");

                    // Compute sqr range for optimizing number of shader dispatches in EvaluatePositions()
                    thisScatter.sqrRange = distribution._Range * distribution._Range;

                    // Parse frustum culling
                    XElement cullRange = scatter.Element("cullImmuneRadius");
                    XElement cullLimit = scatter.Element("cullLimit");
                    thisScatter.cullRadius = (cullRange.Value.ToFloat() / distribution._Range);
                    thisScatter.cullLimit = cullLimit.Value.ToFloat();

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

                    // Parse biomes
                    XElement[] biomeNodes = distributionNode.Element("Biomes").Elements().ToArray();
                    Debug.Log("Length of biome nodes: " + biomeNodes.Length);
                    thisScatter.distribution.biomes = new Dictionary<string, Biome>();
                    foreach (XElement biomeNode in biomeNodes)
                    {
                        Biome biome = new Biome();
                        biome.subBiomes = new Dictionary<string, SubBiome>();
                        string biomeName = biomeNode.Attribute("name").Value;
                        Debug.Log("Biome name: " + biomeName); 
                        foreach (XElement subBiomeNode in biomeNode.Elements())
                        {
                            string name = subBiomeNode.GetStringAttribute("name");
                            string value = subBiomeNode.GetStringAttribute("value");
                            string slope = subBiomeNode.GetStringAttribute("slope");
                            Debug.Log("Sub Biome name: " + name);
                            SubBiome subBiome = new SubBiome();
                            subBiome.flatNoiseIntensity = value.ToFloat();
                            subBiome.slopeNoiseIntensity = slope.ToFloat();
                            biome.subBiomes.Add(name, subBiome);
                        }
                        thisScatter.distribution.biomes.Add(biomeName, biome);
                    }
                    // Parse material node
                    XElement materialNode = scatter.Element("Material");
                    ScatterMaterial material = ParseScatterMaterial(materialNode);
                    thisScatter.material = material;

                    Debug.Log("Parsed Material");

                    if (!thisScatter.sharesNoise)
                    {
                        thisScatter.Register();
                    }

                    thisScatter.RegisterColliders();

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
        material._Mesh = materialNode.GetStringAttribute("mesh", "Assets/Models/Droo/Sphere.fbx");

        string shaderName = materialNode.GetStringAttribute("shader", "Custom/InstancedCutout");
        string castShadows = materialNode.GetStringAttribute("castShadows", "True");
        material.castShadows = castShadows.ToBoolean();

        Debug.Log("Using shader: " + shaderName);
        ScatterShader shader = shaderTemplates[shaderName].Clone() as ScatterShader;

        // Consult the shader bank and search for the config value corresponding to that property
        // All properties MUST be defined in the scatter material node
        // Parse texture paths

        Debug.Log("Loading properties");

        string[] textureProperties = shader.textures.Keys.ToArray();
        foreach (string textureProperty in textureProperties)
        {
            Debug.Log("Loading texture property: " + textureProperty);
            Debug.Log("This node name: " + materialNode.Name);
            XElement el = materialNode.Element(textureProperty);
            if (el == null)
            {
                Debug.Log("Element is null?");
            }
            Debug.Log("Value: " + el.Value);
            shader.textures[textureProperty] = materialNode.Element(textureProperty).Value;
        }
        // Parse float values
        string[] floatProperties = shader.floats.Keys.ToArray();
        foreach (string floatProperty in floatProperties)
        {
            Debug.Log("Loading float property: " + floatProperty);
            shader.floats[floatProperty] = materialNode.Element(floatProperty).Value.ToFloat();
        }
        // Parse vector values
        string[] vectorProperties = shader.vectors.Keys.ToArray();
        foreach (string vectorProperty in vectorProperties)
        {
            Debug.Log("Loading vector property: " + vectorProperty);
            shader.vectors[vectorProperty] = materialNode.Element(vectorProperty).Value.ToVector3();
        }
        // Parse texture scales
        string[] scaleProperties = shader.scales.Keys.ToArray();
        foreach (string scaleProperty in scaleProperties)
        {
            Debug.Log("Loading scale property: " + scaleProperty);
            shader.scales[scaleProperty] = materialNode.Element(scaleProperty).Value.ToVector2();
        }
        // Parse color values
        string[] colorProperties = shader.colors.Keys.ToArray();
        foreach (string colorProperty in colorProperties)
        {
            // XML "ToColor" does not support 0-1 colour ranges, sadly
            Debug.Log("Loading color property: " + colorProperty);
            // Get colour string as definite "1,1,1,1" or "1,1,1"
            string rgbaColours = Regex.Replace(materialNode.Element(colorProperty).Value, @"s", "");
            List<string> colors = rgbaColours.Split(',').ToList();
            // Add the 4th colour component if there are only 3 for consistency
            if (colors.Count == 3)
            {
                colors.Add("1");
            }
            Color color = new Color(colors[0].ToFloat(), colors[1].ToFloat(), colors[2].ToFloat(), colors[3].ToFloat());
            shader.colors[colorProperty] = color;
        }

        material._Shader = shader;

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
        // This whole method can be improved to better support more noise types
        string noiseType = noiseNode.Element("noiseType").Value;
        string modifier = "";
        if (noiseType == "Perlin")
        {
            modifier = ParsePerlinNoise(noiseNode, scatter);
        }
        else if (noiseType == "PerlinFractal")
        {
            modifier = ParseFractalNoise(noiseNode, scatter);
        }

        Debug.Log("Modifier parsed from config to XML:" + modifier);
        return modifier;
    }
    public static string ParsePerlinNoise(XElement noiseNode, Scatter scatter)
    {
        string name = noiseNode.Element("name").Value;
        string seed = noiseNode.Element("seed").Value;
        string frequency = noiseNode.Element("frequency").Value;
        string strength = noiseNode.Element("strength").Value;
        string interpolation = noiseNode.Element("interpolation").Value;

        string modifier = $"<Modifiers>\n\t<Modifier type=\"VertexData.VertexDataNoise\" enabled=\"true\" name=\"{name}\" container=\"Scatter Noise\" basicView=\"true\" pass=\"Height\" noiseType=\"Perlin\" maskDataIndex=\"-1\" seed=\"{seed}\" lockSeed=\"false\" frequency=\"{frequency}\" strength=\"{strength}\" interpolation=\"{interpolation}\" dataIndex=\"7\" />\n\t<Modifier type=\"VertexData.CustomData.UpdateCustomDataFloat\" enabled=\"true\" name=\"Update Custom Data (Float)\" container=\"Scatter Noise\" pass=\"Height\" customDataId=\"{scatter.Id}_Noise_Vertex\" dataIndex=\"7\" />\n</Modifiers>";
        return modifier;
    }
    public static string ParseFractalNoise(XElement noiseNode, Scatter scatter)
    {
        string name = noiseNode.Element("name").Value;
        string seed = noiseNode.Element("seed").Value;
        string frequency = noiseNode.Element("frequency").Value;
        string strength = noiseNode.Element("strength").Value;
        string interpolation = noiseNode.Element("interpolation").Value;
        string octaves = noiseNode.Element("octaves").Value;
        string gain = noiseNode.Element("gain").Value;
        string lacunarity = noiseNode.Element("lacunarity").Value;
        string fractalType = noiseNode.Element("fractalType").Value;

        scatter.distribution._RidgedNoise = noiseNode.Element("ridgedNoise").Value.ToBoolean();

        string modifier = $"<Modifiers>\n\t<Modifier type=\"VertexData.VertexDataNoise\" enabled=\"true\" name=\"{name}\" container=\"Scatter Noise\" basicView=\"true\" pass=\"Height\" noiseType=\"PerlinFractal\" maskDataIndex=\"-1\" seed=\"{seed}\" lockSeed=\"false\" frequency=\"{frequency}\" strength=\"{strength}\" fractalType=\"{fractalType}\" octaves=\"{octaves}\" fractalLacunarityType=\"Default\" lacunarity=\"{lacunarity}\" fractalAmplitudeType=\"Default\" gain=\"{gain}\" interpolation=\"{interpolation}\" dataIndex=\"7\" /> \n\t <Modifier type=\"VertexData.CustomData.UpdateCustomDataFloat\" enabled=\"true\" name=\"Update Custom Data (Float)\" container=\"Scatter Noise\" pass=\"Height\" customDataId=\"{scatter.Id}_Noise_Vertex\" dataIndex=\"7\" />\n</Modifiers>";
        return modifier;
    }
}
