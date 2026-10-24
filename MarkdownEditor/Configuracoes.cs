using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MarkdownEditor
{
    // Preferências do usuário em %LocalAppData%\MarkdownEditor\configuracoes.json.
    // Arquivo ausente ou inválido = valores padrão (idioma do Windows).
    public sealed class Configuracoes
    {
        private static readonly string Arquivo = Path.Combine(PastaDados, "configuracoes.json");

        public static string PastaDados =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MarkdownEditor");

        // null = idioma do Windows.
        public string? Idioma { get; set; }

        public static Configuracoes Carregar()
        {
            try
            {
                if (File.Exists(Arquivo))
                    return JsonSerializer.Deserialize(File.ReadAllText(Arquivo), ContextoJsonConfiguracoes.Default.Configuracoes)
                        ?? new Configuracoes();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
            {
                Trace.TraceError(ex.ToString());
            }

            return new Configuracoes();
        }

        public void Salvar()
        {
            try
            {
                Directory.CreateDirectory(PastaDados);
                File.WriteAllText(Arquivo, JsonSerializer.Serialize(this, ContextoJsonConfiguracoes.Default.Configuracoes));
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                Trace.TraceError(ex.ToString());
            }
        }
    }

    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
    [JsonSerializable(typeof(Configuracoes))]
    internal sealed partial class ContextoJsonConfiguracoes : JsonSerializerContext
    {
    }
}
