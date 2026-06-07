using System;

using System.Diagnostics;

using System.IO;

using System.Threading;

using System.Threading.Tasks;



namespace Xiaomi_Flash

{

    class Fastboot : IDisposable

    {

        Process? process;

        public StreamReader stdout = null!;

        public StreamReader stderr = null!;



        public Fastboot(string? serial, string action)

        {

            process = new Process();

            process.StartInfo.FileName = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fastboot.exe");

            process.StartInfo.Arguments = serial == null ? action :

                "\"-s\" \"" + serial + "\" " + action;

            process.StartInfo.CreateNoWindow = true;

            process.StartInfo.RedirectStandardError = true;

            process.StartInfo.RedirectStandardOutput = true;

            process.StartInfo.RedirectStandardInput = true;

            process.StartInfo.UseShellExecute = false;

            process.Start();

            process.StandardInput.Close();



            stdout = process.StandardOutput;

            stderr = process.StandardError;

        }



        public static bool Run(string? serial, string cmd, Action<string> onOutputLine, int timeoutMs = 0)

        {

            bool failed = false;



            using (Fastboot fastboot = new Fastboot(serial, cmd))

            {

                Task readTask = Task.Run(delegate

                {

                    try

                    {

                        ReadStream(fastboot.stderr, onOutputLine, ref failed);

                        ReadStream(fastboot.stdout, onOutputLine, ref failed);

                    }

                    catch (IOException) { }

                    catch (ObjectDisposedException) { }

                });



                Process? proc = fastboot.process;

                if (proc == null)

                    return false;



                if (timeoutMs > 0)

                    proc.WaitForExit(timeoutMs);

                else

                    proc.WaitForExit();



                if (!proc.HasExited)

                    fastboot.ForceKill();



                try

                {

                    readTask.Wait(2000);

                }

                catch (AggregateException) { }

            }



            return !failed;

        }



        static void ReadStream(StreamReader reader, Action<string> onOutputLine, ref bool failed)

        {

            while (true)

            {

                string? line = reader.ReadLine();

                if (line == null)

                    break;



                onOutputLine(line);

                if (line.IndexOf("FAILED", StringComparison.OrdinalIgnoreCase) >= 0)

                    failed = true;

            }

        }



        public static int GetRebootTimeoutMs(string cmd)

        {

            string trimmed = cmd.TrimStart();

            if (trimmed.StartsWith("reboot", StringComparison.OrdinalIgnoreCase))

                return 15000;

            return 0;

        }



        public void ForceKill()

        {

            if (process == null || process.HasExited)

                return;



            try

            {

                process.Kill(entireProcessTree: true);

            }

            catch (InvalidOperationException) { }

            catch (System.ComponentModel.Win32Exception) { }



            try

            {

                process.WaitForExit(3000);

            }

            catch (InvalidOperationException) { }

        }



        public void Dispose()

        {

            if (process == null)

                return;



            try

            {

                if (!process.HasExited)

                    process.WaitForExit(3000);

                if (!process.HasExited)

                    ForceKill();

            }

            finally

            {

                process.Dispose();

                process = null;

            }

        }



        ~Fastboot()

        {

            if (process != null)

                Dispose();

        }

    }

}


