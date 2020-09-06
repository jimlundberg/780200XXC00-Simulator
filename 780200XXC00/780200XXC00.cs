using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace _780200XXC00
{
    /// <summary>
    /// Class for file copy, move and delete handling
    /// </summary>
    public class FileHandling
    {
        private static readonly Object FileLock = new Object();

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
                    lock (FileLock)
                    {
                        destFile.Delete();
                    }
                }

                sourceFile.CopyTo(Path.Combine(destinationDI.FullName, sourceFile.Name));

                // Delete the source file if removeSource is true
                if (removeSource)
                {
                    lock (FileLock)
                    {
                        sourceFile.Delete();
                    }
                }
            }

            // Delete the source directory if removeSource is true
            if (removeSource)
            {
                lock (FileLock)
                {
                    sourceDI.Delete();
                }
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
                lock (FileLock)
                {
                    Target.Delete();
                }
            }

            // Copy to target file
            lock (FileLock)
            {
                Source.CopyTo(targetFile);
            }

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
                lock (FileLock)
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
            }

            lock (FileLock)
            {
                // Then delete the directory
                Directory.Delete(targetDirectory, false);
            }

            Console.WriteLine(String.Format("Deleted Directory {0}", targetDirectory));
        }
    }

    class Program
    {
        public static Thread tcpListenerThread;
        private static string Job = "1185840_202003250942";
        private static string ProcessingBufferDirectory = @"C:\SSMCharacterizationHandler\ProcessingBuffer";
        private static int Port = 3000;
        private static int CpuCores = 4;

        public static void StartListening()
        {
            // Start TcpServer background thread        
            tcpListenerThread = new Thread(new ThreadStart(ListenForIncommingRequests))
            {
                IsBackground = true
            };
            tcpListenerThread.Start();
        }

        private static void ListenForIncommingRequests()
        {
            int port = 3000;
            string server = "127.0.0.1";
            string job = "1185840_202003250942";

            try
            {
                TcpListener tcpListener = new TcpListener(IPAddress.Parse(server), port);
                tcpListener.Start();
                byte[] bytes = new byte[1024];

                Console.WriteLine("Server Started");

                while (true)
                {
                    // Get a stream object for reading 
                    using (NetworkStream stream = tcpListener.AcceptTcpClient().GetStream())
                    {
                        // Read incomming stream into byte arrary.                      
                        int length = 0;
                        while ((length = stream.Read(bytes, 0, bytes.Length)) != 0)
                        {
                            byte[] incommingData = new byte[length];
                            Array.Copy(bytes, 0, incommingData, 0, length);

                            // Convert byte array to string message.                            
                            string clientMessage = Encoding.ASCII.GetString(incommingData);

                            int stepIndex = 0;
                            if (clientMessage == "status")
                            {
                                for (int i = 0; i < 30; i++)
                                {
                                    stepIndex = i % 5;
                                    string responseMsg = String.Format("Step {0} in process.", stepIndex);

                                    // Translate the passed message into ASCII and store it as a Byte array.
                                    Byte[] responseData = System.Text.Encoding.ASCII.GetBytes(responseMsg);

                                    // Send the message to the connected TcpServer.
                                    stream.Write(responseData, 0, responseData.Length);

                                    Console.WriteLine(String.Format("Sending {0}", responseMsg));
                                }

                                // Send complete message
                                string finalResponse = "Whole process done, socket closed.";

                                // Translate the passed message into ASCII and store it as a Byte array.
                                Byte[] finalResponseData = System.Text.Encoding.ASCII.GetBytes(finalResponse);

                                // Send the message to the connected TcpServer.
                                stream.Write(finalResponseData, 0, finalResponseData.Length);

                                Console.WriteLine(String.Format("Sent {0}", finalResponseData));

                                // Now run the Modeler job Finish
                                RunModelerSimulationFinish(job);
                            }
                        }
                    }
                }
            }
            catch (SocketException socketException)
            {
                Console.WriteLine("SocketException " + socketException.ToString());
            }
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
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step1.mat", processingBufferJobDir);
            Thread.Sleep(1000);
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step2.mat", processingBufferJobDir);
            Thread.Sleep(1000);
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "_step3.mat", processingBufferJobDir);
            Thread.Sleep(1000);

            // Copy EEPROM varables file to the job directory
            FileHandling.CopyFile(testPassDirectory + "EEPROM_variables_" + job + ".mat", processingBufferJobDir);
            Thread.Sleep(1000);

            // Copy the .tab files to the job directory
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "CAP.tab", processingBufferJobDir);
            Thread.Sleep(1000);
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "TUNE.tab", processingBufferJobDir);

            Thread.Sleep(2000);

            // Copy the data.xml without the OverallResult field
            FileHandling.CopyFile(testNoneDirectory + @"\" + job + "Data.xml", processingBufferJobDir);

            Thread.Sleep(5000);

            // Copy the data.xml with the OverallResult field
            FileHandling.CopyFile(testPassDirectory + @"\" + job + "Data.xml", processingBufferJobDir);
        }

        static void Main(string[] args)
        {
            string processingBufferDirectoryArg = args[1];
            string portArg = args[2];
            string cpuCores = args[3];

            if (args.Length > 1)
            {
                processingBufferDirectoryArg = args[1]; // - d C:\SSMCharacterizationHandler\ProcessingBuffer\1185840_202003250942
                portArg = args[3];   // - s 3000
                cpuCores = args[5];  // - p 4
            }

            Job = processingBufferDirectoryArg.Substring(processingBufferDirectoryArg.LastIndexOf("\\") + 1);
            ProcessingBufferDirectory = processingBufferDirectoryArg.Substring(0, processingBufferDirectoryArg.IndexOf("\\"));
            Port = int.Parse(portArg);
            CpuCores = int.Parse(cpuCores);

            Console.WriteLine("\nPress the Enter key to exit the application...\n");

            // Start the TCP/IP receive listening method
            StartListening();

            Console.ReadLine();
        }
    }
}

