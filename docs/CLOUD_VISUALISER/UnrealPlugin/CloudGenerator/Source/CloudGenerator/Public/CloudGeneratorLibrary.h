// Copyright 2026 ProjectC. All rights reserved.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "CloudGeneratorLibrary.generated.h"

// Forward declarations
struct FCloudLayerConfig;
struct FCloudSphere;

UCLASS()
class CLOUDGENERATOR_API UCloudGeneratorLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	// Generate clouds from JSON config (same format as Unity)
	// JSON should have "layers" array with archetype as integer (0=Sphere, 1=Column, etc.)
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Generate From JSON"))
	static TArray<FCloudSphere> GenerateFromJSON(const FString& JSONConfig);

	// Generate clouds from Blueprint layer configs
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Generate From Layers"))
	static TArray<FCloudSphere> GenerateFromLayers(const TArray<FCloudLayerConfig>& Layers);

	// Save layers to JSON config
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Layers To JSON"))
	static FString LayersToJSON(const TArray<FCloudLayerConfig>& Layers);

	// Load JSON config to layers
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "JSON To Layers"))
	static bool JSONToLayers(const FString& JSONConfig, TArray<FCloudLayerConfig>& OutLayers);

	// Get version string
	UFUNCTION(BlueprintPure, Category = "Cloud Generator")
	static FString GetGeneratorVersion() { return TEXT("6.1"); }
};