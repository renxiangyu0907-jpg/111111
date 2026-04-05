// ============================================================================
// AbstractRaycastController.cs — 射线碰撞控制器抽象基类（重写版）
// ============================================================================
using UnityEngine;
using GhostVeil.Data;

namespace GhostVeil.Physics
{
    [RequireComponent(typeof(BoxCollider2D))]
    public abstract class AbstractRaycastController : MonoBehaviour, IRaycastController
    {
        [Header("=== Raycast Settings ===")]
        [SerializeField] protected LayerMask collisionMask;
        [SerializeField] protected LayerMask oneWayPlatformMask;
        [SerializeField, Range(2, 20)] protected int horizontalRayCount = 4;
        [SerializeField, Range(2, 20)] protected int verticalRayCount = 4;
        [SerializeField] protected float skinWidth = 0.015f;
        [SerializeField] protected float maxSlopeAngle = 55f;

        protected CollisionInfo _collisions;
        protected BoxCollider2D _boxCollider;
        protected RaycastOrigins _raycastOrigins;
        protected float _horizontalRaySpacing;
        protected float _verticalRaySpacing;
        protected Vector2 _velocityOld;

        public ref CollisionInfo Collisions => ref _collisions;
        public LayerMask CollisionMask { get => collisionMask; set => collisionMask = value; }
        public LayerMask OneWayPlatformMask { get => oneWayPlatformMask; set => oneWayPlatformMask = value; }

        protected struct RaycastOrigins
        {
            public Vector2 TopLeft, TopRight;
            public Vector2 BottomLeft, BottomRight;
        }

        protected virtual void Awake() { _boxCollider = GetComponent<BoxCollider2D>(); }
        protected virtual void Start() { CalculateRaySpacing(); }

        public virtual Vector2 Move(Vector2 desiredMovement, bool standingOnPlatform = false)
        {
            UpdateRaycastOrigins();
            _collisions.Reset();
            _velocityOld = desiredMovement;

            if (desiredMovement.x != 0)
                _collisions.FaceDir = (int)Mathf.Sign(desiredMovement.x);

            Vector2 movement = desiredMovement;

            if (movement.y < 0) DescendSlope(ref movement);
            if (movement.x != 0) HorizontalCollisions(ref movement);
            VerticalCollisions(ref movement);

            transform.Translate(movement);

            if (standingOnPlatform) _collisions.Below = true;
            return movement;
        }

        public void UpdateRaycastOrigins()
        {
            Bounds b = _boxCollider.bounds;
            b.Expand(skinWidth * -2f);
            _raycastOrigins.BottomLeft  = new Vector2(b.min.x, b.min.y);
            _raycastOrigins.BottomRight = new Vector2(b.max.x, b.min.y);
            _raycastOrigins.TopLeft     = new Vector2(b.min.x, b.max.y);
            _raycastOrigins.TopRight    = new Vector2(b.max.x, b.max.y);
        }

        protected void CalculateRaySpacing()
        {
            Bounds b = _boxCollider.bounds;
            b.Expand(skinWidth * -2f);
            horizontalRayCount = Mathf.Clamp(horizontalRayCount, 2, int.MaxValue);
            verticalRayCount   = Mathf.Clamp(verticalRayCount,   2, int.MaxValue);
            _horizontalRaySpacing = b.size.y / (horizontalRayCount - 1);
            _verticalRaySpacing   = b.size.x / (verticalRayCount   - 1);
        }

        protected abstract void HorizontalCollisions(ref Vector2 movement);
        protected abstract void VerticalCollisions(ref Vector2 movement);
        protected abstract void ClimbSlope(ref Vector2 movement, float slopeAngle, Vector2 slopeNormal);
        protected abstract void DescendSlope(ref Vector2 movement);
    }
}
