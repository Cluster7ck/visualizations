// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "LE/RayMarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Float display name", Float) = 0
        _ColorPowerR ("Float display name", Float) = 1
        _ColorPowerG ("Float display name", Float) = 1
        _ColorPowerB ("Float display name", Float) = 1
        _Spikyness ("Float display name", Float) = 0.1
        _Lobsidedness ("Float display name", Float) = 0
        _Size ("Float display name", Float) = 0.0
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

            #include "UnityCG.cginc"
#define MAX_STEPS 100
#define MAX_DIST 15
#define SURF_DIST 0.008

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 ro : TEXCOORD1;
                float3 hitPos : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Speed;
            float _ColorPowerR;
            float _ColorPowerG;
            float _ColorPowerB;
            float _Spikyness;
            float _Lobsidedness;
            float _Size;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                //o.ro = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos,1));
                o.ro = _WorldSpaceCameraPos;
                //o.hitPos = v.vertex;
                o.hitPos = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            float map(float value, float min1, float max1, float min2, float max2) {
                return min2 + (value - min1) * (max2 - min2) / (max1 - min1);
            }

            float sdSphere(float3 p, float r)
            {
                return length(p) - r;
            }

            float time01(float div)
            {
                return (sin((_Time.y+_Speed) / div) + 1) / 2;
            }

            float mTime(float div, float low, float high)
            {
                return map(time01(div), 0, 1, low, high);
            }

            float DE(float3 pos, float Power, float Bailout, int Iterations) {
                float3 z = pos;
                float dr = 1;
                float r = 0.0;
                for (int i = 0; i < Iterations; i++) {
                    r = length(z);
                    if (r > Bailout) break;

                    // convert to polar coordinates
                    float x = acos(z.z / r);
                    float theta = atan2(x, z.y * _Lobsidedness);
                    float phi = atan2(z.y, z.x) * 2;
                    dr = pow(r, Power - 1.0) * Power * dr + 1.0;

                    // scale and rotate the point
                    float zr = pow(r*(1 - (0.1 * _Size)), Power);
                    float v = mTime(5, 0.01, 1);
                    theta = pow(theta, v) * Power;
                    phi = phi * Power;

                    // convert back to cartesian coordinates
                    z = zr * float3(sin(theta) * cos(phi), sin(phi ) * sin(theta), cos(theta));
                    z += pos;
                }
                return mTime(15,0.1,0.7) * log(r) * r / dr;
            }

            float torus(float3 p, float ro, float ri)
            {
                return length(float2(length(p.xz) - ro, p.y)) - ri;
            }

            float GetDist(float3 p)
            {
                float v = mTime(4,0.05,1);//(sin(_Time.y) + 1) / 2;
                return DE(p, map(v,0,1,1,14), 3, 256);
                //return torus(p, 0.2, 0.1);
            }

            float raymarch(float3 ro, float3 rd)
            {
                float d0 = 0;
                float dS;
                for (int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = ro + d0 * rd;
                    dS = GetDist(p);
                    d0 += dS;
                    if (dS < SURF_DIST || d0 > MAX_DIST) break;
                }

                return d0;
            }

            float3 GetNormal(float3 p)
            {
                float2 e = float2(1e-2, 0);
                float3 n = GetDist(p) - float3(
                    GetDist(p - e.xyy),
                    GetDist(p - e.yxy),
                    GetDist(p - e.yyx)
                    );
                return normalize(n);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = i.uv - 0.5;
                float3 ro = i.ro;
                float3 rd = normalize(i.hitPos-i.ro);

                fixed4 col = 0;
                float d = raymarch(ro, rd);
                float m = dot(uv, uv);

                if (d < MAX_DIST)
                {
                    float3 p = ro + rd * d;
                    float3 n = GetNormal(p);
                    float3 ab = abs(n);
                    float tx = pow(mTime(3,0,1), _ColorPowerR);
                    float ty = pow(mTime(4,0,1), _ColorPowerG);
                    float tz = pow(mTime(5,0,1), _ColorPowerB);
                    //col.rgb = float3((1-tx)-ab.x,(1-ty)-ab.y,ab.z*tz);
                    col.rgb = float3(ab.x,(1-ty)-ab.y, ab.z * tz);
                }
                else {
                    discard;
                }

                //col += smoothstep(.1, .2, m);

                return col;
            }
            ENDCG
        }
    }
}
