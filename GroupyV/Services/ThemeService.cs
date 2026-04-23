using System.Windows;
using System.Windows.Media;

namespace GroupyV.Services
{
    public class ThemeService
    {
        private static ThemeService _instance;
        public static ThemeService Instance => _instance ??= new ThemeService();

        public bool IsDarkMode { get; private set; } = true;

        private ThemeService() { }

        public void ApplyTheme(bool isDark)
        {
            IsDarkMode = isDark;
            var res = Application.Current.Resources;

            if (isDark)
            {
                Set(res, "MainBgColor", "#0F111A");
                Set(res, "SidebarColor", "#1A1C2E");
                Set(res, "AccentColor", "#6C5DD3");
                Set(res, "NavHoverBg", "#2D304D");
                Set(res, "GlassBorder", "#34374C");
                Set(res, "NavTextInactive", "#808191");
                Set(res, "CardBgColor", "#1A1C2E");
                Set(res, "CardBgDarkerColor", "#151723");
                Set(res, "CardHoverColor", "#1E2033");
                Set(res, "BorderSubtleColor", "#2D304D");
                Set(res, "InputBorderColor", "#3F4259");
                Set(res, "TextPrimaryColor", "#FFFFFF");
                Set(res, "TextSecondaryColor", "#808191");
                Set(res, "FooterBgColor", "#252836");
            }
            else
            {
                Set(res, "MainBgColor", "#F0F2F5");
                Set(res, "SidebarColor", "#FFFFFF");
                Set(res, "AccentColor", "#6C5DD3");
                Set(res, "NavHoverBg", "#E8EAF0");
                Set(res, "GlassBorder", "#D1D5DB");
                Set(res, "NavTextInactive", "#6B7280");
                Set(res, "CardBgColor", "#FFFFFF");
                Set(res, "CardBgDarkerColor", "#F5F7FA");
                Set(res, "CardHoverColor", "#EEF0F5");
                Set(res, "BorderSubtleColor", "#E2E4E9");
                Set(res, "InputBorderColor", "#D1D5DB");
                Set(res, "TextPrimaryColor", "#1A1C2E");
                Set(res, "TextSecondaryColor", "#6B7280");
                Set(res, "FooterBgColor", "#F5F7FA");
            }

            // Also update MainWindow local resources if they exist
            var mainWindow = Application.Current.MainWindow;
            if (mainWindow != null)
            {
                foreach (var key in new[] { "MainBgColor", "SidebarColor", "AccentColor", "NavHoverBg", "GlassBorder", "NavTextInactive" })
                {
                    if (mainWindow.Resources.Contains(key))
                        mainWindow.Resources[key] = res[key];
                }
            }
        }

        private static void Set(ResourceDictionary res, string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            var brush = new SolidColorBrush(color);
            brush.Freeze();
            res[key] = brush;
        }
    }
}
