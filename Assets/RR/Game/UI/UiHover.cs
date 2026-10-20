using System.Runtime.CompilerServices;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Hover as a class rather than :hover, so presentation reviews can drive it with synthetic pointer events.
    // Styles target both, so a real pointer and a review shot look the same.
    public static class UiHover
    {
        public const string Class = "hovered";
        static readonly ConditionalWeakTable<VisualElement, object> Tracked = new ConditionalWeakTable<VisualElement, object>();

        public static void Track(VisualElement e)
        {
            if (Tracked.TryGetValue(e, out _)) return;
            Tracked.Add(e, null);
            e.RegisterCallback<PointerEnterEvent>(_ => e.EnableInClassList(Class, e.enabledInHierarchy));
            e.RegisterCallback<PointerLeaveEvent>(_ => e.RemoveFromClassList(Class));
            e.RegisterCallback<DetachFromPanelEvent>(_ => e.RemoveFromClassList(Class));
        }
    }
}
