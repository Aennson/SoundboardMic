# Etapa 8 — Barra rápida flutuante (QuickBar)

**Status:** ✅ Concluída — Debug e Release compilam sem warnings, 119/119 testes

## O que foi feito

Uma **barra flutuante de acesso rápido**: janela topmost sem borda com um botão por
áudio cadastrado, pensada para ficar ao lado do botão Iniciar do Windows e disparar
sons sem abrir a janela principal.

- **Orientação vertical ou horizontal** (menu de contexto na própria barra), posição
  livre por arrasto — tudo persistido no `settings.json`.
- Na horizontal, a **altura total da barra nunca excede a altura da taskbar**
  (medida em runtime: tela cheia − área de trabalho, com clamp 40–56 px e fallback
  48 px; botões dimensionados para caber).
- **Ícone e cor customizáveis por áudio**: catálogo curado de 32 glifos do Segoe
  Fluent Icons + paleta de 9 cores, escolhidos no editor de áudio (seção "Ícone e
  cor", com preview vivo no cabeçalho do dialog) e persistidos no SQLite.
- **Botão de pânico discreto** na ponta da barra (pequeno, cinza, vermelho no hover).
- **Destaque no botão do áudio em reprodução** (borda + glow) — o Core agora expõe
  *quais* arquivos estão tocando, não só a contagem.
- Mostrar/ocultar pela **tray** ("Barra rápida") e pelas **Configurações** (card
  Sistema), sempre em sincronia via `QuickBarService.VisibilidadeAlterada`.

## Decisões técnicas

- **`WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`** aplicados via `SetWindowLongPtr` no
  `OnSourceInitialized`: clicar na barra dispara o som **sem roubar o foco** do
  jogo/chamada ativa (essencial ao caso de uso) e a janela fica fora do Alt-Tab.
  Confirmado em teste: exstyle `0x08080088` = TOPMOST+NOACTIVATE+TOOLWINDOW+LAYERED.
- **Primeiro padrão de migração do SQLite**: `DatabaseBootstrapper.EnsureColumnAsync`
  (`PRAGMA table_info` + `ALTER TABLE ADD COLUMN`, idempotente). Colunas novas:
  `Audios.Icone TEXT NULL` (code-point hex, ex. `"E8D6"`) e `Audios.Cor TEXT NULL`
  (`"#RRGGBB"`). Null = padrão (nota musical, roxo accent).
- **Coleção compartilhada**: a QuickBar usa a MESMA `ObservableCollection` do
  `MainViewModel` (add/edit/delete refletem na hora), mas através de uma
  **`ListCollectionView` própria** — sem ela, o filtro de busca da janela principal
  (que atua na *default view*) filtraria também a barra.
- **Sons ativos por arquivo**: `ActiveSound` ganhou `FilePath`;
  `IMicInjectionEngine.GetActiveSoundPaths()` devolve snapshot (só do mix principal,
  sem duplicar o monitor); `SoundboardStatus.ActiveSoundPaths` propaga e
  `MainViewModel.UpdateStatus` seta `AudioItemViewModel.Tocando` — o mesmo flag
  alimenta o card da lista e o botão da barra.
- **Nunca `Close()`**: `OnClosing` da barra cancela e faz `Hide()` (janela WPF
  fechada não reabre); no `Application.Shutdown` o WPF ignora o cancel.
- **Glifos por code-point**: armazenados como hex no banco e convertidos em runtime
  (`CodepointToGlyphConverter`/`IconCatalog.GlyphChar`) — nenhum caractere PUA
  colado em XAML (regra do projeto).

## Arquivos novos

| Arquivo | Papel |
|---|---|
| `App\Services\IconCatalog.cs` | 32 glifos + 9 cores + defaults + conversão hex→char |
| `App\Services\QuickBarMetrics.cs` | Funções puras: altura da taskbar, tamanho de botão, clamp de posição |
| `App\Services\QuickBarService.cs` | Mostrar/ocultar/alternar + persistência + evento de sincronização |
| `App\ViewModels\QuickBarViewModel.cs` | Orientação, comandos, view própria da coleção |
| `App\Views\QuickBarWindow.xaml(.cs)` | Janela topmost, NOACTIVATE, drag, posição inicial |
| Converters `CodepointToGlyph`, `HexToBrush`, `IsEqual` | Ícone/cor/seleção |
| Estilo `QuickBarButton` (Controls.xaml) | Botão colorido com destaque "tocando" |

## Testes (96 → 119)

- `AudioRepositoryTests` (+3): round-trip de Icone/Cor, null e update.
- `DatabaseBootstrapperTests` (novo): banco antigo ganha as colunas (idempotente),
  linha antiga lê null e aceita update.
- `SoundboardControllerTests` (+2): status expõe paths ativos; vazio após stop-all.
- `QuickBarMetricsTests` (novo, 15 casos): taskbar típica/compacta/gigante/auto-hide,
  tamanho de botão, clamp nas 4 bordas, monitor secundário negativo.

## Como testar manualmente

1. Configurações → Sistema → **"Barra rápida flutuante"** (ou tray → "Barra rápida").
2. Arraste pela alça (⋯) para perto do botão Iniciar; botão direito → Horizontal/Vertical.
3. Edite um áudio e escolha ícone/cor — o botão na barra muda na hora.
4. Com um jogo/bloco de notas em foco, clique num botão: o som dispara **sem** a
   janela ativa perder o foco.
5. O botão fica com borda/glow branco enquanto o som toca; o ⏹ pequeno na ponta
   para tudo.
6. Reinicie o app: visibilidade, posição e orientação persistem.
