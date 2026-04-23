using System;
using System.Globalization;
using System.IO;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace GroupyV.Converters
{
    /// <summary>
    /// Convertisseur pour corriger les chemins d'images relatifs de la base de données
    /// vers des chemins absolus utilisables par WPF
    /// </summary>
    public class ImagePathConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.ToString()))
                return null;

            string imagePath = value.ToString();

            try
            {
                // Si le chemin commence par "uploads/", construire le chemin complet
                if (imagePath.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
                {
                    // Obtenir le répertoire de base de l'application
                    string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;
                    
                    // Remonter au dossier racine du projet (bin/Debug/net8.0-windows -> racine)
                    string projectRoot = Path.GetFullPath(Path.Combine(baseDirectory, @"..\..\..\"));
                    
                    // Construire le chemin complet
                    string fullPath = Path.Combine(projectRoot, imagePath);

                    // Vérifier si le fichier existe
                    if (File.Exists(fullPath))
                    {
                        return new BitmapImage(new Uri(fullPath, UriKind.Absolute));
                    }
                    else
                    {
                        // Fichier non trouvé, retourner null (l'icône de fallback sera affichée)
                        System.Diagnostics.Debug.WriteLine($"⚠️ Image non trouvée : {fullPath}");
                        return null;
                    }
                }

                // Si c'est déjà un chemin complet ou une URI, essayer de le charger
                if (Uri.TryCreate(imagePath, UriKind.Absolute, out Uri uri))
                {
                    return new BitmapImage(uri);
                }

                return null;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Erreur de chargement d'image : {ex.Message}");
                return null;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
