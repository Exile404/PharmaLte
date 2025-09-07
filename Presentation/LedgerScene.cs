using System;
using System.Collections.Generic;
using SplashKitSDK;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;

namespace PharmaChainLite.Presentation
{
    public sealed class LedgerScene : IScene
    {
        private readonly ILedgerRepository _ledger;

        private const double TopPad = 64;
        private const double TitleY = TopPad;
        private const double MsgY   = TitleY + 26;

        private readonly Rectangle _listRect    = SplashKit.RectangleFrom(20, 140, 960, 380);
        private readonly Rectangle _prevRect    = SplashKit.RectangleFrom(20, 540, 120, 36);
        private readonly Rectangle _refreshRect = SplashKit.RectangleFrom(160, 540, 120, 36);
        private readonly Rectangle _nextRect    = SplashKit.RectangleFrom(860, 540, 120, 36);

        private readonly Font _font;
        private readonly int _pageSize = 12;
        private int _skip = 0;

        private List<LedgerEntry> _page = new();
        private string _message = "Ledger entries: payments created by deliveries and retail sales.";

        public LedgerScene(ILedgerRepository ledger)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
            LoadPage();
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                if (PointInRect(p, _prevRect)) { _skip = Math.Max(0, _skip - _pageSize); LoadPage(); }
                else if (PointInRect(p, _nextRect))
                {
                    _skip += _pageSize;
                    LoadPage();
                    if (_page.Count == 0) { _skip = Math.Max(0, _skip - _pageSize); LoadPage(); }
                }
                else if (PointInRect(p, _refreshRect)) { LoadPage(); }
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);

            w.DrawText("PharmaChain Lite - Ledger", Color.Black, _font, 22, 20, TitleY);
            w.DrawText(_message, Color.Black, _font, 16, 20, MsgY);

            w.DrawRectangle(Color.Black, _listRect);
            w.DrawText("Date (UTC)                         From -> To                        Amount        Memo",
                       Color.Black, _font, 16, _listRect.X + 10, _listRect.Y - 22);

            double y = _listRect.Y + 10;
            foreach (var e in _page)
            {
                var line = $"{e.OccurredAt:u}   {e.From} -> {e.To}   {e.Amount,10:C}   {e.Memo}";
                w.DrawText(line, Color.Black, _font, 16, _listRect.X + 10, y);
                y += 28;
                if (y > _listRect.Y + _listRect.Height - 24) break;
            }

            DrawBtn(w, _prevRect,    "Prev",    _font);
            DrawBtn(w, _refreshRect, "Refresh", _font);
            DrawBtn(w, _nextRect,    "Next",    _font);
            // (no Refresh here)
        }

        private void LoadPage()
        {
            _page = new List<LedgerEntry>(_ledger.List(_skip, _pageSize));
            _message = $"Showing {_page.Count} items (skip={_skip}).";
        }

        private static void DrawBtn(Window w, Rectangle r, string label, Font font)
        {
            w.FillRectangle(Color.RGBAColor(30,144,255,255), r);
            w.DrawRectangle(Color.Black, r);
            w.DrawText(label, Color.White, font, 18, r.X + 20, r.Y + 7);
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
