using System.Windows;
using System.Windows.Controls.Primitives;

namespace JTUI.Controls
{
    /// <summary>
    /// JTUI 切换开关。继承自 ToggleButton,用 IsChecked 表示开/关状态。
    /// </summary>
    public class JTToggleSwitch : ToggleButton
    {
        static JTToggleSwitch()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(JTToggleSwitch),
                new FrameworkPropertyMetadata(typeof(JTToggleSwitch)));
        }
    }
}
