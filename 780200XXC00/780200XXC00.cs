using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;

namespace _780200XXC00
{
    /// <summary>
    /// Modeler Step State enum
    /// </summary>
    public enum ModelerStepState
    {
        NONE,
        STEP_1,
        STEP_2,
        STEP_3,
        STEP_4,
        STEP_5,
        STEP_6,
        STEP_COMPLETE
    };

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
        private static bool ExitFlag = false;

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
            string testDirectory = @"C:\SSMCharacterizationHandler\test";
            string testPassDirectory = testDirectory + @"\" + Job + " - Pass";
            string testFailDirectory = testDirectory + @"\" + Job + " - Fail";
            string testNoneDirectory = testDirectory + @"\" + Job + " - None";

            Console.WriteLine("\n780200XXC00 Simulator Started...\n");

            // Copy the starting data.xml file to the job Processing Buffer directory
            Thread.Sleep(5000);
            FileHandling.CopyFile(testDirectoryStartDir + @"\" + "Data.xml", ProcessingBufferDirectory + @"\" + "Data.xml");

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
                    ModelerStepState modelerStepState = ModelerStepState.STEP_1;
                    while ((i = stream.Read(bytes, 0, bytes.Length)) != 0)
                    {
                        // Translate data bytes to a ASCII string
                        clientMessage = System.Text.Encoding.ASCII.GetString(bytes, 0, i);
                        Console.WriteLine("\nReceived: {0}", clientMessage);

                        if (clientMessage == "status")
                        {
                            Thread.Sleep(1000);

                            string responseMsg = String.Empty;
                            switch (modelerStepState)
                            {
                                case ModelerStepState.STEP_1:
                                    modelerStepState = ModelerStepState.STEP_1;
                                    break;

                                case ModelerStepState.STEP_2:
                                    FileHandling.CopyFile(testPassDirectory + @"\" + Job + "_step1.mat", ProcessingBufferDirectory + @"\" + Job + "_step1.mat");
                                    modelerStepState = ModelerStepState.STEP_2;
                                    break;

                                case ModelerStepState.STEP_3:
                                    FileHandling.CopyFile(testPassDirectory + @"\" + Job + "_step1.mat", ProcessingBufferDirectory + @"\" + Job + "_step2.mat");
                                    modelerStepState = ModelerStepState.STEP_3;
                                    break;

                                case ModelerStepState.STEP_4:
                                    FileHandling.CopyFile(testPassDirectory + @"\" + Job + "_step1.mat", ProcessingBufferDirectory + @"\" + Job + "_step3.mat");
                                    modelerStepState = ModelerStepState.STEP_4;
                                    break;

                                case ModelerStepState.STEP_5:
                                    FileHandling.CopyFile(testPassDirectory + @"\" + "EEPROM_variables_" + Job + ".mat", ProcessingBufferDirectory + @"\" + "EEPROM_variables_" + Job + ".mat");
                                    modelerStepState = ModelerStepState.STEP_5;
                                    break;

                                case ModelerStepState.STEP_6:
                                    FileHandling.CopyFile(testPassDirectory + @"\" + "CAP.tab", ProcessingBufferDirectory + @"\" + "CAP.tab");
                                    FileHandling.CopyFile(testPassDirectory + @"\" + "TUNE.tab", ProcessingBufferDirectory + @"\" + "TUNE.tab");
                                    modelerStepState = ModelerStepState.STEP_6;
                                    break;

                                case ModelerStepState.STEP_COMPLETE:
                                    FileHandling.CopyFile(testNoneDirectory + @"\" + "Data.xml", ProcessingBufferDirectory + @"\" + "Data.xml");
                                    modelerStepState = ModelerStepState.STEP_COMPLETE;
                                    break;
                            }

                            // Create the step response message
                            if (modelerStepState != ModelerStepState.STEP_COMPLETE)
                            {
                                responseMsg = String.Format("Step {0} in process.", (int)modelerStepState);
                            }
                            else
                            {
                                Random rand = new Random();
                                int weirdMessage = rand.Next(0, 3);
                                if (weirdMessage == 1)
                                {
                                    responseMsg = "Whole process done, socket closed.";
                                }
                                else
                                {
                                    // Sometimes the modeler gives this combined message for the final message
                                    responseMsg = "Step 1 in process. Whole process done, socket closed.";
                                }
                            }

                            // Send the message to the connected TcpServer
                            Byte[] responseData = Encoding.ASCII.GetBytes(responseMsg);
                            stream.Write(responseData, 0, responseData.Length);
                            Console.WriteLine(String.Format("Sent: {0}", responseMsg));

                            if (modelerStepState == ModelerStepState.STEP_COMPLETE)
                            {
                                // Simulate real opertion where it usually waits to deposit the data.xml file with the OverallResult field after TCP/IP is done
                                Thread.Sleep(5000);

                                // Test not getting the Pass/Fail at all for 1 in 4 jobs
                                Random setRand = new Random(DateTime.Now.Second);
                                int setOrNot = setRand.Next(0, 5);
                                if (setOrNot != 1)
                                {
                                    // Randomly copy over the data.xml with Pass or Fail
                                    Random passFailRand = new Random(DateTime.Now.Second);
                                    int passFail = passFailRand.Next(0, 3);
                                    if (passFail != 1)
                                    {
                                        FileHandling.CopyFile(testPassDirectory + @"\" + "Data.xml", ProcessingBufferDirectory + @"\" + "Data.xml");
                                    }
                                    else
                                    {
                                        FileHandling.CopyFile(testFailDirectory + @"\" + "Data.xml", ProcessingBufferDirectory + @"\" + "Data.xml");
                                    }
                                }

                                simulationComplete = true;
                            }
                            else
                            {
                                // Increment Modeler step
                                modelerStepState++;
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
                    // Stop listening for new clients
                    tcpListener.Stop();
                }
            }
            while (simulationComplete == false);

            // Shutdown and end connection
            client.Close();

            ExitFlag = true;
        }

        /// <summary>
        /// Main
        /// </summary>
        /// <param name="args"></param>
        static void Main(string[] args)
        {
            // Get the raw parameter strings skipping the arg parameters and dash
            string executableDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            string executableFullName = executableDirectory + @"\" + Path.GetFileName(Process.GetCurrentProcess().MainModule.FileName);
            string processingBufferDirectoryArg = args[1];
            string portArg = args[3];
            string cpuCores = args[5];

            ExecutableDirectory = executableFullName;
            Job = processingBufferDirectoryArg.Substring(processingBufferDirectoryArg.LastIndexOf("\\") + 1);
            ProcessingBufferDirectory = processingBufferDirectoryArg;
            Port = int.Parse(portArg);
            CpuCores = int.Parse(cpuCores);

            Console.WriteLine("{0} -d {1} -s {2} -p {3}", ExecutableDirectory, ProcessingBufferDirectory, Port, CpuCores);

            // Start the TCP/IP receive listening method
            StartListening();

            // Wait for exit flag to be set triggering exit
            do
            {
                Thread.Sleep(250);
            }
            while (ExitFlag == false);
        }
    }
}

