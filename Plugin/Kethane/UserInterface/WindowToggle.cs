using Kethane.Toolbar;

namespace Kethane.UserInterface
{
    public class WindowToggle
    {
        public bool IsVisible { get; private set; }

        public WindowToggle()
        {
            IsVisible = true;

            var button = ToolbarManager.Instance.add("Kethane", "toggle");
            button.TexturePath = "Kethane/toolbar";
            refresh(button);
            button.Visibility = new MapViewVisibility();
            button.OnClick += e =>
            {
                IsVisible = !IsVisible;
                refresh(button);
            };
        }

        private void refresh(IButton button)
        {
            button.ToolTip = (IsVisible ? "Hide" : "Show") + " Kethane controls";
        }

        private class MapViewVisibility : IVisibility
        {
            public bool Visible
            {
                get { return (HighLogic.LoadedScene == GameScenes.FLIGHT || HighLogic.LoadedScene == GameScenes.TRACKSTATION) && MapView.MapIsEnabled; }
            }
        }
    }
}
