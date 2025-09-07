using System;
using System.IO;
using System.Linq;
using SplashKitSDK;

namespace PharmaChainLite.Presentation
{

    public static class AppFonts
    {
        public const string UiName = "ui";

        // Always keep a valid font handle to avoid runtime warnings
        public static Font UI { get; private set; } = SplashKit.GetSystemFont();

        private static bool _ready;

        public static void EnsureReady()
        {
            if (_ready) return;

            
            var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
            if (Directory.Exists(resourcesPath))
                SplashKit.SetResourcesPath(resourcesPath);

           
            try
            {
                var bundleFile = Path.Combine(SplashKit.PathToResources(), "bundles", "default.txt");
                if (File.Exists(bundleFile) && !SplashKit.HasResourceBundle("app"))
                    SplashKit.LoadResourceBundle("app", "default.txt");
            }
            catch { /* non-fatal */ }

            // If the bundle already loaded "ui", use it.
            if (SplashKit.HasFont(UiName))
            {
                UI = SplashKit.FontNamed(UiName);
                _ready = true;
                return;
            }

       
            try
            {
                var fontsDir = SplashKit.PathToResources(ResourceKind.FontResource);
                if (Directory.Exists(fontsDir))
                {
                    var candidate = Directory.EnumerateFiles(fontsDir, "*.*", SearchOption.TopDirectoryOnly)
                        .FirstOrDefault(f =>
                        {
                            var name = Path.GetFileName(f);
                            return name != null && name.Equals("ARIAL.TTF", StringComparison.OrdinalIgnoreCase);
                        });

                    if (candidate != null)
                    {
                        // Register under the name "ui" for reliable TextWidth("ui",...) usage. :contentReference[oaicite:6]{index=6}
                        UI = SplashKit.LoadFont(UiName, Path.GetFileName(candidate));
                        _ready = true;
                        return;
                    }
                }
            }
            catch { /* ignore */ }

            // 4) Last resort: system font (keeps drawing working even without Resources)
            UI = SplashKit.GetSystemFont();
            _ready = true;
        }


        public static bool HasUiNameLoaded => SplashKit.HasFont(UiName);

 
        public static int Measure(string text, int size)
        {
            text ??= string.Empty;

            if (HasUiNameLoaded)
                return SplashKit.TextWidth(UiName, text, size); 

           
            return (int)Math.Ceiling(text.Length * size * 0.62);
        }
    }
}
