Shader "Unlit/ParticlesRender"
{
	Properties
	{
		_MainTex("Texture", 2D) = "white" {}
	}
	SubShader
	{
		Pass
		{
			ZWrite Off
			Blend SrcAlpha One // Traditional transparency

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma geometry geom
			#include "UnityCG.cginc"
			#include "Common.cginc"

			StructuredBuffer<Particle> particles;
			
			uniform float4x4 _LocalToWorldMatrix;
			uniform	sampler2D _MainTex;
			uniform float _SizeMin;
			uniform float _SizeMax;
			uniform float _SizeContrast;
			uniform float _SizeDistribution;
			uniform float _FadeWithSize;
			uniform float _SmoothWithSize;
			uniform float _SizeToSmoothExpMul;
			uniform float _SizeSmoothOffset;

			struct v2g
			{
				float4 vertex : SV_POSITION;
				float4 color : TEXCOORD1;
				float angle : TEXCOORD0;
				float size : TEXCOORD2;
				float2 smooth : TEXCOORD3;
			};

			struct g2f
			{
				float4 vertex : SV_POSITION;
				float4 color : TEXCOORD1;
				float2 uv : TEXCOORD0;
				float2 smooth : TEXCOORD2;
			};

			// DX11 vertex shader these 2 parameters come from the draw call: "1" and "particleCount", 
			// SV_VertexID: "1" is the number of vertex to draw per particle, we could easily make quad or sphere particles with this.
			// SV_InstanceID: "particleCount", number of particles...
			v2g vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				float4 worldPosition = mul(_LocalToWorldMatrix, float4(particles[id].position, 1));
				// we modulate size based on age
				float ageToSize = smoothstep(0, 0.1, max(0, particles[id].age)) * smoothstep(0, 0.1, 1 - max(0, particles[id].age));
				float normSize = 1.0 / (1.0 + exp(-(_SizeContrast * 24.0 * (particles[id].random.x - 0.5) + lerp(-10, 10, _SizeDistribution))));
				
				v2g output;
				output.vertex = worldPosition;
				output.color = particles[id].color;
				output.angle = particles[id].random.x * 2 * UNITY_PI;
				output.size = ageToSize * lerp( _SizeMin, _SizeMax, normSize);
				// we only want the smallest particles to be sharp, sharpness should decrease very quickly
				float normSizeExp = 1 - exp(-_SizeToSmoothExpMul * normSize);
				output.smooth = float2(0.5 - _SizeSmoothOffset, 0.5 + _SizeSmoothOffset) + float2(-normSizeExp, normSizeExp) * _SmoothWithSize;
				return output;
			}

			[maxvertexcount(4)]
            void geom(point v2g input[1], inout TriangleStream<g2f> OutputStream)
            {
				float3 look = (_WorldSpaceCameraPos - input[0].vertex).xyz;
				float3 up = mul(rotationMatrix(look, input[0].angle), float4(0, 1, 0, 1)).xyz;
				float3 right = normalize(cross(up, look));
				up = normalize(cross(look, right));

				float halfSize = input[0].size * 0.5;
				float4 v[4];
				v[0] = float4(input[0].vertex + halfSize * right - halfSize * up, 1.0);
				v[1] = float4(input[0].vertex + halfSize * right + halfSize * up, 1.0);
				v[2] = float4(input[0].vertex - halfSize * right - halfSize * up, 1.0);
				v[3] = float4(input[0].vertex - halfSize * right + halfSize * up, 1.0);

				g2f output;
				UNITY_INITIALIZE_OUTPUT(g2f, output);

				output.vertex = mul(UNITY_MATRIX_VP, v[0]);
				output.uv = float2(1.0f, 0.0f);
				output.color = input[0].color;
				output.smooth = input[0].smooth;
				OutputStream.Append(output);

				output.vertex = mul(UNITY_MATRIX_VP, v[1]);
				output.uv = float2(1.0f, 1.0f);
				output.color = input[0].color;
				output.smooth = input[0].smooth;
				OutputStream.Append(output);

				output.vertex = mul(UNITY_MATRIX_VP, v[2]);
				output.uv = float2(0.0f, 0.0f);
				output.color = input[0].color;
				output.smooth = input[0].smooth;
				OutputStream.Append(output);

				output.vertex = mul(UNITY_MATRIX_VP, v[3]);
				output.uv = float2(0.0f, 1.0f);
				output.color = input[0].color;
				output.smooth = input[0].smooth;
				OutputStream.Append(output);
			}
			
			fixed4 frag (g2f i) : SV_Target
			{
				return smoothstep(i.smooth.x, i.smooth.y, tex2D(_MainTex, i.uv)) * i.color;
			}
			ENDCG
		}
	}
}
