using NAudio.Wave;
using SoundboardMic.App.Services;
using SoundboardMic.App.ViewModels;
using SoundboardMic.Core.AudioEngine;
using SoundboardMic.Core.Models;
using SoundboardMic.Data;

namespace SoundboardMic.Tests;

public class AudioEditViewModelTests
{
    [Fact]
    public async Task EditAudio_SelectCreateAndRemoveCategory_PersistAndReopen()
    {
        using var db = new TestDatabase();
        var audios = new AudioRepository(db.Factory);
        var categories = new CategoriaRepository(db.Factory);
        var mappings = new MapeamentoRepository(db.Factory);
        var path = Path.Combine(Path.GetTempPath(), $"soundboard-edit-{Guid.NewGuid():N}.wav");
        try
        {
            using (var writer = new WaveFileWriter(path, new WaveFormat(48000, 16, 1)))
                writer.Write(new byte[960], 0, 960);
            var category = await categories.AddAsync(new Categoria { Nome = "Efeitos" });
            var audio = await audios.AddAsync(new Audio { Nome = "Teste", CaminhoArquivo = path });
            await audios.AddAsync(new Audio
            {
                Nome = "Anterior", CaminhoArquivo = path, CategoriaId = category.Id, Ordem = 5
            });

            async Task<AudioEditViewModel> OpenEditor() => new(audios, mappings, categories,
                new UnusedDialogs(), new PlaybackService(), new TestSettings(), new AudioFileCache(),
                await categories.GetAllAsync(), new AudioItemViewModel((await audios.GetByIdAsync(audio.Id))!, null));

            var editor = await OpenEditor();
            editor.SelecionarCategoriaCommand.Execute("Efeitos");
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Null(editor.Erro);
            Assert.True(editor.Salvou);
            var saved = await audios.GetByIdAsync(audio.Id);
            Assert.Equal(category.Id, saved!.CategoriaId);
            Assert.Equal(6, saved.Ordem);
            Assert.Equal("Efeitos", (await OpenEditor()).CategoriaTexto);

            editor = await OpenEditor();
            editor.CategoriaTexto = " Nova categoria ";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Null(editor.Erro);
            Assert.Equal("Nova categoria", (await OpenEditor()).CategoriaTexto);
            Assert.Equal(2, (await categories.GetAllAsync()).Count);

            editor = await OpenEditor();
            editor.CategoriaTexto = "";
            await editor.SaveCommand.ExecuteAsync(null);
            Assert.Null(editor.Erro);
            Assert.Null((await audios.GetByIdAsync(audio.Id))!.CategoriaId);
        }
        finally { File.Delete(path); }
    }

    private sealed class TestSettings : ISettingsService
    {
        public AppSettings Current { get; } = new();
        public void Save() { }
    }

    private sealed class UnusedDialogs : IDialogService
    {
        public string? PickAudioFile() => throw new NotSupportedException();
        public bool Confirm(string titulo, string mensagem) => throw new NotSupportedException();
        public void Info(string titulo, string mensagem) => throw new NotSupportedException();
        public bool ShowAudioEditor(AudioEditViewModel viewModel) => throw new NotSupportedException();
    }
}
