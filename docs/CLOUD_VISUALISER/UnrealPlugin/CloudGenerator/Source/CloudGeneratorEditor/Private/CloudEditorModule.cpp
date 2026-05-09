// Copyright 2026 ProjectC. All rights reserved.

#include "CloudEditorStyle.h"
#include "Modules/ModuleManager.h"
#include "LevelEditor.h"
#include "IContentBrowserModule.h"
#include "WorkspaceMenuStructure.h"

#define LOCTEXT_NAMESPACE "CloudGeneratorEditor"

void FCloudGeneratorEditorModule::StartupModule()
{
	RegisterMenu();
}

void FCloudGeneratorEditorModule::ShutdownModule()
{
	UnregisterMenu();
}

void FCloudGeneratorEditorModule::RegisterMenu()
{
	FLevelEditorModule& LevelEditorModule = FModuleManager::LoadModuleChecked<FLevelEditorModule>("LevelEditor");

	// Add to Window menu
	MenuCategory = LevelEditorModule.AddMenuCategory(TEXT("CloudGenerator"), LOCTEXT("CloudGenerator", "Cloud Generator"));

	// Note: For full editor integration, you would extend this with:
	// - SWindow with SCbx for visual layer editing
	// - MenuEntry in Window > Developer Tools
	// - Or AssetTools action for Blueprint import
}

void FCloudGeneratorEditorModule::UnregisterMenu()
{
	MenuCategory.Reset();
}

#undef LOCTEXT_NAMESPACE

IMPLEMENT_MODULE(FCloudGeneratorEditorModule, CloudGeneratorEditor)