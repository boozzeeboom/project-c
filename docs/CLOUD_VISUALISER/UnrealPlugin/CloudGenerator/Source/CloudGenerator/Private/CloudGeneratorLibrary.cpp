// Copyright 2026 ProjectC. All rights reserved.

#include "CloudGeneratorLibrary.h"
#include "CloudMath.h"
#include "Json.h"
#include "JsonUtilities.h"

using namespace CloudMath;

struct FDeterministicRandom
{
	uint32 State;

	FDeterministicRandom(int32 seed)
	{
		State = (uint32)(seed != 0 ? seed : 1);
		for (int32 i = 0; i < 10; i++)
		{
			State ^= State << 13;
			State ^= State >> 17;
			State ^= State << 5;
		}
	}

	double Next()
	{
		State ^= State << 13;
		State ^= State >> 17;
		State ^= State << 5;
		return State / (double)TNumericLimits<uint32>::Max();
	}

	double NextDouble() { return Next(); }
};

TArray<FCloudSphere> UCloudGeneratorLibrary::GenerateFromLayers(const TArray<FCloudLayerConfig>& Layers)
{
	TArray<FCloudSphere> AllSpheres;
	double CumulativeY = 0;

	for (int32 i = 0; i < Layers.Num(); i++)
	{
		const FCloudLayerConfig& Layer = Layers[i];
		if (!Layer.Enabled) continue;

		FDeterministicRandom RNG(Layer.Seed);
		TArray<FCloudSphere> RawSpheres;

		// Generate based on archetype
		switch (Layer.Archetype)
		{
		case ECloudArchetype::Sphere:
			// Sphere generation - recursive cascade
			{
				double CloudSize = Layer.CloudSize;
				double BaseRadius = CloudSize * 0.5;
				double RX = BaseRadius * (Layer.EllipsoidXZ / 100.0);
				double RY = BaseRadius * (Layer.EllipsoidY / 100.0);
				double RZ = RX;
				int32 MaxSpheres = Layer.MaxSphereCount;
				double MinRadius = Layer.SizeRange.Min;
				double SizeMax = Layer.SizeRange.Max;
				double ChildRatio = Layer.ChildRatio;
				int32 CascadeDepth = Layer.CascadeDepth;
				int32 BumpsPerLevel = Layer.BumpsPerLevel;
				double SizeVariation = Layer.SizeVariation;
				double Jitter = Layer.Jitter;

				for (int32 p = 0; p < Layer.ParentCount; p++)
				{
					double OffsetAngle = (p / (double)Layer.ParentCount) * M_PI * 2;
					double Spread = Layer.ParentCount > 1 ? (p % 3) * 0.15 : 0;
					double PX = cos(OffsetAngle) * RX * Spread;
					double PY = (p % 2 == 0 ? 1 : -1) * RY * 0.1 * Spread;
					double PZ = sin(OffsetAngle) * RZ * Spread;

					RawSpheres.Add(FCloudSphere{ FVector(PX, PY, PZ), (float)BaseRadius, 1.f, ECloudArchetype::Sphere });

					// Stack for cascade
					TArray<TTuple<FVector, double, int32, double>> Stack;
					Stack.Add(TTuple<FVector, double, int32, double>(FVector(PX, PY, PZ), BaseRadius, 0, 1.0));

					while (Stack.Num() > 0 && RawSpheres.Num() < MaxSpheres)
					{
						auto [X, Y, Z, Radius, Depth, Density] = Stack.Last();
						Stack.Pop();

						if (Depth >= CascadeDepth) continue;
						if (Radius < MinRadius * 1.5) continue;

						// Fibonacci sphere surface points
						auto SurfacePoints = FibonacciSphere(X, Y, Z, Radius, Radius, Radius, BumpsPerLevel);

						for (int32 b = 0; b < SurfacePoints.size() && RawSpheres.Num() < MaxSpheres; b++)
						{
							auto SP = SurfacePoints[b];
							double DistNoise = Perlin3D(SP.X * 0.1, SP.Y * 0.1, SP.Z * 0.1, Layer.Seed + b + 100);
							double ShouldSpawn = (DistNoise + 1) * 0.5;

							if (ShouldSpawn < ChildRatio / 100.0 * (1 - Jitter * 0.5)) continue;

							double SizeNoise = pow((Perlin3D(SP.X * 0.2, SP.Y * 0.2, SP.Z * 0.2, Layer.Seed + 200 + b) + 1) * 0.5, 1.8);
							double NewRadius = MinRadius + SizeNoise * (SizeMax - MinRadius);
							NewRadius *= SizeVariation;

							auto Perturbed = PerturbDir(
								SP.X - X, SP.Y - Y, SP.Z - Z,
								SP.X, SP.Y, SP.Z,
								Jitter * 0.5, Layer.Seed + b);

							double NewX = SP.X + Perturbed.X * Radius * 0.3;
							double NewY = SP.Y + Perturbed.Y * Radius * 0.3;
							double NewZ = SP.Z + Perturbed.Z * Radius * 0.3;
							double NewDensity = Density * (0.7 + (1 - SizeNoise) * 0.3);

							RawSpheres.Add(FCloudSphere{ FVector(NewX, NewY, NewZ), (float)NewRadius, (float)NewDensity, ECloudArchetype::Sphere });
							Stack.Add(TTuple<FVector, double, int32, double>(FVector(NewX, NewY, NewZ), NewRadius, Depth + 1, NewDensity));
						}
					}
				}
			}
			break;

		case ECloudArchetype::Column:
			{
				double Height = Layer.ColumnParams.Height;
				double BaseRadius = Layer.ColumnParams.BaseRadius;
				double TopRadius = Layer.ColumnParams.TopRadius;
				int32 Floors = Layer.ColumnParams.Floors;
				int32 RingsPerFloor = Layer.ColumnParams.RingsPerFloor;
				double Wobble = Layer.ColumnParams.Wobble / 100.0;
				int32 SizeMin = Layer.SizeRange.Min;
				int32 SizeMax = Layer.SizeRange.Max;
				double FloorSpacing = Height / FMath::Max(Floors - 1, 1);

				for (int32 Floor = 0; Floor < Floors; Floor++)
				{
					double T = Floor / (double)FMath::Max(Floors - 1, 1);
					double FloorRadius = BaseRadius + (TopRadius - BaseRadius) * T;
					double Y = Floor * FloorSpacing;

					double WobbleX = Perlin3D(Floor * 0.7, 0, 0, Layer.Seed) * Wobble * FloorRadius + Jitter * BaseRadius * 2.0 * Perlin3D(Floor, 0, 0, Layer.Seed + 1);
					double WobbleZ = Perlin3D(Floor * 0.7, 0, 0, Layer.Seed + 100) * Wobble * FloorRadius + Jitter * BaseRadius * 2.0 * Perlin3D(Floor, 0, 0, Layer.Seed + 2);

					for (int32 R = 0; R < RingsPerFloor; R++)
					{
						double Angle = (R / (double)RingsPerFloor) * M_PI * 2 + Perlin3D(Floor * 0.3, R * 0.5, Layer.Seed, Layer.Seed) * 0.5 * (1 + Jitter * 2);
						double DistNoise = Perlin3D(Floor * 0.5, R * 0.7, Layer.Seed + 50, Layer.Seed);
						double DistFromCenter = FloorRadius * (0.3 + (DistNoise + 1) * 0.5 * 0.7 + 0.5) * 0.5;
						double jitR = Perlin3D(Floor, R, Layer.Seed, Layer.Seed) * Jitter * 0.5;

						double RNoiseBase = pow((Perlin3D(Floor + R * 0.1, R + Floor * 0.1, Layer.Seed + 200, Layer.Seed) + 1) * 0.5, 1.8);
						double RNoiseMult = pow((Perlin3D(Floor + R * 0.1 + 50, R + Floor * 0.1 + 50, Layer.Seed + 250, Layer.Seed) + 1) * 0.5, 1.5);
						double SizeBase = SizeMin + RNoiseBase * (SizeMax - SizeMin);
						double SphereRadius = BaseRadius * SizeBase * 0.12 * (0.1 + RNoiseMult * 0.9);

						double ColOX = (RNG.NextDouble() - 0.5) * 12.0 * SphereRadius * Layer.PositionVariation;
						double ColOY = (RNG.NextDouble() - 0.5) * 12.0 * SphereRadius * Layer.PositionVariation;
						double ColOZ = (RNG.NextDouble() - 0.5) * 12.0 * SphereRadius * Layer.PositionVariation;

						RawSpheres.Add(FCloudSphere{
							FVector(cos(Angle) * DistFromCenter + WobbleX + ColOX, Y + jitR * 0.3 + ColOY, sin(Angle) * DistFromCenter + WobbleZ + ColOZ),
							FMath::Max(SphereRadius, 0.5f),
							(float)(0.6 + (1 - T) * 0.3),
							ECloudArchetype::Column
						});
					}

					if (Floor % 2 == 0)
					{
						RawSpheres.Add(FCloudSphere{ FVector(WobbleX, Y, WobbleZ), (float)(FloorRadius * 0.3), 0.8f, ECloudArchetype::Column });
					}
				}
			}
			break;

		case ECloudArchetype::Platform:
			{
				float Width = Layer.PlatformParams.Width;
				float Depth = Layer.PlatformParams.Depth;
				float CenterThickness = Layer.PlatformParams.CenterThickness;
				float EdgeThickness = Layer.PlatformParams.EdgeThickness;
				int32 InteriorDensity = Layer.PlatformParams.InteriorDensity;
				int32 SizeMin = Layer.SizeRange.Min;

				RawSpheres.Add(FCloudSphere{ FVector::ZeroVector, (float)SizeMin, 1.f, ECloudArchetype::Platform });

				auto InteriorPoints = FibonacciSpiral2D(InteriorDensity);

				for (int32 PI = 0; PI < InteriorPoints.size(); PI++)
				{
					auto PT = InteriorPoints[PI];
					double PX = PT.X * Width * 0.5;
					double PZ = PT.Z * Depth * 0.5;

					double Dist = sqrt((PX * PX) / (Width * Width * 0.25) + (PZ * PZ) / (Depth * Depth * 0.25));
					double Thickness = CenterThickness * exp(-Dist * Dist * 3.0) + EdgeThickness * Dist;
					if (Thickness < 0.2) continue;

					double YNoise = Perlin3D(PX * 0.3, PZ * 0.3, Layer.Seed + 500, Layer.Seed);
					double Y = YNoise * Thickness * 0.5 + Jitter * SizeMin * Perlin3D(PX, PZ, Layer.Seed, Layer.Seed + 1);

					double RNoiseBase = pow((Perlin3D(PX * 0.5, PZ * 0.5, Layer.Seed + 600 + PI, Layer.Seed) + 1) * 0.5, 1.8);
					double RNoiseMult = pow((Perlin3D(PX * 0.5 + 70, PZ * 0.5 + 70, Layer.Seed + 650 + PI, Layer.Seed + 1) + 1) * 0.5, 1.5);
					double SizeBase = SizeMin + RNoiseBase * (Layer.SizeRange.Max - SizeMin);
					double JitterFactor = 1 + Jitter * (1 - Layer.Clustering);
					double Radius = SizeBase * (0.1 + RNoiseMult * 0.9) * JitterFactor;

					double DensityVal = (0.3 + 0.7 * (1 - Dist)) * (Layer.Clustering > 0.5 ? (0.7 + RNoiseBase * 0.3) : 1.0);

					if (DensityVal < 0.1 || Radius < 0.3) continue;

					double PlatOX = (RNG.NextDouble() - 0.5) * 12.0 * Radius * Layer.PositionVariation;
					double PlatOY = (RNG.NextDouble() - 0.5) * 12.0 * Radius * Layer.PositionVariation;
					double PlatOZ = (RNG.NextDouble() - 0.5) * 12.0 * Radius * Layer.PositionVariation;

					RawSpheres.Add(FCloudSphere{ FVector(PX + PlatOX, Y + PlatOY, PZ + PlatOZ), (float)Radius, (float)DensityVal, ECloudArchetype::Platform });
				}
			}
			break;

		case ECloudArchetype::Tree:
			{
				int32 SizeMin = Layer.SizeRange.Min;
				int32 SizeMax = Layer.SizeRange.Max;
				int32 MaxDepth = Layer.TreeParams.MaxDepth;
				float BranchAngle = Layer.TreeParams.BranchAngle * M_PI / 180.0f;
				float BranchProb = Layer.TreeParams.BranchProbability;
				float BranchElong = Layer.TreeParams.BranchElongation;
				float Taper = Layer.TreeParams.TaperRatio;
				float TrunkUpBias = Layer.TreeParams.TrunkUpBias;

				RawSpheres.Add(FCloudSphere{ FVector::ZeroVector, (float)SizeMin, 1.f, ECloudArchetype::Tree });

				TArray<TTuple<FVector, FVector, double, int32, double>> Stack;
				Stack.Add(TTuple<FVector, FVector, double, int32, double>(FVector::ZeroVector, FVector(0, 1, 0), SizeMin, 0, 1.0));

				while (Stack.Num() > 0)
				{
					auto [X, Y, Z, DirX, DirY, DirZ, Radius, Depth, LengthMult] = Stack.Last();
					Stack.Pop();

					if (Depth >= MaxDepth) continue;
					if (Radius < 0.5) continue;

					for (int32 B = 0; B < 3; B++)
					{
						double BranchRand = RNG.NextDouble();
						if (BranchRand > BranchProb) continue;

						double AngleX = (RNG.NextDouble() - 0.5) * BranchAngle * 2;
						double AngleZ = (RNG.NextDouble() - 0.5) * M_PI * 2;

						double NewDirX = DirX * cos(AngleX) + DirY * sin(AngleX);
						double NewDirY = DirY * cos(AngleX) - DirX * sin(AngleX);
						double NewDirZ = DirZ * cos(AngleZ) + DirY * sin(AngleZ);
						double NewDirY2 = DirY * cos(AngleZ) - DirZ * sin(AngleZ);

						double Len = Radius * BranchElong;
						double NewRadius = Radius * Taper;

						RawSpheres.Add(FCloudSphere{ FVector(X, Y, Z), (float)Radius * 0.8f, (float)DensityVal, ECloudArchetype::Tree });

						Stack.Add(TTuple<FVector, FVector, double, int32, double>(
							FVector(X + NewDirX * Len, Y + NewDirY * Len, Z + NewDirZ * Len),
							FVector(NewDirX, NewDirY2, NewDirZ),
							NewRadius, Depth + 1, LengthMult * 0.9));
					}
				}
			}
			break;
		}

		// Calculate Y offset
		double MinY = RawSpheres.Num() > 0 ? TNumericLimits<double>::Max() : 0;
		double MaxY = 0;
		for (auto& S : RawSpheres)
		{
			MinY = FMath::Min(MinY, S.Location.Y - S.Radius);
			MaxY = FMath::Max(MaxY, S.Location.Y + S.Radius);
		}

		double Gap = 2.0;
		double YOffset = FMath::Abs(Layer.YOffset) > 0.001 ? Layer.YOffset : CumulativeY - MinY + Gap;
		bool HasCondensation = Layer.CondensationLevel != -999;

		for (auto& S : RawSpheres)
		{
			S.Location.Y += YOffset;
			if (HasCondensation && (S.Location.Y - S.Radius < Layer.CondensationLevel))
				continue;
			AllSpheres.Add(S);
		}

		CumulativeY = YOffset + MaxY + Gap;
	}

	AllSpheres.RemoveAll([](const FCloudSphere& S) { return S.Location.Y < -10000; });
	return AllSpheres;
}

TArray<FCloudSphere> UCloudGeneratorLibrary::GenerateFromJSON(const FString& JSONConfig)
{
	TArray<FCloudLayerConfig> Layers;
	if (!JSONToLayers(JSONConfig, Layers))
	{
		UE_LOG(LogTemp, Error, TEXT("CloudGenerator: Failed to parse JSON config"));
		return TArray<FCloudSphere>();
	}
	return GenerateFromLayers(Layers);
}

FString UCloudGeneratorLibrary::LayersToJSON(const TArray<FCloudLayerConfig>& Layers)
{
	FCloudConfigExport Export;
	Export.Layers = Layers;
	Export.GeneratorVersion = TEXT("6.1");

	FString OutputString;
	FJsonObjectConverter::UStructToJsonObjectString(Export, OutputString, 0, 0, 0, false);
	return OutputString;
}

bool UCloudGeneratorLibrary::JSONToLayers(const FString& JSONConfig, TArray<FCloudLayerConfig>& OutLayers)
{
	TSharedPtr<FJsonObject> JsonObject;
	TSharedRef<TJsonReader<>> JsonReader = TJsonReaderFactory<>::Create(JSONConfig);

	if (!FJsonSerializer::Deserialize(JsonReader, JsonObject))
	{
		return false;
	}

	// Parse layers array
	const TArray<TSharedPtr<FJsonValue>>* LayersArray;
	if (!JsonObject->TryGetArrayField(TEXT("layers"), LayersArray))
	{
		return false;
	}

	OutLayers.Empty();
	for (int32 i = 0; i < LayersArray->Num(); i++)
	{
		const TSharedPtr<FJsonValue>& LayerValue = (*LayersArray)[i];
		TSharedPtr<FJsonObject> LayerObj = LayerValue->AsObject();
		if (!LayerObj.IsValid()) continue;

		FCloudLayerConfig Layer;

		// Parse common fields
		Layer.Enabled = LayerObj->GetBoolField(TEXT("Enabled"));
		Layer.YOffset = LayerObj->GetNumberField(TEXT("YOffset"));
		Layer.Seed = LayerObj->GetIntegerField(TEXT("Seed"));
		Layer.Density = (float)LayerObj->GetNumberField(TEXT("Density"));
		Layer.Jitter = (float)LayerObj->GetNumberField(TEXT("Jitter"));
		Layer.Clustering = (float)LayerObj->GetNumberField(TEXT("Clustering"));
		Layer.PositionVariation = (float)LayerObj->GetNumberField(TEXT("PositionVariation"));
		Layer.NoiseSalt = LayerObj->GetIntegerField(TEXT("NoiseSalt"));
		Layer.CondensationLevel = LayerObj->GetIntegerField(TEXT("CondensationLevel"));

		// Parse archetype (integer to enum)
		int32 ArchetypeInt = LayerObj->GetIntegerField(TEXT("Archetype"));
		Layer.Archetype = (ECloudArchetype)ArchetypeInt;

		// Parse SizeRange
		TSharedPtr<FJsonObject> SizeRangeObj = LayerObj->GetObjectField(TEXT("SizeRange"));
		if (SizeRangeObj.IsValid())
		{
			Layer.SizeRange.Min = SizeRangeObj->GetIntegerField(TEXT("Min"));
			Layer.SizeRange.Max = SizeRangeObj->GetIntegerField(TEXT("Max"));
		}

		// Parse archetype-specific params
		switch (Layer.Archetype)
		{
		case ECloudArchetype::Sphere:
			Layer.CloudSize = (float)LayerObj->GetNumberField(TEXT("CloudSize"));
			Layer.CascadeDepth = LayerObj->GetIntegerField(TEXT("CascadeDepth"));
			Layer.BumpsPerLevel = LayerObj->GetIntegerField(TEXT("BumpsPerLevel"));
			Layer.ChildRatio = (float)LayerObj->GetNumberField(TEXT("ChildRatio"));
			Layer.SizeVariation = (float)LayerObj->GetNumberField(TEXT("SizeVariation"));
			Layer.ParentCount = LayerObj->GetIntegerField(TEXT("ParentCount"));
			Layer.EllipsoidY = (float)LayerObj->GetNumberField(TEXT("EllipsoidY"));
			Layer.EllipsoidXZ = (float)LayerObj->GetNumberField(TEXT("EllipsoidXZ"));
			Layer.MaxSphereCount = LayerObj->GetIntegerField(TEXT("MaxSphereCount"));
			break;

		case ECloudArchetype::Column:
			{
				TSharedPtr<FJsonObject> ColParams = LayerObj->GetObjectField(TEXT("ColumnParams"));
				if (ColParams.IsValid())
				{
					Layer.ColumnParams.Height = (float)ColParams->GetNumberField(TEXT("Height"));
					Layer.ColumnParams.BaseRadius = (float)ColParams->GetNumberField(TEXT("BaseRadius"));
					Layer.ColumnParams.TopRadius = (float)ColParams->GetNumberField(TEXT("TopRadius"));
					Layer.ColumnParams.Floors = ColParams->GetIntegerField(TEXT("Floors"));
					Layer.ColumnParams.RingsPerFloor = ColParams->GetIntegerField(TEXT("RingsPerFloor"));
					Layer.ColumnParams.Wobble = (float)ColParams->GetNumberField(TEXT("Wobble"));
				}
			}
			break;

		case ECloudArchetype::Platform:
			{
				TSharedPtr<FJsonObject> PlatParams = LayerObj->GetObjectField(TEXT("PlatformParams"));
				if (PlatParams.IsValid())
				{
					Layer.PlatformParams.Width = (float)PlatParams->GetNumberField(TEXT("Width"));
					Layer.PlatformParams.Depth = (float)PlatParams->GetNumberField(TEXT("Depth"));
					Layer.PlatformParams.CenterThickness = (float)PlatParams->GetNumberField(TEXT("CenterThickness"));
					Layer.PlatformParams.EdgeThickness = (float)PlatParams->GetNumberField(TEXT("EdgeThickness"));
					Layer.PlatformParams.InteriorDensity = PlatParams->GetIntegerField(TEXT("InteriorDensity"));
					Layer.PlatformParams.EdgeRings = PlatParams->GetIntegerField(TEXT("EdgeRings"));
				}
			}
			break;

		case ECloudArchetype::Tree:
			{
				TSharedPtr<FJsonObject> TreeParams = LayerObj->GetObjectField(TEXT("TreeParams"));
				if (TreeParams.IsValid())
				{
					Layer.TreeParams.BaseRadius = (float)TreeParams->GetNumberField(TEXT("BaseRadius"));
					Layer.TreeParams.MaxDepth = TreeParams->GetIntegerField(TEXT("MaxDepth"));
					Layer.TreeParams.BranchElongation = (float)TreeParams->GetNumberField(TEXT("BranchElongation"));
					Layer.TreeParams.TaperRatio = (float)TreeParams->GetNumberField(TEXT("TaperRatio"));
					Layer.TreeParams.BranchAngle = (float)TreeParams->GetNumberField(TEXT("BranchAngle"));
					Layer.TreeParams.BranchProbability = (float)TreeParams->GetNumberField(TEXT("BranchProbability"));
					Layer.TreeParams.TrunkUpBias = (float)TreeParams->GetNumberField(TEXT("TrunkUpBias"));
					Layer.TreeParams.LengthFalloff = (float)TreeParams->GetNumberField(TEXT("LengthFalloff"));
					Layer.TreeParams.ThicknessFalloff = (float)TreeParams->GetNumberField(TEXT("ThicknessFalloff"));
				}
			}
			break;
		}

		OutLayers.Add(Layer);
	}

	return true;
}