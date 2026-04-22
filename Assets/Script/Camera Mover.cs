using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// カメラの移動と回転を管理するクラス
/// 
/// 操作方法：
/// - 左ドラッグ：カメラの回転
/// - 右ドラッグ：カメラの移動（ワールド座標）
/// - WASD：カメラの前後左右移動
/// - QE：カメラの上下移動
/// - P：カメラの回転をリセット
/// - スペース：カメラ操作の有効・無効切り替え
/// </summary>
public class CameraMover : MonoBehaviour
{
    // === カメラ操作の速度設定 ===
    [SerializeField, Range(1f, 30.0f)]
    private float _positionStep = 20.0f;      // カメラ移動速度

    [SerializeField, Range(30.0f, 150.0f)]
    private float _mouseSensitive = 90.0f;    // マウス回転感度

    // === カメラの状態管理 ===
    private bool _cameraMoveActive = true;               // カメラ操作が有効かどうか
    private Transform _camTransform;                     // カメラのTransform
    private Vector3 _startMousePos;                      // マウス操作開始位置
    private Vector3 _presentCamRotation;                 // 現在のカメラ回転角度
    private Vector3 _presentCamPos;                      // 現在のカメラ位置
    private Quaternion _initialCamRotation;              // 初期カメラ回転角度（リセット用）
    private bool _uiMessageActive;                       // UIメッセージ表示中かどうか

    // === Input System の参照 ===
    private Mouse _mouse;                   // マウス入力
    private Keyboard _keyboard;             // キーボード入力

    /// <summary>
    /// 初期化処理
    /// カメラのTransformと入力デバイスを設定
    /// </summary>
    void Start()
    {
        _camTransform = this.gameObject.transform;
        _initialCamRotation = this.gameObject.transform.rotation;
        _mouse = Mouse.current;
        _keyboard = Keyboard.current;
    }

    /// <summary>
    /// 毎フレーム更新処理
    /// 入力を取得して各種カメラ操作を実行
    /// </summary>
    void Update()
    {
        // Input Systemが初期化されていない場合はスキップ
        if (_mouse == null || _keyboard == null) return;

        // カメラ操作の有効・無効をチェック
        CamControlIsActive();

        // カメラ操作が有効な場合、各種制御を実行
        if (_cameraMoveActive)
        {
            ResetCameraRotation();
            CameraRotationMouseControl();
            CameraSlideMouseControl();
            CameraPositionKeyControl();
        }
    }

    /// <summary>
    /// スペースキーでカメラ操作の有効・無効を切り替え
    /// </summary>
    public void CamControlIsActive()
    {
        if (_keyboard.spaceKey.wasPressedThisFrame)
        {
            // カメラ操作フラグをトグル
            _cameraMoveActive = !_cameraMoveActive;

            // UIメッセージを表示中でない場合は表示
            if (_uiMessageActive == false)
            {
                StartCoroutine(DisplayUiMessage());
            }
            Debug.Log("CamControl : " + _cameraMoveActive);
        }
    }

    /// <summary>
    /// Pキーでカメラの回転を初期状態にリセット
    /// </summary>
    private void ResetCameraRotation()
    {
        if (_keyboard.pKey.wasPressedThisFrame)
        {
            this.gameObject.transform.rotation = _initialCamRotation;
            Debug.Log("Cam Rotate : " + _initialCamRotation.ToString());
        }
    }

    /// <summary>
    /// 左マウスドラッグでカメラを回転
    /// マウスの移動量に応じてカメラのX軸（上下）とY軸（左右）を回転
    /// </summary>
    private void CameraRotationMouseControl()
    {
        // 左マウスボタンが押された時、ドラッグ開始位置と初期回転を記録
        if (_mouse.leftButton.wasPressedThisFrame)
        {
            _startMousePos = _mouse.position.ReadValue();
            _presentCamRotation.x = _camTransform.eulerAngles.x;
            _presentCamRotation.y = _camTransform.eulerAngles.y;
        }

        // 左マウスボタンが押されている間、マウス移動に応じてカメラを回転
        if (_mouse.leftButton.isPressed)
        {
            // 現在のマウス位置を取得
            Vector2 currentMousePos = _mouse.position.ReadValue();

            // マウス移動量をスクリーン座標の比率に正規化
            float x = (_startMousePos.x - currentMousePos.x) / Screen.width;
            float y = (_startMousePos.y - currentMousePos.y) / Screen.height;

            // オイラー角を計算（Y軸右方向、X軸上下方向）
            float eulerX = _presentCamRotation.x + y * _mouseSensitive;
            float eulerY = _presentCamRotation.y + x * _mouseSensitive;

            // カメラを新しい角度に回転
            _camTransform.rotation = Quaternion.Euler(eulerX, eulerY, 0);
        }
    }

    /// <summary>
    /// 右マウスドラッグでカメラを移動
    /// マウスの移動量に応じてカメラのワールド座標を変更
    /// </summary>
    private void CameraSlideMouseControl()
    {
        // 右マウスボタンが押された時、ドラッグ開始位置と初期位置を記録
        if (_mouse.rightButton.wasPressedThisFrame)
        {
            _startMousePos = _mouse.position.ReadValue();
            _presentCamPos = _camTransform.position;
        }

        // 右マウスボタンが押されている間、マウス移動に応じてカメラを移動
        if (_mouse.rightButton.isPressed)
        {
            // 現在のマウス位置を取得
            Vector2 currentMousePos = _mouse.position.ReadValue();

            // マウス移動量をスクリーン座標の比率に正規化
            float x = (_startMousePos.x - currentMousePos.x) / Screen.width;
            float y = (currentMousePos.y - _startMousePos.y) / Screen.height;

            // 移動速度を計算
            x = x * _positionStep;
            y = y * _positionStep;

            // カメラの回転を考慮して移動ベクトルを計算
            // ローカル座標系での移動をワールド座標系に変換
            Vector3 movement = _camTransform.rotation * new Vector3(x, y, 0);
            _camTransform.position = _presentCamPos + movement;
        }
    }

    /// <summary>
    /// キーボード入力でカメラを移動
    /// WASD：前後左右、QE：上下移動
    /// </summary>
    private void CameraPositionKeyControl()
    {
        // 現在のカメラ位置を取得
        Vector3 campos = _camTransform.position;

        // === 横移動 ===
        // Dキー：カメラの右方向に移動
        if (_keyboard.dKey.isPressed) { campos += _camTransform.right * Time.deltaTime * _positionStep; }
        
        // Aキー：カメラの左方向に移動
        if (_keyboard.aKey.isPressed) { campos -= _camTransform.right * Time.deltaTime * _positionStep; }

        // === 上下移動 ===
        // Eキー：カメラを上方に移動
        if (_keyboard.eKey.isPressed) { campos += _camTransform.up * Time.deltaTime * _positionStep; }
        
        // Qキー：カメラを下方に移動
        if (_keyboard.qKey.isPressed) { campos -= _camTransform.up * Time.deltaTime * _positionStep; }

        // === 前後移動 ===
        // Wキー：カメラの前方に移動
        if (_keyboard.wKey.isPressed) { campos += _camTransform.forward * Time.deltaTime * _positionStep; }
        
        // Sキー：カメラの後方に移動
        if (_keyboard.sKey.isPressed) { campos -= _camTransform.forward * Time.deltaTime * _positionStep; }

        // 計算したカメラ位置を適用
        _camTransform.position = campos;
    }

    /// <summary>
    /// UIメッセージを2秒間表示するコルーチン
    /// </summary>
    private IEnumerator DisplayUiMessage()
    {
        _uiMessageActive = true;
        float time = 0;
        
        // 2秒間待機
        while (time < 2)
        {
            time = time + Time.deltaTime;
            yield return null;
        }
        
        _uiMessageActive = false;
    }

    /// <summary>
    /// GUI描画処理
    /// UIメッセージを画面に表示
    /// </summary>
    void OnGUI()
    {
        // UIメッセージが表示中でない場合はスキップ
        if (_uiMessageActive == false) { return; }

        // メッセージテキストの色を黒に設定
        GUI.color = Color.black;

        // カメラ操作の状態に応じてメッセージを変更
        string message = _cameraMoveActive ? "カメラ操作 有効" : "カメラ操作 無効";

        // 画面中央上部にメッセージを表示
        GUI.Label(new Rect(Screen.width / 2 - 50, Screen.height - 30, 100, 20), message);
    }
}