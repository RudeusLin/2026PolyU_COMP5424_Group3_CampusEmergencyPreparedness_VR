Shader "CampusEmergency/ArchitecturalSurface"
{
    Properties
    {
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _MainTex ("Surface", 2D) = "white" {}
        _DetailTex ("Occlusion / Height / Smoothness", 2D) = "white" {}
        _BumpMeters ("Relief In Meters", Range(0, 0.02)) = 0.002
        _TileMeters ("Texture Size In Meters", Float) = 1
        _Metallic ("Metallic", Range(0, 1)) = 0
        _Glossiness ("Smoothness", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200
        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _DetailTex;
        fixed4 _Color;
        half _Metallic;
        half _Glossiness;
        float _TileMeters;
        float _BumpMeters;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf(Input input, inout SurfaceOutputStandard output)
        {
            float3 normal = normalize(WorldNormalVector(input, float3(0, 0, 1)));
            float3 weights = pow(abs(normal), 4);
            weights /= max(weights.x + weights.y + weights.z, 0.0001);
            float3 uv = input.worldPos / max(_TileMeters, 0.01);
            fixed3 surface = tex2D(_MainTex, uv.zy).rgb * weights.x
                + tex2D(_MainTex, uv.xz).rgb * weights.y
                + tex2D(_MainTex, uv.xy).rgb * weights.z;
            half3 detail = tex2D(_DetailTex, uv.zy).rgb * weights.x
                + tex2D(_DetailTex, uv.xz).rgb * weights.y
                + tex2D(_DetailTex, uv.xy).rgb * weights.z;
            // Surface gradients keep the physical relief scale consistent on each axis.
            float3 dx = ddx(input.worldPos), dy = ddy(input.worldPos);
            float3 rx = cross(dy, normal), ry = cross(normal, dx);
            float determinant = dot(dx, rx);
            float3 bumped = normal;
            if (abs(determinant) > 0.00000001)
                bumped = normalize(normal - _BumpMeters * (ddx(detail.g) * rx + ddy(detail.g) * ry) / determinant);
            output.Normal = float3(dot(bumped, normalize(WorldNormalVector(input, float3(1, 0, 0)))),
                dot(bumped, normalize(WorldNormalVector(input, float3(0, 1, 0)))),
                dot(bumped, normal));
            output.Albedo = surface * _Color.rgb;
            output.Metallic = _Metallic;
            output.Smoothness = _Glossiness * detail.b;
            output.Occlusion = detail.r;
            output.Alpha = 1;
        }
        ENDCG
    }
    FallBack "Standard"
}
