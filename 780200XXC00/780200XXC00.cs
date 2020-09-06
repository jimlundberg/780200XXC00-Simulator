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

    /// <summary>
    /// State object for reading client data asynchronously 
    /// </summary>
    public class StateObject
    {
        // Size of receive buffer.  
        public const int BufferSize = 1024;

        // Receive buffer.  
        public byte[] buffer = new byte[BufferSize];

        // Received data string.
        public StringBuilder sb = new StringBuilder();

        // Client socket.
        public Socket workSocket = null;
    }

    /// <summary>
    /// AsynchronousSocketListener class
    /// </summary>
    public class AsynchronousSocketListener
    {
        public static ManualResetEvent allDone = new ManualResetEvent(false); // Thread signal
        private static string ProcessingBufferDirectory = @"C:\SSMCharacterizationHandler\ProcessingBuffer";
        //private static string Job = "1185840_202003250942";
        //private static string Server = "127.0.0.1";
        //private static int Port = 3000;
        //private static int CpuCores = 4;

        /// <summary>
        /// AsynchronousSocketListener default constructor
        /// </summary>
        public AsynchronousSocketListener() { }

        /// <summary>
        /// Start Listening to TCP/IP
        /// </summary>
        public static void StartListening()
        {
            // Establish the local endpoint for the socket
            IPHostEntry ipHostInfo = Dns.GetHostEntry(Dns.GetHostName());
            IPAddress ipAddress = ipHostInfo.AddressList[0];
            IPEndPoint localEndPoint = new IPEndPoint(ipAddress, 11000);

            // Create a TCP/IP socket
            Socket listener = new Socket(ipAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

            // Bind the socket to the local endpoint and listen for incoming connections
            try
            {
                listener.Bind(localEndPoint);
                listener.Listen(100);

                while (true)
                {
                    // Set the event to nonsignaled state
                    allDone.Reset();

                    // Start an asynchronous socket to listen for connections
                    Console.WriteLine("Waiting for a connection...");
                    listener.BeginAccept(new AsyncCallback(AcceptCallback), listener);

                    // Wait until a connection is made before continuing
                    allDone.WaitOne();
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
            }

            Console.WriteLine("\nPress ENTER to continue...");
            Console.Read();
        }

        /// <summary>
        /// Accept TCP/IP Callback
        /// </summary>
        /// <param name="ar"></param>
        public static void AcceptCallback(IAsyncResult ar)
        {
            // Signal the main thread to continue
            allDone.Set();

            // Get the socket that handles the client request
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Create the state object
            StateObject state = new StateObject();
            state.workSocket = handler;
            handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                new AsyncCallback(ReadCallback), state);
        }

        /// <summary>
        /// Read Callback
        /// </summary>
        /// <param name="ar"></param>
        public static void ReadCallback(IAsyncResult ar)
        {
            String content = String.Empty;

            // Retrieve state object and handler socket from the asynchronous state object
            StateObject state = (StateObject)ar.AsyncState;
            Socket handler = state.workSocket;

            // Read data from the client socket
            int bytesRead = handler.EndReceive(ar);

            if (bytesRead > 0)
            {
                // There  might be more data, so store the data received so far
                state.sb.Append(Encoding.ASCII.GetString(state.buffer, 0, bytesRead));

                // Check for end-of-file tag. If it is not there, read more data
                content = state.sb.ToString();
                if (content.IndexOf("<EOF>") > -1)
                {
                    // All the data has been read from the client so display it on the console
                    Console.WriteLine("Read {0} bytes from socket. \n Data : {1}", content.Length, content);

                    // Echo the data back to the client
                    Send(handler, content);
                }
                else
                {
                    // Not all data received. Get more
                    handler.BeginReceive(state.buffer, 0, StateObject.BufferSize, 0,
                        new AsyncCallback(ReadCallback), state);
                }
            }
        }

        private static void Send(Socket handler, String data)
        {
            // Convert the string data to byte data using ASCII encoding
            byte[] byteData = Encoding.ASCII.GetBytes(data);

            // Begin sending the data to the remote device
            handler.BeginSend(byteData, 0, byteData.Length, 0, new AsyncCallback(SendCallback), handler);
        }

        /// <summary>
        /// Send Callback
        /// </summary>
        /// <param name="ar"></param>
        private static void SendCallback(IAsyncResult ar)
        {
            try
            {
                // Retrieve the socket from the state object
                Socket handler = (Socket)ar.AsyncState;

                // Complete sending the data to the remote device
                int bytesSent = handler.EndSend(ar);
                Console.WriteLine("Sent {0} bytes to client.", bytesSent);

                handler.Shutdown(SocketShutdown.Both);
                handler.Close();
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
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

        public static int Main(String[] args)
        {
            StartListening();
            return 0;
        }
    }
}