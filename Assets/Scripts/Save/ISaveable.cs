// ============================================================================
// ISaveable.cs — 可存档对象接口
// ============================================================================
namespace GhostVeil.Save
{
    /// <summary>
    /// 需要持久化的对象实现此接口。
    /// 存档管理器遍历场景中所有 ISaveable 实现，统一序列化。
    /// </summary>
    public interface ISaveable
    {
        /// <summary>全局唯一存档 ID（用于反序列化时匹配对象）</summary>
        string SaveID { get; }

        /// <summary>捕获当前状态为可序列化数据</summary>
        object CaptureState();

        /// <summary>从存档数据恢复状态</summary>
        void RestoreState(object state);
    }
}
