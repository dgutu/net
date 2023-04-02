using System.Configuration;
using System.IO.Compression;
using Renci.SshNet;

namespace ASHP_FileZip
{
    internal class Program
    {
        static void Main(string[] args)
        {
            // ftp
            string? ftpHost = ConfigurationManager.AppSettings["ftpHost"];
            string? ftpPort = ConfigurationManager.AppSettings["ftpPort"];
            string? ftpUsername = ConfigurationManager.AppSettings["ftpUsername"];
            string? ftpPassword = ConfigurationManager.AppSettings["ftpPassword"];
            string? ftpPdfFolder = ConfigurationManager.AppSettings["ftpPdfFolder"];
            string? ftpXmlFolder = ConfigurationManager.AppSettings["ftpXmlFolder"];

            // xml zip
            string? xmlInput = ConfigurationManager.AppSettings["xmlInput"];
            string? xmlOuput = ConfigurationManager.AppSettings["xmlOutput"];
            string? xmlZipFilename = ConfigurationManager.AppSettings["xmlZipFilename"];
            string fullXmlZipFileName = ZipFolder(xmlInput, xmlOuput, xmlZipFilename, "xml");

            if (!String.IsNullOrEmpty(fullXmlZipFileName))
            {
                FtpUpload(fullXmlZipFileName, ftpHost, ftpPort, ftpUsername, ftpPassword, ftpXmlFolder);
            }

            // pdf zip
            string? pdfInput = ConfigurationManager.AppSettings["pdfInput"];
            string? pdfOuput = ConfigurationManager.AppSettings["pdfOutput"];
            string? pdfZipFilename = ConfigurationManager.AppSettings["pdfZipFilename"];
            string fullPdfZipFileName = ZipFolder(pdfInput, pdfOuput, pdfZipFilename, "pdf");

            if (!String.IsNullOrEmpty(fullPdfZipFileName))
            {
                FtpUpload(fullPdfZipFileName, ftpHost, ftpPort, ftpUsername, ftpPassword, ftpPdfFolder);
            }
        }

        public static string ZipFolder(string? inputDir, string? outputDir, string? outputZipFileName, string fileType)
        {
            string txtPattern = "*.txt";
            string tempDir = String.Format(@"{0}{1}", outputDir, RandomString(30));
            string zipFilename = String.Empty;

            if (String.IsNullOrEmpty(inputDir))
            {
                return zipFilename;
            }


            if (String.IsNullOrEmpty(outputDir))
            {
                return zipFilename;
            }

            if (String.IsNullOrEmpty(outputZipFileName))
            {
                return zipFilename;
            }

            try
            {
                // get the latest filename date
                var inputDirInfo = new DirectoryInfo(inputDir);
                var latestFileName = (from f in inputDirInfo.GetFiles(txtPattern) orderby f.LastWriteTime descending select f).First();
                string latestFileNameDate = GetNumbers(latestFileName.Name);

                zipFilename = String.Format(@"{0}{1}_{2}.zip", outputDir, outputZipFileName, latestFileNameDate);

                // create a temp directory 
                System.IO.Directory.CreateDirectory(tempDir);
                Console.WriteLine(tempDir);

                // copy files to the temp directory
                CopyFilesRecursively(inputDir, tempDir);

                // remove all txt files except the latest filanem
                string[] files = Directory.GetFiles(tempDir, txtPattern);
                foreach (string filePath in files)
                {
                    var name = new FileInfo(filePath).Name;
                    name = name.ToLower();
                    if (!name.Contains(latestFileNameDate))
                    {
                        Console.WriteLine(name + " deleted ...");
                        File.Delete(filePath);
                    }
                }

                // create zip file
                if (!String.IsNullOrEmpty(tempDir))
                {
                    if (File.Exists(zipFilename))
                    {
                        File.Delete(zipFilename);
                    }

                    ZipFile.CreateFromDirectory(tempDir, zipFilename);
                }

                if (IsZipValid(zipFilename))
                {
                    Console.WriteLine(String.Format("output {0} zip file created successfully!", fileType));
                }
                else
                {
                    Console.WriteLine(String.Format("fail to create output {0} zip file!", fileType));
                }

                return zipFilename;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex.Message);
                return String.Empty;
            }
            finally
            {
                // delete temp directory
                Directory.Delete(tempDir, true);
            }
        }

        // check zip file is valid
        public static bool IsZipValid(string path)
        {
            try
            {
                using (var zipFile = ZipFile.OpenRead(path))
                {
                    var entries = zipFile.Entries;
                    return true;
                }
            }
            catch (InvalidDataException)
            {
                return false;
            }
        }

        // copy files from one folder to another
        private static void CopyFilesRecursively(string sourcePath, string targetPath)
        {
            //Now Create all of the directories
            foreach (string dirPath in Directory.GetDirectories(sourcePath, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourcePath, targetPath));
            }

            //Copy all the files & Replaces any files with the same name
            foreach (string newPath in Directory.GetFiles(sourcePath, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourcePath, targetPath), true);
            }
        }

        // generate a random string
        public static string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Repeat(chars, length)
                .Select(s => s[random.Next(s.Length)]).ToArray());
        }

        // get numbers from string
        public static string GetNumbers(string input)
        {
            return new string(input.Where(c => char.IsDigit(c)).ToArray());
        }

        public static void FtpUpload(string? file, string? host, string? port, string? username, string? password, string? ftpFolder)
        {
            if (String.IsNullOrEmpty(file) || String.IsNullOrEmpty(host) || String.IsNullOrEmpty(port) || String.IsNullOrEmpty(username) || String.IsNullOrEmpty(password))
            {
                return;
            }

            using (var client = new SftpClient(host, Int32.Parse(port), username, password))
            {
                client.Connect();
                client.ChangeDirectory(String.Format("/{0}", ftpFolder));
                using (var fileStream = new FileStream(file, FileMode.Open))
                {
                    client.UploadFile(fileStream, Path.GetFileName(file));
                }
                client.Disconnect();
            }

            // Check if the file was uploaded successfully
            using (var client = new SftpClient(host, Int32.Parse(port), username, password))
            {
                client.Connect();
                var fileExists = client.Exists(String.Format("/{0}/{1}/", ftpFolder, Path.GetFileName(file)));
                client.Disconnect();

                if (fileExists)
                {
                    Console.WriteLine("File uploaded successfully!");
                }
                else
                {
                    Console.WriteLine("File upload failed!");
                }
            }
        }
    }
}