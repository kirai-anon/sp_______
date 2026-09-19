Shader "Hidden/PostProcess/VisualDelayUnity6"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    // Blit.hlsl provides standard full-screen quad layouts automatically
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    Texture2D _HistoryTex;
    SamplerState sampler_HistoryTex;

    float _Delay;
    float _Intensity;

    float4 Frag(Varyings input) : SV_Target
    {
        UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
        
        // Grab current screen frame
        float4 currentFrame = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
        
        // Grab persistent decaying past frame
        float4 historyFrame = _HistoryTex.Sample(sampler_LinearClamp, input.texcoord);

        // Blend temporal states
        float3 accumulated = lerp(currentFrame.rgb, historyFrame.rgb, _Delay);
        float3 finalColor = lerp(currentFrame.rgb, accumulated, _Intensity);

        return float4(finalColor, 1.0);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "VisualDelayUnity6Pass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}