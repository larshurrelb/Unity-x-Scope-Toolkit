Shader "Custom/DepthShader"
{
    Properties
    {
        _DepthMin ("Depth Min", Range(0, 1)) = 0
        _DepthMax ("Depth Max", Range(0, 1)) = 1
        _Invert ("Invert", Float) = 0
    }
    
    SubShader
    {
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            float _DepthMin;
            float _DepthMax;
            float _Invert;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float depth = SampleSceneDepth(input.uv);
                depth = Linear01Depth(depth, _ZBufferParams);
                
                // Remap depth from [DepthMin, DepthMax] to [0, 1]
                depth = saturate((depth - _DepthMin) / (_DepthMax - _DepthMin));
                
                // Invert if enabled (0 = no invert, 1 = invert)
                depth = lerp(depth, 1.0 - depth, _Invert);
                
                return half4(depth, depth, depth, 1);
            }
            ENDHLSL
        }
    }
}
