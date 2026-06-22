# JTUI

WPF 自定义控件库，目标框架 `net8.0-windows`。提供无边框窗口、明暗主题切换（自动记忆），以及一套统一风格的常用控件（按钮、文本框、开关、下拉菜单、数字输入框、标签等）。所有控件以 `JT` 前缀命名，统一走 `DynamicResource` 配色，换肤时全局即时生效。

## 安装与引入

把 `JTUI` 项目作为引用加入你的 WPF 应用，然后在 XAML 中声明命名空间：

```xml
xmlns:jt="clr-namespace:JTUI.Controls;assembly=JTUI"
```

应用启动时初始化一次主题（必须，否则控件样式不会注入）：

```csharp
// App.xaml.cs
using JTUI.Theming;

protected override void OnStartup(StartupEventArgs e)
{
    base.OnStartup(e);
    // 优先恢复上次保存的主题，没有则默认 Light
    JTThemeManager.Initialize(JTTheme.Light);
}
```

## 主题

明暗主题一键切换，选择会自动持久化到 `%AppData%\JTUI\theme.json`，下次启动自动恢复，无需自己写保存代码。

```csharp
using JTUI.Theming;

JTThemeManager.Toggle();                  // 明暗一键翻转（常用）
JTThemeManager.Current = JTTheme.Dark;    // 或直接指定：Light / Dark / System
```

`JTTheme.System` 会跟随 Windows 应用主题。如需关闭自动保存，设置 `JTThemeManager.AutoPersist = false`。主题变化时会触发 `JTThemeManager.ThemeChanged` 事件。

---

## 控件一览
### JTWindow —— 无边框窗口

XAML 根标签换成 `jt:JTWindow`，code-behind 基类也改成 `JTWindow`：

```xml
<jt:JTWindow x:Class="JTool.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:jt="clr-namespace:JTUI.Controls;assembly=JTUI"
        Title="演示" Height="450" Width="800">
    <Grid>
        <TextBlock Text="内容区域" HorizontalAlignment="Center" VerticalAlignment="Center"/>
    </Grid>
</jt:JTWindow>
```

```csharp
using JTUI.Controls;

public partial class MainWindow : JTWindow   // ← 不是 Window
{
    public MainWindow() => InitializeComponent();
}
```

标题栏自带最小化 / 关闭按钮，双击标题栏或拖到屏幕顶部可最大化。窗口的 `Theme` 属性可直接切换整个应用主题。

### JTButton —— 基础按钮

默认无边框、直角，可用 `CornerRadius` 设置圆角。

```xml
<jt:JTButton Content="确定" CornerRadius="4" Click="OnOk"/>
```

### JTIconButton —— 图标按钮

无背景、悬停高亮，内容可以是图标字符或任意元素，默认圆角 4。

```xml
<jt:JTIconButton Content="✕" Click="OnClose"/>
```

### JTTextBox —— 文本输入框

聚焦时边框高亮，支持 `Placeholder` 占位提示和 `CornerRadius` 圆角。

```xml
<jt:JTTextBox Placeholder="请输入…" CornerRadius="2" Width="200"/>
```

### JTToggleSwitch —— 开关

继承自 `ToggleButton`，用 `IsChecked` 表示开 / 关。

```xml
<jt:JTToggleSwitch IsChecked="True"/>
```
### JTComboBox —— 下拉单选菜单

继承原生 `ComboBox`，仅重写视觉风格，完整保留 `ItemsSource` / `SelectedItem` / `SelectedValuePath` / 数据模板 / 键盘操作等原生能力。弹出列表风格与库内菜单统一，支持未选中时的占位提示。

```xml
<!-- 静态项 -->
<jt:JTComboBox Width="140" Placeholder="请选择…">
    <ComboBoxItem Content="选项 A"/>
    <ComboBoxItem Content="选项 B"/>
    <ComboBoxItem Content="选项 C"/>
</jt:JTComboBox>

<!-- 数据绑定 -->
<jt:JTComboBox Width="140"
               ItemsSource="{Binding Options}"
               SelectedItem="{Binding Current}"
               DisplayMemberPath="Name"
               SelectedValuePath="Id"/>
```

常用属性：`Placeholder`（未选中时的提示文字）、`CornerRadius`（圆角）。

### JTNumberBox —— 数字输入框（Blender 风格）

无上下箭头按钮。交互方式：

- **鼠标滚轮**：加减一个 `Step`。
- **按住左右拖动**：平滑改值（拖动时指针隐藏并支持无限横向拖动）。
- **单击**：进入键盘编辑态，全选文本，回车确认 / Esc 取消。
- **修饰键**：按住 `Ctrl` 拖 / 滚为粗调（×10），`Shift` 为精调（×0.1）。

布局为 Label 靠左、数值 + 单位靠右。

```xml
<jt:JTNumberBox Value="50" Minimum="0" Maximum="100"
                Step="1" Decimals="0"
                Label="X: " Unit="px" Width="100"/>
```

```csharp
numBox.ValueChanged += (s, e) =>
{
    Console.WriteLine($"新值: {e.NewValue}");
};
```

常用属性：`Value`、`Minimum`、`Maximum`、`Step`（基础步进）、`DragSensitivity`（拖拽灵敏度，每像素改变多少个 Step）、`Decimals`（小数位数）、`Label`（左侧标签）、`Unit`（右侧单位）、`CornerRadius`。

> 提示：`Value` 默认双向绑定，超出范围会自动夹取并按 `Decimals` 四舍五入。

### JTTagControl —— 标签控件

绑定字符串集合，自动渲染为可点击 / 删除 / 重命名 / 新增的标签。自动去重（默认不区分大小写）并对文本做规范校验。

交互方式：

- **悬停单个标签**：高亮并显示右侧 `×` 删除按钮。
- **点击标签本体**：触发 `TagClicked` 回传该标签值。
- **点击 `×`**：删除标签。
- **双击 / 右键**：进入重命名编辑态（回车确认 / Esc 取消 / 失焦自动提交）。
- **末尾加号按钮**：点击后原地出现一个与标签同款的临时输入框，回车即添加（可连续添加），Esc 收回。

```xml
<jt:JTTagControl x:Name="Tags"
                 AddPlaceholder="输入后回车添加…"
                 TagClicked="Tags_TagClicked"
                 TagRejected="Tags_TagRejected"/>
```

```csharp
// 数据源必须用 ObservableCollection<string>（或其他可写并支持变更通知的 IList），
// 这样增删改后 UI 才会自动刷新。
Tags.ItemsSource = new ObservableCollection<string> { "C#", "WPF", "UI" };

// 点击标签拿到值
private void Tags_TagClicked(object sender, TagChangedEventArgs e)
    => MessageBox.Show($"点击了：{e.Text}");

// 校验失败提示
private void Tags_TagRejected(object sender, TagRejectedEventArgs e)
    => Console.WriteLine($"被拒绝：{e.Text} —— {e.Reason}");
```

外部接口（公开方法），返回 `bool` 表示是否成功：

```csharp
Tags.AddTag("新标签");
Tags.RemoveTag("WPF");
Tags.RenameTag("UI", "界面");
Tags.BeginAddTag();   // 主动展开末尾输入框
```

自定义校验：默认规则会去除首尾空白、折叠中间连续空白为单个空格、拒绝空串、并按去重规则排重。可通过 `TagValidator` 整体替换规则：

```csharp
// 返回 (是否通过, 规范化后的文本)
Tags.TagValidator = raw =>
{
    var s = raw.Trim();
    if (s.Length == 0 || s.Length > 20 || s.Contains(',')) return (false, s);
    return (true, s);
};
```

事件：除上面用到的之外，还提供可取消的“将要变更”事件（`TagAdding` / `TagRemoving` / `TagRenaming`，通过 `e.Cancel = true` 拦截）和“已变更”事件（`TagAdded` / `TagRemoved` / `TagRenamed`），便于在外部统一接管增删改逻辑。

常用属性：`CaseSensitive`（去重是否区分大小写，默认 false）、`AllowAdd`（是否显示加号新增，默认 true）、`AddPlaceholder`（新增输入框占位文字）。

> `JTTag` 是 `JTTagControl` 内部使用的单个标签控件，一般无需直接使用。


---

## 配色键约定

所有颜色定义在 `Themes/Colors.Light.xaml` 与 `Themes/Colors.Dark.xaml` 中，键名形如 `JT.<控件>.<用途>`（例如 `JT.Button.HoverBackground`）。两份文件的键一一对应。若要自定义配色，直接修改这两个文件中对应的 `SolidColorBrush` 即可，控件会通过 `DynamicResource` 自动跟随主题。

## 扩展新控件

库内控件遵循“薄 C# 类 + XAML 模板”约定，新增控件的标准步骤：

1. 在 `Controls/` 新建控件类，静态构造里用 `DefaultStyleKeyProperty.OverrideMetadata` 指向自身，按需添加依赖属性。
2. 在 `Themes/` 新建对应的 `JTXxx.xaml`，写 `Style` 与 `ControlTemplate`，颜色一律用 `{DynamicResource JT.Xxx.Yyy}`。
3. 在 `Themes/JTStyles.xaml` 的 `MergedDictionaries` 中引用新建的 xaml（**别忘了这步，否则控件没有样式**）。
4. 在 `Colors.Light.xaml` 和 `Colors.Dark.xaml` 中**成对**补齐用到的 `JT.Xxx.*` 颜色键。
