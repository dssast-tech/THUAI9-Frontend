using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class MapData : MonoBehaviour
{
    [SerializeField, Min(1)] private int subdivisionLevel = 4;
    [SerializeField, Min(0f)] private float integerFlatHalfSize = 0.22f;
    [SerializeField, Min(0f)] private float integerBlendWidth = 0.28f;
    [SerializeField] private bool shareVertices = false;
    [SerializeField] private float heightScale = 0.4f;
    [SerializeField] private float borderHeightValue = -3f;
    [SerializeField] private Material terrainMaterial;

    private Transform mapParent;

    public void GenerateMap(MapDataField mapDataField)
    {
        if (mapParent == null)
        {
            mapParent = new GameObject("MapParent").transform;
        }

        ClearMap();

        int width = mapDataField.mapWidth;
        int height = mapDataField.rows.Length;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        GameObject terrainObject = new GameObject("TerrainMesh");
        terrainObject.transform.SetParent(mapParent, false);

        Mesh terrainMesh = BuildTerrainMesh(mapDataField, width, height);

        MeshFilter meshFilter = terrainObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = terrainMesh;

        MeshRenderer meshRenderer = terrainObject.AddComponent<MeshRenderer>();
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        meshRenderer.sharedMaterial = terrainMaterial != null ? terrainMaterial : new Material(shader)
        {
            color = new Color(0.7f, 0.75f, 0.65f)
        };

        MeshCollider meshCollider = terrainObject.AddComponent<MeshCollider>();
        meshCollider.sharedMesh = terrainMesh;
        
        // Center camera towards map center
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            mainCamera.transform.position = new Vector3(width / 2f, Mathf.Max(width, height), -height / 2f);
            mainCamera.transform.LookAt(new Vector3(width / 2f, 0, height / 2f));
            
            // Add or get CameraController
            if (mainCamera.gameObject.GetComponent<CameraController>() == null)
            {
                mainCamera.gameObject.AddComponent<CameraController>();
            }
        }
    }

    private Mesh BuildTerrainMesh(MapDataField mapDataField, int width, int height)
    {
        int subdivisionsPerTile = Mathf.Max(1, subdivisionLevel) * 2;
        float step = 1f / subdivisionsPerTile;
        int borderTiles = 1;
        int sampleWidth = (width + borderTiles * 2) * subdivisionsPerTile + 1;
        int sampleHeight = (height + borderTiles * 2) * subdivisionsPerTile + 1;
        float worldOffset = borderTiles + 0.5f;
        
        // First pass: collect all quad corner positions and heights
        Vector3[] samplePositions = new Vector3[sampleWidth * sampleHeight];
        for (int z = 0; z < sampleHeight; z++)
        {
            float worldZ = z * step - worldOffset;
            for (int x = 0; x < sampleWidth; x++)
            {
                float worldX = x * step - worldOffset;
                float y = SampleHeight(mapDataField, worldX, worldZ);
                int vertexIndex = z * sampleWidth + x;
                samplePositions[vertexIndex] = new Vector3(worldX, y, worldZ);
            }
        }

        Mesh mesh = new Mesh();
        if (shareVertices)
        {
            // Shared vertices mode: use indices to reference vertices
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int z = 0; z < sampleHeight - 1; z++)
            {
                for (int x = 0; x < sampleWidth - 1; x++)
                {
                    int bottomLeft = z * sampleWidth + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + sampleWidth;
                    int topRight = topLeft + 1;

                    // First triangle
                    triangles.Add(bottomLeft);
                    triangles.Add(topLeft);
                    triangles.Add(bottomRight);

                    // Second triangle
                    triangles.Add(bottomRight);
                    triangles.Add(topLeft);
                    triangles.Add(topRight);
                }
            }

            // Generate UV coordinates
            for (int z = 0; z < sampleHeight; z++)
            {
                for (int x = 0; x < sampleWidth; x++)
                {
                    uv.Add(new Vector2((float)x / (sampleWidth - 1), (float)z / (sampleHeight - 1)));
                }
            }

            if (samplePositions.Length > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.name = "TerrainMesh";
            mesh.vertices = samplePositions;
            mesh.triangles = triangles.ToArray();
            mesh.uv = uv.ToArray();
        }
        else
        {
            // Independent triangles mode: duplicate vertices for each triangle
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uv = new List<Vector2>();
            List<int> triangles = new List<int>();

            for (int z = 0; z < sampleHeight - 1; z++)
            {
                for (int x = 0; x < sampleWidth - 1; x++)
                {
                    int bottomLeft = z * sampleWidth + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = bottomLeft + sampleWidth;
                    int topRight = topLeft + 1;

                    Vector3 v0 = samplePositions[bottomLeft];
                    Vector3 v1 = samplePositions[topLeft];
                    Vector3 v2 = samplePositions[bottomRight];
                    Vector3 v3 = samplePositions[topRight];

                    // First triangle
                    int triStart0 = vertices.Count;
                    vertices.Add(v0);
                    vertices.Add(v1);
                    vertices.Add(v2);
                    uv.Add(new Vector2((float)x / (sampleWidth - 1), (float)z / (sampleHeight - 1)));
                    uv.Add(new Vector2((float)x / (sampleWidth - 1), (float)(z + 1) / (sampleHeight - 1)));
                    uv.Add(new Vector2((float)(x + 1) / (sampleWidth - 1), (float)z / (sampleHeight - 1)));
                    triangles.Add(triStart0);
                    triangles.Add(triStart0 + 1);
                    triangles.Add(triStart0 + 2);

                    // Second triangle
                    int triStart1 = vertices.Count;
                    vertices.Add(v2);
                    vertices.Add(v1);
                    vertices.Add(v3);
                    uv.Add(new Vector2((float)(x + 1) / (sampleWidth - 1), (float)z / (sampleHeight - 1)));
                    uv.Add(new Vector2((float)x / (sampleWidth - 1), (float)(z + 1) / (sampleHeight - 1)));
                    uv.Add(new Vector2((float)(x + 1) / (sampleWidth - 1), (float)(z + 1) / (sampleHeight - 1)));
                    triangles.Add(triStart1);
                    triangles.Add(triStart1 + 1);
                    triangles.Add(triStart1 + 2);
                }
            }

            if (vertices.Count > 65535)
            {
                mesh.indexFormat = IndexFormat.UInt32;
            }

            mesh.name = "TerrainMesh";
            mesh.vertices = vertices.ToArray();
            mesh.triangles = triangles.ToArray();
            mesh.uv = uv.ToArray();
        }
        
        // Calculate flat (unsmoothed) normals for independent triangles
        mesh.RecalculateNormals();
        
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    private float SampleHeight(MapDataField mapDataField, float x, float z)
    {
        return EvaluateFlatTopKernelInterpolation(mapDataField, x, z);
    }

    private float EvaluateFlatTopKernelInterpolation(MapDataField mapDataField, float x, float z)
    {
        int x0 = Mathf.FloorToInt(x);
        int z0 = Mathf.FloorToInt(z);
        int x1 = x0 + 1;
        int z1 = z0 + 1;

        float u = x - x0;
        float v = z - z0;

        float maxHalfSize = 0.5f - 0.0001f;
        float halfSize = Mathf.Clamp(integerFlatHalfSize, 0f, maxHalfSize);
        float maxBlendWidth = Mathf.Max(0f, 0.5f - halfSize);
        float blendWidth = Mathf.Clamp(integerBlendWidth, 0f, maxBlendWidth);

        float su = RemapAxisWithFlatEnds(u, halfSize, blendWidth);
        float sv = RemapAxisWithFlatEnds(v, halfSize, blendWidth);

        float h00 = GetHeight(mapDataField, x0, z0);
        float h10 = GetHeight(mapDataField, x1, z0);
        float h01 = GetHeight(mapDataField, x0, z1);
        float h11 = GetHeight(mapDataField, x1, z1);

        float hx0 = Mathf.Lerp(h00, h10, su);
        float hx1 = Mathf.Lerp(h01, h11, su);
        return Mathf.Lerp(hx0, hx1, sv);
    }

    private float RemapAxisWithFlatEnds(float t, float halfSize, float blendWidth)
    {
        t = Mathf.Clamp01(t);

        if (halfSize <= Mathf.Epsilon)
        {
            return t;
        }

        float leftFlatEnd = halfSize;
        float rightFlatStart = 1f - halfSize;
        if (t <= leftFlatEnd)
        {
            return 0f;
        }

        if (t >= rightFlatStart)
        {
            return 1f;
        }

        if (blendWidth <= Mathf.Epsilon)
        {
            float innerT = (t - leftFlatEnd) / (rightFlatStart - leftFlatEnd);
            return Mathf.Clamp01(innerT);
        }

        float leftBlendEnd = leftFlatEnd + blendWidth;
        float rightBlendStart = rightFlatStart - blendWidth;

        if (leftBlendEnd >= rightBlendStart)
        {
            float overlapT = (t - leftFlatEnd) / (rightFlatStart - leftFlatEnd);
            overlapT = Mathf.Clamp01(overlapT);
            return overlapT * overlapT * overlapT * (overlapT * (overlapT * 6f - 15f) + 10f);
        }

        if (t < leftBlendEnd)
        {
            float tl = (t - leftFlatEnd) / blendWidth;
            tl = Mathf.Clamp01(tl);
            float sl = tl * tl * tl * (tl * (tl * 6f - 15f) + 10f);
            float sAtLeftBlendEnd = (leftBlendEnd - leftFlatEnd) / (rightFlatStart - leftFlatEnd);
            return sl * sAtLeftBlendEnd;
        }

        if (t > rightBlendStart)
        {
            float tr = (rightFlatStart - t) / blendWidth;
            tr = Mathf.Clamp01(tr);
            float sr = tr * tr * tr * (tr * (tr * 6f - 15f) + 10f);
            float sAtRightBlendStart = (rightBlendStart - leftFlatEnd) / (rightFlatStart - leftFlatEnd);
            return 1f - sr * (1f - sAtRightBlendStart);
        }

        float tm = (t - leftFlatEnd) / (rightFlatStart - leftFlatEnd);
        return Mathf.Clamp01(tm);
    }

    private float GetHeight(MapDataField mapDataField, int x, int z)
    {
        if (z < 0 || z >= mapDataField.rows.Length)
        {
            return borderHeightValue * heightScale;
        }

        int[] row = mapDataField.rows[z].row;
        if (row == null || row.Length == 0)
        {
            return 0f;
        }

        if (x < 0 || x >= row.Length)
        {
            return borderHeightValue * heightScale;
        }

        return row[x] * heightScale;
    }
    
    public void ClearMap()
    {
        if (mapParent != null)
        {
            foreach (Transform child in mapParent)
            {
                Destroy(child.gameObject);
            }
        }
    }
}
