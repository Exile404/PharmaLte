using SplashKitSDK;

namespace PharmaChainLite.Presentation
{
    public interface IScene
    {
        void HandleInput();
        void Update();
        void Draw(Window window);
    }

    public sealed class SceneRouter
    {
        public IScene Current { get; private set; }

        public SceneRouter(IScene start)
        {
            Current = start;
        }

        public void GoTo(IScene next)
        {
            Current = next;
        }
    }
}
