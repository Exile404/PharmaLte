using SplashKitSDK;

using PharmaChainLite.Application.Events;
using PharmaChainLite.Application.Payments;
using PharmaChainLite.Application.Sales;
using PharmaChainLite.Application.Shipments;
using PharmaChainLite.Application.Verification;

using PharmaChainLite.Domain.Repositories;

using PharmaChainLite.Infrastructure.Repositories;

using PharmaChainLite.Presentation;

namespace PharmaLite
{
    public static class Program
    {
        public static void Main()
        {
            // Window
            var window = new Window("PharmaChain Lite", 1000, 600);

            // 1) Infrastructure
            IPackRepository packRepo           = new InMemoryPackRepository();
            IShipmentRepository shipmentRepo   = new InMemoryShipmentRepository();
            ILedgerRepository ledgerRepo       = new InMemoryLedgerRepository();

            // 2) Cross-cutting
            var bus    = new InProcessEventBus();
            var policy = new PerUnitPaymentPolicy();
            var paySvc = new PaymentService(
                bus,
                policy,
                ledgerRepo,
                shipmentRepo,
                packRepo,
                deliveryUnitPrice: 8.50m,
                defaultRetailPrice: 12.00m
            );

            // 3) Domain services
            var verifySvc    = new VerificationService(packRepo);
            var shipCore     = new ShipmentService(packRepo, shipmentRepo);
            var shipSvc      = new EventingShipmentService(shipCore, bus, packRepo);
            var salesSvc     = new SalesService(packRepo, bus, paySvc);

            // 4) Scenes
            var scanScene      = new ScanScene(verifySvc);
            var shipmentsScene = new ShipmentsScene(shipSvc);
            var salesScene     = new SalesScene(salesSvc);
            var ledgerScene    = new LedgerScene(ledgerRepo);

            var router = new SceneRouter(scanScene);
            var nav    = new NavBar();

            // 5) Main loop
            while (!window.CloseRequested)
            {
                router.Current.HandleInput();

                // Nav clicks after ProcessEvents()
                var next = nav.HandleInput();
                if (next.HasValue)
                {
                    switch (next.Value)
                    {
                        case SceneKey.Scan:      router.GoTo(scanScene); break;
                        case SceneKey.Shipments: router.GoTo(shipmentsScene); break;
                        case SceneKey.Sales:     router.GoTo(salesScene); break;
                        case SceneKey.Ledger:    router.GoTo(ledgerScene); break;
                    }
                }

                router.Current.Update();
                router.Current.Draw(window);

                // Draw nav bar on top
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
            return SceneKey.Scan;
        }
    }
}
