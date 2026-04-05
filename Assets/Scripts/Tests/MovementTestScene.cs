// ============================================================================
// MovementTestScene.cs — 一键测试场景
// ============================================================================
// 使用方法：
//   1. Unity 中创建空场景
//   2. 创建空 GameObject，命名 "TestRunner"
//   3. 挂上此脚本
//   4. 运行场景
//
// 此脚本会自动创建：
//   · 地面（带 BoxCollider2D，Layer = Ground）
//   · 墙壁（左右各一面）
//   · 玩家（带所有必要组件）
//   · 摄像机
//
// 然后在屏幕上显示实时测试结果：
//   ✅ / ❌ 地面检测、移动、跳跃、碰墙、下沉检测
// ============================================================================

using UnityEngine;
using GhostVeil.Physics;
using GhostVeil.Input;
using GhostVeil.Data.ScriptableObjects;
using GhostVeil.Character.Player;

namespace GhostVeil.Tests
{
    public class MovementTestScene : MonoBehaviour
    {
        // 自动测试结果
        private string _testResults = "等待测试...";
        private GUIStyle _style;
        private GUIStyle _headerStyle;

        // 引用
        private GameObject _player;
        private PlayerPhysicsController _physics;
        private PlayerController _ctrl;

        // 测试计时
        private float _timer;
        private int _testPhase;

        // 测试数据
        private Vector2 _startPos;
        private bool _groundedOnStart;
        private bool _movedRight;
        private bool _movedLeft;
        private bool _jumped;
        private bool _landed;
        private bool _noSinking;
        private float _idleStartY;
        private float _idleCheckTimer;
        private bool _idleChecked;

        // 手动控制已启用
        private bool _manualMode;

        private void Start()
        {
            // ── 确保 Ground 层存在（Layer 8） ──
            // Unity 内置层 0-7，用户层从 8 开始
            // 注意：如果你的项目已经定义了 "Ground" 层，这里会自动使用

            SetupScene();
        }

        private void SetupScene()
        {
            // ══════════════════════════════════════════════
            //  1. 创建地面
            // ══════════════════════════════════════════════

            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.name = "Ground";
            ground.transform.position = new Vector3(0, -1, 0);
            ground.transform.localScale = new Vector3(30, 1, 1);
            ground.layer = LayerMask.NameToLayer("Ground");

            // 移除 3D 碰撞体，添加 2D 碰撞体
            var col3D = ground.GetComponent<Collider>();
            if (col3D != null) Destroy(col3D);
            var groundCol = ground.AddComponent<BoxCollider2D>();
            groundCol.size = new Vector2(1, 1); // 会被 scale 放大

            // 地面颜色
            var groundRend = ground.GetComponent<Renderer>();
            if (groundRend != null)
                groundRend.material.color = new Color(0.3f, 0.6f, 0.3f);

            // ══════════════════════════════════════════════
            //  2. 创建左墙
            // ══════════════════════════════════════════════

            var wallL = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallL.name = "WallLeft";
            wallL.transform.position = new Vector3(-5, 2, 0);
            wallL.transform.localScale = new Vector3(1, 6, 1);
            wallL.layer = LayerMask.NameToLayer("Ground");
            var colL3D = wallL.GetComponent<Collider>();
            if (colL3D != null) Destroy(colL3D);
            wallL.AddComponent<BoxCollider2D>();
            var wallLRend = wallL.GetComponent<Renderer>();
            if (wallLRend != null) wallLRend.material.color = Color.gray;

            // ══════════════════════════════════════════════
            //  3. 创建右墙
            // ══════════════════════════════════════════════

            var wallR = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wallR.name = "WallRight";
            wallR.transform.position = new Vector3(5, 2, 0);
            wallR.transform.localScale = new Vector3(1, 6, 1);
            wallR.layer = LayerMask.NameToLayer("Ground");
            var colR3D = wallR.GetComponent<Collider>();
            if (colR3D != null) Destroy(colR3D);
            wallR.AddComponent<BoxCollider2D>();
            var wallRRend = wallR.GetComponent<Renderer>();
            if (wallRRend != null) wallRRend.material.color = Color.gray;

            // ══════════════════════════════════════════════
            //  4. 创建玩家
            // ══════════════════════════════════════════════

            _player = new GameObject("Player_Test");
            _player.transform.position = new Vector3(0, 1, 0);

            // Sprite 可视化
            var sr = _player.AddComponent<SpriteRenderer>();
            sr.sprite = CreateSquareSprite();
            sr.color = Color.cyan;

            // BoxCollider2D
            var playerCol = _player.AddComponent<BoxCollider2D>();
            playerCol.size = new Vector2(1, 1);

            // Rigidbody2D（PlayerPhysicsController 会自动设为 Kinematic）
            _player.AddComponent<Rigidbody2D>();

            // PlayerPhysicsController
            _physics = _player.AddComponent<PlayerPhysicsController>();
            // 通过 SerializedField 反射设置 groundMask
            SetPrivateField(_physics, "groundMask", LayerMask.GetMask("Ground"));
            SetPrivateField(_physics, "groundCheckDistance", 0.05f);
            SetPrivateField(_physics, "skinWidth", 0.02f);

            // InputSystemProvider
            _player.AddComponent<InputSystemProvider>();

            // PlayerMovementData（运行时创建）
            var moveData = ScriptableObject.CreateInstance<PlayerMovementData>();
            moveData.maxRunSpeed = 8f;
            moveData.groundAccelTime = 0.08f;
            moveData.groundDecelTime = 0.05f;
            moveData.airAccelTime = 0.15f;
            moveData.airDecelTime = 0.25f;
            moveData.jumpHeight = 3.5f;
            moveData.timeToJumpApex = 0.4f;
            moveData.jumpCutMultiplier = 0.5f;
            moveData.jumpBufferTime = 0.1f;
            moveData.coyoteTime = 0.08f;
            moveData.fallGravityMultiplier = 1.5f;
            moveData.maxFallSpeed = -25f;

            // PlayerController
            _ctrl = _player.AddComponent<PlayerController>();
            SetPrivateField(_ctrl, "moveData", moveData);

            // ══════════════════════════════════════════════
            //  5. 设置摄像机
            // ══════════════════════════════════════════════

            var cam = Camera.main;
            if (cam == null)
            {
                var camObj = new GameObject("MainCamera");
                cam = camObj.AddComponent<Camera>();
                camObj.tag = "MainCamera";
            }
            cam.transform.position = new Vector3(0, 2, -10);
            cam.orthographic = true;
            cam.orthographicSize = 7;

            Debug.Log("[MovementTestScene] 场景已创建！WASD 移动，空格跳跃。");
            Debug.Log("[MovementTestScene] 屏幕左上角显示实时测试状态。");

            _startPos = _player.transform.position;
        }

        private void Update()
        {
            if (_physics == null || _ctrl == null) return;

            _timer += Time.deltaTime;
            Vector2 pos = _player.transform.position;

            // ── 第一帧后检查着地 ──
            if (_timer > 0.5f && !_groundedOnStart)
            {
                _groundedOnStart = _physics.IsGrounded;
            }

            // ── 记录 idle 时的 Y，检测下沉 ──
            if (!_idleChecked && _physics.IsGrounded && _timer > 1f)
            {
                if (_idleCheckTimer == 0f)
                {
                    _idleStartY = pos.y;
                    _idleCheckTimer = _timer;
                }
                else if (_timer - _idleCheckTimer > 2f)
                {
                    float drift = Mathf.Abs(pos.y - _idleStartY);
                    _noSinking = drift < 0.01f;
                    _idleChecked = true;
                }
            }

            // ── 检测移动（手动 WASD 触发） ──
            if (!_movedRight && pos.x > _startPos.x + 0.5f)
                _movedRight = true;
            if (!_movedLeft && pos.x < _startPos.x - 0.5f)
                _movedLeft = true;

            // ── 检测跳跃 ──
            if (!_jumped && pos.y > _startPos.y + 1f)
                _jumped = true;
            if (_jumped && !_landed && _physics.IsGrounded && pos.y < _startPos.y + 0.5f)
                _landed = true;

            // ── 构建显示文本 ──
            string Check(bool v) => v ? "<color=lime>✅ PASS</color>" : "<color=red>❌ WAIT</color>";
            string Fail(bool v) => v ? "<color=lime>✅ PASS</color>" : "<color=red>❌ FAIL</color>";

            _testResults =
                $"<b>=== 移动系统测试 ===</b>\n\n" +
                $"<b>实时状态：</b>\n" +
                $"  Position: ({pos.x:F3}, {pos.y:F3})\n" +
                $"  Velocity: ({_ctrl.Velocity.x:F2}, {_ctrl.Velocity.y:F2})\n" +
                $"  IsGrounded: {_physics.IsGrounded}\n" +
                $"  HitL: {_physics.HitLeft}  HitR: {_physics.HitRight}\n" +
                $"  State: {_ctrl.StateMachine?.CurrentStateName ?? "null"}\n\n" +
                $"<b>自动检测：</b>\n" +
                $"  着地检测:      {(_groundedOnStart ? Fail(true) : (_timer > 1f ? Fail(false) : Check(false)))}\n" +
                $"  无下沉(2秒):   {(_idleChecked ? Fail(_noSinking) : Check(false))}\n\n" +
                $"<b>手动测试（请操作）：</b>\n" +
                $"  按 D 向右移动: {Check(_movedRight)}\n" +
                $"  按 A 向左移动: {Check(_movedLeft)}\n" +
                $"  按空格跳跃:    {Check(_jumped)}\n" +
                $"  跳跃后落地:    {Check(_landed)}\n";
        }

        private void OnGUI()
        {
            if (_headerStyle == null)
            {
                _headerStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 16,
                    fontStyle = FontStyle.Bold,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    richText = true,
                    normal = { textColor = Color.white }
                };
            }

            // 半透明背景
            GUI.Box(new Rect(5, 5, 380, 350), "");
            GUI.Label(new Rect(15, 10, 360, 340), _testResults, _style);
        }

        // ══════════════════════════════════════════════
        //  辅助方法
        // ══════════════════════════════════════════════

        /// <summary>创建一个 1x1 白色方块 Sprite</summary>
        private Sprite CreateSquareSprite()
        {
            var tex = new Texture2D(4, 4);
            var colors = new Color[16];
            for (int i = 0; i < 16; i++) colors[i] = Color.white;
            tex.SetPixels(colors);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 4f);
        }

        /// <summary>通过反射设置 SerializeField 私有字段</summary>
        private void SetPrivateField(object target, string fieldName, object value)
        {
            var type = target.GetType();
            while (type != null)
            {
                var field = type.GetField(fieldName,
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            Debug.LogWarning($"[TestScene] 找不到字段: {fieldName}");
        }
    }
}
