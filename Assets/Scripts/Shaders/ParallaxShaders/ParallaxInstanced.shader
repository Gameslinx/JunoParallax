// Upgrade NOTE: replaced 'defined SPOT' with 'defined (SPOT)'

// Upgrade NOTE: replaced '_LightMatrix0' with 'unity_WorldToLight'

// Upgrade NOTE: replaced '_LightMatrix0' with 'unity_WorldToLight'




Shader "Custom/ParallaxInstanced"
{
	Properties
	{
		_MainTex("_MainTex", 2D) = "white" {}
		[NoScaleOffset] _InfluenceMap("_InfluenceMap", 2D) = "white" {}

		[NoScaleOffset] _BumpMap("_BumpMap", 2D) = "bump" {}
		_EdgeBumpMap("_EdgeBumpMap", 2D) = "bump" {}
		_Metallic("_Metallic", Range(0.001, 20)) = 0.2
		_Gloss("_Gloss", Range(0, 250)) = 0
		_MetallicTint("_MetallicTint", COLOR) = (0,0,0)
		_Color("_Color", COLOR) = (1,1,1)
		_FresnelPower("_FresnelPower", Range(0.001, 20)) = 1
		_FresnelColor("_FresnelColor", COLOR) = (0,0,0)
		_PlanetOrigin("_PlanetOrigin", vector) = (0,0,0)

		_NormalSpecularInfluence("_NormalSpecularInfluence", Range(0, 1)) = 1
		_Hapke("_Hapke", Range(0.3, 5)) = 1

		_MainTexUVs("_MainTexUVs", vector) = (0,0,0)
		_BumpScale("_BumpScale", Range(0, 10)) = 1
		_EmissionColor("_EmissionColor", Color) = (0,0,0,0) 

		_ShaderOffset("_ShaderOffset", vector) = (0,0,0)

		_CurrentTime("_CurrentTime", float) = 0

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
			#pragma multi_compile EMISSION_ON EMISSION_OFF
			
			#include "ParallaxUtilsBiplanar.cginc"

			uniform sampler2D _EdgeBumpMap;
			float4 _EdgeBumpMap_ST;
			float3 _MainTexUVs;
			float _BumpScale;
			float _FresnelPower;
			float3 _FresnelColor;

			v2f vert(appdata_t i, uint instanceID: SV_InstanceID)
			{
				v2f o;
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
				
				TRANSFER_VERTEX_TO_FRAGMENT(o);
				return o;
			}
			float3 pixel_shader(v2f i) : SV_TARGET
			{
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				{
					discard;
				}
					
				i.worldNormal = normalize(i.worldNormal);
				float cameraDist = distance(_WorldSpaceCameraPos, i.world_vertex);

				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = ZoomLevel;

				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				float percentage = ZoomLevel - ClampedZoomLevel;
				percentage = pow(percentage, 3);

				float3 dpdx = ddx(i.world_vertex);
				float3 dpdy = ddy(i.world_vertex);
				float3x3 biplanarCoords = GetBiplanarCoordinates(i.world_vertex , i.worldNormal);

				fixed4 surfaceCol = BiplanarTexture_float(_MainTex, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _MainTexUVs, biplanarCoords, i.world_vertex - _ShaderOffset, i.worldNormal, _MainTex_ST) * float4(i.color.rgb, 1) * _Color;
				float4 surfaceNormal = BiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _MainTexUVs, biplanarCoords, i.world_vertex - _ShaderOffset, i.worldNormal, _MainTex_ST);

				float3 normalMap = normalize(UnpackNormal(tex2D(_EdgeBumpMap, i.uv))) * _BumpScale;

				normalMap.z = sqrt(1.0 - saturate(dot(normalMap.xy, normalMap.xy)));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);
                float3 worldNormal = (mul(TBN, normalMap));
				
				worldNormal = BlendNormals(surfaceNormal, normalMap);

				float attenuation = PARALLAX_LIGHT_ATTENUATION(i);
                float3 attenColor = attenuation * _LightColor0.rgb;

				float4 color = BlinnPhong(worldNormal, i.worldNormal, surfaceCol, normalize(i.lightDir), normalize(i.viewDir), attenColor);
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

            float _Cutoff;
          
            shadow_v2f vert(shadow_appdata_t v, uint instanceID : SV_InstanceID)
            {
                shadow_v2f o;
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
			Tags { "LightMode" = "ForwardAdd"  }
			Blend One OneMinusSrcAlpha

			CGPROGRAM
			#pragma vertex vert		
			#pragma fragment pixel_shader
			#pragma multi_compile_lightpass
			#pragma multi_compile EMISSION_ON EMISSION_OFF
			
			#include "ParallaxUtilsBiplanar.cginc"

			uniform sampler2D _EdgeBumpMap;
			float4 _EdgeBumpMap_ST;
			float3 _MainTexUVs;
			float _BumpScale;
			float _FresnelPower;
			float3 _FresnelColor;

			v2f_lighting vert(appdata_t i, uint instanceID: SV_InstanceID)
			{
				v2f_lighting o;
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
			float4 pixel_shader(v2f_lighting i) : SV_TARGET
			{
                if (InterleavedGradientNoise(i.color.a, i.pos.xy + i.pos.z))
				{
					discard;
				}
					
				i.worldNormal = normalize(i.worldNormal);
				float cameraDist = distance(_WorldSpaceCameraPos, i.world_vertex);

				float ZoomLevel = GetZoomLevel(cameraDist);
				int ClampedZoomLevel = ZoomLevel;

				float uvDistortion = pow(2, ClampedZoomLevel - 1);
				float nextUVDist = pow(2, ClampedZoomLevel);
				float percentage = ZoomLevel - ClampedZoomLevel;
				percentage = pow(percentage, 3);

				float3 dpdx = ddx(i.world_vertex);
				float3 dpdy = ddy(i.world_vertex);
				float3x3 biplanarCoords = GetBiplanarCoordinates(i.world_vertex , i.worldNormal);

				fixed4 surfaceCol = BiplanarTexture_float(_MainTex, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _MainTexUVs, biplanarCoords, i.world_vertex - _ShaderOffset, i.worldNormal, _MainTex_ST) * float4(i.color.rgb, 1) * _Color;
				float4 surfaceNormal = BiplanarNormal_float(_BumpMap, dpdx, dpdy, uvDistortion, nextUVDist, percentage, _MainTexUVs, biplanarCoords, i.world_vertex - _ShaderOffset, i.worldNormal, _MainTex_ST);

				float3 normalMap = normalize(UnpackNormal(tex2D(_EdgeBumpMap, i.uv))) * _BumpScale;

				normalMap.z = sqrt(1.0 - saturate(dot(normalMap.xy, normalMap.xy)));
                float3x3 TBN = float3x3(normalize(i.tangentWorld), normalize(i.binormalWorld), normalize(i.worldNormal));
                TBN = transpose(TBN);
                float3 worldNormal = (mul(TBN, normalMap));
				
				worldNormal = BlendNormals(surfaceNormal, normalMap);

				UNITY_LIGHT_ATTENUATION(attenuation, i, i.world_vertex.xyz);
                float3 attenColor = attenuation * _LightColor0.rgb;

				float4 color = BlinnPhong(worldNormal, i.worldNormal, surfaceCol, normalize(i.lightDir), normalize(i.viewDir), attenColor);
                float3 fresnelCol = Fresnel(worldNormal, normalize(i.viewDir), _FresnelPower, _FresnelColor) * saturate(dot(i.worldNormal, _WorldSpaceLightPos0));
                color.rgb += fresnelCol;
                return float4(color.rgb * attenuation, attenuation);
			}
			ENDCG
		}
	}
}