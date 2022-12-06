// Upgrade NOTE: replaced '_Object2World' with 'unity_ObjectToWorld'

Shader "LE/RayMarch"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Speed", Float) = 0
        _ColorR ("Red Channel modifier", Float) = 1
        _ColorG ("Greem Channel modifier", Float) = 1
        _ColorB ("Blue Channel modifier", Float) = 1
        _Spikyness ("Spikyness", Float) = 0.1
        _Lobsidedness ("Lobsidedness", Float) = 0
        _Size ("Size", Float) = 1.0
        _ControlledTime ("Controlled Time", Float) = 0.0
        _Hats ("Hats", Float) = 0.0
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
            float _ColorR;
            float _ColorG;
            float _ColorB;
            float _Spikyness;
            float _Lobsidedness;
            float _Size;
            float _Hats;
            float _ControlledTime;

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
                return (sin((_ControlledTime+_Speed) / div) + 1) / 2;
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
                    float phi = atan2(z.y, z.x) * 2;//_Spikyness;
                    dr = pow(r, Power - 0.5) * Power * dr + 1.0;

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
            
            float3x3 rotate_x(float a){float sa = sin(a); float ca = cos(a); return float3x3(float3(1.,.0,.0), float3(.0,ca,sa), float3(.0,-sa,ca));}
            float3x3 rotate_y(float a){float sa = sin(a); float ca = cos(a); return float3x3(float3(ca,.0,sa), float3(.0,1.,.0), float3(-sa,.0,ca));}
            float3x3 rotate_z(float a){float sa = sin(a); float ca = cos(a); return float3x3(float3(ca,sa,.0), float3(-sa,ca,.0), float3(.0,.0,1.));}

            float opSmoothUnion( float d1, float d2, float k ) {
                float h = clamp( 0.5 + 0.5*(d2-d1)/k, 0.0, 1.0 );
                return lerp( d2, d1, h ) - k*h*(1.0-h);
            }
            
            //float opTwist( in sdf3d primitive, in float3 p )
            //{
            //    const float k = 10.0; // or some other amount
            //    float c = cos(k*p.y);
            //    float s = sin(k*p.y);
            //    Matrix2x2  m = mat2(c,-s,s,c);
            //    float3  q = float3(m*p.xz,p.y);
            //    return float4( max(q,0.0), min(max(q.x,max(q.y,q.z)),0.0) );
            //}

            float torus(float3 p, float ro, float ri)
            {
                return length(float2(length(p.xy) - ro, p.z)) - ri;
            }
            
            float opTwist(in float3 p )
            {
                float k = 100.0; // or some other amount
                float c = cos(k*p.y);
                float s = sin(k*p.y);
                float2x2  m = float2x2(c,-s,s,c);
                float3  q = float3(mul(p.xz, m), p.y);
                return torus(q, 2.2, 0.5);
            }
            
            float opDisplace(in float3 p )
            {
                float d1 = torus(p, 2.2, 0.05);
                float d2 = sin(1*p.x)*sin(2*p.y)*sin(1*p.z) * _Hats;
                return d1+d2;
            }
            

            float GetDist(float3 p)
            {
                float v = mTime(4, 0.05, 1);//(sin(_ControlledTime) + 1) / 2;
                float frac = DE(p, map(v,0,1,1,14), 3, 256);
                float3x3 m_r = rotate_x(sin(_ControlledTime));
                float3 p_r = mul(p, m_r);
                float tor =  opDisplace(p_r);
                return opSmoothUnion(frac, tor, 0.5);
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

            float3 pal( in float t, in float3 a, in float3 b, in float3 c, in float3 d )
            {
                return a + b*cos( 6.28318*(c*t+d) );
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
                    float tx = pow(mTime(3,0,1), _ColorR);
                    float ty = _ColorG;
                    float tz = pow(mTime(5,0,1), _ColorB);
                    float3 pal_col = pal( (ab.x + ab.y + ab.z ) / 3.0,
                        float3(0.5,0.5,0.5),
                        float3(0.5,0.5,0.5),
                        float3(1.0,1.0,0.5),
                        float3(0.8,0.90,0.30) );

                    col.rgb = pal_col;
                    //col.rgb = float3((1-tx)-ab.x,(1-ty)-ab.y,ab.z*tz);
                    //col.rgb = float3(1 - ab.x, 1 - ab.y, 1 - ab.z);
                    //col.rgb = float3(1 - ab.x, 1 - ab.y, 1 - ab.z);
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
