// PlayerDebugOverlay.cs — 运行时调试信息显示
using UnityEngine;

namespace GhostVeil.Character.Player
{
    public class PlayerDebugOverlay : MonoBehaviour
    {
        [SerializeField] private bool enable = true;

        private PlayerController _ctrl;
        private GUIStyle _style;
        private string _info = "";
        private float _lastPosY;

        private void Awake()
        {
            _ctrl = GetComponent<PlayerController>();
        }

        private void LateUpdate()
        {
            if (!enable || _ctrl == null || _ctrl.Physics == null) return;

            float posY = transform.position.y;
            float deltaY = posY - _lastPosY;
            _lastPosY = posY;

            var phys = _ctrl.Physics;
            string stateName = _ctrl.StateMachine?.CurrentStateName ?? "null";

            _info = $"State: {stateName}\n"
                  + $"Pos.Y: {posY:F4}  (dY: {deltaY:F5})\n"
                  + $"Vel: ({_ctrl.Velocity.x:F2}, {_ctrl.Velocity.y:F2})\n"
                  + $"Grounded: {_ctrl.IsGrounded}\n"
                  + $"JumpBuf: {_ctrl.JumpBufferTimer:F3}  Coyote: {_ctrl.CoyoteTimer:F3}\n"
                  + $"HitCeil: {phys.HitCeiling}  HitL: {phys.HitLeft}  HitR: {phys.HitRight}";
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
