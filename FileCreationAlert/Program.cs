using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Threading;

namespace FileCreationAlert {
    class Program {
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

        static void Main() {
            if (!File.Exists(configFileName)) {
                File.WriteAllText(configFileName, defaultConfig);
                Console.WriteLine("Configuration file created. Please fill it with your data and relaunch this application!\n" +
                    "Press any key (except power off/sleep/reset) to close this window!");
                Console.ReadKey();
                return;
            }

            Dictionary<string, string> config = GetConfig();
            if (!Directory.Exists(config["path"])) {
                Console.WriteLine("The path written in the configuration file is invalid. Please change that to a valid folder!\n" +
                    "Press any key (except minimize or full scrren) to close this window!");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("Configuration file loaded, looking for file changes at " + new DirectoryInfo(config["path"]).FullName);

            string[] contents = Directory.GetFiles(config["path"], config["pattern"]);
            while (true) {
                Thread.Sleep(int.Parse(config["interval"]) * 1000);
                string[] newContents = Directory.GetFiles(config["path"], config["pattern"]);
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
                        Console.WriteLine("New file: " + fileName);

                        SmtpClient smtp = new SmtpClient {
                            Host = config["host"],
                            Port = int.Parse(config["port"]),
                            EnableSsl = true,
                            Timeout = 10000,
                            DeliveryMethod = SmtpDeliveryMethod.Network,
                            UseDefaultCredentials = false,
                            Credentials = new NetworkCredential(config["address"], config["password"])
                        };
                        using (MailMessage message = new MailMessage(config["address"], config["address"], config["title"],
                            config["body"].Replace("%f", fileName))) {
                            try {
                                smtp.Send(message);
                                Console.WriteLine("E-mail sent.");
                            } catch (Exception) {
                                Console.WriteLine("Couldn't send e-mail, please check the configuration file!");
                            }
                        }
                    }
                }
                contents = newContents;
            }
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
path=.
; Filter for single file type if needed
pattern=*.*
; Folder checking interval in seconds
interval=60";
    }
}