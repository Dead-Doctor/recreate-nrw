using System.Collections.Concurrent;
using System.Collections.Immutable;
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
    private static readonly ConcurrentDictionary<Profiler, bool> RootTasks = new();

    [PublicAPI]
    public static Profiler Create(string name)
    {
        lock (Lock)
        {
            var task = new Profiler(name);
            RootTasks.TryAdd(task, false);
            return task;
        }
    }

    private readonly string _name;
    private readonly int _thread;
    private readonly TimeSpan _startTime;
    private TimeSpan? _endTime;

    private readonly ConcurrentDictionary<int, ConcurrentBag<Profiler>> _subTasks = new();

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
            _subTasks.GetOrAdd(task._thread, _ => new ConcurrentBag<Profiler>()).Add(task);
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

    public void Start(string name, Action<Profiler> action)
    {
        var task = Start(name);
        action(task);
        task.Stop();
    }
    
    private const float GapY = 15.0f;
    private const float ButtonHeight = 20.0f;
    private const float PaddingX = 1.0f;
    private const float PaddingY = 3.0f;
    private static Profiler? _selectedNode;
    
    public static void Window()
    {
        ImGui.Begin("Profiler");
        
        lock (Lock)
        {
            var startTime = TimeSpan.MaxValue;
            var endTime = TimeSpan.MinValue;
            
            var toggledTasks = new List<Profiler>();
            ImGui.BeginChild("##Selectors", new Vector2(0f, 50f), ImGuiChildFlags.Border | ImGuiChildFlags.ResizeY);
            foreach (var (task, selected) in RootTasks)
            {
                if (selected)
                {
                    if (task._startTime < startTime) startTime = task._startTime;
                    if (task.LastTime > endTime) endTime = task.LastTime;
                }
                
                if (ImGui.Selectable(task._name, selected))
                {
                    toggledTasks.Add(task);
                    _selectedNode = task;
                }
            }
            ImGui.EndChild();

            ImGui.Text(_selectedNode == null
                ? "No node selected"
                : $"{_selectedNode.Description} - Thread: {_selectedNode._thread}");

            ImGui.BeginChild("##FlameGraph", Vector2.Zero, ImGuiChildFlags.Border);
            ImGui.PushStyleVar(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.0f, 0.5f));
            
            var lastY = 0f;
            var id = 0;
            foreach (var (task, value) in RootTasks)
            {
                if (!value) continue;

                ImGui.PushID(id++);
                lastY = task.FlameGraph(startTime, endTime, lastY);
                lastY += GapY;
                ImGui.PopID();
            }
            
            ImGui.PopStyleVar();
            ImGui.EndChild();

            foreach (var task in toggledTasks)
            {
                RootTasks[task] ^= true;
            }
        }
        
        ImGui.End();
    }
    
    private float FlameGraph(TimeSpan startTime, TimeSpan endTime, float startY)
    {
        var deltaTime = endTime - startTime;
        var startPercentage = (_startTime - startTime) / deltaTime;
        var endPercentage = ((_endTime ?? endTime) - startTime) / deltaTime;

        var totalWidth = ImGui.GetContentRegionAvail().X;
        var startX = (int)(startPercentage * totalWidth);
        var endX = (int)(endPercentage * totalWidth);
        
        ImGui.SetCursorPosX(startX + PaddingX);
        ImGui.SetCursorPosY(startY);

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

        var size = new Vector2(MathF.Max(endX - startX - PaddingX * 2, 1f), ButtonHeight);
        if (ImGui.Button(Description, size))
        {
            _selectedNode = this;
        }
        
        ImGui.PopStyleColor(3);

        var id = 0;
        var lastY = startY + ButtonHeight + PaddingY;
        foreach (var tasksPerThread in _subTasks)
        {
            var lastGroupY = lastY;
            foreach (var task in tasksPerThread.Value)
            {
                ImGui.PushID(id++);
                lastGroupY = task.FlameGraph(startTime, endTime, lastY);
                ImGui.PopID();
            }
            lastY = lastGroupY;
        }

        return lastY;
    }

    private TimeSpan LastTime =>
        _endTime ?? (_subTasks.IsEmpty
            ? Stopwatch.Elapsed
            : _subTasks.Max(tasks => tasks.Value.Max(task => task.LastTime)));

    private string Description => $"{_name} - {TimeString}";

    private string TimeString =>
        Stopped
            ? $"{TotalTime.FormatDuration()}"
            : "Running...";

    private bool Stopped => _endTime != null;

    private TimeSpan TotalTime => (TimeSpan)(_endTime! - _startTime);

    public override string ToString() => _name;
}