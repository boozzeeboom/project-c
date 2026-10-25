// ProceduralBumpPrecise.hlsl — точный Normal From Height для городских материалов (PRIMUM, Э3.1).
//
// Зачем: штатный узел Shader Graph «Normal From Height» берёт ddx/ddy от МИРОВОЙ позиции
// (IN.WorldSpacePosition). Город стоит в ~40 км от начала координат, шаг float там ≈ 3.9 мм —
// на пиксель вблизи приходится 1–3 таких шага, производная позиции «ступенчатая», площадь
// пикселя d ≈ 0 или скачет, градиент высоты 1/d взрывается → попиксельная чёрная грануляция
// на ЛЮБОМ рельефе (подтверждено пользователем 25.10).
//
// Здесь производная берётся от позиции в OBJECT SPACE (числа малые, точные) и переводится в мир
// матрицей объекта: ddx(P_ws) = M · ddx(P_os) — тот же результат, без потери точности.
// Дальше — формула штатного узла 1:1 (surface gradient), выход в tangent space, как у узла,
// чтобы мастер-стек графа (NormalTS) не менялся.
//
// FO-safe: мировые координаты в расчёт не входят. Вход TangentMatrix собирается в графе из
// Normal/Tangent/Bitangent Vector (World). Сигнатуру и порядок аргументов не менять (CF биндится по слотам).

#ifndef PC_BUMP_PRECISE_INCLUDED
#define PC_BUMP_PRECISE_INCLUDED

void PC_NormalFromHeightPrecise_float(
    float In, float Strength, float3 PositionOS,
    float3 NormalWS, float3 TangentWS, float3 BitangentWS,
    out float3 Out)
{
    float3x3 m = (float3x3)GetObjectToWorldMatrix();
    float3 dpdx = mul(m, ddx(PositionOS));
    float3 dpdy = mul(m, ddy(PositionOS));

    float3 n = NormalWS;
    float3 crossX = cross(n, dpdx);
    float3 crossY = cross(dpdy, n);
    float d = dot(dpdx, crossY);
    float sgn = d < 0.0 ? -1.0 : 1.0;
    float surface = sgn / max(1.192093e-15, abs(d));

    float dHdx = ddx(In);
    float dHdy = ddy(In);
    float3 surfGrad = surface * (dHdx * crossY + dHdy * crossX);
    float3 nWS = SafeNormalize(n - Strength * surfGrad);

    float3x3 tbn = float3x3(TangentWS, BitangentWS, NormalWS);
    Out = TransformWorldToTangent(nWS, tbn);
}

void PC_NormalFromHeightPrecise_half(
    half In, half Strength, half3 PositionOS,
    half3 NormalWS, half3 TangentWS, half3 BitangentWS,
    out half3 Out)
{
    float3 o;
    PC_NormalFromHeightPrecise_float((float)In, (float)Strength, (float3)PositionOS,
        (float3)NormalWS, (float3)TangentWS, (float3)BitangentWS, o);
    Out = (half3)o;
}

#endif
