// CloudMath.h - Math library for UE
#pragma once

#include "CoreMinimal.h"

struct FVector2D;
struct FVector;

namespace CloudMathUE
{
	constexpr double PHI = 1.6180339887498948482;

	CLOUDGENERATOR_API int32 Hash3(int32 x, int32 y, int32 z, int32 seed);
	CLOUDGENERATOR_API double Fade3(double t);
	CLOUDGENERATOR_API double Lerp3(double a, double b, double t);
	CLOUDGENERATOR_API double Grad3(int32 h, double x, double y, double z);
	CLOUDGENERATOR_API double Perlin3D(double x, double y, double z, int32 seed);
	CLOUDGENERATOR_API double Fbm(double x, double y, double z, int32 octaves = 5, double persistence = 0.5, double lacunarity = 2.0, int32 seed = 0);
	CLOUDGENERATOR_API double Worley3D(double x, double y, double z, double freq, int32 seed = 0);
	CLOUDGENERATOR_API double InvertedWorley(double x, double y, double z, double freq, int32 seed = 0);

	struct FVec3 { double X, Y, Z; };

	CLOUDGENERATOR_API TArray<FVec3> FibonacciSphere(double cx, double cy, double cz, double rx, double ry, double rz, int32 numPoints);
	CLOUDGENERATOR_API TArray<FVec3> FibonacciSpiral2D(int32 count);
	CLOUDGENERATOR_API FVec3 PerturbDir(double nx, double ny, double nz, double px, double py, double pz, double strength, int32 seed);
}