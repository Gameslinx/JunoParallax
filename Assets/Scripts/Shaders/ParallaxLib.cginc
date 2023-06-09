#include "UnityCG.cginc"
float4 _CameraFrustumPlanes[6];
float3 _PlanetNormal;
uint _AlignToTerrainNormal;
struct TransformData
{
    float4x4 mat;
};
struct PositionData
{
    float3 pos;
    float3 scale;
    float rot;
    uint index;
    // Add an integer here to indicate which triangle this object came from
};

float Rand(float2 p)
{
	float3 p3  = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}
float3 TriangleAverage(float3 p1, float3 p2, float3 p3, float r1, float r2)
{
    return ((1 - sqrt(r1)) * p1) + ((sqrt(r1) * (1 - r2)) * p2) + ((r2 * sqrt(r1)) * p3);
}
float TriangleAverage(float p1, float p2, float p3, float r1, float r2)
{
    return ((1 - sqrt(r1)) * p1) + ((sqrt(r1) * (1 - r2)) * p2) + ((r2 * sqrt(r1)) * p3);
}
float3 TriangleAverage(float3 p1, float3 p2, float3 p3)
{
    return (p1 + p2 + p3) / 3;
}
float DegToRad(float deg)
{
    return (3.14159 / 180.0f) * deg;
}
float GetNormalDeviance(float3 normal1, float3 normal2, float3 normal3)
{
    float nrmDev1 = dot(normal1, normal2);
    float nrmDev2 = dot(normal1, normal3);
    float nrmDev3 = dot(normal2, normal3);
    float normalDeviance = min(nrmDev1, min(nrmDev2, nrmDev3)); // Min - The vectors are at their most diverged (align the least)
    return normalDeviance;
}
float4x4 GetTranslationMatrix(float3 pos)
{
    return  float4x4(float4(1, 0, 0, pos.x), float4(0, 1, 0, pos.y), float4(0, 0, 1, pos.z), float4(0,0,0,1));//
}
float4x4 GetRotationMatrix(float3 anglesDeg)
{
    anglesDeg = float3(DegToRad(anglesDeg.x), DegToRad(anglesDeg.y), DegToRad(anglesDeg.z));

    float4x4 rotationX = 
        float4x4(float4(1, 0, 0, 0),
        float4(0, cos(anglesDeg.x), -sin(anglesDeg.x), 0),
        float4(0, sin(anglesDeg.x), cos(anglesDeg.x), 0),
        float4(0, 0, 0, 1));

    float4x4 rotationY = 
        float4x4(float4(cos(anglesDeg.y), 0, sin(anglesDeg.y), 0),
        float4(0, 1, 0, 0),
        float4(-sin(anglesDeg.y), 0, cos(anglesDeg.y), 0),
        float4(0, 0, 0, 1));

    float4x4 rotationZ = 
        float4x4(float4(cos(anglesDeg.z), -sin(anglesDeg.z), 0, 0),
        float4(sin(anglesDeg.z), cos(anglesDeg.z), 0, 0),
        float4(0, 0, 1, 0),
        float4(0, 0, 0, 1));

    return mul(rotationY, mul(rotationX, rotationZ));
}
float4x4 GetScaleMatrix(float3 scale)
{
    return  float4x4(float4(scale.x, 0, 0, 0),
            float4(0, scale.y, 0, 0),
            float4(0, 0, scale.z, 0),
            float4(0, 0, 0, 1));
}
float4x4 TransformToPlanetNormal(float3 a, float3 b)
{
    //if (a == float3(0,0,1))//
    float3 v = (cross(a, b));
    float v1 = v.x;
    float v2 = v.y;
    float v3 = v.z;
    
    float c = dot(a, b);
    float4x4 V = float4x4(
        float4(0, -v3, v2, 0),
        float4(v3, 0, -v1, 0),
        float4(-v2, v1, 0, 0),
        float4(0, 0, 0, 1)
        );
    V = transpose(V);
    float4x4 VPlusI = float4x4(
        float4(1, -v3, v2, 0),
        float4(v3, 1, -v1, 0),
        float4(-v2, v1, 1, 0),
        float4(0, 0, 0, 1)
        );
    VPlusI = transpose(VPlusI);
    float4x4 VSquared = mul(V, V);
    
    float lastPart = (1 / (1 + c));
    
    float4x4 halfMat = VSquared * lastPart;
    float4x4 full = transpose(halfMat + VPlusI);
    full[0].w = 0;
    full[1].w = 0;
    full[2].w = 0;
    
    full[3].w = 1;
    
    full[3].x = 0;
    full[3].y = 0;
    full[3].z = 0;
    return full;
}
float4x4 GetTRSMatrix(float3 position, float3 rotationAngles, float3 scale, float3 terrainNormal)
{
    
    float3 nrm;
    if (_AlignToTerrainNormal == 0)
    {
        nrm = normalize(_PlanetNormal);
    }
    else
    {
        nrm = normalize(terrainNormal);
    }
    float3 up = float3(0,1,0);
    //if (_AlignToNormal == 1)
    //{
    //    nrm = thisNormal;
    //}
    float4x4 mat = TransformToPlanetNormal(up, nrm);
    //return mul(GetTranslationMatrix(position), mul(mat, GetScaleMatrix(float3(1,1,1))));
    //return GetTranslationMatrix(position);
    return mul(GetTranslationMatrix(position), mul(mul(mat, GetRotationMatrix(rotationAngles)), GetScaleMatrix(scale)));
}
float4 CameraDistances0(float3 worldPos)
{
    return float4(
			dot(_CameraFrustumPlanes[0].xyz, worldPos) + _CameraFrustumPlanes[0].w,
			dot(_CameraFrustumPlanes[1].xyz, worldPos) + _CameraFrustumPlanes[1].w,
			dot(_CameraFrustumPlanes[2].xyz, worldPos) + _CameraFrustumPlanes[2].w,
			dot(_CameraFrustumPlanes[3].xyz, worldPos) + _CameraFrustumPlanes[3].w
		);
}
float4 CameraDistances1(float3 worldPos)
{
    return float4(
			dot(_CameraFrustumPlanes[4].xyz, worldPos) + _CameraFrustumPlanes[4].w,
			dot(_CameraFrustumPlanes[5].xyz, worldPos) + _CameraFrustumPlanes[5].w,
			0.00001f,
			0.00001f
		);
}
float4 GetCascades(float3 wpos, float overlapFactor)
{
    float3 fromCenter0 = wpos - unity_ShadowSplitSpheres[0].xyz;
    float3 fromCenter1 = wpos - unity_ShadowSplitSpheres[1].xyz;
    float3 fromCenter2 = wpos - unity_ShadowSplitSpheres[2].xyz;
    float3 fromCenter3 = wpos - unity_ShadowSplitSpheres[3].xyz;

    float4 distances2 = float4(dot(fromCenter0,fromCenter0), dot(fromCenter1,fromCenter1), dot(fromCenter2,fromCenter2), dot(fromCenter3,fromCenter3));
	fixed4 weights = float4(distances2 < unity_ShadowSplitSqRadii - overlapFactor) + float4(distances2 < unity_ShadowSplitSqRadii + overlapFactor);

	weights.yzw = saturate(weights.yzw - weights.xyz);

    return weights;
}