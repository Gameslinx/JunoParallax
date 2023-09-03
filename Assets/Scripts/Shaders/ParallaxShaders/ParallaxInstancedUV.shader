


Shader "Custom/ParallaxInstancedUV"
{
	Properties
	{
		_MainTex("_MainTex", 2D) = "white" {}

		_BumpMap("_BumpMap", 2D) = "bump" {}
		_Metallic("_Metallic", Range(0.001, 200)) = 0.2
		_Gloss("_Gloss", Range(0, 250)) = 0
		_MetallicTint("_MetallicTint", COLOR) = (0,0,0)

		_Hapke("_Hapke", Range(0.3, 5)) = 1

		_BumpScale("_BumpScale", Range(0, 10)) = 1
		_EmissionColor("_EmissionColor", Color) = (0,0,0,0)

		_CurrentTime("_CurrentTime", float) = 0
		_ThisPos("_ThisPos", vector) = (0,0,0)
		_ShaderOffset("_ShaderOffset", vector) = (0,0,0)

		_FresnelPower("_FresnelPower", Range(0.001, 20)) = 1
		_FresnelColor("_FresnelColor", COLOR) = (0,0,0)

		_Color("_Color", COLOR) = (0,0,0)

	}
	SubShader
	{
		//Cull Off
		Tags {"RenderType" = "Opaque" }
		Pass
		{
			Tags { "LightMode" = "ForwardBase" }
			CGPROGRAM
			#pragma vertex vert		
			#pragma fragment pixel_shader
			#pragma multi_compile_fwdbase
			#include "ParallaxUtilsUV.cginc"

			float3 _FresnelColor;
            float _FresnelPower;
			float2 _BumpMap_ST;
			float _BumpScale;

			v2f vert(appdata_t i, uint instanceID: SV_InstanceID)
			{
				v2f o;
				UNITY_INITIALIZE_OUTPUT(v2f, o);
				float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);

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
				
				#if ATMOSPHERE
                    o.atmosColor = GetAtmosphereDataForVertex(o.world_vertex, o.lightDir, _PlanetOrigin, _LightColor0);
                #endif
				o.up = PARALLAX_UP_VECTOR(mat);
				TRANSFER_VERTEX_TO_FRAGMENT(o); //
				return o;
			}
			float3 pixel_shader(v2f i) : SV_TARGET
			{
				if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

				i.worldNormal = normalize(i.worldNormal);

				fixed4 surfaceCol = tex2D(_MainTex, i.uv.xy * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

				
				float3 normalMap = normalize(UnpackNormal(tex2D(_BumpMap, i.uv * _BumpMap_ST))) * _BumpScale;
				normalMap.z = sqrt(1.0 - saturate(dot(normalMap.xy, normalMap.xy)));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);
                float3 worldNormal = (mul(TBN, normalMap));

				float attenuation = PARALLAX_LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.rgb;

				float4 color = BlinnPhongAlbedo(worldNormal, i.worldNormal, i.up, surfaceCol, normalize(i.lightDir), normalize(i.viewDir), attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0));
                color.rgb += fresnelCol;
				#if ATMOSPHERE
                    color.rgb = ApplyAtmoColor(i.atmosColor, 1, color.rgb);
                #endif
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
				UNITY_INITIALIZE_OUTPUT(shadow_v2f, o);
                float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, v.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);
                
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

                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
        }
		Pass
		{
			Tags { "LightMode" = "ForwardAdd" }
			Blend One OneMinusSrcAlpha
			CGPROGRAM
			#pragma vertex vert		
			#pragma fragment pixel_shader
			#pragma multi_compile_lightpass
			#include "ParallaxUtilsUV.cginc"

			float3 _FresnelColor;
            float _FresnelPower;
			float2 _BumpMap_ST;
			float _BumpScale;

			v2f_lighting vert(appdata_t i, uint instanceID: SV_InstanceID)
			{
				v2f_lighting o;
				UNITY_INITIALIZE_OUTPUT(v2f_lighting, o);
				float4x4 mat = _Properties[instanceID].mat;
                float4 pos = mul(mat, i.vertex) + float4(_ShaderOffset, 0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);

                o.pos = UnityObjectToClipPos(pos);
                o.color = 1;//_Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(mat, i.normal));
                o.world_vertex = world_vertex;
                o.tangentWorld = normalize(mul(mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                o.viewDir = normalize(_WorldSpaceCameraPos.xyz - o.world_vertex.xyz);
                o.lightDir = normalize(_WorldSpaceLightPos0.xyz - o.world_vertex.xyz);

				return o;
			}
			float3 pixel_shader(v2f_lighting i) : SV_TARGET
			{
				if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				discard;

				i.worldNormal = normalize(i.worldNormal);

				fixed4 surfaceCol = tex2D(_MainTex, i.uv.xy * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

				
				float3 normalMap = normalize(UnpackNormal(tex2D(_BumpMap, i.uv * _BumpMap_ST))) * _BumpScale;
				normalMap.z = sqrt(1.0 - saturate(dot(normalMap.xy, normalMap.xy)));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);
                float3 worldNormal = (mul(TBN, normalMap));

				// Prevent build errors - Seems like a silly unity bug
				float attenuation = 1;
				#if defined (SHADOWS_SCREEN)
				{
					attenuation = 1;
				}
				#else
				{
					UNITY_LIGHT_ATTENUATION(atten, i, i.world_vertex.xyz);
					attenuation = atten;
				}
				#endif
				
				//float attenuation = 1;

                float3 attenColor = attenuation * _LightColor0.rgb;

				float4 color = BlinnPhongLight(worldNormal, i.worldNormal, surfaceCol, normalize(i.lightDir), normalize(i.viewDir), attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0));
                color.rgb += fresnelCol;
                return float4(color.rgb * attenuation, attenuation);
			}
			ENDCG

		}
	}
}