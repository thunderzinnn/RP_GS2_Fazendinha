#ifndef FARM_TOON_LIGHTING_INCLUDED
#define FARM_TOON_LIGHTING_INCLUDED

#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH

#ifndef SHADERGRAPH_PREVIEW
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#endif

void MainLightColor_float(float3 Position, out float3 Color)
{
    Color = 1;
#ifndef SHADERGRAPH_PREVIEW
    float4 shadowCoord = TransformWorldToShadowCoord(Position);
#if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
    shadowCoord = ComputeScreenPos(TransformWorldToHClip(Position));
#endif
    Light mainLight = GetMainLight(shadowCoord);
    float intensity = max(max(mainLight.color.r, mainLight.color.g), max(mainLight.color.b, 1.0));
    Color = lerp(float3(1, 1, 1), mainLight.color / intensity, 0.35) * lerp(0.72, 1.0, mainLight.shadowAttenuation);
#endif
}

void MainLightColor_half(half3 Position, out half3 Color)
{
    float3 result;
    MainLightColor_float(Position, result);
    Color = result;
}

#endif
