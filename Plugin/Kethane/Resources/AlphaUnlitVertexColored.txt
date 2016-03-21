Shader "Kethane/AlphaUnlitVertexColored" {
    Properties {
        _Color ("Main Color", Color) = (1,1,1,1)
	    _MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
    }

    SubShader {
	    //Tags {"Queue"="Transparent+100" "IgnoreProjector"="True" "RenderType"="Transparent"}
		Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" }
	    LOD 100

	    ZWrite Off
	    Blend SrcAlpha OneMinusSrcAlpha

        BindChannels {
            Bind "Color", color
			Bind "Vertex", vertex
        }

	    Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color: COLOR;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				float4 color: COLOR;
			};

			float4 _Color;
			float4 _MainTex_ST;
			sampler2D _MainTex;

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = mul(UNITY_MATRIX_MVP, v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.color = v.color;
				return o;
			}
			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				return col * i.color * _Color;
			}
			ENDCG
	    }
    }
}
