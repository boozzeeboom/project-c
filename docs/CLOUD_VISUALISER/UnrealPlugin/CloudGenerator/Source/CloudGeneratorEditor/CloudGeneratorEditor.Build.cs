// CloudGeneratorEditor.Build.cs
using UnrealBuildTool;

public class CloudGeneratorEditor : ModuleRules
{
	public CloudGeneratorEditor(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;
		
		PublicIncludePaths.AddRange(
			new string[] {
				"CloudGeneratorEditor/Public"
			}
		);
		
		PrivateIncludePaths.AddRange(
			new string[] {
				"CloudGeneratorEditor/Private"
			}
		);
		
		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"CoreUObject",
				"Engine",
				"EditorStyle",
				"CloudGenerator"
			}
		);
	}
}