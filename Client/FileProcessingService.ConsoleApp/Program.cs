﻿
using FileProcessingService.ConsoleApp.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace FileProcessingService.ConsoleApp
{
    class Program
    {
        private static readonly string _sessionId = Guid.NewGuid().ToString();
        private static string BaseURL = "";
        private static readonly ExtendedConsole console = new();
        public static IConfigurationRoot configuration;

        static void Main()
        {
            ServiceCollection serviceCollection = new();
            ConfigureServices(serviceCollection);
            
            BaseURL = configuration["FileProcessingServiceAPI:Endpoint"].ToString();

            DisplayWelcomeScreen();

            Console.ReadKey();
        }

        private static string DisplayWelcomeScreen()
        {
            string path = "";

            console.AddOption(new Option("Specify File/Folder Path", () =>
            {
                console.ClearMainMenu();
                Console.Clear();
                Console.Write("Please paste xml file path to send or directory you want search xml files: ");

                string filePath = Console.ReadLine();

                if (!string.IsNullOrEmpty(filePath))
                {
                    if (IsDirectory(filePath))
                    {
                        Console.WriteLine("Searching in directory: " + filePath);

                        if (Directory.Exists(filePath))
                        {
                            Directory.GetFiles(filePath).Where(x => x.EndsWith("xml")).ToList().ForEach(s =>
                            {
                                console.AddOption(new Option(s, async () =>
                                {
                                    path = s;
                                    await UploadFile(path);
                                }));
                            });
                            console.AddExit();
                            ExtendedConsole.WriteMenu(console.Options, console.Options[0]);
                        }
                        else
                            Console.WriteLine("path not found");
                    }
                    else
                    {
                        UploadFile(filePath).Wait();
                    }
                }
            }));
            console.AddExit();
            console.Init();

            return path;
        }

        static async Task UploadFile(string filePath)
        {
            Console.Clear();
            Console.WriteLine($"Choosed file path: {filePath}");

            Console.Write("Please enter xml element you want to find in file. Sepetarete with semicolon (;): ");
            string elements = Console.ReadLine();

            if (!string.IsNullOrEmpty(elements))
            {
                Console.WriteLine("Processing started..");

                if (!File.Exists(filePath))
                {
                    return;
                }

                try
                {
                    string retryAfter = DateTime.Now.ToString();
                    (string data, HttpStatusCode statusCode) = await FileProcessingApiClient.Upload($"{BaseURL}/files", filePath, _sessionId, elements);

                    if (statusCode == HttpStatusCode.OK)
                    {
                        (string data, HttpStatusCode statusCode) result = await FileProcessingApiClient.GetDataWithPollingAsync($"{BaseURL}/files/status-info/{_sessionId}", retryAfter);

                        if (result.statusCode == HttpStatusCode.OK)
                        {
                            var messageModel = JsonConvert.DeserializeObject<IEnumerable<StatusReponseModel>>(result.data);

                            foreach (var item in messageModel)
                            {
                                if (item.Completed)
                                {
                                    Console.ForegroundColor = ConsoleColor.Green;
                                }

                                Console.WriteLine($"{item.Message} - {item.CreatedAt}");
                            }
                            Console.ResetColor();

                            if (Console.ReadKey(false).Key == ConsoleKey.Enter)
                                Environment.Exit(0);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(ex.Message);
                    Console.ResetColor();
                }
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("XML element is required!");
                Console.ResetColor();
            }
        }

        static bool IsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);

            return attr.HasFlag(FileAttributes.Directory);
        }

        private static void ConfigureServices(IServiceCollection serviceCollection)
        {
            serviceCollection.AddLogging();

            // Build configuration
            configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetParent(AppContext.BaseDirectory).FullName)
                .AddJsonFile("appsettings.json", false)
                .Build();

            // Add access to generic IConfigurationRoot
            serviceCollection.AddSingleton(configuration);
        }
    }
}
