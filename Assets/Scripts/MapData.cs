using System.Collections.Generic;
using UnityEngine;

public class MapData : MonoBehaviour
{
    private GameObject[,] mapTiles;
    private Transform mapParent;
    private Bounds mapBounds;
    private bool hasMapBounds;

    public void GenerateMap(MapDataField mapDataField)
    {
        if (mapParent == null)
        {
            mapParent = new GameObject("MapParent").transform;
        }

        int width = mapDataField.mapWidth;
        int height = mapDataField.rows.Length;
        mapTiles = new GameObject[height, width];
        
        hasMapBounds = false;

        for (int z = 0; z < height; z++)
        {
            for (int x = 0; x < mapDataField.rows[z].row.Length; x++)
            {
                int y = mapDataField.rows[z].row[x];
                GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cube.transform.SetParent(mapParent);
                
                float yScale = Mathf.Max(y, 0.1f);
                cube.transform.localScale = new Vector3(1, yScale, 1);
                cube.transform.position = new Vector3(x, yScale / 2f, z);
                
                Renderer r = cube.GetComponent<Renderer>();
                if (y == 0) r.material.color = new Color(0.8f, 0.8f, 0.8f);
                else if (y == 1) r.material.color = new Color(0.5f, 0.8f, 0.5f);
                else if (y == 2) r.material.color = new Color(0.8f, 0.8f, 0.5f);
                else r.material.color = new Color(0.8f, 0.5f, 0.5f);
                
                cube.name = $"Tile_z{z}_x{x}";
                mapTiles[z, x] = cube;

                Renderer cubeRenderer = cube.GetComponent<Renderer>();
                if (cubeRenderer != null)
                {
                    if (!hasMapBounds)
                    {
                        mapBounds = cubeRenderer.bounds;
                        hasMapBounds = true;
                    }
                    else
                    {
                        mapBounds.Encapsulate(cubeRenderer.bounds);
                    }
                }
            }
        }
        
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
    
    public void ClearMap()
    {
        if (mapParent != null)
        {
            foreach (Transform child in mapParent)
            {
                Destroy(child.gameObject);
            }
        }

        hasMapBounds = false;
        mapBounds = new Bounds(Vector3.zero, Vector3.zero);
    }

    public bool TryGetMapBounds(out Bounds bounds)
    {
        bounds = mapBounds;
        return hasMapBounds;
    }
}
