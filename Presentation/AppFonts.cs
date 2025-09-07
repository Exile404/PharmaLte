using System;
using System.IO;
using System.Linq;
using SplashKitSDK;

namespace PharmaChainLite.Presentation
{
    /// <summary>
    /// One safe place to load and hold the UI font for the whole app.
    /// </summary>
    public static class AppFonts
    {
        public const string UiName = "ui";

        // Always keep a valid font handle to avoid runtime warnings
        public static Font UI { get; private set; } = SplashKit.GetSystemFont();

        private static bool _ready;

        public static void EnsureReady()
        {
            if (_ready) return;

            // 1) Make sure SplashKit knows where Resources is
            //    (the API allows changing the resources folder at runtime). :contentReference[oaicite:3]{index=3}
            var resourcesPath = Path.Combine(AppContext.BaseDirectory, "Resources");
            if (Directory.Exists(resourcesPath))
                SplashKit.SetResourcesPath(resourcesPath);

            // 2) Try to load a resource bundle if present (Resources/bundles/default.txt).
            //    Bundles can declare: FONT,ui,arial.ttf  :contentReference[oaicite:4]{index=4}
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

            // 3) Direct file load from Resources/fonts, case-insensitive search (handles ARIAL.TTF)
            try
            {
                var fontsDir = SplashKit.PathToResources(ResourceKind.FontResource); // .../Resources/fonts :contentReference[oaicite:5]{index=5}
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

        /// <summary>
        /// True if a font named "ui" is registered (bundle or LoadFont).
        /// </summary>
        public static bool HasUiNameLoaded => SplashKit.HasFont(UiName);

        /// <summary>
        /// Measure text width for layout. Uses name-based TextWidth if "ui" is registered,
        /// otherwise a conservative estimate so buttons never clip.
        /// </summary>
        public static int Measure(string text, int size)
        {
            text ??= string.Empty;

            if (HasUiNameLoaded)
                return SplashKit.TextWidth(UiName, text, size); // documented Text API, name overload. :contentReference[oaicite:7]{index=7}

            // Conservative estimate to avoid clipping if we’re on system font without a name:
            // average glyph width ≈ 0.62em
            return (int)Math.Ceiling(text.Length * size * 0.62);
        }
    }
}
