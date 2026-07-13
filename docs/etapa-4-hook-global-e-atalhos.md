# Etapa 4 — Hook global de teclado, parser de atalhos e dispatcher

**Status:** ✅ Concluída (build OK, 69/69 testes, smoke test do hook real em Windows OK)

## Mudança estrutural

Todos os projetos passaram de `net8.0` para **`net8.0-windows`** (Core, Data, Tests,
DevCli; App já era). O app é Windows-only (WASAPI + `SetWindowsHookEx`), então isso
mantém o P/Invoke limpo e alinha o alvo com a realidade.

## Arquivos novos (`src/SoundboardMic.Core/Hotkeys/`)

- `KeyCombo.cs` — combinação imutável: modificadores (`[Flags]`) + tecla canônica.
- `KeyComboParser.cs` — converte entre texto ("Ctrl+Alt+F1"), `KeyCombo` e virtual-key
  codes. Aceita aliases ("control", "esc", "pgup") e reordena para a forma canônica
  **Ctrl+Shift+Alt+Win+Tecla** (a string que vai para `Mapeamentos.Teclas` no banco).
  Suporta A–Z, 0–9, F1–F24, NumPad0–9, setas, e teclas de edição/navegação.
- `IGlobalKeyboardHook.cs` / `GlobalKeyboardHook.cs` — hook de baixo nível
  `WH_KEYBOARD_LL` via `SetWindowsHookEx`. Funciona com o app minimizado/sem foco.
  Monta o `KeyCombo` lendo os modificadores por `GetAsyncKeyState`. O handler pode
  marcar `Handled = true` para **consumir** a tecla (não vaza para o app em foco).
- `IHotkeyDispatcher.cs` / `HotkeyDispatcher.cs` — roteia combinações → ações.
  `SetBindings` reconstrói a tabela a partir dos `Mapeamentos`; `SetPanicKey` registra
  a tecla de pânico com **prioridade** sobre bindings normais. Ações rodam no
  `ThreadPool` (não na thread do hook), com try/catch para que uma ação defeituosa
  nunca derrube o hook.

## Detalhes de implementação relevantes

- O `delegate` do callback é mantido em campo para não ser coletado pelo GC enquanto
  o hook está instalado (causa clássica de crash em hooks .NET).
- Só teclas **principais** (não-modificadoras) geram um combo; a combinação só é
  consumida se estiver registrada, senão passa adiante normalmente.
- Combinações desconhecidas (multimídia/OEM) são ignoradas com segurança
  (`FromVirtualKey` retorna null).
- O hook exige uma fila de mensagens sendo bombeada na thread que o instalou. No app
  WPF isso é o dispatcher da UI (sempre ativo). No DevCli console há um `GetMessage`
  loop dedicado.

## Testes

28 novos testes (total 69):
- `KeyComboParserTests` — parse válido/inválido, normalização canônica, ida-e-volta
  vk↔combo, igualdade independente de origem, estabilidade da string do banco.
- `HotkeyDispatcherTests` — execução + consumo da tecla, prioridade do pânico,
  `SetBindings` substituindo o conjunto, unbind, `ActiveCombos`, e isolamento de
  exceções nas ações. Usa um `FakeHook` (sem win32).

## Como testar

```powershell
dotnet test        # 69 testes

# Hook global real (pressione mesmo com a janela sem foco):
dotnet run --project tools/SoundboardMic.DevCli -- hook
#   Ctrl+Alt+F1 e Ctrl+Shift+3 disparam ações; Ctrl+Alt+P = pânico; Ctrl+C sai
```

Além dos testes unitários, foi executado um smoke test que instala o hook real, roda um
message loop e sintetiza `Ctrl+Alt+F1` e `Ctrl+Alt+P` via `keybd_event`, confirmando que
o dispatcher dispara as ações corretas (a tecla de pânico tem prioridade).
