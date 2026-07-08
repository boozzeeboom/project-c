Shader "ProjectC/TargetOutline"
{
    // Inverted-hull outline shader for URP.
    // Renders back faces extruded along vertex normals.
    // Apply as a second material on target's MeshRenderer/SkinnedMeshRenderer.
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1.0, 0.6, 0.0, 1.0)
        _OutlineWidth ("Outline Width", Range(0.0, 0.1)) = 0.02
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+1"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                // Extrude vertex along its object-space normal
                float3 extrudedPos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;

                VertexPositionInputs vertexInput = GetVertexPositionInputs(extrudedPos);
                OUT.positionCS = vertexInput.positionCS;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
