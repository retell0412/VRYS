using UnityEngine;

public class CreateSea : MonoBehaviour
{
    public Vector3 objectSize = new Vector3(1f, 1f, 1f); // 生成するオブジェクトのサイズ
    public PrimitiveType primitiveType = PrimitiveType.Cube; // 生成するプリミティブの種類
    public float objectHeight = 0f; // 生成するオブジェクトの高さ
    public FieldGenerator fieldGenerator; // 参照するFieldGenerator
    public GameObject prefabToInstantiate; // インスタンス化するPrefab

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (fieldGenerator == null)
        {
            fieldGenerator = FindObjectOfType<FieldGenerator>();
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    /// <summary>
    /// 指定したサイズでオブジェクトを生成
    /// </summary>
    public void CreateObject()
    {
        Vector3 spawnPosition = transform.position; // デフォルト位置

        // FieldGeneratorからサイズ、高さ、位置を取得して同期
        if (fieldGenerator != null)
        {
            float terrainWidth = fieldGenerator.width * fieldGenerator.scale;
            float terrainHeight = fieldGenerator.height * fieldGenerator.TriangleHeightDouble * fieldGenerator.scale;
            objectSize = new Vector3(terrainWidth * 0.5f, objectSize.y, terrainHeight * 0.5f);
                        
            // 地形の中心を基準に位置を設定
            Vector2 centerXZ = fieldGenerator.IndexToWorldXZ(fieldGenerator.width / 2, fieldGenerator.height / 2);
            spawnPosition = new Vector3(centerXZ.x, objectHeight, centerXZ.y);
        }

        GameObject obj;

        if (prefabToInstantiate != null)
        {
            // Prefabが設定されている場合はPrefabをインスタンス化
            obj = Instantiate(prefabToInstantiate);
            obj.transform.localScale = objectSize;
            obj.transform.position = new Vector3(transform.position.x, objectHeight, transform.position.z);
            obj.name = prefabToInstantiate.name + "_Generated";
        }
        else
        {
            // Prefabが設定されていない場合はプリミティブを生成
            obj = GameObject.CreatePrimitive(primitiveType);
            obj.transform.localScale = objectSize;
            obj.transform.position = new Vector3(transform.position.x, objectHeight, transform.position.z);
            obj.name = primitiveType.ToString() + "_Generated";
        }
        
        Debug.Log($"オブジェクトを生成しました: {obj.name}, サイズ: {objectSize}, 高さ: {objectHeight}");
    }
}
