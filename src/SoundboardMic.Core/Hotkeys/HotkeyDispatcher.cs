namespace SoundboardMic.Core.Hotkeys;

/// <summary>
/// Liga um <see cref="IGlobalKeyboardHook"/> a uma tabela de ações. As ações rodam
/// em uma thread de trabalho (não na thread do hook) para não atrasar a fila de
/// mensagens do Windows nem arriscar reentrância no callback de baixo nível.
/// </summary>
public sealed class HotkeyDispatcher : IHotkeyDispatcher
{
    private readonly IGlobalKeyboardHook _hook;
    private readonly object _lock = new();
    private readonly Dictionary<KeyCombo, HotkeyAction> _bindings = new();

    private KeyCombo? _panicCombo;
    private HotkeyAction? _panicAction;
    private bool _disposed;

    public HotkeyDispatcher(IGlobalKeyboardHook hook)
    {
        _hook = hook;
        _hook.HotkeyPressed += OnHotkeyPressed;
    }

    public IReadOnlyCollection<KeyCombo> ActiveCombos
    {
        get
        {
            lock (_lock)
            {
                var combos = new List<KeyCombo>(_bindings.Keys);
                if (_panicCombo is not null)
                    combos.Add(_panicCombo);
                return combos;
            }
        }
    }

    public void SetBindings(IEnumerable<HotkeyBinding> bindings)
    {
        lock (_lock)
        {
            _bindings.Clear();
            foreach (var b in bindings)
                _bindings[b.Combo] = b.Action;
        }
    }

    public void Bind(KeyCombo combo, HotkeyAction action)
    {
        lock (_lock)
            _bindings[combo] = action;
    }

    public void Unbind(KeyCombo combo)
    {
        lock (_lock)
            _bindings.Remove(combo);
    }

    public void SetPanicKey(KeyCombo? combo, HotkeyAction action)
    {
        lock (_lock)
        {
            _panicCombo = combo;
            _panicAction = combo is null ? null : action;
        }
    }

    private void OnHotkeyPressed(object? sender, HotkeyPressedEventArgs e)
    {
        HotkeyAction? action = null;

        lock (_lock)
        {
            // Pânico tem prioridade sobre qualquer binding normal.
            if (_panicCombo is not null && _panicCombo == e.Combo)
                action = _panicAction;
            else if (_bindings.TryGetValue(e.Combo, out var found))
                action = found;
        }

        if (action is null)
            return;

        // Combinação reconhecida: consome a tecla para não vazar ao app em foco...
        e.Handled = true;

        // ...e executa fora da thread do hook.
        var toRun = action;
        ThreadPool.QueueUserWorkItem(_ =>
        {
            try { toRun(); }
            catch { /* uma ação com defeito não pode derrubar o hook */ }
        });
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _hook.HotkeyPressed -= OnHotkeyPressed;
        lock (_lock)
        {
            _bindings.Clear();
            _panicCombo = null;
            _panicAction = null;
        }
    }
}
