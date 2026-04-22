using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;
using Unity.Mathematics;
using Random = UnityEngine.Random;
using System.Linq;

/// <summary>
/// 手続き的生成によって、PerlinNoiseを使用したテラスのような地形メッシュを自動生成するクラス
/// </summary>
public class FieldGenerator : MonoBehaviour
{
    // メッシュサイズ設定 
    public int width = 60;           // X軸方向の分割数
    public int height = 40;          // Z軸方向の分割数
    public float scale = 5.0f;       // メッシュの1単位あたりのスケール

    // 六角形グリッド用の定数
    const float TriangleHeight = 0.86660254f;        // 六角形の高さ（√3/2）
    public float TriangleHeightDouble = 1.7320508f;   // 六角形の高さの2倍（√3）

    //  第1波形（大規模な起伏）
    public float wave1 = 0.3f;       // PerlinNoiseのスケール（小さいほど粗い）
    public float peak1 = 8f;         // 第1波形の高さ

    //第2波形（中規模な起伏）
    public float wave2 = 0.006f;     // PerlinNoiseのスケール
    public float peak2 = 165f;       // 第2波形の高さ

    // 第3波形（細かい起伏）
    public float wave3 = 0.002f;     // PerlinNoiseのスケール
    public float peak3 = 110f;       // 第3波形の高さ

    Mesh mesh;
    public float[,] heightMap;  // 修正：2次元配列（[,]）で定義

    // PerlinNoise生成用のシード値
    public float _seedX = 0.0f;      // X軸方向のランダムオフセット
    public float _seedZ = 0.0f;      // Z軸方向のランダムオフセット

    public float zeroBlendFactor = 17f; // シグモイド関数のブレンド係数
    public float sigmoidZeroHeight = 40f;  // シグモイド関数のブレンド係数

    public string saveAsAnAssetInPath = "Assets/FieldMesh.asset";

    /// <summary>
    /// Inspectorのコンテキストメニューから呼び出し可能な地形生成メソッド
    /// 六角形グリッドベースのメッシュを生成します
    /// </summary>
    public void OnPressedCreateButton()
    {
        makeGround();
        Debug.Log("地形を生成しました");

        // 生成完了を GameManager に通知（存在すれば Spawn を開始）
        var gameManagers = FindObjectsByType<GameManager>(FindObjectsSortMode.None);
        foreach (var gameManager in gameManagers)
        {
            gameManager.OnFieldGenerated();
        }
    }

    // [ContextMenu("生成")]
    private void makeGround()
    {
        // ランダムシードを先に決定して、heightMap とメッシュで同じシードを使う
        _seedX = Random.value * 1000f;
        _seedZ = Random.value * 1000f;

        mesh = new Mesh();
        int p;
        mesh.Clear();

        // === 高さマップを作成 ===
        // 基準グリッド： x = 0..width, zIndex = 0..(height+1)
        heightMap = new float[width + 1, height + 2];
        for (int zi = 0; zi <= height + 1; zi++)
        {
            float zPos = zi * TriangleHeightDouble * scale;
            for (int xj = 0; xj <= width; xj++)
            {
                float xPos = xj * scale;
                heightMap[xj, zi] = groundHeight(xPos, zPos);
            }
        }

        // === メッシュデータの初期化 ===
        var vertices = new Vector3[((width + 1) * 2 + 1) * (height + 1) + width + 1];
        var uv = new Vector2[((width + 1) * 2 + 1) * (height + 1) + width + 1];
        var triangles = new int[(width * 2 + 1) * (height * 2 + 2) * 3];

        // === 初段（最初のY=0行）の頂点生成 ===
        for (p = 0; p <= width; p++)
        {
            vertices[p].x = p * scale;
            vertices[p].z = 0f;
            // heightMap の zIndex = 0 を利用
            vertices[p].y = heightMap[p, 0];

            // UV座標を0～1の範囲で正規化
            uv[p].x = (float)p / width;
            uv[p].y = 0f;
        }

        // === 次段以降の頂点生成（高さ方向のループ） ===
        for (int i = 0; i <= height; i++)
        {
            // --- 各行の左端頂点 ---
            vertices[p].x = 0f;
            vertices[p].z = i * TriangleHeightDouble * scale + TriangleHeight * scale;
            // オフセット行のため heightMap に対応していないので直接計算
            vertices[p].y = groundHeight(vertices[p].x, vertices[p].z);
            // ごり押し
            
            uv[p].x = 0f;
            uv[p].y = (i * TriangleHeightDouble + TriangleHeight) / (TriangleHeightDouble * height);
            p++;

            // --- 各行の中央頂点（六角形グリッドの特性） ---
            for (int j = 0; j <= width - 1; j++)
            {
                // X座標をオフセット（0.5*scale）させることで六角形パターンを形成
                vertices[p].x = j * scale + 0.5f * scale;
                vertices[p].z = i * TriangleHeightDouble * scale + TriangleHeight * scale;
                // オフセット頂点は基準グリッドと異なるため直接計算
                vertices[p].y = groundHeight(vertices[p].x, vertices[p].z);
                uv[p].x = ((float)j + 0.5f) / width;
                uv[p].y = (i * TriangleHeightDouble + TriangleHeight) / (TriangleHeightDouble * height);
                p++;
            }

            // --- 各行の右端頂点 ---
            vertices[p].x = width * scale;
            vertices[p].z = i * TriangleHeightDouble * scale + TriangleHeight * scale;
            vertices[p].y = groundHeight(vertices[p].x, vertices[p].z);
            uv[p].x = 1f;
            uv[p].y = (i * TriangleHeightDouble + TriangleHeight) / (TriangleHeightDouble * height);
            p++;

            // --- 次の行の下部頂点（ペア三角形用） ---
            // この行では4つの三角形がセットで定義される
            // ここは基準グリッド（zIndex = i+1）に対応しているため heightMap を利用する
            for (int j = 0; j <= width; j++)
            {
                vertices[p].x = j * scale;
                vertices[p].z = (i + 1f) * TriangleHeightDouble * scale;
                vertices[p].y = heightMap[j, i + 1]; // heightMap から読み出し
                uv[p].x = (float)j / width;
                uv[p].y = (i + 1f) / height;
                p++;
            }
        }

        // === ポリゴン順序の定義（三角形インデックス） ===
        p = 0;
        for (int i = 0; i <= height; i++)
        {
            for (int j = 0; j < width; j++)
            {
                // 1ループで4つの三角形（12個のインデックス）を定義
                
                // 三角形1
                triangles[p + 0] = j + (((width + 1) * 2 + 1) * i);
                triangles[p + 1] = j + (width + 1) * (i * 2 + 1) + i;
                triangles[p + 2] = j + (width + 1) * (i * 2 + 1) + i + 1;

                // 三角形2
                triangles[p + 3] = j + (((width + 1) * 2 + 1) * i);
                triangles[p + 4] = j + (width + 1) * (i * 2 + 1) + i + 1;
                triangles[p + 5] = j + (((width + 1) * 2 + 1) * i) + 1;

                // 三角形3
                triangles[p + 6] = j + (width + 1) * (i * 2 + 1) + i;
                triangles[p + 7] = j + (((width + 1) * 2 + 1) * (i + 1));
                triangles[p + 8] = j + (width + 1) * (i * 2 + 1) + i + 1;

                // 三角形4
                triangles[p + 9] = j + (width + 1) * (i * 2 + 1) + i + 1;
                triangles[p + 10] = j + (((width + 1) * 2 + 1) * (i + 1));
                triangles[p + 11] = j + (((width + 1) * 2 + 1) * (i + 1)) + 1;

                p += 12;
            }

            // --- 右端と左端の特別処理（エッジケース） ---
            triangles[p + 0] = width + (((width + 1) * 2 + 1) * i);
            triangles[p + 1] = width + (width + 1) * (i * 2 + 1) + i;
            triangles[p + 2] = width + (width + 1) * (i * 2 + 1) + i + 1;

            triangles[p + 3] = width + (width + 1) * (i * 2 + 1) + i;
            triangles[p + 4] = width + (((width + 1) * 2 + 1) * (i + 1));
            triangles[p + 5] = width + (width + 1) * (i * 2 + 1) + i + 1;

            p += 6;
        }

        // === メッシュに頂点情報を適用 ===
        // 頂点数が 65535 を超える場合は 32bit インデックスを使用する
        if (vertices.Length > 65535)
        {
            mesh.indexFormat = IndexFormat.UInt32;
        }

        var maxX = vertices.Max(v => v.x);
        var maxZ = vertices.Max(v => v.z);
        Debug.Log($"メッシュ情報: 頂点数 {vertices.Length}, ポリゴン数 {triangles.Length / 3}, 最大X {maxX}, 最大Z {maxZ}");

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.uv = uv;

        // === 法線と接線を自動計算（ライティング用） ===
        mesh.RecalculateNormals();
        var filter = GetComponent<MeshFilter>();
        filter.sharedMesh = mesh;
    }

    /// <summary>
    /// 指定座標の地形の高さを計算
    /// 3層のPerlinNoiseを合成して、複雑な地形を生成
    /// </summary>
    float groundHeight(float x, float z)
    {
        float y;
        
        // 複数のPerlinNoiseを組み合わせて、複雑な地形を作成
        y = Mathf.PerlinNoise((x + _seedX) * wave1, (z + _seedZ) * wave1) * peak1
           + Mathf.PerlinNoise((x + _seedX) * wave2, (z + _seedZ) * wave2) * peak2;

        // === 上側のテラス化（高さが高すぎる場所を平坦化） ===
        if (y > (peak2 * 0.55f))
        {
            if (y < (peak2 * 0.65f))
            {
                y = peak2 * 0.55f;  // 段差を作成
            }
            else
            {
                y -= peak2 * 0.1f;  // 徐々に低くする
            }
        }

        // === 下側のテラス化（高さが低すぎる場所を平坦化） ===
        if (y > (peak2 * 0.3f))
        {
            if (y < (peak2 * 0.4f))
            {
                y = peak2 * 0.3f;   // 段差を作成
            }
            else
            {
                y -= peak2 * 0.1f;  // 徐々に低くする
            }
        }

        // === 第3波形を加算（微細な起伏を追加） ===
        y += Mathf.PerlinNoise((x + _seedX) * wave3, (z + _seedZ) * wave3) * peak3;

        // シグモイドを適用
        var maxX = width * scale;
        var maxZ = height * TriangleHeightDouble * scale + TriangleHeight * scale;
        var normX = math.saturate(x / maxX);
        var normZ = math.saturate(z / maxZ);
        
        var blend = Sigmoid(normX) * Sigmoid(1 - normX) * Sigmoid(normZ) * Sigmoid(1 - normZ);
        y *= blend;
        y += (1f - blend) * sigmoidZeroHeight;

        // 注意：groundHeight 内で heightMap に書き込まない（座標系が world float のため）
        return y;

        float Sigmoid(float v)
        {
            return (0.5f - 1f / (1f + math.exp(zeroBlendFactor * v))) * 2f;
        }
    }

    /// <summary>
    /// 生成したメッシュを保存
    /// </summary>
    //[ContextMenu("メッシュの保存")]
    void SaveMesh()
    {
        if (saveAsAnAssetInPath != "")
        {
            AssetDatabase.CreateAsset(mesh, saveAsAnAssetInPath);
            AssetDatabase.SaveAssets();
        }
    }

    public void OnPressedRemoveButton()
    {
        // メッシュジェネレーターに削除処理を委譲
        DestroyImmediate(this.gameObject);  
        Debug.Log("オブジェクトを削除しました");
    }

    /// <summary>
    /// インデックス(ix:0..width, iz:0..height+1) に対する高さを返す。
    /// heightMap があればそれを使い、無ければ groundHeight を計算して返す。
    /// </summary>
    public float GetHeightAtIndex(int ix, int iz)
    {
        ix = Mathf.Clamp(ix, 0, width);
        iz = Mathf.Clamp(iz, 0, height + 1);
        if (heightMap != null)
        {
            return heightMap[ix, iz];
        }
        // heightMap 未生成ならワールド座標に変換して計算
        float worldX = ix * scale;
        float worldZ = iz * TriangleHeightDouble * scale;
        return groundHeight(worldX, worldZ);
    }

    /// <summary>
    /// インデックスからワールドXZを返す（Yは GetHeightAtIndex で取得）
    /// </summary>
    public Vector2 IndexToWorldXZ(int ix, int iz)
    {
        float x = ix * scale;
        float z = iz * TriangleHeightDouble * scale;
        return new Vector2(x, z);
    }
}
