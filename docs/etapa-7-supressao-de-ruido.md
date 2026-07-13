# Etapa 7 — Supressão de ruído do microfone (RNNoise + Noise Gate)

**Status:** ✅ Concluída — Debug e Release compilam sem warnings, 96/96 testes

## O que foi feito

Dois estágios de tratamento de ruído aplicados **apenas ao ramo do microfone físico**
(os sons do soundboard e o monitoramento local não passam por eles), cada um com
liga/desliga próprio na tela de configurações, aplicado em tempo real e persistido
no `settings.json`:

1. **RNNoise** — supressão por rede neural (mesma base do "Noise Suppression" do
   OBS/Discord). Remove ruído de fundo (ventilador, teclado, chiado) mesmo enquanto
   você fala. Pacote NuGet `YellowDogMan.RRNoise.NET` (wrapper de xiph/rnnoise, com
   DLL nativa win-x64/x86 embutida).
2. **Noise gate** — em C# puro, sem dependências: silencia o microfone quando o nível
   fica abaixo do limiar, com attack de 5 ms (não corta o início das palavras), hold
   de 200 ms (não fecha entre sílabas) e release de 150 ms (fecha sem clique).
   Limiar fixo em −45 dB — constantes internas, sem sliders na UI.

## Nova cadeia do ramo do mic (`MicInjectionEngine`)

```
mic físico (WasapiCapture) → buffer → 48 kHz MONO → RNNoise → gate → estéreo → volume → mixer principal
```

- O ramo do mic agora é processado em **mono 48 kHz** (exigência do RNNoise; mic físico
  é mono na prática) e expandido para estéreo só no fim — resultado audível idêntico
  ao anterior quando os toggles estão desligados.
- **Toggles por bypass**: os dois providers ficam sempre na cadeia; `Enabled=false`
  vira pass-through. Nada de reconstruir o grafo com o `WasapiOut` vivo — mesmo padrão
  runtime do `MicVolume`.
- Ordem RNNoise → gate: o gate atua sobre o piso de ruído já reduzido, ficando mais estável.

## Arquivos novos (Core/AudioEngine)

| Arquivo | Responsabilidade |
|---|---|
| `NoiseGateSampleProvider.cs` | Gate por envelope exponencial (threshold/attack/hold/release), 100% testável |
| `IRnNoiseProcessor.cs` | Interface que isola o P/Invoke — testes usam fake sem a DLL nativa |
| `RnNoiseProcessor.cs` | Implementação real; `TryCreate` captura falha de load da DLL; FIFO interno garante "entra N, sai N" (warmup ≤ 480 samples = 10 ms) |
| `RnNoiseSampleProvider.cs` | ISampleProvider mono 48 kHz com bypass e `Reset()` do denoiser ao religar |

O wrapper do pacote já converte a escala de samples (±1 ↔ ±32768) e bufferiza frames
de 480 samples internamente — verificado pelos testes de integração.

## Robustez (plano B)

Se a DLL nativa do rnnoise **não carregar** (`RnNoiseProcessor.TryCreate` falha no
`Start`), o motor monta a cadeia só com o gate, `NoiseSuppressionAvailable` fica
`false`, o `SoundboardController` loga um `Log.Warning` e o checkbox da UI fica
desabilitado com a legenda "Indisponível: a biblioteca nativa não carregou neste
sistema". O app nunca quebra por causa do RNNoise.

## Mudanças em arquivos existentes

- **Core**: `MicInjectionOptions` e `IMicInjectionEngine` ganharam
  `NoiseSuppressionEnabled`, `NoiseGateEnabled` e `NoiseSuppressionAvailable`;
  `MicInjectionEngine` monta a nova cadeia no `Start` e libera o denoiser no `StopCore`.
- **App**: `AppSettings` (2 flags novas, default `false` — opt-in, settings antigos
  desserializam sem migração); `SoundboardController.StartEngine`/`ApplyRuntimeSettings`
  empurram os toggles; `SettingsViewModel` com persistência reativa; novo card
  **"Microfone"** no `SettingsView.xaml` com os dois toggles.

## Testes (74 → 96)

- `NoiseGateSampleProviderTests` (7): silêncio zerado, voz passa, rampa de attack sem
  degrau, hold+release, bypass, religar limpo, formato preservado.
- `RnNoiseSampleProviderTests` (6): com fake — processa/bypass/reset, validação de
  formato mono 48k.
- `RnNoiseIntegrationTests` (3): lib nativa real via `RnNoiseFactAttribute`
  (aparece como *skipped* se a DLL não carregar no ambiente): ruído branco tem energia
  reduzida, tom de voz não é aniquilado (pega escala errada), blocos não múltiplos de
  480 exercitam o FIFO.
- `MicChainCompositionTests` (4): réplica da cadeia do engine sem WASAPI.
- `SoundboardControllerTests` (+2): toggles chegam ao engine via options e em runtime.

## Como testar manualmente

1. `dotnet run --project src/SoundboardMic.App` com o VB-Cable instalado.
2. Em outro app (ou gravador do Windows), escute o **CABLE Output**.
3. Configurações → card **Microfone**:
   - Ambos desligados → comportamento idêntico ao anterior.
   - Só **Noise gate** → silêncio total nas pausas; a fala entra sem cortar o início.
   - Só **RNNoise** → ruído de fundo (ventilador/teclado) some, voz natural.
   - Ambos → pausas completamente limpas.
4. Alterne os toggles **com o motor rodando**: efeito imediato, sem clicks/pops.
5. Reinicie o app: os toggles persistem (`%LOCALAPPDATA%\SoundboardMic\settings.json`).
6. Sons do soundboard e monitoramento local seguem intactos com tudo ligado.
