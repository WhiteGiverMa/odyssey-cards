# Scripts/Audio — 战斗短音

- `ProceduralSfxMath` 用短噪声撞击、实木非谐波模态与桌腔合成确定性的 PCM16；不依赖 Godot 或外部素材。连续力度插值保留，重拍的 `HeavyBomb` / `HeavyPia` 分开合成。
- `SfxManager` 在启动时预烘焙四种拍桌音色及其变体，复用固定播放池与 `CombatSfx` 总线；无设备或静音时不播放。音量设置归 `UIScaler`。
- 基础费用 ≤2 / 3–4 / ≥5 选轻 / 中 / 重；触桌揭幕同帧用 `DeployImpactProfile.SfxIntensity` 选音色，重档默认 `HeavyBomb`。`/sfx bomb` 与 `/sfx pia` 可独立试听。
