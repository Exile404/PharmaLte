using System;
using SplashKitSDK;
using PharmaChainLite.Application.Verification;

namespace PharmaChainLite.Presentation
{
    public sealed class ScanScene : IScene
    {
        private readonly VerificationService _service;

        // Layout (padded below the 48px navbar)
        private readonly Rectangle _inputRect  = SplashKit.RectangleFrom(260, 116, 420, 36);
        private readonly Rectangle _buttonRect = SplashKit.RectangleFrom(260, 166, 120, 36);

        private readonly Font _font;
        private string _token = "";
        private string _message = "Enter or scan a token, then Verify.";
        private VerificationResult? _last;

        public ScanScene(VerificationService service)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _font = SplashKit.LoadFont("ui", "arial.ttf"); // ensure arial.ttf is copied to output
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                // Focus input rectangle to start text entry
                if (PointInRect(p, _inputRect))
                {
                    SplashKit.StartReadingText(_inputRect);
                }

                // Verify button click: end text entry first, then read the value and verify
                if (PointInRect(p, _buttonRect))
                {
                    if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                    CaptureText();
                    DoVerify();
                }
            }

            // Press Enter to finish typing & verify
            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                CaptureText();
                DoVerify();
            }

            // Optional: allow Esc to cancel current typing (doesn't verify)
            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                // If user cancels, don't overwrite _token; just stop reading
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);
            w.DrawText("PharmaChain Lite - Scan & Verify", Color.Black, _font, 22, 20, 60);

            // Label + Input Box
            w.DrawText("Token:", Color.Black, _font, 18, 200, 122);
            w.DrawRectangle(Color.Black, _inputRect);

            // While typing: show SplashKit's collected text; otherwise show the confirmed token
            if (SplashKit.ReadingText())
            {
                SplashKit.DrawCollectedText(Color.Black, _font, 18, SplashKit.OptionDefaults());
                // Focus highlight
                w.DrawRectangle(Color.RGBAColor(30, 144, 255, 255), _inputRect);
            }
            else
            {
                w.DrawText(_token, Color.Black, _font, 18, _inputRect.X + 6, _inputRect.Y + 6);
            }

            // Verify Button
            w.FillRectangle(Color.RGBAColor(30, 144, 255, 255), _buttonRect);
            w.DrawRectangle(Color.Black, _buttonRect);
            w.DrawText("Verify", Color.White, _font, 18, _buttonRect.X + 30, _buttonRect.Y + 7);

            // Messages
            w.DrawText(_message, Color.Black, _font, 18, 20, 230);

            if (_last != null && _last.Found)
            {
                w.DrawText($"Status: {_last.Status}", Color.Black, _font, 18, 20, 260);
                w.DrawText($"Duplicate: {_last.Duplicate}", Color.Black, _font, 18, 20, 290);
                w.DrawText($"Expired: {_last.Expired}", Color.Black, _font, 18, 20, 320);
            }
            // NOTE: no window.Refresh() here; Program.cs refreshes once per frame.
        }

        /// <summary>
        /// Finalize SplashKit text entry (if active) and copy the value into _token.
        /// </summary>
        private void CaptureText()
        {
            // If reading is still active, the caller should have already called EndReadingText().
            // Only update the token if the entry wasn't cancelled.
            if (!SplashKit.TextEntryCancelled())
            {
                var entered = SplashKit.TextInput() ?? "";
                _token = entered.Trim();
            }
        }

        private void DoVerify()
        {
            var token = (_token ?? "").Trim();
            if (token.Length == 0)
            {
                _message = "Please enter a token.";
                _last = null;
                return;
            }

            var result = _service.Verify(token);
            _last = result;
            _message = result.Message;
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
