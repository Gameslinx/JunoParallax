Shader "Custom/InstancedCutout" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "bump" {}
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
        _Metallic("_Metallic", Range(0.001, 100)) = 1
        _Hapke("_Hapke", Range(0.3, 5)) = 1
        _MetallicTint("_MetallicTint", COLOR) = (1,1,1)
        _Gloss("_Gloss", Range(0, 250)) = 0
        _PlanetOrigin("_PlanetOrigin", vector) = (0,0,0)
        _ShaderOffset("_ShaderOffset", vector) = (0,0,0)
        _DitherFactor("_DitherFactor", Range(0, 1)) = 1
        _InitialTime("_InitialTime", float) = 0
        _CurrentTime("_CurrentTime", float) = 0
        _FresnelPower("_FresnelPower", Range(0.001, 20)) = 1
		_FresnelColor("_FresnelColor", COLOR) = (0,0,0)
        _Transmission("_Transmission", Range(-1.0, 1.0)) = 0.0
    }
    SubShader
    {
        ZWrite On
        Cull Off
        Tags {"Queue" = "AlphaTest" "IgnoreProjector" = "True" "RenderType" = "TransparentCutout"}
        Pass
        {
            Tags{ "LightMode" = "ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "ParallaxUtilsUV.cginc"
            
            float _Cutoff;
            float3 _FresnelColor;
            float _FresnelPower;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = pos.xyz;

                pos.xyz += Wind(mat, world_vertex, i.vertex.y);

                o.pos = UnityObjectToClipPos(pos);
                o.color = 1;//_Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(mat, i.normal));
                o.world_vertex = world_vertex; 
                o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir =  normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0.xyz);

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                return o;
            }

            fixed4 frag(v2f i, float facing : VFACE) : SV_Target
            {
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

                float faceSign = ( facing >= 0 ? 1 : -1 );
                i.worldNormal *= lerp(faceSign, 1, _Transmission);  //Get subtle shading. Transmission of 1 means lighting on both sides of face

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;
                clip(col.a - _Cutoff);

                float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), i.worldNormal);
                TBN = transpose(TBN);
                float3 worldNormal = mul(TBN, normalMap);

                float attenuation = PARALLAX_LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.rgb;

                //float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.world_vertex.xyz);
                float4 color = BlinnPhong(worldNormal, i.worldNormal, col, i.lightDir, i.viewDir, attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0)) * attenColor;
                color.rgb += fresnelCol;
                //return 0;
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

            float _Cutoff;
          
            shadow_v2f vert(shadow_appdata_t v, uint instanceID : SV_InstanceID)
            {
                shadow_v2f o;
                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, v.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);
               
                pos.xyz += Wind(mat, world_vertex, v.vertex.y);
                
                o.pos = UnityObjectToClipPos(pos);
                float clamped = min(o.pos.z, o.pos.w*UNITY_NEAR_CLIP_VALUE);
                o.pos.z = lerp(o.pos.z, clamped, unity_LightShadowBias.y);
                o.uv = v.uv;
                o.color = 1;//_Properties[instanceID].color;
                
                return o;
            }
        
            float4 frag(shadow_v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

                fixed4 texcol = tex2D(_MainTex, i.uv);
                clip(texcol.a - _Cutoff);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
        //Pass
        //{
        //    Tags{ "LightMode" = "ForwardAdd" }
        //    Blend One OneMinusSrcAlpha
        //    CGPROGRAM
        //    #pragma vertex vert
        //    #pragma fragment frag
        //    #pragma multi_compile_lightpass
        //    #include "ParallaxUtilsUV.cginc"
        //    
        //    float _Cutoff;
        //    float3 _FresnelColor;
        //    float _FresnelPower;
        //
        //    v2f_lighting vert(appdata_t i, uint instanceID: SV_InstanceID) {
        //        v2f_lighting o;
        //
        //        float4x4 mat = _Properties[instanceID].mat;
        //        float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
        //        float3 world_vertex = pos.xyz;
        //
        //        pos.xyz += Wind(mat, world_vertex, i.vertex.y);
        //
        //        o.pos = UnityObjectToClipPos(pos);
        //        o.color = 1;
        //        o.uv = i.uv;
        //        o.normal = i.normal;
        //        o.worldNormal = normalize(mul(mat, i.normal));
        //        o.world_vertex = world_vertex; 
        //        o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
        //        o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));
        //
        //        o.viewDir =  normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
        //        o.lightDir = normalize(_WorldSpaceLightPos0.xyz - o.world_vertex.xyz);
        //        return o;
        //    }
        //
        //    fixed4 frag(v2f_lighting i, float facing : VFACE) : SV_Target
        //    {
        //        if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
		//		discard;
        //
        //        float faceSign = ( facing >= 0 ? 1 : -1 );
        //        i.worldNormal *= lerp(faceSign, 1, _Transmission);  //Get subtle shading. Transmission of 1 means lighting on both sides of face
        //
        //        float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;
        //        clip(col.a - _Cutoff);
        //
        //        float3 normalMap = UnpackNormal(tex2D(_BumpMap, i.uv));
        //        float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), i.worldNormal);
        //        TBN = transpose(TBN);
        //        float3 worldNormal = mul(TBN, normalMap);
        //
        //        UNITY_LIGHT_ATTENUATION(attenuation, i, i.world_vertex.xyz);
        //        float3 attenColor = attenuation * _LightColor0.rgb;
        //        //float3 viewDirection = normalize(_WorldSpaceCameraPos.xyz - i.world_vertex.xyz);
        //        float3 editedNormal = normalize(_WorldSpaceLightPos0 - i.world_vertex);
        //
        //        float4 color = BlinnPhong(editedNormal, editedNormal, col, i.lightDir, i.viewDir, attenColor);
        //
        //        float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0)) * attenColor;
        //        color.rgb += fresnelCol;
        //        return float4(color.rgb * attenuation, attenuation);
        //    }
        //
        //    ENDCG
        //}
    }
    Fallback "Cutout"
}