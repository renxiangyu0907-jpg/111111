// ============================================================================
// GameEvent.cs — 轻量级类型安全事件总线
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostVeil.Core.Event
{
    /// <summary>
    /// 全局事件总线 —— 基于类型的发布 / 订阅。
    /// 用 struct 做事件载荷可避免 GC；
    /// 订阅自动弱引用管理（手动 Unsubscribe 为最佳实践）。
    /// 
    /// 使用方式:
    ///   GameEvent.Subscribe&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    ///   GameEvent.Publish(new PlayerDiedEvent { ... });
    ///   GameEvent.Unsubscribe&lt;PlayerDiedEvent&gt;(OnPlayerDied);
    /// </summary>
    public static class GameEvent
    {
        private static readonly Dictionary<Type, Delegate> _listeners = new();

        // ── 订阅 ──────────────────────────────────────
        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var key = typeof(T);
            if (_listeners.TryGetValue(key, out var existing))
                _listeners[key] = Delegate.Combine(existing, handler);
            else
                _listeners[key] = handler;
        }

        // ── 取消订阅 ──────────────────────────────────
        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var key = typeof(T);
            if (_listeners.TryGetValue(key, out var existing))
            {
                var result = Delegate.Remove(existing, handler);
                if (result == null)
                    _listeners.Remove(key);
                else
                    _listeners[key] = result;
            }
        }

        // ── 发布 ──────────────────────────────────────
        public static void Publish<T>(T evt) where T : struct
        {
            var key = typeof(T);
            if (_listeners.TryGetValue(key, out var d))
            {
                if (d is Action<T> action)
                    action.Invoke(evt);
                else
                    Debug.LogWarning($"[GameEvent] Type mismatch for {key.Name}");
            }
        }

        // ── 清空（场景切换时调用，防止泄漏） ────────────
        public static void ClearAll()
        {
            _listeners.Clear();
        }
    }
}
