// Copyright 2026 ProjectC. All rights reserved.

#pragma once

#include "CoreMinimal.h"
#include "CloudTypes.generated.h"

// Cloud archetype enum - maps to same integers as Unity (for JSON compatibility)
UENUM(BlueprintType)
enum class ECloudArchetype : uint8
{
	Sphere = 0	UMETA(DisplayName = "Sphere"),
	Column = 1	UMETA(DisplayName = "Column"),
	Platform = 2	UMETA(DisplayName = "Platform"),
	Tree = 3	UMETA(DisplayName = "Tree")
};

// Size range for sphere placement
USTRUCT(BlueprintType)
struct FCloudSizeRange
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "1"))
	int32 Min = 5;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "1"))
	int32 Max = 20;
};

// Column architecture parameters
USTRUCT(BlueprintType)
struct FColumnParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "10", ClampMax = "100"))
	float Height = 40.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "3", ClampMax = "30"))
	float BaseRadius = 8.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "0", ClampMax = "15"))
	float TopRadius = 3.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "3", ClampMax = "20"))
	int32 Floors = 12;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "3", ClampMax = "12"))
	int32 RingsPerFloor = 8;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column", meta = (ClampMin = "0", ClampMax = "1"))
	float Wobble = 0.3f;
};

// Platform architecture parameters
USTRUCT(BlueprintType)
struct FPlatformParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "20", ClampMax = "200"))
	float Width = 50.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "20", ClampMax = "200"))
	float Depth = 50.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "1", ClampMax = "10"))
	float CenterThickness = 5.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "0.1", ClampMax = "5"))
	float EdgeThickness = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "10", ClampMax = "500"))
	int32 InteriorDensity = 120;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform", meta = (ClampMin = "1", ClampMax = "8"))
	int32 EdgeRings = 4;
};

// Tree architecture parameters
USTRUCT(BlueprintType)
struct FTreeParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "3", ClampMax = "30"))
	float BaseRadius = 8.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "2", ClampMax = "8"))
	int32 MaxDepth = 5;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0.5", ClampMax = "3"))
	float BranchElongation = 1.8f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0.5", ClampMax = "0.95"))
	float TaperRatio = 0.8f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "15", ClampMax = "60"))
	float BranchAngle = 40.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0.2", ClampMax = "0.8"))
	float BranchProbability = 0.5f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0", ClampMax = "1"))
	float TrunkUpBias = 0.4f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0.5", ClampMax = "1"))
	float LengthFalloff = 0.75f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree", meta = (ClampMin = "0.5", ClampMax = "1"))
	float ThicknessFalloff = 0.85f;
};

// Generated sphere data
USTRUCT(BlueprintType)
struct FCloudSphere
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	FVector Location = FVector::ZeroVector;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	float Radius = 1.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	float Density = 0.6f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	ECloudArchetype Archetype = ECloudArchetype::Sphere;
};

// Layer configuration - single layer of cloud generation
USTRUCT(BlueprintType)
struct FCloudLayerConfig
{
	GENERATED_BODY()

	// Common parameters
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	bool Enabled = true;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	FVector LocationOffset = FVector::ZeroVector;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "0"))
	float YOffset = 0.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (UIMin = "1", UIMax = "999999"))
	int32 Seed = 42;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "0", ClampMax = "1"))
	float Density = 0.6f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "0", ClampMax = "0.5"))
	float Jitter = 0.3f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "0", ClampMax = "1"))
	float Clustering = 0.5f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "0", ClampMax = "1"))
	float PositionVariation = 0.5f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	int32 NoiseSalt = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common", meta = (ClampMin = "-999", ClampMax = "10000"))
	int32 CondensationLevel = -999;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	FCloudSizeRange SizeRange;

	// Archetype selection
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Archetype")
	ECloudArchetype Archetype = ECloudArchetype::Sphere;

	// Sphere-specific parameters
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "20", ClampMax = "200"))
	float CloudSize = 80.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "1", ClampMax = "5"))
	int32 CascadeDepth = 3;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "12", ClampMax = "128"))
	int32 BumpsPerLevel = 24;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "10", ClampMax = "200"))
	float ChildRatio = 30.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "0", ClampMax = "1.5"))
	float SizeVariation = 1.0f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "1", ClampMax = "12"))
	int32 ParentCount = 1;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "20", ClampMax = "150"))
	float EllipsoidY = 50.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "50", ClampMax = "150"))
	float EllipsoidXZ = 100.f;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere", meta = (ClampMin = "100", ClampMax = "50000"))
	int32 MaxSphereCount = 5000;

	// Archetype-specific params
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	FColumnParams ColumnParams;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	FPlatformParams PlatformParams;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	FTreeParams TreeParams;
};

// JSON config wrapper
USTRUCT(BlueprintType)
struct FCloudConfigExport
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Config")
	TArray<FCloudLayerConfig> Layers;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Config")
	FString GeneratorVersion = TEXT("6.0");
}