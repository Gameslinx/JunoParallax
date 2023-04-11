﻿Shader "Custom/InstancedCutoutEmissive" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "white" {}
        _EmissionTex("Emission Tex", 2D) = "black" {}
        _EmissionColor("Emission Color", COLOR) = (1,1,1)
        _EmissionStrength("Emission Strength", float) = 1
        _Color("Color", COLOR) = (0,0,0)
        _Cutoff("_Cutoff", Range(0, 1)) = 0.5
        _MaxBrightness("_MaxBrightness", float) = 1
        _WindMap("_WindMap", 2D) = "white" {}
        _WorldSize("_WorldSize", vector) = (0,0,0)
        _WindSpeed("Wind Speed", vector) = (1, 1, 1, 1)
        _WaveSpeed("Wave Speed", float) = 1.0
        _WaveAmp("Wave Amp", float) = 1.0
        _HeightCutoff("Height Cutoff", Range(-1, 1)) = -100
        _HeightFactor("HeightFactor", Range(0, 4)) = 1
        _Shininess("_Shininess", Range(0.001, 100)) = 1
        _SpecColor("_SpecColor", COLOR) = (1,1,1)
        _PlanetOrigin("_PlanetOrigin", vector) = (0,0,0)
            _ShaderOffset("_ShaderOffset", vector) = (0,0,0)
    }
    SubShader
    {
            //Tags{ "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        ZWrite On
        //Cull Off
            Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout"}
            //Tags { "RenderType" = "Opaque"}

        Pass 
        {

            Tags{ "LightMode" = "ForwardBase" }
            //Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"
            #include "GrassUtils.cginc"
            #include "noiseSimplex.cginc"
             #include "UnityCG.glslinc"
            
            sampler2D _MainTex;
            float2 _MainTex_ST;
            sampler2D _BumpMap;
            float _Cutoff;
            float _MaxBrightness;
            sampler2D _WindMap;
            sampler2D _EmissionTex;
            float4 _WindSpeed;
            float _WaveSpeed;
            float _WaveAmp;
            float _HeightCutoff;
            float _HeightFactor;
            float3 _PlanetOrigin;
            float3 _ShaderOffset;
            float _EmissionStrength;
            float3 _EmissionColor;

            struct appdata_t 
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f 
            {
                float4 vertex   : SV_POSITION;
                float4 pos : TEXCOORD3;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float3 worldNormal : TEXCOORD1;
                float3 world_vertex : TEXCOORD2;

                float3 tangentWorld: TEXCOORD6;
                float3 binormalWorld: TEXCOORD7;


                LIGHTING_COORDS(3, 4)
            };

            struct MeshProperties 
            {
                float4x4 mat;
                float4 color;
            };

            StructuredBuffer<MeshProperties> _Properties;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4 pos = mul(_Properties[instanceID].mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);
                float3 bf = normalize(abs(normalize(world_vertex - _PlanetOrigin)));
                bf /= dot(bf, (float3)1);
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
                
                float heightFactor = i.vertex.y > _HeightCutoff;
                heightFactor = heightFactor * (pow(i.vertex.y, _HeightFactor));
                if (i.vertex.y < 0)
                {
                    heightFactor = 0;
                }
                
                float2 windSample = -tex2Dlod(_WindMap, float4(wind, 0, 0));
                
                //wind = -windSample;
                
                float3 positionOffset =  mul(unity_ObjectToWorld, float3(windSample.x, 0, windSample.y));//mul(float3(windSample.x, 0, windSample.y), unity_ObjectToWorld);
                
                pos.xyz += sin(_WaveSpeed * positionOffset) * heightFactor;

                o.vertex = UnityObjectToClipPos(pos);
                o.color = _Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(_Properties[instanceID].mat, i.normal));
                o.world_vertex = mul(_Properties[instanceID].mat, i.vertex); 
                o.pos = o.vertex;
                o.tangentWorld = normalize(mul(_Properties[instanceID].mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));
                //o.wind = wind;
                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * i.color * _Color;
                clip(col.a - _Cutoff);
                float4 emission = tex2D(_EmissionTex, i.uv * _MainTex_ST);
                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normalMap);
                worldNormal = mul(unity_ObjectToWorld, worldNormal);
                i.worldNormal = normalize(worldNormal);
                //return col;
                float4 color = BlinnPhong(i.worldNormal, i.world_vertex, col);
                float atten = saturate(LIGHT_ATTENUATION(i) + UNITY_LIGHTMODEL_AMBIENT.rgb);
                color.rgb *= atten;
                //clip(color.rgb - _Cutoff);
                return float4(color + (emission * float4(_EmissionColor, 1) * _EmissionStrength));
            }

            ENDCG
        }
        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
#include "UnityCG.cginc"
            struct appdata_t
            {
                float4 vertex   : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 pos : TEXCOORD3;
                float3 normal : NORMAL;
                float3 worldNormal : TEXCOORD1;
                float3 world_vertex : TEXCOORD2;
                float2 uv : TEXCOORD0;
            };
            struct MeshProperties
            {
                float4x4 mat;
                float4 color;
            };
            StructuredBuffer<MeshProperties> _Properties;
            sampler2D _WindMap;
            float4 _WindSpeed;
            float _WaveSpeed;
            float _WaveAmp;
            float _HeightCutoff;
            float _HeightFactor;
            float3 _PlanetOrigin;
            float _Cutoff;
            sampler2D _MainTex;
            float2 _MainTex_ST;
            float4 _Color;
            float3 _ShaderOffset;
            v2f vert(appdata_t v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float4 pos = mul(_Properties[instanceID].mat, v.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);
                float3 bf = normalize(abs(normalize(world_vertex - _PlanetOrigin)));
                bf /= dot(bf, (float3)1);
                float2 xz = world_vertex.zx * bf.y;
                float2 xy = world_vertex.xy * bf.z;
                float2 zy = world_vertex.yz * bf.x;
                
                
                
                float2 samplePosXZ = xz;
                samplePosXZ += _Time.x * _WindSpeed.xz;
                samplePosXZ = (samplePosXZ)*_WaveAmp;
                
                float2 samplePosXY = xy;
                samplePosXY += _Time.x * _WindSpeed.xy;
                samplePosXY = (samplePosXY)*_WaveAmp;
                
                float2 samplePosZY = zy;
                samplePosZY += _Time.x * _WindSpeed.zy;
                samplePosZY = (samplePosZY)*_WaveAmp;
                
                float2 wind = (samplePosXZ + samplePosXY + samplePosZY) / 3;
                
                float heightFactor = v.vertex.y > _HeightCutoff;
                heightFactor = heightFactor * pow(v.vertex.y, _HeightFactor);
                if (v.vertex.y < 0)
                {
                    heightFactor = 0;
                }
                
                float2 windSample = -tex2Dlod(_WindMap, float4(wind, 0, 0));
                
                float3 positionOffset = mul(unity_ObjectToWorld, float3(windSample.x, 0, windSample.y));
                
                pos.xyz += sin(_WaveSpeed * positionOffset) * heightFactor;
                
                o.vertex = UnityObjectToClipPos(pos);
                o.normal = v.normal;
                o.worldNormal = normalize(mul(_Properties[instanceID].mat, v.normal));
                o.world_vertex = mul(_Properties[instanceID].mat, v.vertex); 
                o.pos = o.vertex;
                float clamped = min(o.pos.z, o.pos.w*UNITY_NEAR_CLIP_VALUE);
                o.pos.z = lerp(o.pos.z, clamped, unity_LightShadowBias.y);
                o.vertex = o.pos;
                //o.pos = UnityApplyLinearShadowBias(o.pos);
                o.uv = v.uv;

                
                return o;
                //vertex = mul(unity_ObjectToClipPos, vertex);
                //normal = mul(unity_ObjectToWorld, normal);
               //vertex = mul(UNITY_MATRIX_VP, mul(_Properties[instanceID].mat, vertex));
               //normal = mul(UNITY_MATRIX_VP, mul(_Properties[instanceID].mat, normal));
            }
        
            float4 frag(v2f i) : SV_Target
            {
                fixed4 texcol = tex2D(_MainTex, i.uv);
                clip(texcol.a - _Cutoff);
                return 0;
                //fixed4 texcol = tex2D(_MainTex, i.uv);
                //clip(texcol.a * _Color.a - _Cutoff);
                //
                //SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
    }
    Fallback "Cutout"
}