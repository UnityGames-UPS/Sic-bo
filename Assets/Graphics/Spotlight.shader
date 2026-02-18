Shader "UI/DiscoLightEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _LightColor1 ("Light Color 1", Color) = (1, 0.7, 0.2, 1)
        _LightColor2 ("Light Color 2", Color) = (0.2, 0.5, 1, 1)
        _LightColor3 ("Light Color 3 (Bottom Left)", Color) = (0.2, 1, 0.4, 1)
        _LightColor4 ("Light Color 4 (Bottom Right)", Color) = (1, 0.2, 0.6, 1)
        _LightIntensity ("Light Intensity", Range(0, 4)) = 2.5
        _BeamWidth ("Beam Width", Range(0.01, 0.3)) = 0.05
        _BeamLength ("Beam Length", Range(0.5, 2.0)) = 1.1
        _SweepSpeed ("Sweep Speed", Range(0, 3)) = 0.6
        _SpotlightSize ("Circle Size", Range(0.1, 0.6)) = 0.25
        _CircleBrightness ("Circle Brightness", Range(0, 8)) = 5.0
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.3
        _CornerOffset ("Corner Offset (Padding)", Range(0.0, 0.4)) = 0.1
        _CornerPhase ("Corner Phase Offset", Range(0, 6.28318)) = 1.5708
        
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata_t
            {
                float4 vertex : POSITION;
                float4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float4 _LightColor1;
            float4 _LightColor2;
            float4 _LightColor3;
            float4 _LightColor4;
            float _LightIntensity;
            float _BeamWidth;
            float _BeamLength;
            float _SweepSpeed;
            float _SpotlightSize;
            float _CircleBrightness;
            float _ShadowIntensity;
            float _CornerOffset;
            float _CornerPhase;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // Reusable helper: computes beam intensity from a start point toward a sweep angle
            float ComputeBeam(float2 uv, float2 startPoint, float sweepAngle, float beamWidth, float beamLength)
            {
                float2 toPixel = uv - startPoint;
                float dist = length(toPixel);
                float pixelAngle = atan2(toPixel.y, toPixel.x);
                
                float angleDiff = abs(pixelAngle - sweepAngle);
                if (angleDiff > 3.14159) angleDiff = 6.28318 - angleDiff;
                
                float beam = 1.0 - smoothstep(0.0, beamWidth, angleDiff);
                beam *= smoothstep(beamLength + 0.3, 0.05, dist);
                beam *= smoothstep(0.0, 0.08, dist);
                beam *= (1.0 - smoothstep(0.1, beamLength, dist)) * 0.5;
                return beam;
            }

            // Reusable helper: computes spotlight circle intensity at end of beam
            float ComputeCircle(float2 uv, float2 startPoint, float2 beamDir, float beamLength, float spotlightSize, float circleBrightness)
            {
                float2 circlePos = startPoint + beamDir * beamLength;
                float distToCircle = length(uv - circlePos);
                
                float circleCenter = 1.0 - smoothstep(0.0, spotlightSize * 0.25, distToCircle);
                circleCenter = pow(circleCenter, 5.0) * 4.0;
                
                float circleBody = 1.0 - smoothstep(0.0, spotlightSize, distToCircle);
                circleBody = pow(circleBody, 2.0) * 2.0;
                
                float circleGlow = 1.0 - smoothstep(spotlightSize * 0.4, spotlightSize * 2.0, distToCircle);
                circleGlow *= 0.8;
                
                return saturate(circleCenter + circleBody + circleGlow) * circleBrightness;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 baseColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                float2 uv = IN.texcoord;
                float time = _Time.y * _SweepSpeed;

                // =====================================================
                // CENTER SPOTLIGHTS (original) — origin: bottom center
                // =====================================================
                float2 startCenter = float2(0.5, 0.0);
                
                float progress = sin(time) * 0.5 + 0.5; // 0 to 1

                // Beam 1: right → top → left
                float angle1 = lerp(0.0, 3.14159, progress);
                // Beam 2: left → top → right (crossing)
                float angle2 = lerp(3.14159, 0.0, progress);

                float2 beamDir1 = float2(cos(angle1), sin(angle1));
                float2 beamDir2 = float2(cos(angle2), sin(angle2));

                float beam1 = ComputeBeam(uv, startCenter, angle1, _BeamWidth, _BeamLength);
                float beam2 = ComputeBeam(uv, startCenter, angle2, _BeamWidth, _BeamLength);

                float circle1 = ComputeCircle(uv, startCenter, beamDir1, _BeamLength, _SpotlightSize, _CircleBrightness);
                float circle2 = ComputeCircle(uv, startCenter, beamDir2, _BeamLength, _SpotlightSize, _CircleBrightness);

                // =====================================================
                // CORNER SPOTLIGHTS — bottom-left and bottom-right
                // Phase-offset so they sweep at a different rhythm
                // =====================================================
                float2 startBL = float2(0.0 + _CornerOffset, 0.0); // bottom-left with padding
                float2 startBR = float2(1.0 - _CornerOffset, 0.0); // bottom-right with padding

                float progressCorner = sin(time + _CornerPhase) * 0.5 + 0.5;

                // Bottom-left beam sweeps 0° → 180° (same upper-half arc)
                float angle3 = lerp(0.0, 3.14159, progressCorner);
                // Bottom-right beam sweeps 180° → 0° (crossing, mirrored)
                float angle4 = lerp(3.14159, 0.0, progressCorner);

                float2 beamDir3 = float2(cos(angle3), sin(angle3));
                float2 beamDir4 = float2(cos(angle4), sin(angle4));

                float beam3 = ComputeBeam(uv, startBL, angle3, _BeamWidth, _BeamLength);
                float beam4 = ComputeBeam(uv, startBR, angle4, _BeamWidth, _BeamLength);

                float circle3 = ComputeCircle(uv, startBL, beamDir3, _BeamLength, _SpotlightSize, _CircleBrightness);
                float circle4 = ComputeCircle(uv, startBR, beamDir4, _BeamLength, _SpotlightSize, _CircleBrightness);

                // =====================================================
                // COMBINE ALL LIGHTS
                // =====================================================
                float3 light1 = (_LightColor1.rgb * beam1 * 0.8) + (_LightColor1.rgb * circle1);
                float3 light2 = (_LightColor2.rgb * beam2 * 0.8) + (_LightColor2.rgb * circle2);
                float3 light3 = (_LightColor3.rgb * beam3 * 0.8) + (_LightColor3.rgb * circle3);
                float3 light4 = (_LightColor4.rgb * beam4 * 0.8) + (_LightColor4.rgb * circle4);

                // === SHADOW ===
                float totalMask = saturate((beam1 + beam2 + beam3 + beam4) * 3.0);
                float shadow = (1.0 - totalMask) * _ShadowIntensity;
                float3 shadowedBG = baseColor.rgb * (1.0 - shadow * 0.2);

                // === FINAL ===
                float3 allLights = (light1 + light2 + light3 + light4) * _LightIntensity;
                float3 finalColor = shadowedBG + allLights;
                finalColor = lerp(baseColor.rgb, finalColor, 0.85);
                
                half4 color = half4(finalColor, baseColor.a);
                
                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif
                
                clip(color.a - 0.001);
                
                return color;
            }
            ENDCG
        }
    }
}
