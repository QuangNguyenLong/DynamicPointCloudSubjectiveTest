Shader"Point_BothEyes"
{
Properties {
_Tint ("Tint", Color) = (0.5, 0.5, 0.5, 1)
}
SubShader
{

Tags { "RenderType"="Opaque" }

Cull off
Pass
{
CGPROGRAM
            
#pragma vertex vert
#pragma fragment frag
#pragma multi_compile _ _COMPUTE_BUFFER

#include "UnityCG.cginc"

struct Point
{
UNITY_VERTEX_INPUT_INSTANCE_ID
#if _COMPUTE_BUFFER
uint vertexID : SV_VertexID;
#else
    float3 pos : POSITION;
    uint col : COLOR;
#endif
};

float _PointSize = 0.01;
float4x4 _Transform;
half4 _Tint;
            
#if _COMPUTE_BUFFER
StructuredBuffer<float3> _Positions;
StructuredBuffer<uint> _Colors;
#endif
struct v2f
{
    float4 pos : SV_POSITION;
    half psize : PSIZE;
    half4 col : COLOR;
UNITY_VERTEX_OUTPUT_STEREO // Added to enable display on both eyes
};

v2f vert(Point v)
{
#if _COMPUTE_BUFFER
float3 p = _Positions[v.vertexID]; 
uint c = _Colors[v.vertexID];
#else                
    float3 p = v.pos;
    uint c = v.col;
#endif
    
    v2f o;
                
    UNITY_SETUP_INSTANCE_ID(v); // Added to enable display on both eyes
    UNITY_INITIALIZE_OUTPUT(v2f, o); // Added to enable display on both eyes
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o); // Added to enable display on both eyes
    
    

    uint icol = c;
    half4 col = half4(
    ((icol) & 0xff) / 255.0,
    ((icol >> 8) & 0xff) / 255.0,
    ((icol >> 16) & 0xff) / 255.0,
    ((icol >> 24) & 0xff) / 255.0
    );
    o.pos = UnityObjectToClipPos(mul(_Transform, float4(p, 1)));
    o.col = col * _Tint;
    o.psize = _PointSize;
    return o;
}

half4 frag(v2f i) : SV_Target
{
    return i.col;
}

ENDCG
}
}
}
