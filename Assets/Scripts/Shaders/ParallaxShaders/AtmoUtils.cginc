struct AtmosData
{
    float3 color;
    float dist;
};
float3 _adjustedCameraPosition;
float _scaleDepth;
float _atmosSizeScale;
float _innerRadius;
float _outerRadius;
int _samples;
float _scale;
float _atmosScale;
float _scaleOverScaleDepth;
float _kr4PI;
float _km4PI;
float _krESun;
float _kmESun;
float _worldPositionScale;
float3 _invWaveLength;

float ExpScale(float cos, float scaleDepth, float atmosSizeScale)
    {
        float x = 1 - cos;
        return pow(scaleDepth * exp(-0.00287 + x * (0.459 + x * (3.83 + x * (-6.80 + x * 5.25)))), 1.0 / atmosSizeScale);
    }
float3 GetAtmosColor(float3 startPos, float3 direction, float3 lightDir, float atmosphereDist, half3 lightColor, float3 invWavelength)
    {
        float3 atmosColor = 0;
        float attenuate = 0;

        float cloudMask = 0;
        
        // Atmos
        float3 posNorm = normalize(startPos);

        float cameraAngle;
        float lightAngle;
        float cameraScale;
        float lightScale;
        
        bool precompute = false;
        float precomputedScatter;
        float precomputedCameraOffset;
        
        if(precompute)
        {
            cameraAngle = saturate(dot(direction, posNorm));
            lightAngle = dot(lightDir.xyz, posNorm);
            cameraScale = ExpScale(cameraAngle, _scaleDepth, _atmosSizeScale);
            lightScale = ExpScale(lightAngle, _scaleDepth, _atmosSizeScale);
            
            float startDepth = exp((_innerRadius - _outerRadius) / _scaleDepth);
            precomputedCameraOffset = startDepth * cameraScale;
            precomputedScatter = (lightScale + cameraScale);
        }

        float sampleLength = atmosphereDist / _samples;
        float scaledLength = sampleLength * _scale * _atmosScale;

        float3 step = direction * sampleLength;
        float stepLen = length(step);
        
        float3 current = startPos;
        for (int index = 0; index < _samples; ++index)
        {
            // Atmos
            float height = length(current);
            float depth = exp(_scaleOverScaleDepth * min(0, _innerRadius - height));
            float scatter;

            if(!precompute)
            {
                float3 currentNormalized = current / height;
                cameraAngle = saturate(dot(direction, currentNormalized));
                lightAngle = dot(lightDir.xyz, currentNormalized);
                cameraScale = ExpScale(cameraAngle, _scaleDepth, _atmosSizeScale);
                lightScale = ExpScale(lightAngle, _scaleDepth, _atmosSizeScale);
                scatter =  exp(-1.0 / _scaleDepth) + depth * (lightScale - cameraScale);
            }
            else
            {
                scatter = depth * precomputedScatter - precomputedCameraOffset;
            }

            float attenuate = exp(-scatter * (invWavelength.xyz * _kr4PI + _km4PI));
            atmosColor += attenuate * (depth) * scaledLength;

            current += step;
        }

        return atmosColor;
    }
AtmosData GetAtmosphereData(float3 vertPosObjectSpace, float3 lightDir, half3 lightColor, float3 invWavelength)
{
    float3 vertexToCameraDir = _adjustedCameraPosition.xyz - vertPosObjectSpace;
    float cameraToVertexDist = length(vertexToCameraDir);
    // Normalize ray.
    vertexToCameraDir /= cameraToVertexDist;
    float atmosphereDist = cameraToVertexDist;

    // If the camera is outside the atmosphere, use the sphere intersection function to determine how much there is.
    //if (_cameraHeight2 > _outerRadius2)
    //{
    //    atmosphereDist = GetIntersectionDist(vertPosObjectSpace, vertexToCameraDir, _outerRadius);
    //}
    // Calculate the atmosphere color.

    float3 atmosColor = GetAtmosColor(vertPosObjectSpace, vertexToCameraDir, lightDir, atmosphereDist, lightColor, invWavelength);
    // NOTE: Color seemed to be getting some NANs or Infinities in Solar SOI. The clamp fixes it.
    AtmosData atmosData;
    atmosData.color = clamp(atmosColor * (invWavelength.xyz * _krESun + _kmESun), 0, 10000);
    atmosData.dist = atmosphereDist;
    return atmosData;
}

// For use in vertex shader
float3 GetAtmosphereDataForVertex(float3 worldPos, float3 lightDir, float3 origin, half3 lightColor)
{
    /* Note: GetAtmosphereData() takes sphereObjectSpaceVertPos, which is an object space vertex position.  For shaders such as GroundFromSpace, the \
       regular vertex's object space position is used as-is.  We can't do that with quad-sphere rendering b/c object space positions are anchored around \
       a quad, not a sphere.  So we're using world-space positions and getting their center-relative positions and then scaling them to construct \
       an object-space position. */
    float3 vertexPosWorldRelative = worldPos.xyz - origin;
    float3 vertexObjectPos = vertexPosWorldRelative / _worldPositionScale;
    AtmosData atmosData = GetAtmosphereData(vertexObjectPos, lightDir, lightColor, _invWaveLength);
    return atmosData.color;
}

float3 ApplyAtmoColor(float3 atmoColor, float atmosphereStrength, float3 col)
{
    float3 atmos = atmoColor * atmosphereStrength;
    // Add in atmosphere, reducing the "saturation" of the base color the more powerful the atmosphere is.
    col.xyz = atmos + col.xyz * (1 - saturate(length(atmos)));
    return col;
}