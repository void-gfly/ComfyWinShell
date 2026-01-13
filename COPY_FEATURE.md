# 镜像源复制功能说明

## 功能概述

在设置页面的镜像配置区域，为每个常用镜像源添加了"复制"按钮，点击后可以快速将 URL 复制到剪贴板。

---

## UI 设计

### GitHub 镜像区域

每个镜像显示为一个独立的卡片，包含：
- **URL 地址**（等宽字体显示）
- **描述标签**（推荐、备用镜像等）
- **复制按钮**（📋 图标 + "复制" 文字）

```
┌──────────────────────────────────────────────────────────────┐
│ https://ghproxy.com/https://github.com              [📋 复制] │
│ 推荐 - 稳定性好                                                │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ https://mirror.ghproxy.com/https://github.com       [📋 复制] │
│ 备用镜像                                                       │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ https://gh.api.99988866.xyz/https://github.com      [📋 复制] │
│ 备用镜像                                                       │
└──────────────────────────────────────────────────────────────┘
```

### pip 镜像区域

同样的卡片设计，包含 4 个常用镜像源：

```
┌──────────────────────────────────────────────────────────────┐
│ https://pypi.tuna.tsinghua.edu.cn/simple            [📋 复制] │
│ 清华大学 - 推荐                                                │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ https://mirrors.aliyun.com/pypi/simple               [📋 复制] │
│ 阿里云                                                         │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ https://pypi.mirrors.ustc.edu.cn/simple             [📋 复制] │
│ 中国科学技术大学                                               │
└──────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────┐
│ https://mirrors.cloud.tencent.com/pypi/simple       [📋 复制] │
│ 腾讯云                                                         │
└──────────────────────────────────────────────────────────────┘
```

---

## 交互效果

### 复制前
- 按钮显示：`📋 复制`
- 鼠标悬停提示：`复制到剪贴板`

### 复制后
- 按钮立即变为：`✓ 已复制`（绿色对勾）
- 2 秒后自动恢复为：`📋 复制`

### 动画流程
```
用户点击 → 复制到剪贴板 → 按钮变为"✓ 已复制" → 等待 2 秒 → 恢复原始状态
```

---

## 实现细节

### XAML 结构

每个镜像卡片使用 `Border` 容器：
```xaml
<Border BorderBrush="#2A2A2A" BorderThickness="1" CornerRadius="4" 
        Padding="8,6" Margin="0,0,0,6" Background="#0A0A0A">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <!-- 左侧：URL 和描述 -->
        <StackPanel Grid.Column="0">
            <TextBlock Text="https://..." FontFamily="Consolas"/>
            <TextBlock Text="描述" Opacity="0.5"/>
        </StackPanel>
        
        <!-- 右侧：复制按钮 -->
        <Button Grid.Column="1" Click="Copy_Click">
            <StackPanel Orientation="Horizontal">
                <TextBlock Text="📋"/>
                <TextBlock Text="复制"/>
            </StackPanel>
        </Button>
    </Grid>
</Border>
```

### C# 复制逻辑

```csharp
private void CopyToClipboard(string text, object sender)
{
    // 1. 复制到剪贴板
    Clipboard.SetText(text);
    
    // 2. 更新按钮状态为"已复制"
    if (sender is Button button)
    {
        button.Content = "✓ 已复制";
        
        // 3. 2秒后恢复原始状态
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += (s, args) => {
            button.Content = "📋 复制";
            timer.Stop();
        };
        timer.Start();
    }
}
```

---

## 用户体验优化

### 视觉反馈
- ✅ 卡片式设计，清晰分隔每个镜像
- ✅ 使用等宽字体（Consolas）显示 URL，便于阅读
- ✅ 描述文字使用较小字号和半透明，不喧宾夺主
- ✅ 复制按钮右对齐，操作区域统一

### 交互优化
- ✅ 一键复制，无需手动选择文本
- ✅ 即时反馈，用户明确知道复制成功
- ✅ 自动恢复按钮状态，无需手动关闭提示
- ✅ 工具提示（Tooltip）说明按钮功能

### 布局优化
- ✅ 所有镜像统一样式，视觉一致性强
- ✅ 合理的间距和内边距，避免拥挤
- ✅ 暗色背景（#0A0A0A）与整体 Dark 主题协调
- ✅ 边框（#2A2A2A）提供微妙的视觉边界

---

## 代码文件

### 修改的文件
1. **`Views/SettingsView.xaml`**
   - 重构镜像列表为卡片式布局
   - 为每个镜像添加复制按钮
   - 添加按钮点击事件绑定

2. **`Views/SettingsView.xaml.cs`**
   - 实现 7 个复制按钮的事件处理方法
   - 实现 `CopyToClipboard` 通用复制方法
   - 实现按钮状态切换和自动恢复逻辑

---

## 镜像列表

### GitHub 镜像（3 个）
| URL | 描述 | 状态 |
|-----|------|------|
| `https://ghproxy.com/https://github.com` | 推荐 - 稳定性好 | ✅ 活跃 |
| `https://mirror.ghproxy.com/https://github.com` | 备用镜像 | ✅ 活跃 |
| `https://gh.api.99988866.xyz/https://github.com` | 备用镜像 | ⚠️ 不稳定 |

### pip 镜像（4 个）
| URL | 机构 | 速度 |
|-----|------|------|
| `https://pypi.tuna.tsinghua.edu.cn/simple` | 清华大学 | ⭐⭐⭐⭐⭐ |
| `https://mirrors.aliyun.com/pypi/simple` | 阿里云 | ⭐⭐⭐⭐ |
| `https://pypi.mirrors.ustc.edu.cn/simple` | 中科大 | ⭐⭐⭐⭐ |
| `https://mirrors.cloud.tencent.com/pypi/simple` | 腾讯云 | ⭐⭐⭐⭐ |

---

## 使用场景

### 场景 1：快速切换镜像
1. 当前镜像访问失败
2. 点击其他镜像的"复制"按钮
3. 粘贴到输入框
4. 保存配置

### 场景 2：分享镜像地址
1. 用户想推荐镜像给他人
2. 点击"复制"按钮
3. 粘贴到聊天工具或文档

### 场景 3：测试多个镜像
1. 逐个复制镜像地址
2. 在外部工具（浏览器、命令行）测试速度
3. 选择最快的镜像

---

## 技术细节

### 剪贴板 API
使用 WPF 标准剪贴板 API：
```csharp
System.Windows.Clipboard.SetText(string text);
```

### DispatcherTimer
用于实现延迟恢复按钮状态：
```csharp
var timer = new System.Windows.Threading.DispatcherTimer
{
    Interval = TimeSpan.FromSeconds(2)
};
timer.Tick += (s, args) => {
    // 恢复逻辑
    timer.Stop();
};
timer.Start();
```

### 动态内容更新
通过修改 `Button.Content` 属性实现按钮文字切换：
```csharp
button.Content = new StackPanel { ... }; // 动态创建内容
```

---

## 未来改进

可选的后续优化：
- [ ] 添加复制动画效果（淡入淡出）
- [ ] 支持右键菜单复制
- [ ] 显示镜像响应速度（实时测速）
- [ ] 支持双击 URL 文本直接复制
- [ ] 添加"一键设置为当前镜像"按钮
- [ ] 记录用户最常用的镜像

---

## 样式参考

### 颜色方案
- 卡片背景：`#0A0A0A`（深黑色）
- 卡片边框：`#2A2A2A`（深灰色）
- URL 文字：`PrimaryTextBrush`（主题色）
- 描述文字：`Opacity="0.5"`（半透明）
- 成功图标：`✓`（绿色对勾）
- 复制图标：`📋`（Emoji 图标）

### 字体
- URL：`Consolas`（等宽字体）
- 描述：系统默认字体
- 按钮：系统默认字体

### 尺寸
- 卡片圆角：`4px`
- 卡片内边距：`8px 6px`
- 卡片间距：`6px`
- 按钮内边距：`8px 4px`
- URL 字号：`12px`
- 描述字号：`11px`
