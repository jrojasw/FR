using PrecioHerramientas.Core.Text;

namespace PrecioHerramientas.Tests;

public class TextNormalizerTests
{
    [Theory]
    [InlineData("$12.990", 12990)]
    [InlineData("$ 1.299.990", 1299990)]
    [InlineData("12990", 12990)]
    [InlineData("Precio: $49.990 c/u", 49990)]
    [InlineData("12.990,50", 12990.50)]
    [InlineData("12990.00", 12990)]
    [InlineData("1,299,990", 1299990)]
    public void ParseClpPrice_reconoce_formatos_chilenos(string text, decimal expected) =>
        Assert.Equal(expected, TextNormalizer.ParseClpPrice(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Consultar")]
    [InlineData("$0")]
    public void ParseClpPrice_devuelve_null_si_no_hay_precio(string? text) =>
        Assert.Null(TextNormalizer.ParseClpPrice(text));

    [Theory]
    [InlineData("7898935427473", "7898935427473")]
    [InlineData("088381077286", "0088381077286")]      // UPC-A → EAN-13
    [InlineData("07898935427473", "7898935427473")]    // GTIN-14 con indicador 0
    [InlineData("7898-9354-2747-3", "7898935427473")]
    public void NormalizeEan_acepta_codigos_validos(string raw, string expected) =>
        Assert.Equal(expected, TextNormalizer.NormalizeEan(raw));

    [Theory]
    [InlineData("7898935427478")]   // dígito verificador incorrecto
    [InlineData("12345")]
    [InlineData("0000000000000")]
    [InlineData("SKU-123")]
    public void NormalizeEan_rechaza_codigos_invalidos(string raw) =>
        Assert.Null(TextNormalizer.NormalizeEan(raw));

    [Fact]
    public void Slugify_quita_tildes_y_signos() =>
        Assert.Equal("taladro-percutor-inalambrico-20v-n-o", TextNormalizer.Slugify("Taladro Percutor Inalámbrico 20V – Ñ/Ó"));

    [Theory]
    [InlineData("DCD-778 D2", "DCD778D2")]
    [InlineData("tc-ac 200/50", "TCAC20050")]
    [InlineData("ABC", null)]
    [InlineData("  ", null)]
    public void NormalizeModel_compacta_y_exige_digitos(string raw, string? expected) =>
        Assert.Equal(expected, TextNormalizer.NormalizeModel(raw));

    [Fact]
    public void Tokenize_normaliza_terminos() =>
        Assert.Equal(["esmeril", "angular", "4", "1", "2"], TextNormalizer.Tokenize("Esmeril  ANGULAR 4 1/2\""));
}
