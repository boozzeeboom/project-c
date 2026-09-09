# T-FO03 — generated migration census

Date: 2026-09-09.

**Status: CANDIDATES ONLY — NOT semantic coverage, migration readiness, runtime testing, or jitter acceptance.**

```json
{
    "sourceFiles": 639,
    "shaderFiles": 48,
    "sourceCandidates": 2033,
    "prefabsScanned": 75,
    "spatialPrefabObjects": 1391,
    "loadedScenes": 1,
    "loadedSpatialObjects": 197,
    "errors": [],
    "reports": [
        "docs/world/floatingorigin/03_SOURCE_CANDIDATES.csv",
        "docs/world/floatingorigin/03_PREFAB_CANDIDATES.csv",
        "docs/world/floatingorigin/03_OPEN_SCENE_CANDIDATES.csv",
        "docs/world/floatingorigin/03_CENSUS_SUMMARY.md"
    ]
}
```

## Candidate categories

| Category | Matches |
|---|---:|
|legacy-origin|157|
|lifecycle-placement|121|
|navigation|46|
|network-serialization|531|
|network-variable|24|
|physics-query|31|
|physics-scene|1|
|point-conversion|23|
|position-access|678|
|shader-position-candidate|96|
|spatial-member|283|
|spatial-rpc-signature|16|
|world-space-effects|26|

## Scope and limitations

- Sources: every imported .cs/.shader/.hlsl/.cginc under Assets, including third-party, Editor, inactive legacy and foundation code. Packages are not part of this source scan.
- Matching is lexical, includes comments/strings, can duplicate a line under different categories, and is NOT a semantic compiler or call graph. A zero match never proves safety.
- Prefabs: all .prefab under Assets/_Project via AssetDatabase; nested/inactive objects included. Component records are asset configurations, not runtime authority or lifecycle evidence.
- Scenes: already loaded scenes only; no scene is opened or saved. Closed WorldScenes, build-only content, Addressables, runtime-created and pooled objects require a later explicit coverage pass.
- Mesh column is the largest absolute component of local mesh BOUNDS on that object, not a vertex scan, not world bounds and not a measured defect. Empty means no mesh inspected on that object.
- No object/scene/prefab/meta/package is modified. Reports overwrite only the four named plain-text census files.
- Every spatial RPC/DTO, persisted target, cache, shader and physics query must still be classified global / frame-local / parent-local / nav-local / direction, with sender/receiver and lifecycle evidence.
