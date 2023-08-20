#include "ParallaxHelperFunctions.cginc"

struct appdata_t
{
    float4 vertex : POSITION;
    float4 color : COLOR;
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
struct v2f_screenPos
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
    
    float4 grabPos : TEXCOORD10;

    #if ATMOSPHERE
        float3 atmosColor : TEXCOORD11;
    #endif
    
    LIGHTING_COORDS(3, 4)
};
struct v2f_screenPos_lighting
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
    
    float4 grabPos : TEXCOORD10;
};
float3x3 GetBiplanarCoordinates(float3 p, float3 n)
{
    n = abs(n);
    uint3 ma = (n.x > n.y && n.x > n.z) ? uint3(0, 1, 2) :
		(n.y > n.z) ? uint3(1, 2, 0) :
		uint3(2, 0, 1);
    uint3 mi = (n.x < n.y && n.x < n.z) ? uint3(0, 1, 2) :
		(n.y < n.z) ? uint3(1, 2, 0) :
		uint3(2, 0, 1);
    uint3 me = 3 - mi - ma;
    return float3x3(ma, mi, me);
}
float4 BiplanarNormal_float
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale)
{
    float3 p = wpos;
    float3 n = abs(wnrm);
    uint3 ma = biplanarCoords[0];
    uint3 me = biplanarCoords[2];

    float4 x = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));
    float4 y = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));

    float4 x1 = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));
    float4 y1 = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));

    float3 n1 = UnpackNormal(x);
    float3 n2 = UnpackNormal(y);
    float3 n3 = UnpackNormal(x1);
    float3 n4 = UnpackNormal(y1);
    
    n1 = lerp(n1, n3, percentage);
    n2 = lerp(n2, n4, percentage);
    n1 = normalize(float3(n1.y + wnrm[ma.z], n1.x + wnrm[ma.y], wnrm[ma.x]));
    n2 = normalize(float3(n2.y + wnrm[me.z], n2.x + wnrm[me.y], wnrm[me.x]));
    n1 = float3(n1[ma.z], n1[ma.y], n1[ma.x]);
    n2 = float3(n2[me.z], n2[me.y], n2[me.x]);
	
    float2 w = float2(n[ma.x], n[me.x]);
    w = saturate((w - 0.5773) / (1 - 0.5773));
    float3 output = normalize((n1 * w.x + n2 * w.y) / (w.x + w.y));

    return float4((output), 1);
}
float4 BiplanarTexture_float
(sampler2D tex, float3 dpdx, float3 dpdy, float UVDistortion, float nextUVDist, float percentage, float3 surfaceUVs, float3x3 biplanarCoords, float3 wpos, float3 wnrm, float2 scale)
{

    float3 p = wpos;
    float3 n = abs(wnrm);
    uint3 ma = biplanarCoords[0];
    uint3 me = biplanarCoords[2];

    float4 x = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));
    float4 y = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / UVDistortion, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / UVDistortion), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / UVDistortion));

    float4 x1 = tex2D(tex, float2(p[ma.y] * scale.x - surfaceUVs[ma.y], p[ma.z] * scale.y - surfaceUVs[ma.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));
    float4 y1 = tex2D(tex, float2(p[me.y] * scale.x - surfaceUVs[me.y], p[me.z] * scale.y - surfaceUVs[me.z]) / nextUVDist, float2(dpdx[biplanarCoords[0].y], dpdx[biplanarCoords[0].z]) * (scale / nextUVDist), float2(dpdy[biplanarCoords[0].y], dpdy[biplanarCoords[0].z]) * (scale / nextUVDist));

    float4 n1 = lerp(x, x1, percentage);
    float4 n2 = lerp(y, y1, percentage);

    float2 w = float2(n[ma.x], n[me.x]);

    w = saturate((w - 0.5773) / (1 - 0.5773));

    float4 output = ((n1 * w.x + n2 * w.y) / (w.x + w.y));

    return output;
}
float GetZoomLevel(float cameraDist)
{
    float ZoomLevel = log2(cameraDist / 1.333f);
    if (ZoomLevel < 4)
    {
        ZoomLevel = 4;
    }
    return ZoomLevel;
}