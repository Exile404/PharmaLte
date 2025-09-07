namespace PharmaChainLite.Presentation
{
    public enum SceneKey
    {
        Scan,
        Shipments,
        Sales,
        Ledger
    }

    public static class SceneMap
    {
        public static string Label(SceneKey key) => key switch
        {
            SceneKey.Scan => "Scan",
            SceneKey.Shipments => "Shipments",
            SceneKey.Sales => "Sales",
            SceneKey.Ledger => "Ledger",
            _ => key.ToString()
        };

        public static SceneKey[] Ordered =>
            new[] { SceneKey.Scan, SceneKey.Shipments, SceneKey.Sales, SceneKey.Ledger };
    }
}
