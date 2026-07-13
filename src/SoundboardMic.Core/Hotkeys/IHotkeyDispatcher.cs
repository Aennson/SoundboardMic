namespace SoundboardMic.Core.Hotkeys;

/// <summary>Ação a executar quando uma combinação registrada é pressionada.</summary>
public delegate void HotkeyAction();

/// <summary>
/// Roteia combinações do hook global para ações registradas. Mantém a tabela
/// combinação → ação e (opcionalmente) uma combinação de pânico com prioridade.
/// Pensado para ser reconstruído a partir dos <c>Mapeamentos</c> do banco.
/// </summary>
public interface IHotkeyDispatcher : IDisposable
{
    /// <summary>Substitui todo o conjunto de atalhos ativos de uma vez.</summary>
    void SetBindings(IEnumerable<HotkeyBinding> bindings);

    /// <summary>Registra/atualiza a ação de uma combinação. Sobrescreve se já existir.</summary>
    void Bind(KeyCombo combo, HotkeyAction action);

    /// <summary>Remove o registro de uma combinação.</summary>
    void Unbind(KeyCombo combo);

    /// <summary>Define a tecla de pânico (executada com prioridade). Null remove.</summary>
    void SetPanicKey(KeyCombo? combo, HotkeyAction action);

    /// <summary>Combinações atualmente registradas (para checagem de conflito na UI).</summary>
    IReadOnlyCollection<KeyCombo> ActiveCombos { get; }
}

/// <summary>Uma associação combinação → ação.</summary>
/// <param name="Combo">Combinação de teclas.</param>
/// <param name="Action">Ação a executar quando pressionada.</param>
public record HotkeyBinding(KeyCombo Combo, HotkeyAction Action);
