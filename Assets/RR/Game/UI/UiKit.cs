using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace RR.Game
{
    /// <summary>Fleet Operations UI primitives. Themes change identity, never simulation or semantic states.</summary>
    public static class UiKit
    {
        public static readonly Color Ink = new Color32(21, 43, 66, 255);
        static readonly CustomStyleProperty<Color> AccentProperty = new CustomStyleProperty<Color>("--icon-accent");

        public static void Install(VisualElement root, string faction)
        {
            root.AddToClassList("rr-ui");
            root.styleSheets.Add(Resources.Load<StyleSheet>("RR/Prismatic"));
            var theme = Resources.Load<StyleSheet>("RR/UIThemes/" + faction);
            if (theme != null) root.styleSheets.Add(theme);
            root.styleSheets.Add(Resources.Load<StyleSheet>("RR/CommandGlass"));
            var slice = Resources.Load<StyleSheet>("RR/SlicePanels");   // the vertical slice's own panels, in this system
            if (slice != null) root.styleSheets.Add(slice);
            root.AddToClassList("command-glass");
            root.AddToClassList("cinematic-ui");
            root.EnableInClassList("reduced-motion", Settings.ReducedMotion);
            root.RegisterCallback<GeometryChangedEvent>(e =>
            {
                root.EnableInClassList("compact", e.newRect.width < 1550);
            });
        }

        public static Icon AccentIcon(IconKind kind, float size = 18)
        {
            var icon = new Icon(kind, new Color32(134, 201, 245, 255), size);
            icon.AddToClassList("accent-icon");
            icon.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if (e.customStyle.TryGetValue(AccentProperty, out var tint)) icon.Set(kind, tint);
            });
            return icon;
        }

        public static Icon ContentIcon(IconKind kind, float size = 18)
        {
            var icon = new Icon(kind, new Color32(229,241,252,255), size);
            icon.AddToClassList("content-icon");
            icon.RegisterCallback<CustomStyleResolvedEvent>(e =>
            {
                if(e.customStyle.TryGetValue(new CustomStyleProperty<Color>("--content-color"),out var tint)) icon.Set(kind,tint);
            });
            return icon;
        }

        public static VisualElement Crest(string faction)
        {
            var tex = Resources.Load<Texture2D>("RR/Emblems/" + faction);
            if (tex == null) return AccentIcon(IconKind.Crown, 34);
            var crest = UIX.Div("faction-crest");
            crest.style.backgroundImage = new StyleBackground(tex);
            crest.pickingMode = PickingMode.Ignore;
            return crest;
        }

        public static VisualElement Ship(string id, bool small = false)
        {
            var frame = UIX.Div("ship-portrait");
            if (small) frame.AddToClassList("ship-portrait-small");
            var tex = id != null ? Resources.Load<Texture2D>("RR/UI/Ships/" + id) : null;
            CinematicUi.Portrait(frame);
            if (tex != null)
            {
                var image=new Image { image=tex,scaleMode=ScaleMode.ScaleToFit,pickingMode=PickingMode.Ignore };
                image.AddToClassList("ship-portrait-image"); frame.Add(image);
            }
            else frame.Add(AccentIcon(IconKind.Fleet, small ? 22 : 42));
            frame.pickingMode = PickingMode.Ignore;
            return frame;
        }

        public static Button InspectShip(string id, bool small = false)
        {
            var button = UIX.Btn("", () => GameRoot.I.InspectUnit(id), "inspect-portrait");
            button.name = "inspect-" + id;
            button.tooltip = "Inspect " + id.Replace('_',' ') + " · rotate, zoom and preview animations";
            button.Add(Ship(id, small));
            var hint=UIX.Text("INSPECT", "inspect-hint");hint.pickingMode=PickingMode.Ignore;button.Add(hint);
            button.RegisterCallback<ClickEvent>(e => e.StopPropagation());
            button.RegisterCallback<PointerDownEvent>(e => e.StopPropagation());
            button.RegisterCallback<PointerUpEvent>(e => e.StopPropagation());
            button.RegisterCallback<NavigationSubmitEvent>(e => e.StopPropagation());
            return button;
        }

        public static VisualElement Section(string index, string title)
        {
            var row = UIX.Div("kit-section", "row");
            row.Add(UIX.Text(index, "section-index"));
            row.Add(UIX.Text(title.ToUpperInvariant(), "h-section"));
            var rule = UIX.Div("section-rule"); row.Add(rule);
            return row;
        }

        public static VisualElement Tabs(string selected, Action<string> choose, params string[] labels)
        {
            var row = UIX.Div("kit-tabs", "row");
            foreach (var label in labels)
            {
                var tab = UIX.Btn("", () => choose(label), "kit-tab");
                tab.Add(UIX.Text(label, "tab-label"));
                tab.name = "tab-" + label.ToLowerInvariant();
                if (label == labels[labels.Length - 1]) tab.AddToClassList("kit-tab-last");
                tab.EnableInClassList("kit-tab-selected", label == selected);
                row.Add(tab);
            }
            return row;
        }

        // Read-only telemetry: strong values first, units and operational state second.
        public static VisualElement Summary(string state, string leftLabel, string leftValue, string rightLabel, string rightValue, bool warning = false)
        {
            var panel = UIX.Div("dossier-summary");
            var status = UIX.Text(state.ToUpperInvariant(), "summary-status");
            if (warning) status.AddToClassList("summary-warning"); panel.Add(status);
            var row = UIX.Div("row", "summary-metrics"); panel.Add(row);
            void Metric(string label, string value)
            {
                var cell = UIX.Div("summary-cell"); if (row.childCount > 0) cell.AddToClassList("summary-cell-last"); cell.Add(UIX.Text(value, "summary-value"));
                cell.Add(UIX.Text(label.ToUpperInvariant(), "summary-label")); row.Add(cell);
            }
            Metric(leftLabel, leftValue); Metric(rightLabel, rightValue); return panel;
        }

        public static VisualElement Data(string label, string value)
        {
            var row = UIX.Div("row", "data-row");
            if(label == "Population") row.AddToClassList("data-population");
            var kind = label == "Sovereign" || label == "Orbit held by" ? IconKind.Flag : label == "Population" ? IconKind.Population : label == "Development" ? IconKind.Development : label == "Shipyard" ? IconKind.Shipyard : label == "Crystal Foundries" ? IconKind.Foundry : label == "Worlds" ? IconKind.Planet : IconKind.Fleet;
            var icon = new Icon(kind, new Color32(80,113,143,255), 19); icon.AddToClassList("data-icon"); row.Add(icon);
            row.Add(UIX.Text(label, "small")); row.Add(UIX.Div("spacer"));
            row.Add(UIX.Text(value, "body", "data-value"));
            return row;
        }

        public static VisualElement Development(int level)
        {
            var row = Data("Development", level + " / 5");
            var meter = UIX.Div("row", "development-meter"); meter.pickingMode = PickingMode.Ignore;
            meter.tooltip = level + " of 5 development levels";
            for (int i=0;i<5;i++) { var seg = UIX.Div("development-segment"); seg.EnableInClassList("development-segment-on",i<level); meter.Add(seg); }
            row.Insert(row.childCount-1, meter); return row;
        }

        public static VisualElement Production(int energy, int materials, int credits, bool blockaded)
        {
            var row = UIX.Div("row", "yield-grid");
            void Add(IconKind icon, string label, int value, bool last)
            {
                var cell = UIX.Div("yield-cell"); if(last) cell.AddToClassList("yield-cell-last");
                var head = UIX.Div("row"); head.Add(new Icon(icon, icon == IconKind.Materials ? new Color32(149,203,241,255) : new Color32(245,211,139,255), 19)); head.Add(UIX.Text(label,"small")); cell.Add(head);
                var number = UIX.Text(blockaded ? "0" : "+"+value, "yield-number"); if(blockaded) number.AddToClassList("text-danger"); cell.Add(number);
                cell.tooltip = blockaded ? "Production suspended: hostile orbital control" : label+" produced per day"; row.Add(cell);
            }
            Add(IconKind.Energy,"Energy",energy,false); Add(IconKind.Materials,"Materials",materials,false); Add(IconKind.Credits,"Credits",credits,true);
            return row;
        }

        public static VisualElement Orbit(bool blockaded)
        {
            var row = UIX.Div("row", "orbit-status"); row.Add(AccentIcon(blockaded ? IconKind.Warning : IconKind.Shield, 27));
            var words=UIX.Div("orbit-copy"); var title=UIX.Text(blockaded ? "Orbit contested." : "Orbit secure.","orbit-title");
            if(blockaded) title.AddToClassList("text-danger"); words.Add(title);
            words.Add(UIX.Text(blockaded ? "Income and construction are suspended until orbit is retaken." : "Local production and fleet operations are available to this world's sovereign.","small")); row.Add(words); return row;
        }

        public static VisualElement Cost(RR.Strategy.Resources cost)
        {
            var row = UIX.Div("row", "cost-row");
            void Add(IconKind kind, int value, string name)
            {
                if (value <= 0) return;
                var group = UIX.Div("row", "cost-value"); group.tooltip = name;
                group.Add(ContentIcon(kind, 16)); group.Add(UIX.Text(value.ToString(), "num")); row.Add(group);
            }
            Add(IconKind.Energy, cost.Energy, "Energy"); Add(IconKind.Materials, cost.Materials, "Materials");
            Add(IconKind.Credits, cost.Credits, "Credits"); Add(IconKind.Influence, cost.Influence, "Influence");
            if(row.childCount>0) row[row.childCount-1].AddToClassList("cost-value-last");
            return row;
        }
    }
}
