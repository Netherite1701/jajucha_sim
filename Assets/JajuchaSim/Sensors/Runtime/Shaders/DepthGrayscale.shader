Shader "Hidden/JajuchaSim/DepthGrayscale"
{
    // Depth shader for Jajucha camera depth sensor.
    // Outputs linear distance from camera as grayscale:
    //   nearer objects = brighter (closer to 255)
    //   farther objects = darker (closer to 0)
    //
    // Used as a replacement shader via Camera.SetReplacementShader().
    // No tags so it replaces ALL shaders regardless of RenderType.

    SubShader
    {
        Pass
        {
            Cull Back
            ZTest LEqual
            ZWrite On

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 worldPos : TEXCOORD0;
            };

            // Camera position in world space (set via shader property)
            float3 _CameraWorldPos;
            // Distance mapping range (in Unity units = cm)
            float _NearDistance;
            float _FarDistance;

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Calculate linear distance from camera
                float dist = distance(i.worldPos.xyz, _CameraWorldPos);

                // Map distance to grayscale value
                // nearer = brighter, farther = darker
                float range = max(_FarDistance - _NearDistance, 0.001);
                float normalized = 1.0 - saturate((dist - _NearDistance) / range);

                return fixed4(normalized, normalized, normalized, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
