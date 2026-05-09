// CloudMath.cpp - Math library implementation for UE
#include "CloudMath.h"
#include <cmath>

namespace CloudMathUE
{
    int32 Hash3(int32 x, int32 y, int32 z, int32 seed)
	{
		int64 n64 = x + y * 57LL + z * 131LL + (int64)seed * 123456789LL;
		int32 n = (int32)n64;
		n = (n << 13) ^ n;
		double dn = n;
		double d = dn * (dn * dn * 15731.0 + 789221.0) + 1376312589.0;
		const double two32 = 4294967296.0;
		double mod = d - two32 * floor(d / two32);
		if (mod >= 2147483648.0) mod -= two32;
		return ((int32)mod) & 0x7FFFFFFF;
	}

	double Fade3(double t) { return t * t * t * (t * (t * 6 - 15) + 10); }
	double Lerp3(double a, double b, double t) { return a + (b - a) * t; }

	double Grad3(int32 h, double x, double y, double z)
	{
		int32 g = h & 15;
		double u = g < 8 ? x : y;
		double v = g < 4 ? y : (g == 12 || g == 14 ? x : z);
		return ((g & 1) != 0 ? -u : u) + ((g & 2) != 0 ? -v : v);
	}

	double Perlin3D(double x, double y, double z, int32 seed)
	{
		int32 xi = (int32)floor(x) & 0xFF;
		int32 yi = (int32)floor(y) & 0xFF;
		int32 zi = (int32)floor(z) & 0xFF;
		double xf = x - floor(x);
		double yf = y - floor(y);
		double zf = z - floor(z);
		double u = Fade3(xf), v = Fade3(yf), w = Fade3(zf);

		int32 aaa = Hash3(xi, yi, zi, seed) % 12;
		int32 aba = Hash3(xi, yi + 1, zi, seed) % 12;
		int32 aab = Hash3(xi, yi, zi + 1, seed) % 12;
		int32 abb = Hash3(xi, yi + 1, zi + 1, seed) % 12;
		int32 baa = Hash3(xi + 1, yi, zi, seed) % 12;
		int32 bba = Hash3(xi + 1, yi + 1, zi, seed) % 12;
		int32 bab = Hash3(xi + 1, yi, zi + 1, seed) % 12;
		int32 bbb = Hash3(xi + 1, yi + 1, zi + 1, seed) % 12;

		double x1 = Lerp3(Grad3(aaa, xf, yf, zf), Grad3(baa, xf - 1, yf, zf), u);
		double x2 = Lerp3(Grad3(aba, xf, yf - 1, zf), Grad3(bba, xf - 1, yf - 1, zf), u);
		double y1 = Lerp3(x1, x2, v);
		double x3 = Lerp3(Grad3(aab, xf, yf, zf - 1), Grad3(bab, xf - 1, yf, zf - 1), u);
		double x4 = Lerp3(Grad3(abb, xf, yf - 1, zf - 1), Grad3(bbb, xf - 1, yf - 1, zf - 1), u);
		double y2 = Lerp3(x3, x4, v);
		return Lerp3(y1, y2, w);
	}

	double Fbm(double x, double y, double z, int32 octaves, double persistence, double lacunarity, int32 seed)
	{
		double value = 0, amplitude = 1, frequency = 1, maxValue = 0;
		for (int32 i = 0; i < octaves; i++)
		{
			value += amplitude * Perlin3D(x * frequency, y * frequency, z * frequency, seed + i);
			maxValue += amplitude;
			amplitude *= persistence;
			frequency *= lacunarity;
		}
		return value / maxValue;
	}

	double Worley3D(double x, double y, double z, double freq, int32 seed)
	{
		int32 ix = (int32)floor(x * freq);
		int32 iy = (int32)floor(y * freq);
		int32 iz = (int32)floor(z * freq);
		double minDist = 1e9;
		for (int32 dx = -1; dx <= 1; dx++)
		{
			for (int32 dy = -1; dy <= 1; dy++)
			{
				for (int32 dz = -1; dz <= 1; dz++)
				{
					int32 cx = ix + dx;
					int32 cy = iy + dy;
					int32 cz = iz + dz;
					int32 h = ((cx * 12345) ^ (cy * 67890) ^ (cz * 13579)) & 0x7FFFFFFF;
					h += (int32)((int64)seed * 123456789);
					double sx = (h % 1000) / 1000.0;
					double sy = ((h / 1000) % 1000) / 1000.0;
					double sz = ((h / 1000000) % 1000) / 1000.0;
					double dx2 = x * freq - (cx + sx);
					double dy2 = y * freq - (cy + sy);
					double dz2 = z * freq - (cz + sz);
					double dist = sqrt(dx2 * dx2 + dy2 * dy2 + dz2 * dz2);
					if (dist < minDist) minDist = dist;
				}
			}
		}
		return minDist;
	}

	double InvertedWorley(double x, double y, double z, double freq, int32 seed)
	{
		return 1.0 - fmin(Worley3D(x, y, z, freq, seed), 1.0);
	}

	TArray<FVec3> FibonacciSphere(double cx, double cy, double cz, double rx, double ry, double rz, int32 numPoints)
	{
		TArray<FVec3> points;
		points.Reserve(numPoints);
		double angleInc = UE_DOUBLE_PI * 2 * PHI;
		for (int32 i = 0; i < numPoints; i++)
		{
			double t = i / (double)numPoints;
			double inclination = acos(1 - 2 * t);
			double azimuth = angleInc * i;
			double sx = sin(inclination) * cos(azimuth);
			double sy = sin(inclination) * sin(azimuth);
			double sz = cos(inclination);
			points.Add({ cx + sx * rx, cy + sy * ry, cz + sz * rz });
		}
		return points;
	}

	TArray<FVec3> FibonacciSpiral2D(int32 count)
	{
		TArray<FVec3> points;
		points.Reserve(count);
		double angleInc = UE_DOUBLE_PI * 2 * PHI;
		for (int32 i = 0; i < count; i++)
		{
			double r = sqrt(i / (double)count);
			double theta = angleInc * i;
			points.Add({ r * cos(theta), 0, r * sin(theta) });
		}
		return points;
	}

	FVec3 PerturbDir(double nx, double ny, double nz, double px, double py, double pz, double strength, int32 seed)
	{
		double a1 = Perlin3D(px * 10, py * 10, pz * 10, seed) * strength;
		double a2 = Perlin3D(px * 10, py * 10, pz * 10 + 50, seed + 1) * strength;
		double cos1 = cos(a1), sin1 = sin(a1);
		double cos2 = cos(a2), sin2 = sin(a2);
		double nx2 = nx * cos1 - ny * sin1;
		double ny2 = nx * sin1 + ny * cos1;
		double nz2 = nz;
		double nx3 = nx2 * cos2 + nz2 * sin2;
		double ny3 = ny2;
		double nz3 = -nx2 * sin2 + nz2 * cos2;
		double len = sqrt(nx3 * nx3 + ny3 * ny3 + nz3 * nz3);
		if (len == 0) len = 1;
		return { nx3 / len, ny3 / len, nz3 / len };
	}
}