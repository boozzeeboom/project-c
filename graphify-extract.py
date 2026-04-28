from graphify.extract import extract
from pathlib import Path
import json

files = [
    'Assets/_Project/Scripts/World/Streaming/WorldStreamingManager.cs',
    'Assets/_Project/Scripts/World/Streaming/ChunkLoader.cs',
    'Assets/_Project/Scripts/World/Streaming/WorldChunkManager.cs',
    'Assets/_Project/Scripts/World/Streaming/PlayerChunkTracker.cs',
    'Assets/_Project/Scripts/World/Scene/SceneTransitionCoordinator.cs',
    'Assets/_Project/Scripts/World/Scene/ClientSceneLoader.cs',
    'Assets/_Project/Scripts/World/Scene/ServerSceneManager.cs',
    'Assets/_Project/Scripts/Core/StreamingSetupRuntime.cs',
    'Assets/_Project/Scripts/Player/PlayerController.cs',
    'Assets/_Project/Scripts/Player/ShipController.cs',
    'Assets/_Project/Scripts/Player/NetworkPlayer.cs',
    'Assets/_Project/Scripts/Player/PlayerStateMachine.cs',
    'Assets/_Project/Editor/WorldSceneGenerator.cs',
]

code_files = [Path(f) for f in files]
result = extract(code_files)
open('graphify-out/.graphify_ast.json', 'w').write(json.dumps(result))
print(f'AST: {len(result["nodes"])} nodes, {len(result["edges"])} edges')