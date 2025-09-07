using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SplashKitSDK;
using PharmaChainLite.Application.Medicines;
using PharmaChainLite.Domain;

namespace PharmaChainLite.Presentation
{
    /// <summary>
    /// Admin screen for medicines with Price and From/To parties.
    /// </summary>
    public sealed class MedicinesScene : IScene
    {
        private readonly MedicineService _service;

        private const int TitleSize = 22;
        private const int TextSize  = 16;
        private const int InputSize = 18;

        private const double TopPad  = 64;
        private const double LeftPad = 20;

        private Rectangle _listRect;
        private Rectangle _refreshBtnRect;

        private Rectangle _nameRect;
        private Rectangle _batchRect;
        private Rectangle _manuRect;
        private Rectangle _expiryRect;
        private Rectangle _priceRect;
        private Rectangle _fromRect;
        private Rectangle _toRect;
        private Rectangle _pinRect;

        private Rectangle _addUpdateBtnRect;
        private Rectangle _removeBtnRect;

        private Rectangle _prevBtnRect;
        private Rectangle _nextBtnRect;

        private double _lastW = -1, _lastH = -1;

        private readonly List<Medicine> _page = new();
        private int _skip = 0;
        private const int PageSize = 100;
        private int _sel = -1;

        private string _nameInput   = "";
        private string _batchInput  = "";
        private string _manuInput   = "";
        private string _expiryInput = "";
        private string _priceInput  = "";
        private string _fromInput   = "ManuCo";
        private string _toInput     = "DistCo";
        private string _pinInput    = "";

        private enum Field { None, Name, Batch, Manu, Expiry, Price, From, To, Pin }
        private Field _active = Field.None;

        private string _message = "Enter medicine details (Admin PIN required), then Add/Update.";

        public MedicinesScene(MedicineService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            RefreshList();
        }

        private void Layout(Window w)
        {
            if (_lastW == w.Width && _lastH == w.Height) return;

            double leftColX  = LeftPad;
            double leftColW  = Math.Max(420, w.Width * 0.42);
            double rightColX = leftColX + leftColW + 20;
            double rightColW = Math.Max(420, w.Width - rightColX - LeftPad);

            _listRect = SplashKit.RectangleFrom(leftColX, 140, leftColW, w.Height - 180);

            const double refreshW = 120, refreshH = 32;
            _refreshBtnRect = SplashKit.RectangleFrom(
                leftColX + leftColW - refreshW,
                TopPad + 42,
                refreshW, refreshH
            );

            double y = 140;
            double gap = 48;

            _nameRect   = SplashKit.RectangleFrom(rightColX, y,                 rightColW, 32); y += gap;
            _batchRect  = SplashKit.RectangleFrom(rightColX, y,                 Math.Min(280, rightColW), 32); y += gap;
            _manuRect   = SplashKit.RectangleFrom(rightColX, y,                 rightColW, 32); y += gap;
            _expiryRect = SplashKit.RectangleFrom(rightColX, y,                 Math.Min(220, rightColW), 32); y += gap;

            // new: price + from/to
            _priceRect  = SplashKit.RectangleFrom(rightColX, y,                 Math.Min(160, rightColW), 32); y += gap;
            _fromRect   = SplashKit.RectangleFrom(rightColX, y,                 Math.Min(260, rightColW), 32);
            _toRect     = SplashKit.RectangleFrom(_fromRect.X + _fromRect.Width + 16, y, Math.Min(260, rightColW - _fromRect.Width - 16), 32);
            y += gap;

            _pinRect    = SplashKit.RectangleFrom(rightColX, y,                 Math.Min(220, rightColW), 32); y += gap + 8;

            _addUpdateBtnRect = SplashKit.RectangleFrom(rightColX,                  y, 160, 34);
            _removeBtnRect    = SplashKit.RectangleFrom(_addUpdateBtnRect.X + 180,  y, 120, 34);

            double pagingY    = w.Height - 38;
            _prevBtnRect      = SplashKit.RectangleFrom(leftColX,       pagingY, 80, 28);
            _nextBtnRect      = SplashKit.RectangleFrom(leftColX + 100, pagingY, 80, 28);

            _lastW = w.Width; _lastH = w.Height;
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                if (PointInRect(p, _listRect))
                {
                    int row = (int)((p.Y - (_listRect.Y + 10)) / 26.0);
                    if (row >= 0 && row < _page.Count)
                    {
                        _sel = row; LoadSelectionIntoInputs(_page[_sel]);
                        _message = $"Selected batch {_batchInput}.";
                    }
                }

                if (PointInRect(p, _nameRect))   { CommitActiveEdit(); BeginEdit(Field.Name); }
                if (PointInRect(p, _batchRect))  { CommitActiveEdit(); BeginEdit(Field.Batch); }
                if (PointInRect(p, _manuRect))   { CommitActiveEdit(); BeginEdit(Field.Manu); }
                if (PointInRect(p, _expiryRect)) { CommitActiveEdit(); BeginEdit(Field.Expiry); }
                if (PointInRect(p, _priceRect))  { CommitActiveEdit(); BeginEdit(Field.Price); }
                if (PointInRect(p, _fromRect))   { CommitActiveEdit(); BeginEdit(Field.From); }
                if (PointInRect(p, _toRect))     { CommitActiveEdit(); BeginEdit(Field.To); }
                if (PointInRect(p, _pinRect))    { CommitActiveEdit(); BeginEdit(Field.Pin); }

                if (PointInRect(p, _refreshBtnRect)) { CommitActiveEdit(); RefreshList(); _message = "Medicines refreshed."; }
                if (PointInRect(p, _addUpdateBtnRect)) { CommitActiveEdit(); DoAddOrUpdate(); }
                if (PointInRect(p, _removeBtnRect))    { CommitActiveEdit(); DoRemove(); }

                if (PointInRect(p, _prevBtnRect)) { if (_skip >= PageSize) _skip -= PageSize; RefreshList(); }
                if (PointInRect(p, _nextBtnRect)) { _skip += PageSize; RefreshList(); }
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                if (_active == Field.Pin) { CommitActiveEdit(); DoAddOrUpdate(); }
                else CommitActiveEdit();
            }

            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                _active = Field.None;
            }

            if (SplashKit.KeyTyped(KeyCode.TabKey))
            {
                _active = _active switch
                {
                    Field.None   => Field.Name,
                    Field.Name   => Field.Batch,
                    Field.Batch  => Field.Manu,
                    Field.Manu   => Field.Expiry,
                    Field.Expiry => Field.Price,
                    Field.Price  => Field.From,
                    Field.From   => Field.To,
                    Field.To     => Field.Pin,
                    _            => Field.None
                };
                if (_active != Field.None) SplashKit.StartReadingText(RectFor(_active));
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            Layout(w);

            w.Clear(Color.White);
            w.DrawText("PharmaChain Lite - Medicines (Admin)", Color.Black, AppFonts.UI, TitleSize, LeftPad, TopPad);
            w.DrawText(_message ?? string.Empty, Color.Black, AppFonts.UI, TextSize, LeftPad, TopPad + 26);

            DrawButton(w, _refreshBtnRect, "Refresh", Color.RGBAColor(60,179,113,255), Color.White, TextSize);

            w.DrawRectangle(Color.Black, _listRect);
            w.DrawText("Medicines", Color.Black, AppFonts.UI, TextSize + 2, _listRect.X + 10, _listRect.Y - 22);

            double y = _listRect.Y + 10;
            for (int i = 0; i < _page.Count; i++)
            {
                var m = _page[i];

                if (i == _sel)
                {
                    var rowRect = SplashKit.RectangleFrom(_listRect.X + 2, y - 2, _listRect.Width - 4, 24);
                    w.FillRectangle(Color.RGBAColor(225, 235, 255, 255), rowRect);
                }

                var exp = m.ExpiryUtc.HasValue ? m.ExpiryUtc.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "n/a";
                var priceStr = TryGetPrice(m)?.ToString("0.##", CultureInfo.InvariantCulture) ?? "-";
                string line = $"{m.BatchNo}  |  {m.Name}  |  {m.Manufacturer}  |  Exp: {exp}  |  Price: {priceStr}";
                w.DrawText(line, Color.Black, AppFonts.UI, TextSize, _listRect.X + 10, y);
                y += 26;
                if (y > (_listRect.Y + _listRect.Height) - 20) break;
            }

            DrawLabel(w, "Name", _nameRect.X, _nameRect.Y - 20);
            DrawBox(w, _nameRect, _nameInput, _active == Field.Name);

            DrawLabel(w, "Batch No", _batchRect.X, _batchRect.Y - 20);
            DrawBox(w, _batchRect, _batchInput, _active == Field.Batch);

            DrawLabel(w, "Manufacturer", _manuRect.X, _manuRect.Y - 20);
            DrawBox(w, _manuRect, _manuInput, _active == Field.Manu);

            DrawLabel(w, "Expiry (YYYY-MM-DD, optional)", _expiryRect.X, _expiryRect.Y - 20);
            DrawBox(w, _expiryRect, _expiryInput, _active == Field.Expiry);

            DrawLabel(w, "Price (optional)", _priceRect.X, _priceRect.Y - 20);
            DrawBox(w, _priceRect, _priceInput, _active == Field.Price);

            DrawLabel(w, "From (party)", _fromRect.X, _fromRect.Y - 20);
            DrawBox(w, _fromRect, _fromInput, _active == Field.From);

            DrawLabel(w, "To (party)", _toRect.X, _toRect.Y - 20);
            DrawBox(w, _toRect, _toInput, _active == Field.To);

            DrawLabel(w, "Admin PIN", _pinRect.X, _pinRect.Y - 20);
            DrawBox(w, _pinRect, string.IsNullOrEmpty(_pinInput) ? "" : new string('•', _pinInput.Length), _active == Field.Pin);

            DrawButton(w, _addUpdateBtnRect, "Add/Update", Color.RGBAColor(30,144,255,255), Color.White, TextSize);
            DrawButton(w, _removeBtnRect,    "Remove",     Color.RGBAColor(220, 20, 60,255), Color.White, TextSize);

            DrawButton(w, _prevBtnRect, "Prev", Color.RGBAColor(230,230,230,255), Color.Black, TextSize);
            DrawButton(w, _nextBtnRect, "Next", Color.RGBAColor(230,230,230,255), Color.Black, TextSize);
        }

        // ---------- Actions ----------
        private void DoAddOrUpdate()
        {
            try
            {
                string name  = (_nameInput  ?? "").Trim();
                string batch = (_batchInput ?? "").Trim();
                string manu  = (_manuInput  ?? "").Trim();
                string pin   = (_pinInput   ?? "").Trim();
                string from  = (_fromInput  ?? "ManuCo").Trim();
                string to    = (_toInput    ?? "DistCo").Trim();

                DateTime? expiryUtc = TryParseExpiryUtc(_expiryInput);
                decimal? price = TryParsePrice(_priceInput);

                _service.AddOrUpdate(name, batch, expiryUtc, manu, pin, from, to, price);

                _message = $"Saved medicine '{name}' (batch {batch}).";
                RefreshList();
            }
            catch (Exception ex)
            {
                _message = $"Save failed: {ex.Message}";
            }
        }

        private void DoRemove()
        {
            try
            {
                var batch = (_batchInput ?? "").Trim();
                var pin   = (_pinInput   ?? "").Trim();
                if (batch.Length == 0) { _message = "Enter a Batch No to remove."; return; }

                bool removed = _service.Remove(batch, pin);
                _message = removed ? $"Removed medicine {batch}." : $"No medicine found for {batch}.";
                RefreshList();
            }
            catch (Exception ex)
            {
                _message = $"Remove failed: {ex.Message}";
            }
        }

        private void RefreshList()
        {
            _page.Clear();
            IEnumerable<Medicine> items;
            try { items = _service.List(_skip, PageSize) ?? Enumerable.Empty<Medicine>(); }
            catch { items = Enumerable.Empty<Medicine>(); }

            foreach (var m in items) _page.Add(m);
            if (_page.Count == 0) { _sel = -1; _message = "No medicines found. Enter details and click Add/Update."; }
            else
            {
                if (_sel < 0) _sel = 0;
                else if (_sel >= _page.Count) _sel = _page.Count - 1;
            }
        }

        private void LoadSelectionIntoInputs(Medicine m)
        {
            _nameInput   = m?.Name ?? "";
            _batchInput  = m?.BatchNo ?? "";
            _manuInput   = m?.Manufacturer ?? "";
            _expiryInput = m?.ExpiryUtc.HasValue == true
                ? m.ExpiryUtc!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                : "";

            var price = TryGetPrice(m);
            _priceInput = price.HasValue ? price.Value.ToString("0.##", CultureInfo.InvariantCulture) : "";

            // Keep From/To as currently typed; shipments are created at save-time.
        }

        // ---------- Parsing helpers ----------
        private static DateTime? TryParseExpiryUtc(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;

            if (DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d))
                return DateTime.SpecifyKind(d, DateTimeKind.Utc);

            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var any))
                return any;

            return null;
        }

        private static decimal? TryParsePrice(string s)
        {
            s = (s ?? "").Trim();
            if (s.Length == 0) return null;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out var d)) return d;
            if (decimal.TryParse(s, out d)) return d;
            return null;
        }

        private static decimal? TryGetPrice(Medicine m)
        {
            try
            {
                var pi = m.GetType().GetProperty("Price", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.IgnoreCase);
                if (pi == null) return null;
                var v = pi.GetValue(m);
                return v is decimal dec ? dec : v as decimal?;
            }
            catch { return null; }
        }

        // ---------- Text edit & drawing ----------
        private Rectangle RectFor(Field f) => f switch
        {
            Field.Name   => _nameRect,
            Field.Batch  => _batchRect,
            Field.Manu   => _manuRect,
            Field.Expiry => _expiryRect,
            Field.Price  => _priceRect,
            Field.From   => _fromRect,
            Field.To     => _toRect,
            Field.Pin    => _pinRect,
            _            => SplashKit.RectangleFrom(0, 0, 0, 0)
        };

        private void BeginEdit(Field f)
        {
            _active = f;
            SplashKit.StartReadingText(RectFor(f));
        }

        private void CommitActiveEdit()
        {
            if (!SplashKit.ReadingText()) return;

            SplashKit.EndReadingText();
            if (!SplashKit.TextEntryCancelled())
            {
                string text = (SplashKit.TextInput() ?? "").Trim();
                switch (_active)
                {
                    case Field.Name:   _nameInput = text; break;
                    case Field.Batch:  _batchInput = text; break;
                    case Field.Manu:   _manuInput = text; break;
                    case Field.Expiry: _expiryInput = text; break;
                    case Field.Price:  _priceInput = text; break;
                    case Field.From:   _fromInput = text; break;
                    case Field.To:     _toInput = text; break;
                    case Field.Pin:    _pinInput = text; break;
                }
            }
            _active = Field.None;
        }

        private static void DrawLabel(Window w, string text, double x, double y) =>
            w.DrawText(text ?? string.Empty, Color.Black, AppFonts.UI, TextSize, x, y);

        private static void DrawBox(Window w, Rectangle rect, string text, bool focused)
        {
            w.DrawRectangle(Color.Black, rect);
            if (focused && SplashKit.ReadingText())
            {
                SplashKit.DrawCollectedText(Color.Black, AppFonts.UI, InputSize, SplashKit.OptionDefaults());
                w.DrawRectangle(Color.RGBAColor(30,144,255,255), rect);
            }
            else
            {
                w.DrawText(text ?? string.Empty, Color.Black, AppFonts.UI, InputSize, rect.X + 6, rect.Y + 6);
            }
        }

        private static void DrawButton(Window w, Rectangle rect, string label, Color fill, Color textColor, int size = 18)
        {
            w.FillRectangle(fill, rect);
            w.DrawRectangle(Color.Black, rect);

            int labelWidth = AppFonts.Measure(label ?? string.Empty, size);
            double tx = rect.X + Math.Max(8, (rect.Width - labelWidth) / 2.0);
            double ty = rect.Y + (rect.Height - size) / 2.0 + 1;

            w.DrawText(label ?? string.Empty, textColor, AppFonts.UI, size, tx, ty);
        }

        private static bool PointInRect(Point2D p, Rectangle r) =>
            p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
