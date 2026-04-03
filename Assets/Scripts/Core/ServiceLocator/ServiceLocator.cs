// ============================================================================
// ServiceLocator.cs — 轻量级服务定位器
// ============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;

namespace GhostVeil.Core
{
    /// <summary>
    /// 极简服务定位器 —— 运行时依赖查找。
    /// 
    /// 适用场景：跨系统的松耦合通信（如 PlayerController 获取 INarrativeController）。
    /// 注册时机：各管理器在 Awake 中注册自身，消费者在 Start 或更晚获取。
    /// 
    /// 为什么不用 Singleton？
    /// → Singleton 强制类型为 MonoBehaviour 且全局唯一，
    ///   ServiceLocator 按接口注册，允许运行时替换（方便测试 Mock）。
    /// </summary>
    public static class ServiceLocator
    {
        private static readonly Dictionary<Type, object> _services = new();

        /// <summary>注册服务实例（通常在 Awake 中调用）</summary>
        public static void Register<T>(T service) where T : class
        {
            var key = typeof(T);
            if (_services.ContainsKey(key))
            {
                Debug.LogWarning($"[ServiceLocator] Overwriting existing service: {key.Name}");
            }
            _services[key] = service;
        }

        /// <summary>获取服务（找不到时返回 null 并打警告）</summary>
        public static T Get<T>() where T : class
        {
            var key = typeof(T);
            if (_services.TryGetValue(key, out var service))
                return service as T;

            Debug.LogWarning($"[ServiceLocator] Service not found: {key.Name}");
            return null;
        }

        /// <summary>尝试获取服务（不打警告）</summary>
        public static bool TryGet<T>(out T service) where T : class
        {
            var key = typeof(T);
            if (_services.TryGetValue(key, out var obj))
            {
                service = obj as T;
                return service != null;
            }
            service = null;
            return false;
        }

        /// <summary>注销服务</summary>
        public static void Unregister<T>() where T : class
        {
            _services.Remove(typeof(T));
        }

        /// <summary>清空所有注册（场景重载时调用）</summary>
        public static void ClearAll()
        {
            _services.Clear();
        }
    }
}
