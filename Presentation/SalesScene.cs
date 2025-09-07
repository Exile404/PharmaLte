using System;
using SplashKitSDK;
using PharmaChainLite.Application.Sales;   

namespace PharmaChainLite.Presentation
{
    public sealed class SalesScene : IScene
    {
        private readonly SalesService _salesSvc; 
        private readonly Font _font;

        // Layout
        private readonly Rectangle _panelRect    = SplashKit.RectangleFrom(20, 120, 940, 420);

        private readonly Rectangle _tokenRect    = SplashKit.RectangleFrom(40, 200, 360, 32);
        private readonly Rectangle _retailerRect = SplashKit.RectangleFrom(40, 252, 360, 32);
        private readonly Rectangle _customerRect = SplashKit.RectangleFrom(40, 304, 360, 32);
        private readonly Rectangle _priceRect    = SplashKit.RectangleFrom(40, 356, 160, 32);

        private readonly Rectangle _recordBtn    = SplashKit.RectangleFrom(420, 200, 140, 36);
        private readonly Rectangle _clearBtn     = SplashKit.RectangleFrom(420, 252, 140, 36);

        private string _msg = "Enter sale details and Record.";
        private string _token = "";
        private string _retailer = "";
        private string _customer = "";
        private string _priceText = "";

        private enum Active { None, Token, Retailer, Customer, Price }
        private Active _active = Active.None;

  
        private readonly bool _debug = false;
        private string _dbgBuf = ""; private string _dbgNote = "";

        public SalesScene(SalesService salesSvc)          
        {
            _salesSvc = salesSvc ?? throw new ArgumentNullException(nameof(salesSvc));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                if (PointIn(p, _tokenRect))    { Commit("click->token");    Begin(Active.Token); }
                else if (PointIn(p, _retailerRect)) { Commit("click->ret"); Begin(Active.Retailer); }
                else if (PointIn(p, _customerRect)) { Commit("click->cust"); Begin(Active.Customer); }
                else if (PointIn(p, _priceRect))    { Commit("click->price");Begin(Active.Price);   }
                else if (PointIn(p, _recordBtn))    { Commit("click->record"); DoRecord(); }
                else if (PointIn(p, _clearBtn))     { Commit("click->clear");  ClearAll(); }
                else { Commit("click->bg"); }
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                Commit("Enter");
                DoRecord();
            }

            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                _active = Active.None;
            }

            if (SplashKit.KeyTyped(KeyCode.TabKey))
            {
                Commit("Tab");
                _active = _active switch
                {
                    Active.None      => Active.Token,
                    Active.Token     => Active.Retailer,
                    Active.Retailer  => Active.Customer,
                    Active.Customer  => Active.Price,
                    Active.Price     => Active.Token,
                    _                => Active.Token
                };
                Begin(_active);
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);

            // Title
            w.DrawText("PharmaChain Lite - Sales", Color.Black, _font, 22, 20, 60);

            // Message (subtle gray)
            w.DrawText(_msg, Color.RGBAColor(60,60,60,255), _font, 16, 20, 90);

            // Panel
            w.DrawRectangle(Color.Black, _panelRect);

            // Labels + inputs (ASCII only, no fancy glyphs)
            w.DrawText("Pack Token:", Color.Black, _font, 16, _tokenRect.X, _tokenRect.Y - 22);
            DrawBox(w, _tokenRect, _token, _active == Active.Token);

            w.DrawText("Retailer:", Color.Black, _font, 16, _retailerRect.X, _retailerRect.Y - 22);
            DrawBox(w, _retailerRect, _retailer, _active == Active.Retailer);

            w.DrawText("Customer:", Color.Black, _font, 16, _customerRect.X, _customerRect.Y - 22);
            DrawBox(w, _customerRect, _customer, _active == Active.Customer);

            w.DrawText("Price (opt.):", Color.Black, _font, 16, _priceRect.X, _priceRect.Y - 22);
            DrawBox(w, _priceRect, _priceText, _active == Active.Price);

            // Buttons
            w.FillRectangle(Color.RGBAColor(30,144,255,255), _recordBtn);
            w.DrawRectangle(Color.Black, _recordBtn);
            w.DrawText("Record", Color.White, _font, 16, _recordBtn.X + 36, _recordBtn.Y + 8);

            w.FillRectangle(Color.RGBAColor(200,200,200,255), _clearBtn);
            w.DrawRectangle(Color.Black, _clearBtn);
            w.DrawText("Clear", Color.Black, _font, 16, _clearBtn.X + 42, _clearBtn.Y + 8);

            if (_debug)
            {
                var hud = $"DBG active={_active} reading={SplashKit.ReadingText()} | buf='{_dbgBuf}' note={_dbgNote}";
                w.DrawText(hud, Color.RGBAColor(90,90,90,255), _font, 12, 20, 560);
            }
        }

        // ---- helpers --------------------------------------------------------

        private void DrawBox(Window w, Rectangle r, string text, bool focused)
        {
            w.DrawRectangle(Color.Black, r);
            if (focused && SplashKit.ReadingText())
            {
                SplashKit.DrawCollectedText(Color.Black, _font, 18, SplashKit.OptionDefaults());
                w.DrawRectangle(Color.RGBAColor(30,144,255,255), r);
            }
            else
            {
                w.DrawText(text ?? "", Color.Black, _font, 18, r.X + 6, r.Y + 6);
            }
        }

        private void Begin(Active field)
        {
            _active = field;
            var r = field switch {
                Active.Token    => _tokenRect,
                Active.Retailer => _retailerRect,
                Active.Customer => _customerRect,
                Active.Price    => _priceRect,
                _               => _tokenRect
            };
            SplashKit.StartReadingText(r);
        }

        /// Commit current SplashKit text buffer to the active field.
        private void Commit(string reason)
        {
            bool wasReading = SplashKit.ReadingText();
            if (wasReading) SplashKit.EndReadingText();

            string buf = SplashKit.TextInput() ?? "";
            string t = buf.Trim();

            if (_active == Active.Token)
            {
                if (t.Length > 0) _token = t.ToUpperInvariant();
            }
            else if (_active == Active.Retailer)
            {
                if (t.Length > 0) _retailer = t;
            }
            else if (_active == Active.Customer)
            {
                if (t.Length > 0) _customer = t;
            }
            else if (_active == Active.Price)
            {
                if (t.Length > 0) _priceText = t;
            }

            if (_debug) { _dbgBuf = buf; _dbgNote = $"{reason} | wasReading={wasReading}"; }
            _active = Active.None;
        }

        private void DoRecord()
        {
            var token = (_token ?? "").Trim().ToUpperInvariant();
            if (token.Length == 0) { _msg = "Pack token is required."; return; }

            // price?
            decimal? price = null;
            if (!string.IsNullOrWhiteSpace(_priceText))
            {
                if (decimal.TryParse(_priceText, out var p)) price = p;
                else { _msg = "Invalid price format."; return; }
            }

            try
            {
             
                _salesSvc.RecordSale(token, _retailer?.Trim(), _customer?.Trim(), price);
            

                _msg = $"Sale recorded for {token}.";
                _token = _retailer = _customer = _priceText = "";
            }
            catch (Exception ex)
            {
                _msg = ex.Message;
            }
        }

        private void ClearAll()
        {
            _token = _retailer = _customer = _priceText = "";
            _msg = "Cleared.";
        }

        private static bool PointIn(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
