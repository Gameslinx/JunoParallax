Shader "Custom/DepthShader" {
  Properties {
    _MainTex("Texture", 2D) = "white" {}
  }

  SubShader {
    Pass {
      CGPROGRAM
      #pragma vertex vert_img
      #pragma fragment frag
      #include "UnityCG.cginc" // required for v2f_img

      // Properties
      sampler2D _MainTex;
      UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

      float4 frag(v2f_img input) : COLOR {
        // sample texture for color
        float distFromCenter = distance(input.uv.xy, float2(0.5, 0.5));
        return tex2D(_CameraDepthTexture, input.uv.xy).r;
        return float4(distFromCenter, distFromCenter, distFromCenter, 1.0);
      }
      ENDCG
}}}