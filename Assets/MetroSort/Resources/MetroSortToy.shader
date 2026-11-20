// Glossy toy-plastic look for everything built through MaterialLibrary: soft wrapped light with tinted
// shadows, a clean highlight and a light rim around the silhouette. One directional light, real-time shadows.
// Surfaces can carry a texture projected in world space (so merged meshes need no UVs): _MapMode 1 uses it
// as tile detail on top of the colour, 2 shows the picture itself; _MapST is scale (xy) and offset (zw).
Shader "MetroSort/Toy"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _Smoothness ("Gloss", Range(0, 1)) = 0.35
        _BaseMap ("Texture", 2D) = "white" {}
        _MapST ("Texture scale and offset", Vector) = (1, 1, 0, 0)
        _MapMode ("Texture mode (0 off, 1 detail, 2 picture)", Float) = 0
        _VertexAO ("Baked occlusion from vertex colour", Range(0, 1)) = 0
        _Sheen ("Fabric sheen", Range(0, 1)) = 0
        _UVBoost ("Texture brightness in UV mode (3)", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness;
            float4 _MapST;
            half _MapMode;
            half _VertexAO;
            half _Sheen;
            half _UVBoost;
        CBUFFER_END
        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);
        ENDHLSL

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half3 ambient : TEXCOORD2;
                half occlusion : TEXCOORD3;
                float2 uv : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);
                o.positionWS = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.ambient = SampleSH(o.normalWS);
                o.occlusion = lerp(1.0h, v.color.r, _VertexAO);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                half3 n = normalize(i.normalWS);
                half3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                Light sun = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                half3 base = _BaseColor.rgb;
                // UV mode (3): a painted character texture; tinted regions store shading around 0.5,
                // so colour * texture * 2 gives the tint with the painted shading on top.
                if (_MapMode > 2.5)
                {
                    half3 painted = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv).rgb;
                    base *= painted * _UVBoost;
                }

                // World-space projection along the dominant axis of the surface.
                float3 facing = abs(n);
                float2 plane = facing.y > 0.5 ? i.positionWS.xz : (facing.z >= facing.x ? i.positionWS.xy : i.positionWS.zy);
                float2 uv = plane * _MapST.xy + _MapST.zw;
                half3 picture = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb;
                half3 mean = SAMPLE_TEXTURE2D_LOD(_BaseMap, sampler_BaseMap, uv, 12).rgb;
                half3 detail = picture / max(mean, 0.04);
                base *= _MapMode > 2.5 ? half3(1, 1, 1) : (_MapMode > 1.5 ? picture : (_MapMode > 0.5 ? detail : half3(1, 1, 1)));

                // Wrapped light: the terminator is soft and sits past the edge, like light through plastic.
                half ndl = dot(n, sun.direction);
                // Matches plain Lambert where the light is full on, so the scene keeps its exposure.
                half lit = pow(saturate((ndl + 0.35) / 1.35), 1.5) * sun.shadowAttenuation;
                half3 colour = base * (i.ambient + sun.color * lit);
                // Shadows keep their colour: a touch cooler and more saturated instead of going grey.
                half3 shade = base * base * half3(0.80, 0.82, 1.0) * (i.ambient + 0.25);
                colour = lerp(lerp(colour, shade, 0.35), colour, lit);

                // Highlight: tight and bright on glossy parts, a broad sheen on matt ones.
                half3 h = normalize(sun.direction + v);
                half gloss = _Smoothness;
                // Baked occlusion: creases and contact areas sink into a soft, slightly cool shade.
                half ao = i.occlusion;
                colour *= lerp(half3(0.80, 0.78, 0.88), half3(1, 1, 1), ao) * lerp(0.62h, 1.0h, ao);

                // Fabric: a soft velvety glow towards the silhouette instead of a hard highlight.
                half ndv = saturate(dot(n, v));
                colour += lerp(base, half3(1, 1, 1), 0.35) * pow(1.0 - ndv, 2.0) * _Sheen * (0.35 + 0.65 * lit) * ao * 0.55;

                half spec = ao * pow(saturate(dot(n, h)), lerp(30.0, 150.0, gloss)) * gloss * gloss * gloss * 2.0 * lit;
                colour += sun.color * spec;

                // Rim: a light edge from above that lifts shapes off the background.
                half fresnel = pow(1.0 - saturate(dot(n, v)), 3.0);
                half rim = fresnel * saturate(n.y * 0.6 + 0.55) * (0.08 + 0.42 * gloss);
                colour += lerp(base, half3(1, 1, 1), 0.55) * rim;

                return half4(colour, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            float3 _LightDirection;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, _LightDirection));
                #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
                #endif
                return positionCS;
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                return TransformObjectToHClip(v.positionOS.xyz);
            }

            half4 frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Lit"
}
