using System.Text;
using Serilog;
using Serilog.Debugging;
using Serilog.Formatting.Json;

namespace TaskManagerApp;

public class Program
{
    public static void Main()
    {
        bool loggerReady = false;

        try
        {
            Console.InputEncoding = new UTF8Encoding(false);
            Console.OutputEncoding = new UTF8Encoding(false);
            ConfigureLogging();
            loggerReady = true;
            Tracer.Configure();

            Log.Information("Программа TaskManager с обработкой исключений запущена");
            Console.WriteLine($"Папка логов: {Path.GetFullPath("logs")}");
            RunTaskManager();
        }
        catch (Exception exception)
        {
            if (!loggerReady)
            {
                Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();
            }

            string operation = exception.Data["Operation"] as string ?? "Main";
            ExceptionHandler.Report(exception, operation, fatal: true);
            Environment.ExitCode = 1;
        }
        finally
        {
            Log.Information("Завершение работы программы");
            Tracer.Close();
            Log.CloseAndFlush();
            SelfLog.Disable();
        }
    }

    private static void ConfigureLogging()
    {
        SelfLog.Enable(message => Console.Error.WriteLine($"[ERROR] Сбой логирования: {message.TrimEnd()}"));
        string outputTemplate = "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}";

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.WithProperty("Application", "TaskManager")
            .Enrich.WithProperty("SessionId", Guid.NewGuid())
            .WriteTo.Console(outputTemplate: outputTemplate)
            .WriteTo.File(new JsonFormatter(renderMessage: true), Path.Combine("logs", "taskmanager-.jsonl"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                encoding: new UTF8Encoding(false))
            .CreateLogger();
    }

    private static void RunTaskManager()
    {
        ShowHelp();
        var taskManager = new TaskManager();
        bool isRunning = true;

        while (isRunning)
        {
            Console.Write("\nКоманда > ");
            string? command = Console.ReadLine();

            if (command == null)
            {
                Log.Information("Поток ввода завершён");
                break;
            }

            command = command.Trim().ToLowerInvariant();
            Log.Debug("Получена команда {Command}", command);

            switch (command)
            {
                case "add":
                case "remove":
                    Console.Write(command == "add"
                        ? "Название новой задачи: "
                        : "Название задачи для удаления: ");
                    string? title = Console.ReadLine();

                    if (title == null)
                    {
                        Log.Warning("Операция {Command} отменена: поток ввода завершён", command);
                        isRunning = false;
                        break;
                    }

                    if (command == "add")
                    {
                        ExceptionHandler.Execute("AddTask", () => taskManager.AddTask(title), title);
                    }
                    else
                    {
                        ExceptionHandler.Execute("RemoveTask", () => taskManager.RemoveTask(title), title);
                    }

                    break;
                case "list":
                    ExceptionHandler.Execute("ListTasks", taskManager.ListTasks);
                    break;
                case "help":
                    ShowHelp();
                    break;
                case "exit":
                    isRunning = false;
                    break;
                default:
                    Log.Warning("Неизвестная команда \"{Command:l}\". Введите help для справки", command);
                    break;
            }
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("add    - добавить задачу");
        Console.WriteLine("remove - удалить задачу по названию");
        Console.WriteLine("list   - вывести список задач");
        Console.WriteLine("help   - показать команды");
        Console.WriteLine("exit   - завершить работу");
        Console.WriteLine("Название: от 1 до 100 символов без управляющих символов");
    }
}
