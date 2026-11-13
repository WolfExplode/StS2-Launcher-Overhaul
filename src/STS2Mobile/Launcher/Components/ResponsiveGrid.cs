using System;
using System.Collections.Generic;
using Godot;

namespace STS2Mobile.Launcher.Components;

// Lays children out in equal-width columns, using as many columns (up to
// maxColumns) as fit every child's minimum width, and stacks them when even
// two do not fit. The choice is made again on every resize, so a row built in
// landscape still fits after rotating to portrait.
//
// The minimum width is the widest child, so the grid never forces its parent
// wider than one column needs. As with a wrapping label, the minimum height
// follows the column count chosen for the current width.
internal sealed partial class ResponsiveGrid : Container
{
    private readonly int _separation;
    private readonly int _maxColumns;
    private int _columns = 1;

    internal ResponsiveGrid(int separation, int maxColumns = int.MaxValue)
    {
        _separation = separation;
        _maxColumns = Math.Max(1, maxColumns);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
    }

    public override Vector2 _GetMinimumSize()
    {
        var children = LaidOutChildren();
        var width = 0f;
        foreach (var child in children)
            width = Math.Max(width, child.GetCombinedMinimumSize().X);
        return new Vector2(width, Height(children, _columns));
    }

    public override void _Notification(int what)
    {
        if (what != NotificationSortChildren)
            return;

        var children = LaidOutChildren();
        var columns = ColumnsThatFit(children, Size.X);
        if (columns != _columns)
        {
            _columns = columns;
            UpdateMinimumSize();
        }

        var cellWidth = CellWidth(Size.X, columns);
        var y = 0f;
        for (var start = 0; start < children.Count; start += columns)
        {
            var rowHeight = RowHeight(children, start, columns);
            for (var i = start; i < Math.Min(start + columns, children.Count); i++)
                FitChildInRect(children[i], new Rect2((i - start) * (cellWidth + _separation), y, cellWidth, rowHeight));
            y += rowHeight + _separation;
        }
    }

    private int ColumnsThatFit(List<Control> children, float width)
    {
        for (var columns = Math.Min(_maxColumns, children.Count); columns > 1; columns--)
        {
            var cellWidth = CellWidth(width, columns);
            if (children.TrueForAll(child => child.GetCombinedMinimumSize().X <= cellWidth))
                return columns;
        }

        return 1;
    }

    private float CellWidth(float width, int columns)
        => Math.Max(0f, (width - _separation * (columns - 1)) / columns);

    private float Height(List<Control> children, int columns)
    {
        var height = 0f;
        for (var start = 0; start < children.Count; start += columns)
            height += RowHeight(children, start, columns) + (start > 0 ? _separation : 0);
        return height;
    }

    private static float RowHeight(List<Control> children, int start, int columns)
    {
        var height = 0f;
        for (var i = start; i < Math.Min(start + columns, children.Count); i++)
            height = Math.Max(height, children[i].GetCombinedMinimumSize().Y);
        return height;
    }

    private List<Control> LaidOutChildren()
    {
        var children = new List<Control>();
        foreach (var node in GetChildren())
        {
            if (node is Control { Visible: true, TopLevel: false } control)
                children.Add(control);
        }

        return children;
    }
}
