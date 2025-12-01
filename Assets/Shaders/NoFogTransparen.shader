Shader "Unlit/NoFogTransparent_V2" // 更改 Shader 名称，便于识别
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint Color (RGBA)", Color) = (1,1,1,1) // 添加颜色属性，用其A通道控制透明度
    }
    SubShader
    {
        // 1. 【关键修改】设置 RenderType 和 Queue，用于透明渲染
        Tags { "RenderType"="Transparent" "Queue"="Transparent" } 
        LOD 100

        Pass
        {
            // 2. 【关键添加】设置混合模式 (SrcAlpha OneMinusSrcAlpha 是标准透明混合)
            Blend SrcAlpha OneMinusSrcAlpha 
            // 3. 【关键添加】关闭深度写入 (防止透明物体错误地遮挡后面的物体)
            ZWrite Off 
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // 此处未包含雾效宏，所以不接收雾效

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color; // 在 CG 块中声明新的颜色变量

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                // 4. 【关键修改】使用 _Color 变量与贴图颜色相乘，应用 Alpha
                fixed4 col = tex2D(_MainTex, i.uv) * _Color; 
                
                return col;
            }
            ENDCG
        }
    }
}