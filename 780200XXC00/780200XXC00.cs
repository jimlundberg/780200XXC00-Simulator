using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace _780200XXC00
{
    /// <summary>
    /// Class for file copy, move and delete handling
    /// </summary>
    public class FileHandling
    {
        /// <summary>
        /// CopyFolderContents - Copy files and folders from source to destination and optionally remove source files/folders
        /// </summary>
        /// <param name="sourcePath"></param>
        /// <param name="destinationPath"></param>
        /// <param name="removeSource"></param>
        /// <param name="overwrite"></param>
        public static void CopyFolderContents(string sourcePath, string destinationPath,
            bool removeSource = false, bool overwrite = false)
        {
            DirectoryInfo sourceDI = new DirectoryInfo(sourcePath);
            DirectoryInfo destinationDI = new DirectoryInfo(destinationPath);

            // If the destination directory does not exist, create it
            if (!destinationDI.Exists)
            {
                destinationDI.Create();
            }

            // Copy files one by one
            FileInfo[] sourceFiles = sourceDI.GetFiles();
            foreach (FileInfo sourceFile in sourceFiles)
            {
                // This is the destination folder plus the new filename
                FileInfo destFile = new FileInfo(Path.Combine(destinationDI.FullName, sourceFile.Name));
                if (destFile == null)
                {
                    Console.WriteLine("FileHandling destFile failed to instantiate");
                }

                // Delete the destination file if overwrite is true
                if (destFile.Exists && overwrite)
                {
                    destFile.Delete();
                }

                sourceFile.CopyTo(Path.Combine(destinationDI.FullName, sourceFile.Name));

                // Delete the source file if removeSource is true
                if (removeSource)
                {
                    sourceFile.Delete();
                }
            }

            // Delete the source directory if removeSource is true
            if (removeSource)
            {
                sourceDI.Delete();
            }
        }

        /// <summary>
        /// Copy file from source to target
        /// </summary>
        /// <param name="sourceFile"></param>
        /// <param name="targetFile"></param>
        public static void CopyFile(string sourceFile, string targetFile)
        {
            FileInfo Source = new FileInfo(sourceFile);
            FileInfo Target = new FileInfo(targetFile);
            if (Target == null)
            {
                Console.WriteLine("FileHandling Target failed to instantiate");
            }

            if (Target.Exists)
            {
                // Delete the Target file first
                Target.Delete();
            }

            // Copy to target file
            Source.CopyTo(targetFile);

            Console.WriteLine(String.Format("Copied {0} -> {1}", sourceFile, targetFile));
        }

        /// <summary>
        /// Deletes a directory after deleting files inside
        /// </summary>
        /// <param name="targetDirectory"></param>
        public static void DeleteDirectory(string targetDirectory)
        {
            // First delete all files in target directory
            string[] files = Directory.GetFiles(targetDirectory);
            foreach (string file in files)
            {
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }

            // Then delete the directory
            Directory.Delete(targetDirectory, false);

            Console.WriteLine(String.Format("Deleted Directory {0}", targetDirectory));
        }
    }

    class Program
    {
        public static Thread tcpListenerThread;
        private static string Job;
        private static string ExecutableDirectory;
        private static string ProcessingBufferDirectory;
        private static string TestDirectory = @"C:\SSMCharacterizationHandler\test";
        private static string Server = "127.0.0.1";
        private static int Port = 3000;
        private static int CpuCores = 4;

        /// <summary>
        /// Start Listening for TCP/IP
        /// </summary>
        public static void StartListening()
        {
            // Start TcpServer background thread        
            tcpListenerThread = new Thread(new ThreadStart(ListenForIncommingRequests))
            {
                IsBackground = true
            };
            tcpListenerThread.Start();
        }

        /// <summary>
        /// Listen for Incoming TCP/IP requests
        /// </summary>
        private static void ListenForIncommingRequests()
        {
            string testDirectoryStartDir = TestDirectory + @"\" + Job + " - Start";
            string processingBufferJobDir = ProcessingBufferDirectory + @"\" + Job;

            Console.WriteLine("\n780200XXC00 Simulator Started...\n");

            // Copy the starting data.xml file to the job Processing Buffer directory
            Thread.Sleep(5000);
            FileHandling.CopyFile(testDirectoryStartDir + @"\" + "Data.xml", processingBufferJobDir + @"\" + "Data.xml");

            TcpListener tcpListener = null;
            // TcpListener server = new TcpListener(port);
            tcpListener = new TcpListener(IPAddress.Parse(Server), Port);

            // Start listening for client requests
            tcpListener.Start();

            // Buffer for reading data
            Byte[] bytes = new Byte[256];
            String clientMessage;

            // Enter the TCP/IP listening loop
            Console.Write("\nWaiting for a TCP/IP Connection for job {0} on Port {1}...", Job, Port);

            // Perform a blocking call to accept requests
            TcpClient client = tcpListener.AcceptTcpClient();
            Console.WriteLine("Connected!");

            // Get a stream object for reading and writing
            NetworkStream stream = client.GetStream();

            // Loop to receive all the data sent by the client
            bool simulationComplete = false;
            do
            {
                try
                {
                    int i = 0;
                    int stepIndex = 1;
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string
                        clientMessage = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Console.WriteLine("\nReceived: {0}", clientMessage);

                        //if (clientMessage == "status")
                        {
                            if (stepIndex < 7)
                            {
                                Thread.Sleep(2000);

                                // Create the response message
                                string responseMsg = String.Format("Step {0} in process.", stepIndex++);
                                Byte[] responseData = System.Text.Encoding.ASCII.GetBytes(responseMsg);

                                // Send the message to the connected TcpServer
                                stream.Write(responseData, 0, responseData.Length);
                                Console.WriteLine(String.Format("Sent {0}", responseMsg));
                            }
                            else
                            {
                                Thread.Sleep(2000);

                                // Create the response message
                                string finalResponse = "Whole process done, socket closed.";
                                Byte[] finalResponseData = System.Text.Encoding.ASCII.GetBytes(finalResponse);

                                // Send the message to the connected TcpServer
                                stream.Write(finalResponseData, 0, finalResponseData.Length);
                                Console.WriteLine(String.Format("Sent {0}", finalResponse));

                                Thread.Sleep(5000);

                                // Wait then run the Modeler job complete copies
                                RunModelerSimulationFinish(Job);

                                simulationComplete = true;
                            }
                        }
                    }
                }
                catch (SocketException e)
                {
                    Console.WriteLine("SocketException: {0}", e);
                }
                finally
                {
                    // Stop listening for new clients.
                    tcpListener.Stop();
                }
            }
            while (simulationComplete == false);

            // Shutdown and end connection
            client.Close();
        }

        /// <summary>
        /// Run the Modeler Simuation handling
        /// </summary>
        /// <param name="job"></param>
        public static void RunModelerSimulationFinish(string job)
        {
            string testDirectory = @"C:\SSMCharacterizationHandler\test";
            string testNoneDirectory = testDirectory + @"\" + job + " - None";
            string testPassDirectory = testDirectory + @"\" + job + " - Pass";
            string processingBufferJobDir = ProcessingBufferDirectory + @"\" + job;

            // Copy .mat files to the job directory
            Console.WriteLine("Copying .mat files...");
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step1.mat", processingBufferJobDir + @"\" + job + "_step1.mat");
            Thread.Sleep(1000);
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step2.mat", processingBufferJobDir + @"\" + job + "_step2.mat");
            Thread.Sleep(1000);
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step3.mat", processingBufferJobDir + @"\" + job + "_step3.mat");
            Thread.Sleep(1000);

            // Copy EEPROM varables file to the job directory
            Console.WriteLine("Copying EEPROM_variables file...");
            FileHandling.CopyFile(testPassDirectory + @"\" + "EEPROM_variables_" + job + ".mat", processingBufferJobDir + @"\" + "EEPROM_variables_" + job + ".mat");
            Thread.Sleep(1000);

            // Copy the .tab files to the job directory
            Console.WriteLine("Copying CAP.tab file...");
            FileHandling.CopyFile(testPassDirectory + @"\" + "CAP.tab", processingBufferJobDir + @"\" + "CAP.tab");
            Thread.Sleep(1000);

            Console.WriteLine("Copying CAP.tab file...");
            FileHandling.CopyFile(testPassDirectory + @"\" + "TUNE.tab", processingBufferJobDir + @"\" + "TUNE.tab");
            Thread.Sleep(2000);

            // Copy the data.xml without the OverallResult field
            FileHandling.CopyFile(testNoneDirectory + @"\" + "Data.xml", processingBufferJobDir + @"\" + "Data.xml");
            Thread.Sleep(5000);

            // Copy the data.xml with the OverallResult field
            FileHandling.CopyFile(testPassDirectory + @"\" + "Data.xml", processingBufferJobDir + @"\" + "Data.xml");
        }

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Get the raw parameter strings skipping the arg parameters and dash
            string executableDirectory = Environment.CurrentDirectory + "\\" + Process.GetCurrentProcess().ProcessName;
            string processingBufferDirectoryArg = args[1]; // -d C:\SSMCharacterizationHandler\ProcessingBuffer\1185840_202003250942
            string portArg = args[3];  // -s 3000
            string cpuCores = args[5];  // -p 4

            ExecutableDirectory = executableDirectory;
            Job = processingBufferDirectoryArg.Substring(processingBufferDirectoryArg.LastIndexOf("\\") + 1);
            ProcessingBufferDirectory = processingBufferDirectoryArg.Substring(0, processingBufferDirectoryArg.LastIndexOf("\\"));
            Port = int.Parse(portArg);
            CpuCores = int.Parse(cpuCores);

            Console.WriteLine("{0} -d {1} -s {2} -p {3}", ExecutableDirectory, ProcessingBufferDirectory, Port, CpuCores);

            // Start the TCP/IP receive listening method
            StartListening();

            Console.WriteLine("\nPress the Enter key to exit the application...");
            Console.ReadLine();
        }
    }
}

