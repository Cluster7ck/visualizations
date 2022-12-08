Shader "LE/kifs"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Replicate ("Replicate", Float) = 3.9
        _Zoom ("Zoom", Float) = 1.0
        _Rotation ("Rotation", Float) = 0.0
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
            float _Rotation;

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
            
            float2x2 rotationMatrix(float angle)
            {
            	angle *= 3.1415 / 180.0;
                float s=sin(angle), c=cos(angle);
                return float2x2( c, -s, s, c );
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = (i.uv * 2.0 - 1.0);
                uv = mul(rotationMatrix(_Rotation), uv);
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


                if(n < 4 )
                {
                    float2 centeredUV = (i.uv - 0.5) / _Zoom + 0.5;
                    col += tex2D(_MainTex, centeredUV) * smoothstep(0.99,0.98, r);
                }
                else
                {
                    float2 ogId = float2(x,y);
                    uv = ogId;
                    float2 centeredUV = (uv) / _Zoom;
                    col += tex2D(_MainTex, (centeredUV.xy + float2(1.0,1.0) ) / 2.0) * smoothstep(0.99,0.98, r);
                }

                return col;
            }
            ENDCG
        }
    }
}
