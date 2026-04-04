// ============================================================================
// SpineSkinManager.cs — Spine 纸娃娃换装管理器（Skin API 换肤/战损/武器切换）
// ============================================================================
//
// ┌──────────────────────────────────────────────────────────────────────────┐
// │  核心功能：                                                              │
// │                                                                          │
// │  1. 管理 Spine 的 Skin 组合系统（纸娃娃 Paper Doll）                   │
// │  2. 支持按部位独立换装：头发、上衣、裤子、武器、特效层                  │
// │  3. 支持战损状态切换（完整衣服 → 破烂衣服）                             │
// │  4. 支持武器切换（空手 → 手枪 → 步枪）                                │
// │  5. 预留代码接口供装备系统、商城系统调用                                │
// │                                                                          │
// │  Spine Skin 工作原理：                                                   │
// │    · Spine 编辑器中为每个可替换部位创建独立的 Skin                      │
// │    · 例如：skin_hair_01, skin_hair_02, skin_body_normal, skin_body_torn │
// │    · 运行时通过 combineSkins API 将多个 Skin 组合为最终外观             │
// │                                                                          │
// │  挂载方式：                                                               │
// │    · 与 SpineAnimator 挂在同一物体上                                    │
// └──────────────────────────────────────────────────────────────────────────┘

using System;
using System.Collections.Generic;
using UnityEngine;
using GhostVeil.Animation.Spine;

namespace GhostVeil.Animation
{
    public class SpineSkinManager : MonoBehaviour
    {
        // ══════════════════════════════════════════════
        //  Inspector 配置
        // ══════════════════════════════════════════════

        [Header("=== 引用（留空自动查找） ===")]
        [SerializeField] private SpineAnimator spineAnimator;

        [Header("=== 默认皮肤配置 ===")]
        [Tooltip("角色的默认皮肤配置（启动时自动应用）")]
        [SerializeField] private SkinSlotConfig[] defaultSkinConfig = new SkinSlotConfig[]
        {
            new SkinSlotConfig { slotName = "body", skinName = "default" },
        };

        // ══════════════════════════════════════════════
        //  事件
        // ══════════════════════════════════════════════

        /// <summary>皮肤变更时触发（部位名, 新皮肤名）</summary>
        public event Action<string /*slotName*/, string /*skinName*/> OnSkinChanged;

        /// <summary>完整皮肤重建时触发</summary>
        public event Action OnSkinRebuilt;

        // ══════════════════════════════════════════════
        //  运行时状态
        // ══════════════════════════════════════════════

        /// <summary>当前各部位的皮肤映射</summary>
        private Dictionary<string, string> _currentSkins = new Dictionary<string, string>();

        /// <summary>当前战损等级（0 = 完好，1 = 轻微，2 = 严重，3 = 濒死）</summary>
        public int DamageLevel { get; private set; }

        // ══════════════════════════════════════════════
        //  Unity 生命周期
        // ══════════════════════════════════════════════

        private void Awake()
        {
            if (spineAnimator == null)
                spineAnimator = GetComponent<SpineAnimator>();
            if (spineAnimator == null)
                spineAnimator = GetComponentInChildren<SpineAnimator>();
        }

        private void Start()
        {
            // 应用默认皮肤配置
            if (defaultSkinConfig != null && defaultSkinConfig.Length > 0)
            {
                foreach (var config in defaultSkinConfig)
                {
                    if (!string.IsNullOrEmpty(config.slotName) && !string.IsNullOrEmpty(config.skinName))
                        _currentSkins[config.slotName] = config.skinName;
                }
                RebuildSkin();
            }
        }

        // ══════════════════════════════════════════════
        //  公共 API — 按部位换装
        // ══════════════════════════════════════════════

        /// <summary>
        /// 设置指定部位的皮肤。
        /// 
        /// 使用示例：
        ///   skinManager.SetSlotSkin("weapon", "skin_weapon_pistol");
        ///   skinManager.SetSlotSkin("body", "skin_body_torn");
        ///   skinManager.SetSlotSkin("hair", "skin_hair_02");
        /// </summary>
        /// <param name="slotName">部位名称（自定义 key，如 "body", "weapon", "hair"）</param>
        /// <param name="skinName">Spine 中的 Skin 名称</param>
        public void SetSlotSkin(string slotName, string skinName)
        {
            _currentSkins[slotName] = skinName;
            RebuildSkin();
            OnSkinChanged?.Invoke(slotName, skinName);

            Debug.Log($"[SkinManager] 部位 \"{slotName}\" → \"{skinName}\"");
        }

        /// <summary>
        /// 移除指定部位的皮肤（该部位不再参与组合）。
        /// </summary>
        public void RemoveSlotSkin(string slotName)
        {
            if (_currentSkins.Remove(slotName))
            {
                RebuildSkin();
                Debug.Log($"[SkinManager] 移除部位 \"{slotName}\"");
            }
        }

        /// <summary>
        /// 一次性设置所有部位。
        /// </summary>
        public void SetAllSkins(Dictionary<string, string> skins)
        {
            _currentSkins.Clear();
            foreach (var kvp in skins)
                _currentSkins[kvp.Key] = kvp.Value;
            RebuildSkin();
        }

        // ══════════════════════════════════════════════
        //  公共 API — 武器切换
        // ══════════════════════════════════════════════

        /// <summary>
        /// 快捷切换武器皮肤。
        /// 
        /// 使用示例：
        ///   skinManager.EquipWeapon("skin_weapon_pistol");
        ///   skinManager.EquipWeapon("skin_weapon_rifle");
        ///   skinManager.UnequipWeapon(); // 空手
        /// </summary>
        public void EquipWeapon(string weaponSkinName)
        {
            SetSlotSkin("weapon", weaponSkinName);
        }

        /// <summary>卸下武器（空手）</summary>
        public void UnequipWeapon()
        {
            RemoveSlotSkin("weapon");
        }

        // ══════════════════════════════════════════════
        //  公共 API — 战损系统
        // ══════════════════════════════════════════════

        /// <summary>
        /// 设置战损等级，自动切换对应皮肤。
        /// 
        /// 命名规则（可在 Spine 编辑器中预设）：
        ///   等级 0: skin_body_normal
        ///   等级 1: skin_body_damaged_1
        ///   等级 2: skin_body_damaged_2
        ///   等级 3: skin_body_damaged_3
        /// 
        /// 使用示例：
        ///   skinManager.SetDamageLevel(0, "skin_body_normal");
        ///   // 受伤后：
        ///   skinManager.SetDamageLevel(1, "skin_body_damaged_1");
        /// </summary>
        public void SetDamageLevel(int level, string bodySkinName)
        {
            DamageLevel = level;
            SetSlotSkin("body", bodySkinName);
            Debug.Log($"[SkinManager] 战损等级: {level}");
        }

        /// <summary>
        /// 便捷方法：根据当前生命百分比自动计算战损等级。
        /// 
        /// 使用示例：
        ///   skinManager.UpdateDamageFromHealth(currentHP, maxHP);
        /// </summary>
        /// <param name="currentHP">当前生命值</param>
        /// <param name="maxHP">最大生命值</param>
        public void UpdateDamageFromHealth(float currentHP, float maxHP)
        {
            if (maxHP <= 0) return;

            float ratio = currentHP / maxHP;
            int level;
            string skinName;

            if (ratio > 0.75f)      { level = 0; skinName = "skin_body_normal"; }
            else if (ratio > 0.5f)  { level = 1; skinName = "skin_body_damaged_1"; }
            else if (ratio > 0.25f) { level = 2; skinName = "skin_body_damaged_2"; }
            else                    { level = 3; skinName = "skin_body_damaged_3"; }

            if (level != DamageLevel)
                SetDamageLevel(level, skinName);
        }

        // ══════════════════════════════════════════════
        //  公共 API — 附加皮肤（叠加而非替换）
        // ══════════════════════════════════════════════

        /// <summary>
        /// 直接附加一个 Skin（不通过部位管理）。
        /// 适用于临时效果（如"着火"特效层、光环等）。
        /// </summary>
        public void AddOverlaySkin(string skinName)
        {
            if (spineAnimator != null)
                spineAnimator.AddSkin(skinName);
        }

        // ══════════════════════════════════════════════
        //  查询 API
        // ══════════════════════════════════════════════

        /// <summary>获取指定部位当前的皮肤名</summary>
        public string GetSlotSkin(string slotName)
        {
            return _currentSkins.TryGetValue(slotName, out var name) ? name : null;
        }

        /// <summary>获取所有当前皮肤配置的只读视图</summary>
        public IReadOnlyDictionary<string, string> GetAllSkins()
        {
            return _currentSkins;
        }

        // ══════════════════════════════════════════════
        //  内部方法
        // ══════════════════════════════════════════════

        /// <summary>
        /// 根据当前 _currentSkins 字典重建组合皮肤。
        /// </summary>
        private void RebuildSkin()
        {
            if (spineAnimator == null) return;

            if (_currentSkins.Count == 0)
            {
                // 无皮肤 → 使用默认
                spineAnimator.SetCombinedSkins("default");
            }
            else
            {
                // 收集所有皮肤名称
                var skinNames = new string[_currentSkins.Count];
                int i = 0;
                foreach (var kvp in _currentSkins)
                {
                    skinNames[i++] = kvp.Value;
                }
                spineAnimator.SetCombinedSkins(skinNames);
            }

            OnSkinRebuilt?.Invoke();
        }
    }

    // ══════════════════════════════════════════════════
    //  数据结构
    // ══════════════════════════════════════════════════

    /// <summary>
    /// 皮肤部位配置（Inspector 用）。
    /// </summary>
    [System.Serializable]
    public class SkinSlotConfig
    {
        [Tooltip("部位名称（自定义 key）")]
        public string slotName = "";

        [Tooltip("Spine 中的 Skin 名称")]
        public string skinName = "";
    }
}
