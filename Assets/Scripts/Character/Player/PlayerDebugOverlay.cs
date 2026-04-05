// ============================================================================
// PlayerDebugOverlay.cs — 运行时调试信息显示
// ============================================================================
// 挂到 Player 物体上，在游戏画面左上角显示关键状态。
// 仅在 UNITY_EDITOR 或 Development Build 中可见。
// 正式发布前删除此脚本或取消勾选 Enable。
// ============================================================================
using UnityEngine;

namespace GhostVeil.Character.Player
{
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [Tooltip("是否显示调试信息")]
        [SerializeField] private bool enable = true;

        private PlayerController _ctrl;
        private GUIStyle _style;
        private string _info = "";

        // 记录上一帧 position.y 以计算帧间变化量
        private float _lastPosY;

        private void Awake()
        {
            _ctrl = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (!enable || _ctrl == null) return;

            float posY = transform.position.y;
            float deltaY = posY - _lastPosY;
            _lastPosY = posY;

            ref var col = ref _ctrl.RaycastCtrl.Collisions;
            string stateName = _ctrl.StateMachine?.CurrentStateName ?? "null";

            _info = $"State: {stateName}\n"
                  + $"Pos.Y: {posY:F4}  (dY: {deltaY:F5})\n"
                  + $"Vel: ({_ctrl.Velocity.x:F2}, {_ctrl.Velocity.y:F2})\n"
                  + $"Grounded: {_ctrl.IsGrounded}  Below: {col.Below}\n"
                  + $"JumpBuf: {_ctrl.JumpBufferTimer:F3}  Coyote: {_ctrl.CoyoteTimer:F3}\n"
                  + $"Slope: {col.SlopeAngle:F1}  Climb: {col.ClimbingSlope}  Desc: {col.DescendingSlope}";
        }

        private void OnGUI()
        {
            if (!enable) return;

            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 14,
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.yellow }
                };
            }

            GUI.Label(new Rect(10, 10, 400, 200), _info, _style);
        }
    }
}
