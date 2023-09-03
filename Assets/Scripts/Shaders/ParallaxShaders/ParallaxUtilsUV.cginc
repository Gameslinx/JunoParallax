#include "ParallaxHelperFunctions.cginc"
#pragma multi_compile ATMOSPHERE

struct appdata_t
{
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float4 tangent : TANGENT;
};
struct v2f
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float3 worldNormal : TEXCOORD1;
    float3 world_vertex : TEXCOORD2;

    float3 tangentWorld : TEXCOORD6;
    float3 binormalWorld : TEXCOORD7;

    float3 viewDir : TEXCOORD8;
    float3 lightDir : TEXCOORD9;
    
    #if ATMOSPHERE
        float3 atmosColor : TEXCOORD10;
    #endif
    float3 up : TEXCOORD11;
    LIGHTING_COORDS(3, 4)
};
struct v2f_lighting
{
    float4 pos : SV_POSITION;
    fixed4 color : COLOR;
    float2 uv : TEXCOORD0;
    float3 normal : NORMAL;
    float3 worldNormal : TEXCOORD1;
    float3 world_vertex : TEXCOORD2;

    float3 tangentWorld : TEXCOORD6;
    float3 binormalWorld : TEXCOORD7;

    float3 viewDir : TEXCOORD8;
    float3 lightDir : TEXCOORD9;
};
struct shadow_appdata_t
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
};
struct shadow_v2f
{
    float4 pos : SV_POSITION;
    float2 uv : TEXCOORD0;
    float4 color : COLOR;
};
