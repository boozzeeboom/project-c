// CloudGeneratorBPLibrary.h
// Blueprint Function Library for Cloud Generation
#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "CloudGeneratorBPLibrary.generated.h"

// Forward declaration
struct FCloudSphereBP;
struct FCloudLayerConfigBP;

UCLASS()
class CLOUDGENERATOR_API UCloudGeneratorBPLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:
	// Generate from JSON string
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Generate From JSON"))
	static TArray<FCloudSphereBP> GenerateFromJSON(const FString& JSONString);

	// Load and generate from JSON file
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Generate From File"))
	static TArray<FCloudSphereBP> GenerateFromFile(const FString& FilePath);

	// Generate from layer config struct
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Generate From Config"))
	static TArray<FCloudSphereBP> GenerateFromConfig(const FCloudLayerConfigBP& Config);

	// Convert layers to JSON
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "Layers To JSON"))
	static FString LayersToJSON(const TArray<FCloudLayerConfigBP>& Layers);

	// Parse JSON to layers
	UFUNCTION(BlueprintCallable, Category = "Cloud Generator", meta = (DisplayName = "JSON To Layers"))
	static bool JSONToLayers(const FString& JSONString, TArray<FCloudLayerConfigBP>& OutLayers);
};