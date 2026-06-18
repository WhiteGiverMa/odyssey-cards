using System;
using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace OdysseyCards.Infrastructure;

/// <summary>
/// 键盘输入管理器 — Autoload 单例。
///
/// 职责：将物理按键（Key 枚举）映射到逻辑动作（OdysseyInput StringName），
/// 并直接调用 HotkeyManager 分发回调。
///
/// 三层架构中的第一层（物理→逻辑）：
///   物理按键 → InputManager._UnhandledKeyInput → HotkeyManager.DispatchAction → 已注册回调
///
/// 不使用 InputEventAction 中转的原因：
///   Input.ParseInputEvent 在 _UnhandledKeyInput 阶段同步触发存在时序不确定性。
///   直接调用 HotkeyManager 更可靠、更可调试。
///
/// 核心设计：
///   - 维护 Dictionary&lt;StringName, Key&gt; 映射表
///   - 支持多套键位配置（profiles），保存到 user://keybindings/profiles.json
///   - 默认键位对齐 STS2
///   - 桌面端 IsMobile=false 时激活；移动端可选择性启用
///
/// 用法：
///   InputManager.Instance.GetKey(OdysseyInput.EndTurn)  → Key.E
///   InputManager.Instance.SetKey(OdysseyInput.EndTurn, Key.Space)
///   InputManager.Instance.SaveProfiles()
/// </summary>
public partial class InputManager : Node
{
	// ===== 单例 =====

	public static InputManager Instance { get; private set; } = null!;

	// ===== 常量 =====

	private const string ProfilesDir = "user://keybindings/";
	private const string ProfilesFile = "user://keybindings/profiles.json";
	private const string DefaultProfileName = "默认";

	// ===== 状态 =====

	/// <summary>当前活跃的键位映射（动作名 → 物理按键）。</summary>
	private readonly Dictionary<StringName, Key> _keyMap = new();

	/// <summary>所有键位配置集合（配置名 → 映射序列化数据）。</summary>
	private Dictionary<string, Dictionary<string, string>> _profiles = new();

	/// <summary>当前活跃的配置名称。</summary>
	public string ActiveProfileName { get; private set; } = DefaultProfileName;

	/// <summary>是否启用键盘输入转换（桌面端默认启用）。</summary>
	public bool Enabled { get; set; } = true;

	// ===== Godot 生命周期 =====

	public override void _Ready()
	{
		Instance = this;
		LoadProfiles();
	}

	/// <summary>
	/// 原始按键事件入口 — Godot 在 _UnhandledKeyInput 阶段调用。
	/// 遍历映射表，匹配到的按键直接调用 HotkeyManager 分发。
	/// </summary>
	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (!Enabled)
			return;
		if (@event is not InputEventKey keyEvent)
			return;
		if (keyEvent.Echo)
			return; // 忽略按键重复

		// 如果 ChatScreen 可见，只处理控制台相关按键（由 ChatScreen 自己的 _Input 处理）
		var devConsole = GetNodeOrNull<ChatScreen>("/root/ChatScreen");
		if (devConsole?.IsVisible == true)
			return;

		// 如果有 LineEdit 或 TextEdit 正在编辑，不拦截按键
		var focusOwner = GetViewport()?.GuiGetFocusOwner();
		if (focusOwner is LineEdit or TextEdit)
			return;

		bool pressed = keyEvent.Pressed;
		foreach (var (actionName, mappedKey) in _keyMap)
		{
			if (keyEvent.Keycode == mappedKey)
			{
				// 直接调用 HotkeyManager 分发（避免 InputEventAction 时序问题）
				HotkeyManager.Instance?.DispatchAction(actionName, pressed);
				GetViewport()?.SetInputAsHandled();
			}
		}
	}

	// ===== 公共 API =====

	/// <summary>获取指定动作绑定的物理按键。</summary>
	public Key GetKey(StringName action)
	{
		return _keyMap.TryGetValue(action, out var key) ? key : Key.None;
	}

	/// <summary>设置指定动作绑定的物理按键（运行时修改，不持久化）。</summary>
	public void SetKey(StringName action, Key key)
	{
		_keyMap[action] = key;
	}

	/// <summary>检查指定按键是否已被某个动作绑定。</summary>
	public bool IsKeyBound(Key key, out StringName boundAction)
	{
		foreach (var (action, mappedKey) in _keyMap)
		{
			if (mappedKey == key)
			{
				boundAction = action;
				return true;
			}
		}
		boundAction = default;
		return false;
	}

	/// <summary>获取当前配置的完整映射表（用于序列化）。</summary>
	public IReadOnlyDictionary<StringName, Key> GetCurrentMap() => _keyMap;

	// ===== 配置管理 =====

	/// <summary>获取所有配置名称列表。</summary>
	public IEnumerable<string> GetProfileNames() => _profiles.Keys;

	/// <summary>切换到指定配置。</summary>
	public void SwitchProfile(string profileName)
	{
		if (!_profiles.TryGetValue(profileName, out var serializedMap))
		{
			GD.PrintErr($"[InputManager] 配置不存在: {profileName}");
			return;
		}

		ActiveProfileName = profileName;
		_keyMap.Clear();

		// 先应用默认键位，确保新增动作有默认值
		ApplyDefaults();

		// 再覆盖用户自定义的键位（已保存的配置优先）
		foreach (var (actionStr, keyStr) in serializedMap)
		{
			if (Enum.TryParse<Key>(keyStr, out var key))
				_keyMap[new StringName(actionStr)] = key;
		}

		GD.Print($"[InputManager] 切换到键位配置: {profileName}");
	}

	/// <summary>复制当前配置为新配置。</summary>
	public void DuplicateProfile(string newProfileName)
	{
		_profiles[newProfileName] = SerializeCurrentMap();
		GD.Print($"[InputManager] 复制键位配置: {newProfileName}");
	}

	/// <summary>删除指定配置（不允许删除最后一个）。</summary>
	public void DeleteProfile(string profileName)
	{
		if (_profiles.Count <= 1)
		{
			GD.PrintErr("[InputManager] 不允许删除最后一个键位配置");
			return;
		}

		_profiles.Remove(profileName);

		// 如果删除的是当前活跃配置，切换到第一个
		if (ActiveProfileName == profileName)
		{
			foreach (var name in _profiles.Keys)
			{
				SwitchProfile(name);
				break;
			}
		}

		GD.Print($"[InputManager] 删除键位配置: {profileName}");
	}

	/// <summary>重命名当前配置。</summary>
	public void RenameProfile(string oldName, string newName)
	{
		if (!_profiles.ContainsKey(oldName))
			return;
		if (_profiles.ContainsKey(newName))
		{
			GD.PrintErr($"[InputManager] 配置名已存在: {newName}");
			return;
		}

		_profiles[newName] = _profiles[oldName];
		_profiles.Remove(oldName);

		if (ActiveProfileName == oldName)
			ActiveProfileName = newName;

		GD.Print($"[InputManager] 重命名配置: {oldName} → {newName}");
	}

	/// <summary>重置当前配置为默认键位。</summary>
	public void ResetToDefaults()
	{
		_keyMap.Clear();
		ApplyDefaults();
		GD.Print("[InputManager] 已重置为默认键位");
	}

	/// <summary>保存所有配置到文件。</summary>
	public void SaveProfiles()
	{
		// 先把当前 _keyMap 同步到 profiles
		_profiles[ActiveProfileName] = SerializeCurrentMap();

		try
		{
			using var dir = DirAccess.Open("user://");
			if (dir != null && !dir.DirExists("keybindings"))
				dir.MakeDir("keybindings");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[InputManager] 无法创建配置目录: {e.Message}");
		}

		var json = JsonSerializer.Serialize(new ProfilesData
		{
			Profiles = _profiles,
			ActiveProfile = ActiveProfileName,
		}, new JsonSerializerOptions { WriteIndented = true });

		using var file = FileAccess.Open(ProfilesFile, FileAccess.ModeFlags.Write);
		if (file != null)
		{
			file.StoreString(json);
			GD.Print($"[InputManager] 键位配置已保存: {ProfilesFile}");
		}
		else
		{
			GD.PrintErr($"[InputManager] 无法保存键位配置: {ProfilesFile}");
		}
	}

	// ===== 内部方法 =====

	/// <summary>从文件加载所有配置。</summary>
	private void LoadProfiles()
	{
		if (!FileAccess.FileExists(ProfilesFile))
		{
			GD.Print("[InputManager] 键位配置文件不存在，使用默认配置");
			ApplyDefaults();
			_profiles[DefaultProfileName] = SerializeCurrentMap();
			return;
		}

		try
		{
			using var file = FileAccess.Open(ProfilesFile, FileAccess.ModeFlags.Read);
			var json = file.GetAsText();
			var data = JsonSerializer.Deserialize<ProfilesData>(json);

			if (data?.Profiles == null || data.Profiles.Count == 0)
			{
				GD.Print("[InputManager] 配置文件为空，使用默认配置");
				ApplyDefaults();
				_profiles[DefaultProfileName] = SerializeCurrentMap();
				return;
			}

			_profiles = data.Profiles;
			var activeProfile = data.ActiveProfile ?? DefaultProfileName;

			if (!_profiles.ContainsKey(activeProfile))
			{
				// 活跃配置不存在，使用第一个
				foreach (var name in _profiles.Keys)
				{
					activeProfile = name;
					break;
				}
			}

			SwitchProfile(activeProfile);
			GD.Print($"[InputManager] 加载键位配置: {_profiles.Count} 套, 活跃: {activeProfile}");
		}
		catch (Exception e)
		{
			GD.PrintErr($"[InputManager] 加载配置文件失败: {e.Message}");
			ApplyDefaults();
			_profiles[DefaultProfileName] = SerializeCurrentMap();
		}
	}

	/// <summary>应用默认键位（对齐 STS2）。</summary>
	private void ApplyDefaults()
	{
		// 标准导航
		_keyMap[OdysseyInput.Up] = Key.Up;
		_keyMap[OdysseyInput.Down] = Key.Down;
		_keyMap[OdysseyInput.Left] = Key.Left;
		_keyMap[OdysseyInput.Right] = Key.Right;
		_keyMap[OdysseyInput.Accept] = Key.Enter;
		_keyMap[OdysseyInput.Cancel] = Key.Escape;
		_keyMap[OdysseyInput.Select] = Key.Space;

		// 手牌直选（数字键 1~0）
		_keyMap[OdysseyInput.SelectCard1] = Key.Key1;
		_keyMap[OdysseyInput.SelectCard2] = Key.Key2;
		_keyMap[OdysseyInput.SelectCard3] = Key.Key3;
		_keyMap[OdysseyInput.SelectCard4] = Key.Key4;
		_keyMap[OdysseyInput.SelectCard5] = Key.Key5;
		_keyMap[OdysseyInput.SelectCard6] = Key.Key6;
		_keyMap[OdysseyInput.SelectCard7] = Key.Key7;
		_keyMap[OdysseyInput.SelectCard8] = Key.Key8;
		_keyMap[OdysseyInput.SelectCard9] = Key.Key9;
		_keyMap[OdysseyInput.SelectCard10] = Key.Key0;

		// 战斗命令
		_keyMap[OdysseyInput.EndTurn] = Key.E;
		_keyMap[OdysseyInput.Pause] = Key.Escape;
		_keyMap[OdysseyInput.ViewDeck] = Key.D;
		_keyMap[OdysseyInput.ViewDiscard] = Key.S;

		// 目标循环
		_keyMap[OdysseyInput.TabTarget] = Key.Tab;

		// 场景导航
		_keyMap[OdysseyInput.PageUp] = Key.Pageup;
		_keyMap[OdysseyInput.PageDown] = Key.Pagedown;
		_keyMap[OdysseyInput.Skip] = Key.Backspace;

		// 全局界面
		_keyMap[OdysseyInput.InfoScreen] = Key.Capslock;
		_keyMap[OdysseyInput.HeroPower] = Key.H;
	}

	/// <summary>将当前 _keyMap 序列化为可保存的字典。</summary>
	private Dictionary<string, string> SerializeCurrentMap()
	{
		var result = new Dictionary<string, string>();
		foreach (var (action, key) in _keyMap)
		{
			result[action.ToString() ?? string.Empty] = key.ToString();
		}
		return result;
	}

	// ===== 序列化数据结构 =====

	private class ProfilesData
	{
		public Dictionary<string, Dictionary<string, string>> Profiles { get; set; } = new();
		public string? ActiveProfile { get; set; }
	}
}
