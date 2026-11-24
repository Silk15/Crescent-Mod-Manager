using Avalonia;
using Avalonia.Controls;
using Crescent.Plugins;
using Crescent.UI.Controls.Tabs.Elements;

namespace Crescent.UI.Controls.Tabs
{
    public static class ModLayouts
    {
        public static readonly Dictionary<ModGridType, IModLayout> Layouts = typeof(ModLayouts).Assembly.GetTypes().Where(type => typeof(IModLayout).IsAssignableFrom(type) && !type.IsInterface && !type.IsAbstract).Select(type => (IModLayout)Activator.CreateInstance(type)!).ToDictionary(layout => layout.GridType, layout => layout);

        public static IModLayout Get(ModGridType gridType) => Layouts.TryGetValue(gridType, out IModLayout? layout) ? layout : Layouts[ModGridType.Rows];

        public static Border CreateLine(Thickness margin) => new()
        {
            Height = 1,
            Background = Colors.Separator,
            Margin = margin
        };

        public static void DetachControls(RenderedMod mod)
        {
            DetachControl(mod.thumbnailBorder);
            DetachControl(mod.modToggle);
            DetachControl(mod.nameBlock);
            DetachControl(mod.updateControl);
            DetachControl(mod.versionBlock);
            DetachControl(mod.manualButton);
            DetachControl(mod.nexusButton);
            DetachControl(mod.foldoutButton);
            DetachControl(mod.menuButton);
            DetachControl(mod.descriptionBlock);
            DetachControl(mod.releaseDateBlock);
            DetachControl(mod.updatedDateBlock);
        }

        private static void DetachControl(Control? control)
        {
            if (control == null) return;
            if (control.Parent is Panel panel) panel.Children.Remove(control);
            else if (control.Parent is Decorator decorator) decorator.Child = null;

            Grid.SetRow(control, 0);
            Grid.SetColumn(control, 0);
            Grid.SetRowSpan(control, 1);
            Grid.SetColumnSpan(control, 1);
        }
    }
}