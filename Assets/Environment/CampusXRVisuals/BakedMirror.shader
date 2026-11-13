Shader "CampusEmergency/BakedMirror"
{
    Properties
    {
        [NoScaleOffset] _Cube ("Baked Room Reflection", Cube) = "" {}
        _Tint ("Silver Tint", Color) = (.94,.96,.97,1)
        [HideInInspector] _Cube_HDR ("HDR Decode", Vector) = (1,1,0,0)
        [HideInInspector] _ProbePosition ("Capture Position", Vector) = (0,0,0,0)
        [HideInInspector] _BoxMin ("Room Minimum", Vector) = (-1,-1,-1,0)
        [HideInInspector] _BoxMax ("Room Maximum", Vector) = (1,1,1,0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            samplerCUBE _Cube;
            half4 _Cube_HDR, _Tint;
            float4 _ProbePosition, _BoxMin, _BoxMax;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 position : TEXCOORD0;
                float3 normal : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            v2f vert(appdata input)
            {
                v2f output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(v2f,output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.vertex=UnityObjectToClipPos(input.vertex);
                output.position=mul(unity_ObjectToWorld,input.vertex).xyz;
                output.normal=UnityObjectToWorldNormal(input.normal);
                return output;
            }
            half4 frag(v2f input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float3 direction=reflect(-normalize(UnityWorldSpaceViewDir(input.position)),normalize(input.normal));
                // Project into the captured room box, rather than reflecting an infinitely distant sky.
                float3 safeDirection=sign(direction+1e-7)*max(abs(direction),1e-5);
                float3 face=lerp(_BoxMin.xyz,_BoxMax.xyz,step(0,direction));
                float3 distance=(face-input.position)/safeDirection;
                float hit=min(distance.x,min(distance.y,distance.z));
                float3 sampleDirection=input.position+direction*max(hit,0)-_ProbePosition.xyz;
                half3 reflection=DecodeHDR(texCUBE(_Cube,sampleDirection),_Cube_HDR);
                return half4(reflection*_Tint.rgb,1);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
