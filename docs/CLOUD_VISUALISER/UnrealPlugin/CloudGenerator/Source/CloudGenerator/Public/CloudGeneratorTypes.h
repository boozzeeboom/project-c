// CloudGeneratorTypes.h
// Blueprint structs for Cloud Generator plugin
#pragma once

#include "CoreMinimal.h"
#include "CloudGeneratorTypes.generated.h"

// Archetype enum - same values as Unity for JSON compatibility
UENUM(BlueprintType)
enum class ECloudArchetypeBP : uint8
{
	Sphere = 0	UMETA(DisplayName = "Sphere"),
	Column = 1	UMETA(DisplayName = "Column"),
	Platform = 2	UMETA(DisplayName = "Platform"),
	Tree = 3	UMETA(DisplayName = "Tree")
};

// Size range
USTRUCT(BlueprintType)
struct FCloudSizeRangeBP
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	int32 Min = 5;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	int32 Max = 20;
};

// Column params
USTRUCT(BlueprintType)
struct FColumnParamsBP
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	float Height = 40.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	float BaseRadius = 8.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	float TopRadius = 3.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	int32 Floors = 12;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	int32 RingsPerFloor = 8;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	float Wobble = 0.3f;
};

// Platform params
USTRUCT(BlueprintType)
struct FPlatformParamsBP
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	float Width = 50.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	float Depth = 50.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	float CenterThickness = 5.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	float EdgeThickness = 1.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	int32 InteriorDensity = 120;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	int32 EdgeRings = 4;
};

// Tree params
USTRUCT(BlueprintType)
struct FTreeParamsBP
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float BaseRadius = 8.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	int32 MaxDepth = 5;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float BranchElongation = 1.8f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float TaperRatio = 0.8f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float BranchAngle = 40.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float BranchProbability = 0.5f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float TrunkUpBias = 0.4f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float LengthFalloff = 0.75f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	float ThicknessFalloff = 0.85f;
};

// Output sphere
USTRUCT(BlueprintType)
struct FCloudSphereBP
{
	GENERATED_BODY()
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	FVector Location = FVector::ZeroVector;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	float Radius = 1.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	float Density = 0.6f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Result")
	ECloudArchetypeBP Archetype = ECloudArchetypeBP::Sphere;
};

// Layer config
USTRUCT(BlueprintType)
struct FCloudLayerConfigBP
{
	GENERATED_BODY()

	// Common
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	bool Enabled = true;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	float YOffset = 0.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	int32 Seed = 42;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	float Density = 0.6f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	float Jitter = 0.3f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	float Clustering = 0.5f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	float PositionVariation = 0.5f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Common")
	FCloudSizeRangeBP SizeRange;

	// Archetype
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Archetype")
	ECloudArchetypeBP Archetype = ECloudArchetypeBP::Sphere;

	// Sphere
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	float CloudSize = 80.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	int32 CascadeDepth = 3;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	int32 BumpsPerLevel = 24;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	float ChildRatio = 30.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	float SizeVariation = 1.0f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	int32 ParentCount = 1;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	float EllipsoidY = 50.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	float EllipsoidXZ = 100.f;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Sphere")
	int32 MaxSphereCount = 5000;

	// Archetype params
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Column")
	FColumnParamsBP ColumnParams;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Platform")
	FPlatformParamsBP PlatformParams;
	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Cloud|Tree")
	FTreeParamsBP TreeParams;
};