using System;
using System.Globalization;
using SplashKitSDK;
using PharmaChainLite.Application.Sales;

namespace PharmaChainLite.Presentation
{
    public sealed class SalesScene : IScene
    {
        private readonly SalesService _sales;

        // Padded below the navbar
        private readonly Rectangle _tokenRect    = SplashKit.RectangleFrom(180, 150, 360, 32);
        private readonly Rectangle _retailerRect = SplashKit.RectangleFrom(180, 200, 360, 32);
        private readonly Rectangle _customerRect = SplashKit.RectangleFrom(180, 250, 360, 32);
        private readonly Rectangle _priceRect    = SplashKit.RectangleFrom(180, 300, 160, 32);
        private readonly Rectangle _sellBtnRect  = SplashKit.RectangleFrom(180, 350, 120, 36);

        private readonly Font _font;

        private string _token = "";
        private string _retailer = "";
        private string _customer = "";
        private string _priceText = "";
        private string _message = "Enter token, retailer, customer, optional price -> Sell";

        private enum ActiveField { None, Token, Retailer, Customer, Price }
        private ActiveField _active = ActiveField.None;

        public SalesScene(SalesService sales)
        {
            _sales = sales ?? throw new ArgumentNullException(nameof(sales));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                // Clicking into a field: commit current edit first, then start new edit
                if (PointInRect(p, _tokenRect))
                {
                    CommitActiveEdit();
                    BeginEdit(ActiveField.Token);
                }
                else if (PointInRect(p, _retailerRect))
                {
                    CommitActiveEdit();
                    BeginEdit(ActiveField.Retailer);
                }
                else if (PointInRect(p, _customerRect))
                {
                    CommitActiveEdit();
                    BeginEdit(ActiveField.Customer);
                }
                else if (PointInRect(p, _priceRect))
                {
                    CommitActiveEdit();
                    BeginEdit(ActiveField.Price);
                }

                // Sell button: commit then sell
                if (PointInRect(p, _sellBtnRect))
                {
                    CommitActiveEdit();
                    DoSell();
                }
            }

            // Press Enter = commit & Sell
            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                CommitActiveEdit();
                DoSell();
            }

            // Tab between fields while preserving text
            if (SplashKit.KeyTyped(KeyCode.TabKey))
            {
                CommitActiveEdit();
                _active = NextField(_active);
                BeginEdit(_active);
            }

            // Esc = finish editing without changing stored value
            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText(); // don't overwrite the stored value
                _active = ActiveField.None;
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);
            w.DrawText("PharmaChain Lite - Sales", Color.Black, _font, 22, 20, 60);
            w.DrawText(_message, Color.Black, _font, 16, 20, 86);

            DrawLabel(w, "Pack Token",  60, 156);
            DrawLabel(w, "Retailer",    60, 206);
            DrawLabel(w, "Customer",    60, 256);
            DrawLabel(w, "Price (opt.)",60, 306);

            DrawBox(w, _tokenRect,    _token,    _active == ActiveField.Token);
            DrawBox(w, _retailerRect, _retailer, _active == ActiveField.Retailer);
            DrawBox(w, _customerRect, _customer, _active == ActiveField.Customer);
            DrawBox(w, _priceRect,    _priceText,_active == ActiveField.Price);

            w.FillRectangle(Color.RGBAColor(30,144,255,255), _sellBtnRect);
            w.DrawRectangle(Color.Black, _sellBtnRect);
            w.DrawText("Sell", Color.White, _font, 18, _sellBtnRect.X + 42, _sellBtnRect.Y + 7);
        }

        // ----- Helpers -----

        private void BeginEdit(ActiveField field)
        {
            _active = field;
            Rectangle r = field switch
            {
                ActiveField.Token    => _tokenRect,
                ActiveField.Retailer => _retailerRect,
                ActiveField.Customer => _customerRect,
                ActiveField.Price    => _priceRect,
                _ => _tokenRect
            };
            SplashKit.StartReadingText(r); // start collecting text for the focused box
        }

        /// <summary>
        /// Finish SplashKit text entry (if active) and copy the collected value
        /// into the correct backing field. This follows the official pattern:
        /// EndReadingText() -> TextInput() -> store value. :contentReference[oaicite:1]{index=1}
        /// </summary>
        private void CommitActiveEdit()
        {
            if (!SplashKit.ReadingText()) return;

            // Finish the current edit session
            SplashKit.EndReadingText();

            // If the user didn't cancel, read the text they typed
            if (!SplashKit.TextEntryCancelled())
            {
                string text = (SplashKit.TextInput() ?? "").Trim();

                switch (_active)
                {
                    case ActiveField.Token:
                        if (text.Length > 0) _token = text.ToUpperInvariant();
                        break;
                    case ActiveField.Retailer:
                        if (text.Length > 0) _retailer = text;
                        break;
                    case ActiveField.Customer:
                        if (text.Length > 0) _customer = text;
                        break;
                    case ActiveField.Price:
                        _priceText = text; // allow empty to clear
                        break;
                }
            }

            _active = ActiveField.None;
        }

        private static ActiveField NextField(ActiveField f) => f switch
        {
            ActiveField.None     => ActiveField.Token,
            ActiveField.Token    => ActiveField.Retailer,
            ActiveField.Retailer => ActiveField.Customer,
            ActiveField.Customer => ActiveField.Price,
            ActiveField.Price    => ActiveField.Token,
            _ => ActiveField.Token
        };

        private void DoSell()
        {
            // Always validate against the stored strings (committed from the last edit)
            var token    = (_token ?? "").Trim();
            var retailer = (_retailer ?? "").Trim();
            var customer = (_customer ?? "").Trim();

            if (token.Length == 0)    { _message = "Enter a pack token."; return; }
            if (retailer.Length == 0) { _message = "Enter retailer name."; return; }
            if (customer.Length == 0) { _message = "Enter customer name."; return; }

            decimal? price = null;
            if (!string.IsNullOrWhiteSpace(_priceText))
            {
                if (decimal.TryParse(_priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var p) && p > 0)
                    price = p;
                else { _message = "Price must be a positive number."; return; }
            }

            try
            {
                _sales.SellPack(token, retailer, customer, price);
                _message = $"Sold {token} to {customer} @ {retailer}" + (price.HasValue ? $" for {price.Value:C}" : "");
                _priceText = "";
            }
            catch (Exception ex) { _message = ex.Message; }
        }

        private void DrawLabel(Window w, string text, double x, double y)
            => w.DrawText(text, Color.Black, _font, 16, x, y);

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

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
