#if NET48
// Só compila na perna net48 — PXToolBoxDev.Integrations só existe lá. Testa
// Integrations/OrderContextPathLoader.cs (carregar o contexto de produção a partir de um
// CAMINHO colado — ver Production/OrderContextPathDialog) com arquivos TEMPORÁRIOS reais,
// mesmo padrão de AtomicFileWriterTests (nunca mocka File.Exists/ReadAllText).
using System;
using System.IO;
using PXToolBoxDev.Integrations;
using Xunit;

namespace PXToolBox.Tests
{
    public class OrderContextPathLoaderTests : IDisposable
    {
        private readonly string _tempDir =
            Path.Combine(Path.GetTempPath(), $"pxtoolbox-pathloader-{Guid.NewGuid():N}");

        private const string ValidJson = @"{
            ""formatId"": ""NexorOrderContext"",
            ""schemaVersion"": 1,
            ""exportedAtUtc"": ""2026-10-30T12:00:00Z"",
            ""source"": { ""rollId"": 1 },
            ""job"": { ""jobHash"": ""abc"", ""pedidoLabel"": ""Qualquer"" }
        }";

        public OrderContextPathLoaderTests()
        {
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }

        [Fact]
        public void Load_EmptyPath_Fails()
        {
            var result = OrderContextPathLoader.Load("");
            Assert.False(result.IsValid);
            Assert.Contains("Informe o caminho", result.FailureReason);
        }

        [Fact]
        public void Load_WhitespaceOnlyPath_Fails()
        {
            var result = OrderContextPathLoader.Load("   ");
            Assert.False(result.IsValid);
        }

        [Fact]
        public void Load_NonExistentPath_Fails()
        {
            var path = Path.Combine(_tempDir, "nao-existe.json");
            var result = OrderContextPathLoader.Load(path);
            Assert.False(result.IsValid);
            Assert.Contains("não encontrado", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_DirectoryInsteadOfFile_FailsWithClearMessage()
        {
            var result = OrderContextPathLoader.Load(_tempDir);
            Assert.False(result.IsValid);
            Assert.Contains("pasta", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        public void Load_UnreadableFile_FailsWithoutThrowing()
        {
            // Simula "arquivo ilegível" mantendo o arquivo aberto com acesso exclusivo —
            // ReadAllText falha com IOException (arquivo em uso), sem precisar de
            // permissões reais de ACL (frágeis em CI).
            var path = Path.Combine(_tempDir, "trancado.json");
            using (var locked = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                var result = OrderContextPathLoader.Load(path);
                Assert.False(result.IsValid);
                Assert.Contains("Não foi possível ler", result.FailureReason);
            }
        }

        [Fact]
        public void Load_IncompatibleJson_FailsWithValidatorReason()
        {
            var path = Path.Combine(_tempDir, "incompativel.json");
            File.WriteAllText(path, @"{""formatId"":""OutroFormato""}");

            var result = OrderContextPathLoader.Load(path);
            Assert.False(result.IsValid);
            Assert.NotNull(result.FailureReason);
        }

        [Fact]
        public void Load_ValidFile_Succeeds()
        {
            var path = Path.Combine(_tempDir, "valido.json");
            File.WriteAllText(path, ValidJson);

            var result = OrderContextPathLoader.Load(path);

            Assert.True(result.IsValid);
            Assert.NotNull(result.Context);
            Assert.Equal(1, result.Context!.RollId);
            Assert.Equal(path, result.NormalizedPath);
        }

        [Fact]
        public void Load_EquivalentToDirectlyValidatingTheSameContent_AsTheFileSelectorDoes()
        {
            // O seletor de arquivo existente (ControlUI.OnLoadOrderContextClick) lê o texto e
            // chama OrderContextService.Validate diretamente. Colar o caminho precisa chegar
            // ao MESMO resultado pro MESMO conteúdo — nunca uma segunda validação divergente.
            var path = Path.Combine(_tempDir, "valido.json");
            File.WriteAllText(path, ValidJson);

            var viaPath = OrderContextPathLoader.Load(path);
            var viaDirectValidation = OrderContextService.Validate(ValidJson);

            Assert.True(viaPath.IsValid);
            Assert.True(viaDirectValidation.IsValid);
            Assert.Equal(viaDirectValidation.Context!.RollId, viaPath.Context!.RollId);
            Assert.Equal(viaDirectValidation.Context.JobHash, viaPath.Context.JobHash);
            Assert.Equal(viaDirectValidation.Context.PedidoLabel, viaPath.Context.PedidoLabel);
        }

        [Fact]
        public void Load_PathWithSpacesAndAccents_Succeeds()
        {
            var subdir = Path.Combine(_tempDir, "pasta com espaço e açentuação");
            Directory.CreateDirectory(subdir);
            var path = Path.Combine(subdir, "joão-contexto-produção.json");
            File.WriteAllText(path, ValidJson);

            var result = OrderContextPathLoader.Load(path);

            Assert.True(result.IsValid);
        }

        [Fact]
        public void Load_PathWithOuterQuotes_StripsThemAndSucceeds()
        {
            // "Copiar como caminho" do Explorer envolve o caminho inteiro em aspas.
            var path = Path.Combine(_tempDir, "valido.json");
            File.WriteAllText(path, ValidJson);

            var result = OrderContextPathLoader.Load($"\"{path}\"");

            Assert.True(result.IsValid);
            Assert.Equal(path, result.NormalizedPath);
        }

        [Fact]
        public void Load_PathWithSurroundingWhitespace_IsTrimmed()
        {
            var path = Path.Combine(_tempDir, "valido.json");
            File.WriteAllText(path, ValidJson);

            var result = OrderContextPathLoader.Load($"   {path}   ");

            Assert.True(result.IsValid);
            Assert.Equal(path, result.NormalizedPath);
        }

        [Fact]
        public void NormalizePath_UnbalancedQuote_NeverStripsAsymmetrically()
        {
            // Só uma aspa (colagem malformada) — nunca remove de forma assimétrica.
            var result = OrderContextPathLoader.NormalizePath("\"C:\\temp\\arquivo.json");
            Assert.StartsWith("\"", result);
        }

        [Fact]
        public void Load_TextThatLooksLikeAUrl_NeverAttemptsNetworkAccess_JustFailsAsNotFound()
        {
            // Nunca interpreta como comando/URL — só File.Exists, que dá false pra isto,
            // exatamente como qualquer outro caminho inexistente (nenhuma chamada de rede).
            var result = OrderContextPathLoader.Load("https://example.com/contexto.json");
            Assert.False(result.IsValid);
            Assert.Contains("não encontrado", result.FailureReason, StringComparison.OrdinalIgnoreCase);
        }
    }
}
#endif
