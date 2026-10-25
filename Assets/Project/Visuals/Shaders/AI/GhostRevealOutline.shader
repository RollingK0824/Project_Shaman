// A ghost revealed by the bell glows like a Minecraft "glowing" entity: only its outline, seen through walls.
// Added as an extra material on the ghost's renderers while it is revealed (AI_RevealOutline).
// Pass 1 marks the ghost's own pixels in the stencil, pass 2 draws a slightly swollen copy everywhere else,
// which leaves a band around the silhouette. Hard-edged meshes may show small gaps at their corners.
Shader "ProjectShaman/AI/GhostRevealOutline"
{
    Properties
    {
        [HDR] _OutlineColor("Outline Color", Color) = (1, 1, 1, 1)
        _OutlineWidth("Outline Width (m)", Range(0, 0.2)) = 0.04
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+100" "RenderType"="Transparent" }

        Pass
        {
            Name "RevealMask"
            Tags { "LightMode"="SRPDefaultUnlit" }
            ZWrite Off
            ZTest Always
            Cull Off
            ColorMask 0
            Stencil { Ref 64 WriteMask 64 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "RevealOutline"
            Tags { "LightMode"="UniversalForward" }
            ZWrite Off
            ZTest Always
            Cull Off
            Stencil { Ref 64 ReadMask 64 Comp NotEqual }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz) + TransformObjectToWorldNormal(input.normalOS) * _OutlineWidth;
                output.positionCS = TransformWorldToHClip(positionWS);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
