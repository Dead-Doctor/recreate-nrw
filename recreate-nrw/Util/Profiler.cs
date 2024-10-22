using System.Collections.Concurrent;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using JetBrains.Annotations;

namespace recreate_nrw.Util;

public class Profiler
{
    private static readonly object Lock = new();
    private static readonly Stopwatch Stopwatch = Stopwatch.StartNew();
    private static readonly ConcurrentBag<int> UniqueThreads = new();
    private static readonly Profiler RootTask = new("root");

    [PublicAPI]
    public static Profiler Create(string name)
    {
        return RootTask.Start(name);
    }

    private readonly string _name;
    private readonly int _thread;
    private readonly TimeSpan _startTime;
    private TimeSpan? _endTime;

    private readonly ConcurrentBag<Profiler> _subTasks = new();

    private Profiler(string name)
    {
        _name = name;
        _thread = Environment.CurrentManagedThreadId;
        if (!UniqueThreads.Contains(_thread)) UniqueThreads.Add(_thread);
        _startTime = Stopwatch.Elapsed;
    }

    public Profiler Start(string name)
    {
        lock (Lock)
        {
            var task = new Profiler(name);
            _subTasks.Add(task);
            return task;
        }
    }

    public void Stop()
    {
        lock (Lock)
        {
            _endTime = Stopwatch.Elapsed;
        }
    }
    
    private const float ButtonHeight = 20.0f;
    private const float PaddingY = 3.0f;
    private const float TotalRowHeight = ButtonHeight + PaddingY;
    private static Profiler? _lastClickedNode;

    public static void Window()
    {
        ImGui.Begin("Profiler");
        lock (Lock)
        {
            var startTime = TimeSpan.MaxValue;
            var endTime = TimeSpan.MinValue;
            foreach (var task in RootTask._subTasks)
            {
                if (task._startTime < startTime) startTime = task._startTime;
                if (task.LastTime > endTime) endTime = task.LastTime;
            }

            var lastY = 0.0f;
            foreach (var task in RootTask._subTasks)
            {
                lastY = task.FlameGraph(startTime, endTime, lastY);
                lastY += 15.0f;
            }
        }

        if (ImGui.BeginPopup("nodeInfoPopup"))
        {
            ImGui.Text(_lastClickedNode!.Description);
            ImGui.EndPopup();
        }

        ImGui.End();
    }

    private float FlameGraph(TimeSpan startTime, TimeSpan endTime, float startY, int depth = 0)
    {
        var deltaTime = endTime - startTime;
        var startPercentage = (_startTime - startTime) / deltaTime;
        var endPercentage = (_endTime ?? endTime - startTime) / deltaTime;

        var totalWidth = ImGui.GetContentRegionAvail().X;
        var startX = startPercentage * totalWidth;
        var endX = endPercentage * totalWidth;

        ImGui.SetCursorPosX(ImGui.GetCursorStartPos().X + (float)startX);
        ImGui.SetCursorPosY(ImGui.GetCursorStartPos().Y + startY);
        
        var threadsCount = UniqueThreads.Count;
        var threadIndex = -1;
        for (var i = 0; i < threadsCount; i++)
        {
            if (UniqueThreads.ElementAt(i) != _thread) continue;
            threadIndex = i;
            break;
        }
        var hue = (float)threadIndex / threadsCount;
        ImGui.PushStyleColor(ImGuiCol.Button, ImGuiExtension.Hsv(hue, 0.6f, 0.6f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, ImGuiExtension.Hsv(hue, 0.7f, 0.7f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, ImGuiExtension.Hsv(hue, 0.8f, 0.8f));
        
        // ReSharper disable once InvertIf
        if (ImGui.Button(Description, new Vector2((float)(endX - startX), ButtonHeight)))
        {
            _lastClickedNode = this;
            ImGui.OpenPopup("nodeInfoPopup");
        }
        ImGui.PopStyleColor(3);

        var lastY = startY + TotalRowHeight;
        // ReSharper disable once LoopCanBeConvertedToQuery
        foreach (var node in _subTasks)
        {
            lastY = node.FlameGraph(startTime, endTime, lastY, depth + 1);
        }
        return lastY;
    }

    private TimeSpan LastTime =>
        _endTime ?? (_subTasks.IsEmpty ? Stopwatch.Elapsed : _subTasks.Max(node => node.LastTime));

    private string Description => $"{_name} - {DurationString}";

    private string DurationString =>
        Stopped
            ? $"{TotalTime.FormatDuration()}"
            : $"Running... ({GetRunningTask})";

    private bool Stopped => _endTime != null;

    private TimeSpan TotalTime => (TimeSpan)(_endTime! - _startTime);

    private string GetRunningTask
    {
        get
        {
            if (_subTasks.IsEmpty) return _name;
            var lastProcess = _subTasks.Last();
            return lastProcess.Stopped ? _name : lastProcess.GetRunningTask;
        }
    }

    public override string ToString() => _name;
}