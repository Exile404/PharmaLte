using System;
using System.Collections.Generic;
using System.Linq;
using SplashKitSDK;
using PharmaChainLite.Application.Shipments;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Presentation
{
    public sealed class ShipmentsScene : IScene
    {
        private readonly EventingShipmentService _shipSvc;
        private readonly Font _font;

        // Layout
        private readonly Rectangle _listRect      = SplashKit.RectangleFrom(20, 120, 360, 420);
        private readonly Rectangle _detailsRect   = SplashKit.RectangleFrom(400, 120, 560, 420);

        private readonly Rectangle _tokenRect     = SplashKit.RectangleFrom(420, 320, 300, 32);
        private readonly Rectangle _addBtnRect    = SplashKit.RectangleFrom(730, 320, 100, 34);

        private readonly Rectangle _inTransitBtn  = SplashKit.RectangleFrom(420, 372, 140, 34);
        private readonly Rectangle _deliveredBtn  = SplashKit.RectangleFrom(570, 372, 140, 34);
        private readonly Rectangle _refreshBtn    = SplashKit.RectangleFrom(720, 372, 110, 34);

        private string _message = "Select a shipment, then add pack token.";
        private string _addToken = "";
        private int _selectedIndex = -1;

        private enum Active { None, Token }
        private Active _active = Active.None;

        // cache
        private List<Shipment> _cache = new();

     
        private readonly bool _debug = false;   
        private string _dbgLastBuf = "";
        private string _dbgNote = "";
        // -------------------------------------------

        public ShipmentsScene(EventingShipmentService shipSvc)
        {
            _shipSvc = shipSvc ?? throw new ArgumentNullException(nameof(shipSvc));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                if (PointInRect(p, _listRect))
                {
                    CommitBuffer("click->list");
                    SelectRowByPoint(p);
                }

                if (PointInRect(p, _tokenRect))
                {
                    CommitBuffer("click->tokenBox");
                    Begin(Active.Token);
                }

                if (PointInRect(p, _addBtnRect))
                {
                    CommitBuffer("click->add");
                    DoAddPack();
                }

                if (PointInRect(p, _inTransitBtn))
                {
                    CommitBuffer("click->inTransit");
                    DoTransition(ShipmentStatus.InTransit);
                }
                if (PointInRect(p, _deliveredBtn))
                {
                    CommitBuffer("click->delivered");
                    DoTransition(ShipmentStatus.Delivered);
                }
                if (PointInRect(p, _refreshBtn))
                {
                    CommitBuffer("click->refresh");
                    RefreshList();
                    _message = "Refreshed.";
                }
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                CommitBuffer("Enter");
                DoAddPack();
            }

            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                _active = Active.None;
            }

            if (SplashKit.KeyTyped(KeyCode.TabKey))
            {
                CommitBuffer("Tab");
                _active = _active == Active.Token ? Active.None : Active.Token;
                if (_active != Active.None) Begin(_active);
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);

            // Title
            w.DrawText("PharmaChain Lite - Shipments", Color.Black, _font, 22, 20, 60);

            // STATUS MESSAGE (moved up a bit, subtle gray)
            const int msgY = 72; 
            w.DrawText(_message, Color.RGBAColor(60, 60, 60, 255), _font, 16, _detailsRect.X, msgY);

            // Left list
            w.DrawText("Shipments", Color.Black, _font, 18, _listRect.X, _listRect.Y - 28);
            w.DrawRectangle(Color.Black, _listRect);
            DrawShipmentsList(w);

            // Right details
            w.DrawText("Details", Color.Black, _font, 18, _detailsRect.X, _detailsRect.Y - 28);
            w.DrawRectangle(Color.Black, _detailsRect);
            DrawDetails(w);

            if (_debug)
            {
                var hud = $"DBG | active={_active} reading={SplashKit.ReadingText()} | addToken='{_addToken}' | lastBuf='{(_dbgLastBuf ?? "")}' | note={_dbgNote}";
                w.DrawText(hud, Color.RGBAColor(90, 90, 90, 255), _font, 12, 20, 560);
            }
        }

        private void DrawShipmentsList(Window w)
        {
            if (_cache.Count == 0) RefreshList();

            double rowH = 28;
            int visible = (int)Math.Floor(_listRect.Height / rowH);
            int start = 0;
            int end = Math.Min(_cache.Count, start + visible);

            for (int i = start; i < end; i++)
            {
                var y = _listRect.Y + (i - start) * rowH;
                var rowRect = SplashKit.RectangleFrom(_listRect.X, y, _listRect.Width, rowH);

                if (i == _selectedIndex)
                    w.FillRectangle(Color.RGBAColor(230, 244, 255, 255), rowRect);

                w.DrawText($"{_cache[i].Id} [{_cache[i].Status}]", Color.Black, _font, 14, rowRect.X + 6, y + 6);
                w.DrawLine(Color.RGBAColor(230, 230, 230, 255), rowRect.X, y + rowH, rowRect.X + rowRect.Width, y + rowH);
            }
        }

        private void DrawDetails(Window w)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _cache.Count)
            {
                w.DrawText("No shipment selected.", Color.Black, _font, 14, _detailsRect.X + 12, _detailsRect.Y + 12);
                return;
            }

            var s = _cache[_selectedIndex];
            double x = _detailsRect.X + 12;
            double y = _detailsRect.Y + 12;
            double line = 24;

            w.DrawText($"Id: {s.Id}", Color.Black, _font, 14, x, y); y += line;
            // Use ASCII arrow to avoid missing glyphs
            w.DrawText($"From: {s.FromParty}  ->  To: {s.ToParty}", Color.Black, _font, 14, x, y); y += line;
            w.DrawText($"Status: {s.Status}", Color.Black, _font, 14, x, y); y += line;

            y += 8;
            w.DrawText("Packs:", Color.Black, _font, 14, x, y); y += line;

            if (s.PackTokens.Any())
            {
                foreach (var t in s.PackTokens.Take(10))
                {
                    // Use ASCII hyphen as bullet to avoid '?' on systems without • glyph
                    w.DrawText($"- {t}", Color.Black, _font, 14, x + 10, y);
                    y += line;
                }
                if (s.PackTokens.Count > 10)
                {
                    w.DrawText($"(+{s.PackTokens.Count - 10} more...)", Color.RGBAColor(110,110,110,255), _font, 12, x + 10, y);
                    y += line;
                }
            }
            else
            {
                w.DrawText("(none)", Color.RGBAColor(110,110,110,255), _font, 12, x + 10, y); y += line;
            }

            // Add pack controls
            y = _tokenRect.Y - 24;
            w.DrawText("Add Pack Token", Color.Black, _font, 14, _tokenRect.X, y);

            DrawBox(w, _tokenRect, _addToken, _active == Active.Token);

            w.FillRectangle(Color.RGBAColor(30, 144, 255, 255), _addBtnRect);
            w.DrawRectangle(Color.Black, _addBtnRect);
            w.DrawText("Add", Color.White, _font, 16, _addBtnRect.X + 34, _addBtnRect.Y + 7);

            // Status buttons
            w.FillRectangle(Color.RGBAColor(255, 215, 0, 255), _inTransitBtn);
            w.DrawRectangle(Color.Black, _inTransitBtn);
            w.DrawText("Set InTransit", Color.Black, _font, 14, _inTransitBtn.X + 14, _inTransitBtn.Y + 8);

            w.FillRectangle(Color.RGBAColor(60, 179, 113, 255), _deliveredBtn);
            w.DrawRectangle(Color.Black, _deliveredBtn);
            w.DrawText("Set Delivered", Color.White, _font, 14, _deliveredBtn.X + 14, _deliveredBtn.Y + 8);

            w.FillRectangle(Color.RGBAColor(200, 200, 200, 255), _refreshBtn);
            w.DrawRectangle(Color.Black, _refreshBtn);
            w.DrawText("Refresh", Color.Black, _font, 14, _refreshBtn.X + 24, _refreshBtn.Y + 8);
        }

        private void DrawBox(Window w, Rectangle rect, string text, bool focused)
        {
            w.DrawRectangle(Color.Black, rect);
            if (focused && SplashKit.ReadingText())
            {
                SplashKit.DrawCollectedText(Color.Black, _font, 18, SplashKit.OptionDefaults());
                w.DrawRectangle(Color.RGBAColor(30,144,255,255), rect);
            }
            else
            {
                w.DrawText(text ?? "", Color.Black, _font, 18, rect.X + 6, rect.Y + 6);
            }
        }


        private void DoAddPack()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _cache.Count)
            {
                _message = "Select a shipment first.";
                return;
            }

            var s = _cache[_selectedIndex];
            var token = (_addToken ?? string.Empty).Trim().ToUpperInvariant();
            if (token.Length == 0) { _message = "Enter a token to add."; return; }

            if (_debug) Console.WriteLine($"[ShipmentsScene.DoAddPack] Using token='{token}' for shipment='{s.Id}'");

            try
            {
                _shipSvc.AddPack(s.Id, token);
                _message = $"Added {token} to {s.Id}.";
                _addToken = "";
                RefreshList();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
                if (_debug) Console.WriteLine($"[ShipmentsScene.DoAddPack] Exception: {ex}");
            }
        }

        private void DoTransition(ShipmentStatus next)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _cache.Count)
            {
                _message = "Select a shipment first.";
                return;
            }

            var s = _cache[_selectedIndex];
            try
            {
                _shipSvc.Transition(s.Id, next);
                _message = $"Shipment {s.Id} -> {next}.";
                RefreshList();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
                if (_debug) Console.WriteLine($"[ShipmentsScene.DoTransition] Exception: {ex}");
            }
        }

        private void RefreshList()
        {
            _cache = _shipSvc.List(0, 500).ToList();
            if (_selectedIndex >= _cache.Count) _selectedIndex = _cache.Count - 1;
        }


        private void Begin(Active field)
        {
            _active = field;
            Rectangle r = field switch
            {
                Active.Token => _tokenRect,
                _ => _tokenRect
            };
            SplashKit.StartReadingText(r);
            if (_debug) Console.WriteLine($"[ShipmentsScene.Begin] field={_active}");
        }

        private void CommitBuffer(string reason)
        {
            bool wasReading = SplashKit.ReadingText();
            if (wasReading) SplashKit.EndReadingText();

            string buf = (SplashKit.TextInput() ?? string.Empty);
            string trimmed = buf.Trim();

            _dbgLastBuf = Abbrev(buf, 40);
            _dbgNote = $"{reason} | wasReading={wasReading} | bufLen={buf.Length}";

            if (_active == Active.Token)
            {
                if (trimmed.Length > 0)
                    _addToken = trimmed.ToUpperInvariant();
            }

            if (_debug) Console.WriteLine($"[ShipmentsScene.Commit] reason={reason} active={_active} captured='{buf}' -> addTokenNow='{_addToken}'");

            _active = Active.None;
        }

        private void SelectRowByPoint(Point2D p)
        {
            double rowH = 28;
            int index = (int)Math.Floor((p.Y - _listRect.Y) / rowH);
            if (index < 0) index = 0;

            int visible = (int)Math.Floor(_listRect.Height / rowH);
            int start = 0;
            int end = Math.Min(_cache.Count, start + visible);

            int absolute = start + index;
            if (absolute >= 0 && absolute < end)
            {
                _selectedIndex = absolute;
                if (_debug) Console.WriteLine($"[ShipmentsScene.Select] selectedIndex={_selectedIndex} id={_cache[_selectedIndex].Id}");
            }
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;

        private static string Abbrev(string s, int n) => s.Length <= n ? s : s.Substring(0, n) + "…";
    }
}
