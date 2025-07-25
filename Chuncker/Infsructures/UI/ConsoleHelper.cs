namespace Chuncker.Infsructures.UI
{
    /// <summary>
    /// Konsol kullanıcı arayüzü için yardımcı sınıf
    /// </summary>
    public static class ConsoleHelper
    {
        /// <summary>
        /// Başlık çizgisi oluşturur
        /// </summary>
        /// <param name="title">Başlık metni</param>
        /// <param name="width">Genişlik</param>
        public static void WriteHeader(string title, int width = 60)
        {
            string line = new string('=', width);

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine();
            Console.WriteLine(line);
            Console.WriteLine(CenterText(title, width));
            Console.WriteLine(line);
            Console.ResetColor();
        }

        /// <summary>
        /// Alt başlık oluşturur
        /// </summary>
        /// <param name="subtitle">Alt başlık metni</param>
        /// <param name="width">Genişlik</param>
        public static void WriteSubHeader(string subtitle, int width = 60)
        {
            string line = new string('-', width);

            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine();
            Console.WriteLine(subtitle);
            Console.WriteLine(line);
            Console.ResetColor();
        }

        /// <summary>
        /// Başarı mesajı yazar
        /// </summary>
        /// <param name="message">Mesaj</param>
        public static void WriteSuccess(string message)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"✓ {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Hata mesajı yazar
        /// </summary>
        /// <param name="message">Mesaj</param>
        public static void WriteError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"✗ {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Bilgi mesajı yazar
        /// </summary>
        /// <param name="message">Mesaj</param>
        public static void WriteInfo(string message)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"ℹ {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Uyarı mesajı yazar
        /// </summary>
        /// <param name="message">Mesaj</param>
        public static void WriteWarning(string message)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"⚠ {message}");
            Console.ResetColor();
        }

        /// <summary>
        /// Komut istemi görüntüler
        /// </summary>
        public static void WritePrompt()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("> ");
            Console.ResetColor();
        }

        /// <summary>
        /// Etiket-değer çifti yazar
        /// </summary>
        /// <param name="label">Etiket</param>
        /// <param name="value">Değer</param>
        public static void WriteLabelValue(string label, string value)
        {
            Console.Write($"{label}: ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(value);
            Console.ResetColor();
        }

        /// <summary>
        /// Ayırıcı çizgi çizer
        /// </summary>
        /// <param name="width">Genişlik</param>
        public static void WriteSeparator(int width = 60)
        {
            Console.WriteLine(new string('-', width));
        }

        /// <summary>
        /// Metni belirtilen genişlikte ortalar
        /// </summary>
        /// <param name="text">Metin</param>
        /// <param name="width">Genişlik</param>
        /// <returns>Ortalanmış metin</returns>
        private static string CenterText(string text, int width)
        {
            if (string.IsNullOrEmpty(text))
                return new string(' ', width);

            return text.Length > width
                ? text.Substring(0, width)
                : text.PadLeft(text.Length + (width - text.Length) / 2).PadRight(width);
        }

        /// <summary>
        /// Tablo başlığını yazar
        /// </summary>
        /// <param name="columns">Sütun başlıkları</param>
        public static void WriteTableHeader(params string[] columns)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;

            // Başlıkları yaz
            Console.WriteLine(string.Join(" | ", columns));

            // Çizgi çiz
            string separator = string.Join("-+-", columns.Select(c => new string('-', c.Length)));
            Console.WriteLine(separator);

            Console.ResetColor();
        }

        /// <summary>
        /// Tablo satırını yazar
        /// </summary>
        /// <param name="values">Sütun değerleri</param>
        public static void WriteTableRow(params object[] values)
        {
            Console.WriteLine(string.Join(" | ", values.Select(v => v?.ToString() ?? "")));
        }

        /// <summary>
        /// Yardım menüsünü görüntüler
        /// </summary>
        /// <remarks>
        /// Bu metot artık CliApplication içinde ShowHelp() metodu tarafından yönetilmektedir.
        /// Geriye dönük uyumluluk için burada tutulmaktadır.
        /// </remarks>
        public static void ShowHelp()
        {
            WriteSubHeader("Kullanılabilir Komutlar", 60);

            Console.WriteLine("  upload <dosya_yolu>    - Bir dosyayı sisteme yükler");
            Console.WriteLine("  list                   - Sistemdeki tüm dosyaları listeler");
            Console.WriteLine("  download <id> [--output <çıktı_yolu>] - Bir dosyayı sistemden indirir");
            Console.WriteLine("  delete <id>            - Bir dosyayı sistemden siler");
            Console.WriteLine("  verify <id>            - Bir dosyanın bütünlüğünü kontrol eder");
            Console.WriteLine("  seek [--path <dizin_yolu>] [--recursive true|false] - Dosya sistemini tarar");
            Console.WriteLine("  help                   - Yardım bilgisini gösterir");
            Console.WriteLine("  exit                   - Uygulamadan çıkar");

            Console.WriteLine();
            Console.WriteLine("Örnek Kullanım:");
            Console.WriteLine("  > upload /home/user/belge.pdf");
            Console.WriteLine("  > list");
            Console.WriteLine("  > download abc123 --output /home/user/indirilen.pdf");
            Console.WriteLine();
            Console.WriteLine("Detaylı komut yardımı için: 'help <komut>' veya '<komut> --help' kullanabilirsiniz.");
        }
    }
}