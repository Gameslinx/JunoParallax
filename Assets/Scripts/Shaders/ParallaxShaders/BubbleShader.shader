Shader "Custom/Bubble2" {
    Properties
    {
        _Color("Color",Color) = (1,1,1,1)
        _MainTex("BubbleTexture",2D) = "white"{}
        _Metallic("Metallic",Range(0,1)) = 1
        _Shininess("Smoothness",Range(0,100)) = 1
        _SpecColor("SpecColor", COLOR) = (0,0,0)
        _TextureAlpha("TextureAlpha",Range(0,1)) = 0.5
        _Alpha("Alpha",Range(0,1)) = 1
        _Intensity("Saturation", Range(0, 10)) = 1
        _WobbleIntensity("Wobble Intensity", Range(0, 10)) = 1
        _WobbleSpeed("Wobble Speed", Range(0, 10)) = 1
        _MaxYOffset("Max Y Offset", Range(0, 100)) = 1
        _BobSpeed("Bob Speed", Range(0, 100)) = 1
        _NoiseScale("Noise scale", Range(0.001, 2000)) = 1
        _OffsetNoiseScale("Offset noise scale", Range(0.001, 2000)) = 1
        _PlanetOrigin("Planet Origin", vector) = (0,0,0)
        _ShaderOffset("_ShaderOffset", vector) = (0,0,0)
    }
        SubShader
        {
            //Tags{ "RenderType" = "Opaque" "LightMode" = "ForwardBase" }
        //ZTest Always
        ZWrite Off
        //Cull Off

        //Pass {
        //        //ZWrite On
        //        ColorMask A
        //    }
        //Cull Off
            //Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
            Tags { "Queue" = "Transparent" "IgnoreProjector" = "False" "RenderType" = "Transparent" }

        Pass
        {
            
            Tags{ "LightMode" = "ForwardBase" }
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Max
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #include "AutoLight.cginc"
            #include "GrassUtils.cginc"
            #include "noiseSimplex.cginc"
            
            sampler2D _MainTex;
            float2 _MainTex_ST;
            float _Metallic;
            float4 _EmissionColor;
            float _TextureAlpha;
            float _Alpha;
            float _Intensity;
            float _WobbleIntensity;
            float _WobbleSpeed;
            float _MaxYOffset;
            float _BobSpeed;
            float _NoiseScale;
            float _OffsetNoiseScale;
            float3 _PlanetOrigin;
            float3 _ShaderOffset;

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
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
                float3 viewDir : TEXCOORD5;
            };

            struct MeshProperties
            {
                float4x4 mat;
                float4 color;
            };

            StructuredBuffer<MeshProperties> _Properties;

            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;

                
                
                //Object position is i.vertex
                float4 objectPos = mul(_Properties[instanceID].mat, float3(1.99,0,1.25));
                float3 worldPos = mul(unity_ObjectToWorld, objectPos) + _ShaderOffset;
                float3 worldPos2 = worldPos - _ShaderOffset;
                float3 planetNormal = normalize(worldPos - _PlanetOrigin);
                float dist = distance(float3(i.vertex.x, 0, i.vertex.z), float3(0, 0, 0)) / 10;
                float initialYOffset = snoise(worldPos2 / _OffsetNoiseScale) * 0.5 + 0.5;
                float offsetY = (sin((_Time.x * 1 * frac(worldPos2.x * worldPos2.z) * _BobSpeed * initialYOffset))) * initialYOffset + 0.55;
                
                
                float noise = snoise(i.vertex.xyz / _NoiseScale + (_Time.x * worldPos2));
                
                i.vertex.xyz += sin(_Time.x * frac(worldPos2.x * worldPos2.z) * 100 * _WobbleSpeed) * (noise) / 10 * i.normal * _WobbleIntensity;
                
                
                //i.vertex.y += initialYOffset;
                float4 pos = mul(_Properties[instanceID].mat, i.vertex) + float4(_ShaderOffset, 0);
                pos.xyz += ((offsetY * 5) * _MaxYOffset * initialYOffset) * planetNormal + float3(0, 0 ,0);
                float3 world_vertex = mul(unity_ObjectToWorld, pos);
                o.viewDir = normalize(_WorldSpaceCameraPos - world_vertex);
                
                o.vertex = UnityObjectToClipPos(pos);
                o.color = _Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(_Properties[instanceID].mat, i.normal));
                o.world_vertex = world_vertex;//mul(_Properties[instanceID].mat, i.vertex); 
                o.pos = o.vertex;
                return o;
            }

            fixed4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {

                float timeLim = i.color.a + 0.75;
				float ditherFactor = (_Time.y - i.color.a) / (timeLim - i.color.a);
				if (InterleavedGradientNoise(i.pos.xy + i.pos.z) > ditherFactor)
			    {
                    discard;
                }
                float dist = distance(i.world_vertex, _WorldSpaceCameraPos);
                float colMult = 1 - saturate(dist / 20000);
                float3 NdotL = dot(i.worldNormal , i.viewDir);
                float3 Rim = (1 - NdotL) * _Color;
                NdotL = (NdotL + 1) / 2;
                i.uv = pow(pow((i.uv - 0.5), 2), 0.5) + 0.5;
                float4 Bubble = lerp(0,tex2D(_MainTex, NdotL * _MainTex_ST * i.uv), _TextureAlpha);
                Bubble *= _Intensity;
                //_Offset = _Intensity;
                Bubble -= _Intensity / 2;
                float lighting = 1;//BlinnPhongBubble(_WorldSpaceLightPos0, normalize(i.world_vertex - _PlanetOrigin));
                //return lighting;
                //return lighting;
                //return float4(cubeSample, 1);
                //return float4(NdotL.x, NdotL.y, NdotL.z, 1);

                return float4((Bubble * 0.5 * _Color.rgb + Rim + 0.5) * colMult, _Alpha * colMult) * lighting;
            }

            ENDCG
        }
    }

}