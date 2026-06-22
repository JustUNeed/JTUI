using JTUI.Theming;
using System.Windows;

namespace JTool
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            // 优先用上次保存的主题,没保存过则默认 Dark
            JTThemeManager.Initialize(JTTheme.Light);
        }

    }

}
