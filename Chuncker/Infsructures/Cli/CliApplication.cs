using Chuncker.Interfaces;
using Chuncker.Infsructures.UI;
using Microsoft.Extensions.DependencyInjection;
using System.Text;

namespace Chuncker.Infsructures.Cli
{
    /// <summary>
    /// Komut satırı arayüzü uygulaması
    /// </summary>
    public class CliApplication
    {
        private readonly Dictionary<string, ICliCommand> _commands = new(StringComparer.OrdinalIgnoreCase);
        private readonly IServiceProvider _serviceProvider;
        private string _name;
        private string _description;

        /// <summary>
        /// <see cref="CliApplication"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="serviceProvider">Servis sağlayıcı</param>
        public CliApplication(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _name = "Command-Line Application";
            _description = "A command-line application";
        }

        /// <summary>
        /// Uygulamanın adını ve açıklamasını ayarlar
        /// </summary>
        /// <param name="name">Uygulamanın adı</param>
        /// <param name="description">Uygulamanın açıklaması</param>
        /// <returns>Metod zincirleme için bu örnek</returns>
        public CliApplication WithNameAndDescription(string name, string description)
        {
            _name = name ?? throw new ArgumentNullException(nameof(name));
            _description = description ?? throw new ArgumentNullException(nameof(description));
            return this;
        }

        /// <summary>
        /// Uygulamaya bir komut ekler
        /// </summary>
        /// <param name="command">Eklenecek komut</param>
        /// <returns>Metod zincirleme için bu örnek</returns>
        public CliApplication AddCommand(ICliCommand command)
        {
            if (command == null)
            {
                throw new ArgumentNullException(nameof(command));
            }

            _commands[command.Name] = command;
            return this;
        }

        /// <summary>
        /// Bağımlılık enjeksiyonu kullanarak uygulamaya bir komut ekler
        /// </summary>
        /// <typeparam name="TCommand">Komut işleyicisinin türü</typeparam>
        /// <returns>Metod zincirleme için bu örnek</returns>
        public CliApplication AddCommand<TCommand>() where TCommand : ICliCommand
        {
            var command = _serviceProvider.GetRequiredService<TCommand>();
            return AddCommand(command);
        }

        /// <summary>
        /// Uygulama veya belirli bir komut için yardım gösterir
        /// </summary>
        /// <param name="commandName">Yardım gösterilecek komutun adı, uygulama için null</param>
        public void ShowHelp(string commandName = null)
        {
            if (string.IsNullOrWhiteSpace(commandName))
            {
                // Show application help
                ConsoleHelper.WriteHeader(_name, 60);
                Console.WriteLine(_description);
                Console.WriteLine();
                Console.WriteLine("Available commands:");
                Console.WriteLine();

                foreach (var command in _commands.Values)
                {
                    Console.WriteLine($"  {command.Name,-15} {command.Description}");

                    // Show command options in the main menu
                    if (command.Options.Any())
                    {
                        foreach (var option in command.Options)
                        {
                            var shortName = !string.IsNullOrEmpty(option.ShortName) ? $"-{option.ShortName}," : "   ";
                            Console.WriteLine($"     {shortName} --{option.Name,-12} {option.Description}");
                        }

                        Console.WriteLine();
                    }
                }

                Console.WriteLine();
                Console.WriteLine("Run 'help <command>' to see help for a specific command.");
                return;
            }

            // Show command help
            if (!_commands.TryGetValue(commandName, out var cmdToShow))
            {
                ConsoleHelper.WriteError($"Unknown command: {commandName}");
                return;
            }

            Console.WriteLine($"Command: {cmdToShow.Name}");
            Console.WriteLine($"Description: {cmdToShow.Description}");
            Console.WriteLine();

            if (cmdToShow.Arguments.Any())
            {
                Console.WriteLine("Arguments:");
                foreach (var argument in cmdToShow.Arguments)
                {
                    Console.WriteLine(
                        $"  {argument.Name,-15} {(argument.IsRequired ? "[required]" : "[optional]")} {argument.Description}");
                }

                Console.WriteLine();
            }

            if (cmdToShow.Options.Any())
            {
                Console.WriteLine("Options:");
                foreach (var option in cmdToShow.Options)
                {
                    var shortName = !string.IsNullOrEmpty(option.ShortName) ? $"-{option.ShortName}," : "   ";
                    Console.WriteLine($"  {shortName} --{option.Name,-12} {option.Description}");
                }

                Console.WriteLine();
            }
        }

        /// <summary>
        /// Sağlanan argümanlara göre bir komutu ayrıştırır ve çalıştırır
        /// </summary>
        /// <param name="args">Komut satırı argümanları</param>
        /// <param name="cancellationToken">İptal belirteci</param>
        /// <returns>Komut yürütmesinden çıkış kodu</returns>
        public async Task<int> ExecuteAsync(string[] args, CancellationToken cancellationToken = default)
        {
            if (args == null || args.Length == 0)
            {
                ShowHelp();
                return 0;
            }

            var commandName = args[0];

            if (commandName.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                if (args.Length > 1)
                {
                    ShowHelp(args[1]);
                }
                else
                {
                    ShowHelp();
                }

                return 0;
            }

            if (!_commands.TryGetValue(commandName, out var selectedCommand))
            {
                ConsoleHelper.WriteError($"Unknown command: {commandName}");
                ShowHelp();
                return 1;
            }

            try
            {
                var context = new CliContext(_serviceProvider);
                var parser = new CliArgumentParser(selectedCommand);

                if (!parser.ParseArguments(args.Skip(1).ToArray(), context))
                {
                    return 1;
                }

                return await selectedCommand.ExecuteAsync(context, cancellationToken);
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteError($"Error executing command: {ex.Message}");
                return 1;
            }
        }

        /// <summary>
        /// Uygulamayı etkileşimli modda çalıştırır
        /// </summary>
        /// <param name="cancellationToken">İptal belirteci</param>
        /// <returns>Uygulamadan çıkış kodu</returns>
        public async Task<int> RunInteractiveModeAsync(CancellationToken cancellationToken = default)
        {
            ShowHelp();

            while (!cancellationToken.IsCancellationRequested)
            {
                ConsoleHelper.WritePrompt();
                var input = Console.ReadLine()?.Trim();

                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                if (input.Equals("exit", StringComparison.OrdinalIgnoreCase))
                {
                    ConsoleHelper.WriteInfo("Exiting application...");
                    break;
                }

                var args = ParseCommandLine(input);
                await ExecuteAsync(args, cancellationToken);
            }

            return 0;
        }

        /// <summary>
        /// Bir komut satırını argüman dizisine ayrıştırır
        /// </summary>
        /// <param name="commandLine">Ayrıştırılacak komut satırı</param>
        /// <returns>Argüman dizisi</returns>
        private static string[] ParseCommandLine(string commandLine)
        {
            if (string.IsNullOrWhiteSpace(commandLine))
            {
                return Array.Empty<string>();
            }

            var args = new List<string>();
            var currentArg = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < commandLine.Length; i++)
            {
                var c = commandLine[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                    continue;
                }

                if (c == ' ' && !inQuotes)
                {
                    if (currentArg.Length > 0)
                    {
                        args.Add(currentArg.ToString());
                        currentArg.Clear();
                    }

                    continue;
                }

                currentArg.Append(c);
            }

            if (currentArg.Length > 0)
            {
                args.Add(currentArg.ToString());
            }

            return args.ToArray();
        }
    }

    /// <summary>
    /// Komut satırı argümanları için ayrıştırıcı
    /// </summary>
    public class CliArgumentParser
    {
        private readonly ICliCommand _command;

        /// <summary>
        /// <see cref="CliArgumentParser"/> sınıfının yeni bir örneğini başlatır
        /// </summary>
        /// <param name="command">Argümanları ayrıştırılacak komut</param>
        public CliArgumentParser(ICliCommand command)
        {
            _command = command ?? throw new ArgumentNullException(nameof(command));
        }

        /// <summary>
        /// Komut için argümanları ayrıştırır ve bağlamı doldurur
        /// </summary>
        /// <param name="args">Ayrıştırılacak argümanlar</param>
        /// <param name="context">Doldurulacak bağlam</param>
        /// <returns>Ayrıştırma başarılıysa true, aksi halde false</returns>
        public bool ParseArguments(string[] args, CliContext context)
        {
            var argumentIndex = 0;
            var argumentsList = _command.Arguments.ToList();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];

                if (arg.StartsWith("--"))
                {
                    // Long-form option
                    var optionName = arg.Substring(2);
                    bool negated = false;
                    
                    // "--no-option" formatı için, "no-" öneki kaldırılıp boolean olarak false değeri verilecek
                    if (optionName.StartsWith("no-"))
                    {
                        optionName = optionName.Substring(3);
                        negated = true;
                    }
                    
                    var option = _command.Options.FirstOrDefault(o =>
                        string.Equals(o.Name, optionName, StringComparison.OrdinalIgnoreCase));

                    if (option == null)
                    {
                        ConsoleHelper.WriteError($"Unknown option: {arg}");
                        return false;
                    }

                    if (option.ValueType == typeof(bool))
                    {
                        if (negated)
                        {
                            // "--no-option" formatı kullanıldığında false değeri ata
                            context.SetOptionValue(option.Name, false);
                        }
                        // Boolean değeri için sonraki parametreyi kontrol et
                        else if (i + 1 < args.Length && (args[i+1].Equals("true", StringComparison.OrdinalIgnoreCase) || 
                                                  args[i+1].Equals("false", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Bir sonraki parametre true veya false ise
                            bool boolValue = bool.Parse(args[++i]);
                            context.SetOptionValue(option.Name, boolValue);
                        }
                        else
                        {
                            // Boolean flag - sadece flag verilmişse true olarak ayarla
                            context.SetOptionValue(option.Name, true);
                        }
                    }
                    else
                    {
                        // Option with value
                        if (i + 1 >= args.Length)
                        {
                            ConsoleHelper.WriteError($"Option {arg} requires a value");
                            return false;
                        }

                        var value = args[++i];
                        if (!option.TryConvertValue(value, out var convertedValue))
                        {
                            ConsoleHelper.WriteError($"Invalid value for option {arg}: {value}");
                            return false;
                        }

                        context.SetOptionValue(option.Name, convertedValue);
                    }
                }
                else if (arg.StartsWith("-"))
                {
                    // Short-form option
                    var optionShortName = arg.Substring(1);
                    bool negated = false;
                    
                    // "-no-x" formatı için, "no-" öneki kaldırılıp boolean olarak false değeri verilecek
                    if (optionShortName.StartsWith("no-"))
                    {
                        optionShortName = optionShortName.Substring(3);
                        negated = true;
                    }
                    
                    var option = _command.Options.FirstOrDefault(o =>
                        string.Equals(o.ShortName, optionShortName, StringComparison.OrdinalIgnoreCase));

                    if (option == null)
                    {
                        ConsoleHelper.WriteError($"Unknown option: {arg}");
                        return false;
                    }

                    if (option.ValueType == typeof(bool))
                    {
                        if (negated)
                        {
                            // "-no-x" formatı kullanıldığında false değeri ata
                            context.SetOptionValue(option.Name, false);
                        }
                        // Boolean değeri için sonraki parametreyi kontrol et
                        else if (i + 1 < args.Length && (args[i+1].Equals("true", StringComparison.OrdinalIgnoreCase) || 
                                                  args[i+1].Equals("false", StringComparison.OrdinalIgnoreCase)))
                        {
                            // Bir sonraki parametre true veya false ise
                            bool boolValue = bool.Parse(args[++i]);
                            context.SetOptionValue(option.Name, boolValue);
                        }
                        else
                        {
                            // Boolean flag - sadece flag verilmişse true olarak ayarla
                            context.SetOptionValue(option.Name, true);
                        }
                    }
                    else
                    {
                        // Option with value
                        if (i + 1 >= args.Length)
                        {
                            ConsoleHelper.WriteError($"Option {arg} requires a value");
                            return false;
                        }

                        var value = args[++i];
                        if (!option.TryConvertValue(value, out var convertedValue))
                        {
                            ConsoleHelper.WriteError($"Invalid value for option {arg}: {value}");
                            return false;
                        }

                        context.SetOptionValue(option.Name, convertedValue);
                    }
                }
                else
                {
                    // Positional argument
                    if (argumentIndex >= argumentsList.Count)
                    {
                        ConsoleHelper.WriteError($"Too many arguments");
                        return false;
                    }

                    var argument = argumentsList[argumentIndex++];
                    if (!argument.TryConvertValue(arg, out var convertedValue))
                    {
                        ConsoleHelper.WriteError($"Invalid value for argument {argument.Name}: {arg}");
                        return false;
                    }

                    context.SetArgumentValue(argument.Name, convertedValue);
                }
            }

            // Check for required arguments
            foreach (var argument in _command.Arguments.Where(a => a.IsRequired))
            {
                if (context.GetArgumentValue<object>(argument.Name) == null)
                {
                    ConsoleHelper.WriteError($"Missing required argument: {argument.Name}");
                    return false;
                }
            }

            // Set default values for options not provided
            foreach (var option in _command.Options)
            {
                if (context.GetOptionValue<object>(option.Name) == null && option.DefaultValue != null)
                {
                    context.SetOptionValue(option.Name, option.DefaultValue);
                }
            }

            return true;
        }
    }
}