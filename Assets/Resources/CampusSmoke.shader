Shader "CampusEmergency/SmokeParticle"
{
    Properties {
        _MainTex ("Smoke sprite", 2D) = "white" {}
        _ClipBoundsEnabled ("Constrain smoke", Float) = 0
        _ClipMin ("World minimum", Vector) = (-10000,-10000,-10000,0)
        _ClipMax ("World maximum", Vector) = (10000,10000,10000,0)
        _ClipFade ("Boundary fade metres", Float) = .12
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float _ClipBoundsEnabled,_ClipFade;float4 _ClipMin,_ClipMax;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; float2 uv : TEXCOORD0; fixed4 color : COLOR; float3 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.world=mul(unity_ObjectToWorld,v.vertex).xyz;
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                fixed4 result=tex2D(_MainTex,i.uv)*i.color;
                if(_ClipBoundsEnabled>.5){
                    float3 edge=min(i.world-_ClipMin.xyz,_ClipMax.xyz-i.world);
                    float distance=min(edge.x,min(edge.y,edge.z));clip(distance);
                    result.a*=saturate(distance/max(_ClipFade,.001));
                }
                return result;
            }
            ENDCG
        }
    }
}
