namespace DmxBox.Server;

/// <summary>
/// Undo and redo for what the project holds: the looks and sequences of the memories, the
/// stack, the notes and the bindings of the independent keys. The faders, Clear, the master,
/// blackout and the settings of the UI are no steps of the history.
/// </summary>
/// <remarks>
/// <para>
/// Every change that may alter the project runs inside <see cref="Change"/>, which keeps the
/// project as it was before. If the project did change, that copy becomes a step to go back to.
/// Changes of the same kind that follow each other quickly, like the moves of a speed fader or a
/// series of taps, make one step. At most <see cref="MostSteps"/> steps are kept.
/// </para>
/// <para>
/// One history for the whole desk, whichever UI made a change. Loading a project, a new one and
/// an import start an empty history. While the stack is live, nothing that is stored can change,
/// so undo and redo are refused then, as the desk refuses the changes themselves.
/// </para>
/// <para>Thread-safe: the changes, undo and redo take turns.</para>
/// </remarks>
internal sealed class UndoHistory
{
    /// <summary>The most steps that are kept to go back to.</summary>
    public const int MostSteps = 100;

    /// <summary>Changes of the same kind within this time of each other make one step.</summary>
    public static readonly TimeSpan JoinWithin = TimeSpan.FromSeconds(2);

    private readonly Desk _desk;
    private readonly TimeProvider _time;
    private readonly Lock _lock = new();

    // The steps to go back to, the newest last, and those to go forward to again, the next first.
    private readonly LinkedList<Entry> _undo = new();
    private readonly Stack<Entry> _redo = new();

    // What TakeChange gave out the last time.
    private HistoryState _told = new(null, null);

    /// <summary>Makes an empty history.</summary>
    /// <param name="desk">The desk whose project the history keeps.</param>
    /// <param name="time">The clock that tells which changes follow each other quickly; the tests pass one they can set by hand.</param>
    public UndoHistory(Desk desk, TimeProvider? time = null)
    {
        _desk = desk;
        _time = time ?? TimeProvider.System;
    }

    /// <summary>What undo and redo would do now.</summary>
    public HistoryState State
    {
        get
        {
            lock (_lock)
            {
                return CurrentState();
            }
        }
    }

    /// <summary>
    /// Wraps a change of the project: use it in a "using" around the call to the desk. If the
    /// project changed by the time the scope ends, it becomes a step of the history, and what
    /// was undone before cannot be redone any more.
    /// </summary>
    /// <param name="what">What the change does, for the UI: "storing memory 5".</param>
    /// <param name="kind">
    /// Changes of the same kind that follow within <see cref="JoinWithin"/> make one step, like
    /// "speed 3" for the moves of the speed fader of memory 3; null for a change that is a step
    /// of its own.
    /// </param>
    public Scope Change(string what, string? kind = null)
    {
        // Held until the scope ends: the copy of before and the change itself belong together,
        // and a change of another UI must not come between them.
        _lock.Enter();
        try
        {
            (ProjectContent before, long changes) = _desk.GetProject();
            return new Scope(this, new Entry(before, what, kind, _time.GetTimestamp()), changes);
        }
        catch
        {
            _lock.Exit();
            throw;
        }
    }

    /// <summary>Goes back one step: the project is as it was before the last change.</summary>
    /// <returns>What was undone, like "storing memory 5".</returns>
    /// <exception cref="DeskException">There is nothing to undo, or the stack is live.</exception>
    public string Undo()
    {
        lock (_lock)
        {
            Entry back = _undo.Last?.Value ?? throw new DeskException("There is nothing to undo.");
            (ProjectContent now, _) = _desk.GetProject();
            _desk.RestoreProject(back.Content);   // refused while the stack is live, before anything changed
            _undo.RemoveLast();
            _redo.Push(back with { Content = now });
            return back.What;
        }
    }

    /// <summary>Goes forward one step again, after undo: the change is made again.</summary>
    /// <returns>What was redone, like "storing memory 5".</returns>
    /// <exception cref="DeskException">There is nothing to redo, or the stack is live.</exception>
    public string Redo()
    {
        lock (_lock)
        {
            if (!_redo.TryPeek(out Entry? forward))
                throw new DeskException("There is nothing to redo.");

            (ProjectContent now, _) = _desk.GetProject();
            _desk.RestoreProject(forward.Content);
            _redo.Pop();
            _undo.AddLast(forward with { Content = now, Kind = null, At = 0 });
            return forward.What;
        }
    }

    /// <summary>Forgets every step, as after loading a project.</summary>
    public void Clear()
    {
        lock (_lock)
        {
            _undo.Clear();
            _redo.Clear();
        }
    }

    /// <summary>
    /// For telling the UIs: what undo and redo would do now, if that differs from what this
    /// method gave out the last time; otherwise null.
    /// </summary>
    public HistoryState? TakeChange()
    {
        lock (_lock)
        {
            HistoryState current = CurrentState();
            if (current == _told)
                return null;

            _told = current;
            return current;
        }
    }

    // Call with the lock held.
    private HistoryState CurrentState()
    {
        return new HistoryState(_undo.Last?.Value.What, _redo.TryPeek(out Entry? next) ? next.What : null);
    }

    // Call with the lock held, at the end of a change. A change that changed nothing leaves no step.
    private void Finish(Entry entry, long changesBefore)
    {
        if (_desk.ProjectChanges == changesBefore)
            return;

        _redo.Clear();
        if (_undo.Last?.Value is { } last
            && entry.Kind is not null
            && last.Kind == entry.Kind
            && _time.GetElapsedTime(last.At, entry.At) <= JoinWithin)
        {
            // The step before is of the same kind and just now: it already holds the project as
            // it was before the series. The series goes on from this change.
            _undo.Last.Value = last with { At = entry.At };
            return;
        }

        _undo.AddLast(entry);
        if (_undo.Count > MostSteps)
            _undo.RemoveFirst();
    }

    /// <summary>A step of the history: the project as it was before a change, and what the change did.</summary>
    internal sealed record Entry(ProjectContent Content, string What, string? Kind, long At);

    /// <summary>The time of a change of the project; see <see cref="Change"/>.</summary>
    public sealed class Scope : IDisposable
    {
        private readonly UndoHistory _history;
        private readonly Entry _entry;
        private readonly long _changesBefore;
        private bool _done;

        internal Scope(UndoHistory history, Entry entry, long changesBefore)
        {
            _history = history;
            _entry = entry;
            _changesBefore = changesBefore;
        }

        /// <summary>Ends the change: a step of the history if the project changed.</summary>
        public void Dispose()
        {
            if (_done)
                return;

            _done = true;
            try
            {
                _history.Finish(_entry, _changesBefore);
            }
            finally
            {
                _history._lock.Exit();
            }
        }
    }
}

/// <summary>What undo and redo would do now, for the UI.</summary>
/// <param name="Undo">What undo takes back, like "storing memory 5"; null when there is nothing to undo.</param>
/// <param name="Redo">What redo makes again; null when there is nothing to redo.</param>
/// <remarks>Public only because <see cref="IDeskClient"/> has to be.</remarks>
public sealed record HistoryState(string? Undo, string? Redo);
