// Copyright 2026 ProjectC. All rights reserved.

#pragma once

#include "Modules/ModuleManager.h"

class FCloudGeneratorEditorModule : public IModuleInterface
{
public:
	/** IModuleInterface implementation */
	virtual void StartupModule() override;
	virtual void ShutdownModule() override;

private:
	void RegisterMenu();
	void UnregisterMenu();
};

#define LOCTEXT_NAMESPACE "CloudGeneratorEditor"