using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    // Runtime panels never draw VisualElement.tooltip (only the Editor does), so this layer does: one glass card above
    // everything else, fed by the tooltip strings the controls already carry. "Title · body" splits into two lines.
    // Presentation only; the card never takes input.
    public sealed class TooltipLayer
    {
        const long DelayMs = 450;
        const float WarmSeconds = .3f;   // moving between controls soon after a tooltip closed shows the next at once
        public const string Separator = " · ";

        readonly VisualElement _root, _card;
        readonly Label _title, _body;
        readonly EventCallback<DetachFromPanelEvent> _ownerDetached;
        IVisualElementScheduledItem _pending;
        VisualElement _owner, _dismissed;
        float _hiddenAt = -10;
        bool _shown, _revealed;

        public VisualElement Card => _card;
        public VisualElement Owner => _shown ? _owner : null;

        public TooltipLayer(VisualElement root)
        {
            _root = root;
            _card = UIX.Div("rr-tooltip"); _card.pickingMode = PickingMode.Ignore;
            _title = UIX.Text("", "tooltip-title"); _title.pickingMode = PickingMode.Ignore; _card.Add(_title);
            _body = UIX.Text("", "tooltip-body"); _body.pickingMode = PickingMode.Ignore; _card.Add(_body);
            _card.style.display = DisplayStyle.None;
            root.Add(_card);
            _ownerDetached = _ => Hide();
            _card.RegisterCallback<GeometryChangedEvent>(_ => Place());
            root.RegisterCallback<PointerOverEvent>(e => Over(e.target as VisualElement), TrickleDown.TrickleDown);
            root.RegisterCallback<PointerOutEvent>(e => { if (_owner != null && !_owner.worldBound.Contains(e.position)) Hide(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerDownEvent>(_ => { _dismissed = _owner; Hide(); }, TrickleDown.TrickleDown);
            root.RegisterCallback<WheelEvent>(_ => Hide(), TrickleDown.TrickleDown);
        }

        /// <summary>Starts the tooltip for the element (or nearest ancestor) that carries one, as the pointer entering it would.</summary>
        public void Over(VisualElement target)
        {
            var owner = target;
            while (owner != null && owner != _root && string.IsNullOrEmpty(owner.tooltip)) owner = owner.parent;
            if (owner == _root) owner = null;
            if (owner == _owner) return;
            if (owner != _dismissed) _dismissed = null;   // a click dismisses until the pointer moves to something else
            Hide();
            if (owner == null || owner == _dismissed) return;
            _owner = owner;
            owner.RegisterCallback(_ownerDetached);
            bool warm = Time.unscaledTime - _hiddenAt < WarmSeconds;
            _pending = _root.schedule.Execute(Show).StartingIn(warm ? 0 : DelayMs);
        }

        public void Hide()
        {
            _pending?.Pause(); _pending = null;
            _owner?.UnregisterCallback(_ownerDetached);
            _owner = null;
            if (!_shown) return;
            _shown = false; _hiddenAt = Time.unscaledTime;
            _card.style.display = DisplayStyle.None;
        }

        void Show()
        {
            _pending = null;
            if (_owner == null || _owner.panel == null || string.IsNullOrEmpty(_owner.tooltip)) { Hide(); return; }
            string text = _owner.tooltip;
            int split = text.IndexOf(Separator, StringComparison.Ordinal);
            _title.text = split > 0 ? text.Substring(0, split) : "";
            _title.style.display = split > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _body.text = split > 0 ? text.Substring(split + Separator.Length) : text;
            _shown = true; _revealed = false;
            _card.style.opacity = 0;   // placed on its first layout pass, then revealed
            _card.style.display = DisplayStyle.Flex;
            _card.BringToFront();
            _card.schedule.Execute(Place).StartingIn(40);   // in case the first layout pass reports no change
        }

        // Below the control, centred; above it when there is no room; always inside the panel.
        void Place()
        {
            if (!_shown || _owner == null) return;
            var size = _card.layout.size;
            if (float.IsNaN(size.x) || size.x < 1) return;
            var area = _root.contentRect;
            var target = _root.WorldToLocal(_owner.worldBound);
            float x = Mathf.Clamp(target.center.x - size.x * .5f, area.xMin + 8, Mathf.Max(area.xMin + 8, area.xMax - size.x - 8));
            bool above = target.yMax + 8 + size.y > area.yMax - 8;
            float y = Mathf.Clamp(above ? target.yMin - 8 - size.y : target.yMax + 8, area.yMin + 8, Mathf.Max(area.yMin + 8, area.yMax - size.y - 8));
            if (Mathf.Abs(_card.layout.x - x) > .5f || Mathf.Abs(_card.layout.y - y) > .5f) { _card.style.left = x; _card.style.top = y; return; }
            if (_revealed) return;
            _revealed = true;
            float rise = above ? -4 : 4;
            UiMotion.Run(_card, "reveal", .14f, k =>
            {
                float q = 1 - (1 - k) * (1 - k);
                _card.style.opacity = q; _card.style.translate = new Translate(0, rise * (1 - q));
            });
        }
    }
}
