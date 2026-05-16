using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using System;
using System.Numerics;

namespace Mountify.UI;

internal sealed class CardScope : IDisposable
{
    private readonly ImDrawListPtr _drawList;
    private readonly Vector2 _cardMin;
    private readonly float _cardWidth;
    private readonly float _headerHeight;
    private readonly string _title;
    private readonly Vector4 _accentColor;
    private readonly float _scale;
    private readonly float _contentInsetX;

    public CardScope(string title, Vector4 accentColor)
    {
        _title = title;
        _accentColor = accentColor;
        _scale = ImGuiHelpers.GlobalScale;
        _contentInsetX = 12f * _scale;

        var titleSize = ImGui.CalcTextSize(title);
        _headerHeight = MathF.Max(34f * _scale, titleSize.Y + (18f * _scale));

        _drawList = ImGui.GetWindowDrawList();
        _drawList.ChannelsSplit(2);
        _drawList.ChannelsSetCurrent(1);

        _cardMin = ImGui.GetCursorScreenPos();
        _cardWidth = ImGui.GetContentRegionAvail().X;

        // Reserve header space + top content padding in a single Dummy
        ImGui.Dummy(new Vector2(_cardWidth, _headerHeight + (8f * _scale)));
        ImGui.Indent(_contentInsetX);
    }

    public void Dispose()
    {
        ImGui.Unindent(_contentInsetX);
        ImGui.Dummy(new Vector2(0f, 10f * _scale));

        var cardMax = new Vector2(_cardMin.X + _cardWidth, ImGui.GetCursorScreenPos().Y);

        _drawList.ChannelsSetCurrent(0);
        DrawGeometry(_cardMin, cardMax);
        _drawList.ChannelsMerge();

        ImGui.Dummy(new Vector2(0f, 4f * _scale));
    }

    private void DrawGeometry(Vector2 min, Vector2 max)
    {
        var rounding = 8f * _scale;
        var headerMax = new Vector2(max.X, min.Y + _headerHeight);

        _drawList.AddRectFilled(min, max, ImGui.GetColorU32(Theme.CardBg), rounding);
        _drawList.AddRectFilled(min, headerMax, ImGui.GetColorU32(Theme.CardHead), rounding, ImDrawFlags.RoundCornersTop);
        _drawList.AddLine(
            new Vector2(min.X + 1f, min.Y + _headerHeight),
            new Vector2(max.X - 1f, min.Y + _headerHeight),
            ImGui.GetColorU32(Theme.Border with { W = 0.28f }),
            1f * _scale);
        _drawList.AddRect(min, max, ImGui.GetColorU32(Theme.Border), rounding, ImDrawFlags.None, 1f * _scale);

        var titleText = _title.ToUpperInvariant();
        var titleSize = ImGui.CalcTextSize(titleText);
        var titlePos = new Vector2(
            min.X + (14f * _scale),
            min.Y + ((_headerHeight - titleSize.Y) * 0.5f));
        _drawList.AddText(titlePos, ImGui.GetColorU32(_accentColor with { W = 0.90f }), titleText);
    }
}
