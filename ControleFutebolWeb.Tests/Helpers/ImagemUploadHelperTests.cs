using ControleFutebolWeb.Helpers;

namespace ControleFutebolWeb.Tests.Helpers
{
    // O upload grava arquivos dentro de wwwroot, servidos pelo próprio domínio —
    // aceitar o arquivo errado aqui vira conteúdo ativo na mesma origem do cookie
    // de sessão. Estes testes travam a checagem por conteúdo (magic bytes).
    public class ImagemUploadHelperTests
    {
        private static byte[] Cabecalho(params byte[] bytes)
        {
            var buffer = new byte[ImagemUploadHelper.BytesCabecalho];
            bytes.CopyTo(buffer, 0);
            return buffer;
        }

        private static byte[] Jpeg() => Cabecalho(0xFF, 0xD8, 0xFF, 0xE0);
        private static byte[] Png() => Cabecalho(0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A);
        private static byte[] Gif() => Cabecalho((byte)'G', (byte)'I', (byte)'F', (byte)'8', (byte)'9', (byte)'a');

        private static byte[] Webp()
        {
            var b = Cabecalho((byte)'R', (byte)'I', (byte)'F', (byte)'F');
            b[8] = (byte)'W'; b[9] = (byte)'E'; b[10] = (byte)'B'; b[11] = (byte)'P';
            return b;
        }

        [Fact]
        public void DetectarFormato_ReconheceFormatosAceitos()
        {
            Assert.Equal("jpg", ImagemUploadHelper.DetectarFormato(Jpeg()));
            Assert.Equal("png", ImagemUploadHelper.DetectarFormato(Png()));
            Assert.Equal("gif", ImagemUploadHelper.DetectarFormato(Gif()));
            Assert.Equal("webp", ImagemUploadHelper.DetectarFormato(Webp()));
        }

        [Fact]
        public void DetectarFormato_SvgRecusado()
        {
            // SVG é XML e pode conter <script> — recusado de propósito.
            var svg = System.Text.Encoding.ASCII.GetBytes("<svg xmlns=");
            Assert.Null(ImagemUploadHelper.DetectarFormato(svg));
        }

        [Fact]
        public void DetectarFormato_ExecutavelRecusado()
        {
            // "MZ" — cabeçalho de .exe/.dll do Windows.
            Assert.Null(ImagemUploadHelper.DetectarFormato(Cabecalho((byte)'M', (byte)'Z', 0x90, 0x00)));
        }

        [Fact]
        public void DetectarFormato_HtmlRecusado()
        {
            var html = System.Text.Encoding.ASCII.GetBytes("<!DOCTYPE ht");
            Assert.Null(ImagemUploadHelper.DetectarFormato(html));
        }

        [Fact]
        public void Validar_ExecutavelRenomeadoComoJpg_ContinuaRecusado()
        {
            // O nome do arquivo não participa da decisão: só o conteúdo.
            var (ok, erro, ext) = ImagemUploadHelper.Validar(Cabecalho((byte)'M', (byte)'Z'), 1024);
            Assert.False(ok);
            Assert.Null(ext);
            Assert.Contains("Formato não suportado", erro);
        }

        [Fact]
        public void Validar_ImagemValida_Aceita()
        {
            var (ok, erro, ext) = ImagemUploadHelper.Validar(Png(), 500_000);
            Assert.True(ok);
            Assert.Null(erro);
            Assert.Equal("png", ext);
        }

        [Fact]
        public void Validar_AcimaDoLimite_Recusa()
        {
            var (ok, erro, _) = ImagemUploadHelper.Validar(Jpeg(), ImagemUploadHelper.TamanhoMaximoBytes + 1);
            Assert.False(ok);
            Assert.Contains("muito grande", erro);
        }

        [Fact]
        public void Validar_ArquivoVazio_Recusa()
        {
            var (ok, erro, _) = ImagemUploadHelper.Validar(Jpeg(), 0);
            Assert.False(ok);
            Assert.Contains("vazio", erro);
        }

        [Fact]
        public void GerarCaminhoRelativo_ParticionaPorAnoMesEUsaNomeAleatorio()
        {
            var data = new DateTime(2026, 3, 9, 0, 0, 0, DateTimeKind.Utc);
            var a = ImagemUploadHelper.GerarCaminhoRelativo("png", data);
            var b = ImagemUploadHelper.GerarCaminhoRelativo("png", data);

            Assert.StartsWith("uploads/blog/2026/03/", a);
            Assert.EndsWith(".png", a);
            Assert.NotEqual(a, b); // nome sempre novo: nunca sobrescreve outro upload
        }
    }
}
