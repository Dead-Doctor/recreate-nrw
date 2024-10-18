using System.Collections;
using System.Diagnostics;
using System.Numerics;
using ImGuiNET;
using JetBrains.Annotations;

namespace recreate_nrw.Util;

//TODO: improve ui

public static class Profiler
{
    private static readonly object Lock = new();
    private static readonly Stopwatch Stopwatch = new();
    private static readonly Hashtable ProcessNodes;

    static Profiler()
    {
        Stopwatch.Start();
        ProcessNodes = Hashtable.Synchronized(new Hashtable());
    }

    private static TimeSpan GetElapsed()
    {
        lock (Lock) return Stopwatch.Elapsed;
    }

    [PublicAPI]
    public static void Start(string name)
    {
        lock (Lock)
        {
            var thread = Environment.CurrentManagedThreadId;
            if (!ProcessNodes.Contains(thread))
                ProcessNodes.Add(thread, new ProcessNode($"{Thread.CurrentThread.Name} ({thread})", GetElapsed()));

            var node = (ProcessNode)ProcessNodes[thread]!;
            node.Start(name, GetElapsed());
        }
    }

    [PublicAPI]
    public static void Stop()
    {
        lock (Lock)
        {
            var thread = Environment.CurrentManagedThreadId;
            if (!ProcessNodes.Contains(thread))
                throw new InvalidOperationException(
                    $"Tried to stop profiler but no processes are running on Thread ({thread}).");

            var node = (ProcessNode)ProcessNodes[thread]!;
            node.Stop(GetElapsed());
        }
    }

    public static void Window()
    {
        ImGui.Begin("Profiler");
        var y = 0.0f;
        lock (Lock)
        {
            var startTime = TimeSpan.MaxValue;
            var endTime = TimeSpan.MinValue;
            foreach (ProcessNode node in ProcessNodes.Values)
            {
                if (node.StartTime < startTime) startTime = node.StartTime;
                if (node.LastTime > endTime) endTime = node.LastTime;
            }
            foreach (ProcessNode node in ProcessNodes.Values)
            {
                y += node.FlameGraph(startTime, endTime, y);
                y += 15.0f;
            }
        }
        if (ImGui.BeginPopup("nodeInfoPopup"))
        {
            ImGui.Text(ProcessNode.LastClickedNode!.Description);
            ImGui.EndPopup();
        }

        ImGui.End();
    }

    private class ProcessNode
    {
        private const float Height = 20.0f;

        public static ProcessNode? LastClickedNode;
        
        private readonly string _name;
        public readonly TimeSpan StartTime;
        private TimeSpan? _endTime;

        private readonly List<ProcessNode> _subProcessNodes = new();

        public ProcessNode(string name, TimeSpan elapsed)
        {
            _name = name;
            StartTime = elapsed;
        }

        public void Start(string name, TimeSpan elapsed)
        {

            lock (_subProcessNodes)
            {
                if (_subProcessNodes.Count > 0)
                {
                    var lastProcess = _subProcessNodes.Last();
                    if (!lastProcess.Stopped)
                    {
                        lastProcess.Start(name, elapsed);
                        return;
                    }

                    if (lastProcess._endTime > elapsed)
                        throw new Exception("Next process started earlier then last one stopped (on the same thread).");
                }

                _subProcessNodes.Add(new ProcessNode(name, elapsed));
            }
        }

        public void Stop(TimeSpan elapsed)
        {
            lock (_subProcessNodes)
            {
                if (_subProcessNodes.Count != 0)
                {
                    var lastProcess = _subProcessNodes.Last();
                    if (!lastProcess.Stopped)
                    {
                        lastProcess.Stop(elapsed);
                        return;
                    }
                }

                _endTime = elapsed;
            }
        }

        public TimeSpan LastTime =>
            _endTime ?? (_subProcessNodes.Count <= 0 ? GetElapsed() : _subProcessNodes.Max(node => node.LastTime));

        private string GetRunningTask()
        {
            if (_subProcessNodes.Count <= 0) return _name;
            var lastProcess = _subProcessNodes.Last();
            return lastProcess.Stopped ? _name : lastProcess.GetRunningTask();
        }

        private TimeSpan TotalTime() => (TimeSpan)(_endTime! - StartTime);
        private TimeSpan TotalTimeInSubprocess() => new(_subProcessNodes.Sum(node => node.TotalTime().Ticks));
        
        public float FlameGraph(TimeSpan startTime, TimeSpan endTime, float startY)
        {
            var y = ImGui.GetCursorStartPos().Y + startY;
            lock (_subProcessNodes)
            {
                return _subProcessNodes.Select(node => node.FlameGraph(startTime, endTime, y, 0)).Prepend(0.0f).Max();
            }
        }

        private float FlameGraph(TimeSpan startTime, TimeSpan endTime, float startY, int depth)
        {
            var deltaTime = endTime - startTime;
            var startPercentage = (StartTime - startTime) / deltaTime;
            var endPercentage = (_endTime ?? endTime - startTime) / deltaTime;

            var totalWidth = ImGui.GetContentRegionAvail().X;
            var startX = startPercentage * totalWidth;
            var endX = endPercentage * totalWidth;

            ImGui.SetCursorPosX(ImGui.GetCursorStartPos().X + (float)startX);
            var y = (Height + 3.0f) * depth;
            ImGui.SetCursorPosY(startY + y);

            if (ImGui.Button(Description, new Vector2((float)(endX - startX), Height)))
            {
                LastClickedNode = this;
                ImGui.OpenPopup("nodeInfoPopup");
            }
            
            return _subProcessNodes.Select(node => node.FlameGraph(startTime, endTime, startY, depth + 1)).Prepend(y + Height).Max();
        }

        private string DurationString =>
            Stopped
                ? $"{FormatDuration(TotalTime())} ({FormatDuration(TotalTime() - TotalTimeInSubprocess())})"
                : $"Running... ({GetRunningTask()})";

        public string Description => $"{_name} - {DurationString}";
        
        private bool Stopped => _endTime != null;

        public override string ToString() => _name;
    }
    

    [PublicAPI]
    public static string FormatDuration(TimeSpan duration) =>
        duration.TotalSeconds < 1.0 ? $"{duration.TotalMilliseconds:N0}ms"
        : duration.TotalMinutes < 1.0 ? $"{duration.TotalSeconds:N1}s"
        : duration.TotalHours < 1.0 ? $"{duration.TotalMinutes:N1}min"
        : duration.TotalDays < 1.0 ? $"{duration.TotalHours:N1}min"
        : $"{duration.TotalDays:N1}min";
}