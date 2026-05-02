using System.Text;
using System.Windows;

namespace GroupyV
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Enregistre les encodages Windows (Windows-1252, etc.)
            // nécessaire pour l'export CSV compatible Excel français
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            base.OnStartup(e);
        }
    }
}
