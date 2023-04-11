Shader "Custom/InstancedAlphaBillboard" {
    Properties
    {
        _MainTex("Main Tex", 2D) = "white" {}
        _BumpMap("Bump Map", 2D) = "white" {}
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
        _DitherFactor("_DitherFactor", Range(0, 1)) = 1
        _InitialTime("_InitialTime", float) = 0
        _CurrentTime("_CurrentTime", float) = 0
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
            float4 _WindSpeed;
            float _WaveSpeed;
            float _WaveAmp;
            float _HeightCutoff;
            float _HeightFactor;
            float3 _PlanetOrigin;
            float3 _ShaderOffset;

            float _DitherFactor;
            float _InitialTime;
            float _CurrentTime;

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
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
                float3 worldNormal : TEXCOORD1;
                float3 world_vertex : TEXCOORD2;

                float3 tangentWorld: TEXCOORD6;
                float3 binormalWorld: TEXCOORD7;


                LIGHTING_COORDS(3, 4)
            };

            struct GrassData
            {
                float4x4 mat;
                float4 color;
            };

            StructuredBuffer<GrassData> _Properties;
            
            v2f vert(appdata_t i, uint instanceID: SV_InstanceID) {
                v2f o;
                AllAxisBillboard(i.vertex, _Properties[instanceID].mat);
                float4 pos = i.vertex;//mul(_Properties[instanceID].mat, i.vertex) + float4(_ShaderOffset, 0);
                
                float3 world_vertex = mul(unity_ObjectToWorld, pos.xyz);

                o.pos = UnityObjectToClipPos(pos);
                o.color = _Properties[instanceID].color;
                o.uv = i.uv;
                o.normal = i.normal;
                o.worldNormal = normalize(mul(_Properties[instanceID].mat, i.normal));
                o.world_vertex = mul(_Properties[instanceID].mat, i.vertex); 
                o.tangentWorld = normalize(mul(_Properties[instanceID].mat, i.tangent).xyz);
                o.binormalWorld = normalize(cross(o.worldNormal, o.tangentWorld));

                TRANSFER_VERTEX_TO_FRAGMENT(o);
                
                return o;
            }

            fixed4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
            {
                float timeLim = i.color.a + 0.75;
                float ditherFactor = (_Time.y - i.color.a) / (timeLim - i.color.a);
                if (InterleavedGradientNoise(i.pos.xy + i.pos.z) > ditherFactor)
				discard;

                float4 col = tex2D(_MainTex, i.uv * _MainTex_ST) * float4(i.color.rgb, 1) * _Color;

                clip(col.a - _Cutoff);
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
                return float4(color);
            }

            ENDCG
        }
        //Pass
        //{
        //    Tags{ "LightMode" = "ShadowCaster" }
        //    Cull Off
        //    CGPROGRAM
        //    #pragma vertex vert
        //    #pragma fragment frag
        //    #include "UnityCG.cginc"
        //    #include "GrassUtils.cginc"
        //    struct appdata_t
        //    {
        //        float4 vertex   : POSITION;
        //        float3 normal : NORMAL;
        //        float2 uv : TEXCOORD0;
        //    };
        //    struct v2f
        //    {
        //        float4 pos : SV_POSITION;
        //        float3 normal : NORMAL;
        //        float2 uv : TEXCOORD0;
        //        float4 color : COLOR;
        //    };
        //    struct GrassData
        //    {
        //        float4x4 mat;
        //        float4 color;
        //    };
        //    StructuredBuffer<GrassData> _Properties;
        //    sampler2D _WindMap;
        //    float4 _WindSpeed;
        //    float _WaveSpeed;
        //    float _WaveAmp;
        //    float _HeightCutoff;
        //    float _HeightFactor;
        //    float3 _PlanetOrigin;
        //    float _Cutoff;
        //    sampler2D _MainTex;
        //    float2 _MainTex_ST;
        //    float3 _ShaderOffset;
        //
        //    float _DitherFactor;
        //    float _InitialTime;
        //    float _CurrentTime;
        //
        //    v2f vert(appdata_t v, uint instanceID : SV_InstanceID)
        //    {
        //        v2f o;
        //        Billboard(v.vertex, _Properties[instanceID].mat);
        //        float4 pos = mul(_Properties[instanceID].mat, v.vertex) + float4(_ShaderOffset, 0);
        //        
        //        float3 world_vertex = mul(unity_ObjectToWorld, pos.xyz);
        //
        //        o.pos = UnityObjectToClipPos(pos);
        //        o.color = _Properties[instanceID].color;
        //        o.normal = v.normal;
        //        o.uv = v.uv;
        //        
        //        return o;
        //    }
        //
        //    float4 frag(v2f i, uint instanceID : SV_InstanceID) : SV_Target
        //    {
        //        float timeLim = i.color.a + 0.75;
        //        float ditherFactor = (_Time.y - i.color.a) / (timeLim - i.color.a);
        //        if (InterleavedGradientNoise(i.pos.xy + i.pos.z) > ditherFactor)
		//		discard;
        //
        //        fixed4 texcol = tex2D(_MainTex, i.uv);
        //        clip(texcol.a - _Cutoff);
        //        return 0;
        //    }
        //    ENDCG
        //}
    }
    Fallback "Cutout"
}