using System;
using System.Globalization;
using SplashKitSDK;
using PharmaChainLite.Application.Shipments;
using PharmaChainLite.Application.Verification;
using PharmaChainLite.Domain;
using PharmaChainLite.Domain.Repositories;
using PharmaChainLite.Infrastructure.Repositories; // SqlitePackRepository.UpsertTokenOnly

namespace PharmaChainLite.Presentation
{
    public sealed class AdminScene : IScene
    {
        private readonly IPackRepository _packs;
        private readonly IShipmentRepository _shipments;
        private readonly EventingShipmentService _shipSvc;
        private readonly ITokenValidator _validator = new SimpleTokenValidator();

        private readonly Font _font;

        // Layout
        private readonly Rectangle _tokenRect  = SplashKit.RectangleFrom(200, 140, 320, 32);
        private readonly Rectangle _medRect    = SplashKit.RectangleFrom(200, 190, 320, 32);
        private readonly Rectangle _batchRect  = SplashKit.RectangleFrom(200, 240, 320, 32);
        private readonly Rectangle _expiryRect = SplashKit.RectangleFrom(200, 290, 200, 32);

        private readonly Rectangle _shipIdRect = SplashKit.RectangleFrom(200, 360, 220, 32);
        private readonly Rectangle _fromRect   = SplashKit.RectangleFrom(200, 410, 220, 32);
        private readonly Rectangle _toRect     = SplashKit.RectangleFrom(200, 460, 220, 32);

        private readonly Rectangle _createBtn  = SplashKit.RectangleFrom(200, 510, 180, 36);

        private enum Active { None, Token, Med, Batch, Expiry, ShipId, From, To }
        private Active _active = Active.None;

        private string _token = "";
        private string _med   = "";
        private string _batch = "";
        private string _expiryText = "";

        private string _shipId = "";
        private string _from   = "ManuCo";
        private string _to     = "DistCo";

        private string _msg = "Admin: Create medicine (pack) and add to a shipment.";

        // ---------- DEBUG ----------
        private readonly bool _debug = true;  // set false to hide HUD
        private string _dbgLastBuffer = "";
        private string _dbgNote = "";
        // ---------------------------

        public AdminScene(IPackRepository packs, IShipmentRepository shipments, EventingShipmentService shipSvc)
        {
            _packs = packs ?? throw new ArgumentNullException(nameof(packs));
            _shipments = shipments ?? throw new ArgumentNullException(nameof(shipments));
            _shipSvc = shipSvc ?? throw new ArgumentNullException(nameof(shipSvc));
            _font = SplashKit.LoadFont("ui", "arial.ttf");
        }

        public void HandleInput()
        {
            SplashKit.ProcessEvents();

            if (SplashKit.MouseClicked(MouseButton.LeftButton))
            {
                var p = SplashKit.MousePosition();

                if (PointInRect(p, _tokenRect))  { CommitBuffer("click->Token");  Begin(Active.Token); }
                else if (PointInRect(p, _medRect))    { CommitBuffer("click->Med");    Begin(Active.Med); }
                else if (PointInRect(p, _batchRect))  { CommitBuffer("click->Batch");  Begin(Active.Batch); }
                else if (PointInRect(p, _expiryRect)) { CommitBuffer("click->Expiry"); Begin(Active.Expiry); }
                else if (PointInRect(p, _shipIdRect)) { CommitBuffer("click->ShipId"); Begin(Active.ShipId); }
                else if (PointInRect(p, _fromRect))   { CommitBuffer("click->From");   Begin(Active.From); }
                else if (PointInRect(p, _toRect))     { CommitBuffer("click->To");     Begin(Active.To); }

                if (PointInRect(p, _createBtn))
                {
                    CommitBuffer("click->CreateBtn");        // <- ALWAYS capture latest text
                    DoCreate();
                }
            }

            if (SplashKit.KeyTyped(KeyCode.ReturnKey))
            {
                CommitBuffer("Enter");
                DoCreate();
            }

            if (SplashKit.KeyTyped(KeyCode.TabKey))
            {
                CommitBuffer("Tab");
                _active = Next(_active);
                Begin(_active);
            }

            if (SplashKit.KeyTyped(KeyCode.EscapeKey))
            {
                if (SplashKit.ReadingText()) SplashKit.EndReadingText();
                _active = Active.None;
            }
        }

        public void Update() { }

        public void Draw(Window w)
        {
            w.Clear(Color.White);
            w.DrawText("PharmaChain Lite - Admin", Color.Black, _font, 22, 20, 60);
            w.DrawText(_msg, Color.Black, _font, 16, 20, 86);

            DrawLabel(w, "Token", 80, 146);
            DrawLabel(w, "Medicine Name", 80, 196);
            DrawLabel(w, "Batch No", 80, 246);
            DrawLabel(w, "Expiry (yyyy-mm-dd)", 80, 296);

            DrawLabel(w, "Shipment Id (optional)", 80, 366);
            DrawLabel(w, "From", 80, 416);
            DrawLabel(w, "To", 80, 466);

            DrawBox(w, _tokenRect,  _token,  _active == Active.Token);
            DrawBox(w, _medRect,    _med,    _active == Active.Med);
            DrawBox(w, _batchRect,  _batch,  _active == Active.Batch);
            DrawBox(w, _expiryRect, _expiryText, _active == Active.Expiry);

            DrawBox(w, _shipIdRect, _shipId, _active == Active.ShipId);
            DrawBox(w, _fromRect,   _from,   _active == Active.From);
            DrawBox(w, _toRect,     _to,     _active == Active.To);

            w.FillRectangle(Color.RGBAColor(30,144,255,255), _createBtn);
            w.DrawRectangle(Color.Black, _createBtn);
            w.DrawText("Create & Add", Color.White, _font, 18, _createBtn.X + 26, _createBtn.Y + 7);

            // ---- DEBUG HUD (bottom-left) ----
            if (_debug)
            {
                var hud = $"DBG | active={_active} reading={SplashKit.ReadingText()} | token='{_token}' | lastBuf='{_dbgLastBuffer}' | note={_dbgNote}";
                w.DrawText(hud, Color.RGBAColor(90,90,90,255), _font, 12, 20, 560);
            }
        }

        // ---- helpers ---------------------------------------------------------

        private void Begin(Active field)
        {
            _active = field;
            var r = field switch
            {
                Active.Token  => _tokenRect,
                Active.Med    => _medRect,
                Active.Batch  => _batchRect,
                Active.Expiry => _expiryRect,
                Active.ShipId => _shipIdRect,
                Active.From   => _fromRect,
                Active.To     => _toRect,
                _ => _tokenRect
            };
            SplashKit.StartReadingText(r);
            if (_debug) Console.WriteLine($"[Begin] field={_active}");
        }

        /// <summary>
        /// Bullet-proof commit: we try to capture text whether SplashKit still says we are reading or not.
        /// We also log what we captured so we can see if/where it becomes empty.
        /// </summary>
        private void CommitBuffer(string reason)
        {
            bool wasReading = SplashKit.ReadingText();
            if (wasReading) SplashKit.EndReadingText();

            // Always attempt to read the buffer (even if SplashKit auto-ended it before our handler ran).
            string buf = (SplashKit.TextInput() ?? string.Empty);

            string trimmed = buf.Trim();
            _dbgLastBuffer = Abbrev(buf, 40);
            _dbgNote = $"{reason} | wasReading={wasReading} | bufLen={buf.Length}";

            // Only overwrite when we actually have a value OR we are editing the field that allows clearing.
            if (_active == Active.Token && trimmed.Length > 0) _token = trimmed.ToUpperInvariant();
            else if (_active == Active.Med  && trimmed.Length > 0) _med = trimmed;
            else if (_active == Active.Batch && trimmed.Length > 0) _batch = trimmed;
            else if (_active == Active.Expiry) _expiryText = trimmed;     // allow clear
            else if (_active == Active.ShipId) _shipId = trimmed;         // allow clear
            else if (_active == Active.From && trimmed.Length > 0) _from = trimmed;
            else if (_active == Active.To   && trimmed.Length > 0) _to   = trimmed;

            if (_debug) Console.WriteLine($"[Commit] reason={reason} active={_active} captured='{buf}' -> tokenNow='{_token}'");
            _active = Active.None;
        }

        private static string Abbrev(string s, int n)
        {
            if (s == null) return "";
            return s.Length <= n ? s : s.Substring(0, n) + "…";
        }

        private static Active Next(Active a) => a switch
        {
            Active.None   => Active.Token,
            Active.Token  => Active.Med,
            Active.Med    => Active.Batch,
            Active.Batch  => Active.Expiry,
            Active.Expiry => Active.ShipId,
            Active.ShipId => Active.From,
            Active.From   => Active.To,
            Active.To     => Active.Token,
            _ => Active.Token
        };

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

        // ---- action ----------------------------------------------------------

        private void DoCreate()
        {
            // Read the value we actually will use:
            var token = (_token ?? "").Trim().ToUpperInvariant();

            if (_debug) Console.WriteLine($"[DoCreate] using token='{token}'");

            if (token.Length == 0) { _msg = "Please enter a token."; return; }

            var err = _validator.Validate(token);
            if (err != null) { _msg = err; return; }

            if (string.IsNullOrWhiteSpace(_med))   { _msg = "Medicine name is required."; return; }
            if (string.IsNullOrWhiteSpace(_batch)) { _msg = "Batch number is required."; return; }

            if (!string.IsNullOrWhiteSpace(_expiryText))
            {
                if (!DateTime.TryParseExact(_expiryText, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out _))
                { _msg = "Expiry must be yyyy-mm-dd."; return; }
            }

            try
            {
                if (_packs is SqlitePackRepository sqlite)
                {
                    sqlite.UpsertTokenOnly(token, PackStatus.Produced);
                }
                else
                {
                    _msg = "Admin requires SQLite repository (SqlitePackRepository).";
                    return;
                }
            }
            catch (Exception ex)
            {
                _msg = ex.Message;
                if (_debug) Console.WriteLine($"[DoCreate] Upsert threw: {ex.Message}");
                return;
            }

            var sid = string.IsNullOrWhiteSpace(_shipId)
                ? $"SHP-{DateTime.UtcNow:yyyyMMddHHmmss}"
                : _shipId.Trim();

            var s = _shipments.FindById(sid);
            if (s == null) _shipments.Upsert(new Shipment(sid, _from.Trim(), _to.Trim()));

            try
            {
                _shipSvc.AddPack(sid, token);
                _msg = $"Pack '{token}' recorded ({_med}, {_batch}{(string.IsNullOrWhiteSpace(_expiryText) ? "" : $", exp {_expiryText}" )}) and added to shipment {sid}.";
            }
            catch (Exception ex)
            {
                _msg = ex.Message;
                if (_debug) Console.WriteLine($"[DoCreate] AddPack threw: {ex.Message}");
            }
        }

        private static bool PointInRect(Point2D p, Rectangle r)
            => p.X >= r.X && p.X <= r.X + r.Width && p.Y >= r.Y && p.Y <= r.Y + r.Height;
    }
}
