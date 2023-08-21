using System.Diagnostics;
using ImGuiNET;
using JetBrains.Annotations;

namespace recreate_nrw.Util;

public class Profiler
{
    private readonly Stopwatch _stopwatch = new();
    private readonly ProcessNode _processNode;

    public Profiler(string name)
    {
        _stopwatch.Start();
        _processNode = new ProcessNode(name, _stopwatch.Elapsed);
    }

    [PublicAPI]
    public void Start(string name)
    {
        _processNode.Start(name, _stopwatch.Elapsed);
    }

    [PublicAPI]
    public void Stop()
    {
        _processNode.Stop(_stopwatch.Elapsed);
    }

    [PublicAPI]
    public void StopProfiler()
    {
        _processNode.Stop(_stopwatch.Elapsed);
        if (!_processNode.Stopped()) throw new Exception("Can't stop profiler until all process have been stopped.");
        _stopwatch.Stop();
    }

    public void ImGuiTree()
    {
        _processNode.ImGuiTreeNode();
    }

    private class ProcessNode
    {
        private readonly string _name;
        private readonly TimeSpan _startTime;
        private TimeSpan? _endTime;

        private readonly List<ProcessNode> _subProcessNodes = new();

        public ProcessNode(string name, TimeSpan elapsed)
        {
            _name = name;
            _startTime = elapsed;
        }

        public void Start(string name, TimeSpan elapsed)
        {
            if (_subProcessNodes.Count > 0)
            {
                var lastProcess = _subProcessNodes.Last();
                if (!lastProcess.Stopped())
                {
                    lastProcess.Start(name, elapsed);
                    return;
                }
            }

            _subProcessNodes.Add(new ProcessNode(name, elapsed));
        }

        public void Stop(TimeSpan elapsed)
        {
            if (_subProcessNodes.Count != 0)
            {
                var lastProcess = _subProcessNodes.Last();
                if (!lastProcess.Stopped())
                {
                    lastProcess.Stop(elapsed);
                    return;
                }
            }

            _endTime = elapsed;
        }

        private string GetRunningTask()
        {
            if (_subProcessNodes.Count <= 0) return _name;
            var lastProcess = _subProcessNodes.Last();
            return lastProcess.Stopped() ? _name : lastProcess.GetRunningTask();
        }

        private TimeSpan TotalTime() => (TimeSpan) (_endTime! - _startTime);
        private TimeSpan TotalTimeInSubprocess() => new(_subProcessNodes.Sum(node => node.TotalTime().Ticks));

        public void ImGuiTreeNode()
        {
            if (!ImGui.TreeNode(_name)) return;
            ImGui.Text($"Time: {DurationString()}");
            foreach (var subProcessNode in _subProcessNodes)
            {
                subProcessNode.ImGuiTreeNode();
            }
            ImGui.TreePop();
        }

        private string DurationString() =>
            Stopped()
                ? $"{FormatDuration(TotalTime())} ({FormatDuration(TotalTime() - TotalTimeInSubprocess())})"
                : $"Running... ({GetRunningTask()})";

        public bool Stopped() => _endTime != null;

        public override string ToString() => _name;
    }
    
    
    [PublicAPI]
    public static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalSeconds < 1.0 ? $"{duration.TotalMilliseconds:N0}ms"
            : duration.TotalMinutes < 1.0 ? $"{duration.TotalSeconds:N1}s"
            : duration.TotalHours < 1.0 ? $"{duration.TotalMinutes:N1}min"
            : duration.TotalDays < 1.0 ? $"{duration.TotalHours:N1}min"
            : $"{duration.TotalDays:N1}min";
    }
}