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
        private readonly EventingShipmentService _service;

        // Layout: padded below the navbar and title
        private const double TopPad = 64;     // navbar space
        private const double TitleY = TopPad; // title y
        private const double MsgY   = TitleY + 26;

        private readonly Rectangle _listRect       = SplashKit.RectangleFrom(20, 140, 280, 430);
        private readonly Rectangle _detailRect     = SplashKit.RectangleFrom(320, 140, 660, 430);
        private readonly Rectangle _addTokenRect   = SplashKit.RectangleFrom(340, 420, 320, 32);
        private readonly Rectangle _addTokenBtn    = SplashKit.RectangleFrom(670, 418, 120, 36);
        private readonly Rectangle _toInTransitBtn = SplashKit.RectangleFrom(340, 470, 180, 36);
        private readonly Rectangle _toDeliveredBtn = SplashKit.RectangleFrom(530, 470, 180, 36);

        private Font _font;
        private List<Shipment> _shipments = new();
        private int _selectedIndex = -1;
        private string _message = "Select a shipment, add packs, and update status.";
        private string _pendingToken = "";

        public ShipmentsScene(EventingShipmentService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
            RefreshShipments();
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                // List selection
                if (PointInRect(p, _listRect))
                {
                    int row = (int)((p.Y - (_listRect.Y + 10)) / 28.0);
                    if (row >= 0 && row < _shipments.Count) _selectedIndex = row;
                }

                // Start typing in token box
                if (PointInRect(p, _addTokenRect)) SplashKit.StartReadingText(_addTokenRect);

                // Add pack button
                if (PointInRect(p, _addTokenBtn))
                {
                    if (SplashKit.ReadingText()) SplashKit.EndReadingText(); // <-- finish edit
                    CaptureText();
                    DoAddPack();
                }

                // Transitions
                if (PointInRect(p, _toInTransitBtn)) DoTransition(ShipmentStatus.InTransit);
                if (PointInRect(p, _toDeliveredBtn)) DoTransition(ShipmentStatus.Delivered);
            }

            // Enter key = finish typing and Add
            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText(); // <-- finish edit
                CaptureText();
                DoAddPack();
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);

            // Title + helper message
            w.DrawText("PharmaChain Lite - Shipments", Color.Black, _font, 22, 20, TitleY);
            w.DrawText(_message, Color.Black, _font, 16, 20, MsgY);

            // Left: Shipment list
            w.DrawRectangle(Color.Black, _listRect);
            w.DrawText("Shipments", Color.Black, _font, 18, _listRect.X + 10, _listRect.Y - 22);

            double y = _listRect.Y + 10;
            for (int i = 0; i < _shipments.Count; i++)
            {
                var s = _shipments[i];
                var rowRect = SplashKit.RectangleFrom(_listRect.X + 4, y - 4, _listRect.Width - 8, 24);

                if (i == _selectedIndex)
                    w.FillRectangle(Color.RGBAColor(235, 245, 255, 255), rowRect);

                w.DrawText($"{s.Id}  [{s.Status}]", Color.Black, _font, 16, _listRect.X + 10, y);
                y += 28;
            }

            // Right: Details for selected shipment
            w.DrawRectangle(Color.Black, _detailRect);
            w.DrawText("Details", Color.Black, _font, 18, _detailRect.X + 10, _detailRect.Y - 22);

            if (_selectedIndex >= 0 && _selectedIndex < _shipments.Count)
            {
                var s = _shipments[_selectedIndex];

                double x0 = _detailRect.X + 12;
                double y0 = _detailRect.Y + 12;

                w.DrawText($"Id: {s.Id}", Color.Black, _font, 18, x0, y0); y0 += 28;
                w.DrawText($"From: {s.FromParty}", Color.Black, _font, 18, x0, y0); y0 += 28;
                w.DrawText($"To: {s.ToParty}", Color.Black, _font, 18, x0, y0); y0 += 28;
                w.DrawText($"Status: {s.Status}", Color.Black, _font, 18, x0, y0); y0 += 28;
                if (s.DeliveredAt.HasValue)
                {
                    w.DrawText($"DeliveredAt: {s.DeliveredAt.Value:u}", Color.Black, _font, 16, x0, y0); y0 += 24;
                }

                // Pack list
                y0 += 8;
                w.DrawText("Packs:", Color.Black, _font, 18, x0, y0); y0 += 26;

                foreach (var t in s.PackTokens)
                {
                    w.DrawText($"- {t}", Color.Black, _font, 16, x0 + 16, y0);
                    y0 += 22;
                    if (y0 > _detailRect.Y + _detailRect.Height - 150) break; // keep within panel
                }

                // Add Pack input (label above the box)
                double labelY = _addTokenRect.Y - 22;
                double labelX = _detailRect.X + 12;
                w.DrawText("Add Pack Token:", Color.Black, _font, 18, labelX, labelY);

                w.DrawRectangle(Color.Black, _addTokenRect);
                if (SplashKit.ReadingText())
                {
                    SplashKit.DrawCollectedText(Color.Black, _font, 18, SplashKit.OptionDefaults());
                    w.DrawRectangle(Color.RGBAColor(30, 144, 255, 255), _addTokenRect);
                }
                else
                {
                    w.DrawText(_pendingToken, Color.Black, _font, 18, _addTokenRect.X + 6, _addTokenRect.Y + 6);
                }

                // Buttons
                w.FillRectangle(Color.RGBAColor(30, 144, 255, 255), _addTokenBtn);
                w.DrawRectangle(Color.Black, _addTokenBtn);
                w.DrawText("Add", Color.White, _font, 18, _addTokenBtn.X + 40, _addTokenBtn.Y + 7);

                w.FillRectangle(Color.RGBAColor(60, 179, 113, 255), _toInTransitBtn);
                w.DrawRectangle(Color.Black, _toInTransitBtn);
                w.DrawText("Set InTransit", Color.White, _font, 16, _toInTransitBtn.X + 20, _toInTransitBtn.Y + 8);

                w.FillRectangle(Color.RGBAColor(255, 140, 0, 255), _toDeliveredBtn);
                w.DrawRectangle(Color.Black, _toDeliveredBtn);
                w.DrawText("Set Delivered", Color.White, _font, 16, _toDeliveredBtn.X + 18, _toDeliveredBtn.Y + 8);
            }
        }

        private void RefreshShipments()
        {
            _shipments = new List<Shipment>(_service.List());
            if (_selectedIndex >= _shipments.Count) _selectedIndex = _shipments.Count - 1;
            if (_selectedIndex < 0 && _shipments.Count > 0) _selectedIndex = 0;
        }

        /// <summary>Finalize text entry (if any) and copy into _pendingToken.</summary>
        private void CaptureText()
        {
            if (!SplashKit.TextEntryCancelled())
            {
                var entered = SplashKit.TextInput() ?? "";
                _pendingToken = entered.Trim();
            }
        }

        private void DoAddPack()
        {
            if (_selectedIndex < 0 || _selectedIndex >= _shipments.Count)
            {
                _message = "Select a shipment first.";
                return;
            }

            var token = (_pendingToken ?? "").Trim().ToUpperInvariant();
            if (token.Length == 0)
            {
                _message = "Enter a pack token to add.";
                return;
            }

            var s = _shipments[_selectedIndex];

            // Duplicate check (case-insensitive) for clearer UX
            if (s.PackTokens.Any(t => string.Equals(t, token, StringComparison.OrdinalIgnoreCase)))
            {
                _message = $"'{token}' is already in shipment {s.Id} (ignored).";
                return;
            }

            try
            {
                _service.AddPack(s.Id, token);
                _message = $"Added pack '{token}' to {s.Id}.";
                _pendingToken = "";
                RefreshShipments();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
            }
        }

        private void DoTransition(ShipmentStatus next)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _shipments.Count)
            {
                _message = "Select a shipment first.";
                return;
            }
            try
            {
                var s = _shipments[_selectedIndex];
                _service.Transition(s.Id, next);
                _message = $"Shipment {s.Id} set to {next}.";
                RefreshShipments();
            }
            catch (Exception ex)
            {
                _message = ex.Message;
            }
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
