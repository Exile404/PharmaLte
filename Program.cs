using SplashKitSDK;

using PharmaChainLite.Application.Events;
using PharmaChainLite.Application.Payments;
using PharmaChainLite.Application.Sales;
using PharmaChainLite.Application.Shipments;
using PharmaChainLite.Application.Verification;

using PharmaChainLite.Domain.Repositories;

using PharmaChainLite.Infrastructure.Data;
using PharmaChainLite.Infrastructure.Repositories;

using PharmaChainLite.Presentation;

namespace PharmaLite
{
    public static class Program
    {
        public static void Main()
        {
            var window = new Window("PharmaChain Lite", 1000, 600);

            // --- Persistence: SQLite ---
            var db = new SqliteDb("pharmachain.db");
            db.EnsureCreated();

            IPackRepository packRepo         = new SqlitePackRepository(db);
            IShipmentRepository shipRepo     = new SqliteShipmentRepository(db);
            ILedgerRepository ledgerRepo     = new SqliteLedgerRepository(db);

            // --- Cross-cutting ---
            var bus    = new InProcessEventBus();
            var policy = new PerUnitPaymentPolicy();
            var paySvc = new PaymentService(bus, policy, ledgerRepo, shipRepo, packRepo, 8.50m, 12.00m);

            // --- Domain services ---
            var verifySvc = new VerificationService(packRepo);
            var shipCore  = new ShipmentService(packRepo, shipRepo);
            var shipSvc   = new EventingShipmentService(shipCore, bus, packRepo);
            var salesSvc  = new SalesService(packRepo, bus, paySvc);

            // --- Scenes ---
            var scanScene      = new ScanScene(verifySvc);
            var shipmentsScene = new ShipmentsScene(shipSvc);
            var salesScene     = new SalesScene(salesSvc);
            var ledgerScene    = new LedgerScene(ledgerRepo);
            var adminScene     = new AdminScene(packRepo, shipRepo, shipSvc);

            var router = new SceneRouter(scanScene);
            var nav    = new NavBar();

            while (!window.CloseRequested)
            {
                router.Current.HandleInput();

                var next = nav.HandleInput();
                if (next.HasValue)
                {
                    switch (next.Value)
                    {
                        case SceneKey.Scan:      router.GoTo(scanScene); break;
                        case SceneKey.Shipments: router.GoTo(shipmentsScene); break;
                        case SceneKey.Sales:     router.GoTo(salesScene); break;
                        case SceneKey.Ledger:    router.GoTo(ledgerScene); break;
                        case SceneKey.Admin:     router.GoTo(adminScene); break;
                    }
                }

                router.Current.Update();
                router.Current.Draw(window);

                nav.Draw(window, ActiveKey(router.Current));
                window.Refresh(60);
            }

            paySvc.Dispose();
            SplashKit.CloseAllWindows();
        }

        private static SceneKey ActiveKey(IScene scene)
        {
            if (scene is ShipmentsScene) return SceneKey.Shipments;
            if (scene is SalesScene)     return SceneKey.Sales;
            if (scene is LedgerScene)    return SceneKey.Ledger;
            if (scene is AdminScene)     return SceneKey.Admin;
            return SceneKey.Scan;
        }
    }
}
