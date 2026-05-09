// CloudGeneratorBPLibrary.cpp
// Implementation of Blueprint functions
#include "CloudGeneratorBPLibrary.h"
#include "CloudMath.h"
#include "Json.h"
#include "JsonUtilities.h"
#include "HAL/FileManager.h"

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

	double Next() { State ^= State << 13; State ^= State >> 17; State ^= State << 5; return State / (double)UINT32_MAX; }
	double NextDouble() { return Next(); }
};

struct FStackFrame { FVector Pos; double Radius; int32 Depth; };

// Helper to generate spheres for one archetype
TArray<FVector> GenerateArchetypeSpheres(int32 Archetype, int32 Seed, float Density, float Jitter, float PositionVariation,
	float YOffset, int32 SizeMin, int32 SizeMax,
	float CloudSize, int32 CascadeDepth, int32 BumpsPerLevel, float ChildRatio,
	float Height, float BaseRadius, float TopRadius, int32 Floors, int32 RingsPerFloor, float Wobble,
	float Width, float Depth, float CenterThickness, float EdgeThickness, int32 InteriorDensity,
	float BaseRadiusT, int32 MaxDepth, float BranchElong, float TaperRatio, float BranchAngle, float BranchProb)
{
	TArray<FVector> Result;
	if (Seed == 0) Seed = 42;
	FDeterministicRandom RNG(Seed);

	if (Archetype == 0) // Sphere
	{
		double BaseR = CloudSize * 0.5;
		double RX = BaseR, RY = BaseR, RZ = BaseR;
		TArray<TPair<FVector, double>> Stack;
		Stack.Add(TPair<FVector, double>(FVector::ZeroVector, BaseR));

		while (Stack.Num() > 0 && Result.Num() < 5000)
		{
			auto [Pos, Radius] = Stack.Last(); Stack.Pop();
			if (Radius < SizeMin * 1.5) continue;

			auto Points = FibonacciSphere(Pos.X, Pos.Y, Pos.Z, Radius, Radius, Radius, BumpsPerLevel);
			for (size_t i = 0; i < Points.size() && Result.Num() < 5000; i++)
			{
				auto P = Points[i];
				double DistN = Perlin3D(P.X * 0.1, P.Y * 0.1, P.Z * 0.1, Seed + i + 100);
				if ((DistN + 1) * 0.5 < ChildRatio / 100.0f * (1 - Jitter * 0.5f)) continue;
				double SizeN = pow((Perlin3D(P.X * 0.2, P.Y * 0.2, P.Z * 0.2, Seed + 200 + i) + 1) * 0.5, 1.8);
				double NewR = SizeMin + SizeN * (SizeMax - SizeMin);
				Result.Add(FVector(P.X, P.Y, P.Z));
				Stack.Add(TPair<FVector, double>(FVector(P.X, P.Y, P.Z), NewR));
			}
		}
	}
	else if (Archetype == 1) // Column
	{
		double H = Height, BR = BaseRadius, TR = TopRadius;
		int32 F = FMath::Max(Floors, 1), RPF = FMath::Max(RingsPerFloor, 1);
		double FloorSpacing = H / FMath::Max(F - 1, 1);

		for (int32 Floor = 0; Floor < F; Floor++)
		{
			double T = Floor / (double)FMath::Max(F - 1, 1);
			double FR = BR + (TR - BR) * T;
			double Y = Floor * FloorSpacing;

			for (int32 R = 0; R < RPF; R++)
			{
				double Angle = (R / (double)RPF) * UE_DOUBLE_PI * 2 + Perlin3D(Floor * 0.3, R * 0.5, Seed, Seed) * 0.5 * (1 + Jitter * 2);
				double Dist = FR * (0.3 + (Perlin3D(Floor * 0.5, R * 0.7, Seed + 50, Seed) + 1) * 0.35);
				double jitR = Perlin3D(Floor, R, Seed, Seed) * Jitter * 0.5;
				double SizeN = pow((Perlin3D(Floor + R * 0.1, R + Floor * 0.1, Seed + 200, Seed) + 1) * 0.5, 1.8);
				double SphereR = BR * (SizeMin + SizeN * (SizeMax - SizeMin)) * 0.12 * (0.1 + 0.9 * SizeN);
				double OX = (RNG.NextDouble() - 0.5) * 12 * SphereR * PositionVariation;
				double OY = (RNG.NextDouble() - 0.5) * 12 * SphereR * PositionVariation;
				double OZ = (RNG.NextDouble() - 0.5) * 12 * SphereR * PositionVariation;
				Result.Add(FVector(cos(Angle) * Dist + OX, Y + jitR * 0.3 + OY, sin(Angle) * Dist + OZ));
			}
		}
	}
	else if (Archetype == 2) // Platform
	{
		Result.Add(FVector(0, 0, 0));
		auto Points = FibonacciSpiral2D(InteriorDensity);
		for (size_t i = 0; i < Points.size(); i++)
		{
			auto P = Points[i];
			double PX = P.X * Width * 0.5, PZ = P.Z * Depth * 0.5;
			double Dist = sqrt((PX * PX) / (Width * Width * 0.25) + (PZ * PZ) / (Depth * Depth * 0.25));
			double Thick = CenterThickness * exp(-Dist * Dist * 3) + EdgeThickness * Dist;
			if (Thick < 0.2) continue;
			double YN = Perlin3D(PX * 0.3, PZ * 0.3, Seed + 500, Seed);
			double Y = YN * Thick * 0.5 + Jitter * SizeMin * Perlin3D(PX, PZ, Seed, Seed + 1);
			double SizeN = pow((Perlin3D(PX * 0.5, PZ * 0.5, Seed + 600 + i, Seed) + 1) * 0.5, 1.8);
			double R = (SizeMin + SizeN * (SizeMax - SizeMin)) * (0.1 + 0.9 * SizeN) * (1 + Jitter);
			double OX = (RNG.NextDouble() - 0.5) * 12 * R * PositionVariation;
			double OY = (RNG.NextDouble() - 0.5) * 12 * R * PositionVariation;
			double OZ = (RNG.NextDouble() - 0.5) * 12 * R * PositionVariation;
			Result.Add(FVector(PX + OX, Y + OY, PZ + OZ));
		}
	}
	else // Tree
	{
		TArray<TPair<FVector, FVector>> Stack;
		Stack.Add(TPair<FVector, FVector>(FVector::ZeroVector, FVector(0, 1, 0)));
		double BAngle = BranchAngle * M_PI / 180.0;

		while (Stack.Num() > 0 && Result.Num() < 2000)
		{
			auto [Pos, Dir] = Stack.Last(); Stack.Pop();
			Result.Add(Pos);
			for (int32 B = 0; B < 3; B++)
			{
				if (RNG.NextDouble() > BranchProb) continue;
				double AX = (RNG.NextDouble() - 0.5) * BAngle * 2;
				double AZ = (RNG.NextDouble() - 0.5) * M_PI * 2;
				FVector NewDir = Dir.RotateAngleAxis(AX * 180 / M_PI, FVector::CrossProduct(Dir, FVector::UpVector).GetSafeNormal());
				NewDir.Z += 0.1f * BranchElong;
				NewDir.Normalize();
				double Len = Radius * BranchElong;
				FVector NewPos = Pos + NewDir * Len;
				Stack.Add(TPair<FVector, FVector>(NewPos, NewDir));
			}
		}
	}

	// Apply Y offset
	for (auto& V : Result)
		V.Y += YOffset;

	return Result;
}

TArray<FCloudSphereBP> UCloudGeneratorBPLibrary::GenerateFromJSON(const FString& JSONString)
{
	TArray<FCloudLayerConfigBP> Layers;
	if (!JSONToLayers(JSONString, Layers))
		return TArray<FCloudSphereBP>();

	return GenerateFromConfig(Layers[0]);
}

TArray<FCloudSphereBP> UCloudGeneratorBPLibrary::GenerateFromFile(const FString& FilePath)
{
	if (!FPlatformFileManager::Get().GetPlatformFile().FileExists(*FilePath))
		return TArray<FCloudSphereBP>();

	FString Content;
	if (!FFileHelper::LoadFileToString(Content, *FilePath))
		return TArray<FCloudSphereBP>();

	return GenerateFromJSON(Content);
}

TArray<FCloudSphereBP> UCloudGeneratorBPLibrary::GenerateFromConfig(const FCloudLayerConfigBP& Config)
{
	TArray<FCloudSphereBP> Result;
	if (!Config.Enabled) return Result;

	auto Positions = GenerateArchetypeSpheres(
		(int32)Config.Archetype, Config.Seed, Config.Density, Config.Jitter, Config.PositionVariation,
		Config.YOffset, Config.SizeRange.Min, Config.SizeRange.Max,
		Config.CloudSize, Config.CascadeDepth, Config.BumpsPerLevel, Config.ChildRatio,
		Config.ColumnParams.Height, Config.ColumnParams.BaseRadius, Config.ColumnParams.TopRadius,
		Config.ColumnParams.Floors, Config.ColumnParams.RingsPerFloor, Config.ColumnParams.Wobble,
		Config.PlatformParams.Width, Config.PlatformParams.Depth,
		Config.PlatformParams.CenterThickness, Config.PlatformParams.EdgeThickness, Config.PlatformParams.InteriorDensity,
		Config.TreeParams.BaseRadius, Config.TreeParams.MaxDepth, Config.TreeParams.BranchElongation,
		Config.TreeParams.TaperRatio, Config.TreeParams.BranchAngle * M_PI / 180.0f, Config.TreeParams.BranchProbability
	);

	for (auto& Pos : Positions)
		Result.Add(FCloudSphereBP{ Pos, 1.0f, Config.Density, Config.Archetype });

	return Result;
}

FString UCloudGeneratorBPLibrary::LayersToJSON(const TArray<FCloudLayerConfigBP>& Layers)
{
	TArray<TSharedPtr<FJsonValue>> LayerArray;
	for (auto& L : Layers)
	{
		TSharedPtr<FJsonObject> Obj = MakeShared<FJsonObject>();
		Obj->SetBoolField(TEXT("Enabled"), L.Enabled);
		Obj->SetNumberField(TEXT("YOffset"), L.YOffset);
		Obj->SetIntegerField(TEXT("Seed"), L.Seed);
		Obj->SetNumberField(TEXT("Density"), L.Density);
		Obj->SetNumberField(TEXT("Jitter"), L.Jitter);
		Obj->SetNumberField(TEXT("Clustering"), L.Clustering);
		Obj->SetNumberField(TEXT("PositionVariation"), L.PositionVariation);
		Obj->SetIntegerField(TEXT("Archetype"), (int32)L.Archetype);
		TSharedPtr<FJsonObject> SR = MakeShared<FJsonObject>();
		SR->SetIntegerField(TEXT("Min"), L.SizeRange.Min);
		SR->SetIntegerField(TEXT("Max"), L.SizeRange.Max);
		Obj->SetObjectField(TEXT("SizeRange"), SR);
		TSharedPtr<FJsonObject> CP = MakeShared<FJsonObject>();
		CP->SetNumberField(TEXT("Height"), L.ColumnParams.Height);
		CP->SetNumberField(TEXT("BaseRadius"), L.ColumnParams.BaseRadius);
		Obj->SetObjectField(TEXT("ColumnParams"), CP);
		LayerArray.Add(MakeShared<FJsonValueObject>(Obj));
	}
	TSharedPtr<FJsonObject> Root = MakeShared<FJsonObject>();
	Root->SetArrayField(TEXT("layers"), LayerArray);
	Root->SetStringField(TEXT("generatorVersion"), TEXT("6.1"));
	FString Result;
	FJsonSerializer::Serialize(Root, TJsonWriterFactory<TCHAR, TPrettyJsonPrintPolicy<TCHAR>>::Create(&Result));
	return Result;
}

bool UCloudGeneratorBPLibrary::JSONToLayers(const FString& JSONString, TArray<FCloudLayerConfigBP>& OutLayers)
{
	TSharedPtr<FJsonObject> Root;
	TSharedRef<TJsonReader<>> Reader = TJsonReaderFactory<>::Create(JSONString);
	if (!FJsonSerializer::Deserialize(Reader, Root)) return false;

	const TArray<TSharedPtr<FJsonValue>>* LayerArray;
	if (!Root->TryGetArrayField(TEXT("layers"), LayerArray)) return false;

	OutLayers.Empty();
	for (auto& V : *LayerArray)
	{
		TSharedPtr<FJsonObject> Obj = V->AsObject();
		if (!Obj.IsValid()) continue;

		FCloudLayerConfigBP Layer;
		Layer.Enabled = Obj->GetBoolField(TEXT("Enabled"));
		Layer.YOffset = Obj->GetNumberField(TEXT("YOffset"));
		Layer.Seed = Obj->GetIntegerField(TEXT("Seed"));
		Layer.Density = Obj->GetNumberField(TEXT("Density"));
		Layer.Jitter = Obj->GetNumberField(TEXT("Jitter"));
		Layer.Clustering = Obj->GetNumberField(TEXT("Clustering"));
		Layer.PositionVariation = Obj->GetNumberField(TEXT("PositionVariation"));
		Layer.Archetype = (ECloudArchetypeBP)Obj->GetIntegerField(TEXT("Archetype"));

		if (TSharedPtr<FJsonObject> SR = Obj->GetObjectField(TEXT("SizeRange")))
		{
			Layer.SizeRange.Min = SR->GetIntegerField(TEXT("Min"));
			Layer.SizeRange.Max = SR->GetIntegerField(TEXT("Max"));
		}
		if (TSharedPtr<FJsonObject> CP = Obj->GetObjectField(TEXT("ColumnParams")))
		{
			Layer.ColumnParams.Height = CP->GetNumberField(TEXT("Height"));
			Layer.ColumnParams.BaseRadius = CP->GetNumberField(TEXT("BaseRadius"));
		}
		OutLayers.Add(Layer);
	}
	return true;
}