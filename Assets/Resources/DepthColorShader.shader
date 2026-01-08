Shader "Custom/DepthColorShader"
{
    Properties
    {
        _DepthMin ("Depth Min", Range(0, 1)) = 0
        _DepthMax ("Depth Max", Range(0, 1)) = 1
        _Invert ("Invert", Float) = 0
        _ColorNear ("Near Color", Color) = (1, 1, 1, 1)
        _ColorFar ("Far Color", Color) = (0, 0, 0, 1)
        _ColorCharacter ("Character Color", Color) = (1, 0, 0, 1)
        _CharacterMask ("Character Mask", 2D) = "black" {}
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
            float4 _ColorNear;
            float4 _ColorFar;
            float4 _ColorCharacter;
            TEXTURE2D(_CharacterMask);
            SAMPLER(sampler_CharacterMask);

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Sample character mask
                float characterMask = SAMPLE_TEXTURE2D(_CharacterMask, sampler_CharacterMask, input.uv).r;
                
                // Make mask binary - if any white is detected, use character color 100%
                characterMask = step(0.001, characterMask);
                
                float depth = SampleSceneDepth(input.uv);
                depth = Linear01Depth(depth, _ZBufferParams);
                
                // Remap depth from [DepthMin, DepthMax] to [0, 1]
                depth = saturate((depth - _DepthMin) / (_DepthMax - _DepthMin));
                
                // Invert if enabled
                depth = lerp(depth, 1.0 - depth, _Invert);
                
                // Lerp between two colors based on depth
                float4 color = lerp(_ColorNear, _ColorFar, depth);
                
                // Completely replace with character color where mask is present
                color = lerp(color, _ColorCharacter, characterMask);
                
                return color;
            }
            ENDHLSL
        }
    }
}
