using UnityEngine;

public class DebugQuadSetup : MonoBehaviour
{
    public Material DebugMaterial;
    
    void Start()
    {
        // Get DistantCloudManager data
        var managers = Object.FindObjectsOfType<ProjectC.Core.DistantCloudManager>();
        Material useMat = DebugMaterial;
        Texture2D useTex = null;
        
        // Always try to get from manager first
        if (managers.Length > 0)
        {
            var mgr = managers[0];
            useMat = mgr.ImpostorMaterial;
            if (mgr.Textures != null && mgr.Textures.Length > 0)
                useTex = mgr.Textures[0];
            Debug.Log($"[DebugQuad] Using material: {useMat?.name}, texture: {useTex?.name}");
        }
        else
        {
            Debug.LogWarning("[DebugQuad] No DistantCloudManager found, using DebugMaterial");
        }
        
        // Create quad mesh
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[] {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3(0.5f, -0.5f, 0),
            new Vector3(0.5f, 0.5f, 0),
            new Vector3(-0.5f, 0.5f, 0)
        };
        mesh.uv = new Vector2[] {
            new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)
        };
        mesh.triangles = new int[] { 0, 2, 1, 0, 3, 2 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        var mf = GetComponent<MeshFilter>();
        mf.mesh = mesh;
        
        var mr = GetComponent<MeshRenderer>();
        
        if (useMat != null)
        {
            var matCopy = new Material(useMat);
            if (useTex != null)
                matCopy.mainTexture = useTex;
            mr.material = matCopy;
            Debug.Log($"[DebugQuad] Material set: {matCopy.name}, shader: {matCopy.shader.name}, queue: {matCopy.renderQueue}");
        }
        else
        {
            Debug.LogError("[DebugQuad] No material available!");
        }
    }
}
