# 🎙️ SoundboardMic

Soundboard desktop para Windows que **injeta áudio no seu microfone**: você cadastra sons,
associa cada um a uma tecla de atalho global e, ao pressioná-la, o áudio é mixado com o seu
microfone e enviado a um microfone virtual — de modo que todos numa chamada (Discord, Teams,
Google Meet, Zoom) ouçam como se você tivesse falado.

Construído em **.NET 8 / WPF** com **NAudio** (WASAPI), **SQLite** e hook global de teclado.

---

## ✨ Recursos

- 🎵 **Cadastro de áudios** (.mp3, .wav, .ogg) com nome amigável, preview e volume individual.
- ⌨️ **Atalhos globais personalizáveis** (ex.: `Ctrl+Alt+F1`) que funcionam mesmo com o app
  minimizado ou sem foco.
- 🎚️ **Mixagem em tempo real** do microfone físico + sons do soundboard, com resampling
  automático e latência baixa.
- 🔴 **Tecla de pânico** global que interrompe imediatamente todos os sons.
- 🎧 **Monitoramento local**: ouça os sons também nos seus fones enquanto injeta.
- 🤫 **Supressão de ruído do microfone**: RNNoise (rede neural) e/ou noise gate,
  cada um com liga/desliga próprio — só o mic é tratado, os sons ficam intactos.
- 🎛️ Seleção de dispositivos (mic, saída virtual, fones), volumes de mic vs. soundboard.
- 🗂️ **Bandeja do sistema** — hotkeys continuam ativas com a janela fechada.
- 🚀 Iniciar com o Windows, indicadores de status e detecção do VB-Cable.

---

## 📋 Pré-requisitos

### 1. .NET 8 Desktop Runtime
Necessário para executar. Baixe em
<https://dotnet.microsoft.com/download/dotnet/8.0> (escolha **.NET Desktop Runtime 8.x**).
Para compilar, instale o **.NET 8 SDK**.

### 2. VB-Audio Virtual Cable (obrigatório para injeção)
O app envia o áudio mixado para o dispositivo **"CABLE Input"** criado por este driver.

1. Baixe em <https://vb-audio.com/Cable/> (gratuito / doationware).
2. Extraia o ZIP e execute **`VBCABLE_Setup_x64.exe` como administrador**.
3. Clique em **Install Driver** e **reinicie o computador**.
4. Após reiniciar, o Windows terá dois novos dispositivos:
   - **CABLE Input** (uma *saída* — é onde o SoundboardMic escreve o áudio);
   - **CABLE Output** (uma *entrada* — é o "microfone" que os apps de chamada vão ouvir).

O SoundboardMic detecta o CABLE Input automaticamente e mostra um aviso claro nas
Configurações caso ele não esteja instalado.

---

## 🚀 Como compilar e executar

```powershell
# Na raiz do repositório
dotnet build                              # compila a solution
dotnet test                               # roda os testes unitários
dotnet run --project src/SoundboardMic.App  # executa o app
```

O executável final fica em
`src/SoundboardMic.App/bin/Debug/net8.0-windows/SoundboardMic.App.exe`.

Para uma build de distribuição:

```powershell
dotnet publish src/SoundboardMic.App -c Release -r win-x64 --self-contained false -o dist
```

---

## 🎧 Configuração de ponta a ponta

1. **Instale o VB-Cable** (ver acima) e reinicie.
2. Abra o **SoundboardMic** e vá em **Configurações**:
   - **Microfone (entrada)**: seu microfone físico.
   - **Saída virtual — CABLE Input**: selecione **CABLE Input (VB-Audio Virtual Cable)**.
   - **Monitoramento local — fones** (opcional): seus fones, se quiser ouvir os sons.
3. Volte para **Meus sons**, clique em **Adicionar** e cadastre um áudio:
   - Escolha o arquivo, dê um nome, ajuste o volume.
   - Clique em **Gravar** e pressione a combinação desejada (ex.: `Ctrl+Alt+1`).
   - **Salvar**.
4. Verifique se a injeção está ligada (botão **Iniciar injeção** / indicador verde
   "Injetando áudio").
5. **No seu app de chamada** (Discord/Teams/Meet/Zoom), abra as configurações de voz e
   selecione **CABLE Output (VB-Audio Virtual Cable)** como **microfone de entrada**.
6. Fale normalmente (o app repassa seu microfone) e pressione seus atalhos para disparar os
   sons — todos na chamada ouvem o resultado mixado.

> 💡 Configure a **tecla de pânico** (Configurações → Tecla de pânico) para cortar qualquer
> som instantaneamente.

### Configuração por aplicativo

| App          | Onde selecionar o microfone                                             |
|--------------|-------------------------------------------------------------------------|
| **Discord**  | Configurações do Usuário → Voz e Vídeo → Dispositivo de entrada          |
| **MS Teams** | ⋯ → Configurações → Dispositivos → Microfone                            |
| **Google Meet** | Ícone de engrenagem → Áudio → Microfone (no navegador)               |
| **Zoom**     | Configurações → Áudio → Microfone                                       |

Em todos, escolha **CABLE Output (VB-Audio Virtual Cable)**.

---

## 🏗️ Arquitetura

Solução em camadas:

| Projeto | Responsabilidade |
|---------|------------------|
| `SoundboardMic.Core` | Domínio + serviços de áudio (NAudio) + hotkeys (P/Invoke) |
| `SoundboardMic.Data` | Repositórios SQLite (`Microsoft.Data.Sqlite`) |
| `SoundboardMic.App`  | Interface WPF (MVVM com `CommunityToolkit.Mvvm`) |
| `SoundboardMic.Tests`| Testes unitários (xUnit) |
| `SoundboardMic.DevCli`| Utilitário de linha de comando para testar áudio/hotkeys |

**Grafo de áudio** (motor de injeção, `Core/AudioEngine/MicInjectionEngine`):

```
mic físico (WasapiCapture) → buffer → 48k mono → rnnoise → gate → estéreo → volume ─┐
                                                                                    ├─ mixer → CABLE Input (WasapiOut)
sons do soundboard → resample/canais → volume ── mixer de sons ─────────────────────┘
                                              └─→ mixer de monitor → fones (opcional)
```

O mix interno é float 32-bit, 48 kHz, estéreo; qualquer formato de mic/arquivo é convertido
automaticamente. O hook global (`Core/Hotkeys/GlobalKeyboardHook`) usa `SetWindowsHookEx`
(`WH_KEYBOARD_LL`) e roteia as combinações para o motor via `HotkeyDispatcher`.

Dados persistidos em `%LOCALAPPDATA%\SoundboardMic\`:
- `soundboard.db` — SQLite (áudios e mapeamentos);
- `settings.json` — configurações;
- `logs/` — logs do Serilog (rotação diária, 7 dias).

### CLI de desenvolvimento

```powershell
# Lista dispositivos e detecta o VB-Cable
dotnet run --project tools/SoundboardMic.DevCli -- devices

# Toca um arquivo em um dispositivo
dotnet run --project tools/SoundboardMic.DevCli -- play "C:\som.mp3" --volume 0.8

# Testa a injeção (mic + som) de ponta a ponta
dotnet run --project tools/SoundboardMic.DevCli -- inject "C:\som.mp3" --monitor

# Testa o hook global de teclado
dotnet run --project tools/SoundboardMic.DevCli -- hook
```

---

## 🛠️ Troubleshooting

**"VB-Cable não encontrado" nas Configurações**
Instale o VB-Audio Virtual Cable e reinicie. Confirme em *Configurações de Som do Windows*
que "CABLE Input" e "CABLE Output" aparecem.

**Ninguém na chamada ouve os sons**
Verifique se selecionou **CABLE Output** como microfone no app de chamada e se a injeção
está ligada (indicador "Injetando áudio").

**Não ouço meu próprio microfone/sons**
Isso é esperado — o app envia tudo para o CABLE, não para seus fones. Ative o
**monitoramento local** nas Configurações e escolha seus fones para ouvir os sons.

**Latência / atraso no áudio**
- Use fones (evita eco/microfonia entre alto-falantes e microfone).
- Feche outros apps que disputam o dispositivo de áudio em modo exclusivo.
- Mantenha os drivers de áudio atualizados. O app usa WASAPI em modo *shared* com buffers
  curtos (~50 ms); a latência real depende do driver do seu dispositivo.

**As hotkeys não disparam**
- Confirme que o mapeamento está **ativo** (toggle na lista).
- Alguns jogos em tela cheia exclusiva podem capturar o teclado antes do hook; rode em
  janela/borderless nesses casos.
- Combinações reservadas pelo Windows (ex.: `Ctrl+Alt+Del`) não podem ser capturadas.

**Áudio distorcido/estourado**
Reduza o volume do soundboard e/ou do mic nas Configurações — a soma dos volumes pode
saturar. Valores em torno de 80–100% costumam ser seguros.

---

## 📄 Licença

Projeto pessoal. O VB-Audio Virtual Cable é um software de terceiros com seus próprios termos
(donationware) — veja <https://vb-audio.com/Cable/>.
