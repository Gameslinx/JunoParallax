Shader "Custom/InstancedIndirectColor" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "blue" {}
        _Color("Color", COLOR) = (0,0,0)
        _Cutoff("_Cutoff", Range(0, 1)) = 0.5
        _WindMap("_WindMap", 2D) = "white" {}
        _WorldSize("_WorldSize", vector) = (0,0,0)
        _WindSpeed("Wind Speed", vector) = (1, 1, 1, 1)
        _WaveSpeed("Wave Speed", float) = 1.0
        _WaveAmp("Wave Amp", float) = 1.0
        _HeightCutoff("Height Cutoff", Range(-1, 1)) = -100
        _HeightFactor("HeightFactor", Range(0, 4)) = 1
        _Metallic("_Metallic", Range(0.001, 100)) = 1
        _MetallicTint("_MetallicTint", COLOR) = (1,1,1)
        _Gloss("_Gloss", Range(0, 250)) = 0
        _Hapke("_Hapke", Range(0.3, 5)) = 1
        _PlanetOrigin("_PlanetOrigin", vector) = (0,0,0)
        _ShaderOffset("_ShaderOffset", vector) = (0,0,0)

        _FresnelPower("_FresnelPower", Range(0.001, 20)) = 1
		_FresnelColor("_FresnelColor", COLOR) = (0,0,0)

        _DitherFactor("_DitherFactor", Range(0, 1)) = 1
        _InitialTime("_InitialTime", float) = 0
        _CurrentTime("_CurrentTime", float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque"}

        Pass 
        {

            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "ParallaxUtilsUV.cginc"

            float3 _FresnelColor;
            float _FresnelPower;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;
                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);

                pos.xyz += Wind(mat, world_vertex, i.vertex.y);

                o.pos = UnityObjectToClipPos(pos);
                o.color = 1;//_Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(mat, i.normal));
                o.world_vertex = world_vertex; 
                o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0.xyz);

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            float4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), i.worldNormal);
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normalMap);

                float attenuation = PARALLAX_LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.rgb;

                float4 color = BlinnPhong(worldNormal, i.worldNormal, col, i.lightDir, i.viewDir, attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0));

                color.rgb += fresnelCol;
                return float4(color);
            }

            ENDCG
        }
        Pass
        {
            Tags{ "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "ParallaxUtilsUV.cginc"

            v2f vert(appdata_t v, uint instanceID : SV_InstanceID)
            {
                v2f o;
                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, v.vertex) + float4(_ShaderOffset.xyz, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, v.vertex);
                
                pos.xyz += Wind(mat, world_vertex, v.vertex.y);
        
                o.pos = UnityObjectToClipPos(pos);
                o.pos = UnityApplyLinearShadowBias(o.pos);
                float clamped = min(o.pos.z, o.pos.w*UNITY_NEAR_CLIP_VALUE);
                o.pos.z = lerp(o.pos.z, clamped, unity_LightShadowBias.y);
                o.color = 1;//_Properties[instanceID].color;
                o.uv = v.uv;
                return o;
            }
        
            float4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
        Pass 
        {

            Tags{ "LightMode" = "ForwardAdd" }
            Blend One OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_lightpass
            #include "ParallaxUtilsUV.cginc"

            float3 _FresnelColor;
            float _FresnelPower;

            v2f_lighting vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f_lighting o;
                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);

                pos.xyz += Wind(mat, world_vertex, i.vertex.y);

                o.pos = UnityObjectToClipPos(pos);
                o.color = 1;//_Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(mat, i.normal));
                o.world_vertex = world_vertex; 
                o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0.xyz- o.world_vertex.xyz);

                return o;
            }

            float4 frag(v2f_lighting i, uint instanceID : SV_InstanceID) : SV_Target
            {
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), i.worldNormal);
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normalMap);

                UNITY_LIGHT_ATTENUATION(attenuation, i, i.world_vertex.xyz);

                float3 attenColor = attenuation * _LightColor0.rgb;

                float4 color = BlinnPhong(worldNormal, i.worldNormal, col, i.lightDir, i.viewDir, attenColor);

                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0));

                color.rgb += fresnelCol;
                return float4(color.rgb * attenuation, attenuation);
            }

            ENDCG
        }
    }
    //Fallback "Diffuse"
}