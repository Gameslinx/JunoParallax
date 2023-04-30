#include "AutoLight.cginc"
#include "Lighting.cginc"
#include "noiseSimplex.cginc"
#include "UnityCG.cginc"
float3 _PlanetOrigin;
float3 _ShaderOffset;
float3 _WindSpeed;
float _WaveAmp;
float _HeightCutoff;
float _HeightFactor;
float _WaveSpeed;
sampler2D _WindMap;

sampler2D _MainTex;
float2 _MainTex_ST;
sampler2D _BumpMap;

float _DitherFactor;
float _InitialTime;
float _CurrentTime;

float _Transmission;
float _Hapke = 1;
float _Gloss = 1;
float _Metallic = 1;
float3 _MetallicTint;
float4 _Color;

struct GrassData
{
    float4x4 mat;
};

StructuredBuffer<GrassData> _Properties;

float3 Wind(float4x4 mat, float3 world_vertex, float localVertexHeight)
{
    float3 bf = normalize(abs(normalize(world_vertex - _PlanetOrigin)));
    bf /= dot(bf, (float3) 1);
    float2 xz = world_vertex.zx * bf.y;
    float2 xy = world_vertex.xy * bf.z;
    float2 zy = world_vertex.yz * bf.x;
                
    float2 samplePosXZ = xz;
    samplePosXZ += _Time.x * _WindSpeed.xz;
    samplePosXZ = (samplePosXZ) * _WaveAmp;
                
    float2 samplePosXY = xy;
    samplePosXY += _Time.x * _WindSpeed.xy;
    samplePosXY = (samplePosXY) * _WaveAmp;
                
    float2 samplePosZY = zy;
    samplePosZY += _Time.x * _WindSpeed.zy;
    samplePosZY = (samplePosZY) * _WaveAmp;
                
    float2 wind = (samplePosXZ + samplePosXY + samplePosZY) / 3;
                
    float heightFactor = localVertexHeight > _HeightCutoff;
    heightFactor = heightFactor * pow(localVertexHeight, _HeightFactor);
    if (localVertexHeight < 0)
    {
        heightFactor = 0;
    }
                
    float2 windSample = -tex2Dlod(_WindMap, float4(wind, 0, 0));

    float3 positionOffset = mul(unity_ObjectToWorld, float3(windSample.x, 0, windSample.y));
                
    return sin(_WaveSpeed * positionOffset) * heightFactor;
}
float InterleavedGradientNoise(float alpha, float2 uv)
{
    float timeLim = alpha + 0.75;
    float ditherFactor = (_Time.y - alpha) / (timeLim - alpha);
    return frac(sin(dot(uv / 10, float2(12.9898, 78.233))) * 43758.5453123) > ditherFactor;
}
float4 BlinnPhong(float3 normal, float3 basicNormal, float4 diffuseCol, float3 lightDir, float3 viewDir, float3 attenCol)
{

    half3 halfDir = normalize(lightDir + viewDir);

    // Dot
    half NdotL = max(0, dot(normal, lightDir));
    NdotL = pow(NdotL, _Hapke);

    half NdotH = max(0, dot(normal, halfDir));
    // Color
    fixed3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * diffuseCol.rgb;
    fixed3 diffuse =  attenCol * diffuseCol.rgb * NdotL;
    fixed3 specular = pow(NdotH, _Gloss * diffuseCol.a) * _Metallic * diffuseCol.a;
    
    float angle = saturate(dot(normalize(basicNormal), _WorldSpaceLightPos0));
    angle = 1 - pow(1 - angle, 7);
    specular *= saturate(angle - 0.2);
    
    specular = specular * _MetallicTint.rgb;
    fixed4 color = fixed4(ambient + diffuse + specular, 1.0);

    return color;
}
float3 Fresnel(float3 normal, float3 viewDir, float smoothness, float3 color)
{
    float fresnel = dot(normal, viewDir);
    fresnel = saturate(1 - fresnel);
    fresnel = pow(fresnel, smoothness);
    return fresnel * color;
}
void Billboard(inout float4 vertex, float4x4 mat)
{
    float4x4 localMat = mul(mat, unity_WorldToObject);

    const float3 local = float3(vertex.x, vertex.y, vertex.z); // this is the quad verts as generated by MakeMesh.cs in the localPos list.
    const float3 offset = 0;//vertex.xyz - local;
    
    const float3 upVector = float3(0, 1, 0);
    const float3 forwardVector = mul(UNITY_MATRIX_IT_MV[2].xyz, localMat); // camera forward   
    const float3 rightVector = normalize(cross(forwardVector, upVector));
 
    float3 position = 0;
    position += local.x * rightVector;
    position += local.y * upVector;
    position += local.z * forwardVector;
 
    const float3x3 rotMat = float3x3(upVector, forwardVector, rightVector);
    
    vertex = float4(offset + position, 1);
}
#define PARALLAX_LIGHT_ATTENUATION(v2f) attenuation = LIGHT_ATTENUATION(v2f); attenuation = 1 - pow(1 - attenuation, 3);