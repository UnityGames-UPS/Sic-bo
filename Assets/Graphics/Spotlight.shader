Shader "UI/DiscoLightEffect"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _LightColor1 ("Light Color 1", Color) = (1, 0.7, 0.2, 1)
        _LightColor2 ("Light Color 2", Color) = (0.2, 0.5, 1, 1)
        _LightIntensity ("Light Intensity", Range(0, 4)) = 2.5
        _BeamWidth ("Beam Width", Range(0.01, 0.2)) = 0.05
        _BeamLength ("Beam Length", Range(0.5, 2.0)) = 1.1
        _SweepSpeed ("Sweep Speed", Range(0, 3)) = 0.6
        _SpotlightSize ("Circle Size", Range(0.1, 0.6)) = 0.25
        _CircleBrightness ("Circle Brightness", Range(0, 8)) = 5.0
        _ShadowIntensity ("Shadow Intensity", Range(0, 1)) = 0.3
        
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
            float _LightIntensity;
            float _BeamWidth;
            float _BeamLength;
            float _SweepSpeed;
            float _SpotlightSize;
            float _CircleBrightness;
            float _ShadowIntensity;

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

            fixed4 frag(v2f IN) : SV_Target
            {
                half4 baseColor = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                float2 uv = IN.texcoord;
                float time = _Time.y * _SweepSpeed;
                
                // BOTTOM CENTER starting point (green dot at 0,0)
                float2 startPoint = float2(0.5, 0.0);
                
                // === SWEEP RANGE: 0° to 180° (UPPER HALF ONLY) ===
                float progress = sin(time) * 0.5 + 0.5; // 0 to 1
                
                // Spotlight 1: 0° to 180° (right → top → left)
                float angle1 = lerp(0.0, 3.14159, progress);
                
                // Spotlight 2: 180° to 0° (left → top → right) CROSSING
                float angle2 = lerp(3.14159, 0.0, progress);
                
                // FIXED: Correct beam direction calculation
                // cos(angle) = X direction, sin(angle) = Y direction
                float2 beamDir1 = float2(cos(angle1), sin(angle1));
                float2 beamDir2 = float2(cos(angle2), sin(angle2));
                
                // === BEAM 1 ===
                float2 toPixel1 = uv - startPoint;
                float dist1 = length(toPixel1);
                float pixelAngle1 = atan2(toPixel1.y, toPixel1.x);
                
                float angleDiff1 = abs(pixelAngle1 - angle1);
                if (angleDiff1 > 3.14159) angleDiff1 = 6.28318 - angleDiff1;
                
                float beam1 = 1.0 - smoothstep(0.0, _BeamWidth, angleDiff1);
                beam1 *= smoothstep(_BeamLength + 0.3, 0.05, dist1);
                beam1 *= smoothstep(0.0, 0.08, dist1);
                beam1 *= (1.0 - smoothstep(0.1, _BeamLength, dist1)) * 0.5;
                
                // === BEAM 2 ===
                float2 toPixel2 = uv - startPoint;
                float dist2 = length(toPixel2);
                float pixelAngle2 = atan2(toPixel2.y, toPixel2.x);
                
                float angleDiff2 = abs(pixelAngle2 - angle2);
                if (angleDiff2 > 3.14159) angleDiff2 = 6.28318 - angleDiff2;
                
                float beam2 = 1.0 - smoothstep(0.0, _BeamWidth, angleDiff2);
                beam2 *= smoothstep(_BeamLength + 0.3, 0.05, dist2);
                beam2 *= smoothstep(0.0, 0.08, dist2);
                beam2 *= (1.0 - smoothstep(0.1, _BeamLength, dist2)) * 0.5;
                
                // === CIRCLE 1 - UPPER HALF ONLY ===
                float2 circle1Pos = startPoint + beamDir1 * _BeamLength;
                float distToCircle1 = length(uv - circle1Pos);
                
                float circle1Center = 1.0 - smoothstep(0.0, _SpotlightSize * 0.25, distToCircle1);
                circle1Center = pow(circle1Center, 5.0) * 4.0;
                
                float circle1Body = 1.0 - smoothstep(0.0, _SpotlightSize, distToCircle1);
                circle1Body = pow(circle1Body, 2.0) * 2.0;
                
                float circle1Glow = 1.0 - smoothstep(_SpotlightSize * 0.4, _SpotlightSize * 2.0, distToCircle1);
                circle1Glow *= 0.8;
                
                float circle1 = saturate(circle1Center + circle1Body + circle1Glow) * _CircleBrightness;
                
                // === CIRCLE 2 - UPPER HALF ONLY ===
                float2 circle2Pos = startPoint + beamDir2 * _BeamLength;
                float distToCircle2 = length(uv - circle2Pos);
                
                float circle2Center = 1.0 - smoothstep(0.0, _SpotlightSize * 0.25, distToCircle2);
                circle2Center = pow(circle2Center, 5.0) * 4.0;
                
                float circle2Body = 1.0 - smoothstep(0.0, _SpotlightSize, distToCircle2);
                circle2Body = pow(circle2Body, 2.0) * 2.0;
                
                float circle2Glow = 1.0 - smoothstep(_SpotlightSize * 0.4, _SpotlightSize * 2.0, distToCircle2);
                circle2Glow *= 0.8;
                
                float circle2 = saturate(circle2Center + circle2Body + circle2Glow) * _CircleBrightness;
                
                // === COMBINE LIGHTS ===
                float3 light1 = (_LightColor1.rgb * beam1 * 0.8) + (_LightColor1.rgb * circle1);
                float3 light2 = (_LightColor2.rgb * beam2 * 0.8) + (_LightColor2.rgb * circle2);
                
                // === SHADOW ===
                float totalMask = saturate((beam1 + beam2) * 3.0);
                float shadow = (1.0 - totalMask) * _ShadowIntensity;
                float3 shadowedBG = baseColor.rgb * (1.0 - shadow * 0.2);
                
                // === FINAL ===
                float3 allLights = (light1 + light2) * _LightIntensity;
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