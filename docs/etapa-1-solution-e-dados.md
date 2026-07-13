# Etapa 1 — Solution, projetos e camada de dados SQLite

**Status:** ✅ Concluída (build OK, 21/21 testes passando)

## Estrutura criada

```
SoundboardMic.sln
├── src/SoundboardMic.Core        (domínio, net8.0)
│   ├── Models/Audio.cs           — entidade Audios
│   ├── Models/Mapeamento.cs      — entidade Mapeamentos
│   └── Repositories/             — IAudioRepository, IMapeamentoRepository,
│                                   TeclasDuplicadasException
├── src/SoundboardMic.Data        (SQLite via Microsoft.Data.Sqlite 10.0.9)
│   ├── SqliteConnectionFactory.cs — conexões com FK habilitada; DB padrão em
│   │                                %LOCALAPPDATA%\SoundboardMic\soundboard.db
│   ├── DatabaseBootstrapper.cs    — cria as tabelas na primeira execução (idempotente)
│   ├── AudioRepository.cs         — CRUD completo de Audios
│   └── MapeamentoRepository.cs    — CRUD completo de Mapeamentos
├── src/SoundboardMic.App         (WPF net8.0-windows — template, preenchido na etapa 5)
└── tests/SoundboardMic.Tests     (xUnit, 21 testes)
```

## Schema do banco

- `Audios(Id PK AI, Nome NOT NULL, CaminhoArquivo NOT NULL, DuracaoMs, VolumePadrao DEFAULT 1.0, CriadoEm ISO 8601)`
- `Mapeamentos(Id PK AI, AudioId FK → Audios ON DELETE CASCADE, Teclas UNIQUE, Ativo DEFAULT 1, CriadoEm ISO 8601)`
- Índice `IX_Mapeamentos_AudioId`

## Decisões de implementação

- Violação de `UNIQUE(Teclas)` é convertida em `TeclasDuplicadasException` (exceção de
  domínio) em vez de vazar `SqliteException` — a UI usará isso na validação de conflitos.
- `CriadoEm` gravado em UTC no formato round-trip ISO 8601 (`"O"`); leitura preserva
  `DateTimeKind.Utc`.
- Repositórios 100% async; uma conexão por operação (pooling do driver cuida do resto).
- Testes usam banco SQLite temporário em arquivo, isolado por teste e apagado no dispose.
  Cobertura: CRUD, validações de campos obrigatórios, ordenação, unicidade de teclas,
  FK inválida e cascade delete.

## Como testar

```powershell
dotnet build   # compila a solution inteira
dotnet test    # roda os testes → Passed: 21
```
