Shader "Custom/DayNightCubemapSkybox"
{
    Properties
    {
        _Tint ("Tint Color", Color) = (0.5, 0.5, 0.5, 0.5)
        [Gamma] _Exposure ("Exposure", Range(0, 8)) = 1.0
        _Rotation ("Day Horizontal Rotation (Y)", Range(0, 360)) = 0
        _Pitch ("Day Vertical Tilt (X)", Range(-30, 30)) = 0
        _NightRotation ("Night Horizontal Rotation (Y)", Range(0, 360)) = 0
        _NightPitch ("Night Vertical Tilt (X)", Range(-30, 30)) = 0
        _Blend ("Day/Night Blend (0=Day, 1=Night)", Range(0, 1)) = 0
        [NoScaleOffset] _Tex ("Day Cubemap (AllSkyFree)", Cube) = "grey" {}
        [NoScaleOffset] _NightTex ("Night Cubemap (AllSkyFree)", Cube) = "grey" {}
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            samplerCUBE _Tex;
            half4 _Tex_HDR;
            samplerCUBE _NightTex;
            half4 _NightTex_HDR;

            half4 _Tint;
            half _Exposure;
            float _Rotation;
            float _Pitch;
            float _NightRotation;
            float _NightPitch;
            half _Blend;

            float3 RotateAroundYInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(mul(m, vertex.xz), vertex.y).xzy;
            }

            float3 RotateAroundXInDegrees(float3 vertex, float degrees)
            {
                float alpha = degrees * UNITY_PI / 180.0;
                float sina, cosa;
                sincos(alpha, sina, cosa);
                float2x2 m = float2x2(cosa, -sina, sina, cosa);
                return float3(vertex.x, mul(m, vertex.yz));
            }

            struct appdata_t
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldDir : TEXCOORD0;
                float3 texcoordDay : TEXCOORD1;
                float3 texcoordNight : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            v2f vert(appdata_t v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                float3 dayDir = RotateAroundYInDegrees(v.vertex.xyz, _Rotation);
                dayDir = RotateAroundXInDegrees(dayDir, _Pitch);

                float3 nightDir = RotateAroundYInDegrees(v.vertex.xyz, _NightRotation);
                nightDir = RotateAroundXInDegrees(nightDir, _NightPitch);

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldDir = v.vertex.xyz;
                o.texcoordDay = dayDir;
                o.texcoordNight = nightDir;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 wDir = normalize(i.worldDir);
                float3 dDir = normalize(i.texcoordDay);
                float3 nDir = normalize(i.texcoordNight);

                // Prevent vertical tilt (_Pitch) from pulling the dark ground below the cubemap horizon into the upper sky
                if (wDir.y > 0.0)
                {
                    dDir.y = max(dDir.y, wDir.y * 0.25);
                    nDir.y = max(nDir.y, wDir.y * 0.25);
                    dDir = normalize(dDir);
                    nDir = normalize(nDir);
                }

                half4 texDay = texCUBE(_Tex, dDir);
                half3 cDay = DecodeHDR(texDay, _Tex_HDR);

                half4 texNight = texCUBE(_NightTex, nDir);
                half3 cNight = DecodeHDR(texNight, _NightTex_HDR);

                half3 c = lerp(cDay, cNight, _Blend);
                c = c * _Tint.rgb * unity_ColorSpaceDouble.rgb * _Exposure;
                return half4(c, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
