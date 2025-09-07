using System;
using SplashKitSDK;

namespace PharmaChainLite.Presentation
{

    public sealed class NavBar
    {
        private readonly Font _font;
        private Rectangle[] _tabRects = Array.Empty<Rectangle>();
        private readonly SceneKey[] _order = SceneMap.Ordered;

        public double Height { get; } = 48;

        public NavBar()
        {
            _font = SplashKit.LoadFont("ui", "arial.ttf");
        }


        public void Draw(Window w, SceneKey active)
        {
          
            w.FillRectangle(Color.RGBAColor(245, 247, 250, 255), 0, 0, w.Width, Height);
            w.DrawLine(Color.RGBAColor(220, 225, 230, 255), 0, Height, w.Width, Height);

            
            _tabRects = ComputeTabLayout(w.Width);

            for (int i = 0; i < _order.Length; i++)
            {
                var key = _order[i];
                var r = _tabRects[i];

             
                var isActive = key.Equals(active);
                if (isActive)
                    w.FillRectangle(Color.RGBAColor(225, 235, 255, 255), r);

                w.DrawRectangle(Color.RGBAColor(180, 190, 200, 255), r);
                var label = SceneMap.Label(key);

                // Center the text
                var tx = r.X + (r.Width - 8 * label.Length) / 2; // rough centering
                var ty = r.Y + 8;
                w.DrawText(label, isActive ? Color.RGBAColor(20, 60, 160, 255) : Color.Black, _font, 18, tx, ty);
            }
        }

       
        public SceneKey? HandleInput()
        {
            if (!SplashKit.MouseClicked(MouseButton.LeftButton)) return null;

            var p = SplashKit.MousePosition();
            for (int i = 0; i < _tabRects.Length; i++)
            {
                if (PointInRect(p, _tabRects[i]))
                    return _order[i];
            }
            return null;
        }

        private Rectangle[] ComputeTabLayout(int windowWidth)
        {
            const double leftPad = 16;
            const double gap = 12;
            const double top = 8;
            const double tabW = 130;
            const double tabH = 32;

            var rects = new Rectangle[_order.Length];
            double x = leftPad;

            for (int i = 0; i < rects.Length; i++)
            {
                rects[i] = SplashKit.RectangleFrom(x, top, tabW, tabH);
                x += tabW + gap;
            }

            
            return rects;
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
