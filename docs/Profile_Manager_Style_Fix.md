# 配置管理页面样式修复

## 修复内容

修正了配置管理（Profile Manager）页面中 DataGrid 表格的选中背景色，从原来的亮紫色改为与整体金色主题协调的暗金色系。

## 视觉效果改进

### 修复前
- ❌ 选中行背景：亮紫色（系统默认）
- ❌ 字体显示：在紫色背景下不清晰
- ❌ 与整体黑金主题不协调

### 修复后
- ✅ 选中行背景：`#2A2416`（暗金色）
- ✅ 选中行文字：`#FFD700`（亮金色）
- ✅ 鼠标悬停：`#1A1A1A`（浅灰色）
- ✅ 与整体主题完美协调

## 配色方案

### 表格整体
| 元素 | 颜色 | 说明 |
|-----|------|------|
| 背景 | `#0A0A0A` | 深黑色 |
| 边框 | `#333333` | 深灰色 |
| 行背景 | `#141414` | 黑色 |
| 交替行背景 | `#0F0F0F` | 更深黑色 |
| 网格线 | `#222222` | 深灰线 |

### 列标题
| 元素 | 颜色 | 说明 |
|-----|------|------|
| 背景 | `#1A1A1A` | 深灰黑色 |
| 文字 | `#D4AF37` | 标准金色（主题色） |
| 字体 | 13px Bold | 加粗突出 |

### 选中状态
| 元素 | 颜色 | 说明 |
|-----|------|------|
| 背景 | `#2A2416` | 暗金色（金色底调） |
| 文字 | `#FFD700` | 亮金色（高对比度） |

### 鼠标悬停
| 元素 | 颜色 | 说明 |
|-----|------|------|
| 背景 | `#1A1A1A` | 浅灰黑色 |
| 文字 | 继承当前色 | - |

## 实现细节

### 自定义样式组件

**文件**：`Views/ProfileManagerView.xaml`

添加了 4 个自定义样式：

1. **DataGridStyle**：表格整体样式
   - 背景、边框、行颜色
   - 网格线设置

2. **DataGridColumnHeaderStyle**：列标题样式
   - 金色文字 + 深色背景
   - 加粗字体

3. **DataGridCellStyle**：单元格样式
   - 选中状态：金色系
   - 悬停效果

4. **DataGridRowStyle**：行样式
   - 选中高亮
   - 悬停效果

### 应用样式

```xml
<DataGrid Style="{StaticResource DataGridStyle}"
          ColumnHeaderStyle="{StaticResource DataGridColumnHeaderStyle}"
          CellStyle="{StaticResource DataGridCellStyle}"
          RowStyle="{StaticResource DataGridRowStyle}">
```

## 设计原则

### 1. 主题一致性
- 沿用应用全局的黑金主题
- 金色：`#D4AF37`（标准）、`#FFD700`（高亮）
- 黑色：多层次灰阶（`#050505` ~ `#333333`）

### 2. 可读性优先
- 选中行使用暗金色背景 + 亮金色文字
- 高对比度确保字体清晰可读
- 避免使用饱和度过高的颜色

### 3. 视觉层次
- 列标题：金色加粗（最高层次）
- 选中行：亮金色（次高层次）
- 普通行：浅色（基础层次）
- 悬停行：微亮（交互反馈）

### 4. 交互反馈
- 鼠标悬停：背景微亮
- 点击选中：金色高亮
- 状态清晰可辨

## 技术实现

### Trigger 触发器

```xml
<Style.Triggers>
    <!-- 选中状态 -->
    <Trigger Property="IsSelected" Value="True">
        <Setter Property="Background" Value="#2A2416"/>
        <Setter Property="Foreground" Value="#FFD700"/>
    </Trigger>
    
    <!-- 鼠标悬停 -->
    <Trigger Property="IsMouseOver" Value="True">
        <Setter Property="Background" Value="#1A1A1A"/>
    </Trigger>
</Style.Triggers>
```

### 单元格模板

自定义 `ControlTemplate` 确保样式正确应用：

```xml
<ControlTemplate TargetType="DataGridCell">
    <Border Background="{TemplateBinding Background}" 
            BorderBrush="{TemplateBinding BorderBrush}" 
            BorderThickness="{TemplateBinding BorderThickness}"
            Padding="{TemplateBinding Padding}">
        <ContentPresenter VerticalAlignment="Center"/>
    </Border>
</ControlTemplate>
```

## 兼容性

- ✅ .NET 10.0 WPF
- ✅ Windows 10/11
- ✅ 所有 DPI 缩放比例
- ✅ 深色主题系统

## 其他改进

### 细节优化
- 取消行头宽度（`RowHeaderWidth="0"`）
- 固定行高（`CanUserResizeRows="False"`）
- 单选模式（`SelectionMode="Single"`）
- 统一网格线颜色

### 内边距调整
- 列标题：`Padding="10,8"`
- 单元格：`Padding="10,8"`
- 确保内容不贴边

## 测试建议

### 视觉测试
1. 启动应用 → 导航到"配置"页面
2. 验证表格整体配色与主题协调
3. 点击不同行，检查选中高亮效果
4. 鼠标悬停，检查交互反馈
5. 验证文字清晰度和对比度

### 功能测试
1. 选中配置 → 点击"设为默认"按钮
2. 创建新配置 → 验证自动选中新行
3. 删除配置 → 验证选中状态清除
4. 导入导出 → 验证操作后选中状态

---

**修复版本**：v1.0  
**修复日期**：2026-01-14  
**影响范围**：配置管理页面（ProfileManagerView）
