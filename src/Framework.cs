using System;
using System.Collections.Generic;
using System.Text;

namespace PawnshopCardRoom;

// One enabled session. Register releases before acquisition to cover partial failures.
internal sealed class RuntimeLifetime
{
    private enum Phase { Stopped, Starting, Running, Stopping, Faulted }
    private readonly List<Action> _releases = new List<Action>();
    private readonly Action<Exception> _report;
    private Phase _phase;
    internal bool IsFaulted => _phase == Phase.Faulted;

    public RuntimeLifetime(Action<Exception> report) { _report = report; }

    public bool Start(Action initialize)
    {
        if (_phase == Phase.Faulted) throw new InvalidOperationException("运行期清理失败，请重启游戏后再启用");
        if (_phase != Phase.Stopped) return false;
        _phase = Phase.Starting;
        try
        {
            initialize();
            if (_phase != Phase.Starting) throw new InvalidOperationException("启动阶段被中断");
            _phase = Phase.Running;
            return true;
        }
        catch { Stop(); throw; }
    }

    public void Own(Action acquire, Action release)
    {
        if (_phase != Phase.Starting) throw new InvalidOperationException("资源只能在启动阶段获取");
        _releases.Add(release ?? throw new ArgumentNullException(nameof(release)));
        acquire();
    }

    public void Continue(Action initialize)
    {
        if (_phase != Phase.Running) throw new InvalidOperationException("运行期不在等待启动阶段");
        _phase = Phase.Starting;
        try
        {
            initialize();
            if (_phase != Phase.Starting) throw new InvalidOperationException("启动阶段被中断");
            _phase = Phase.Running;
        }
        catch { Stop(); throw; }
    }

    public void Stop()
    {
        if (_phase == Phase.Stopped || _phase == Phase.Stopping || _phase == Phase.Faulted) return;
        _phase = Phase.Stopping;
        bool failed = false;
        for (int i = _releases.Count - 1; i >= 0; i--)
        {
            try { _releases[i](); }
            catch (Exception error) { failed = true; try { _report?.Invoke(error); } catch { } }
        }
        _releases.Clear();
        _phase = failed ? Phase.Faulted : Phase.Stopped;
    }
}

internal sealed class MaintenanceDiagnostics
{
    private const int Capacity = 64;
    private readonly Dictionary<(string Module, string Stage, string Code, string Error), int> _counts =
        new Dictionary<(string, string, string, string), int>();
    private int _omitted;

    internal void Record(string module, string stage, string code, Exception error)
    {
        var key = (module, stage, code, error?.GetType().Name);
        if (_counts.TryGetValue(key, out int count))
            _counts[key] = Increment(count);
        else if (_counts.Count < Capacity)
            _counts.Add(key, 1);
        else
            _omitted = Increment(_omitted);
    }

    internal string Format()
    {
        var report = new StringBuilder("诊断故障");
        foreach (var entry in _counts)
        {
            var (module, stage, code, error) = entry.Key;
            report.Append('\n').Append(module).Append('/').Append(stage).Append('/').Append(code);
            if (error != null) report.Append(" (").Append(error).Append(')');
            report.Append(" x").Append(entry.Value);
        }
        if (_omitted > 0) report.Append("\n超出容量的故障次数 ").Append(_omitted);
        if (_counts.Count == 0) report.Append(" 0");
        return report.ToString();
    }

    internal void Clear()
    {
        _counts.Clear();
        _omitted = 0;
    }

    private static int Increment(int value) => value == int.MaxValue ? value : value + 1;
}
