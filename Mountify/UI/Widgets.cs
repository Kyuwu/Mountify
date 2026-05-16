using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Colors;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using System;
using System.Numerics;

namespace Mountify.UI;

internal readonly record struct ActionButtonStyle
{
    public ActionButtonStyle() { }
    public float Height          { get; init; } = 30f;
    public float PaddingX        { get; init; } = 11f;
    public float ContentGap      { get; init; } = 9f;
    public float IconBoxSize     { get; init; } = 16f;
    public float Rounding        { get; init; } = 6f;
    public float BorderAlpha     { get; init; } = 0.32f;
    public float FillAlpha       { get; init; } = 0.06f;
    public float HoverFillAlpha  { get; init; } = 0.11f;
    public float ActiveFillAlpha { get; init; } = 0.16f;
    public float TextAlpha       { get; init; } = 0.95f;
    public float TextHoverAlpha  { get; init; } = 0.99f;
}

internal static class ActionButton
{
    public static float CalcWidth(FontAwesomeIcon icon, string label, ActionButtonStyle? styleOverride = null)
    {
        var style = styleOverride ?? new ActionButtonStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var height = MathF.Max(style.Height * scale, ImGui.GetFrameHeight());
        var iconBoxSize = MathF.Max(style.IconBoxSize * scale, height - (8f * scale));
        var contentGap = string.IsNullOrEmpty(label) ? 0f : style.ContentGap * scale;
        var textWidth = string.IsNullOrEmpty(label) ? 0f : ImGui.CalcTextSize(label).X;
        return iconBoxSize + contentGap + textWidth + (style.PaddingX * 2f * scale);
    }

    public static bool Draw(
        string id,
        FontAwesomeIcon icon,
        string label,
        Vector4 accentColor,
        float? width = null,
        bool center = false,
        ActionButtonStyle? styleOverride = null)
    {
        var style = styleOverride ?? new ActionButtonStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var height = MathF.Max(style.Height * scale, ImGui.GetFrameHeight());
        var paddingX = style.PaddingX * scale;
        var contentGap = string.IsNullOrEmpty(label) ? 0f : style.ContentGap * scale;
        var iconBoxSize = MathF.Max(style.IconBoxSize * scale, height - (8f * scale));
        var iconText = icon.ToIconString();

        Vector2 iconSize;
        using (ImRaii.PushFont(UiBuilder.IconFont))
            iconSize = ImGui.CalcTextSize(iconText);

        var textSize = ImGui.CalcTextSize(label);
        var contentWidth = iconBoxSize + (string.IsNullOrEmpty(label) ? 0f : contentGap + textSize.X);
        var buttonWidth = width ?? CalcWidth(icon, label, style);
        var size = new Vector2(buttonWidth, height);
        var pressed = ImGui.InvisibleButton(id, size);
        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var active = ImGui.IsItemActive();
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        var rounding = style.Rounding * scale;

        var baseFill = new Vector4(0.09f, 0.09f, 0.12f, 0.98f);
        var borderColor = accentColor with { W = hovered ? 0.52f : style.BorderAlpha };
        var accentFill = accentColor with
        {
            W = active ? style.ActiveFillAlpha : hovered ? style.HoverFillAlpha : style.FillAlpha
        };

        dl.AddRectFilled(min, max, ImGui.GetColorU32(baseFill), rounding);
        dl.AddRectFilled(min, max, ImGui.GetColorU32(accentFill), rounding);
        dl.AddRect(min, max, ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.None, 1f * scale);

        var contentStartX = center
            ? min.X + ((buttonWidth - contentWidth) * 0.5f)
            : min.X + paddingX;
        var iconMin = new Vector2(contentStartX, min.Y + ((height - iconBoxSize) * 0.5f));
        var iconColor = accentColor with { W = hovered ? 0.98f : 0.88f };

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            var iconPos = new Vector2(
                iconMin.X + ((iconBoxSize - iconSize.X) * 0.5f),
                iconMin.Y + ((iconBoxSize - iconSize.Y) * 0.5f));
            dl.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), iconPos, ImGui.GetColorU32(iconColor), iconText);
        }

        if (!string.IsNullOrEmpty(label))
        {
            var textPos = new Vector2(
                iconMin.X + iconBoxSize + contentGap,
                min.Y + ((height - textSize.Y) * 0.5f));
            var textColor = ImGuiColors.DalamudWhite with { W = hovered ? style.TextHoverAlpha : style.TextAlpha };
            dl.AddText(textPos, ImGui.GetColorU32(textColor), label);
        }

        return pressed;
    }
}
internal readonly record struct StatusPillStyle
{
    public StatusPillStyle() { }
    public float Height        { get; init; } = 24f;
    public float PaddingX      { get; init; } = 10f;
    public float DotRadius     { get; init; } = 3f;
    public float DotGap        { get; init; } = 8f;
    public float IconGap       { get; init; } = 8f;
    public float Rounding      { get; init; } = 999f;
    public float BorderAlpha   { get; init; } = 0.34f;
    public float FillAlpha     { get; init; } = 0.11f;
    public float HoverFillAlpha { get; init; } = 0.17f;
}

internal static class StatusPill
{
    public static Vector2 CalcSize(string label, FontAwesomeIcon icon = FontAwesomeIcon.None, StatusPillStyle? styleOverride = null)
    {
        var style = styleOverride ?? new StatusPillStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var width = (style.PaddingX * 2f * scale) + ImGui.CalcTextSize(label).X;

        if (icon != FontAwesomeIcon.None)
        {
            using var f = ImRaii.PushFont(UiBuilder.IconFont);
            width += ImGui.CalcTextSize(icon.ToIconString()).X + (style.IconGap * scale);
        }
        else
        {
            width += ((style.DotRadius * 2f) + style.DotGap) * scale;
        }

        return new Vector2(width, style.Height * scale);
    }

    public static bool Draw(
        string id,
        string label,
        Vector4 accentColor,
        string? tooltip = null,
        FontAwesomeIcon icon = FontAwesomeIcon.None,
        bool active = true,
        bool interactive = false,
        StatusPillStyle? styleOverride = null)
    {
        var style = styleOverride ?? new StatusPillStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var size = CalcSize(label, icon, style);
        var clicked = false;

        if (interactive)
            clicked = ImGui.InvisibleButton(id, size);
        else
            ImGui.Dummy(size);

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        var rounding = style.Rounding * scale;

        var borderColor = active
            ? accentColor with { W = hovered ? 0.56f : style.BorderAlpha }
            : new Vector4(0.50f, 0.50f, 0.54f, hovered ? 0.40f : 0.24f);
        var fillColor = active
            ? accentColor with { W = hovered ? style.HoverFillAlpha : style.FillAlpha }
            : new Vector4(0.24f, 0.24f, 0.26f, hovered ? 0.16f : 0.10f);
        var textColor = active
            ? ImGuiColors.DalamudWhite with { W = hovered ? 0.98f : 0.92f }
            : new Vector4(0.66f, 0.66f, 0.70f, 0.92f);

        dl.AddRectFilled(min, max, ImGui.GetColorU32(fillColor), rounding);
        dl.AddRect(min, max, ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.None, 1f * scale);

        var cursor = new Vector2(min.X + (style.PaddingX * scale), min.Y + ((size.Y - ImGui.GetTextLineHeight()) * 0.5f));

        if (icon != FontAwesomeIcon.None)
        {
            using var f = ImRaii.PushFont(UiBuilder.IconFont);
            var iconText = icon.ToIconString();
            var iconSize = ImGui.CalcTextSize(iconText);
            var iconPos = new Vector2(cursor.X, min.Y + ((size.Y - iconSize.Y) * 0.5f));
            dl.AddText(UiBuilder.IconFont, ImGui.GetFontSize(), iconPos, ImGui.GetColorU32(accentColor with { W = active ? 0.92f : 0.52f }), iconText);
            cursor.X = iconPos.X + iconSize.X + (style.IconGap * scale);
        }
        else
        {
            var dotRadius = style.DotRadius * scale;
            var dotCenter = new Vector2(
                min.X + (style.PaddingX * scale) + dotRadius,
                min.Y + (size.Y * 0.5f));
            dl.AddCircleFilled(dotCenter, dotRadius, ImGui.GetColorU32(accentColor with { W = active ? 0.98f : 0.42f }), 8);
            cursor.X = dotCenter.X + dotRadius + (style.DotGap * scale);
        }

        dl.AddText(cursor, ImGui.GetColorU32(textColor), label);

        if (hovered && !string.IsNullOrEmpty(tooltip))
            ImGui.SetTooltip(tooltip);

        return clicked;
    }
}

internal readonly record struct SegmentedToggleStyle
{
    public SegmentedToggleStyle() { }
    public float Width        { get; init; } = 72f;
    public float Height       { get; init; } = 0f;
    public float OuterPadding { get; init; } = 2f;
    public float LabelGap     { get; init; } = 10f;
    public float Rounding     { get; init; } = 999f;
}

internal static class SegmentedToggle
{
    public static bool Draw(string label, ref bool value, Vector4 accentColor, SegmentedToggleStyle? styleOverride = null)
    {
        var style = styleOverride ?? new SegmentedToggleStyle();
        var scale = ImGuiHelpers.GlobalScale;
        var height = style.Height > 0f ? style.Height * scale : ImGui.GetFrameHeight();
        var size = new Vector2(style.Width * scale, height);
        var (displayText, idText) = SplitLabel(label);

        var changed = DrawControl(idText, ref value, size, accentColor, style, scale);

        if (!string.IsNullOrEmpty(displayText))
        {
            ImGui.SameLine(0f, style.LabelGap * scale);
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(displayText);
        }

        return changed;
    }

    private static bool DrawControl(string id, ref bool value, Vector2 size, Vector4 accentColor, SegmentedToggleStyle style, float scale)
    {
        var alpha = ImGui.GetStyle().Alpha;
        ImGui.PushID(id);

        var changed = false;
        if (ImGui.InvisibleButton("##seg", size))
        {
            value = !value;
            changed = true;
        }

        var hovered = ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenBlockedByActiveItem);
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var dl = ImGui.GetWindowDrawList();
        var rounding = style.Rounding * scale;
        var innerPadding = MathF.Min(style.OuterPadding * scale, MathF.Max(1f, size.Y * 0.18f));
        var segmentWidth = (size.X - (innerPadding * 2f)) * 0.5f;

        var outerFill  = ApplyAlpha(new Vector4(0.08f, 0.08f, 0.10f, hovered ? 0.98f : 0.92f), alpha);
        var borderColor = ApplyAlpha(accentColor, hovered ? 0.54f : 0.34f, alpha);
        var activeFill  = ApplyAlpha(new Vector4(0.96f, 0.96f, 0.98f, hovered ? 1.00f : 0.97f), alpha);
        var inactiveText = ApplyAlpha(accentColor, hovered ? 0.82f : 0.66f, alpha);
        var activeText   = ApplyAlpha(new Vector4(0.09f, 0.09f, 0.10f, 0.96f), alpha);

        var pillMin = new Vector2(min.X + innerPadding + (value ? segmentWidth : 0f), min.Y + innerPadding);
        var pillMax = new Vector2(pillMin.X + segmentWidth, max.Y - innerPadding);

        dl.AddRectFilled(min, max, ImGui.GetColorU32(outerFill), rounding);
        dl.AddRect(min, max, ImGui.GetColorU32(borderColor), rounding, ImDrawFlags.None, 1f * scale);
        dl.AddRectFilled(pillMin, pillMax, ImGui.GetColorU32(activeFill), rounding);

        DrawSegText(dl, min, segmentWidth, size.Y, "OFF", !value, activeText, inactiveText);
        DrawSegText(dl, new Vector2(min.X + innerPadding + segmentWidth, min.Y), segmentWidth, size.Y, "ON", value, activeText, inactiveText);

        ImGui.PopID();
        return changed;
    }

    private static void DrawSegText(ImDrawListPtr dl, Vector2 origin, float w, float h, string text, bool active, Vector4 activeColor, Vector4 inactiveColor)
    {
        var sz = ImGui.CalcTextSize(text);
        var pos = new Vector2(origin.X + ((w - sz.X) * 0.5f), origin.Y + ((h - sz.Y) * 0.5f));
        dl.AddText(pos, ImGui.GetColorU32(active ? activeColor : inactiveColor), text);
    }

    private static (string Display, string Id) SplitLabel(string label)
    {
        var idx = label.IndexOf("##", StringComparison.Ordinal);
        if (idx < 0) return (label, label);
        var display = label[..idx];
        var id = label[(idx + 2)..];
        return (display, string.IsNullOrEmpty(id) ? display : id);
    }

    private static Vector4 ApplyAlpha(Vector4 c, float a) => new(c.X, c.Y, c.Z, c.W * a);
    private static Vector4 ApplyAlpha(Vector4 c, float target, float alpha) => new(c.X, c.Y, c.Z, target * alpha);
}
