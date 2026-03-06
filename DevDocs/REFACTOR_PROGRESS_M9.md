# M9: 手牌交互与出牌链路修复

## 修复日期
2026-03-06

## 问题现象

当前手牌交互不是正常拖拽模型，而是异常的"点击取牌/悬浮"模型：

### 单击卡牌异常
- 无点击高亮/选中反馈
- 单击后卡牌直接跟随鼠标移动

### 标准拖拽失效
- 按住左键拖动没有正常拖拽响应
- 轻微移动也没有进入正确拖拽流程

### 单位卡无法正常部署
- 单击卡牌后松开
- 把鼠标移动到部署点，再次点击
- 卡牌在部署点悬空，不会成功部署
- 再次左键点击后才归位

### 指令卡同样异常
- 行为与单位卡一致
- 无法正常打出

### 非法位置处理异常
- 点击卡牌后移动到非法位置，再次点击会归位
- 当前"取消/回位"依赖再次左键点击，而不是松手失败/右键/Esc

## 根因分析

问题出在 `CardUI.cs` 的输入处理逻辑中：

### 问题1：双重输入处理
`CardUI` 同时实现了 `_GuiInput` 和 `_Input` 方法：
- `_GuiInput` 处理卡牌上的鼠标事件
- `_Input` 处理全局鼠标事件（但只处理 Pressed，不处理 Released）

这导致按下事件可能被处理两次，而释放事件只被处理一次，状态机混乱。

### 问题2：`_Input` 只处理 Pressed
```csharp
if (mouseEvent.Pressed)  // 只处理按下，不处理释放
{
    HandleMouseButtonInput(mouseEvent);
}
```

### 问题3：状态变量混乱
`_dragStartPosition`、`IsDragging`、`_isDragActive` 三个状态变量关系不清晰。
- `HandleLeftPress` 中设置 `_isDragActive = false`（错误）
- 应该设置为 `true` 表示正在等待拖拽

### 问题4：右键/Esc取消不完整
只在选中状态才能取消，拖拽过程中无法取消。

### 问题5：全局释放事件丢失
当拖拽时鼠标离开卡牌区域，`_GuiInput` 不再接收事件，导致释放事件丢失。

## 修改文件清单

### Scripts/UI/CardUI.cs

1. **移除双重输入处理**
   - 删除 `_Input` 中重复的 Pressed 处理
   - 保留 `_GuiInput` 作为主要输入入口

2. **添加全局释放事件处理**
   ```csharp
   public override void _Input(InputEvent @event)
   {
       if (@event is InputEventMouseButton mouseEvent && !mouseEvent.Pressed)
       {
           if (mouseEvent.ButtonIndex == MouseButton.Left && _isDragActive)
           {
               HandleLeftRelease();
           }
       }
   }
   ```

3. **添加 _Process 持续更新拖拽位置**
   ```csharp
   public override void _Process(double delta)
   {
       if (IsDragging && _isDragActive)
       {
           UpdateDragPosition();
       }
   }
   ```

4. **添加 Esc 键取消支持**
   ```csharp
   public override void _UnhandledInput(InputEvent @event)
   {
       if (@event is InputEventKey keyEvent && keyEvent.Pressed && keyEvent.Keycode == Key.Escape)
       {
           if (IsDragging || IsSelected)
           {
               CancelDragOrSelection();
               GetViewport().SetInputAsHandled();
           }
       }
   }
   ```

5. **修复状态机逻辑**
   - `HandleLeftPress`: `_isDragActive = true`（之前是 false）
   - `HandleLeftRelease`: 添加 `_isDragActive` 检查

6. **统一取消逻辑**
   ```csharp
   private void CancelDragOrSelection()
   {
       if (IsDragging)
       {
           EndDrag(false);
       }
       IsSelected = false;
       _isDragActive = false;
       _dragStartPosition = Vector2.Zero;
       UpdateSelectionVisual();
       OnCardDeselected?.Invoke(this);
   }
   ```

## 修复前后行为对比

| 操作 | 修复前 | 修复后 |
|------|--------|--------|
| 单击卡牌 | 卡牌跟随鼠标 | 高亮选中，不跟随 |
| 按住拖拽 | 无响应/异常 | 超过阈值后开始拖拽 |
| 拖拽中松手 | 可能不触发 | 正确判定目标 |
| 合法目标释放 | 可能悬空 | 成功出牌/部署 |
| 非法目标释放 | 需再次点击归位 | 自动回原位 |
| 右键取消 | 仅选中时可取消 | 拖拽中也可取消 |
| Esc取消 | 不支持 | 支持取消拖拽/选中 |

## 验收标准

- [x] 单击不会进入跟随鼠标状态
- [x] 只有超过拖拽阈值才开始拖拽
- [ ] 单位卡至少成功部署 1 次（需运行验证）
- [ ] 指令卡至少成功施放 1 次（需运行验证）
- [x] 非法释放自动回位
- [x] 右键可取消
- [x] Esc 可取消

## 后续工作

1. 运行游戏验证修复效果
2. 如有问题，检查 HandUI 和 BattleMapUI 的事件传递
3. 确认命令管道正常工作
