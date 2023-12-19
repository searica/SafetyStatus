Shader"PlayerBaseCircle" {
    Properties {
        _MainTex ("Base (RFB)", 2D) = "white" {}
        _AreaColor ("Area Color", Color) = (1, 1, 1)
        _Center ("Center", Vector) = (0, 0, 0, 0)
        _Radius ("Radius", Range(0, 500)) = 75
        _Border ("Border", Range(0, 100)) = 12
    }
    SubShader {
        Tags {
            "RenderType"="Opaque"
        }

        CGPROGRAM
        #pragma surface surf Lambert

        sampler2D _MainTex;
        fixed3 _AreaColor;
        float3 _Center;
        float _Radius;
        float _Border;

        struct Input{
            float2 uv_MainTex;
            float3 worldPos;
        }

        void surf(Input IN, inout SurfaceOutput output){
            half4 color = tex2D(_MainTex, IN.uv_MainTex);
            float dist = distance(_Center, IN.worldPos);

            if (dist > _Radius && dist < (_Radius + _Border)){
                output.Albedo = _AreaColor;
            }
            else{
                o.Albedo = color.rgb;
            }
            o.Alpha = color.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
