# Etapa 5 — Interface WPF completa (MVVM)

**Status:** ✅ Concluída (build OK, 69/69 testes, app validado visualmente em execução real)

## Visão geral

Interface desktop moderna em WPF com MVVM (`CommunityToolkit.Mvvm`), injeção de dependência
(`Microsoft.Extensions.DependencyInjection`), logging (`Serilog` em arquivo) e bandeja do
sistema (`Hardcodet.NotifyIcon.Wpf`). Tema escuro Fluent com cor de destaque violeta→azul,
ícones vetoriais nativos (**Segoe Fluent Icons**), cantos arredondados, sombras e gradientes.

## Design system (`Themes/`)

- `Palette.xaml` — cores (superfícies, texto, destaque em gradiente, estados success/danger/
  warning), fonte de ícones, métricas de canto.
- `Controls.xaml` — estilos reutilizáveis: botão primário (gradiente + sombra), secundário,
  botão de ícone circular, botão de play, TextBox, ComboBox custom, Slider com preenchimento
  proporcional, toggle switch estilo iOS, botão de navegação e cartão base.
- `Converters.xaml` + `Converters/Converters.cs` — bool↔Visibility, inverso, string→Visibility,
  volume→percentual, preenchimento do slider (MultiBinding).

## Camada App (`Services/`, `ViewModels/`, `Views/`)

**Serviços**
- `SettingsService` — persiste `AppSettings` em JSON (%LOCALAPPDATA%\SoundboardMic).
- `StartupService` — auto-início via `HKCU\...\Run`.
- `SoundboardController` — **orquestrador central**: liga hook↔motor↔repositórios, recarrega
  hotkeys a partir do banco, aplica volumes/monitor em runtime, expõe status e erros.
- `DialogService` — seletor de arquivo, confirmação/aviso estilizados, editor de áudio.
- `HotkeyCapture` — converte tecla WPF → `KeyCombo`.
- `AppIcon` — gera o ícone do app em runtime (microfone em gradiente) para janela e bandeja.

**ViewModels**
- `MainViewModel` — lista, navegação (Sons/Config), status, comandos (adicionar/editar/
  excluir/tocar/toggle/pânico/motor), banner de erro, filtro de busca.
- `AudioItemViewModel` — linha da lista (nome, arquivo, duração, volume, chip de atalho, toggle).
- `AudioEditViewModel` — editor com preview, gravação de atalho e validação de conflito.
- `SettingsViewModel` — dispositivos, volumes, monitor, tecla de pânico, integração com Windows.

**Views**
- `MainWindow` — shell com barra de título custom (arrastar/min/max/fechar), sidebar com logo
  e navegação, header com busca e ações, banner de erro, bandeja com menu de contexto, e
  minimizar-para-bandeja no fechar.
- `SoundboardView` — lista de cards de áudio + estado vazio ilustrado.
- `SettingsView` — seções em cards: dispositivos (com detecção de VB-Cable), volumes, tecla
  de pânico, sistema (toggles).
- `AudioEditDialog` — editor modal com captura de atalho ao vivo (destaque animado).
- `MessageDialog` — confirmação/aviso estilizados.

## Fiação (DI + inicialização)

`App.xaml.cs` configura o container, inicializa o banco (`DatabaseBootstrapper`), abre a
janela e chama `MainViewModel.InitializeAsync()` — que carrega áudios, instala o hook global,
recarrega os bindings e inicia o motor (se `AutoStartEngine`). Exceções fatais e da UI são
logadas e mostradas ao usuário.

## Verificação visual (app em execução real)

Screenshots confirmaram:
- **Meus sons** — sidebar, header com Pânico/Parar injeção/Adicionar, estado vazio ilustrado,
  rodapé de status ("Injetando áudio", mic e CABLE Input detectados).
- **Configurações** — card verde "VB-Audio Virtual Cable detectado", combos de dispositivos,
  sliders de volume com preenchimento em gradiente.
- **Novo áudio** (dialog) — campos, preview de volume, gravação de atalho, Salvar desabilitado
  sem dados obrigatórios.

## Como testar

```powershell
dotnet build
dotnet run --project src/SoundboardMic.App
# ou o exe (sem console): src/SoundboardMic.App/bin/Debug/net8.0-windows/SoundboardMic.App.exe
```

## Pendências para a etapa 6

- README.md completo (pré-requisitos VB-Cable, compilação, configuração do app de chamada,
  troubleshooting de latência).
- Testes automatizados da UI/serviços do App (o `HotkeyCapture` e `SettingsViewModel` podem
  ganhar cobertura); tecla de pânico e monitoramento já implementados nas etapas 3–5.
- Polimento final.
