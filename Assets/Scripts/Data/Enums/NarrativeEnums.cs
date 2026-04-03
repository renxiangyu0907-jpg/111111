// ============================================================================
// NarrativeEnums.cs — 叙事 / 剧情系统枚举
// ============================================================================
namespace GhostVeil.Data
{
    /// <summary>
    /// 叙事控制权级别（优先级从低到高）
    /// 用于解决"谁在控制角色"的多源冲突
    /// </summary>
    public enum NarrativeAuthorityLevel
    {
        None       = 0,   // 无叙事干预，玩家完全控制
        Ambient    = 10,  // 环境叙事（旁白文字、BGM 切换），不夺取输入
        Dialogue   = 20,  // 对话中，锁定移动但保留 UI 输入
        Cutscene   = 30,  // 过场演出，完全接管角色 + 相机
        Scripted   = 40   // 脚本序列（QTE / 过场战斗），有限输入窗口
    }

    /// <summary>
    /// 交互对象的类别标签
    /// </summary>
    public enum InteractionType
    {
        Examine,    // 纯观察（如墙上的画）
        PickUp,     // 捡起道具
        Dialogue,   // 触发 NPC 对话
        Mechanism,  // 机关 / 开关
        Portal      // 区域切换 / 门
    }
}
