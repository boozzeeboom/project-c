using UnrealBuildTool;

public class CloudGenerator : ModuleRules
{
	public CloudGenerator(ReadOnlyTargetRules Target) : base(Target)
	{
		PCHUsage = ModuleRules.PCHUsageMode.UseExplicitOrSharedPCHs;

		PublicIncludePaths.AddRange(
			new string[] {
				"CloudGenerator/Public"
			}
		);

		PrivateIncludePaths.AddRange(
			new string[] {
				"CloudGenerator/Private"
			}
		);

		PublicDependencyModuleNames.AddRange(
			new string[]
			{
				"Core",
				"CoreUObject",
				"Engine",
				"Json",
				"JsonUtilities"
			}
		);
	}
}