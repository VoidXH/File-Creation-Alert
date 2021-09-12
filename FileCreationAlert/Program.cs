using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;

namespace FileCreationAlert {
    class Program {
        static Dictionary<string, string> config;
        static SmtpClient smtp;

        static readonly object logLock = new object(), mailLock = new object();

        static Dictionary<string, string> GetConfig() {
            Dictionary<string, string> config = new Dictionary<string, string>();
            string[] source = File.ReadAllLines(configFileName);
            foreach (string line in source) {
                if (line.Length == 0 || line[0] == '[' || line[0] == ';')
                    continue;
                int split = line.IndexOf('=');
                if (split != -1)
                    config.Add(line.Substring(0, split), line.Substring(split + 1));
            }
            return config;
        }

        static void Process(int thread, string path, string mailTitle) {
            lock (logLock)
                Console.WriteLine($"[{thread}] Looking for file changes at {path}.");

            string[] contents = Directory.GetFiles(path, config["pattern"]);
            while (true) {
                Thread.Sleep(int.Parse(config["interval"]) * 1000);
                string[] newContents = Directory.GetFiles(path, config["pattern"]);
                for (int n = 0; n < newContents.Length; ++n) {
                    bool exists = false;
                    for (int o = 0; o < contents.Length; ++o) {
                        if (newContents[n] == contents[o]) {
                            exists = true;
                            break;
                        }
                    }
                    if (!exists) {
                        string fileName = new FileInfo(newContents[n]).Name;
                        lock (logLock)
                            Console.WriteLine($"[{thread}] New file: {fileName}.");

                        using (MailMessage message = new MailMessage(config["address"], config["address"], mailTitle,
                            config["body"].Replace("%f", fileName))) {
                            try {
                                lock (mailLock)
                                    smtp.Send(message);
                                lock (logLock)
                                    Console.WriteLine($"[{thread}] E-mail sent with this title: {mailTitle}");
                            } catch {
                                lock (logLock)
                                    Console.WriteLine($"[{thread}] Couldn't send e-mail, please check the configuration file!");
                            }
                        }
                    }
                }
                contents = newContents;
            }
        }

        static void Main() {
            if (!File.Exists(configFileName)) {
                File.WriteAllText(configFileName, defaultConfig);
                Console.WriteLine("Configuration file created. Please fill it with your data and relaunch this application!\n" +
                    "Press any key (except power off/sleep/reset) to close this window!");
                Console.ReadKey();
                return;
            }

            config = GetConfig();

            smtp = new SmtpClient {
                Host = config["host"],
                Port = int.Parse(config["port"]),
                EnableSsl = true,
                Timeout = 10000,
                DeliveryMethod = SmtpDeliveryMethod.Network,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(config["address"], config["password"])
            };

            int pathCount = 1;
            List<Thread> threads = new List<Thread>();
            while (config.ContainsKey("path" + pathCount)) {
                if (Directory.Exists(config["path" + pathCount])) {
                    int i = pathCount;
                    string title;
                    if (config.ContainsKey("title" + i))
                        title = config["title" + i];
                    else
                        title = config["title"];
                    threads.Add(new Thread(() => Process(i, new DirectoryInfo(config["path" + i]).FullName, title)));
                    threads[threads.Count - 1].Start();
                } else
                    lock (logLock)
                        Console.WriteLine($"The path \"{config["path" + pathCount]}\" is invalid.");
                ++pathCount;
            }

            if (threads.Count == 0) {
                Console.WriteLine("All paths were invalid, please fix them in the configuration file!\n" +
                    "Press any key (except minimize or full screen) to close this window!");
                Console.ReadKey();
                return;
            }

            lock (logLock)
                Console.WriteLine("Configuration file loaded.");

            for (int i = 0; i < threads.Count; ++i)
                threads[i].Join(); // Exit if all threads crash for some reason
        }

        const string configFileName = "config.ini";
        const string defaultConfig = @"[E-mail]
host=smtp.gmail.com
port=587
address=user@gmail.com
; For Gmail with 2FA, ask for a password at https://myaccount.google.com/apppasswords
password=password
title=New file created!
; Use the %f tag to mark the name of the file
body=File name: %f

[Application]
; The folder to watch for changes, the default is the local folder
; Additional folders can be added like path2, path3, path4...
; This also works for title, it can be overridden like title1, title2...
path1=.
; Filter for single file type if needed
pattern=*.*
; Folder checking interval in seconds
interval=60";
    }
}