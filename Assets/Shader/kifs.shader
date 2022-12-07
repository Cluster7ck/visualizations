Shader "LE/kifs"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Replicate ("Replicate", Float) = 32.0
        _Zoom ("Replicate", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Replicate;
            float _Zoom;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                UNITY_TRANSFER_FOG(o,o.vertex);
                return o;
            }

            float2 N(float angle)
            {
                return float2(sin(angle), cos(angle));
            }

            float div(float a, float b)
            {
                float r = a / b;
                return floor(r);
            }


            float fmod(float a, float b)
            {
                float x = a / b;
                return x - floor(x);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.uv * 2.0 - 1.0);
                float4 col = float4(0,0,0,1);
                
                float n = _Replicate;
                float r = sqrt(pow(uv.x, 2) + pow(uv.y, 2));
                float phi = atan2(abs(uv.y), abs(uv.x));
                float sectorAngle = (2 * 3.1415) / n;
                float remainder = fmod(phi, sectorAngle);
                float whole = div(phi, sectorAngle);
                float isEven = whole % 2;
                float isOdd = abs(isEven - 1);
                float oddProjected = phi - whole * sectorAngle;
                float evenProjected = (1 - remainder) * sectorAngle;
                float back = (oddProjected * isOdd + evenProjected * isEven);

                float x = r * cos(back);
                float y = r * sin(back);

                float2 ogId = float2(x,y);
                uv = ogId;
                uv *= _Zoom;

                if(n < 4 )
                {
                    // Define the center of the texture
                    float offset = _Zoom * 0.5;
                    
                    // Translate the UV coordinates back to the center
                    float2 centeredUV = (i.uv - 0.5) / _Zoom + 0.5;
                    
                    //uv_n = uv_n * float2(1 / _Zoom, 1 / _Zoom);
                    col += tex2D(_MainTex, centeredUV) * smoothstep(0.99,0.98, r);
                }
                else
                {
                    col += tex2D(_MainTex, (uv.xy + float2(1.0,1.0) ) / 2.0) * smoothstep(0.99,0.98, r);
                }

                return col;
            }
            ENDCG
        }
    }
}
