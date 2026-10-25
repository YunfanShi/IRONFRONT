Shader "IRONFRONT/Soft Billowing Smoke"
{
    Properties
    {
        _MainTex("Smoke mask", 2D) = "white" {}
        _SoftDistance("Surface blend distance", Float) = 0.65
        _CloudFade("Cloud dissipation", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _SoftDistance;
                float _CloudFade;
            CBUFFER_END
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float eyeDepth : TEXCOORD1;
                half fog : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.eyeDepth = -position.positionVS.z;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                output.fog = ComputeFogFactor(position.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 mask = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float sceneRaw = SampleSceneDepth(screenUV);
                // An absent depth buffer (e.g. Mobile preset) must not make
                // smoke disappear. Desktop's URP asset supplies real depth.
                float sceneFade = 1;
                if (sceneRaw > 0.00001 && sceneRaw < 0.99999)
                    sceneFade = saturate((LinearEyeDepth(sceneRaw, _ZBufferParams) -
                        input.eyeDepth) / max(0.01, _SoftDistance));
                float nearFade = saturate((input.eyeDepth - 0.08) / 0.38);
                half alpha = mask.a * input.color.a * sceneFade * nearFade * _CloudFade;
                clip(alpha - 0.001);
                half3 rgb = MixFog(mask.rgb * input.color.rgb, input.fog);
                return half4(rgb, alpha);
            }
            ENDHLSL
        }
    }
}
