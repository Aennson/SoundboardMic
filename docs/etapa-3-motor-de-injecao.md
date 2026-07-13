# Etapa 3 — Motor de injeção: captura do mic + mixing + CABLE Input

**Status:** ✅ Concluída (build OK, 41/41 testes, smoke test do motor em hardware real OK)

## Grafo de áudio implementado

```
mic físico (WasapiCapture, 50 ms) → BufferedWaveProvider → resample/canais → volume ─┐
                                                                                     ├─ mixer principal ─→ CABLE Input (WasapiOut shared, 50 ms)
sons do soundboard → resample/canais → volume individual ─→ mixer de sons ── master ─┘
                                                        └─→ mixer de monitor ── master ─→ fones (WasapiOut, opcional)
```

Formato interno do mix: **float 32-bit, 48 kHz, estéreo**. Qualquer entrada divergente
(mp3 mono 44.1k, mic array 4 canais etc.) passa por `WdlResamplingSampleProvider` +
conversão de canais automática (`SampleProviderConverter`).

## Arquivos novos (`src/SoundboardMic.Core/AudioEngine/`)

- `MicInjectionEngine.cs` — o motor (implementa `IMicInjectionEngine`)
- `IMicInjectionEngine.cs` — contrato + `EngineStoppedEventArgs`
- `MicInjectionOptions.cs` — dispositivos, volumes e monitor iniciais
- `SampleProviderConverter.cs` — normalização de formato para o mix

## Capacidades do motor

- **Sons simultâneos**: cada disparo vira uma entrada independente no sub-mixer de sons.
- **Pânico** (`StopAllSounds`): remove todas as entradas de som dos mixers **sem tocar
  no microfone** (por isso o sub-mixer separado).
- **Monitoramento local**: liga/desliga em tempo real (`MonitorEnabled`); usa um reader
  próprio por som (um stream não pode alimentar dois outputs) e uma saída WASAPI própria.
- **Volumes em tempo real**: `MicVolume` e `SoundboardVolume` (0–2, clampados) ajustam
  os `VolumeSampleProvider` sem reiniciar o motor.
- **Latência baixa**: WASAPI shared com event sync, captura 50 ms, saída 50 ms, buffer
  do mic 500 ms com `DiscardOnBufferOverflow` (nunca trava a captura).
- **Robustez**: `StoppedUnexpectedly` disparado quando dispositivo é desconectado
  (capture ou render); `Start` faz rollback completo se qualquer peça falhar;
  `ActiveSoundsChanged` para o indicador visual da UI (etapa 5).

## Como testar

```powershell
dotnet test        # 41 testes (8 novos no SampleProviderConverterTests)

# Teste interativo de ponta a ponta (instale o VB-Cable antes; sem ele o comando
# usa a saída padrão — use fones para evitar feedback):
dotnet run --project tools/SoundboardMic.DevCli -- inject "C:\caminho\som.mp3" --monitor
#   [Enter] toca o som   [s] pânico   [m] liga/desliga monitor   [q] sai

# Com VB-Cable instalado: selecione "CABLE Output" como microfone no Discord/Teams/
# Meet/Zoom e fale — os participantes ouvem seu mic + os sons disparados.
```

Smoke test não interativo executado nesta máquina (mic mutado, tom de 440 Hz a volume
baixo na saída padrão): Start → 2 sons simultâneos → pânico → término natural → Stop →
reinício, tudo OK.

## Observações

- Sem o VB-Cable instalado o motor funciona normalmente apontando para qualquer saída
  (útil para testar), mas a injeção real em chamadas exige o driver.
- O monitor reproduz apenas os **sons do soundboard** nos fones (não o mic — o usuário
  já se ouve naturalmente).
