# Scripts/Audio — 战斗短音

- `ProceduralSfxMath` 只依赖 .NET，固定种子生成 PCM16；响度、淡变、去直流和力度用纯 C# 单测锁定。
- `SfxManager` 是 Godot 播放薄壳：缓存 `AudioStreamWav` 变体、固定复音池和独立 `CombatSfx` 总线。无设备或静音时跳过播放。音量设置归 `UIScaler`。
- 由 `HandUI` / `BoardUI` / `CombatUI` 在实际表现点发音，不订阅规则结算或预测投放。`DeployImpactProfile.SfxIntensity` 传连续轻重参数；本轮使用标准力度。
