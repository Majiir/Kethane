
namespace Kethane.UserInterface
{
    public class WindowToggle
    {
        public bool IsVisible { get; private set; }

        public WindowToggle()
        {
            var tex = GameDatabase.Instance.GetTexture("Kethane/toolbar", false);
            var button = ApplicationLauncher.Instance.AddModApplication(onToggleOn, onToggleOff, null, null, null, null, ApplicationLauncher.AppScenes.MAPVIEW | ApplicationLauncher.AppScenes.TRACKSTATION, tex);
            button.SetTrue();
        }

        private void onToggleOn()
        {
            IsVisible = true;
        }

        private void onToggleOff()
        {
            IsVisible = false;
        }
    }
}
