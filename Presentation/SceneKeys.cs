namespace PharmaChainLite.Presentation
{
    public enum SceneKey
    {
        Scan,
        Shipments,
        Sales,
        Ledger,
        Admin
    }

    public static class SceneMap
    {
        public static string Label(SceneKey key) => key switch
        {
            SceneKey.Scan      => "Scan",
            SceneKey.Shipments => "Shipments",
            SceneKey.Sales     => "Sales",
            SceneKey.Ledger    => "Ledger",
            SceneKey.Admin     => "Admin",
            _ => key.ToString()
        };

        public static SceneKey[] Ordered =>
            new[] { SceneKey.Scan, SceneKey.Shipments, SceneKey.Sales, SceneKey.Ledger, SceneKey.Admin };
    }
}
