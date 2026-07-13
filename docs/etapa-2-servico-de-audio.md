# Etapa 2 — Serviço de áudio: reprodução em dispositivo escolhido

**Status:** ✅ Concluída (build OK, 33/33 testes passando, smoke test de dispositivos OK)

## O que foi adicionado

Pacotes no `SoundboardMic.Core`: **NAudio 2.3.0** e **NAudio.Vorbis 1.5.0** (suporte a .ogg).

```
src/SoundboardMic.Core/AudioEngine/
├── AudioDeviceInfo.cs              — record (Id, Nome, Kind, PadraoDoSistema)
├── IAudioDeviceService.cs          — contrato de enumeração de dispositivos
├── AudioDeviceService.cs           — WASAPI via MMDeviceEnumerator; FindCableInput()
│                                     detecta o "CABLE Input" do VB-Audio
├── AudioFileDecoder.cs             — abre .mp3/.wav (AudioFileReader) e .ogg
│                                     (VorbisWaveReader); GetDurationMs() para o cadastro
├── IPlaybackService.cs             — contrato de reprodução simples (preview/teste)
├── PlaybackService.cs              — WasapiOut shared (100 ms) + VolumeSampleProvider
├── AudioDeviceNotFoundException.cs — dispositivo salvo não existe mais
└── FormatoNaoSuportadoException.cs — extensão desconhecida ou arquivo corrompido

tools/SoundboardMic.DevCli/         — CLI de teste manual (sem UI)
```

> O namespace é `SoundboardMic.Core.AudioEngine` (não `...Core.Audio`) para não colidir
> com a entidade `Audio` do domínio.

## Decisões de implementação

- `PlaybackService` toca **um arquivo por vez** (preview local). O mixing contínuo
  mic + soundboard é o motor da etapa 3.
- Volume linear clampado em `[0, 2]` via `VolumeSampleProvider`.
- Erros viram exceções de domínio claras: `FileNotFoundException` (arquivo movido),
  `FormatoNaoSuportadoException` (extensão/conteúdo inválido),
  `AudioDeviceNotFoundException` (dispositivo desconectado).
- `PlaybackService` é thread-safe (lock) e limpa `WasapiOut`/reader/MMDevice ao parar,
  trocar de áudio ou descartar.
- Dispositivo padrão do sistema vem primeiro na listagem, com flag `PadraoDoSistema`.

## Testes

12 novos testes em `AudioFileDecoderTests` (sem depender de hardware): whitelist de
extensões, arquivo inexistente, extensão inválida, WAV corrompido, decodificação de WAV
gerado em runtime (float 32-bit) e probe de duração com tolerância de 10 ms.

## Como testar

```powershell
dotnet test                                          # 33 testes

# Lista dispositivos e verifica se o VB-Cable está instalado:
dotnet run --project tools/SoundboardMic.DevCli -- devices

# Toca um arquivo no dispositivo padrão (ou --device N da lista acima):
dotnet run --project tools/SoundboardMic.DevCli -- play "C:\caminho\som.mp3" --volume 0.8
```

## Pendência para a etapa 3

⚠️ O VB-Audio Virtual Cable **não está instalado** nesta máquina (o `devices` reporta
ausente). Instalar de https://vb-audio.com/Cable/ antes de testar a injeção no mic.
