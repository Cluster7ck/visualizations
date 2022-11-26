// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "LE/RayMarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
                return (sin(_Time.y / div) + 1) / 2;
            }

            float mTime(float div, float low, float high)
            {
                return map(time01(div), 0, 1, low, high);
            }

            float DE(float3 pos, float Power, float Bailout, int Iterations) {
                float3 z = pos;
                float dr = 1.0;
                float r = 0.0;
                for (int i = 0; i < Iterations; i++) {
                    r = length(z);
                    if (r > Bailout) break;

                    // convert to polar coordinates
                    float theta = acos(z.z / r);
                    float phi = atan2(z.y, z.x);
                    dr = pow(r, Power - 1.0) * Power * dr + 1.0;

                    // scale and rotate the point
                    float zr = pow(r, Power);
                    float v = 
                    theta = pow(theta, mTime(5, 1, 2.5)) * Power;
                    phi = phi * Power;

                    // convert back to cartesian coordinates
                    z = zr * float3(sin(theta) * cos(phi), sin(phi ) * sin(theta), cos(theta));
                    z += pos;
                }
                return 0.5 * log(r) * r / dr;
            }

            float torus(float3 p, float ro, float ri)
            {
                return length(float2(length(p.xz) - ro, p.y)) - ri;
            }

            float GetDist(float3 p)
            {
                float v = (sin(_Time.y/5) + 1) / 2;
                return DE(p, map(v,0,1,1,15), 3, 512);
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
                    col.rgb = float3(1-n.x,1-n.y,1-n.z);
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
