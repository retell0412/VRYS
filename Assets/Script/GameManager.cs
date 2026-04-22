using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public GameObject[] Prefabs;
    public FieldGenerator fieldGenerator;

    [Header("生成数設定")]
    public int SpawnLimit = 10;
    public float spawnDensity = 0f;

    [Header("高さ制限")]
    public float minHeightThreshold = 0f;
    public float maxHeightThreshold = float.MaxValue;

    [Header("傾斜制限")]
    [Range(0f, 90f)]
    public float maxSlopeAngle = 45f; // 生成可能な最大傾斜角（度）

    public int ObjectHeight = 1;
    private List<GameObject> spawnedInstances = new List<GameObject>();

    void Start()
    {
        if (fieldGenerator == null) fieldGenerator = FindObjectOfType<FieldGenerator>();
    }

    public void Spawn()
    {
        if (fieldGenerator == null || fieldGenerator.heightMap == null) return;

        ClearSpawnedInstances();

        int targetSpawnCount = CalculateSpawnCount();
        int currentSpawned = 0;
        int maxAttempts = targetSpawnCount * 10; // 無限ループ防止用の最大試行回数
        int attempts = 0;

        Debug.Log($"生成開始: 目標数 {targetSpawnCount}", gameObject);

        while (currentSpawned < targetSpawnCount && attempts < maxAttempts)
        {
            attempts++;

            // 1. ランダムなインデックスを決定
            int ix = Random.Range(0, fieldGenerator.width + 1);
            int iz = Random.Range(0, fieldGenerator.height + 1);

            // 2. 高さを取得してチェック
            float y = fieldGenerator.GetHeightAtIndex(ix, iz);
            if (y < minHeightThreshold || y > maxHeightThreshold) continue;

            // 3. 傾斜をチェック
            if (IsTooSteep(ix, iz)) continue;

            // 4. 生成処理
            Vector2 wxz = fieldGenerator.IndexToWorldXZ(ix, iz);
            if (Prefabs == null || Prefabs.Length == 0) break;

            int prefabIndex = Random.Range(0, Prefabs.Length);
            Vector3 localPos = new Vector3(wxz.x, y + 0.1f, wxz.y);
            Vector3 worldPos = fieldGenerator.transform.TransformPoint(localPos);

            GameObject inst = Instantiate(Prefabs[prefabIndex], worldPos, Quaternion.identity, this.transform);
            spawnedInstances.Add(inst);
            
            currentSpawned++;
        }

        Debug.Log($"生成完了: {currentSpawned}個 (試行回数: {attempts})");
    }

    // 生成すべき総数を計算
    private int CalculateSpawnCount()
    {
        if (spawnDensity <= 0f) return SpawnLimit;

        float terrainWidth = fieldGenerator.width * fieldGenerator.scale;
        float terrainHeight = fieldGenerator.height * fieldGenerator.TriangleHeightDouble * fieldGenerator.scale;
        float area = terrainWidth * terrainHeight;
        float blockArea = 10f * 10f; 
        return Mathf.RoundToInt((area / blockArea) * spawnDensity);
    }

    // 傾斜が急すぎるかどうかを判定
    private bool IsTooSteep(int x, int z)
    {
        // 隣接する点との高さの差から法線を簡易計算（近似値）
        // fieldGeneratorにGetNormal(x, z)のような関数があればそれを使うのがベストです
        float hL = fieldGenerator.GetHeightAtIndex(Mathf.Max(0, x - 1), z);
        float hR = fieldGenerator.GetHeightAtIndex(Mathf.Min(fieldGenerator.width, x + 1), z);
        float hD = fieldGenerator.GetHeightAtIndex(x, Mathf.Max(0, z - 1));
        float hU = fieldGenerator.GetHeightAtIndex(x, Mathf.Min(fieldGenerator.height, z + 1));

        // 法線ベクトルを算出
        Vector3 normal = new Vector3(hL - hR, 2.0f, hD - hU).normalized;

        // 上方向(Vector3.up)との角度を計算
        float angle = Vector3.Angle(Vector3.up, normal);

        return angle > maxSlopeAngle;
    }

    public void OnFieldGenerated()
    {
        ClearSpawnedInstances();
        Spawn();
    }

    void ClearSpawnedInstances()
    {
        if (spawnedInstances == null) spawnedInstances = new List<GameObject>();
        foreach (var inst in spawnedInstances)
        {
            if (inst != null) Destroy(inst);
        }
        spawnedInstances.Clear();
    }
}